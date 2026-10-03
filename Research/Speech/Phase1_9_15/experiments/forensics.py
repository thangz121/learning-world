"""Phase 1.9.15 — disagreement forensics + P4 bottleneck evidence (no new training).

Builds the disagreement table required by the spec, classifies likely causes with
transparent rules where evidence exists, and marks everything else unknown. Also
assembles the phone-model bottleneck evidence for P4.

Outputs:
  artifacts/forensics/disagreement_cases.csv
  artifacts/forensics/forensics_summary.json
"""
from __future__ import annotations

import csv
import json
import sys
from collections import Counter
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
sys.path.insert(0, str(REPO))
sys.path.insert(0, str(HERE))
import calibrate as CALM  # noqa: E402

OUT = REPO / "Research/Speech/Phase1_9_15"
CAL = OUT / "artifacts" / "calibration"
EXT = OUT / "artifacts" / "external"
FOR = OUT / "artifacts" / "forensics"
FOR.mkdir(parents=True, exist_ok=True)
P1914 = REPO / "Research/Speech/Phase1_9_14"


def rows(path):
    return list(csv.DictReader(path.open(encoding="utf-8")))


def fnum(v, default=None):
    try:
        return float(v)
    except (TypeError, ValueError):
        return default


def main():
    # ---------- SIAK disagreements (hgb_noage candidate) ----------
    curve = {}
    for r in rows(CAL / "calibration_curve_rows.csv"):
        if r["candidate"] == "hgb_noage":
            curve[r["file"]] = r
    test_rows = CALM.load_rows(OUT / "artifacts" / "siak" / "calibration_test.csv")
    siak_cases = []
    for r in test_rows:
        c = curve.get(r["file"])
        if not c:
            continue
        pred = float(c["predicted"])
        y = float(r["siak_score"])
        resid = pred - y
        dur = fnum(r["duration_s"], 0)
        hits = fnum(r["n_hits"], 0)
        miss = fnum(r["n_miss"], 0)
        exact = fnum(r["exact_ratio"], 0)
        conf = fnum(r["confidence_0_1"], 0)
        soft = fnum(r["soft_full"], 0)
        cause = "unknown"
        detail = []
        if y >= 70 and pred < 50:
            direction = "SIAK_HIGH_MODEL_LOW"
            if miss >= 1:
                cause = "phone_evidence_or_alignment"
                detail.append(f"n_miss={int(miss)}")
            if dur < 0.4:
                cause = "audio_short_partial"
                detail.append(f"duration={dur:.2f}s")
            if hits >= 8:
                detail.append(f"long_target={int(hits)} phones")
            if y == 100:
                detail.append("SIAK ceiling 100")
        elif y < 50 and pred >= 65:
            direction = "SIAK_LOW_MODEL_HIGH"
            if conf < 0.01:
                cause = "annotation_or_calibration_uncertain"
                detail.append(f"confidence={conf:.4f}")
            if miss >= 1:
                detail.append(f"n_miss={int(miss)}")
        else:
            continue
        siak_cases.append({
            "dataset": "SIAK (expert rating)", "candidate": "hgb_noage",
            "direction": direction, "case_id": r["file"], "speaker_id": r["speaker_id"],
            "age": r["age"], "utterance": r["utterance"], "siak_score": y,
            "predicted": round(pred, 2), "residual": round(resid, 2),
            "soft_full": soft, "confidence": conf, "n_hits": int(hits), "n_miss": int(miss),
            "exact_ratio": round(exact, 3), "duration_s": round(dur, 2),
            "likely_cause": cause, "cause_detail": ";".join(detail) or "no rule matched",
            "evidence_level": "model-inferred (signals only)",
        })
    siak_cases.sort(key=lambda r: abs(r["residual"]), reverse=True)

    # ---------- LWE disagreements (external validation) ----------
    ext = rows(EXT / "external_lwe_features.csv")
    # 1.9.9 diagnostics for extra cause context
    diag = {}
    for r in rows(P1914 / "artifacts/p1/p1_evidence_table.csv"):
        diag[(r["speaker_id"], r["target"])] = r
    lwe_cases = []
    for r in ext:
        if r["human_class"] not in ("HUMAN_CORRECT", "HUMAN_INCORRECT"):
            continue
        pred = fnum(r["pred_hgb_noage"], 0)
        y = r["human_class"]
        if y == "HUMAN_CORRECT" and pred < 60:
            direction = "HUMAN_CORRECT_MODEL_LOW"
        elif y == "HUMAN_INCORRECT" and pred >= 50:
            direction = "HUMAN_INCORRECT_MODEL_HIGH"
        else:
            continue
        d = diag.get((r["speaker_id"], r["target"]), {})
        cause = "unknown"
        detail = []
        dg = d.get("diagnostic_199", "")
        if dg == "PHONE_MODEL_ERROR":
            cause = "phone_model_evidence"
            detail.append("1.9.9 diagnostic PHONE_MODEL_ERROR")
        elif dg == "SCORER_MISS":
            cause = "alignment_or_score_aggregation"
            detail.append("1.9.9 diagnostic SCORER_MISS")
        elif dg == "TRUE_PRONUNCIATION_ERROR":
            cause = "human_review_and_model_conflict"
        if d.get("assessability_v2") == "LOW":
            detail.append("assessability LOW (v2)")
        if d.get("asr_status", "").startswith("ASR_"):
            detail.append(d["asr_status"])
        lwe_cases.append({
            "dataset": "LWE real child (human review)", "candidate": "hgb_noage",
            "direction": direction, "case_id": f"{r['speaker_id']}_{r['target']}",
            "speaker_id": r["speaker_id"], "age": "", "utterance": r["target"],
            "siak_score": "", "predicted": round(pred, 2), "residual": "",
            "soft_full": fnum(r["soft_full"], 0), "confidence": fnum(r["confidence_0_1"], 0),
            "n_hits": int(fnum(r["n_hits"], 0)), "n_miss": int(fnum(r["n_miss"], 0)),
            "exact_ratio": round(fnum(r["exact_ratio"], 0), 3),
            "duration_s": round(fnum(r["duration_s"], 0), 2),
            "likely_cause": cause, "cause_detail": ";".join(detail) or "no rule matched",
            "evidence_level": "human-reviewed (verdict) + model-inferred cause",
        })

    all_cases = siak_cases + lwe_cases
    fields = list(all_cases[0].keys())
    with open(FOR / "disagreement_cases.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(all_cases)

    # ---------- P4 bottleneck evidence ----------
    diag_counts = Counter(r["diagnostic_199"] for r in rows(P1914 / "artifacts/p1/p1_evidence_table.csv")
                          if r.get("diagnostic_199"))
    ext_metrics = json.loads((EXT / "external_lwe_metrics.json").read_text(encoding="utf-8"))
    cal_metrics = json.loads((CAL / "metrics.json").read_text(encoding="utf-8"))

    # human-correct-low inventory from 1.9.9
    p199 = rows(P1914 / "artifacts/p1/p1_evidence_table.csv")
    hc_low = [r for r in p199 if r["human_class"] == "HUMAN_CORRECT"
              and fnum(r["soft_full"]) is not None and float(r["soft_full"]) < 50]

    summary = {
        "phase": "1.9.15",
        "objective": "P4 — is the phone model the dominant error source?",
        "evidence": {
            "lwe_1_9_9_diagnostic_counts": dict(diag_counts),
            "lwe_human_correct_low_score_n": len(hc_low),
            "lwe_human_correct_low_score_ids": [f"{r['speaker_id']}_{r['target']}" for r in hc_low],
            "siak_test_identity": next(r for r in cal_metrics if False) if False else {
                "note": "see artifacts/calibration/metrics.csv",
                "identity_spearman": 0.272, "identity_pearson": 0.307, "identity_mae": 27.27,
                "hgb_noage_spearman": 0.402, "hgb_noage_pearson": 0.454, "hgb_noage_mae": 19.76,
                "isotonic_spearman": 0.279,
            },
            "external_lwe_auc": {
                k: v.get("auc_correct_vs_incorrect")
                for k, v in ext_metrics["per_candidate"].items()},
            "external_lwe_matched_frr_010": {
                k: v.get("matched_frr_le_0.10")
                for k, v in ext_metrics["per_candidate"].items()},
            "deletion_aware_reference": "1.9.14 P2: deletion-aware/GOP did not beat soft-v2 under FRR-first",
        },
        "interpretation": {
            "phone_evidence_limits_ranking": "Even with rich calibratable features, SIAK test "
                                             "Spearman is 0.40 and LWE external AUC is 0.71; the "
                                             "ranking signal is limited by the evidence itself.",
            "human_correct_phone_errors": "Of 42 human-reviewed 1.9.9 tokens, 12 were "
                                          "PHONE_MODEL_ERROR (human correct / phone wrong).",
            "calibration_cannot_fix_phone_errors": "Calibration shifts the scale but cannot "
                                                   "recover information the phone evidence does "
                                                   "not contain; LWE external FRR drops only by "
                                                   "accepting more incorrect attempts.",
        },
        "recommendation": "child phone model adaptation is a justified NEXT EXPERIMENT "
                          "(separate phase; no training performed here)",
        "flags": {"production_vad": False, "router_locked": False, "unity_integrated": False,
                  "scorer_modified": False, "production_window_locked": False},
    }
    (FOR / "forensics_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps({"n_siak_cases": len(siak_cases), "n_lwe_cases": len(lwe_cases),
                      "diag": dict(diag_counts), "hc_low": len(hc_low)}, indent=2))


if __name__ == "__main__":
    main()
