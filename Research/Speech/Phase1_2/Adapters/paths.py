"""Shared paths / env for Phase 1.2 research (external caches, not git)."""
from pathlib import Path
import os

REPO = Path(__file__).resolve().parents[4]
PHASE11_AUDIO = REPO / "Research" / "Speech" / "Phase1_1" / "audio"
PHASE12 = REPO / "Research" / "Speech" / "Phase1_2"
RESULTS = PHASE12 / "Results"
SPEECH_LAB = Path(os.environ.get("LWE_SPEECH_LAB", r"D:\speech-lab"))
MODELS = SPEECH_LAB / "models"
VENV_PY = SPEECH_LAB / "venvs" / "p0" / "Scripts" / "python.exe"
CMUDICT = MODELS / "cmudict.dict"
MOONSHINE = MODELS / "sherpa-onnx-moonshine-tiny-en-int8"
HF_CACHE = MODELS

# ARPAbet (stress stripped) -> IPA approx for LWE scorer
ARPA2IPA = {
    "R": "ɹ", "EH": "ɛ", "D": "d", "AE": "æ", "P": "p", "AH": "ə", "L": "l",
    "K": "k", "T": "t", "B": "b", "UW": "uː", "IY": "iː", "AO": "ɔ", "W": "w",
    "N": "n", "Z": "z", "S": "s", "G": "ɡ", "F": "f", "V": "v", "M": "m",
    "IH": "ɪ", "UH": "ʊ", "AA": "ɑ", "EY": "eɪ", "OW": "oʊ", "AY": "aɪ",
    "AW": "aʊ", "OY": "ɔɪ", "CH": "tʃ", "JH": "dʒ", "SH": "ʃ", "TH": "θ",
    "DH": "ð", "NG": "ŋ", "HH": "h", "Y": "j", "ER": "ɝ", "IY0": "i",
}


def strip_stress(p: str) -> str:
    return "".join(c for c in p if not c.isdigit())


def arpa_list_to_ipa(arpa: list) -> list:
    out = []
    for p in arpa:
        base = strip_stress(p)
        out.append(ARPA2IPA.get(base, base.lower()))
    return out
