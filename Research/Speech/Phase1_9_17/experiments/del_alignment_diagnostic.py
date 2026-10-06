"""Phase 1.9.17 — word-final deletion/alignment/scoring diagnostic (RESEARCH ONLY).

No production code change, no training, no commit. Reconstructs, for the known
failure corpus (12 PHONE_MODEL_ERROR + 6 SCORER_MISS + 28 final-consonant human
labels from phase 1.9.12), the full evidence chain:

  frozen logits -> baseline forced alignment (ctc_forced_v1) -> soft_match score/decision
  frozen logits -> deletion-aware blank-interleaved Viterbi -> presence margin/GOP
  frame-level evidence -> counterfactual scoring rules

Classification: ACOUSTIC / ALIGNMENT / DELETION / SCORING / THRESHOLD / MIXED /
UNKNOWN (+ ASSESSABILITY_FAILURE kept separate).

Outputs (Phase 1.9.17 artifacts):
  artifacts/case_evidence.csv          all 80 studio tokens with frame evidence
  FAILURE_CASE_ANALYSIS.csv            failure rows + classification
  EXPERIMENT_RESULTS.json              metrics / aggregation / root-cause / gates
"""
from __future__ import annotations

import csv
import importlib.util
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
HERE = Path(__file__).resolve().parent
OUT = REPO / "Research/Speech/Phase1_9_17"
ART = OUT / "artifacts"
ART.mkdir(parents=True, exist_ok=True)

from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter  # noqa: E402
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2  # noqa: E402

# reuse the validated deletion-aware Viterbi from 1.9.14 (research module)
spec = importlib.util.spec_from_file_location(
    "p2del", REPO / "Research/Speech/Phase1_9_14/experiments/p2_deletion_aware.py")
p2 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p2)

P198 = REPO / "Research/Speech/Phase1_9_8/Results"
P1910 = REPO / "Research/Speech/Phase1_9_10/Results"
P1911 = REPO / "Research/Speech/Phase1_9_11/Results"
P1912 = REPO / "Research/Speech/Phase1_9_12/Results"
P1914 = REPO / "Research/Speech/Phase1_9_14/artifacts/p1/p1_evidence_table.csv"
ZEN = REPO / "Research/Speech/ExternalData/zenodo_200495"
DER = ZEN / "derived_16k"

REFUSAL_STATES = {"NO_SPEECH", "UNINTELLIGIBLE", "FREE_SPEAK", "INCOMPLETE"}
VOWELS = {"ɑ", "æ", "ə", "ɔ", "aʊ", "aɪ", "ɛ", "ɝ", "eɪ", "ɪ", "iː", "oʊ", "ɔɪ", "ʊ", "uː",
          "ʌ", "ɑː", "e", "o", "i", "u", "a", "ɐ", "ɜ", "ɚ", "ɞ"}


def rows(p: Path):
    return list(csv.DictReader(p.open(encoding="utf-8-sig")))


def sha256_file(p: Path) -> str:
    import hashlib
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def load_mono16(path: Path):
    key = sha256_file(path)[:16] + "_" + re.sub(r"[^A-Za-z0-9_.-]+", "_", path.name)[:60]
    cache = DER / f"{key}.wav"
    if cache.exists():
        return cache
    x, sr = sf.read(str(path))
    if x.ndim > 1:
        x = x.mean(axis=1)
    x = x.astype(np.float32)
    if sr != 16000:
        n = int(len(x) * 16000 / sr)
        xs = np.linspace(0, 1, len(x), endpoint=False)
        xt = np.linspace(0, 1, n, endpoint=False)
        x = np.interp(xt, xs, x).astype(np.float32)
        sr = 16000
    DER.mkdir(parents=True, exist_ok=True)
    sf.write(str(cache), x, sr)
    return cache


def find_source(speaker_id, target):
    m = re.match(r"child_(\d+)", speaker_id or "")
    if not m:
        return None
    nn = m.group(1)
    base = ZEN / "extracted/english_children/english_words_sentences"
    for sp in base.iterdir():
        if sp.name.startswith(nn + "_"):
            for mic in ("studio_mic", "port_mic", "nao_mic"):
                c = sp / mic / "numbers" / f"{target}.wav"
                if c.exists():
                    return c
            hits = list(sp.rglob(f"numbers/{target}.wav"))
            if hits:
                return hits[0]
    return None


def fnum(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def main():
    tokens = rows(P198 / "pronunciation_results.csv")           # 80, defines tok_XX order
    p1 = {(r["speaker_id"], r["target"]): r for r in rows(P1914)}
    fc = {r["case_id"]: r["human_final_label"]
          for r in rows(P1912 / "final_consonant_human_review.csv")}
    pme = {(r["speaker_id"], r["target"]): r for r in rows(P1910 / "phone_model_error_cases.csv")}
    sm = {(r["speaker_id"], r["target"]): r for r in rows(P1911 / "scorer_miss_human_review.csv")}
    win = {f"tok_{i+1:02d}": r for i, r in enumerate(rows(P1911 / "window_ab_80_tokens.csv"))}

    pev = PhoneEvidenceV2()
    pev._ensure()
    class_ids = {c: ids for c, ids in pev._canon_ids.items() if c != "sil"}
    tgt = CmuDictTargetAdapter()

    case_rows = []
    for i, r in enumerate(tokens):
        sid, word = r["speaker_id"], r["target"]
        src = find_source(sid, word)
        if src is None:
            print("MISSING_AUDIO", sid, word, flush=True)
            continue
        path16 = load_mono16(src)
        st = tgt.build(word)
        arpa = [str(p) for p in st.arpabet]
        target = pev.inv.arpa_seq_to_canon(arpa)
        final_arpa, final_canon = arpa[-1], target[-1]
        probs_t, dur, _ = pev.logits(str(path16))
        probs = probs_t.numpy().astype(np.float64)
        T = probs.shape[0]

        smr = pev.soft_match(str(path16), arpa)
        last = smr.hits[-1] if smr.hits else None
        base_match = last.match_type if last else ""
        base_present = 1 if base_match in ("exact", "soft") else 0
        base_post = last.posterior if last else None
        base_sim = last.sim if last else None
        base_best_obs = last.best_obs if last else ""

        spans = pev.ctc_align(probs_t, target)
        sf0, sf1 = (spans[-1] if spans else (0, T - 1))
        span_slice = probs[sf0:sf1 + 1]
        ids_final = class_ids.get(final_canon, [])
        span_post = span_slice[:, ids_final].sum(axis=1) if ids_final else np.zeros(1)
        span_max = float(span_post.max()) if len(span_post) else 0.0
        span_mean = float(span_post.mean()) if len(span_post) else 0.0

        tail_n = min(15, max(5, int(round(0.25 * T))))
        tail = probs[-tail_n:]
        tail_post = tail[:, ids_final].sum(axis=1) if ids_final else np.zeros(1)
        rec_tail_max = float(tail_post.max())
        rec_tail_mean = float(tail_post.mean())

        greedy = pev.greedy_phones(probs_t)
        greedy_canons = [g[0] for g in greedy]
        greedy_has_final = final_canon in greedy_canons
        greedy_last = greedy_canons[-1] if greedy_canons else ""

        emit, names, emit_blank = p2.normalized_emissions(probs, class_ids, pev._blank)
        idx = {n: k for k, n in enumerate(names)}
        emit_all = emit[:, [idx[c] for c in target]]
        dm = p2.deletion_margin(emit_all, emit_blank, len(target))
        margin_per_frame = dm["margin"] / max(1, T)
        spans_da = p2.spans_from_path(dm["path_present"], len(target))
        da_last = spans_da[-1] if spans_da else None
        if da_last:
            da_slice = probs[da_last[0]:da_last[1] + 1]
            da_post = da_slice[:, ids_final].sum(axis=1) if ids_final else np.zeros(1)
            da_span_max = float(da_post.max())
            da_span_mean = float(da_post.mean())
        else:
            da_span_max = da_span_mean = 0.0
        gop = p2.gop_for_final(probs, target, spans_da, class_ids, pev._blank)

        if (margin_per_frame >= 0.02 or da_span_max >= 0.25 or greedy_has_final):
            evidence = "STRONG"
        elif (margin_per_frame > -0.05 and (da_span_max >= 0.10 or rec_tail_max >= 0.10)):
            evidence = "WEAK"
        else:
            evidence = "NONE"

        key = (sid, word)
        p1row = p1.get(key, {})
        winrow = win.get(f"tok_{i+1:02d}", {})
        hfl = fc.get(f"fc_{sid}_{word}", "")
        human_present = 1 if hfl in ("FINAL_CONSONANT_CLEARLY_PRESENT",
                                     "FINAL_CONSONANT_PROBABLY_PRESENT") else (
            0 if hfl in ("FINAL_CONSONANT_CLEARLY_ABSENT",
                         "FINAL_CONSONANT_PROBABLY_ABSENT") else "")
        group = "PME" if key in pme else ("SCORER_MISS" if key in sm else "")
        case_rows.append({
            "case_id": f"{sid}_{word}", "tok": f"tok_{i+1:02d}", "speaker_id": sid, "word": word,
            "group": group, "final_phone": final_canon, "final_arpabet": final_arpa,
            "is_final_consonant": int(final_canon not in VOWELS),
            "human_final_label": hfl, "human_present": human_present,
            "human_verdict": p1row.get("human_verdict", ""),
            "human_class": p1row.get("human_class", ""),
            "assessability_v2": p1row.get("assessability_v2", ""),
            "fidelity_state_v2": p1row.get("fidelity_state_v2", ""),
            "human_audio_quality": p1row.get("human_audio_quality", ""),
            "baseline_soft": smr.soft_score_0_100, "baseline_conf": smr.confidence_0_1,
            "baseline_final_match": base_match, "baseline_final_present": base_present,
            "baseline_final_posterior": base_post, "baseline_final_sim": base_sim,
            "baseline_final_observed": base_best_obs,
            "span_start": sf0, "span_end": sf1, "span_frames": sf1 - sf0 + 1,
            "span_max_post": round(span_max, 4), "span_mean_post": round(span_mean, 4),
            "rec_tail_max_post": round(rec_tail_max, 4), "rec_tail_mean_post": round(rec_tail_mean, 4),
            "greedy_has_final": int(greedy_has_final), "greedy_last_phone": greedy_last,
            "n_frames": T, "dur_s": round(dur, 3),
            "da_present_free": int(dm["present_free"]), "da_margin": round(dm["margin"], 3),
            "da_margin_per_frame": round(margin_per_frame, 5),
            "da_span_max_post": round(da_span_max, 4), "da_span_mean_post": round(da_span_mean, 4),
            "da_gop_ratio": None if gop["gop_ratio"] is None else round(gop["gop_ratio"], 3),
            "evidence_class": evidence,
            "window_full": winrow.get("full_score", ""), "window_raw": winrow.get("raw_vad_score", ""),
            "window_pad250": winrow.get("pad250_score", ""),
        })
        print(f"[{i+1}/80] {sid}_{word} {final_arpa} human={hfl or '-'} "
              f"base={base_match} ev={evidence} margin/f={margin_per_frame:.3f}", flush=True)

    fields = list(case_rows[0].keys())
    with open(ART / "case_evidence.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(case_rows)

    # ---------------- classification (Phase F) ----------------
    def classify(r):
        hp, bp = r["human_present"], r["baseline_final_present"]
        if r["assessability_v2"] in REFUSAL_STATES or r["human_verdict"] in (
                "HUMAN_UNCERTAIN", "HUMAN_CONFLICTED", "AMBIGUOUS"):
            return "HUMAN_UNCERTAIN_OR_ASSESSABILITY", "human verdict uncertain/assessability"
        if not r["is_final_consonant"]:
            return "NON_FINAL_CONSONANT_CASE", "final expected phone is a vowel"
        if hp == "":
            return "UNKNOWN", "no human final consonant label"
        if hp == 1 and bp == 1:
            return "OK", "human present, baseline accepted"
        if hp == 0 and bp == 0:
            return "OK", "human absent, baseline rejected"
        ev = r["evidence_class"]
        soft = fnum(r["baseline_soft"]) or 0
        if hp == 1 and bp == 0:
            if ev == "STRONG":
                if r["da_span_max_post"] >= 0.15:
                    return ("THRESHOLD" if 40 <= soft < 50 else "SCORING",
                            f"evidence in aligned span (da_span_max={r['da_span_max_post']}), "
                            f"score {soft}")
                return "ALIGNMENT", "evidence at greedy/tail but not in aligned span"
            if ev == "WEAK":
                return "MIXED", "weak evidence, present phone rejected"
            return "ACOUSTIC", "no encoder evidence for the present phone"
        if hp == 0 and bp == 1:
            if ev == "NONE":
                return "DELETION", "absent phone accepted without acoustic support"
            if ev == "WEAK":
                return "ALIGNMENT", "weak/ambiguous evidence assigned to absent phone"
            return "MIXED", "strong evidence for a phone the human rates absent"
        return "UNKNOWN", "unclassified"

    failure_rows = []
    for r in case_rows:
        cls, reason = classify(r)
        r["classification"] = cls
        r["classification_reason"] = reason
        is_failure = cls in ("ACOUSTIC", "ALIGNMENT", "DELETION", "SCORING", "THRESHOLD",
                             "MIXED")
        if is_failure or r["group"] in ("PME", "SCORER_MISS") or r["human_present"] != "":
            failure_rows.append(r)
    with open(OUT / "FAILURE_CASE_ANALYSIS.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields + ["classification", "classification_reason"])
        w.writeheader()
        w.writerows(failure_rows)

    # ---------------- counterfactual scoring (Phase D) ----------------
    lab = [r for r in case_rows if r["human_present"] != "" and r["is_final_consonant"]]
    n_present = sum(r["human_present"] for r in lab)
    n_absent = len(lab) - n_present

    def cf_metrics(pred_fn):
        tp = sum(1 for r in lab if pred_fn(r) and r["human_present"] == 1)
        fn = sum(1 for r in lab if not pred_fn(r) and r["human_present"] == 1)
        fp = sum(1 for r in lab if pred_fn(r) and r["human_present"] == 0)
        tn = sum(1 for r in lab if not pred_fn(r) and r["human_present"] == 0)
        return {"present_recall": tp / n_present if n_present else None,
                "absent_detection": tn / n_absent if n_absent else None,
                "tp": tp, "fn": fn, "fp": fp, "tn": tn}

    baseline_cf = {"present_recall": sum(1 for r in lab if r["baseline_final_present"]
                                         and r["human_present"] == 1) / n_present,
                   "absent_detection": sum(1 for r in lab if not r["baseline_final_present"]
                                           and r["human_present"] == 0) / n_absent}
    sweeps = {"cf_tail_post": [], "cf_margin_per_frame": [], "cf_hybrid": []}
    for th in np.arange(0.02, 0.61, 0.02).round(3):
        sweeps["cf_tail_post"].append(dict(threshold=float(th),
                                           **cf_metrics(lambda r, th=th: r["rec_tail_max_post"] >= th)))
    for th in np.arange(-0.10, 0.101, 0.01).round(4):
        sweeps["cf_margin_per_frame"].append(dict(threshold=float(th),
                                                  **cf_metrics(lambda r, th=th: r["da_margin_per_frame"] >= th)))
    for th in np.arange(0.05, 0.41, 0.05).round(3):
        sweeps["cf_hybrid"].append(dict(threshold=float(th), **cf_metrics(
            lambda r, th=th: (r["greedy_has_final"] or r["da_span_max_post"] >= th)
            and r["da_margin_per_frame"] >= -0.05)))
    cf_free = cf_metrics(lambda r: r["da_present_free"] == 1)

    def best_of(sweep, key):
        ok = [s for s in sweep if s[key] is not None]
        return max(ok, key=lambda s: (s["present_recall"] + s["absent_detection"]) / 2) if ok else None

    counterfactual = {
        "n_labeled_consonant_finals": len(lab), "n_present": n_present, "n_absent": n_absent,
        "baseline": baseline_cf,
        "deletion_aware_free_presence": cf_free,
        "cf_tail_post_best_balanced": best_of(sweeps["cf_tail_post"], "present_recall"),
        "cf_margin_best_balanced": best_of(sweeps["cf_margin_per_frame"], "present_recall"),
        "cf_hybrid_best_balanced": best_of(sweeps["cf_hybrid"], "present_recall"),
        "sweeps": sweeps,
    }

    # ---------------- aggregation (Phase E) ----------------
    def agg(keyfn):
        out = {}
        for r in lab:
            k = keyfn(r)
            d = out.setdefault(k, {"n": 0, "present": 0, "absent": 0,
                                   "base_recall": 0, "base_absent_det": 0,
                                   "da_free_present": 0})
            d["n"] += 1
            if r["human_present"] == 1:
                d["present"] += 1
                d["base_recall"] += r["baseline_final_present"]
                d["da_free_present"] += r["da_present_free"]
            else:
                d["absent"] += 1
                d["base_absent_det"] += 1 - r["baseline_final_present"]
        for k, d in out.items():
            d["base_recall"] = round(d["base_recall"] / d["present"], 3) if d["present"] else None
            d["base_absent_det"] = round(d["base_absent_det"] / d["absent"], 3) if d["absent"] else None
            d["da_free_present_rate"] = round(d["da_free_present"] / d["present"], 3) if d["present"] else None
        return out

    aggregation = {
        "by_phone": agg(lambda r: r["final_phone"]),
        "by_word": agg(lambda r: r["word"]),
        "note": "subgroups with n<5 are reported but must not be used for claims",
    }

    # ---------------- root cause distribution (Phase F) ----------------
    failures = [r for r in failure_rows if r["classification"] in
                ("ACOUSTIC", "ALIGNMENT", "DELETION", "SCORING", "THRESHOLD", "MIXED")]
    dist = Counter(r["classification"] for r in failures)
    rootcause = {"n_failures": len(failures), "distribution": dict(dist),
                 "percentages": {k: round(100 * v / max(1, len(failures)), 1)
                                 for k, v in dist.items()},
                 "assessability": {
                     "n": sum(1 for r in failure_rows
                              if r["classification"] == "HUMAN_UNCERTAIN_OR_ASSESSABILITY"),
                     "ids": [r["case_id"] for r in failure_rows
                             if r["classification"] == "HUMAN_UNCERTAIN_OR_ASSESSABILITY"]},
                 "by_group": {g: dict(Counter(r["classification"] for r in failures
                                              if r["group"] == g))
                              for g in ("PME", "SCORER_MISS")}}

    # ---------------- window dependence + assessability cross-check ----------------
    window_dep = [
        {"case_id": r["case_id"], "full": r["window_full"], "raw": r["window_raw"],
         "pad250": r["window_pad250"]}
        for r in case_rows
        if fnum(r["window_full"]) is not None and fnum(r["window_raw"]) is not None
        and ((fnum(r["window_full"]) < 50 <= fnum(r["window_raw"])) or
             (fnum(r["window_raw"]) < 50 <= fnum(r["window_full"])))]
    assessability_flagged = [r["case_id"] for r in case_rows
                             if r["fidelity_state_v2"] in REFUSAL_STATES]

    results = {
        "phase": "1.9.17", "objective": "word-final deletion/alignment/scoring diagnosis",
        "model": {"id": "facebook/wav2vec2-xlsr-53-espeak-cv-ft",
                  "revision": "2c733782da5604684829819a5eb744c193fe9398",
                  "pipeline": "PhoneEvidenceV2@1.4.0 ctc_forced_v1 + soft_match (frozen)"},
        "counts": {"tokens_analyzed": len(case_rows),
                   "human_final_labels": sum(1 for r in case_rows if r["human_present"] != ""),
                   "pme": sum(1 for r in case_rows if r["group"] == "PME"),
                   "scorer_miss": sum(1 for r in case_rows if r["group"] == "SCORER_MISS")},
        "counterfactual": counterfactual,
        "aggregation": aggregation,
        "root_cause": rootcause,
        "window_dependence": {"n": len(window_dep), "cases": window_dep},
        "assessability": {"refusal_states_in_corpus": assessability_flagged,
                          "independent_review_note": "failure tokens are disjoint from the "
                                                     "1.9.15 115-item independent review"},
        "flags": {"production_vad": False, "router_locked": False, "unity_integrated": False,
                  "scorer_modified": False, "production_window_locked": False},
    }
    (OUT / "EXPERIMENT_RESULTS.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(json.dumps({"root_cause": rootcause, "baseline_cf": baseline_cf,
                      "cf_free": cf_free}, indent=2))


if __name__ == "__main__":
    main()
