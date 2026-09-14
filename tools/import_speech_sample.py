#!/usr/bin/env python3
"""Import a curated real speech sample for the local-assessment benchmark.

Real child speech is the ONLY path to human validation (synthetic fixtures are
SIMULATED by label). This script guards that path:

- accepts ONLY 16-bit mono WAV (16 kHz preferred; refuses anything else),
- refuses files > 8 s (capture window) or empty/silent headers,
- REQUIRES an explicit --license text (public dataset license or collection
  consent note). No license = no import. Never commit samples whose license
  or consent is unclear (§21).
- appends {file, target, expectedDecision, source, license, notes} to
  Content/speech_samples/manifest.json (created on first import).
- audio bytes live ONLY in Content/speech_samples/ (gitignored — never
  committed; privacy: no raw voice in the repo, ever).

Usage:
  python tools/import_speech_sample.py --file /path/to/ball_01.wav \\
      --target ball --expected Pass --source "lab session 2026-09" \\
      --license "collected with parental consent 2026-09-14, lab-only use"

Expected decisions: StrongPass Pass Partial PossibleAttempt WrongWord
NoSpeech TooWeak Unclear (must match SpeakingDecision names).
"""
import json
import sys
import wave
from pathlib import Path

VALID_DECISIONS = {
    "StrongPass", "Pass", "Partial", "PossibleAttempt",
    "WrongWord", "NoSpeech", "TooWeak", "Unclear",
}

MAX_SEC = 8.0


def fail(msg):
    print(f"FAIL: {msg}")
    sys.exit(1)


def main():
    args = sys.argv[1:]
    get = {}
    i = 0
    while i < len(args):
        if args[i].startswith("--") and i + 1 < len(args):
            get[args[i][2:]] = args[i + 1]
            i += 2
        else:
            i += 1
    src = get.get("file")
    target = get.get("target")
    expected = get.get("expected")
    source = get.get("source", "")
    license_text = get.get("license", "")
    notes = get.get("notes", "")
    if not src:
        fail("--file is required (16-bit mono WAV)")
    if not target:
        fail("--target is required (WordId value, e.g. ball)")
    if expected not in VALID_DECISIONS:
        fail(f"--expected must be one of {sorted(VALID_DECISIONS)}")
    if not license_text.strip():
        fail("--license is required: dataset license or consent note. "
             "Unclear license/consent = no import, ever.")
    p = Path(src)
    if not p.is_file():
        fail(f"file not found: {src}")
    if p.suffix.lower() != ".wav":
        fail("only .wav is accepted (16-bit mono)")
    try:
        with wave.open(str(p), "rb") as w:
            nch = w.getnchannels()
            width = w.getsampwidth()
            rate = w.getframerate()
            nframes = w.getnframes()
    except Exception as e:
        fail(f"unreadable WAV: {e}")
    if nch != 1:
        fail(f"must be mono, got {nch} channels")
    if width != 2:
        fail("must be 16-bit")
    dur = nframes / max(1, rate)
    if dur <= 0 or dur > MAX_SEC:
        fail(f"duration {dur:.2f}s outside (0, {MAX_SEC}]s capture window")
    if rate != 16000:
        print(f"WARN: sample rate is {rate} Hz (engine runs 16 kHz; resample to 16000 first)")
    root = Path(__file__).resolve().parent.parent / "Content" / "speech_samples"
    root.mkdir(parents=True, exist_ok=True)
    dest = root / p.name
    if dest.exists():
        fail(f"already imported: {dest.name} (rename the file to re-import)")
    dest.write_bytes(p.read_bytes())
    manifest_path = root / "manifest.json"
    manifest = []
    if manifest_path.is_file():
        try:
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        except Exception as e:
            fail(f"existing manifest unreadable: {e}")
    manifest.append({
        "file": p.name,
        "target": target,
        "expectedDecision": expected,
        "source": source,
        "license": license_text,
        "notes": notes,
    })
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"OK: imported {p.name} ({dur:.2f}s, {rate} Hz mono16) -> Content/speech_samples/")
    print("Bytes are LOCAL ONLY (gitignored). Run EditMode to benchmark against manifest.")


if __name__ == "__main__":
    main()
