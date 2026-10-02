"""CMUdict canonical target generator. Child voice never defines target."""
from __future__ import annotations
import re
from pathlib import Path
from typing import Dict, List

from Research.Speech.Phase1_2.Adapters.paths import CMUDICT, arpa_list_to_ipa
from Research.Speech.Phase1_2.Schemas.speaking_schemas import SpeakingTarget


class CmuDictTargetAdapter:
    name = "cmudict"
    version = "cmudict.dict@2026-10-02"

    def __init__(self, path: Path = None):
        self.path = Path(path) if path else CMUDICT
        self._map: Dict[str, List[List[str]]] = {}
        self._load()

    def _load(self):
        if not self.path.exists():
            raise FileNotFoundError(f"CMUdict missing: {self.path}")
        with open(self.path, encoding="latin-1") as f:
            for line in f:
                if line.startswith(";;;") or not line.strip():
                    continue
                parts = line.strip().split()
                w = parts[0].lower()
                base = re.sub(r"\(\d+\)$", "", w)
                self._map.setdefault(base, []).append(parts[1:])

    def build(self, text: str) -> SpeakingTarget:
        norm = re.sub(r"[^a-zA-Z\s']", "", text).lower().strip()
        words = [w for w in norm.split() if w]
        arpa: List[str] = []
        variants_all: List[List[str]] = []
        warnings: List[str] = []
        stress: List[str] = []
        for w in words:
            entries = self._map.get(w)
            if not entries:
                warnings.append(f"missing_in_cmudict:{w}")
                continue
            primary = entries[0]
            arpa.extend(primary)
            variants_all.append(primary)
            if len(entries) > 1:
                for e in entries[1:]:
                    variants_all.append(e)
            for p in primary:
                m = re.search(r"(\d)$", p)
                stress.append(m.group(1) if m else "0")
        return SpeakingTarget(
            text=text,
            normalized_text=norm,
            arpabet=arpa,
            ipa=arpa_list_to_ipa(arpa),
            stress=stress,
            variants=variants_all,
            dict_version=self.version,
            warnings=warnings,
        )
