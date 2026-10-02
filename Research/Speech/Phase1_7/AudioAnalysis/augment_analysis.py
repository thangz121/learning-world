"""Augment IMG_0639 analysis with fixed-window ASR + multilingual LID probes + VAD forensics."""
from __future__ import annotations
import json
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
RES = REPO / "Research" / "Speech" / "Phase1_7" / "Results"
base = json.loads((RES / "IMG_0639_analysis.json").read_text(encoding="utf-8"))
fw = json.loads((RES / "faster_whisper_windows.json").read_text(encoding="utf-8"))
mw = json.loads((RES / "fixed_window_asr.json").read_text(encoding="utf-8"))

# language summary from faster-whisper tiny (WEAK evidence)
from collections import Counter
import numpy as np
langs = Counter(r["lang"] for r in fw)
base["language_probes"] = {
    "faster_whisper_tiny_windows_10s": {
        "lang_counts": dict(langs),
        "mean_lang_p_by_lang": {
            k: float(np.mean([r["lang_p"] for r in fw if r["lang"] == k])) for k in langs
        },
        "windows": fw,
        "reliability": "LOW — tiny model on continuous noisy/ambient audio; frequent empty text; mixed ja/zh/ko/en/vi with often low p",
    },
    "moonshine_en_only_fixed_8s": {
        "n_windows": len(mw),
        "n_nonempty": sum(1 for r in mw if r.get("asr")),
        "windows": mw,
        "reliability": "EN-only model; nonempty may be hallucination (repetition) or English-like content",
    },
}
base["vad_forensics"] = {
    "silero_default_threshold": "0 segments",
    "silero_threshold_0.1": "1 short segment ~1.3-1.7s",
    "silero_threshold_0.05": "1 segment ~0.7-1.7s",
    "energy_profile": "near-continuous rms~0.05-0.14 across 1s windows; no clear speech/silence gating",
    "failure_class": "VAD",
    "implication": "real-audio continuous energy defeats default Silero settings used on clean TTS benchmarks",
}
base["language_global"] = "UNKNOWN"
base["language_evidence_summary"] = (
    "No reliable LID. EN-only Moonshine mostly empty with few possibly-hallucinated English strings. "
    "faster-whisper tiny assigns mixed languages with generally low probability; one window tagged vi p~0.67 "
    "is insufficient to label the full file Vietnamese. Human listen required."
)
base["speaker_age_status"] = "UNKNOWN"
base["consent_status"] = "unknown"
base["research_roles"] = [
    "REAL_SPEAKER_UNKNOWN_AGE",
    "REAL_MICROPHONE_OR_DEVICE_RECORDING_CONDITION_SAMPLE",
    "VAD_STRESS_CASE_CONTINUOUS_ENERGY",
    "PIPELINE_REAL_AUDIO_FAILURE_DISCOVERY",
    "NOT_CANONICAL_REFERENCE",
    "NOT_GOLDEN_CHILD_VOICE",
    "NOT_ENGLISH_PRONUNCIATION_BENCHMARK_WITHOUT_KNOWN_TARGET",
    "POSSIBLE_MIXED_OR_NON_ENGLISH_CONTENT_UNCONFIRMED",
]
base["pronunciation_scoring"] = {
    "ran": False,
    "reason": "no authorized known English target; ASR hypotheses are not ground-truth targets",
}
base["git_policy"] = {
    "commit_original_mp3": False,
    "commit_derived_wav": False,
    "reason": "privacy_sensitive_real_audio",
}
(RES / "IMG_0639_analysis.json").write_text(json.dumps(base, ensure_ascii=False, indent=2), encoding="utf-8")
print("updated analysis", flush=True)
