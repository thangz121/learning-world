"""Phase 1.9.16 — Experiments D/E/F/G/H/I: A/B evaluation of the child-adapted head.

SAME AUDIO -> current frozen phone evidence vs child-adapted evidence -> SAME
soft-v2 alignment/scoring logic (PhoneEvidenceV2.soft_match is reused verbatim;
only the frame posteriors come from the adapted head). No formula change.

Targets / evaluation sets:
  - LWE real-child tokens (48 human verdicts + 28 final-consonant labels) = EXTERNAL
  - SIAK test speakers (493) + ages 4-6 external (99) = speaker-disjoint
  - speechocean762 test children (1,280, 64 speakers) with 5-expert phone scores

Outputs: artifacts/ab/*.csv, ab_metrics.json
"""
from __future__ import annotations

import argparse
import csv
import json
import math
import re
import sys
import time
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
import soundfile as sf
import torch

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter  # noqa: E402
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2  # noqa: E402

SO = Path(r"D:\speech-lab\data\speechocean762")
ZEN = REPO / "Research/Speech/ExternalData/zenodo_200495"
SIAK = REPO / "Research/Speech/ExternalData/SIAK"
AB = REPO / "Research/Speech/Phase1_9_16/artifacts/ab"
AUDIT = REPO / "Research/Speech/Phase1_9_16/artifacts/audit"
AB.mkdir(parents=True, exist_ok=True)
P1914_P1 = REPO / "Research/Speech/Phase1_9_14/artifacts/p1/p1_evidence_table.csv"
P1912_FC = REPO / "Research/Speech/Phase1_9_12/Results/final_consonant_human_review.csv"
SI_TEST = REPO / "Research/Speech/Phase1_9_15/artifacts/siak/calibration_test.csv"
SI_46 = REPO / "Research/Speech/Phase1_9_15/artifacts/siak/calibration_ages46.csv"


class AdaptedPEV(PhoneEvidenceV2):
    """Frozen encoder + trained head replaces the CTC logits; everything else identical."""

    def __init__(self, head_path: Path):
        super().__init__()
        self._ensure()
        ck = torch.load(head_path, weights_only=False)
        cfg = ck["config"]
        from train_head import CTCHead
        self.head = CTCHead(vocab=ck["vocab"], hidden=ck.get("hidden", 512)).eval()
        self.head.load_state_dict(ck["state_dict"])

    def logits(self, wav_path: str):
        audio, sr = sf.read(str(wav_path))
        if audio.ndim > 1:
            audio = audio.mean(axis=1)
        audio = audio.astype(np.float32)
        if sr != 16000:
            raise ValueError(f"need 16k, got {sr}")
        with torch.no_grad():
            inp = self._feat(audio, sampling_rate=16000, return_tensors="pt").input_values
            h = self._model.wav2vec2(inp).last_hidden_state
            logits = self.head(h)[0]
        probs = torch.softmax(logits, dim=-1)
        dur = len(audio) / sr
        return probs, dur, len(audio)


def num(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def load_mono16(path: Path):
    x, sr = sf.read(str(path))
    if x.ndim > 1:
        x = x.mean(axis=1)
    return x.astype(np.float32), int(sr)


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


def pearson(xs, ys):
    if len(xs) < 3:
        return None
    mx, my = sum(xs) / len(xs), sum(ys) / len(ys)
    num_ = sum((a - mx) * (b - my) for a, b in zip(xs, ys))
    dx = math.sqrt(sum((a - mx) ** 2 for a in xs))
    dy = math.sqrt(sum((b - my) ** 2 for b in ys))
    return num_ / (dx * dy) if dx and dy else None


def rank(v):
    order = sorted(range(len(v)), key=lambda i: v[i])
    r = [0.0] * len(v)
    i = 0
    while i < len(order):
        j = i
        while j + 1 < len(order) and v[order[j + 1]] == v[order[i]]:
            j += 1
        a = (i + j) / 2 + 1
        for k in range(i, j + 1):
            r[order[k]] = a
        i = j + 1
    return r


def spearman(xs, ys):
    return pearson(rank(xs), rank(ys)) if len(xs) >= 3 else None


def auc_bin(pos, neg):
    if not pos or not neg:
        return None
    wins = sum(1.0 if a > b else (0.5 if a == b else 0.0) for a in pos for b in neg)
    return wins / (len(pos) * len(neg))


def frr_far(rows, field):
    cor = [r[field] for r in rows if r["human_class"] == "HUMAN_CORRECT"]
    inc = [r[field] for r in rows if r["human_class"] == "HUMAN_INCORRECT"]
    return {"frr_lt50": float(np.mean([v < 50 for v in cor])) if cor else None,
            "far_ge50": float(np.mean([v >= 50 for v in inc])) if inc else None,
            "n_correct": len(cor), "n_incorrect": len(inc)}


def run_lwe(pev, apev, out):
    base = list(csv.DictReader(P1914_P1.open(encoding="utf-8")))
    fc = {r["case_id"]: r for r in csv.DictReader(P1912_FC.open(encoding="utf-8"))}
    tgt = CmuDictTargetAdapter()
    rows = []
    t0 = time.perf_counter()
    for i, r in enumerate(base):
        src = find_source(r["speaker_id"], r["target"])
        if src is None:
            continue
        st = tgt.build(r["target"])
        x, sr = load_mono16(src)
        tmp = AB / "_tmp16.wav"
        if sr != 16000:
            n = int(len(x) * 16000 / sr)
            x16 = np.interp(np.linspace(0, 1, n, endpoint=False),
                            np.linspace(0, 1, len(x), endpoint=False), x).astype(np.float32)
        else:
            x16 = x
        sf.write(str(tmp), x16, 16000)
        sm_b = pev.soft_match(str(tmp), st.arpabet)
        sm_a = apev.soft_match(str(tmp), st.arpabet)
        last_b = sm_b.hits[-1] if sm_b.hits else None
        last_a = sm_a.hits[-1] if sm_a.hits else None
        cid = f"fc_{r['speaker_id']}_{r['target']}"
        rows.append({
            "case_id": f"{r['speaker_id']}_{r['target']}",
            "human_class": r["human_class"], "human_verdict": r["human_verdict"],
            "assessability_v2": r["assessability_v2"],
            "baseline_soft": sm_b.soft_score_0_100, "adapted_soft": sm_a.soft_score_0_100,
            "baseline_conf": sm_b.confidence_0_1, "adapted_conf": sm_a.confidence_0_1,
            "baseline_final_match": last_b.match_type if last_b else "",
            "adapted_final_match": last_a.match_type if last_a else "",
            "fc_human_label": fc.get(cid, {}).get("human_final_label", ""),
        })
        if (i + 1) % 20 == 0:
            print(f"  lwe {i+1}/{len(base)} ({time.perf_counter()-t0:.0f}s)", flush=True)
    tmp = AB / "_tmp16.wav"
    if tmp.exists():
        tmp.unlink()
    with open(out, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)
    lab = [r for r in rows if r["human_class"]]
    fc_rows = [r for r in rows if r["fc_human_label"]]
    res = {
        "n_labeled": len(lab),
        "baseline": frr_far(lab, "baseline_soft"), "adapted": frr_far(lab, "adapted_soft"),
        "auc_baseline": auc_bin([r["baseline_soft"] for r in lab if r["human_class"] == "HUMAN_CORRECT"],
                                [r["baseline_soft"] for r in lab if r["human_class"] == "HUMAN_INCORRECT"]),
        "auc_adapted": auc_bin([r["adapted_soft"] for r in lab if r["human_class"] == "HUMAN_CORRECT"],
                               [r["adapted_soft"] for r in lab if r["human_class"] == "HUMAN_INCORRECT"]),
        "final_consonant": {
            "n": len(fc_rows),
            "baseline_present_recall": float(np.mean([r["baseline_final_match"] in ("exact", "soft")
                                                      for r in fc_rows if "PRESENT" in r["fc_human_label"]])),
            "adapted_present_recall": float(np.mean([r["adapted_final_match"] in ("exact", "soft")
                                                     for r in fc_rows if "PRESENT" in r["fc_human_label"]])),
            "baseline_absent_detection": float(np.mean([r["baseline_final_match"] == "miss"
                                                        for r in fc_rows if "ABSENT" in r["fc_human_label"]])),
            "adapted_absent_detection": float(np.mean([r["adapted_final_match"] == "miss"
                                                       for r in fc_rows if "ABSENT" in r["fc_human_label"]])),
        },
        "note": "threshold 50 on soft score; single reviewer labels; adapted head trained "
                "without LWE data",
    }
    print(json.dumps(res, indent=2))
    return res


def run_siak(pev, apev, split_csv, tag, out):
    rows_in = list(csv.DictReader(split_csv.open(encoding="utf-8")))
    idx = {p.name: p for p in (SIAK / "flac").rglob("*.flac")}
    tgt = CmuDictTargetAdapter()
    rows = []
    t0 = time.perf_counter()
    for i, r in enumerate(rows_in):
        p = idx.get(r["file"])
        if p is None:
            continue
        st = tgt.build(r["utterance"])
        sm_b = pev.soft_match(str(p), st.arpabet)
        sm_a = apev.soft_match(str(p), st.arpabet)
        rows.append({"file": r["file"], "speaker_id": r["speaker_id"], "age": int(r["age"]),
                     "siak_score": float(r["siak_score"]),
                     "baseline_soft": sm_b.soft_score_0_100, "adapted_soft": sm_a.soft_score_0_100,
                     "baseline_conf": sm_b.confidence_0_1, "adapted_conf": sm_a.confidence_0_1})
        if (i + 1) % 100 == 0:
            print(f"  {tag} {i+1}/{len(rows_in)} ({time.perf_counter()-t0:.0f}s)", flush=True)
    with open(out, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)
    sc = [r["siak_score"] for r in rows]

    def block(field):
        v = [r[field] for r in rows]
        good = [r for r in rows if r["siak_score"] >= 80]
        uncorr = [r for r in rows if r["siak_score"] < 50]
        return {"pearson": pearson(sc, v), "spearman": spearman(sc, v),
                "mae": float(np.mean([abs(a - b) for a, b in zip(v, sc)])),
                "frr_proxy_lt50": float(np.mean([r[field] < 50 for r in good])) if good else None,
                "far_proxy_ge80": float(np.mean([r[field] >= 80 for r in uncorr])) if uncorr else None}

    res = {"n": len(rows), "baseline": block("baseline_soft"), "adapted": block("adapted_soft")}
    print(tag, json.dumps(res, indent=2))
    return res


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
        scores = [round(sum(v) / len(v), 3) if v and all(x != "?" for x in v) else "" for v in per]
        words.append({"ref": ref, "scores": scores})
    return words


def run_so762(pev, apev, out):
    detail = json.loads((SO / "resource/scores-detail.json").read_text(encoding="utf-8"))
    man = list(csv.DictReader((AUDIT / "so762_manifest.csv").open(encoding="utf-8")))
    test_child = [m for m in man if m["split"] == "test" and m["is_child"] == "1"]
    hits_csv = AB / "so762_adapted_hits.csv"
    done = set()
    if hits_csv.exists():
        done = {r["utt_id"] for r in csv.DictReader(hits_csv.open(encoding="utf-8"))}
    mode = "a" if done else "w"
    f = hits_csv.open(mode, newline="", encoding="utf-8")
    w = csv.DictWriter(f, fieldnames=["utt_id", "speaker_id", "age", "phone_i", "is_word_final",
                                      "arpabet", "canon", "human_phone_score", "observed",
                                      "match_type", "sim", "posterior"])
    if not done:
        w.writeheader()
    t0 = time.perf_counter()
    n_done = 0
    for i, m in enumerate(test_child):
        utt = m["utt_id"]
        if utt in done:
            continue
        spk = int(m["speaker_id"])
        wav = SO / "WAVE" / f"SPEAKER{spk:04d}" / f"{utt}.WAV"
        words = parse_words(detail[utt])
        ref_flat, scores, finals = [], [], []
        for wi, wd in enumerate(words):
            for pi, (ph, scn) in enumerate(zip(wd["ref"], wd["scores"])):
                ref_flat.append(ph)
                scores.append(scn)
                finals.append(pi == len(wd["ref"]) - 1)
        if not ref_flat:
            continue
        sm = apev.soft_match(str(wav), ref_flat)
        for j, (hit, arpa, hsc, fin) in enumerate(zip(sm.hits, ref_flat, scores, finals)):
            canon = apev.inv.normalize_symbol(arpa, source_kind="arpa")
            w.writerow({"utt_id": utt, "speaker_id": m["speaker_id"], "age": int(m["age"]),
                        "phone_i": j, "is_word_final": int(fin), "arpabet": arpa, "canon": canon,
                        "human_phone_score": hsc, "observed": hit.best_obs,
                        "match_type": hit.match_type, "sim": round(hit.sim, 4),
                        "posterior": round(hit.posterior, 6)})
        f.flush()
        n_done += 1
        if n_done % 50 == 0:
            print(f"  so762 adapted {i+1}/{len(test_child)} ({time.perf_counter()-t0:.0f}s)", flush=True)
    f.close()
    return {"n_new": n_done, "runtime_s": round(time.perf_counter() - t0, 1)}


def compare_so762(out):
    base = list(csv.DictReader((REPO / "Research/Speech/Phase1_9_16/artifacts/zeroshot/so762_test_phone_hits.csv").open(encoding="utf-8")))
    adap = list(csv.DictReader((AB / "so762_adapted_hits.csv").open(encoding="utf-8")))
    # baseline CSV predates phone_i: reconstruct by row order within each utt
    cnt = {}
    base2 = []
    for b in base:
        b = dict(b)
        k = b["utt_id"]
        b["phone_i"] = str(cnt.get(k, 0))
        cnt[k] = cnt.get(k, 0) + 1
        base2.append(b)
    base = base2
    # align by (utt_id, phone_i)
    amap = {(r["utt_id"], r["phone_i"]): r for r in adap}
    pairs = []
    for b in base:
        a = amap.get((b["utt_id"], b["phone_i"]))
        if a is None:
            continue
        pairs.append((b, a))
    rows = []
    for b, a in pairs:
        if b["human_phone_score"] == "" or a["human_phone_score"] == "":
            continue
        rows.append({
            "utt_id": b["utt_id"], "speaker_id": b["speaker_id"], "age": b["age"],
            "canon": b["canon"], "is_word_final": b["is_word_final"],
            "human": float(b["human_phone_score"]),
            "base_match": b["match_type"], "adap_match": a["match_type"],
            "base_post": float(b["posterior"]), "adap_post": float(a["posterior"]),
            "base_sim": float(b["sim"]), "adap_sim": float(a["sim"]),
        })
    with open(AB / "so762_phone_ab.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)

    def band(rs, lo=None, hi=None):
        out = {}
        for name, sel in (("score2", lambda r: r["human"] >= 1.5),
                          ("score1", lambda r: 0.5 <= r["human"] < 1.5),
                          ("score0", lambda r: r["human"] < 0.5)):
            ss = [r for r in rs if sel(r)]
            if not ss:
                out[name] = None
                continue
            out[name] = {
                "n": len(ss),
                "base_exact": float(np.mean([r["base_match"] == "exact" for r in ss])),
                "adap_exact": float(np.mean([r["adap_match"] == "exact" for r in ss])),
                "base_miss": float(np.mean([r["base_match"] == "miss" for r in ss])),
                "adap_miss": float(np.mean([r["adap_match"] == "miss" for r in ss])),
                "base_post": float(np.mean([r["base_post"] for r in ss])),
                "adap_post": float(np.mean([r["adap_post"] for r in ss])),
            }
        return out

    finals = [r for r in rows if r["is_word_final"] == "1" and r["canon"] not in
              {"ɑ", "æ", "ə", "ɔ", "aʊ", "aɪ", "ɛ", "ɝ", "eɪ", "ɪ", "iː", "oʊ", "ɔɪ", "ʊ", "uː"}]
    res = {
        "n_phones": len(rows),
        "all": band(rows),
        "word_final_consonants": band(finals),
        "auc_base_score2_vs_score0": auc_bin([r["base_post"] for r in rows if r["human"] >= 1.5],
                                             [r["base_post"] for r in rows if r["human"] < 0.5]),
        "auc_adap_score2_vs_score0": auc_bin([r["adap_post"] for r in rows if r["human"] >= 1.5],
                                             [r["adap_post"] for r in rows if r["human"] < 0.5]),
    }
    # per-phone deltas (most improved / degraded by exact rate)
    by_ph = defaultdict(lambda: {"n": 0, "b": 0, "a": 0})
    for r in rows:
        d = by_ph[r["canon"]]
        d["n"] += 1
        d["b"] += int(r["base_match"] == "exact")
        d["a"] += int(r["adap_match"] == "exact")
    deltas = [{"canon": k, "n": v["n"], "base_exact": v["b"] / v["n"], "adap_exact": v["a"] / v["n"],
               "delta": (v["a"] - v["b"]) / v["n"]} for k, v in by_ph.items() if v["n"] >= 30]
    deltas.sort(key=lambda x: x["delta"])
    res["most_degraded"] = deltas[:8]
    res["most_improved"] = deltas[-8:]
    print(json.dumps(res, indent=2))
    return res


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tasks", nargs="*", default=["lwe", "siak", "so762"])
    args = ap.parse_args()
    pev = PhoneEvidenceV2()
    pev._ensure()
    apev = AdaptedPEV(AB / "head.pt")

    metrics = {}
    if "lwe" in args.tasks:
        metrics["lwe_ab"] = run_lwe(pev, apev, AB / "lwe_ab_tokens.csv")
    if "siak" in args.tasks:
        metrics["siak_test_ab"] = run_siak(pev, apev, SI_TEST, "siak_test", AB / "siak_test_ab.csv")
        metrics["siak_46_ab"] = run_siak(pev, apev, SI_46, "siak_46", AB / "siak_46_ab.csv")
    if "so762" in args.tasks:
        metrics["so762_run"] = run_so762(pev, apev, AB / "so762_adapted_hits.csv")
        metrics["so762_ab"] = compare_so762(AB)
    (AB / "ab_metrics.json").write_text(json.dumps(metrics, indent=2), encoding="utf-8")
    print("AB DONE")


if __name__ == "__main__":
    main()
