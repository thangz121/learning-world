"""Stabilize OpenPronounce: resolve CLI, run JSON, compare to scorer-v1 on LWE set."""
from __future__ import annotations
import json, sys, shutil
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_2.Adapters.paths import PHASE11_AUDIO
from Research.Speech.Phase1_2.Adapters.openpronounce_adapter import OpenPronounceAdapter

OUT = REPO / "Research" / "Speech" / "Phase1_3" / "Results"
OUT.mkdir(parents=True, exist_ok=True)

CASES = [
    ("sapi_red.wav", "red"),
    ("sapi_blue.wav", "blue"),
    ("sapi_cat.wav", "cat"),
    ("pregen_apple_normal.wav", "apple"),
    ("stress_silence.wav", "red"),
]


def main():
    op = OpenPronounceAdapter()
    report = {
        "cli_available": op.available(),
        "cli_path": shutil.which("openpronounce"),
        "cases": [],
    }
    pipe = SpeakingPipeline(enable_openpronounce=True)
    for fn, tgt in CASES:
        r = pipe.run(str(PHASE11_AUDIO / fn), tgt)
        s1 = r.scores.get("scorer_v1")
        op_r = r.scores.get("openpronounce")
        comb = r.scores.get("combined")
        report["cases"].append({
            "file": fn, "target": tgt, "asr": r.asr.text,
            "s1_score": s1.score_0_100 if s1 else None,
            "s1_conf": s1.confidence_0_1 if s1 else None,
            "op_score": op_r.score_0_100 if op_r else None,
            "op_conf": op_r.confidence_0_1 if op_r else None,
            "op_warnings": op_r.warnings if op_r else None,
            "combined_score": comb.score_0_100 if comb else None,
            "combined_conf": comb.confidence_0_1 if comb else None,
            "primary": r.primary_source,
            "total_s": r.total_processing_s,
        })
        print("OP", fn, "s1", s1.score_0_100 if s1 else None,
              "op", op_r.score_0_100 if op_r else None,
              "warn", (op_r.warnings if op_r else None), flush=True)
    (OUT / "openpronounce_stabilize.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("WROTE", OUT / "openpronounce_stabilize.json", flush=True)


if __name__ == "__main__":
    main()
