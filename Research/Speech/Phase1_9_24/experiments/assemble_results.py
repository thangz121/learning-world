"""Assemble ENCODER_DESIGN_EXPERIMENT_RESULTS.json from the WP-1.9.24 artifacts."""
from __future__ import annotations

import csv
import json
import sys
from collections import Counter
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_24"
ART = OUT / "artifacts"


def main():
    typeb = list(csv.DictReader(open(OUT / "ENCODER_TYPE_B_CASES.csv", encoding="utf-8")))
    confusion = list(csv.DictReader(open(OUT / "PHONE_CONFUSION_PROFILE.csv", encoding="utf-8")))
    child = json.loads((ART / "child_speech_stats.json").read_text(encoding="utf-8"))
    alt = json.loads((ART / "alt_encoder_summary.json").read_text(encoding="utf-8"))
    pack = list(csv.DictReader(open(OUT / "HUMAN_LABEL_REVIEW_PACK_FINAL.csv", encoding="utf-8")))

    labels = {
        "existing_lwe_blind_labels": 28,
        "existing_present": 16,
        "existing_absent": 12,
        "existing_r_present": 1,
        "existing_r_absent": 5,
        "new_labels_collected": 0,
        "single_reviewer_limitation": True,
        "pack_candidates": len(pack),
        "pack_classes": dict(Counter(r["candidate_class"] for r in pack)),
        "pack_r_priority": sum(1 for r in pack if r["tag_r_priority"] == "1"),
        "pack_weak_present": sum(1 for r in pack if r["tag_weak_present"] == "1"),
        "pack_rank25": sum(1 for r in pack if r["tag_rank25_identity"] == "1"),
        "pack_strong_false_accept": sum(1 for r in pack
                                        if r["tag_strong_false_accept"] == "1"),
        "review_tooling": "Research/Speech/Phase1_9_15/experiments/serve_review.py "
                          "(hard-coded to the fidelity_v2 pack; a minimal adaptation or the "
                          "WP-1.9.12 standalone blind HTML is required for this pack)",
    }
    verdicts = {r["case_id"]: {"verdict": r["verdict"], "reason": r["verdict_reason"],
                               "fals": {k: int(r[f"fals_{k}_{n}"]) for k, n in [
                                   ("A", "strong"), ("B", "window_stable"),
                                   ("C", "neighbor_support"), ("D", "position"),
                                   ("E", "beats_competitor"), ("F", "not_blank"),
                                   ("G", "confident_label")]}}
                for r in typeb}
    out = {
        "phase": "1.9.24",
        "mission": "human label expansion + encoder evidence design audit",
        "model_provenance": {
            "id": "facebook/wav2vec2-xlsr-53-espeak-cv-ft",
            "snapshot": "2c733782da5604684829819a5eb744c193fe9398",
            "architecture": "Wav2Vec2ForCTC (XLSR-53 large), hidden 1024, 24 layers, vocab 392, "
                            "blank <pad> id 0, 16 kHz",
            "training_domain_record": "local record Phase1_1 candidate 32: phoneme CTC with espeak "
                                      "en-us backend; base XLSR-53 multilingual pre-training; "
                                      "no child-speech fine-tuning documented",
            "license": "apache-2.0 (verified via HF model_info 2026-10-02, local record)",
            "known_domain_gap": "adult multilingual read speech vs 4-6 y/o Vietnamese-L1 children; "
                                "no Vietnamese-L1 child corpus exists publicly (HANDOFF 1.9.20)",
        },
        "type_b_cases": typeb,
        "type_b_verdicts": verdicts,
        "type_b_summary": {
            "n": len(typeb),
            "encoder_false_evidence_confirmed": sum(1 for r in typeb
                                                    if r["verdict"] == "STRONG_ENCODER_FALSE_EVIDENCE"),
            "unresolved": sum(1 for r in typeb if r["verdict"] == "UNRESOLVED"),
            "unresolved_label_limited": sum(1 for r in typeb if r["fals_G_confident_label"] == "0"),
            "unresolved_isolated_peak": sum(1 for r in typeb
                                            if r["fals_C_neighbor_support"] == "0"),
        },
        "phone_confusion_profile": confusion,
        "child_speech_stats": child,
        "alt_encoder_counterfactual": alt,
        "label_audit": labels,
        "flags": {"production_vad": False, "router_locked": False, "unity_integrated": False,
                  "scorer_modified": False, "production_window_locked": False,
                  "b2_training": False, "encoder_finetuning": False},
    }
    (OUT / "ENCODER_DESIGN_EXPERIMENT_RESULTS.json").write_text(
        json.dumps(out, indent=2, ensure_ascii=False), encoding="utf-8")
    print("type-B summary:", out["type_b_summary"])
    print("alt summary keys:", list(alt["summary"].keys()))
    print("DONE assemble_results")


if __name__ == "__main__":
    main()
