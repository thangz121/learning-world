"""Phase 1.9.15 — consolidated machine-readable metrics + provenance hashes."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
OUT = REPO / "Research/Speech/Phase1_9_15"


def sha256_file(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def load(p: Path):
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else None


def main():
    cal = load(OUT / "artifacts/calibration/metrics.json")
    ext = load(OUT / "artifacts/external/external_lwe_metrics.json")
    fid = load(OUT / "artifacts/fidelity/independent_metrics.json")
    beh = load(OUT / "artifacts/fidelity/system_behavior_audit.json")
    fore = load(OUT / "artifacts/forensics/forensics_summary.json")
    split = load(OUT / "artifacts/siak/split_manifest.json")

    cal_table = [r for r in (cal or {}).get("candidates", [])]
    metrics = {
        "phase": "1.9.15",
        "title": "population calibration + independent child validation",
        "flags": {"production_vad": False, "router_locked": False, "unity_integrated": False,
                  "scorer_modified": False, "production_window_locked": False},
        "P1_independent_fidelity": {
            "n_items": (fid or {}).get("n_rows"),
            "single_reviewer": True,
            "v1_false_gate_rate_valid_attempts": (fid or {}).get("v1", {}).get("false_gate_rate"),
            "v2_false_gate_rate_valid_attempts": (fid or {}).get("v2", {}).get("false_gate_rate"),
            "v2_dangerous_accept_strict_rate": (fid or {}).get("v2", {}).get("dangerous_accept_strict_rate"),
            "v1_dangerous_accept_strict_rate": (fid or {}).get("v1", {}).get("dangerous_accept_strict_rate"),
            "unlabeled_behavior_audit": beh,
        },
        "P2_siak_calibration": {
            "splits": (cal or {}).get("splits"),
            "candidate_names": cal_table,
            "age_conditioning_test": (cal or {}).get("age_conditioning_test"),
            "age_conditioning_rejected": True,
            "siak_test_score_semantics": (cal or {}).get("siak_test_score_semantics"),
        },
        "P3_external_lwe": {
            "n_labeled": (ext or {}).get("n_labeled"),
            "n_correct": (ext or {}).get("n_human_correct"),
            "n_incorrect": (ext or {}).get("n_human_incorrect"),
            "per_candidate_auc": {k: v.get("auc_correct_vs_incorrect")
                                  for k, v in (ext or {}).get("per_candidate", {}).items()},
            "per_candidate_human_correct_frr50": {
                k: v.get("human_correct", {}).get("frr_lt50")
                for k, v in (ext or {}).get("per_candidate", {}).items()},
            "per_candidate_human_incorrect_far50": {
                k: v.get("human_incorrect", {}).get("far_ge50")
                for k, v in (ext or {}).get("per_candidate", {}).items()},
        },
        "P4_bottleneck": {
            "recommendation": (fore or {}).get("recommendation"),
            "diagnostic_counts_199": (fore or {}).get("evidence", {}).get(
                "lwe_1_9_9_diagnostic_counts"),
        },
        "leakage": {
            "speaker_overlap": (split or {}).get("leakage_check"),
            "duplicate_audio_groups": (split or {}).get("n_duplicate_audio_groups"),
        },
        "decision": "B = CALIBRATION_PROMISING_BUT_INSUFFICIENT",
        "p4_child_phone_model_research_justified": True,
    }
    (OUT / "artifacts" / "metrics.json").write_text(json.dumps(metrics, indent=2), encoding="utf-8")

    inputs = [
        "Research/Speech/ExternalData/SIAK/train.csv",
        "Research/Speech/ExternalData/SIAK/test.csv",
        "Research/Speech/ExternalData/SIAK/README.md",
        "Research/Speech/Phase1_9_14/artifacts/siak/siak_scored.csv",
        "Research/Speech/Phase1_9_14/artifacts/p1/p1_evidence_table.csv",
        "Research/Speech/Phase1_9_12/Results/final_consonant_human_review.csv",
    ]
    outputs = [
        "Research/Speech/Phase1_9_15/artifacts/siak/siak_expanded_scored.csv",
        "Research/Speech/Phase1_9_15/artifacts/siak/calibration_train.csv",
        "Research/Speech/Phase1_9_15/artifacts/siak/calibration_valid.csv",
        "Research/Speech/Phase1_9_15/artifacts/siak/calibration_test.csv",
        "Research/Speech/Phase1_9_15/artifacts/siak/calibration_ages46.csv",
        "Research/Speech/Phase1_9_15/artifacts/siak/split_manifest.json",
        "Research/Speech/Phase1_9_15/artifacts/calibration/metrics.csv",
        "Research/Speech/Phase1_9_15/artifacts/calibration/metrics.json",
        "Research/Speech/Phase1_9_15/artifacts/external/external_lwe_validation.csv",
        "Research/Speech/Phase1_9_15/artifacts/external/external_lwe_metrics.json",
        "Research/Speech/Phase1_9_15/artifacts/fidelity/fidelity_independent.csv",
        "Research/Speech/Phase1_9_15/artifacts/fidelity/independent_metrics.json",
        "Research/Speech/Phase1_9_15/artifacts/fidelity/system_behavior_audit.json",
        "Research/Speech/Phase1_9_15/artifacts/forensics/disagreement_cases.csv",
        "Research/Speech/Phase1_9_15/Results/fidelity_v2_StageA_Filled.csv",
    ]
    prov = {
        "phase": "1.9.15",
        "base": "d594821 (1.9.14)",
        "model": "facebook/wav2vec2-xlsr-53-espeak-cv-ft via frozen PhoneEvidenceV2@1.4.0 "
                 "(Apache-2.0); Moonshine-tiny ASR local; no model training",
        "human_review": {
            "pack": "fidelity_v2", "n_items": 115, "reviewer": "human_maynode",
            "single_reviewer": True,
            "submission_file": "Results/fidelity_v2_StageA_Filled.csv",
        },
        "inputs": {p: (sha256_file(REPO / p) if (REPO / p).exists() else "MISSING") for p in inputs},
        "outputs": {p: (sha256_file(REPO / p) if (REPO / p).exists() else "MISSING") for p in outputs},
    }
    (OUT / "manifests").mkdir(parents=True, exist_ok=True)
    (OUT / "manifests" / "provenance.json").write_text(json.dumps(prov, indent=2), encoding="utf-8")
    print("metrics.json + provenance.json written")


if __name__ == "__main__":
    main()
