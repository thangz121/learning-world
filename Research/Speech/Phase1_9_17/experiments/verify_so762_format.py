"""Phase 1.9.17 SpeechOcean762 audio-format verification (NO audio decode).

Streams the HF mirror with the audio column cast to decode=False, so the raw
stored WAVE bytes are read and their RIFF/`fmt ` headers parsed directly
(no torchcodec / soundfile dependency). Samples many files across both splits
to prove the format is uniform, and keeps 2 representative original OpenSLR
wav examples (one child, one adult) for the record.
"""
from __future__ import annotations

import io
import json
import struct
import wave
from collections import Counter
from pathlib import Path

OUT = Path(__file__).resolve().parents[1]
FORMAT_CAP_PER_SPLIT = 20
MAX_SCAN_PER_SPLIT = 500
KEEP = 2


def parse_wav_header(b: bytes) -> dict:
    """Parse a RIFF/WAVE header without decoding samples."""
    if b[:4] != b"RIFF" or b[8:12] != b"WAVE":
        return {"container": "NOT_RIFF_WAVE", "first16": repr(b[:16])}
    pos = 12
    info: dict = {"container": "RIFF/WAVE"}
    while pos + 8 <= len(b):
        cid = b[pos:pos + 4]
        (size,) = struct.unpack("<I", b[pos + 4:pos + 8])
        body = b[pos + 8:pos + 8 + size]
        if cid == b"fmt ":
            (fmt_tag, ch, rate, _br, _ba, bits) = struct.unpack("<HHIIHH", body[:16])
            info.update(audio_format=fmt_tag, channels=ch, sample_rate=rate,
                        bits_per_sample=bits)
            if size >= 18:
                (extra,) = struct.unpack("<H", body[16:18])
                info["fmt_extension_size"] = extra
        elif cid == b"data":
            info["data_bytes"] = size
        pos += 8 + size + (size & 1)
    # cross-check with the stdlib wave reader
    try:
        with wave.open(io.BytesIO(b), "rb") as w:
            info["wave_module"] = {
                "channels": w.getnchannels(), "sampwidth": w.getsampwidth(),
                "framerate": w.getframerate(), "nframes": w.getnframes(),
                "comptype": w.getcomptype(),
            }
    except Exception as e:  # noqa: BLE001
        info["wave_module_error"] = f"{type(e).__name__}: {e}"
    return info


def main() -> dict:
    from datasets import Audio, load_dataset
    rows = []
    kept: list[dict] = []
    total = 0
    per_split = {"train": 0, "test": 0}
    for split in ("train", "test"):
        ds = load_dataset("mispeech/speechocean762", split=split, streaming=True)
        ds = ds.cast_column("audio", Audio(decode=False))
        scanned = 0
        for ex in ds:
            scanned += 1
            total += 1
            per_split[split] += 1
            b = ex["audio"]["bytes"]
            hdr = parse_wav_header(b)
            hdr.update(split=split, speaker=ex["speaker"], age=ex["age"],
                       path=ex["audio"]["path"], bytes=len(b))
            if scanned <= FORMAT_CAP_PER_SPLIT:
                rows.append(hdr)
            if len(kept) < KEEP:
                have_child = any(k["age"] <= 15 for k in kept)
                have_adult = any(k["age"] > 15 for k in kept)
                if (ex["age"] <= 15 and not have_child) or \
                   (ex["age"] > 15 and not have_adult):
                    role = "child" if ex["age"] <= 15 else "adult"
                    kept.append(dict(hdr, role=role))
            if scanned >= FORMAT_CAP_PER_SPLIT and len(kept) >= KEEP:
                break
            if scanned >= MAX_SCAN_PER_SPLIT:
                break
    fmts = Counter(
        (r.get("container"), r.get("audio_format"), r.get("channels"),
         r.get("sample_rate"), r.get("bits_per_sample"))
        for r in rows
    )
    report = {
        "source": "mispeech/speechocean762 (mirror of OpenSLR SLR101)",
        "method": "audio column cast to decode=False; RIFF/`fmt ` header parsed; "
                  "stdlib wave cross-check; no sample decoding",
        "files_sampled": len(rows),
        "files_scanned_per_split": per_split,
        "distinct_formats": [
            {"container": c, "audio_format": a, "channels": ch,
             "sample_rate": r, "bits_per_sample": bits, "count": n}
            for (c, a, ch, r, bits), n in fmts.items()
        ],
        "expected_pipeline_input": {"sample_rate": 16000, "channels": 1},
        "format_uniform": len(fmts) == 1,
        "matches_pipeline_input": all(r.get("sample_rate") == 16000
                                      and r.get("channels") == 1 for r in rows),
        "examples": kept,
    }
    (OUT / "so762_format_probe.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    return report


if __name__ == "__main__":
    main()
