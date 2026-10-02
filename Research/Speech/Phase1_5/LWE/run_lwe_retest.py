"""LWE re-test across phases: 1.2/1.3/1.4/1.5 channels side-by-side."""
from __future__ import annotations
import json, sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.Adapters.paths import PHASE11_AUDIO
from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2

OUT = REPO / "Research" / "Speech" / "Phase1_5" / "Results"
OUT.mkdir(parents=True, exist_ok=True)

# historical anchors from reports
HIST = {
    "red": {"p12": 91.1, "p13": 91.1, "p14_soft": 100.0},
    "blue": {"p12": 11.8, "p13": 11.8, "p14_soft": 5.8},
    "apple": {"p12": 29.6, "p13": 29.6, "p14_soft": 75.2},
    "book": {"p12": 30.6, "p13": 30.6, "p14_soft": 66.7},
    "big": {"p12": 35.2, "p13": 35.2, "p14_soft": 33.3},
    "dog": {"p12": 54.8, "p13": 54.8, "p14_soft": 33.3},
    "cat": {"p12": 68.9, "p13": 68.9, "p14_soft": 100.0},
}

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
]


def main():
    pipe = SpeakingPipeline(enable_openpronounce=True)
    pev = PhoneEvidenceV2()
    rows = []
    for fn, w in CASES:
        r = pipe.run(str(PHASE11_AUDIO / fn), w)
        soft = pev.soft_match(str(PHASE11_AUDIO / fn), r.target.arpabet)
        soft_s = soft.soft_score_0_100 if r.vad.speech_detected else 0.0
        soft_c = soft.confidence_0_1 if r.vad.speech_detected else 0.0
        op = r.scores.get("openpronounce")
        op_s = op.score_0_100 if op and "not_found" not in "".join(op.warnings or []) else None
        key = w if w in HIST else w.split()[0]
        h = HIST.get(key, {})
        # p15 primary: soft; if OP conflict use conservative min for reporting candidate
        primary = soft_s
        reason = ["soft_v2_primary"]
        if op_s is not None and abs(soft_s - op_s) >= 30:
            primary = min(soft_s, op_s)
            reason = ["conflict_conservative_min"]
        elif op_s is not None:
            primary = round(0.5 * soft_s + 0.5 * op_s, 1)
            reason = ["agree_mean"]
        rows.append({
            "word": w, "file": fn, "asr": r.asr.text,
            "p12_s1": h.get("p12"), "p13_s1": h.get("p13"), "p14_soft": h.get("p14_soft"),
            "p15_soft": soft_s, "p15_op": op_s, "p15_primary": primary,
            "p15_conf": soft_c, "primary_rule": reason,
            "s1_now": r.scores["scorer_v1"].score_0_100,
        })
        print("LWE", w, "p14", h.get("p14_soft"), "p15_soft", soft_s, "op", op_s, "primary", primary, flush=True)
    (OUT / "lwe_phase_compare.json").write_text(json.dumps(rows, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
