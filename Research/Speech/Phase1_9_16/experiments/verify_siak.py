"""Phase 1.9.16 SIAK integrity verification (no training).

Checks the re-downloaded release against:
  - 1.9.15 provenance.json input hashes (byte-identity with ASUS data),
  - expected counts (train 12,308 / test 4,000 / flac 16,308),
  - speaker-disjointness of train/test, duplicate audio, missing files.
Writes a JSON summary to stdout. Exits nonzero on FAILURE.
"""
from __future__ import annotations

import csv
import hashlib
import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
SIAK = REPO / "Research" / "Speech" / "ExternalData" / "SIAK"
PROV = (REPO / "Research" / "Speech" / "Phase1_9_15"
        / "manifests" / "provenance.json")

EXPECTED = {
    "Research/Speech/ExternalData/SIAK/train.csv":
        "0EE4F498E52947AF84587AD60600419E9F57BA3C4CECFD4DEC08763665D8A812",
    "Research/Speech/ExternalData/SIAK/test.csv":
        "AB98C9AD972536041336C846259A55753050E26D09CBB333ECAF7B70D156AF39",
    "Research/Speech/ExternalData/SIAK/README.md":
        "E2E38FF8DE3B7ADC599481846A8103ECE604918E4D5DDDCD0102E9A9BA6E3513",
}


def sha256(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest().upper()


def speaker_of(fname: str) -> str:
    # e.g. train001_fifi_07_....flac -> train001 ; test143_enuk_... -> test143
    return fname.split("_")[0]


def main() -> dict:
    out: dict = {"siak_dir": str(SIAK), "checks": {}}
    ok = True

    prov = json.loads(PROV.read_text(encoding="utf-8"))
    hash_ok = {}
    for rel, exp in EXPECTED.items():
        p = REPO / Path(*rel.split("/"))
        got = sha256(p) if p.exists() else "MISSING"
        match = (got == exp)
        hash_ok[rel] = {"match": match, "got": got[:16] + "..."}
        if not match:
            ok = False
    out["checks"]["provenance_hashes"] = hash_ok
    out["checks"]["provenance_byte_identical"] = all(
        v["match"] for v in hash_ok.values())

    def load_rows(name: str) -> list:
        with open(SIAK / name, newline="", encoding="utf-8") as f:
            return list(csv.DictReader(f))

    train = load_rows("train.csv")
    test = load_rows("test.csv")
    out["train_rows"] = len(train)
    out["test_rows"] = len(test)
    if len(train) != 12308 or len(test) != 4000:
        ok = False
    out["checks"]["row_counts"] = {
        "train": len(train) == 12308, "test": len(test) == 4000}

    flacs = sorted((SIAK / "flac").rglob("*.flac"))
    out["flac_count"] = len(flacs)
    if len(flacs) != 16308:
        ok = False
    out["checks"]["flac_count_16308"] = (len(flacs) == 16308)

    refs = [r["file"] for r in train + test]
    have = {f.name for f in flacs}
    missing = [r for r in refs if r not in have]
    out["missing_audio_refs"] = len(missing)
    out["missing_audio_sample"] = missing[:5]
    if missing:
        ok = False

    tr_spk = {speaker_of(r["file"]) for r in train}
    te_spk = {speaker_of(r["file"]) for r in test}
    out["train_speakers"] = len(tr_spk)
    out["test_speakers"] = len(te_spk)
    out["total_speakers"] = len(tr_spk | te_spk)
    leak = sorted(tr_spk & te_spk)
    out["split_speaker_overlap"] = leak
    if leak:
        ok = False
    out["checks"]["speaker_disjoint_splits"] = (len(leak) == 0)

    dup_ids = len(refs) - len(set(refs))
    out["duplicate_file_ids"] = dup_ids
    if dup_ids:
        ok = False

    # missing scores
    no_score = sum(1 for r in train + test if not r.get("score"))
    out["missing_scores"] = no_score
    if no_score:
        ok = False

    total_bytes = sum(f.stat().st_size for f in flacs)
    out["flac_bytes"] = total_bytes
    out["flac_mb"] = round(total_bytes / 1e6, 1)

    # corrupt-file probe: decode first/middle/last 20 flac via soundfile
    import soundfile as sf
    probe = (flacs[:7] + flacs[len(flacs)//2-3:len(flacs)//2+4]
             + flacs[-7:]) if flacs else []
    bad = []
    for f in probe:
        try:
            d, sr = sf.read(str(f))
            if len(d) == 0:
                bad.append(f.name)
        except Exception:
            bad.append(f.name)
    out["probe_n"] = len(probe)
    out["probe_corrupt"] = bad
    if bad:
        ok = False

    out["status"] = "VERIFIED_COMPLETE" if ok else "FAILED"
    print(json.dumps(out, indent=2))
    return out


if __name__ == "__main__":
    sys.exit(0 if main()["status"] == "VERIFIED_COMPLETE" else 1)
