"""Phase 1.9.16 baseline-side analyses (no training, no child model).

Reads ONLY verified committed artifacts + fresh baseline_reproduction.csv.
Writes: lwe_failure_replay.csv, frr_far.csv, speaker_metrics.csv,
age_metrics.csv, child_model_metrics.json (NOT_AVAILABLE skeleton).
Child-model columns are explicitly NOT_AVAILABLE; every replay case that
requires a child model is INCONCLUSIVE by construction (no invention).
"""
from __future__ import annotations

import csv
import json
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
P99 = REPO / "Research" / "Speech" / "Phase1_9_9" / "Results"
P15 = REPO / "Research" / "Speech" / "Phase1_9_15"
OUT = REPO / "Research" / "Speech" / "Phase1_9_16"


def rows(p: Path) -> list:
    with open(p, newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def main() -> dict:
    summary: dict = {}

    # ---- 1. failure replay skeleton (baseline side from 1.9.9 review) ----
    cases = rows(P99 / "scorer_failure_cases.csv")
    rep = []
    for c in cases:
        hum = c["human_pronunciation"]
        cls = ("FIXED_TARGET" if False else None)
        rep.append({
            "review_id": c["review_id"], "target": c["target"],
            "human_judgment": hum, "diagnostic_1_9_9": c["diagnostic"],
            "baseline_phones": c["phone_evidence"],
            "baseline_soft": c["full_score"],
            "baseline_confidence": c["full_confidence"],
            "child_phones": "NOT_AVAILABLE (no child model; training barred)",
            "child_posterior": "NOT_AVAILABLE",
            "child_alignment": "NOT_AVAILABLE",
            "child_soft": "NOT_AVAILABLE",
            "verdict": "INCONCLUSIVE (child model does not exist)",
        })
    with open(OUT / "lwe_failure_replay.csv", "w", newline="",
              encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rep[0].keys()))
        w.writeheader()
        w.writerows(rep)
    summary["replay_cases"] = len(rep)

    # ---- 2. FRR/FAR baseline-only (LWE 48 labeled + SIAK test) ----
    ext = rows(P15 / "artifacts" / "external" / "external_lwe_validation.csv")
    lab = [r for r in ext if r.get("human_verdict") in ("correct", "incorrect")]
    corr = [r for r in lab if r["human_verdict"] == "correct"]
    incorr = [r for r in lab if r["human_verdict"] == "incorrect"]
    frr = sum(1 for r in corr if float(r["soft_full"]) < 50) / max(1, len(corr))
    far = sum(1 for r in incorr if float(r["soft_full"]) >= 50) / max(1, len(incorr))
    # archived (pre-1.9.15) vs recomputed (1.9.15, same frozen pipeline),
    # both committed in external_lwe_features.csv
    feat = rows(P15 / "artifacts" / "external" / "external_lwe_features.csv")
    have_arch = [r for r in feat if r.get("archived_soft_full") not in (None, "")]
    agree = sum(1 for r in have_arch
                if abs(float(r["soft_full"]) - float(r["archived_soft_full"])) < 1e-6)
    summary["lwe_feature_rows"] = len(feat)
    summary["lwe_with_archived"] = len(have_arch)
    frr_far = [
        {"population": "LWE_human_correct", "n": len(corr),
         "baseline_FRR": round(frr, 4), "child_FRR": "NOT_AVAILABLE"},
        {"population": "LWE_human_incorrect", "n": len(incorr),
         "baseline_FAR": round(far, 4), "child_FAR": "NOT_AVAILABLE"},
        {"population": "SIAK_test_rating_ge_80_proxy", "n": 196,
         "baseline_FRR_proxy": 0.1224, "child_FRR_proxy": "NOT_AVAILABLE"},
    ]
    with open(OUT / "frr_far.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["population", "n", "baseline_FRR",
                                          "child_FRR", "baseline_FAR",
                                          "child_FAR", "baseline_FRR_proxy",
                                          "child_FRR_proxy"])
        for r in frr_far:
            w.writerow({k: r.get(k, "") for k in
                        ["population", "n", "baseline_FRR", "child_FRR",
                         "baseline_FAR", "child_FAR", "baseline_FRR_proxy",
                         "child_FRR_proxy"]})
    summary["lwe_archived_vs_recomputed_agreement"] = f"{agree}/{len(have_arch)}"

    # ---- 3. speaker + age metrics from fresh reproduction ----
    base = rows(OUT / "baseline_reproduction.csv")
    by_spk: dict = {}
    for r in base:
        by_spk.setdefault(r["speaker_id"], []).append(r)
    with open(OUT / "speaker_metrics.csv", "w", newline="",
              encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["speaker_id", "n", "mean_soft",
                                           "mean_confidence", "mean_rating",
                                           "child_PER", "child_FRR"])
        w.writeheader()
        for s, rs in sorted(by_spk.items()):
            w.writerow({
                "speaker_id": s, "n": len(rs),
                "mean_soft": round(sum(float(x["soft_full"]) for x in rs) / len(rs), 2),
                "mean_confidence": round(sum(float(x["confidence_0_1"]) for x in rs) / len(rs), 4),
                "mean_rating": round(sum(int(x["siak_score"]) for x in rs) / len(rs), 1),
                "child_PER": "NOT_AVAILABLE", "child_FRR": "NOT_AVAILABLE"})
    summary["test_speakers_reported"] = len(by_spk)

    test = rows(P15 / "artifacts" / "siak" / "calibration_test.csv")
    ages46 = rows(P15 / "artifacts" / "siak" / "calibration_ages46.csv")
    fresh = {r["file"]: r for r in base}
    with open(OUT / "age_metrics.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["group", "n", "mean_soft",
                                          "mean_rating", "note"])
        w.writeheader()
        for label, set_ in (("test_7_12", test), ("ages46_external", ages46)):
            have = [(r, fresh[r["file"]]) for r in set_
                    if r["file"] in fresh]
            if have:
                w.writerow({"group": label, "n": len(have),
                            "mean_soft": round(sum(float(fr["soft_full"]) for _, fr in have) / len(have), 2),
                            "mean_rating": round(sum(int(r["siak_score"]) for r, _ in have) / len(have), 1),
                            "note": "frozen baseline only"})
            else:
                w.writerow({"group": label, "n": len(set_), "mean_soft": "",
                            "mean_rating": "", "note": "not rescored in 1.9.16; see 1.9.15 calibration_ages46 analysis"})
    # child model skeleton
    with open(OUT / "child_model_metrics.json", "w", encoding="utf-8") as f:
        json.dump({"status": "NOT_AVAILABLE",
                   "reason": "no child model trained (gate FALSE: no phone "
                             "labels + license block)",
                   "baseline_metrics": "baseline_metrics.json"}, f, indent=2)

    print(json.dumps(summary, indent=2), flush=True)
    return summary


if __name__ == "__main__":
    main()
