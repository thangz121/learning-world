"""Speaker-invariance + wrong-target checks on existing corruption set."""
from __future__ import annotations
import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_2.Adapters.paths import PHASE11_AUDIO, RESULTS, REPO

CORRUPT_DIR = REPO / "Research" / "Speech" / "Phase1_1" / "Experiments"


def main():
    RESULTS.mkdir(parents=True, exist_ok=True)
    pipe = SpeakingPipeline(enable_openpronounce=False)  # faster; s1 only
    rows = []
    # baseline
    base = PHASE11_AUDIO / "sapi_red.wav"
    r = pipe.run(str(base), "red")
    rows.append({"case": "baseline_red", "score": r.primary_score_0_100,
                 "conf": r.primary_confidence_0_1, "per": r.scores["scorer_v1"].per,
                 "asr": r.asr.text})
    print("BASE", rows[-1], flush=True)
    # corruptions
    for p in sorted(CORRUPT_DIR.glob("corrupt_red_*.wav")):
        r = pipe.run(str(p), "red")
        rows.append({"case": p.stem, "score": r.primary_score_0_100,
                     "conf": r.primary_confidence_0_1, "per": r.scores["scorer_v1"].per,
                     "asr": r.asr.text})
        print("CORR", rows[-1], flush=True)
    # wrong target
    r = pipe.run(str(base), "blue")
    rows.append({"case": "wrong_target_blue", "score": r.primary_score_0_100,
                 "conf": r.primary_confidence_0_1, "per": r.scores["scorer_v1"].per,
                 "asr": r.asr.text})
    print("WRONG", rows[-1], flush=True)
    # silence
    r = pipe.run(str(PHASE11_AUDIO / "stress_silence.wav"), "red")
    rows.append({"case": "silence", "score": r.primary_score_0_100,
                 "conf": r.primary_confidence_0_1, "per": r.scores["scorer_v1"].per,
                 "asr": r.asr.text, "vad": r.vad.speech_detected})
    print("SIL", rows[-1], flush=True)
    out = RESULTS / "invariance_summary.json"
    out.write_text(json.dumps(rows, indent=2), encoding="utf-8")
    print("WROTE", out, flush=True)


if __name__ == "__main__":
    main()
