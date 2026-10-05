"""Phase 1.9.16 external-material integrity verification (no training).

Reproducible counterpart to the ad-hoc probes used during recovery:
  - SO762 child phone-tier parquet (rows / speakers / child speakers / phone tier),
  - Zenodo-200495 english_children.zip (MD5 + extracted wav count),
  - base phone-model snapshot file set.

Reads only cached/extracted material (downloads nothing). Writes a JSON summary
to stdout and exits nonzero on FAILURE.
"""
from __future__ import annotations

import glob
import hashlib
import json
import os
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_2.Adapters.paths import HF_CACHE  # noqa: E402

SO762_REV = "06385584fad212b26134c656fdd3ccf9f093f33e"
SO762_EXPECTED = {"rows": 5000, "speakers": 250, "child_speakers_le15": 122}
ZENODO_DIR = REPO / "Research" / "Speech" / "ExternalData" / "zenodo_200495"
ZENODO_MD5 = "1a4fd6116554593324a0a493e44a1eea"
ZENODO_WAV_EXPECTED = 671
MODEL_REV = "2c733782da5604684829819a5eb744c193fe9398"
MODEL_SNAP_FILES = {
    "config.json", "preprocessor_config.json", "pytorch_model.bin",
    "special_tokens_map.json", "tokenizer_config.json", "vocab.json",
}


def md5(p: Path) -> str:
    h = hashlib.md5()
    with open(p, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def check_model() -> dict:
    base = Path(HF_CACHE)
    hits = glob.glob(
        str(base / "models--facebook--wav2vec2-xlsr-53-espeak-cv-ft"
            / "snapshots" / "*"))
    snap = sorted(h for h in hits if os.path.isdir(h))
    out = {"snapshots": [Path(s).name for s in snap],
           "pinned_revision": MODEL_REV}
    if not snap:
        out["status"] = "MISSING"
        return out
    pinned = [s for s in snap if Path(s).name == MODEL_REV]
    s = Path(pinned[0]) if pinned else Path(snap[-1])
    present = {p.name for p in s.iterdir() if p.is_file()}
    out["snapshot"] = str(s)
    out["missing_required_files"] = sorted(MODEL_SNAP_FILES - present)
    out["status"] = ("VERIFIED_COMPLETE" if not (MODEL_SNAP_FILES - present)
                     else "FAILED")
    return out


def check_so762() -> dict:
    import pyarrow.parquet as pq
    base = Path(os.environ.get("HF_HUB_CACHE",
                               Path.home() / ".cache" / "huggingface" / "hub"))
    snaps = glob.glob(str(base / "datasets--mispeech--speechocean762"
                          / "snapshots" / SO762_REV))
    if not snaps:
        snaps = [d for d in glob.glob(
            str(base / "datasets--mispeech--speechocean762" / "snapshots" / "*"))
            if os.path.isdir(d)]
    if not snaps:
        return {"status": "MISSING", "searched": str(base)}
    s = snaps[0]
    rows = 0
    speakers, child = set(), set()
    has_phone_tier = False
    for f in glob.glob(s + r"\**\*.parquet", recursive=True):
        t = pq.read_table(f)
        d = t.to_pydict()
        rows += t.num_rows
        for i in range(t.num_rows):
            speakers.add(str(d["speaker"][i]))
            a = d["age"][i]
            if a is not None and a <= 15:
                child.add(str(d["speaker"][i]))
        w = d["words"][0]
        if isinstance(w, list) and w and "phones-accuracy" in w[0]:
            has_phone_tier = True
    got = {"rows": rows, "speakers": len(speakers),
           "child_speakers_le15": len(child), "has_phone_tier": has_phone_tier}
    ok = (rows == SO762_EXPECTED["rows"]
          and len(speakers) == SO762_EXPECTED["speakers"]
          and len(child) == SO762_EXPECTED["child_speakers_le15"]
          and has_phone_tier)
    return {"snapshot": s, **got, "expected": SO762_EXPECTED,
            "status": "VERIFIED_COMPLETE" if ok else "FAILED"}


def check_zenodo() -> dict:
    zips = list(ZENODO_DIR.glob("*.zip")) if ZENODO_DIR.exists() else []
    if not zips:
        return {"status": "MISSING", "dir": str(ZENODO_DIR)}
    got = md5(zips[0])
    wavs = glob.glob(str(ZENODO_DIR / "**" / "*.wav"), recursive=True)
    ok = got == ZENODO_MD5 and len(wavs) == ZENODO_WAV_EXPECTED
    return {"zip": zips[0].name, "zip_md5": got, "expected_md5": ZENODO_MD5,
            "wav_count": len(wavs), "expected_wav": ZENODO_WAV_EXPECTED,
            "status": "VERIFIED_COMPLETE" if ok else "FAILED"}


def main() -> dict:
    out = {
        "base_model": check_model(),
        "so762_child_phone_tier": check_so762(),
        "zenodo_200495_age4_english": check_zenodo(),
    }
    out["status"] = ("VERIFIED_COMPLETE"
                     if all(v.get("status") == "VERIFIED_COMPLETE"
                            for v in out.values() if isinstance(v, dict))
                     else "FAILED")
    print(json.dumps(out, indent=2, ensure_ascii=True), flush=True)
    return out


if __name__ == "__main__":
    sys.exit(0 if main()["status"] == "VERIFIED_COMPLETE" else 1)
