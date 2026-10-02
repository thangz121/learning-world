"""Silero VAD adapter."""
from __future__ import annotations
from typing import List
import wave
import struct
from pathlib import Path

from Research.Speech.Phase1_2.Schemas.speaking_schemas import (
    VadResult, VadSegment, now)


class SileroVadAdapter:
    name = "silero-vad"
    version = "6.2.3"

    def __init__(self):
        self._model = None

    def _ensure(self):
        if self._model is not None:
            return
        from silero_vad import load_silero_vad
        self._model = load_silero_vad()

    def run(self, wav_path: str) -> VadResult:
        import torch
        from silero_vad import get_speech_timestamps
        t0 = now()
        self._ensure()
        p = Path(wav_path)
        with wave.open(str(p), "rb") as w:
            assert w.getframerate() == 16000
            n = w.getnframes()
            pcm = [x / 32768.0 for x in struct.unpack("<" + "h" * n, w.readframes(n))]
        audio = torch.tensor(pcm)
        ts = get_speech_timestamps(audio, self._model, return_seconds=True)
        segs = [VadSegment(float(a["start"]), float(a["end"])) for a in ts]
        return VadResult(
            speech_detected=len(segs) > 0,
            segments=segs,
            duration_s=n / 16000.0,
            processing_s=now() - t0,
            model=f"{self.name}@{self.version}",
        )
