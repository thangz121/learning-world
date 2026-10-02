"""Offline SpeakingPipeline protocol — request/response schemas + CLI runner.
Unity can later shell this without importing research internals.
"""
from __future__ import annotations
import argparse, json, sys, time
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_2.Schemas.speaking_schemas import PIPELINE_VERSION

PROTOCOL_VERSION = "lwe-speaking-protocol-1.3.0"


def handle_request(req: dict) -> dict:
    t0 = time.perf_counter()
    audio = req.get("audio")
    target = req.get("target")
    if not audio or not target:
        return {
            "ok": False,
            "error": {"code": "INVALID_REQUEST", "message": "audio and target required"},
            "protocol_version": PROTOCOL_VERSION,
        }
    if not Path(audio).exists():
        return {
            "ok": False,
            "error": {"code": "AUDIO_NOT_FOUND", "message": str(audio)},
            "protocol_version": PROTOCOL_VERSION,
        }
    enable_op = bool(req.get("enable_openpronounce", False))
    pipe = SpeakingPipeline(enable_openpronounce=enable_op, enable_whisperx=False)
    pop = req.get("population_label", "unspecified")
    r = pipe.run(str(audio), str(target), population_label=pop)
    s1 = r.scores.get("scorer_v1")
    return {
        "ok": True,
        "protocol_version": PROTOCOL_VERSION,
        "pipeline_version": PIPELINE_VERSION,
        "recognized_text": r.asr.text,
        "score": r.primary_score_0_100,
        "confidence": r.primary_confidence_0_1,
        "primary_source": r.primary_source,
        "per": s1.per if s1 else None,
        "phonemes": [
            {
                "expected": d.expected,
                "observed": d.observed,
                "status": d.status,
                "score": d.score,
                "confidence": d.confidence,
                "error_type": d.error_type,
            }
            for d in (s1.diagnostics if s1 else [])
        ],
        "vad": {
            "speech_detected": r.vad.speech_detected,
            "segments": [{"start": s.start_s, "end": s.end_s} for s in r.vad.segments],
        },
        "acoustic": {
            "f0_mean": r.acoustic.f0_mean,
            "f1_mean": r.acoustic.f1_mean,
            "f2_mean": r.acoustic.f2_mean,
            "duration_s": r.acoustic.duration_s,
        },
        "target": {
            "text": r.target.text,
            "arpabet": r.target.arpabet,
            "ipa": r.target.ipa,
            "dict_version": r.target.dict_version,
        },
        "model_versions": r.model_versions,
        "warnings": r.warnings,
        "timings": {
            "total_s": r.total_processing_s,
            "wall_s": round(time.perf_counter() - t0, 3),
        },
        "population_label": r.population_label,
    }


def main():
    ap = argparse.ArgumentParser(description="LWE Speaking Protocol 1.3")
    ap.add_argument("--request", help="JSON request file")
    ap.add_argument("--audio", help="wav path")
    ap.add_argument("--target", help="target text")
    ap.add_argument("--out", help="write response JSON")
    ap.add_argument("--enable-openpronounce", action="store_true")
    args = ap.parse_args()
    if args.request:
        req = json.loads(Path(args.request).read_text(encoding="utf-8"))
    else:
        req = {
            "audio": args.audio,
            "target": args.target,
            "enable_openpronounce": args.enable_openpronounce,
            "population_label": "cli",
        }
    resp = handle_request(req)
    text = json.dumps(resp, ensure_ascii=False, indent=2)
    if args.out:
        Path(args.out).write_text(text, encoding="utf-8")
    # stdout may be cp1258; write ascii-safe summary line
    print("OK" if resp.get("ok") else "ERR",
          "score=", resp.get("score"), "conf=", resp.get("confidence"),
          "asr=", (resp.get("recognized_text") or "")[:40], flush=True)
    if args.out:
        print("WROTE", args.out, flush=True)
    else:
        Path(r"D:\speech-lab\protocol_last.json").write_text(text, encoding="utf-8")


if __name__ == "__main__":
    main()
