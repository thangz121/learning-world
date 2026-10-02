"""OpenPronounce vs soft-v2 conflict analysis on LWE + sample SO762.
Evidence provenance + conflict detector rules (versioned thresholds).
"""
from __future__ import annotations
import json, sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_2.Adapters.paths import PHASE11_AUDIO
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2

OUT = REPO / "Research" / "Speech" / "Phase1_5" / "Results"
OUT.mkdir(parents=True, exist_ok=True)

# versioned thresholds (not tuned on SO762 test)
THR = {
    "version": "conflict-rules-v1.5.0",
    "HIGH_SCORE": 80.0,
    "LOW_SCORE": 40.0,
    "HIGH_CONF": 0.6,
    "LOW_CONF": 0.25,
    "DISAGREE_ABS": 30.0,
}

LWE = [
    ("sapi_red.wav", "red"),
    ("sapi_blue.wav", "blue"),
    ("pregen_apple_normal.wav", "apple"),
    ("sapi_book.wav", "book"),
    ("sapi_big.wav", "big"),
    ("sapi_dog.wav", "dog"),
    ("sapi_cat.wav", "cat"),
    ("sapi_red_apple.wav", "red apple"),
    ("stress_silence.wav", "red"),
    ("sapi_ball_normal.wav" if False else "pregen_ball_normal.wav", "ball"),
]


def detect_conflicts(soft_s, soft_c, op_s, asr, vad, reasons_in):
    conflicts = []
    if op_s is not None and abs(soft_s - op_s) >= THR["DISAGREE_ABS"]:
        conflicts.append("PHONE_VS_OPENPRONOUNCE")
    if soft_s >= THR["HIGH_SCORE"] and soft_c < THR["LOW_CONF"]:
        conflicts.append("HIGH_SCORE_LOW_CONFIDENCE")
    if soft_s <= THR["LOW_SCORE"] and soft_c >= THR["HIGH_CONF"]:
        conflicts.append("LOW_SCORE_HIGH_CONFIDENCE")
    if vad and soft_s < 20 and soft_c < 0.15:
        conflicts.append("CLEAN_OR_SPEECH_LOW_EVIDENCE")
    if not vad and soft_s > 5:
        conflicts.append("NO_VAD_BUT_NONEZERO_SCORE")  # should be gated
    asr_ok = bool(asr and asr.strip())
    # ASR match is informational only — never score boost
    if asr_ok and soft_s < 25:
        conflicts.append("PHONE_VS_ASR_INFORMATIONAL")
    return conflicts


def provenance(soft, op, r, soft_score, soft_conf):
    return {
        "phoneEvidence": {
            "value": soft_score, "confidence": soft_conf,
            "source": soft.model, "alignment": soft.alignment_method,
            "mean_sim": soft.mean_sim, "mean_posterior": soft.mean_posterior,
            "hits": [
                {"exp": h.expected, "obs": h.best_obs, "type": h.match_type,
                 "sim": round(h.sim, 3), "post": round(h.posterior, 3)}
                for h in soft.hits
            ],
        },
        "openPronounceEvidence": {
            "value": op.score_0_100 if op else None,
            "confidence": op.confidence_0_1 if op else None,
            "source": "openpronounce" if op else None,
            "warnings": op.warnings if op else ["not_run"],
        },
        "asrEvidence": {
            "value": r.asr.text, "confidence": r.asr.confidence,
            "source": r.asr.model,
            "role": "supporting_only_not_pronunciation_judge",
        },
        "vadEvidence": {
            "speech_detected": r.vad.speech_detected,
            "segments": len(r.vad.segments),
            "source": r.vad.model,
        },
        "acousticEvidence": {
            "f0": r.acoustic.f0_mean, "f1": r.acoustic.f1_mean, "f2": r.acoustic.f2_mean,
            "voiced_frac": r.acoustic.f0_voiced_frac, "source": r.acoustic.model,
        },
        "scorerV1Evidence": {
            "value": r.scores["scorer_v1"].score_0_100,
            "confidence": r.scores["scorer_v1"].confidence_0_1,
            "per": r.scores["scorer_v1"].per,
        },
    }


def main():
    pipe = SpeakingPipeline(enable_openpronounce=True)
    pev = PhoneEvidenceV2()
    rows = []
    matrix = {"both_high": [], "both_low": [], "soft_high_op_low": [], "soft_low_op_high": []}

    for fn, word in LWE:
        path = PHASE11_AUDIO / fn
        if not path.exists():
            continue
        r = pipe.run(str(path), word)
        soft = pev.soft_match(str(path), r.target.arpabet)
        soft_s = soft.soft_score_0_100 if r.vad.speech_detected else 0.0
        soft_c = soft.confidence_0_1 if r.vad.speech_detected else 0.0
        op = r.scores.get("openpronounce")
        op_s = op.score_0_100 if op and "openpronounce_cli_not_found" not in (op.warnings or []) else None
        confs = detect_conflicts(soft_s, soft_c, op_s, r.asr.text, r.vad.speech_detected, soft.conf_reasons)
        # matrix bucket
        if op_s is not None:
            sh, sl = soft_s >= 70, soft_s <= 40
            oh, ol = op_s >= 70, op_s <= 40
            if sh and oh:
                matrix["both_high"].append(word)
            elif sl and ol:
                matrix["both_low"].append(word)
            elif sh and ol:
                matrix["soft_high_op_low"].append(word)
            elif sl and oh:
                matrix["soft_low_op_high"].append(word)

        # ensemble options (evaluated, not auto-applied as truth)
        ensembles = {"soft_only": soft_s}
        if op_s is not None:
            ensembles["op_only"] = op_s
            ensembles["mean"] = round(0.5 * soft_s + 0.5 * op_s, 1)
            # confidence-weighted
            oc = op.confidence_0_1 if op else 0.5
            w = soft_c + oc
            ensembles["conf_weighted"] = round((soft_s * soft_c + op_s * oc) / w, 1) if w > 1e-6 else ensembles["mean"]
            if abs(soft_s - op_s) >= THR["DISAGREE_ABS"]:
                # rule: prefer lower score when conflict (conservative for kids learning)
                ensembles["rule_conservative_min"] = round(min(soft_s, op_s), 1)
                ensembles["rule_soft60_op40"] = round(0.6 * soft_s + 0.4 * op_s, 1)
            else:
                ensembles["rule_conservative_min"] = ensembles["mean"]
                ensembles["rule_soft60_op40"] = ensembles["mean"]

        row = {
            "file": fn, "target": word, "asr": r.asr.text,
            "soft": soft_s, "soft_conf": soft_c, "op": op_s,
            "s1": r.scores["scorer_v1"].score_0_100,
            "conflicts": confs,
            "conf_reasons": soft.conf_reasons + (["OPENPRONOUNCE_DISAGREEMENT"] if "PHONE_VS_OPENPRONOUNCE" in confs else []),
            "ensembles": ensembles,
            "provenance": provenance(soft, op, r, soft_s, soft_c),
            "thresholds": THR,
        }
        rows.append(row)
        print("CONF", word, "soft", soft_s, "op", op_s, "conflicts", confs, flush=True)

    (OUT / "conflicts_lwe.json").write_text(json.dumps({"matrix": matrix, "rows": rows, "thresholds": THR}, ensure_ascii=False, indent=2), encoding="utf-8")
    (OUT / "conflict_matrix.json").write_text(json.dumps(matrix, indent=2), encoding="utf-8")
    print("MATRIX", matrix, flush=True)


if __name__ == "__main__":
    main()
