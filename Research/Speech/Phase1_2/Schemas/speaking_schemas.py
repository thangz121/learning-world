"""Phase 1.2 research schemas — inspectable intermediate results.
Score and confidence are always separate. Child voice never defines target.
"""
from __future__ import annotations
from dataclasses import dataclass, field, asdict
from typing import Any, Dict, List, Optional
import json
import time


PIPELINE_VERSION = "phase1.2.0-2026-10-02"


@dataclass
class SpeakingTarget:
    text: str
    normalized_text: str
    arpabet: List[str]
    ipa: List[str] = field(default_factory=list)
    stress: List[str] = field(default_factory=list)
    variants: List[List[str]] = field(default_factory=list)
    dict_version: str = "cmudict.dict@2026-10-02"
    target_version: str = "1"
    reference_audio: Optional[str] = None
    warnings: List[str] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)


@dataclass
class VadSegment:
    start_s: float
    end_s: float
    conf: Optional[float] = None


@dataclass
class VadResult:
    speech_detected: bool
    segments: List[VadSegment]
    duration_s: float
    processing_s: float
    model: str
    warnings: List[str] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)


@dataclass
class AsrResult:
    text: str
    language: Optional[str]
    confidence: Optional[float]
    processing_s: float
    model: str
    raw: Dict[str, Any] = field(default_factory=dict)
    warnings: List[str] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)


@dataclass
class PhoneToken:
    phone: str
    conf: float
    start_frame: Optional[int] = None
    end_frame: Optional[int] = None


@dataclass
class PhoneEvidence:
    phones: List[PhoneToken]
    frame_mean_conf: float
    processing_s: float
    model: str
    inventory: str = "espeak-ipa"
    warnings: List[str] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)


@dataclass
class WordTiming:
    word: str
    start_s: float
    end_s: float


@dataclass
class AlignmentResult:
    words: List[WordTiming]
    processing_s: float
    model: str
    available: bool = True
    warnings: List[str] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)


@dataclass
class AcousticFeatures:
    f0_mean: Optional[float]
    f0_std: Optional[float]
    f0_voiced_frac: Optional[float]
    f1_mean: Optional[float]
    f2_mean: Optional[float]
    f3_mean: Optional[float]
    intensity_mean: Optional[float]
    duration_s: float
    processing_s: float
    model: str
    warnings: List[str] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)


@dataclass
class PhonemeDiagnostic:
    expected: str
    observed: Optional[str]
    status: str  # ok | sub | del | ins
    score: float  # 0-100 for this phone
    confidence: float  # 0-1
    error_type: Optional[str] = None
    start_s: Optional[float] = None
    end_s: Optional[float] = None
    acoustic: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)


@dataclass
class PronunciationResult:
    source: str  # openpronounce | scorer_v1 | combined
    score_0_100: float
    confidence_0_1: float
    per: Optional[float]
    diagnostics: List[PhonemeDiagnostic]
    processing_s: float
    raw: Dict[str, Any] = field(default_factory=dict)
    warnings: List[str] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)


@dataclass
class SpeakingResult:
    audio_path: str
    target: SpeakingTarget
    vad: VadResult
    asr: AsrResult
    phone: PhoneEvidence
    alignment: AlignmentResult
    acoustic: AcousticFeatures
    scores: Dict[str, PronunciationResult]  # keyed by source
    primary_score_0_100: float
    primary_confidence_0_1: float
    primary_source: str
    pipeline_version: str = PIPELINE_VERSION
    model_versions: Dict[str, str] = field(default_factory=dict)
    total_processing_s: float = 0.0
    warnings: List[str] = field(default_factory=list)
    population_label: str = "adult-tts-or-synthetic"  # never claim 4yo unless true

    def to_dict(self) -> Dict[str, Any]:
        d = asdict(self)
        d["scores"] = {k: v if isinstance(v, dict) else v.to_dict()
                       for k, v in self.scores.items()}
        return d

    def to_json(self, path: str) -> None:
        with open(path, "w", encoding="utf-8") as f:
            json.dump(self.to_dict(), f, ensure_ascii=False, indent=2)


def now() -> float:
    return time.perf_counter()
