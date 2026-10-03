"""Phase 1.9.16 dataset audit (no training).

Measures quality/distribution facts for the SIAK speaker-disjoint selection
(3,168 utt) from local verified data + committed CSVs, and records the
phone-label determination for every candidate corpus.
Writes dataset_manifest.json + prints the DATA_AUDIT section rows as JSON.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

import numpy as np

REPO = Path(__file__).resolve().parents[4]
SIAK = REPO / "Research" / "Speech" / "ExternalData" / "SIAK"
ART = REPO / "Research" / "Speech" / "Phase1_9_15" / "artifacts" / "siak"
OUT = REPO / "Research" / "Speech" / "Phase1_9_16"


def read_csv(name: str) -> list:
    with open(ART / name, newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def main() -> dict:
    import soundfile as sf
    idx = {}
    for p in (SIAK / "flac").rglob("*.flac"):
        idx[p.name] = p

    per_file = []
    for fname in ("calibration_train.csv", "calibration_valid.csv",
                  "calibration_test.csv", "calibration_ages46.csv"):
        per_file += read_csv(fname)

    durs, srs, chs, silrat = [], [], [], []
    clips, missing, n_decode = 0, [], 0
    for r in per_file:
        p = idx.get(r["file"])
        if p is None:
            missing.append(r["file"])
            continue
        info = sf.info(str(p))
        durs.append(info.duration)
        srs.append(info.samplerate)
        chs.append(info.channels)
        try:
            x, _ = sf.read(str(p), dtype="float32")
            if x.ndim > 1:
                x = x.mean(axis=1)
            n_decode += 1
            if np.max(np.abs(x)) >= 0.999:
                clips += 1
            silrat.append(float(np.mean(np.abs(x) < 0.01)))
        except Exception:
            missing.append(r["file"] + "#decode")

    durs = np.array(durs)
    manifest = {
        "phase": "1.9.16",
        "datasets": {
            "SIAK": {
                "role": "child population for validation/calibration; "
                        "NOT supervised phone training (ratings only)",
                "license": "CC-BY-ND-4.0 (training use BLOCKED pending review)",
                "n_utterances_selected": len(per_file),
                "total_duration_s": round(float(durs.sum()), 1),
                "sample_rates": sorted(set(srs)),
                "channels": sorted(set(chs)),
                "duration_s": {"mean": round(float(durs.mean()), 3),
                               "min": round(float(durs.min()), 3),
                               "max": round(float(durs.max()), 3)},
                "clipped_files": clips,
                "mean_silence_ratio_lt_0.01": round(float(np.mean(silrat)), 4) if silrat else None,
                "missing_or_undecodable": len(missing),
                "phone_labels": "NONE (columns: file, utterance, score only)",
                "label_quality": "single expert rating 0-100; min 1 max 100; "
                                 "rejected items absent from release",
            },
            "LWE_committed": {
                "role": "external validation / failure replay (derivatives)",
                "n_reviewed_tokens_1_9_9": 42,
                "phone_labels": "NONE (human verdicts + model phone_evidence "
                                "strings only)",
            },
            "CMU_Kids_mirrors": {
                "role": "assessed, NOT downloaded",
                "reason": "word transcripts only; no phone labels; mirror "
                          "license claims unverified (MagicLuke asserts MIT, "
                          "1 like; kwhu5695 none)",
                "status": "NOT_REQUIRED",
            },
            "MyST_PFSTAR": {"status": "BLOCKED_ACCESS"},
        },
        "phone_label_determination": {
            "SIAK": "RATINGS_ONLY — no phone-level labels exist in train.csv "
                    "/ test.csv (verified column inspection)",
            "LWE": "VERDICTS_ONLY — human correct/incorrect + model outputs",
            "CMU_Kids": "WORDS_ONLY (not downloaded; no phone tier)",
            "conclusion": "NO corpus with defensible gold phone labels is "
                           "available. Supervised phone-head training (B1) "
                           "is UNSUPPORTABLE on current materials.",
        },
        "status": ("VERIFIED_COMPLETE" if not missing else "FAILED"),
    }
    with open(OUT / "dataset_manifest.json", "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
    print(json.dumps(manifest, indent=2)[:3000], flush=True)
    return manifest


if __name__ == "__main__":
    m = main()
    sys.exit(0 if m["status"] == "VERIFIED_COMPLETE" else 1)
