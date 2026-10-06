"""Phase 1.9.16 — consolidated metrics + provenance manifests."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
OUT = REPO / "Research/Speech/Phase1_9_16"


def sha256_file(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def load(p: Path):
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else None


def main():
    ab = load(OUT / "artifacts/ab/ab_metrics.json") or {}
    zs = load(OUT / "artifacts/zeroshot/zeroshot_summary.json") or {}
    rep = load(OUT / "artifacts/replay/replay_summary.json") or {}
    gen = load(OUT / "artifacts/generalization/generalization_summary.json") or {}
    st = load(OUT / "artifacts/stress/stress_summary.json") or {}
    rt = load(OUT / "artifacts/runtime/runtime_license.json") or {}
    feas = load(OUT / "artifacts/audit/feasibility_questions.json") or {}
    so = load(OUT / "artifacts/audit/so762_summary.json") or {}
    inv = load(OUT / "artifacts/inventory/inventory_summary.json") or {}
    qual = load(OUT / "artifacts/quality/quality_summary.json") or {}

    metrics = {
        "phase": "1.9.16",
        "title": "child phone model adaptation research (B1: frozen encoder + CTC head)",
        "decision": "B = ADAPTATION_PROMISING_BUT_INSUFFICIENT",
        "flags": {"production_vad": False, "router_locked": False, "unity_integrated": False,
                  "scorer_modified": False, "production_window_locked": False},
        "feasibility": feas.get("conclusion"),
        "so762": {"train_children": so.get("train_children"), "test_children": so.get("test_children"),
                  "phone_score_distribution": so.get("phone_score_distribution")},
        "inventory": {k: inv.get(k) for k in ("status_counts", "so762_phones_without_canon_mapping",
                                              "model_vocab_unmapped_tokens")},
        "quality_domination": qual.get("domination_checks"),
        "zero_shot_baseline": {k: (zs.get("corpora", {}).get(k, {}).get("canonical")
                                   if k != "siak" else zs.get("corpora", {}).get(k, {}).get("canonical"))
                               for k in ("so762_test_children", "siak_test_speakers")},
        "baseline_human_phone_auc_so762": zs.get("corpora", {}).get("so762_test_children", {}).get(
            "auc_posterior_score2_vs_score0"),
        "ab": {
            "lwe_external": ab.get("lwe_external"),
            "siak_test": ab.get("siak_test"),
            "siak_46_external": ab.get("siak_46_external"),
            "so762_word_final": ab.get("so762_test_children", {}).get("word_final_consonants"),
            "so762_auc": {"baseline": ab.get("so762_test_children", {}).get("auc_base_score2_vs_score0"),
                          "adapted": ab.get("so762_test_children", {}).get("auc_adap_score2_vs_score0")},
        },
        "replay": rep.get("outcomes"),
        "generalization": {k: gen.get(k) for k in
                           ("lwe_speakers_improved_final_recall", "lwe_speakers_degraded_final_recall",
                            "siak_speakers_pearson_up", "siak_speakers_pearson_down",
                            "so762_child_speakers_exact_up", "so762_child_speakers_exact_down")},
        "stress": st.get("conditions"),
        "runtime": rt.get("runtime"),
        "licenses": {k: v.get("license") for k, v in (rt.get("licenses") or {}).items()},
    }
    (OUT / "artifacts" / "metrics.json").write_text(json.dumps(metrics, indent=2), encoding="utf-8")

    inputs = [
        "D:/speech-lab/data/speechocean762/README.md",
        "D:/speech-lab/data/speechocean762/resource/scores-detail.json",
        "D:/speech-lab/data/speechocean762/train/utt2spk",
        "D:/speech-lab/data/speechocean762/test/utt2spk",
        "Research/Speech/ExternalData/SIAK/README.md",
        "Research/Speech/Phase1_9_15/artifacts/siak/calibration_train.csv",
        "Research/Speech/Phase1_9_15/artifacts/siak/calibration_test.csv",
        "Research/Speech/Phase1_9_14/artifacts/p1/p1_evidence_table.csv",
        "Research/Speech/Phase1_9_12/Results/final_consonant_human_review.csv",
    ]
    outputs = [
        "Research/Speech/Phase1_9_16/artifacts/audit/so762_manifest.csv",
        "Research/Speech/Phase1_9_16/artifacts/inventory/phone_mapping.csv",
        "Research/Speech/Phase1_9_16/artifacts/quality/quality_summary.json",
        "Research/Speech/Phase1_9_16/artifacts/zeroshot/zeroshot_summary.json",
        "Research/Speech/Phase1_9_16/artifacts/zeroshot/so762_test_phone_hits.csv",
        "Research/Speech/Phase1_9_16/artifacts/ab/head.pt",
        "Research/Speech/Phase1_9_16/artifacts/ab/training_log.json",
        "Research/Speech/Phase1_9_16/artifacts/ab/ab_metrics.json",
        "Research/Speech/Phase1_9_16/artifacts/ab/lwe_ab_tokens.csv",
        "Research/Speech/Phase1_9_16/artifacts/ab/siak_test_ab.csv",
        "Research/Speech/Phase1_9_16/artifacts/ab/siak_46_ab.csv",
        "Research/Speech/Phase1_9_16/artifacts/ab/so762_phone_ab.csv",
        "Research/Speech/Phase1_9_16/artifacts/replay/failure_replay.csv",
        "Research/Speech/Phase1_9_16/artifacts/generalization/generalization_summary.json",
        "Research/Speech/Phase1_9_16/artifacts/stress/stress_summary.json",
        "Research/Speech/Phase1_9_16/artifacts/runtime/runtime_license.json",
    ]
    prov = {
        "phase": "1.9.16",
        "base": "3f62ef4 (1.9.15)",
        "model_revision": "facebook/wav2vec2-xlsr-53-espeak-cv-ft snapshot "
                          "2c733782da5604684829819a5eb744c193fe9398 (Apache-2.0)",
        "training": "B1 frozen encoder + Conv1d CTC head; seed 1516; so762 train children + "
                    "SIAK train ratings>=60; LWE never trained",
        "inputs": {p: (sha256_file(Path(p)) if Path(p).exists() else
                       (sha256_file(REPO / p) if (REPO / p).exists() else "MISSING"))
                   for p in inputs},
        "outputs": {p: (sha256_file(REPO / p) if (REPO / p).exists() else "MISSING")
                    for p in outputs},
    }
    (OUT / "manifests").mkdir(parents=True, exist_ok=True)
    (OUT / "manifests" / "provenance.json").write_text(json.dumps(prov, indent=2), encoding="utf-8")
    print("metrics.json + provenance.json written")


if __name__ == "__main__":
    main()
