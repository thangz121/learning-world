"""WP-1.9.18 — research-only deletion-aware evidence & decision layer.

Does NOT touch production code or thresholds; imports PhoneEvidenceV2 read-only and
replaces only the decision layer for target phones, allowing:

    PRESENT / ABSENT / UNCERTAIN   (no target phone is forced to own a span)

Evidence per target phone (frozen logits, no training):
  - baseline production decision + span (replicated from PhoneEvidenceV2.soft_match)
  - deletion-aware Viterbi: free presence, forced-present span, margin LLR
  - alignment-free support: frame_max, sustained (max of min over adjacent frames),
    count of frames >= 0.3, candidate span
  - GOP-ratio vs competing classes
  - reason codes

Datasets (speaker-disjoint):
  - dev  : speechocean762 train children, first 24 speakers (threshold selection)
  - test : speechocean762 test children, first 24 speakers (held out)
  - ext  : LWE 80 studio tokens (28 blind final labels; never used for selection)
  - human-uncertain: 19 LWE tokens kept separate

Variants: A baseline | B deletion-aware free | C conservative present |
          D conservative + wide UNCERTAIN band (thresholds grid-selected on dev)
"""
from __future__ import annotations

import argparse
import csv
import importlib.util
import json
import math
import re
import sys
import time
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(REPO))
OUT = REPO / "Research/Speech/Phase1_9_18"
ART = OUT / "artifacts"
ART.mkdir(parents=True, exist_ok=True)

from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter  # noqa: E402
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2  # noqa: E402
from Research.Speech.Phase1_4.PhoneInventory.inventory import phone_similarity  # noqa: E402

spec = importlib.util.spec_from_file_location(
    "p2del", REPO / "Research/Speech/Phase1_9_14/experiments/p2_deletion_aware.py")
p2 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p2)

P198 = REPO / "Research/Speech/Phase1_9_8/Results"
P1912 = REPO / "Research/Speech/Phase1_9_12/Results"
P1917 = REPO / "Research/Speech/Phase1_9_17"
SO = Path(r"D:\speech-lab\data\speechocean762")
SO_MANIFEST = REPO / "Research/Speech/Phase1_9_16/artifacts/audit/so762_manifest.csv"
ZEN = REPO / "Research/Speech/ExternalData/zenodo_200495"
DER = ZEN / "derived_16k"
VOWELS = {"ɑ", "æ", "ə", "ɔ", "aʊ", "aɪ", "ɛ", "ɝ", "eɪ", "ɪ", "iː", "oʊ", "ɔɪ", "ʊ", "uː",
          "ʌ", "ɑː", "e", "o", "i", "u", "a", "ɐ", "ɜ", "ɚ", "ɞ"}


def sha256_file(p: Path) -> str:
    import hashlib
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def load_mono16(path: Path) -> Path:
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
        x = np.interp(np.linspace(0, 1, n, endpoint=False),
                      np.linspace(0, 1, len(x), endpoint=False), x).astype(np.float32)
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


def parse_words(utt):
    words = []
    for w in utt.get("words", []):
        ref = (w.get("ref-phones") or "").split()
        experts = w.get("phones") or []
        per = [[] for _ in ref]
        for es in experts:
            j, ok = 0, True
            for t in es.split():
                if t.startswith("[") and t.endswith("]"):
                    continue
                if j >= len(ref):
                    ok = False
                    break
                sc = 0 if (t.startswith("(") and t.endswith(")")) else (
                    1 if (t.startswith("{") and t.endswith("}")) else 2)
                per[j].append(sc)
                j += 1
            if not ok or j != len(ref):
                for k in range(len(ref)):
                    per[k].append("?")
        scores = [round(sum(v) / len(v), 3) if v and all(x != "?" for x in v) else ""
                  for v in per]
        words.append({"ref": ref, "scores": scores})
    return words


def support_stats(p):
    """Alignment-free evidence for one phone class posterior trajectory p (T,)."""
    if len(p) == 0:
        return {"frame_max": 0.0, "sustained": 0.0, "n_ge_03": 0, "best_span": None}
    frame_max = float(p.max())
    if len(p) >= 2:
        pair = np.minimum(p[:-1], p[1:])
        sustained = float(pair.max())
        j = int(pair.argmax())
        best_span = (j, j + 1)
    else:
        sustained = float(p[0])
        best_span = (0, 0)
    return {"frame_max": frame_max, "sustained": sustained,
            "n_ge_03": int((p >= 0.3).sum()), "best_span": best_span}


def baseline_final_decision(probs, spans, target, class_ids, pev, j):
    """Replicate PhoneEvidenceV2.soft_match's decision for target phone j."""
    canon = target[j]
    s, e = spans[j] if j < len(spans) else (0, probs.shape[0] - 1)
    seg = probs[s:e + 1].mean(axis=0)
    top = np.argsort(seg)[-5:][::-1]
    topk = []
    for i in top:
        c = pev.inv.normalize_symbol(pev._id2tok.get(int(i), "?"))
        if c:
            topk.append((c, float(seg[i])))
    ids = class_ids.get(canon, [])
    post = float(seg[ids].sum()) if ids else 0.0
    best_obs = topk[0][0] if topk else ""
    best_sim = phone_similarity(canon, best_obs)
    for obs, pv in topk:
        sim = phone_similarity(canon, obs)
        if sim > best_sim or (sim == best_sim and pv > post):
            best_sim, best_obs = sim, obs
    if best_obs == canon or post >= 0.25:
        match = "exact" if (best_obs == canon or post >= 0.35) else "soft"
        sim = 1.0 if best_obs == canon else max(best_sim, min(1.0, post * 2))
    elif best_sim >= 0.35:
        match = "soft"
        sim = best_sim * (0.5 + 0.5 * post)
    else:
        match = "miss"
        sim = max(best_sim, post) * 0.5
    return {"match": match, "sim": float(sim), "post": post, "best_obs": best_obs,
            "span": (int(s), int(e))}


def analyze_utterance(pev, probs_t, words_phones, target_list, focus_idx, class_ids, meta):
    """Return evidence rows for the focus target phone index (word-final)."""
    probs = probs_t.numpy().astype(np.float64)
    T = probs.shape[0]
    spans = pev.ctc_align(probs_t, target_list)
    emit, names, emit_blank = p2.normalized_emissions(probs, class_ids, pev._blank)
    idx = {n: k for k, n in enumerate(names)}
    emit_all = emit[:, [idx[c] for c in target_list]]
    dm = p2.deletion_margin(emit_all, emit_blank, len(target_list))
    margin = dm["margin"] / max(1, T)
    spans_da = p2.spans_from_path(dm["path_present"], len(target_list))
    canon = target_list[focus_idx]
    ids = class_ids.get(canon, [])
    p = probs[:, ids].sum(axis=1) if ids else np.zeros(T)
    sup = support_stats(p)
    base = baseline_final_decision(probs, spans, target_list, class_ids, pev, focus_idx)
    da_span = spans_da[focus_idx] if spans_da and spans_da[focus_idx] else None
    da_max = float(p[da_span[0]:da_span[1] + 1].max()) if da_span else 0.0
    gop = p2.gop_for_final(probs, target_list, spans_da, class_ids, pev._blank)
    return {
        **meta,
        "target_canon": canon, "n_phones": len(target_list), "n_frames": T,
        "baseline_match": base["match"], "baseline_present": int(base["match"] in ("exact", "soft")),
        "baseline_span": f"{base['span'][0]}-{base['span'][1]}", "baseline_span_post": round(base["post"], 4),
        "baseline_best_obs": base["best_obs"], "baseline_sim": round(base["sim"], 4),
        "da_free_present": int(dm["present_free"]), "da_margin_per_frame": round(margin, 5),
        "da_span": f"{da_span[0]}-{da_span[1]}" if da_span else "", "da_span_max": round(da_max, 4),
        "da_gop_ratio": None if gop["gop_ratio"] is None else round(gop["gop_ratio"], 3),
        "frame_max": round(sup["frame_max"], 4), "sustained": round(sup["sustained"], 4),
        "n_frames_ge_03": sup["n_ge_03"],
        "best_span": f"{sup['best_span'][0]}-{sup['best_span'][1]}" if sup["best_span"] else "",
    }


def run_lwe(pev, class_ids, limit=0):
    tgt = CmuDictTargetAdapter()
    tokens = list(csv.DictReader((P198 / "pronunciation_results.csv").open(encoding="utf-8-sig")))
    if limit:
        tokens = tokens[:limit]
    p1 = {(r["speaker_id"], r["target"]): r for r in csv.DictReader(
        (REPO / "Research/Speech/Phase1_9_14/artifacts/p1/p1_evidence_table.csv").open(encoding="utf-8"))}
    fc = {r["case_id"]: r["human_final_label"] for r in csv.DictReader(
        (P1912 / "final_consonant_human_review.csv").open(encoding="utf-8-sig"))}
    archived = {(r["token_id"].split("_")[1] + "_" + r["token_id"].split("_", 2)[2]):
                r["phone_final_match"] for r in csv.DictReader(
        (P1912 / "final_consonant_human_vs_model.csv").open(encoding="utf-8-sig"))}
    rows = []
    t0 = time.perf_counter()
    mism = 0
    for i, r in enumerate(tokens):
        sid, word = r["speaker_id"], r["target"]
        src = find_source(sid, word)
        if src is None:
            continue
        p16 = load_mono16(src)
        st = tgt.build(word)
        arpa = [str(x) for x in st.arpabet]
        target = pev.inv.arpa_seq_to_canon(arpa)
        words_phones = [(word, target)]
        probs_t, dur, _ = pev.logits(str(p16))
        meta = {"token_id": f"{sid}_{word}", "speaker_id": sid, "word": word,
                "is_final_consonant": int(target[-1] not in VOWELS),
                "human_final_label": fc.get(f"fc_{sid}_{word}", ""),
                "human_verdict": p1.get((sid, word), {}).get("human_verdict", "")}
        ev = analyze_utterance(pev, probs_t, words_phones, target, len(target) - 1,
                               class_ids, meta)
        arch = archived.get(f"{sid}_{word}")
        if arch and arch != ev["baseline_match"]:
            mism += 1
            ev["baseline_repro_mismatch"] = f"{arch}->{ev['baseline_match']}"
        rows.append(ev)
        if (i + 1) % 20 == 0:
            print(f"  lwe {i+1}/{len(tokens)} ({time.perf_counter()-t0:.0f}s)", flush=True)
    print(f"LWE baseline reproduction mismatches vs 1.9.12: {mism}/{len(rows)}", flush=True)
    return rows


def run_so762(pev, class_ids, speakers, tag, max_utt_per_spk=20):
    detail = json.loads((SO / "resource/scores-detail.json").read_text(encoding="utf-8"))
    man = list(csv.DictReader(SO_MANIFEST.open(encoding="utf-8")))
    rows = []
    t0 = time.perf_counter()
    n_utt = 0
    per_spk = Counter()
    for m in man:
        if m["is_child"] != "1" or m["speaker_id"] not in speakers:
            continue
        if per_spk[m["speaker_id"]] >= max_utt_per_spk:
            continue
        per_spk[m["speaker_id"]] += 1
        spk = int(m["speaker_id"])
        wav = SO / "WAVE" / f"SPEAKER{spk:04d}" / f"{m['utt_id']}.WAV"
        words = parse_words(detail[m["utt_id"]])
        ref_flat, final_flags, scores, words_of = [], [], [], []
        for wi, w in enumerate(words):
            for pi, (ph, sc) in enumerate(zip(w["ref"], w["scores"])):
                ref_flat.append(ph)
                final_flags.append(pi == len(w["ref"]) - 1)
                scores.append(sc)
                words_of.append(wi)
        if not ref_flat:
            continue
        target = pev.inv.arpa_seq_to_canon(ref_flat)
        probs_t, dur, _ = pev.logits(str(wav))
        n_utt += 1
        for j, (canon, fin, sc) in enumerate(zip(target, final_flags, scores)):
            if not fin or canon in VOWELS or sc == "":
                continue
            meta = {"token_id": f"{m['utt_id']}_{j}", "speaker_id": m["speaker_id"],
                    "age": m["age"], "is_final_consonant": 1,
                    "human_phone_score": float(sc), "split": tag}
            ev = analyze_utterance(pev, probs_t, None, target, j, class_ids, meta)
            rows.append(ev)
        if n_utt % 40 == 0:
            print(f"  {tag} utt {n_utt} ({time.perf_counter()-t0:.0f}s)", flush=True)
    print(f"{tag}: {n_utt} utt, {len(rows)} final-consonant phones "
          f"({time.perf_counter()-t0:.0f}s)", flush=True)
    return rows


def metrics(rows, truth_key, decision_fn):
    tp = fn = fp = tn = 0
    unc = 0
    unsupported = 0
    for r in rows:
        d = decision_fn(r)
        if d == "UNCERTAIN":
            unc += 1
            continue
        present = (d == "PRESENT")
        if present and r.get("sustained", 0) < 0.1 and r.get("da_margin_per_frame", 0) < 0:
            unsupported += 1
        y = r[truth_key]
        if y == 1:
            tp += present
            fn += (not present)
        else:
            fp += present
            tn += (not present)
    n = len(rows)
    dec = tp + fn + fp + tn
    return {
        "n": n, "present": tp + fn, "absent": fp + tn,
        "present_recall": round(tp / (tp + fn), 4) if (tp + fn) else None,
        "absent_detection": round(tn / (fp + tn), 4) if (fp + tn) else None,
        "frr": round(fn / (tp + fn), 4) if (tp + fn) else None,
        "far": round(fp / (fp + tn), 4) if (fp + tn) else None,
        "unsupported_present_rate": round(unsupported / n, 4) if n else None,
        "uncertain_rate": round(unc / n, 4) if n else None,
        "decision_coverage": round(dec / n, 4) if n else None,
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--smoke", action="store_true")
    args = ap.parse_args()
    pev = PhoneEvidenceV2()
    pev._ensure()
    class_ids = {c: ids for c, ids in pev._canon_ids.items() if c != "sil"}

    # speaker-disjoint dev/test
    man = list(csv.DictReader(SO_MANIFEST.open(encoding="utf-8")))
    n_spk = 2 if args.smoke else 24
    cap = 3 if args.smoke else 20
    dev_spk = sorted({m["speaker_id"] for m in man if m["split"] == "train" and m["is_child"] == "1"})[:n_spk]
    test_spk = sorted({m["speaker_id"] for m in man if m["split"] == "test" and m["is_child"] == "1"})[:n_spk]
    print(f"dev speakers {len(dev_spk)} | test speakers {len(test_spk)} | overlap "
          f"{len(set(dev_spk) & set(test_spk))}", flush=True)

    lwe = run_lwe(pev, class_ids, limit=8 if args.smoke else 0)
    dev = run_so762(pev, class_ids, set(dev_spk), "so762_dev", max_utt_per_spk=cap)
    test = run_so762(pev, class_ids, set(test_spk), "so762_test", max_utt_per_spk=cap)

    with open(ART / "lwe_phone_evidence.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(lwe[0].keys()))
        w.writeheader()
        w.writerows(lwe)
    with open(ART / "so762_dev_finals.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(dev[0].keys()))
        w.writeheader()
        w.writerows(dev)
    with open(ART / "so762_test_finals.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(test[0].keys()))
        w.writeheader()
        w.writerows(test)

    # ground truth views
    def lwe_truth():
        out = []
        for r in lwe:
            hf = r["human_final_label"]
            if hf.endswith("PRESENT") or hf.endswith("PROBABLY_PRESENT"):
                r["truth"] = 1
                out.append(r)
            elif hf.endswith("ABSENT"):
                r["truth"] = 0
                out.append(r)
        return out

    lwe_lab = lwe_truth()
    lwe_uncertain = [r for r in lwe if not r["human_final_label"]]

    def so762_truth(rows, thr=0.5):
        for r in rows:
            s = r["human_phone_score"]
            r["truth"] = 1 if s >= thr else 0
        return rows

    dev_lab = so762_truth(dev)
    test_lab = so762_truth(test)

    # ---- variants ----
    def decide_A(r):
        return "PRESENT" if r["baseline_present"] else "ABSENT"

    def decide_B(r):
        return "PRESENT" if r["da_free_present"] else "ABSENT"

    def make_C(tm, ts):
        def f(r):
            if r["da_margin_per_frame"] >= tm and r["sustained"] >= ts:
                return "PRESENT"
            if r["da_margin_per_frame"] < 0 and r["sustained"] < ts:
                return "ABSENT"
            return "UNCERTAIN"
        return f

    def make_D(tm, tp, ta, tl):
        def f(r):
            if r["da_margin_per_frame"] >= tm and r["sustained"] >= tp:
                return "PRESENT"
            if r["da_margin_per_frame"] <= ta and r["sustained"] < tl:
                return "ABSENT"
            return "UNCERTAIN"
        return f

    # dev grid for variant C (single support threshold)
    grid = []
    for tm in (-0.10, -0.05, -0.02, 0.0, 0.02, 0.05):
        for ts in (0.05, 0.10, 0.20, 0.30, 0.50):
            m = metrics(dev_lab, "truth", make_C(tm, ts))
            grid.append({"variant": "C", "tm": tm, "ts": ts, **m})
    gridD = []
    for tm in (0.0, 0.02, 0.05):
        for tp in (0.20, 0.30, 0.50):
            for ta in (-0.10, -0.05, -0.02):
                for tl in (0.05, 0.10, 0.20):
                    m = metrics(dev_lab, "truth", make_D(tm, tp, ta, tl))
                    gridD.append({"variant": "D", "tm": tm, "tp": tp, "ta": ta, "tl": tl, **m})

    def pick(grid, min_recall=0.95, min_cov=0.80):
        ok = [g for g in grid if (g["present_recall"] or 0) >= min_recall
              and (g["decision_coverage"] or 0) >= min_cov]
        if not ok:
            ok = [g for g in grid if (g["present_recall"] or 0) >= min_recall]
        return max(ok, key=lambda g: (g["absent_detection"] or 0, g["decision_coverage"] or 0)) if ok else None

    fallbackC = {"variant": "C", "tm": -0.02, "ts": 0.20, "fallback": True}
    fallbackD = {"variant": "D", "tm": 0.0, "tp": 0.30, "ta": -0.02, "tl": 0.10,
                 "fallback": True}
    bestC = pick(grid, 0.95, 0.80) or pick(grid, 0.90, 0.60) or fallbackC
    bestD = pick(gridD, 0.95, 0.60) or pick(gridD, 0.90, 0.40) or fallbackD

    cf = make_C(bestC["tm"], bestC["ts"])
    df = make_D(bestD["tm"], bestD["tp"], bestD["ta"], bestD["tl"])

    variants = {"A_baseline": decide_A, "B_delaware_free": decide_B,
                "C_conservative": cf, "D_wide_uncertain": df}

    results = {"phase": "1.9.18",
               "research_question": "Can PRESENT/ABSENT/UNCERTAIN for target phones without "
                                    "forcing spans improve final-consonant decisions under "
                                    "speaker-disjoint FRR-first evaluation?",
               "datasets": {
                   "dev": {"dataset": "so762 train children", "speakers": len(dev_spk),
                           "utterances": len({r["token_id"].rsplit("_", 1)[0] for r in dev}),
                           "phones": len(dev_lab),
                           "present": sum(r["truth"] for r in dev_lab)},
                   "test": {"dataset": "so762 test children", "speakers": len(test_spk),
                            "utterances": len({r["token_id"].rsplit("_", 1)[0] for r in test}),
                            "phones": len(test_lab),
                            "present": sum(r["truth"] for r in test_lab)},
                   "external": {"dataset": "LWE real-child (blind 1.9.12 finals)",
                                "phones": len(lwe_lab),
                                "present": sum(r["truth"] for r in lwe_lab)},
               },
               "selected_thresholds": {"C": bestC, "D": bestD},
               "results": {}, "flags": {"production_vad": False, "router_locked": False,
                                        "unity_integrated": False, "scorer_modified": False,
                                        "production_window_locked": False}}
    for name, fn in variants.items():
        results["results"][name] = {
            "dev": metrics(dev_lab, "truth", fn),
            "test_speaker_disjoint": metrics(test_lab, "truth", fn),
            "lwe_external": metrics(lwe_lab, "truth", fn),
        }
    results["uncertain_human_cases"] = {
        "n": len(lwe_uncertain),
        "decisions": {name: dict(Counter(fn(r) for r in lwe_uncertain))
                      for name, fn in variants.items()},
        "note": "human word-verdict uncertain; no final labels; never used as ground truth",
    }
    results["sweep_dev_C"] = grid
    results["sweep_dev_D"] = gridD
    (OUT / "EXPERIMENT_RESULTS.json").write_text(json.dumps(results, indent=2), encoding="utf-8")

    print(json.dumps({k: results["results"][k] for k in variants}, indent=2)[:4500])


if __name__ == "__main__":
    main()
