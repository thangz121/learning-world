#!/usr/bin/env python3
"""pregen_listen.py — generate the listening-pack audio LOCALLY (free, offline).

Source of truth: Content/listening/listen_garden.json (words + voice + rates).
Engine: Piper (MIT) voice en_US-libritts_r-medium (LibriTTS-R dataset, CC BY 4.0);
        model files live OUTSIDE the repo (D:\\speech-lab\\models\\piper); only the
        generated MP3s are committed (like every other pregen master).

Quality loop (self-checking foundation): for every word/mode, synthesize up to
N candidate takes and keep the FIRST take that faster-whisper transcribes back
to the exact word (normalized). Ties/failures keep the best-scoring take and
print a WARN — the pack is never silently wrong.

Output: Assets/StreamingAssets/audio/listen_<word>_<normal|slow>.mp3
        + Content/audio_manifest.json and the StreamingAssets mirror updated
        (ids 'listen_*' replaced idempotently).

Usage:
  python tools/pregen_listen.py          # generate + self-check + update manifests
  python tools/pregen_listen.py --tries 2
"""
from __future__ import annotations

import argparse
import json
import re
import sys
import tempfile
import wave
from pathlib import Path

import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[1]
PACK = REPO / "Content" / "listening" / "listen_garden.json"
SHIP = REPO / "Assets" / "StreamingAssets" / "audio"
CONTENT_MANIFEST = REPO / "Content" / "audio_manifest.json"
MIRROR = SHIP / "manifest.json"
PIPER_MODEL = Path(r"D:\speech-lab\models\piper\en_US-libritts_r-medium.onnx")
SPEAKER_ID = 100          # female LibriTTS-R speaker (F0 ~214 Hz)
TARGET_SR = 24000
NORMAL_LS = 1.0
SLOW_LS = 1.25
DEFAULT_TRIES = 6


def norm(s):
    return re.sub(r"[^a-z]", "", s.lower())


def word_entries(pack):
    out = []
    for w in pack["words"]:
        for mode, ls in (("normal", NORMAL_LS), ("slow", SLOW_LS)):
            out.append((w["id"], w["en"], mode, ls))
    return out


def _lev(a, b):
    if not a:
        return len(b)
    if not b:
        return len(a)
    prev = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        cur = [i]
        for j, cb in enumerate(b, 1):
            cur.append(min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + (ca != cb)))
        prev = cur
    return prev[-1]


def synth_take(word_id, text, mode, length_scale, voice, tmp: Path, take: int) -> Path:
    from piper import PiperVoice, SynthesisConfig  # noqa: F401
    import librosa
    cfg = SynthesisConfig(speaker_id=SPEAKER_ID, length_scale=length_scale)
    raw = tmp / f"raw_{word_id}_{mode}_{take}.wav"
    with wave.open(str(raw), "wb") as wf:
        voice.synthesize_wav(text, wf, syn_config=cfg)
    x, sr = sf.read(str(raw), dtype="float32")
    raw.unlink()
    if x.ndim > 1:
        x = x.mean(axis=1)
    # No aggressive trim: initial stops are low energy and a top_db trim clips
    # them (heard "and Anna"). Keep a 60 ms pad, normalize, resample to 24k.
    pad = int(0.06 * sr)
    x = np.concatenate([np.zeros(pad, dtype="float32"), x, np.zeros(pad, dtype="float32")])
    peak = float(np.max(np.abs(x))) or 1.0
    x = x / peak * 0.7
    x24 = librosa.resample(x, orig_sr=sr, target_sr=TARGET_SR)
    out = tmp / f"take_{word_id}_{mode}_{take}.mp3"
    sf.write(str(out), x24, TARGET_SR, format="MP3")
    return out


def transcribe(asr, path: Path, librosa):
    x, sr = sf.read(str(path), dtype="float32")
    x16 = librosa.resample(x, orig_sr=sr, target_sr=16000)
    segs, _ = asr.transcribe(x16, beam_size=1, language="en")
    return " ".join(s.text for s in segs).strip()


def pick_take(asr, librosa, voice, word_id, text, mode, ls, tmp: Path, tries: int, dst: Path):
    # Convergence cache: a committed take that already self-checks is kept as-is
    # (each run only re-rolls the entries that failed), so the pack converges.
    if dst.exists():
        heard = transcribe(asr, dst, librosa)
        if _lev(norm(heard), norm(text)) == 0:
            return dst, heard, -1, True
    best = None  # (dist, path, heard, take)
    for take in range(tries):
        p = synth_take(word_id, text, mode, ls, voice, tmp, take)
        heard = transcribe(asr, p, librosa)
        dist = _lev(norm(heard), norm(text))
        if dist == 0:
            return p, heard, take, True
        if best is None or dist < best[0]:
            best = (dist, p, heard, take)
    return best[1], best[2], best[3], False


def update_manifests(pack, generated):
    voice = pack["voice"]
    lang = pack["lang"]
    rate_n = pack["audioRateNormal"]
    rate_s = pack["audioRateSlow"]
    fresh = []
    for word_id, text, mode, _ls in generated:
        fresh.append({
            "id": f"listen_{word_id}_{mode}",
            "text": text,
            "voice": voice,
            "lang": lang,
            "rate": rate_n if mode == "normal" else rate_s,
            "pitch": 0.0,
            "style": "Clear",
            "format": "Mp3_44100",
            "file": f"listen_{word_id}_{mode}.mp3",
        })
    content = json.loads(CONTENT_MANIFEST.read_text(encoding="utf-8"))
    content = [e for e in content if not str(e.get("id", "")).startswith("listen_")]
    content.extend(fresh)
    CONTENT_MANIFEST.write_text(json.dumps(content, indent=2, ensure_ascii=False) + "\n",
                                encoding="utf-8")
    mirror = json.loads(MIRROR.read_text(encoding="utf-8"))
    entries = [e for e in (mirror.get("entries") or [])
               if not str(e.get("id", "")).startswith("listen_")]
    entries.extend(fresh)
    MIRROR.write_text(json.dumps({"entries": entries}, indent=2, ensure_ascii=False) + "\n",
                      encoding="utf-8")
    return fresh


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tries", type=int, default=DEFAULT_TRIES)
    args = ap.parse_args()
    pack = json.loads(PACK.read_text(encoding="utf-8"))
    generated = word_entries(pack)
    if not PIPER_MODEL.exists():
        print(f"FAIL: piper model missing at {PIPER_MODEL} (see the WP-1.9.32 report)")
        sys.exit(1)
    from piper import PiperVoice
    import librosa
    from faster_whisper import WhisperModel
    voice = PiperVoice.load(str(PIPER_MODEL))
    asr = WhisperModel("base.en", device="cpu", compute_type="int8")
    SHIP.mkdir(parents=True, exist_ok=True)
    passed = 0
    with tempfile.TemporaryDirectory(prefix="listen_tts_") as td:
        tmp = Path(td)
        for word_id, text, mode, ls in generated:
            dst = SHIP / f"listen_{word_id}_{mode}.mp3"
            p, heard, take, ok = pick_take(asr, librosa, voice, word_id, text, mode, ls,
                                           tmp, args.tries, dst)
            if p != dst:
                dst.write_bytes(p.read_bytes())
            passed += int(ok)
            flag = "OK " if ok else "WARN"
            origin = "cached" if take < 0 else f"take={take}"
            print(f"  {flag} listen_{word_id}_{mode:<6} {origin:<7} heard='{heard}' "
                  f"({dst.stat().st_size} bytes)")
    fresh = update_manifests(pack, generated)
    print(f"manifests updated: {len(fresh)} listen entries (Content + StreamingAssets mirror)")
    print(f"self-check: {passed}/{len(generated)} exact ASR matches")
    print("DONE pregen_listen")


if __name__ == "__main__":
    main()
