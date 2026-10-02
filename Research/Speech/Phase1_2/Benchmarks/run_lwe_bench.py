"""Run LWE Phase 1.2 end-to-end bench on Phase 1.1 corpus."""
from __future__ import annotations
import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_2.Adapters.paths import PHASE11_AUDIO, RESULTS

CASES = [
    ("sapi_red.wav", "red"),
    ("sapi_blue.wav", "blue"),
    ("sapi_cat.wav", "cat"),
    ("sapi_red_apple.wav", "red apple"),
    ("pregen_apple_normal.wav", "apple"),
    ("stress_silence.wav", "red"),
    ("stress_noise.wav", "red"),
    ("sapi_big.wav", "big"),
    ("sapi_book.wav", "book"),
    ("sapi_dog.wav", "dog"),
]


def main():
    RESULTS.mkdir(parents=True, exist_ok=True)
    pipe = SpeakingPipeline(enable_openpronounce=True, enable_whisperx=False)
    rows = []
    for fn, tgt in CASES:
        wav = PHASE11_AUDIO / fn
        if not wav.exists():
            rows.append({"file": fn, "error": "missing"})
            print("MISS", fn, flush=True)
            continue
        r = pipe.run(str(wav), tgt, population_label="adult-tts-or-synthetic")
        out = RESULTS / f"lwe_{Path(fn).stem}.json"
        r.to_json(str(out))
        row = {
            "file": fn, "target": tgt,
            "asr": r.asr.text,
            "vad_speech": r.vad.speech_detected,
            "vad_segs": len(r.vad.segments),
            "primary_source": r.primary_source,
            "score": r.primary_score_0_100,
            "conf": r.primary_confidence_0_1,
            "s1": r.scores["scorer_v1"].score_0_100 if "scorer_v1" in r.scores else None,
            "s1_conf": r.scores["scorer_v1"].confidence_0_1 if "scorer_v1" in r.scores else None,
            "s1_per": r.scores["scorer_v1"].per if "scorer_v1" in r.scores else None,
            "op": r.scores["openpronounce"].score_0_100 if "openpronounce" in r.scores else None,
            "phones_n": len(r.phone.phones),
            "f0": r.acoustic.f0_mean,
            "total_s": r.total_processing_s,
            "warnings_n": len(r.warnings),
        }
        rows.append(row)
        print(
            f"LWE {fn:28s} tgt={tgt:12s} asr={r.asr.text!r:16s} "
            f"score={r.primary_score_0_100:6.1f} conf={r.primary_confidence_0_1:5.3f} "
            f"s1={row['s1']} per={row['s1_per']} T={r.total_processing_s:.2f}s",
            flush=True,
        )
    summary = RESULTS / "lwe_summary.json"
    summary.write_text(json.dumps(rows, indent=2), encoding="utf-8")
    print("WROTE", summary, flush=True)


if __name__ == "__main__":
    main()
