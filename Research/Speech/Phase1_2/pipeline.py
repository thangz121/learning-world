"""Headless end-to-end pronunciation pipeline (Phase 1.2).
No Unity. All stages inspectable. SCORE ≠ CONFIDENCE.
"""
from __future__ import annotations
from pathlib import Path
from typing import Dict, Optional
import json

from Research.Speech.Phase1_2.Adapters.vad_silero import SileroVadAdapter
from Research.Speech.Phase1_2.Adapters.asr_moonshine import MoonshineAsrAdapter
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
from Research.Speech.Phase1_2.Adapters.phone_w2v2 import Wav2Vec2PhoneAdapter
from Research.Speech.Phase1_2.Adapters.acoustic_parselmouth import ParselmouthAcousticAdapter
from Research.Speech.Phase1_2.Adapters.scorer_v1 import ScorerV1Adapter
from Research.Speech.Phase1_2.Adapters.openpronounce_adapter import OpenPronounceAdapter
from Research.Speech.Phase1_2.Adapters.align_whisperx import WhisperXAlignAdapter
from Research.Speech.Phase1_2.Schemas.speaking_schemas import (
    SpeakingResult, PronunciationResult, now, PIPELINE_VERSION)


class SpeakingPipeline:
    def __init__(self, enable_openpronounce: bool = True, enable_whisperx: bool = False):
        self.vad = SileroVadAdapter()
        self.asr = MoonshineAsrAdapter()
        self.target = CmuDictTargetAdapter()
        self.phone = Wav2Vec2PhoneAdapter()
        self.acoustic = ParselmouthAcousticAdapter()
        self.scorer_v1 = ScorerV1Adapter()
        self.openpronounce = OpenPronounceAdapter() if enable_openpronounce else None
        self.align = WhisperXAlignAdapter(enabled=enable_whisperx)
        self._loaded = False

    def run(self, wav_path: str, target_text: str,
            population_label: str = "adult-tts-or-synthetic") -> SpeakingResult:
        t_all = now()
        wav_path = str(wav_path)
        tgt = self.target.build(target_text)
        vad = self.vad.run(wav_path)
        asr = self.asr.run(wav_path)
        phone = self.phone.run(wav_path)
        align = self.align.run(wav_path)
        acoustic = self.acoustic.run(wav_path)

        scores: Dict[str, PronunciationResult] = {}
        s1 = self.scorer_v1.run(tgt, phone, acoustic=acoustic, asr_text=asr.text)
        scores["scorer_v1"] = s1

        if self.openpronounce is not None:
            op = self.openpronounce.run(wav_path, target_text)
            scores["openpronounce"] = op

        # Combined: average scores when both present and OP didn't hard-fail
        primary_source = "scorer_v1"
        primary_score = s1.score_0_100
        primary_conf = s1.confidence_0_1
        if "openpronounce" in scores and "openpronounce_cli_not_found" not in scores["openpronounce"].warnings \
                and "openpronounce_error" not in "".join(scores["openpronounce"].warnings) \
                and "no_json_output" not in "".join(scores["openpronounce"].warnings):
            op = scores["openpronounce"]
            # weighted: scorer_v1 phoneme-aware + OP global
            comb_score = 0.55 * s1.score_0_100 + 0.45 * op.score_0_100
            comb_conf = 0.55 * s1.confidence_0_1 + 0.45 * op.confidence_0_1
            # disagreement reduces confidence
            if abs(s1.score_0_100 - op.score_0_100) > 25:
                comb_conf *= 0.75
            comb = PronunciationResult(
                source="combined_v1_op",
                score_0_100=round(comb_score, 1),
                confidence_0_1=round(comb_conf, 3),
                per=s1.per,
                diagnostics=list(s1.diagnostics),
                processing_s=s1.processing_s + op.processing_s,
                raw={"s1": s1.score_0_100, "op": op.score_0_100},
                warnings=["combined_weighted_0.55_s1_0.45_op"] + (
                    ["score_disagreement>25"] if abs(s1.score_0_100 - op.score_0_100) > 25 else []),
            )
            scores["combined"] = comb
            primary_source = "combined"
            primary_score = comb.score_0_100
            primary_conf = comb.confidence_0_1

        warnings = []
        warnings.extend(tgt.warnings)
        warnings.extend(vad.warnings)
        warnings.extend(asr.warnings)
        warnings.extend(phone.warnings)
        warnings.extend(align.warnings)
        warnings.extend(acoustic.warnings)
        if not vad.speech_detected:
            warnings.append("vad:no_speech")

        return SpeakingResult(
            audio_path=wav_path,
            target=tgt,
            vad=vad,
            asr=asr,
            phone=phone,
            alignment=align,
            acoustic=acoustic,
            scores=scores,
            primary_score_0_100=primary_score,
            primary_confidence_0_1=primary_conf,
            primary_source=primary_source,
            pipeline_version=PIPELINE_VERSION,
            model_versions={
                "vad": self.vad.version,
                "asr": self.asr.version,
                "target": self.target.version,
                "phone": self.phone.version,
                "acoustic": self.acoustic.version,
                "scorer_v1": self.scorer_v1.version,
            },
            total_processing_s=round(now() - t_all, 3),
            warnings=warnings,
            population_label=population_label,
        )
