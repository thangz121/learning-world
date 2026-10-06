"""Phase 1.9.16 — Experiment E (historical failure replay) + I (speaker generalization).

E: replay the 12 PHONE_MODEL_ERROR and 6 SCORER_MISS cases from 1.9.9/1.9.10 with
the frozen baseline and the adapted head, classify FIXED / PARTIALLY_FIXED /
UNCHANGED / REGRESSED / INCONCLUSIVE with transparent rules.

I: per-speaker generalization for LWE (80 tokens), SIAK test and so762.

Outputs: artifacts/replay/failure_replay.csv, replay_summary.json,
         artifacts/generalization/*.csv, generalization_summary.json
"""
from __future__ import annotations

import csv
import json
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
P1910 = REPO / "Research/Speech/Phase1_9_10/Results"
P1911 = REPO / "Research/Speech/Phase1_9_11/Results"
AB = REPO / "Research/Speech/Phase1_9_16/artifacts/ab"
GEN = REPO / "Research/Speech/Phase1_9_16/artifacts/generalization"
REP = REPO / "Research/Speech/Phase1_9_16/artifacts/replay"
GEN.mkdir(parents=True, exist_ok=True)
REP.mkdir(parents=True, exist_ok=True)


def rows(p):
    return list(csv.DictReader(p.open(encoding="utf-8-sig")))


def main():
    ab2 = {}
    for r in rows(AB / "lwe_ab_tokens.csv"):
        parts = r["case_id"].split("_")
        speaker = parts[0] + "_" + parts[1]
        target = "_".join(parts[2:])
        ab2[(speaker, target)] = r

    # ---- E: PHONE_MODEL_ERROR (12), human correct / phone wrong ----
    replay = []
    pme = rows(P1910 / "phone_model_error_cases.csv")
    for r in pme:
        key = (r["speaker_id"], r["target"])
        a = ab2.get(key)
        if not a:
            replay.append({"case_id": r["case_id"], "group": "PHONE_MODEL_ERROR",
                           "speaker_id": r["speaker_id"], "target": r["target"],
                           "human": r["human_pronunciation"], "canonical": r["canonical_phones"],
                           "observed_199": r["observed_phones"], "baseline_soft": "", "adapted_soft": "",
                           "baseline_final": "", "adapted_final": "",
                           "outcome": "INCONCLUSIVE", "note": "audio/label not in 80-token AB set"})
            continue
        b, ad = float(a["baseline_soft"]), float(a["adapted_soft"])
        d = ad - b
        if d >= 10:
            outcome = "FIXED"
        elif d > 3:
            outcome = "PARTIALLY_FIXED"
        elif d <= -10:
            outcome = "REGRESSED"
        elif d < -3:
            outcome = "PARTIALLY_REGRESSED"
        else:
            outcome = "UNCHANGED"
        replay.append({"case_id": r["case_id"], "group": "PHONE_MODEL_ERROR",
                       "speaker_id": r["speaker_id"], "target": r["target"],
                       "human": r["human_pronunciation"], "canonical": r["canonical_phones"],
                       "observed_199": r["observed_phones"],
                       "baseline_soft": b, "adapted_soft": ad, "delta": round(d, 1),
                       "baseline_final": a["baseline_final_match"], "adapted_final": a["adapted_final_match"],
                       "outcome": outcome,
                       "note": "human correct, phone-model wrong in 1.9.9"})

    # ---- E: SCORER_MISS (6), human incorrect / baseline soft >= 50 ----
    sm = rows(P1911 / "scorer_miss_human_review.csv")
    for r in sm:
        key = (r["speaker_id"], r["target"])
        a = ab2.get(key)
        if not a:
            replay.append({"case_id": r["case_id"], "group": "SCORER_MISS",
                           "speaker_id": r["speaker_id"], "target": r["target"],
                           "human": r["human_state"], "canonical": r["canonical"],
                           "observed_199": r["observed_full"], "baseline_soft": "", "adapted_soft": "",
                           "baseline_final": "", "adapted_final": "",
                           "outcome": "INCONCLUSIVE", "note": "not in AB set"})
            continue
        b, ad = float(a["baseline_soft"]), float(a["adapted_soft"])
        d = ad - b
        if ad < 50 and b >= 50:
            outcome = "FIXED"
        elif d <= -15 and ad >= 50:
            outcome = "PARTIALLY_FIXED"
        elif d >= 10:
            outcome = "REGRESSED"
        else:
            outcome = "UNCHANGED"
        replay.append({"case_id": r["case_id"], "group": "SCORER_MISS",
                       "speaker_id": r["speaker_id"], "target": r["target"],
                       "human": r["human_state"], "canonical": r["canonical"],
                       "observed_199": r["observed_full"], "baseline_soft": b, "adapted_soft": ad,
                       "delta": round(d, 1), "baseline_final": a["baseline_final_match"],
                       "adapted_final": a["adapted_final_match"], "outcome": outcome,
                       "note": "human incorrect, baseline score high (scorer miss)"})

    with open(REP / "failure_replay.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(replay[0].keys()))
        w.writeheader()
        w.writerows(replay)
    summary = {"n": len(replay),
               "outcomes": dict(Counter(r["outcome"] for r in replay)),
               "by_group": {g: dict(Counter(r["outcome"] for r in replay if r["group"] == g))
                            for g in ("PHONE_MODEL_ERROR", "SCORER_MISS")}}
    (REP / "replay_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")

    # ---- I: speaker generalization ----
    # LWE: per speaker from lwe_ab_tokens (labels where available)
    lwe = rows(AB / "lwe_ab_tokens.csv")
    lwe_rows = []
    for spk in sorted(set(r["case_id"].split("_")[0] + "_" + r["case_id"].split("_")[1]
                          for r in lwe)):
        rs = [r for r in lwe if r["case_id"].startswith(spk + "_")]
        lab = [r for r in rs if r["human_class"]]
        if not lab:
            continue
        cor = [r for r in lab if r["human_class"] == "HUMAN_CORRECT"]
        fin = [r for r in rs if r["fc_human_label"] and "PRESENT" in r["fc_human_label"]]
        lwe_rows.append({
            "speaker_id": spk, "n_tokens": len(rs), "n_labeled": len(lab),
            "baseline_mean_soft": round(float(np.mean([float(r["baseline_soft"]) for r in rs])), 1),
            "adapted_mean_soft": round(float(np.mean([float(r["adapted_soft"]) for r in rs])), 1),
            "baseline_frr": round(float(np.mean([float(r["baseline_soft"]) < 50 for r in cor])), 3) if cor else None,
            "adapted_frr": round(float(np.mean([float(r["adapted_soft"]) < 50 for r in cor])), 3) if cor else None,
            "baseline_final_recall": round(float(np.mean([r["baseline_final_match"] in ("exact", "soft") for r in fin])), 3) if fin else None,
            "adapted_final_recall": round(float(np.mean([r["adapted_final_match"] in ("exact", "soft") for r in fin])), 3) if fin else None,
        })
    with open(GEN / "lwe_per_speaker.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(lwe_rows[0].keys()))
        w.writeheader()
        w.writerows(lwe_rows)

    # SIAK test per speaker
    si = rows(AB / "siak_test_ab.csv")
    si_rows = []
    for spk in sorted(set(r["speaker_id"] for r in si)):
        rs = [r for r in si if r["speaker_id"] == spk]
        if len(rs) < 10:
            continue
        sc = [float(r["siak_score"]) for r in rs]
        def pr(f):
            v = [float(r[f]) for r in rs]
            if np.std(v) == 0 or np.std(sc) == 0:
                return None
            return float(np.corrcoef(v, sc)[0, 1])
        si_rows.append({"speaker_id": spk, "n": len(rs), "age": rs[0]["age"],
                        "baseline_pearson": pr("baseline_soft"), "adapted_pearson": pr("adapted_soft"),
                        "baseline_mean": round(float(np.mean([float(r["baseline_soft"]) for r in rs])), 1),
                        "adapted_mean": round(float(np.mean([float(r["adapted_soft"]) for r in rs])), 1),
                        "siak_mean": round(float(np.mean(sc)), 1)})
    with open(GEN / "siak_test_per_speaker.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(si_rows[0].keys()))
        w.writeheader()
        w.writerows(si_rows)

    # so762 per speaker exact delta
    so = rows(AB / "so762_phone_ab.csv")
    so_rows = []
    for spk in sorted(set(r["speaker_id"] for r in so), key=lambda s: int(s)):
        rs = [r for r in so if r["speaker_id"] == spk]
        if len(rs) < 30:
            continue
        be = float(np.mean([r["base_match"] == "exact" for r in rs]))
        ae = float(np.mean([r["adap_match"] == "exact" for r in rs]))
        so_rows.append({"speaker_id": spk, "age": rs[0]["age"], "n_phones": len(rs),
                        "baseline_exact": round(be, 3), "adapted_exact": round(ae, 3),
                        "delta": round(ae - be, 3)})
    with open(GEN / "so762_per_speaker.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(so_rows[0].keys()))
        w.writeheader()
        w.writerows(so_rows)

    gen_summary = {
        "lwe_speakers_improved_final_recall": sum(1 for r in lwe_rows
                                                  if r["baseline_final_recall"] is not None
                                                  and r["adapted_final_recall"] > r["baseline_final_recall"]),
        "lwe_speakers_degraded_final_recall": sum(1 for r in lwe_rows
                                                  if r["baseline_final_recall"] is not None
                                                  and r["adapted_final_recall"] < r["baseline_final_recall"]),
        "siak_speakers_pearson_up": sum(1 for r in si_rows if r["adapted_pearson"] is not None
                                        and r["baseline_pearson"] is not None
                                        and r["adapted_pearson"] > r["baseline_pearson"]),
        "siak_speakers_pearson_down": sum(1 for r in si_rows if r["adapted_pearson"] is not None
                                          and r["baseline_pearson"] is not None
                                          and r["adapted_pearson"] < r["baseline_pearson"]),
        "so762_child_speakers_exact_up": sum(1 for r in so_rows if r["delta"] > 0),
        "so762_child_speakers_exact_down": sum(1 for r in so_rows if r["delta"] < 0),
        "worst_so762_delta": sorted(so_rows, key=lambda r: r["delta"])[:5],
        "best_so762_delta": sorted(so_rows, key=lambda r: r["delta"])[-5:],
    }
    (GEN / "generalization_summary.json").write_text(json.dumps(gen_summary, indent=2), encoding="utf-8")
    print(json.dumps({"replay": summary, "generalization": gen_summary}, indent=2)[:4000])


if __name__ == "__main__":
    main()
