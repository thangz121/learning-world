"""OpenPronounce adapter — optional; degrades cleanly if unavailable."""
from __future__ import annotations
import json
import os
import subprocess
import tempfile
from pathlib import Path

from Research.Speech.Phase1_2.Schemas.speaking_schemas import (
    PronunciationResult, PhonemeDiagnostic, now)


class OpenPronounceAdapter:
    name = "openpronounce"
    version = "0.3.0"

    def __init__(self, cli_path: str = None):
        self.cli_path = cli_path
        self._available = None
        self._resolved = None

    def _resolve(self) -> str:
        if self._resolved is not None:
            return self._resolved
        import shutil
        from pathlib import Path
        candidates = []
        if self.cli_path:
            candidates.append(self.cli_path)
        which = shutil.which("openpronounce")
        if which:
            candidates.append(which)
        # known speech-lab venv
        candidates.append(r"D:\speech-lab\venvs\p0\Scripts\openpronounce.exe")
        for c in candidates:
            if c and Path(c).exists():
                self._resolved = c
                return c
        self._resolved = ""
        return ""

    def available(self) -> bool:
        if self._available is not None:
            return self._available
        self._available = bool(self._resolve())
        return self._available

    def run(self, wav_path: str, target_text: str) -> PronunciationResult:
        t0 = now()
        cli = self._resolve()
        if not cli:
            return PronunciationResult(
                source=self.name, score_0_100=0.0, confidence_0_1=0.0, per=None,
                diagnostics=[], processing_s=now() - t0,
                warnings=["openpronounce_cli_not_found"])
        env = os.environ.copy()
        env["PYTHONIOENCODING"] = "utf-8"
        env["PATH"] = env.get("PATH", "") + r";C:\Program Files\eSpeak NG"
        try:
            proc = subprocess.run(
                [cli, "--json", "--no-prosody", str(wav_path), target_text],
                capture_output=True, timeout=180, env=env)
            raw = (proc.stdout or b"").decode("utf-8", errors="replace").strip()
            # strip BOM
            if raw.startswith("\ufeff"):
                raw = raw.lstrip("\ufeff")
            # find JSON object
            start = raw.find("{")
            end = raw.rfind("}")
            if start < 0 or end < 0:
                return PronunciationResult(
                    source=self.name, score_0_100=0.0, confidence_0_1=0.0, per=None,
                    diagnostics=[], processing_s=now() - t0,
                    warnings=[f"no_json_output:rc={proc.returncode}"],
                    raw={"stdout": raw[:500], "stderr": (proc.stderr or b"").decode("utf-8", errors="replace")[:500]})
            data = json.loads(raw[start:end + 1])
            score = float(data.get("score", 0.0))
            # OpenPronounce does not always expose separate confidence; use
            # inverse-normalized distance as a soft confidence proxy and label it.
            dist = data.get("distance")
            conf = None
            warnings = ["confidence_is_distance_proxy"]
            if isinstance(dist, (int, float)):
                # distance ~500 correct, ~800+ wrong in Phase 1.1 samples
                conf = max(0.0, min(1.0, 1.0 - (float(dist) - 400.0) / 600.0))
            else:
                conf = score / 100.0
                warnings = ["confidence_copied_from_score_normalized"]
            return PronunciationResult(
                source=self.name,
                score_0_100=round(score, 2),
                confidence_0_1=round(float(conf), 3),
                per=None,
                diagnostics=[],
                processing_s=now() - t0,
                raw=data,
                warnings=warnings,
            )
        except Exception as e:
            return PronunciationResult(
                source=self.name, score_0_100=0.0, confidence_0_1=0.0, per=None,
                diagnostics=[], processing_s=now() - t0,
                warnings=[f"openpronounce_error:{type(e).__name__}:{e}"])
