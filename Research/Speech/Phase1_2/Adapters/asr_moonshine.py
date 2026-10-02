"""Moonshine-tiny ASR via sherpa-onnx (primary offline STT from Phase 1.1)."""
from __future__ import annotations
import wave
import struct
from pathlib import Path

from Research.Speech.Phase1_2.Adapters.paths import MOONSHINE
from Research.Speech.Phase1_2.Schemas.speaking_schemas import AsrResult, now


class MoonshineAsrAdapter:
    name = "moonshine-tiny-en-int8"
    version = "sherpa-onnx-1.13.8"

    def __init__(self, model_dir: Path = None):
        self.model_dir = Path(model_dir) if model_dir else MOONSHINE
        self._rec = None

    def _ensure(self):
        if self._rec is not None:
            return
        import sherpa_onnx
        d = self.model_dir
        self._rec = sherpa_onnx.OfflineRecognizer.from_moonshine(
            preprocessor=str(d / "preprocess.onnx"),
            encoder=str(d / "encode.int8.onnx"),
            uncached_decoder=str(d / "uncached_decode.int8.onnx"),
            cached_decoder=str(d / "cached_decode.int8.onnx"),
            tokens=str(d / "tokens.txt"),
            num_threads=4,
        )

    def run(self, wav_path: str) -> AsrResult:
        t0 = now()
        self._ensure()
        with wave.open(str(wav_path), "rb") as w:
            assert w.getframerate() == 16000
            n = w.getnframes()
            pcm = [x / 32768.0 for x in struct.unpack("<" + "h" * n, w.readframes(n))]
        st = self._rec.create_stream()
        st.accept_waveform(16000, pcm)
        self._rec.decode_stream(st)
        text = (st.result.text or "").strip()
        return AsrResult(
            text=text,
            language="en",
            confidence=None,  # moonshine via sherpa does not expose conf here
            processing_s=now() - t0,
            model=f"{self.name}@{self.version}",
            raw={"text": text},
        )
