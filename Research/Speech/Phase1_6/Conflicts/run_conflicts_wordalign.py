"""Conflict resolution evaluation on LWE + optional SO762 sample.
Compares soft-only, OP-only, mean, min, max, conf-weighted — no magic ASR boost.
"""
from __future__ import annotations
import json, sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_2.Adapters.paths import PHASE11_AUDIO
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2
from Research.Speech.Phase1_6.Alignment.word_aligner import WordAlignerV1

OUT = REPO / "Research" / "Speech" / "Phase1_6" / "Results"
OUT.mkdir(parents=True, exist_ok=True)

CASES = [
    ("sapi_red.wav", "red"),
    ("sapi_blue.wav", "blue"),
    ("pregen_apple_normal.wav", "apple"),
    ("sapi_book.wav", "book"),
    ("sapi_big.wav", "big"),
    ("sapi_dog.wav", "dog"),
    ("sapi_cat.wav", "cat"),
    ("sapi_red_apple.wav", "red apple"),
    ("stress_silence.wav", "red"),
    ("pregen_ball_normal.wav", "ball"),
]


def classify_conflict(soft, op, asr, vad, hits):
    """Heuristic evidence-based classification — documented, not score rewrite."""
    if op is None:
        return "OP_UNAVAILABLE", "use_soft"
    d = abs(soft - op)
    if d < 15:
        return "AGREE", "mean"
    # large disagree
    # if soft has all miss and OP high -> soft_fail_possible
    miss_rate = sum(1 for h in hits if h.get("type") == "miss") / max(1, len(hits))
    if soft < 40 and op > 70 and miss_rate >= 0.5:
        return "SOFT_LIKELY_WEAK_PHONE_MODEL", "inspect_both_prefer_lower_for_safety_not_truth"
    if soft > 70 and op < 40:
        return "OP_LIKELY_STRICT_OR_FAIL", "inspect_both"
    if not asr and soft > 50:
        return "ASR_EMPTY_SOFT_NONZERO", "inspect_audio"
    if vad and soft < 20 and op < 30:
        return "BOTH_LOW", "agree_low"
    return "BOTH_UNCERTAIN", "no_auto_pick"


def main():
    pipe = SpeakingPipeline(enable_openpronounce=True)
    pev = PhoneEvidenceV2()
    aligner = WordAlignerV1()
    rows = []
    for fn, text in CASES:
        path = PHASE11_AUDIO / fn
        if not path.exists():
            continue
        words = text.split()
        r = pipe.run(str(path), text)
        soft_utt = pev.soft_match(str(path), r.target.arpabet)
        soft_s = soft_utt.soft_score_0_100 if r.vad.speech_detected else 0.0
        soft_c = soft_utt.confidence_0_1 if r.vad.speech_detected else 0.0
        op = r.scores.get("openpronounce")
        op_s = op.score_0_100 if op and "not_found" not in "".join(op.warnings or []) else None
        spans, meta = aligner.align_words(str(path), words) if r.vad.speech_detected else ([], {})
        hits = [{"type": h.match_type} for h in soft_utt.hits]
        ctype, action = classify_conflict(soft_s, op_s, r.asr.text, r.vad.speech_detected, hits)

        ensembles = {"soft": soft_s}
        if op_s is not None:
            ensembles.update({
                "op": op_s,
                "mean": round(0.5 * soft_s + 0.5 * op_s, 1),
                "min": round(min(soft_s, op_s), 1),
                "max": round(max(soft_s, op_s), 1),
            })
            w = soft_c + (op.confidence_0_1 or 0.5)
            ensembles["conf_weighted"] = round(
                (soft_s * soft_c + op_s * (op.confidence_0_1 or 0.5)) / max(1e-6, w), 1)

        rows.append({
            "file": fn, "target": text, "asr": r.asr.text,
            "soft": soft_s, "soft_conf": soft_c, "op": op_s,
            "ensembles": ensembles,
            "conflict_class": ctype, "suggested_action": action,
            "word_spans": [
                {"word": s.word, "t0": s.start_s, "t1": s.end_s,
                 "soft": s.soft_score, "conf": s.soft_conf, "phones": s.phone_hits}
                for s in spans
            ],
            "alignment_method": meta.get("method"),
            "note": "ensemble values are candidates; final policy requires held-out human eval on SO762",
        })
        print("C", text, "soft", soft_s, "op", op_s, "class", ctype, "words",
              [(s.word, s.start_s, s.end_s, s.soft_score) for s in spans], flush=True)

    (OUT / "conflicts_and_word_align_lwe.json").write_text(
        json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8")
    print("WROTE", OUT / "conflicts_and_word_align_lwe.json", flush=True)


if __name__ == "__main__":
    main()
