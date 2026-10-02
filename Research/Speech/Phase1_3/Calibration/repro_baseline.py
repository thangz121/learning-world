"""Reproduce Phase 1.2 LWE baseline anchors before any Phase 1.3 score changes."""
from __future__ import annotations
import json, sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_2.Adapters.paths import PHASE11_AUDIO

OUT = REPO / "Research" / "Speech" / "Phase1_3" / "Results"
OUT.mkdir(parents=True, exist_ok=True)

ANCHORS = [
    ("sapi_red.wav", "red", 91.1, 0.911),
    ("stress_silence.wav", "red", 0.0, 0.0),
    ("sapi_blue.wav", "blue", 11.8, 0.0),
]


def main():
    pipe = SpeakingPipeline(enable_openpronounce=False)
    rows = []
    for fn, tgt, exp_s, exp_c in ANCHORS:
        r = pipe.run(str(PHASE11_AUDIO / fn), tgt)
        s1 = r.scores["scorer_v1"]
        row = {
            "file": fn, "target": tgt,
            "score": s1.score_0_100, "conf": s1.confidence_0_1, "per": s1.per,
            "asr": r.asr.text,
            "exp_score": exp_s, "exp_conf": exp_c,
            "score_delta": round(s1.score_0_100 - exp_s, 2),
            "conf_delta": round(s1.confidence_0_1 - exp_c, 3),
        }
        rows.append(row)
        print("BASELINE", row, flush=True)
    (OUT / "phase12_baseline_repro.json").write_text(json.dumps(rows, indent=2), encoding="utf-8")
    print("WROTE", OUT / "phase12_baseline_repro.json", flush=True)


if __name__ == "__main__":
    main()
