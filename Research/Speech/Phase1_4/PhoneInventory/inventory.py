"""Versioned phoneme inventory normalization.
Maps CMUdict ARPAbet / IPA / espeak-w2v2 symbols into a canonical phone set.
"""
from __future__ import annotations
from dataclasses import dataclass, asdict
from typing import Dict, List, Optional, Tuple
import json
import re
from pathlib import Path

VERSION = "phone-inventory-v1.4.0"


# ARPAbet (stress stripped) -> canonical IPA-like
ARPA_TO_CANON = {
    "AA": "ɑ", "AE": "æ", "AH": "ə", "AO": "ɔ", "AW": "aʊ", "AY": "aɪ",
    "B": "b", "CH": "tʃ", "D": "d", "DH": "ð", "EH": "ɛ", "ER": "ɝ",
    "EY": "eɪ", "F": "f", "G": "ɡ", "HH": "h", "IH": "ɪ", "IY": "iː",
    "JH": "dʒ", "K": "k", "L": "l", "M": "m", "N": "n", "NG": "ŋ",
    "OW": "oʊ", "OY": "ɔɪ", "P": "p", "R": "ɹ", "S": "s", "SH": "ʃ",
    "T": "t", "TH": "θ", "UH": "ʊ", "UW": "uː", "V": "v", "W": "w",
    "Y": "j", "Z": "z", "ZH": "ʒ", "SIL": "sil", "SPN": "sil",
}

# Unicode / espeak aliases -> canonical
ALIAS_TO_CANON = {
    "g": "ɡ", "r": "ɹ", "ɾ": "ɹ", "ɚ": "ɝ", "ɐ": "ə", "ɜ": "ɝ",
    "i": "iː", "iː": "iː", "ɪ": "ɪ", "u": "uː", "uː": "uː", "ʊ": "ʊ",
    "ɑː": "ɑ", "a": "æ", "aː": "ɑ", "o": "oʊ", "oː": "ɔ", "ɔː": "ɔ",
    "e": "ɛ", "ɛː": "ɛ", "æː": "æ", "y": "j", "ɹ": "ɹ", "ɡ": "ɡ",
    "t͡ʃ": "tʃ", "d͡ʒ": "dʒ", "ʧ": "tʃ", "ʤ": "dʒ",
    " ": "", "|": "", "<pad>": "", "[PAD]": "", "<s>": "", "</s>": "",
    "sil": "sil", "spn": "sil", "ʔ": "sil",
}


@dataclass
class MappingRecord:
    source: str
    normalized: str
    mapping_type: str  # exact | alias | arpa | drop | unknown
    lossy: bool
    reason: str


class PhonemeInventoryAdapter:
    version = VERSION

    def __init__(self):
        self.records: List[MappingRecord] = []

    def strip_stress(self, p: str) -> str:
        return re.sub(r"\d$", "", p)

    def normalize_symbol(self, sym: str, source_kind: str = "auto") -> str:
        if sym is None:
            return ""
        s = str(sym).strip()
        if not s:
            return ""
        # drop stress digits on ARPAbet-like
        s0 = self.strip_stress(s)
        if s0 in ARPA_TO_CANON and (source_kind in ("arpa", "auto") and s0.isupper() or s0 in ARPA_TO_CANON and s.isupper()):
            out = ARPA_TO_CANON[s0]
            self.records.append(MappingRecord(s, out, "arpa", False, "cmudict_arpa"))
            return out
        if s0.upper() == s0 and s0 in ARPA_TO_CANON:
            out = ARPA_TO_CANON[s0]
            self.records.append(MappingRecord(s, out, "arpa", False, "cmudict_arpa"))
            return out
        if s in ALIAS_TO_CANON:
            out = ALIAS_TO_CANON[s]
            self.records.append(MappingRecord(s, out, "alias" if out else "drop", bool(out == "" or out != s), "alias_table"))
            return out
        # length-mark variants
        s2 = s.replace("ː", "").replace(":", "")
        if s2 in ALIAS_TO_CANON:
            out = ALIAS_TO_CANON[s2]
            # preserve length if original had it and canon has long vowel pair
            if "ː" in s or ":" in s:
                if out in ("i", "u") or out in ("ɪ", "ʊ"):
                    pass
            self.records.append(MappingRecord(s, out, "alias", True, "length_strip_alias"))
            return out
        # already canonical?
        if s in ARPA_TO_CANON.values() or s in set(ALIAS_TO_CANON.values()):
            self.records.append(MappingRecord(s, s, "exact", False, "already_canon"))
            return s
        self.records.append(MappingRecord(s, s, "unknown", True, "passthrough"))
        return s

    def normalize_seq(self, seq: List[str], source_kind: str = "auto") -> List[str]:
        out = []
        for p in seq:
            n = self.normalize_symbol(p, source_kind)
            if n:
                out.append(n)
        return out

    def arpa_seq_to_canon(self, arpa: List[str]) -> List[str]:
        return self.normalize_seq(arpa, source_kind="arpa")


# Phoneme similarity groups for soft matching (not score bonuses — evidence weights)
# Same group = partial credit candidate; cross-group = distant
SIM_GROUPS = {
    "high_front": {"iː", "ɪ"},
    "mid_front": {"eɪ", "ɛ"},
    "low_front": {"æ"},
    "low_back": {"ɑ", "ʌ", "ə"},
    "mid_back": {"ɔ", "oʊ"},
    "high_back": {"uː", "ʊ"},
    "rhotic": {"ɝ", "ɹ"},
    "labial_stop": {"p", "b"},
    "alveolar_stop": {"t", "d"},
    "velar_stop": {"k", "ɡ"},
    "labiodental_fric": {"f", "v"},
    "dental_fric": {"θ", "ð"},
    "alveolar_fric": {"s", "z"},
    "postal_fric": {"ʃ", "ʒ"},
    "affricate": {"tʃ", "dʒ"},
    "nasal": {"m", "n", "ŋ"},
    "approx": {"w", "j", "l", "ɹ"},
    "sil": {"sil"},
}

# pairwise boost within known confusable pairs (0-1 similarity)
PAIR_SIM = {
    frozenset(["iː", "ɪ"]): 0.55,
    frozenset(["æ", "ɛ"]): 0.50,
    frozenset(["uː", "ʊ"]): 0.55,
    frozenset(["ɑ", "ʌ"]): 0.45,
    frozenset(["ɑ", "ɔ"]): 0.40,
    frozenset(["θ", "t"]): 0.35,
    frozenset(["θ", "s"]): 0.40,
    frozenset(["ð", "d"]): 0.35,
    frozenset(["ð", "z"]): 0.40,
    frozenset(["ɹ", "l"]): 0.35,
    frozenset(["v", "w"]): 0.40,
    frozenset(["v", "b"]): 0.45,
    frozenset(["ʃ", "s"]): 0.45,
    frozenset(["tʃ", "ʃ"]): 0.50,
    frozenset(["p", "b"]): 0.50,
    frozenset(["t", "d"]): 0.50,
    frozenset(["k", "ɡ"]): 0.50,
}


def phone_similarity(a: str, b: str) -> float:
    if not a or not b:
        return 0.0
    if a == b:
        return 1.0
    key = frozenset([a, b])
    if key in PAIR_SIM:
        return PAIR_SIM[key]
    # same group partial
    for g, members in SIM_GROUPS.items():
        if a in members and b in members:
            return 0.35
    return 0.0


def dump_mapping_table(path: Path):
    rows = []
    for k, v in sorted(ARPA_TO_CANON.items()):
        rows.append({"source": k, "normalized": v, "type": "arpa"})
    for k, v in sorted(ALIAS_TO_CANON.items()):
        rows.append({"source": k, "normalized": v, "type": "alias"})
    path.write_text(json.dumps({"version": VERSION, "mappings": rows,
                                "pair_sim": {str(sorted(k)): v for k, v in PAIR_SIM.items()}},
                               ensure_ascii=False, indent=2), encoding="utf-8")
