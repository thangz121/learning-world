"""Phase 1.9.13 SYSTEM 06 — run OpenPronounce (MIT, Wav2Vec2+DTW) on the LWE corpus.

Workarounds (documented, research-only):
- espeak-ng provided by `espeakng-loader` (phonemizer backend).
- gTTS reference replaced by local Windows SAPI WAVs (gTTS rate-limited; documented).
Isolated venv: D:\\speech-lab\\venvs\\op13
"""
from __future__ import annotations

import json
import os
import re
import sys
import time
from pathlib import Path

import numpy as np
import soundfile as sf

# --- espeak backend for phonemizer ---
import espeakng_loader

os.environ["PHONEMIZER_ESPEAK_LIBRARY"] = espeakng_loader.get_library_path()
os.environ["PHONEMIZER_ESPEAK_DATA_PATH"] = espeakng_loader.get_data_path()

from openpronounce import audio as op_audio  # noqa: E402
from openpronounce import phones as op_phones  # noqa: E402
from openpronounce import speech as op_speech  # noqa: E402

REPO = Path(r"D:\Vscode\little-world-english")
OUT = REPO / "Research/Speech/Phase1_9_13"
RAW = OUT / "RAW_OUTPUT/openpronounce"
REF = RAW / "ref"
RAW.mkdir(parents=True, exist_ok=True)
LWE = REPO / "Research/Speech/Phase1_1/audio"
ZEN = REPO / "Research/Speech/ExternalData/zenodo_200495/extracted/english_children/english_words_sentences"

REF_MAP = {
    "red": REF / "red.wav",
    "blue": REF / "blue.wav",
    "cat": REF / "cat.wav",
    "apple": REF / "apple.wav",
    "big": REF / "big.wav",
    "book": REF / "book.wav",
    "dog": REF / "dog.wav",
    "red apple": REF / "red_apple.wav",
    "four": REF / "four.wav",
    "one": REF / "one.wav",
    "eight": REF / "eight.wav",
}


def local_text2speech(text: str, lang: str | None = None):
    key = text.strip().lower()
    p = REF_MAP.get(key)
    if p is None or not p.exists():
        raise FileNotFoundError(f"no local reference for {text!r}")
    return str(p)


op_audio.text2speech = local_text2speech  # monkeypatch: gTTS -> local SAPI reference


def zen_child_file(child: str, word: str) -> Path | None:
    m = re.match(r"child_(\d+)", child)
    if not m:
        return None
    nn = m.group(1)
    for sp in ZEN.iterdir():
        if sp.name.startswith(nn + "_"):
            for mic in ("studio_mic", "port_mic", "nao_mic"):
                c = sp / mic / "numbers" / f"{word}.wav"
                if c.exists():
                    return c
    return None


CASES = []
for fn, target in [
    ("sapi_red.wav", "red"),
    ("sapi_blue.wav", "blue"),
    ("sapi_cat.wav", "cat"),
    ("pregen_apple_normal.wav", "apple"),
    ("sapi_big.wav", "big"),
    ("sapi_book.wav", "book"),
    ("sapi_dog.wav", "dog"),
    ("sapi_red_apple.wav", "red apple"),
]:
    p = LWE / fn
    if p.exists():
        CASES.append((f"adult_{target.replace(' ', '_')}", p, target, "01_CORRECT/10_NORMAL"))
CASES.append(("adult_red_vs_blue", LWE / "sapi_red.wav", "blue", "04_WRONG_WORD"))
for child, word, cond in [
    ("child_07", "four", "03_MISSING_FINAL_CONSONANT"),
    ("child_06", "four", "03_MISSING_FINAL_CONSONANT"),
    ("child_09", "four", "03_MISSING_FINAL_CONSONANT"),
    ("child_05", "four", "UNLABELED_CHILD"),
    ("child_07", "one", "01_CORRECT_child(final present)"),
    ("child_02", "eight", "01_CORRECT_child(final present)"),
]:
    p = zen_child_file(child, word)
    if p is not None:
        CASES.append((f"{child}_{word}", p, word, cond))
sil = RAW / "_silence_1s.wav"
sf.write(str(sil), np.zeros(16000, dtype=np.float32), 16000)
CASES.append(("silence_1s_vs_red", sil, "red", "06_SILENCE"))

print("phone model enabled:", op_phones.is_enabled(), "model:", getattr(op_phones, "MODEL_NAME", "?"), flush=True)

rows = []
for name, path, target, cond in CASES:
    t0 = time.time()
    try:
        sound = op_audio.load(str(path))
        pred = op_speech.compare_audio_with_text(sound, target)
        rt = time.time() - t0
        d = pred.get("differences", {}) if isinstance(pred, dict) else {}
        errors = []
        for e in (d.get("errors") or []):
            if isinstance(e, dict):
                errors.append({"word": e.get("word"), "expected": e.get("expected"), "actual": e.get("actual")})
        row = {
            "system": "openpronounce",
            "test_id": name,
            "audio_file": str(path),
            "target_text": target,
            "expected_condition": cond,
            "score": pred.get("score") if isinstance(pred, dict) else None,
            "transcription": d.get("transcribe"),
            "phoneme_error_rate": d.get("phoneme_error_rate"),
            "word_error_rate": d.get("word_error_rate"),
            "errors": errors,
            "feedback": pred.get("feedback") if isinstance(pred, dict) else None,
            "runtime_ms": int(rt * 1000),
        }
        rows.append(row)
        raw_out = {**row, "acoustic_distance": pred.get("acoustic_distance"), "distance": pred.get("distance")}
        (RAW / f"{name}.json").write_text(json.dumps(raw_out, indent=2, ensure_ascii=True), encoding="utf-8")
        print(f"OK {name:28s} score={row['score']} rt={row['runtime_ms']}ms per={row['phoneme_error_rate']} errors={len(errors)}", flush=True)
    except Exception as e:
        rt = time.time() - t0
        rows.append(
            {
                "system": "openpronounce",
                "test_id": name,
                "audio_file": str(path),
                "target_text": target,
                "expected_condition": cond,
                "score": None,
                "transcription": None,
                "phoneme_error_rate": None,
                "word_error_rate": None,
                "errors": [],
                "feedback": None,
                "runtime_ms": int(rt * 1000),
                "error": f"{type(e).__name__}: {e}",
            }
        )
        print(f"FAIL {name}: {type(e).__name__}: {e}", flush=True)

(RAW / "test_matrix_rows.json").write_text(json.dumps(rows, indent=2, ensure_ascii=True), encoding="utf-8")
print("DONE", len(rows), "cases")
