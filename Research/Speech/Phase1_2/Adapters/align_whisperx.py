"""WhisperX timing adapter — optional; skips cleanly if env/deps missing."""
from __future__ import annotations
from Research.Speech.Phase1_2.Schemas.speaking_schemas import (
    AlignmentResult, WordTiming, now)


class WhisperXAlignAdapter:
    name = "whisperx"
    version = "optional"

    def __init__(self, enabled: bool = False):
        self.enabled = enabled
        self._ok = None

    def available(self) -> bool:
        if not self.enabled:
            return False
        if self._ok is not None:
            return self._ok
        try:
            import whisperx  # noqa: F401
            self._ok = True
        except Exception:
            self._ok = False
        return self._ok

    def run(self, wav_path: str, language: str = "en") -> AlignmentResult:
        t0 = now()
        if not self.available():
            return AlignmentResult(
                words=[], processing_s=now() - t0, model=self.name,
                available=False,
                warnings=["whisperx_disabled_or_unavailable"])
        # Full whisperX path is heavy; Phase 1.2 default leaves it optional.
        return AlignmentResult(
            words=[], processing_s=now() - t0, model=self.name,
            available=False,
            warnings=["whisperx_not_invoked_in_default_pipeline"])
