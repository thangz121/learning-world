"""Phase 1.9.16 speaker-disjoint split construction (no training).

Adopts the verified 1.9.15 speaker partition VERBATIM (same speakers, same seed)
so future adaptation results are directly comparable to the calibration baseline.
Verifies every listed file against the fresh MAYNODE SIAK copy, then writes
Research/Speech/Phase1_9_16/split_manifest.json. Exits nonzero on any mismatch.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
SIAK = REPO / "Research" / "Speech" / "ExternalData" / "SIAK"
SRC = (REPO / "Research" / "Speech" / "Phase1_9_15" / "artifacts" / "siak"
       / "split_manifest.json")
SPLIT_FILES = {
    "train": "calibration_train.csv",
    "validation": "calibration_valid.csv",
    "test": "calibration_test.csv",
    "ages46_external": "calibration_ages46.csv",
}
DEST = REPO / "Research" / "Speech" / "Phase1_9_16" / "split_manifest.json"


def main() -> dict:
    src = json.loads(SRC.read_text(encoding="utf-8"))
    art = (REPO / "Research" / "Speech" / "Phase1_9_15" / "artifacts" / "siak")
    have = {p.name for p in (SIAK / "flac").rglob("*.flac")}

    splits = {}
    missing: list = []
    all_files: list = []
    for key, fname in SPLIT_FILES.items():
        with open(art / fname, newline="", encoding="utf-8") as f:
            rows = list(csv.DictReader(f))
        speakers = sorted({r["speaker_id"] for r in rows})
        files = [r["file"] for r in rows]
        missing += [x for x in files if x not in have]
        all_files += files
        # cross-check speaker lists against the 1.9.15 manifest
        man_key = {"ages46_external": "external"}.get(key, key)
        man_spk = set(src["splits"][man_key]["speakers"])
        assert set(speakers) == man_spk, f"speaker drift in {key}"
        splits[key] = {
            "n_utterances": len(rows),
            "n_speakers": len(speakers),
            "speakers": speakers,
            "source_csv": f"Phase1_9_15/artifacts/siak/{fname}",
        }

    tr = set(splits["train"]["speakers"])
    va = set(splits["validation"]["speakers"])
    te = set(splits["test"]["speakers"])
    ext = set(splits["ages46_external"]["speakers"])
    overlap = sorted((tr & va) | (tr & te) | (va & te)
                     | ((tr | va | te) & ext))
    dup = len(all_files) - len(set(all_files))

    ok = (not missing and not overlap and dup == 0
          and splits["train"]["n_utterances"] == 2094
          and splits["test"]["n_utterances"] == 482)
    out = {
        "phase": "1.9.16",
        "inherits_partition_from": "Phase1_9_15 split_manifest.json (seed 1515)",
        "random_seed": 1515,
        "verified_against": "MAYNODE SIAK copy (byte-identical to ASUS inputs)",
        "splits": splits,
        "overlap": overlap,
        "duplicate_recording_ids": dup,
        "missing_files_in_maynode_copy": len(missing),
        "status": "VERIFIED_COMPLETE" if ok else "FAILED",
    }
    DEST.write_text(json.dumps(out, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in out.items() if k != "splits"},
                     indent=2))
    for n, s in splits.items():
        print(f"{n}: {s['n_utterances']} utt / {s['n_speakers']} spk")
    return out


if __name__ == "__main__":
    sys.exit(0 if main()["status"] == "VERIFIED_COMPLETE" else 1)
