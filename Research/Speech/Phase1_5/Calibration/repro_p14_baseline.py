"""Reproduce Phase 1.4 soft-v2 anchors before Phase 1.5 changes."""
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

# Phase 1.4 reported soft scores
ANCHORS = [
    ("sapi_red.wav", "red", 100.0),
    ("sapi_blue.wav", "blue", 5.8),
    ("pregen_apple_normal.wav", "apple", 75.2),
    ("stress_silence.wav", "red", 0.0),  # gated
]


def main():
    pipe = SpeakingPipeline(enable_openpronounce=False)
    pev = PhoneEvidenceV2()
    rows = []
    for fn, tgt, exp in ANCHORS:
        r = pipe.run(str(PHASE11_AUDIO / fn), tgt)
        soft = pev.soft_match(str(PHASE11_AUDIO / fn), r.target.arpabet)
        score = soft.soft_score_0_100 if r.vad.speech_detected else 0.0
        rows.append({
            "file": fn, "target": tgt, "exp_soft": exp,
            "soft": score, "delta": round(score - exp, 2),
            "s1": r.scores["scorer_v1"].score_0_100,
            "vad": r.vad.speech_detected, "asr": r.asr.text,
        })
        print("BASE", rows[-1], flush=True)
    (OUT / "phase14_baseline_repro.json").write_text(json.dumps(rows, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
