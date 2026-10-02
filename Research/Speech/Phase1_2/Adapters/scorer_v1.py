"""scorer-v1: CMUdict target + phone evidence + Levenshtein ops.
SCORE and CONFIDENCE are separate. Canonical target is external to speaker.
"""
from __future__ import annotations
from typing import List, Tuple
import numpy as np

from Research.Speech.Phase1_2.Schemas.speaking_schemas import (
    SpeakingTarget, PhoneEvidence, PronunciationResult, PhonemeDiagnostic, now,
    AcousticFeatures)


def _lev_ops(ref: List[str], hyp: List[str]):
    n, m = len(ref), len(hyp)
    dp = [[0] * (m + 1) for _ in range(n + 1)]
    for i in range(n + 1):
        dp[i][0] = i
    for j in range(m + 1):
        dp[0][j] = j
    for i in range(1, n + 1):
        for j in range(1, m + 1):
            cost = 0 if ref[i - 1] == hyp[j - 1] else 1
            dp[i][j] = min(dp[i - 1][j] + 1, dp[i][j - 1] + 1, dp[i - 1][j - 1] + cost)
    ops = []
    i, j = n, m
    while i > 0 or j > 0:
        if i > 0 and j > 0 and ref[i - 1] == hyp[j - 1] and dp[i][j] == dp[i - 1][j - 1]:
            ops.append(("eq", i - 1, j - 1, ref[i - 1], hyp[j - 1])); i -= 1; j -= 1
        elif i > 0 and j > 0 and dp[i][j] == dp[i - 1][j - 1] + 1:
            ops.append(("sub", i - 1, j - 1, ref[i - 1], hyp[j - 1])); i -= 1; j -= 1
        elif i > 0 and dp[i][j] == dp[i - 1][j] + 1:
            ops.append(("del", i - 1, -1, ref[i - 1], None)); i -= 1
        else:
            ops.append(("ins", -1, j - 1, None, hyp[j - 1])); j -= 1
    ops.reverse()
    return ops, dp[n][m]


class ScorerV1Adapter:
    name = "scorer-v1"
    version = "1.0.0-phase1.2"

    def run(self, target: SpeakingTarget, phone: PhoneEvidence,
            acoustic: AcousticFeatures = None,
            asr_text: str = None) -> PronunciationResult:
        t0 = now()
        ref = list(target.ipa) if target.ipa else []
        hyp = [p.phone for p in phone.phones]
        hyp_conf = [p.conf for p in phone.phones]
        warnings = list(target.warnings) + list(phone.warnings)

        if not ref:
            return PronunciationResult(
                source=self.name, score_0_100=0.0, confidence_0_1=0.0, per=1.0,
                diagnostics=[], processing_s=now() - t0,
                warnings=warnings + ["empty_target"])

        if not hyp:
            diags = [PhonemeDiagnostic(expected=r, observed=None, status="del",
                                       score=0.0, confidence=0.2, error_type="deletion")
                     for r in ref]
            return PronunciationResult(
                source=self.name, score_0_100=0.0, confidence_0_1=0.0, per=1.0,
                diagnostics=diags, processing_s=now() - t0,
                warnings=warnings + ["no_phone_evidence"])

        ops, dist = _lev_ops(ref, hyp)
        n = max(1, len(ref))
        diags: List[PhonemeDiagnostic] = []
        conf_acc = []
        for op, ri, hj, rt, ht in ops:
            if op == "eq":
                c = hyp_conf[hj] if 0 <= hj < len(hyp_conf) else 0.5
                diags.append(PhonemeDiagnostic(
                    expected=rt, observed=ht, status="ok",
                    score=round(100.0 * c, 1), confidence=round(c, 3)))
                conf_acc.append(c)
            elif op == "sub":
                c = hyp_conf[hj] if 0 <= hj < len(hyp_conf) else 0.5
                # confident wrong phone → low score; uncertain → slightly higher residual
                diags.append(PhonemeDiagnostic(
                    expected=rt, observed=ht, status="sub",
                    score=round(20.0 * (1.0 - c), 1), confidence=round(c, 3),
                    error_type="substitution"))
                conf_acc.append(c)
            elif op == "del":
                diags.append(PhonemeDiagnostic(
                    expected=rt, observed=None, status="del",
                    score=0.0, confidence=0.3, error_type="deletion"))
                conf_acc.append(0.3)
            elif op == "ins":
                diags.append(PhonemeDiagnostic(
                    expected="*", observed=ht, status="ins",
                    score=0.0, confidence=hyp_conf[hj] if 0 <= hj < len(hyp_conf) else 0.3,
                    error_type="insertion"))

        phone_scores = [d.score for d in diags if d.status != "ins"]
        word = float(np.mean(phone_scores)) if phone_scores else 0.0
        per = dist / n
        conf = float(np.mean(conf_acc)) * (1.0 - min(1.0, per)) if conf_acc else 0.0

        # Light acoustic modulation: unvoiced whole-file reduces confidence only
        if acoustic is not None and acoustic.f0_voiced_frac is not None:
            if acoustic.f0_voiced_frac < 0.05 and word > 0:
                conf *= 0.5
                warnings.append("acoustic:near_unvoiced_conf_penalty")

        # ASR mismatch is a confidence signal, not a score override
        if asr_text is not None:
            at = "".join(ch for ch in asr_text.lower() if ch.isalpha())
            tt = "".join(ch for ch in target.normalized_text.lower() if ch.isalpha())
            if at and tt and at != tt and word > 50:
                conf *= 0.7
                warnings.append(f"asr_text_mismatch:{asr_text!r}")

        return PronunciationResult(
            source=self.name,
            score_0_100=round(word, 1),
            confidence_0_1=round(float(conf), 3),
            per=round(per, 3),
            diagnostics=diags,
            processing_s=now() - t0,
            raw={"ref_ipa": ref, "hyp_ipa": hyp, "dist": dist},
            warnings=warnings,
        )
