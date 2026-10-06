"""Phase 1.9.16 — Experiment A/C: zero-shot frozen-model phone benchmark.

Runs the FROZEN phone model (PhoneEvidenceV2@1.4.0) on speaker-disjoint child
test data and records per-phone evidence:

  - speechocean762 test children (64 speakers, 1,280 utt) with 5-expert
    per-phone human scores (2 correct / 1 accented / 0 incorrect-or-missed)
  - SIAK test speakers (27 speakers, 493 utt) canonical agreement only

Metrics: canonical agreement, evidence vs human phone score (AUC, deletion-like
rates), word-final consonant analysis, expected->observed confusion, runtime.
No training, no scorer change.

Outputs: artifacts/zeroshot/*.csv, zeroshot_summary.json, confusion_so762.csv
"""
from __future__ import annotations

import argparse
import csv
import faulthandler
import json
import re
import sys
import time
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter  # noqa: E402
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2  # noqa: E402
from Research.Speech.Phase1_2.Adapters.asr_moonshine import MoonshineAsrAdapter  # noqa: E402

SO = Path(r"D:\speech-lab\data\speechocean762")
SIAK = REPO / "Research/Speech/ExternalData/SIAK"
OUT = REPO / "Research/Speech/Phase1_9_16/artifacts/zeroshot"
OUT.mkdir(parents=True, exist_ok=True)
SI_SPLIT = REPO / "Research/Speech/Phase1_9_15/artifacts/siak/calibration_test.csv"


def parse_words(utt):
    """Return list of words: [{'text','ref':[phones],'scores':[floats],'is_final':[bool]}]"""
    words = []
    for w in utt.get("words", []):
        ref = (w.get("ref-phones") or "").split()
        experts = w.get("phones") or []
        per = [[] for _ in ref]
        for es in experts:
            j = 0
            ok = True
            for t in es.split():
                if t.startswith("[") and t.endswith("]"):
                    continue
                if j >= len(ref):
                    ok = False
                    break
                sc = 2
                if t.startswith("(") and t.endswith(")"):
                    sc = 0
                elif t.startswith("{") and t.endswith("}"):
                    sc = 1
                per[j].append(sc)
                j += 1
            if not ok or j != len(ref):
                for k in range(len(ref)):
                    per[k].append("?")
        scores = [round(sum(v) / len(v), 3) if v and all(x != "?" for x in v) else ""
                  for v in per]
        words.append({"text": w.get("text", ""), "ref": ref, "scores": scores})
    return words


def agreement(rows):
    n = len(rows)
    if not n:
        return {}
    return {
        "n": n,
        "exact": sum(1 for r in rows if r["match_type"] == "exact") / n,
        "exact_or_soft": sum(1 for r in rows if r["match_type"] in ("exact", "soft")) / n,
        "miss": sum(1 for r in rows if r["match_type"] == "miss") / n,
        "mean_posterior": float(np.mean([r["posterior"] for r in rows])),
        "mean_sim": float(np.mean([r["sim"] for r in rows])),
    }


def auc(rows, score_key, pos_pred, neg_pred):
    pos = [r[score_key] for r in rows if pos_pred(r)]
    neg = [r[score_key] for r in rows if neg_pred(r)]
    if not pos or not neg:
        return None
    wins = sum(1.0 if a > b else (0.5 if a == b else 0.0) for a in pos for b in neg)
    return wins / (len(pos) * len(neg))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--corpus", default="both", choices=["so762", "siak", "both"])
    args = ap.parse_args()
    pev = PhoneEvidenceV2()
    tgt = CmuDictTargetAdapter()

    all_summary = {"phase": "1.9.16", "model": "wav2vec2-xlsr-53-espeak-cv-ft (frozen)",
                   "alignment": "ctc_forced_v1", "corpora": {}}

    if args.corpus in ("so762", "both"):
        detail = json.loads((SO / "resource/scores-detail.json").read_text(encoding="utf-8"))
        man = list(csv.DictReader((REPO / "Research/Speech/Phase1_9_16/artifacts/audit/so762_manifest.csv").open(encoding="utf-8")))
        test_child = [m for m in man if m["split"] == "test" and m["is_child"] == "1"]
        hits_csv = OUT / "so762_test_phone_hits.csv"
        utt_csv = OUT / "so762_test_per_utt.csv"
        done = set()
        if hits_csv.exists():
            done = {r["utt_id"] for r in csv.DictReader(hits_csv.open(encoding="utf-8"))}
        mode = "a" if done else "w"
        hits_f = hits_csv.open(mode, newline="", encoding="utf-8")
        utt_f = utt_csv.open(mode, newline="", encoding="utf-8")
        hits_w = csv.DictWriter(hits_f, fieldnames=[
            "utt_id", "speaker_id", "age", "word_i", "is_word_final", "arpabet", "canon",
            "human_phone_score", "observed", "match_type", "sim", "posterior"])
        utt_w = csv.DictWriter(utt_f, fieldnames=[
            "utt_id", "speaker_id", "age", "soft_full", "confidence", "n_phones",
            "mean_sim", "mean_posterior"])
        if not done:
            hits_w.writeheader()
            utt_w.writeheader()
        t0 = time.perf_counter()
        n_done = 0
        for i, m in enumerate(test_child):
            utt = m["utt_id"]
            if utt in done:
                continue
            spk = int(m["speaker_id"])
            wav = SO / "WAVE" / f"SPEAKER{spk:04d}" / f"{utt}.WAV"
            words = parse_words(detail[utt])
            ref_flat, score_flat, final_flat, word_flat = [], [], [], []
            for wi, w in enumerate(words):
                for pi, (ph, sc) in enumerate(zip(w["ref"], w["scores"])):
                    ref_flat.append(ph)
                    score_flat.append(sc)
                    final_flat.append(pi == len(w["ref"]) - 1)
                    word_flat.append(wi)
            if not ref_flat:
                continue
            print(f"  start {utt} ({len(ref_flat)} phones)", flush=True)
            faulthandler.dump_traceback_later(120, exit=True)
            sm = pev.soft_match(str(wav), ref_flat)
            faulthandler.cancel_dump_traceback_later()
            utt_w.writerow({"utt_id": utt, "speaker_id": m["speaker_id"], "age": int(m["age"]),
                            "soft_full": sm.soft_score_0_100, "confidence": sm.confidence_0_1,
                            "n_phones": len(ref_flat), "mean_sim": sm.mean_sim,
                            "mean_posterior": sm.mean_posterior})
            for hit, arpa, hsc, fin, wi in zip(sm.hits, ref_flat, score_flat, final_flat, word_flat):
                canon = pev.inv.normalize_symbol(arpa, source_kind="arpa")
                hits_w.writerow({
                    "utt_id": utt, "speaker_id": m["speaker_id"], "age": int(m["age"]),
                    "word_i": wi, "is_word_final": int(fin), "arpabet": arpa, "canon": canon,
                    "human_phone_score": hsc, "observed": hit.best_obs,
                    "match_type": hit.match_type, "sim": round(hit.sim, 4),
                    "posterior": round(hit.posterior, 6)})
            hits_f.flush()
            utt_f.flush()
            n_done += 1
            if n_done % 50 == 0:
                print(f"  so762 {i+1}/{len(test_child)} ({time.perf_counter()-t0:.0f}s)", flush=True)
        hits_f.close()
        utt_f.close()
        dt = time.perf_counter() - t0
        rows = list(csv.DictReader(hits_csv.open(encoding="utf-8")))
        per_utt = list(csv.DictReader(utt_csv.open(encoding="utf-8")))
        for r in rows:
            r["sim"] = float(r["sim"])
            r["posterior"] = float(r["posterior"])
            r["is_word_final"] = int(r["is_word_final"])
        for r in per_utt:
            r["soft_full"] = float(r["soft_full"])
            r["confidence"] = float(r["confidence"])
            r["mean_sim"] = float(r["mean_sim"])
            r["mean_posterior"] = float(r["mean_posterior"])

        scored = [r for r in rows if r["human_phone_score"] != ""]
        s2 = [r for r in scored if float(r["human_phone_score"]) >= 1.5]
        s1 = [r for r in scored if 0.5 <= float(r["human_phone_score"]) < 1.5]
        s0 = [r for r in scored if float(r["human_phone_score"]) < 0.5]
        finals = [r for r in scored if r["is_word_final"] == 1]
        fcons = [r for r in finals if r["canon"] not in
                 {"ɑ", "æ", "ə", "ɔ", "aʊ", "aɪ", "ɛ", "ɝ", "eɪ", "ɪ", "iː", "oʊ", "ɔɪ", "ʊ", "uː"}]
        conf = Counter((r["canon"], r["observed"]) for r in scored)
        top_conf = conf.most_common(25)
        so_summary = {
            "n_utt": len(per_utt), "n_phones": len(rows), "n_scored_phones": len(scored),
            "runtime_s": round(dt, 1), "sec_per_utt": round(dt / max(1, len(per_utt)), 3),
            "canonical": agreement(rows),
            "human_score_counts": {"2": len(s2), "1": len(s1), "0": len(s0)},
            "evidence_by_human_score": {
                "score2": agreement(s2), "score1": agreement(s1), "score0": agreement(s0)},
            "auc_posterior_score2_vs_score0": auc(scored, "posterior",
                                                  lambda r: float(r["human_phone_score"]) >= 1.5,
                                                  lambda r: float(r["human_phone_score"]) < 0.5),
            "auc_sim_score2_vs_score0": auc(scored, "sim",
                                            lambda r: float(r["human_phone_score"]) >= 1.5,
                                            lambda r: float(r["human_phone_score"]) < 0.5),
            "deletion_like_rate_on_score0": sum(1 for r in s0 if r["match_type"] == "miss") / max(1, len(s0)),
            "deletion_like_rate_on_score2": sum(1 for r in s2 if r["match_type"] == "miss") / max(1, len(s2)),
            "word_final": agreement(finals),
            "word_final_consonant": agreement(fcons),
            "word_final_consonant_by_human": {
                "score2": agreement([r for r in fcons if float(r["human_phone_score"]) >= 1.5]),
                "score1": agreement([r for r in fcons if 0.5 <= float(r["human_phone_score"]) < 1.5]),
                "score0": agreement([r for r in fcons if float(r["human_phone_score"]) < 0.5]),
            },
            "top_confusions_expected_observed": [{"expected": a, "observed": b, "n": n}
                                                 for (a, b), n in top_conf],
        }
        all_summary["corpora"]["so762_test_children"] = so_summary

    if args.corpus in ("siak", "both"):
        test_rows = list(csv.DictReader(SI_SPLIT.open(encoding="utf-8")))
        rows = []
        per_utt = []
        idx = {p.name: p for p in (SIAK / "flac").rglob("*.flac")}
        t0 = time.perf_counter()
        for i, m in enumerate(test_rows):
            p = idx.get(m["file"])
            if p is None:
                continue
            st = tgt.build(m["utterance"])
            arpa = [str(x) for x in st.arpabet]
            if not arpa:
                continue
            sm = pev.soft_match(str(p), arpa)
            per_utt.append({"file": m["file"], "siak_score": int(float(m["siak_score"])),
                            "speaker_id": m["speaker_id"], "age": int(m["age"]),
                            "soft_full": sm.soft_score_0_100, "confidence": sm.confidence_0_1,
                            "n_phones": len(arpa)})
            for j, (hit, a) in enumerate(zip(sm.hits, arpa)):
                canon = pev.inv.normalize_symbol(a, source_kind="arpa")
                rows.append({"file": m["file"], "siak_score": int(float(m["siak_score"])),
                             "speaker_id": m["speaker_id"], "age": int(m["age"]),
                             "is_word_final": int(j == len(arpa) - 1),
                             "arpabet": a, "canon": canon, "observed": hit.best_obs,
                             "match_type": hit.match_type, "sim": round(hit.sim, 4),
                             "posterior": round(hit.posterior, 6)})
            if (i + 1) % 100 == 0:
                print(f"  siak {i+1}/{len(test_rows)} ({time.perf_counter()-t0:.0f}s)", flush=True)
        dt = time.perf_counter() - t0
        with open(OUT / "siak_test_phone_hits.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
            w.writeheader()
            w.writerows(rows)
        with open(OUT / "siak_test_per_utt_phone.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=list(per_utt[0].keys()))
            w.writeheader()
            w.writerows(per_utt)
        finals = [r for r in rows if r["is_word_final"] == 1]
        sc = np.array([r["siak_score"] for r in per_utt])
        soft = np.array([r["soft_full"] for r in per_utt])
        all_summary["corpora"]["siak_test_speakers"] = {
            "n_utt": len(per_utt), "n_phones": len(rows), "runtime_s": round(dt, 1),
            "sec_per_utt": round(dt / max(1, len(per_utt)), 3),
            "canonical": agreement(rows),
            "word_final": agreement(finals),
            "pearson_soft_vs_siak": float(np.corrcoef(soft, sc)[0, 1]) if len(soft) > 2 else None,
        }

    if len(all_summary["corpora"]) == 2:
        pass  # cross-corpus comparison left to report layer
    (OUT / "zeroshot_summary.json").write_text(json.dumps(all_summary, indent=2), encoding="utf-8")
    print(json.dumps(all_summary, indent=2)[:5000])


if __name__ == "__main__":
    main()
