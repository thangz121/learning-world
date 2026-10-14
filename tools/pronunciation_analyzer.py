# -*- coding: utf-8 -*-
"""tools/pronunciation_analyzer.py — Phoneme-level Pronunciation Assessment & Kid-friendly Feedback."""
from __future__ import annotations

import difflib
import json
import os
import re
from pathlib import Path
from typing import Dict, List, Optional, Tuple

CMUDICT_PATH = Path(r"D:\speech-lab\models\cmudict.dict")

# Fallback in-memory dictionary for essential vocabulary
BUILTIN_CMU = {
    "apple": ["AE", "P", "AH", "L"],
    "ball": ["B", "AO", "L"],
    "banana": ["B", "AH", "N", "AE", "N", "AH"],
    "orange": ["AO", "R", "AH", "N", "JH"],
    "carrot": ["K", "AE", "R", "AH", "T"],
    "tomato": ["T", "AH", "M", "EY", "T", "OW"],
    "watermelon": ["W", "AO", "T", "ER", "M", "EH", "L", "AH", "N"],
    "grape": ["G", "R", "EY", "P"],
    "grapes": ["G", "R", "EY", "P", "S"],
    "red": ["R", "EH", "D"],
    "one": ["W", "AH", "N"],
    "please": ["P", "L", "IY", "Z"],
    "teddy": ["T", "EH", "D", "IY"],
    "cat": ["K", "AE", "T"],
    "dog": ["D", "AO", "G"],
    "fish": ["F", "IH", "SH"],
    "bird": ["B", "ER", "D"],
    "book": ["B", "UH", "K"],
}

PHONEME_HINTS = {
    "S": ("âm xì /s/", "Con nhớ xì nhẹ một tiếng 's' ở cuối từ nhé!"),
    "T": ("âm bật /t/", "Con nhớ bật nhẹ đầu lưỡi ra âm 't' ở cuối nhé!"),
    "K": ("âm bật /k/", "Con nhớ khựng nhẹ ở cổ họng ra âm 'k' nhé!"),
    "P": ("âm mím môi /p/", "Con mím nhẹ hai môi lại rồi bật ra tiếng 'p' nhé!"),
    "D": ("âm /d/", "Con chạm đầu lưỡi lên vòm họng phát âm 'd' nhé!"),
    "L": ("âm /l/", "Con cong nhẹ đầu lưỡi lên chạm vòm miệng nhé!"),
    "R": ("âm /r/", "Con hơi cong lưỡi vào trong một chút nhé!"),
    "JH": ("âm bật /dʒ/", "Con tròn môi và bật mạnh âm 'ch' nhé!"),
    "Z": ("âm rung /z/", "Con rung nhẹ đầu lưỡi tạo âm 'z' nhé!"),
    "AE": ("nguyên âm /æ/", "Con mở rộng miệng hình chữ A hơn một chút nhé!"),
    "AO": ("nguyên âm /ɔː/", "Con tròn miệng lại một chút nhé!"),
    "IY": ("nguyên âm /iː/", "Con kéo nhẹ khóe miệng sang hai bên như đang cười nhé!"),
    "EY": ("nguyên âm /eɪ/", "Con đọc lướt từ 'ê' sang 'i' thật tự nhiên nhé!"),
}

_CMUDICT_CACHE: Optional[Dict[str, List[str]]] = None


def load_cmudict() -> Dict[str, List[str]]:
    global _CMUDICT_CACHE
    if _CMUDICT_CACHE is not None:
        return _CMUDICT_CACHE

    d = dict(BUILTIN_CMU)
    if CMUDICT_PATH.exists():
        try:
            with open(CMUDICT_PATH, "r", encoding="utf-8") as f:
                for line in f:
                    if line.startswith(";;;"):
                        continue
                    parts = line.strip().split()
                    if not parts:
                        continue
                    word = re.sub(r"\(\d+\)$", "", parts[0]).lower()
                    phones = [re.sub(r"\d+", "", p) for p in parts[1:]]
                    if word not in d:
                        d[word] = phones
        except Exception:
            pass

    _CMUDICT_CACHE = d
    return d


def get_phonemes(word: str) -> List[str]:
    w = re.sub(r"[^a-z]", "", word.lower())
    cmu = load_cmudict()
    return cmu.get(w, [])


def analyze_pronunciation(target_word: str, spoken_transcript: str) -> dict:
    """Analyzes pronunciation between target word and spoken transcript."""
    target_phones = get_phonemes(target_word)
    spoken_clean = re.sub(r"[^a-z ]", "", spoken_transcript.lower()).strip()
    spoken_tokens = spoken_clean.split()

    if not spoken_tokens:
        return {
            "percent": 0,
            "level": "TryTogether",
            "feedback_vi": "Thử lại nhé!",
            "tip": "Con lắng nghe mẫu và nói to lên nhé.",
            "target_phonemes": target_phones,
            "spoken_phonemes": [],
            "error_phoneme": "",
            "error_type": "no_speech",
        }

    # Best match token against target
    best_spoken_phones = []
    best_ratio = 0.0

    for tok in spoken_tokens:
        tok_phones = get_phonemes(tok)
        if not tok_phones:
            continue
        sm = difflib.SequenceMatcher(None, target_phones, tok_phones)
        r = sm.ratio()
        if r > best_ratio:
            best_ratio = r
            best_spoken_phones = tok_phones

    if not best_spoken_phones and spoken_tokens:
        # Fallback to single token character comparison if not in dictionary
        best_spoken_phones = [ch.upper() for ch in spoken_tokens[0]]

    sm = difflib.SequenceMatcher(None, target_phones, best_spoken_phones)
    ratio = sm.ratio()
    percent = int(round(ratio * 100))

    error_phoneme = ""
    error_type = "none"
    tip = ""
    missing_ending = False
    if target_phones and len(target_phones) > 1 and target_phones[-1] not in best_spoken_phones:
        # Check if the spoken phones match the prefix of target phones
        prefix_len = len(target_phones) - 1
        prefix_matches = sum(1 for a, b in zip(target_phones[:prefix_len], best_spoken_phones) if a == b)
        if prefix_matches >= max(1, prefix_len - 1):
            missing_ending = True
            error_phoneme = target_phones[-1]
            error_type = "missing_ending"
            hint_name, hint_text = PHONEME_HINTS.get(
                error_phoneme, (f"âm đuôi /{error_phoneme}/", f"Con nhớ phát âm rõ âm đuôi {error_phoneme} nhé!")
            )
            tip = f"Con thiếu {hint_name}. {hint_text}"
            if percent > 75:
                percent = 75
            elif percent < 45:
                percent = 50  # Give fair credit for speaking the whole prefix

    if not missing_ending:
        matching_blocks = sm.get_matching_blocks()
        total_matched = sum(b.size for b in matching_blocks)
        if ratio < 0.45 or total_matched <= 1:
            return {
                "percent": int(round(ratio * 100)),
                "level": "TryTogether",
                "feedback_vi": "Thử lại nhé!",
                "tip": "Con lắng nghe mẫu và phát âm lại nhé.",
                "target_phonemes": target_phones,
                "spoken_phonemes": best_spoken_phones,
                "error_phoneme": "",
                "error_type": "wrong_word",
            }

    if percent >= 85 and not missing_ending:
        level = "Perfect"
        feedback_vi = "Xuất sắc! Con phát âm rất chuẩn."
    elif percent >= 65:
        level = "Great"
        feedback_vi = "Đúng rồi! Cố gắng thêm một chút nữa nhé."
    elif percent >= 40:
        level = "Almost"
        feedback_vi = "Gần đúng rồi!"
    else:
        level = "TryTogether"
        feedback_vi = "Thử lại nhé!"
        if not tip:
            tip = "Con lắng nghe mẫu rồi đọc lại nhé."

    if not missing_ending and percent < 85 and target_phones:
        # Check vowel or other phoneme differences
        for tag, i1, i2, j1, j2 in sm.get_opcodes():
            if tag in ("delete", "replace"):
                error_phoneme = target_phones[i1]
                error_type = "phoneme_mismatch"
                hint_name, hint_text = PHONEME_HINTS.get(
                    error_phoneme, (f"âm /{error_phoneme}/", f"Con chú ý âm {error_phoneme} nhé!")
                )
                tip = f"Chú ý {hint_name}: {hint_text}"
                break

    return {
        "percent": percent,
        "level": level,
        "feedback_vi": feedback_vi,
        "tip": tip,
        "target_phonemes": target_phones,
        "spoken_phonemes": best_spoken_phones,
        "error_phoneme": error_phoneme,
        "error_type": error_type,
    }


if __name__ == "__main__":
    import sys

    # Quick self-test
    res1 = analyze_pronunciation("grapes", "grape")
    print(f"grapes vs grape: {res1['percent']}% ({res1['level']}) -> {res1['tip']}")

    res2 = analyze_pronunciation("cat", "cat")
    print(f"cat vs cat: {res2['percent']}% ({res2['level']}) -> {res2['feedback_vi']}")

    res3 = analyze_pronunciation("apple", "orange")
    print(f"apple vs orange: {res3['percent']}% ({res3['level']}) -> {res3['feedback_vi']}")
