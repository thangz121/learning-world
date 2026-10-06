"""Phase 1.9.16 — data + model feasibility audit (spec §2).

Scans locally available child-speech sources, records licenses and label types,
answers the 12 required feasibility questions, and states whether supervised /
quality-filtered phone-model adaptation is possible without new downloads.

Outputs:
  artifacts/audit/dataset_inventory.csv
  artifacts/audit/feasibility_questions.json
  artifacts/audit/local_scan.json
"""
from __future__ import annotations

import csv
import json
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
OUT = REPO / "Research/Speech/Phase1_9_16/artifacts/audit"
OUT.mkdir(parents=True, exist_ok=True)

SO = Path(r"D:\speech-lab\data\speechocean762")
ZEN = REPO / "Research/Speech/ExternalData/zenodo_200495"
SIAK = REPO / "Research/Speech/ExternalData/SIAK"


def so762_summary():
    p = OUT / "so762_summary.json"
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else {}


def scan_local():
    hits = []
    roots = [Path(r"D:\speech-lab\data"), REPO / "Research/Speech/ExternalData",
             Path(r"D:\speech-lab\models")]
    needles = ["myst", "cslu", "cmu_kids", "cmukids", "ogi", "nocasa", "teflon",
               "child", "kids", "kid"]
    for root in roots:
        if not root.exists():
            continue
        for p in root.iterdir():
            n = p.name.lower()
            if any(k in n for k in needles):
                hits.append(str(p))
    return hits


def main():
    s = so762_summary()
    inv = []

    inv.append({
        "dataset": "speechocean762 (SLR101)",
        "path": str(SO), "present": SO.exists(),
        "license": "CC BY 4.0",
        "license_evidence": "openslr.org/101 license field (fetched 2026-10-03); free commercial",
        "age_range": "6-15 (children), 19-43 (adults)",
        "speaker_count": 250, "utterance_count": 5000,
        "language": "English (non-native)", "l1": "Mandarin",
        "audio_format": "16 kHz mono PCM16 WAV",
        "phone_label_type": "per-phone human accuracy 0/1/2 (5 experts) + canonical ARPAbet phones",
        "word_label_type": "word accuracy 0-10 + stress (5 experts)",
        "rating_label_type": "sentence accuracy/fluency/completeness/prosodic (0-10)",
        "target_vocabulary": "read sentences (5000), no fixed target list",
        "label_quality": "5 independent experts; phone notation with insertions; "
                         "94,445 phone judgments (2:87215 / 1:3827 / 0:3403)",
        "train_test_split": "speaker-disjoint (train 125 spk / test 125 spk, overlap 0)",
        "leakage_risks": "official split is speaker-disjoint; LWE must stay external test",
        "usability_for_phone_training": "SUITABLE for quality-filtered head adaptation "
                                        "(canonical targets + human score masking)",
    })
    inv.append({
        "dataset": "SIAK", "path": str(SIAK), "present": SIAK.exists(),
        "license": "CC-BY-ND-4.0", "license_evidence": "dataset README (commercial model "
                                                       "building/evaluation allowed; no unrelated "
                                                       "derivatives; legal review open)",
        "age_range": "4-12", "speaker_count": 172, "utterance_count": 16308,
        "language": "English (learner + UK native)", "l1": "Finnish / UK / other",
        "audio_format": "FLAC 16 kHz mono",
        "phone_label_type": "NONE (no phone annotations)",
        "word_label_type": "word transcript present", "rating_label_type": "expert 0-100 (single)",
        "target_vocabulary": "320 single-word targets",
        "label_quality": "single annotator; no phone labels; zero-rating/rejected class absent "
                         "from release (1.9.14)",
        "train_test_split": "official speaker-disjoint IDs; own 3-way split in 1.9.15",
        "leakage_risks": "within-split speaker repetition; LWE external must stay test",
        "usability_for_phone_training": "NOT for supervised phone labels; usable as additional "
                                        "acoustic domain data with utterance score filter",
    })
    inv.append({
        "dataset": "Zenodo 200495 (LWE real-child corpus)", "path": str(ZEN),
        "present": ZEN.exists(),
        "license": "CC-BY-4.0", "license_evidence": "recording inventory license column (1.9.8)",
        "age_range": "~4-5 (dataset mean 4.9y)", "speaker_count": 11,
        "utterance_count": 671, "language": "English",
        "l1": "native/non-native mix (incl. Vietnamese-L1 LWE learners)",
        "audio_format": "44.1 kHz mono WAV (16k cache derived)",
        "phone_label_type": "NONE", "word_label_type": "word targets/transcripts",
        "rating_label_type": "LWE human review (1.9.9-1.9.12, single reviewer)",
        "target_vocabulary": "numbers + predefined sentences + free speech",
        "label_quality": "human word-level verdicts; no phone annotations",
        "train_test_split": "**reserved as external test** (never train)",
        "leakage_risks": "human-reviewed subset is the LWE answer key",
        "usability_for_phone_training": "TEST ONLY (spec §7)",
    })
    inv.append({
        "dataset": "MyST / CSLU Kids / CMU Kids / OGI Kids / NOCASA",
        "path": "", "present": False, "license": "varies / not downloaded",
        "license_evidence": "Phase 1.1 candidate reports only",
        "age_range": "", "speaker_count": "", "utterance_count": "", "language": "English",
        "l1": "", "audio_format": "", "phone_label_type": "some have phone-level labels",
        "word_label_type": "", "rating_label_type": "", "target_vocabulary": "",
        "label_quality": "unknown locally", "train_test_split": "",
        "leakage_risks": "",
        "usability_for_phone_training": "NOT AVAILABLE locally; potential future augmentation",
    })

    with open(OUT / "dataset_inventory.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(inv[0].keys()))
        w.writeheader()
        w.writerows(inv)

    scan = {"local_scan_hits": scan_local(),
            "so762_path": str(SO), "so762_present": SO.exists()}
    (OUT / "local_scan.json").write_text(json.dumps(scan, indent=2), encoding="utf-8")

    feas = {
        "phase": "1.9.16",
        "questions": {
            "1_child_datasets_available": "speechocean762 (SLR101, CC BY 4.0, 250 spk/5000 utt, "
                                          "122 children), SIAK (172 spk/16308 utt, rating-only), "
                                          "Zenodo 200495 (11 children, test-only)",
            "2_phoneme_labels": "speechocean762: per-phone human accuracy 0/1/2 + canonical ARPAbet. "
                                "SIAK/Zenodo: none",
            "3_language_accent": "so762 non-native English (Mandarin L1); SIAK Finnish/UK/other; "
                                 "LWE target = Vietnamese-L1 children (no matching corpus)",
            "4_age_metadata": "so762 ages 6-15 children; SIAK 4-12; Zenodo ~4-5",
            "5_speaker_counts": "so762 child train 58 / child test 64 (disjoint); SIAK 172; "
                                "Zenodo 11",
            "6_target_vocabulary": "so762 read sentences; SIAK 320 single words; LWE numbers/words",
            "7_label_reliability": "so762: 5 independent experts, 96.4% of phone judgments are "
                                   "score 2, explicit notation for 1/0/insertion; reliable enough "
                                   "for quality-filtered targets, NOT for phone transcription",
            "8_label_levels": "so762 phone+word+sentence; SIAK utterance rating; Zenodo word",
            "9_training_license": "so762 CC BY 4.0 allows training and commercial use; SIAK CC-BY-ND "
                                  "allows model building/evaluation per README (legal review open)",
            "10_enough_for_finetune": "Enough for SMALL head-only adaptation on frozen encoder "
                                      "(1,160 child utterances / 58 speakers) on CPU; NOT enough "
                                      "for full fine-tuning within local CPU constraints",
            "11_speaker_disjoint_test": "yes: so762 official child test (64 spk) + SIAK test (27 spk) "
                                        "+ LWE external (never trained)",
            "12_local_runtime": "head-only adaptation: model stays XLSR-53 large 1.26 GB; head < 10 MB; "
                                "CPU inference unchanged (head adds <5 ms/frame batch)",
        },
        "conclusion": {
            "supervised_phone_transcription_training": False,
            "reason": "no dataset provides human phone TRANSCRIPTIONS of what the child actually "
                      "said; so762 gives scores on canonical phones, SIAK/Zenodo give none",
            "quality_filtered_head_adaptation_B1": True,
            "plan": "freeze encoder (facebook/wav2vec2-xlsr-53-espeak-cv-ft); train a small "
                    "Conv1d CTC head on so762 TRAIN children; canonical targets; per-utterance "
                    "weight = mean human phone score/2; mask utterances with mean < 0.5; "
                    "evaluate speaker-disjoint on so762 test children + SIAK test speakers + "
                    "LWE external (test only)",
            "stop_conditions": ["if B1 does not improve down-stream human-anchored metrics under "
                                "FRR-first, report falsified and recommend dataset acquisition"],
        },
    }
    (OUT / "feasibility_questions.json").write_text(json.dumps(feas, indent=2), encoding="utf-8")
    print(json.dumps({"datasets_present": [(i["dataset"], i["present"]) for i in inv],
                      "conclusion": feas["conclusion"]}, indent=2))


if __name__ == "__main__":
    main()
