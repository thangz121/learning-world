"""SpeakingResult / protocol v2 with soft evidence + structured confidence reasons."""
from __future__ import annotations
import argparse, json, sys, time
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_2.Schemas.speaking_schemas import PIPELINE_VERSION
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2
from Research.Speech.Phase1_4.PhoneInventory.inventory import VERSION as INV_VERSION

PROTOCOL_VERSION = "lwe-speaking-protocol-1.4.0"


def handle(req: dict) -> dict:
    t0 = time.perf_counter()
    audio, target = req.get("audio"), req.get("target")
    if not audio or not target:
        return {"ok": False, "error": {"code": "INVALID_REQUEST", "message": "audio+target required"},
                "protocol_version": PROTOCOL_VERSION}
    if not Path(audio).exists():
        return {"ok": False, "error": {"code": "AUDIO_NOT_FOUND", "message": str(audio)},
                "protocol_version": PROTOCOL_VERSION}

    enable_op = bool(req.get("enable_openpronounce", True))
    pipe = SpeakingPipeline(enable_openpronounce=enable_op)
    pev = PhoneEvidenceV2()
    pop = req.get("population_label", "unspecified")
    r = pipe.run(str(audio), str(target), population_label=pop)
    soft = pev.soft_match(str(audio), r.target.arpabet)

    # VAD gate on soft score
    if not r.vad.speech_detected:
        soft_score, soft_conf = 0.0, 0.0
        reasons = list(soft.conf_reasons) + ["VAD_NO_SPEECH"]
    else:
        soft_score, soft_conf = soft.soft_score_0_100, soft.confidence_0_1
        reasons = list(soft.conf_reasons)

    s1 = r.scores["scorer_v1"]
    op = r.scores.get("openpronounce")
    op_score = op.score_0_100 if op else None
    op_ok = op and "openpronounce_cli_not_found" not in (op.warnings or [])

    # primary: soft evidence (phone layer), not ASR
    primary_score = soft_score
    primary_conf = soft_conf
    primary_source = "soft_phone_v2"
    if op_ok:
        if abs(soft_score - op_score) > 30:
            reasons.append("CONFLICTING_EVIDENCE")
            primary_score = round(0.6 * soft_score + 0.4 * op_score, 1)
            primary_conf = round(primary_conf * 0.75, 3)
            primary_source = "ensemble_soft0.6_op0.4_disagreement"
        else:
            primary_score = round(0.5 * soft_score + 0.5 * op_score, 1)
            primary_source = "ensemble_soft_op_mean"

    if r.acoustic.f0_voiced_frac is not None and r.acoustic.f0_voiced_frac < 0.05 and r.vad.speech_detected:
        reasons.append("ACOUSTIC_UNSTABLE")
        primary_conf = round(primary_conf * 0.7, 3)

    reasons = sorted(set(reasons))

    return {
        "ok": True,
        "protocol_version": PROTOCOL_VERSION,
        "pipeline_version": PIPELINE_VERSION + "+soft_v2",
        "inventory_version": INV_VERSION,
        "recognized_text": r.asr.text,
        "score": primary_score,
        "confidence": primary_conf,
        "primary_source": primary_source,
        "score_channels": {
            "scorer_v1": s1.score_0_100,
            "soft_phone_v2": soft_score,
            "openpronounce": op_score,
        },
        "confidence_channels": {
            "scorer_v1": s1.confidence_0_1,
            "soft_phone_v2": soft_conf,
        },
        "confidenceReasons": reasons,
        "phonemeDiagnostics": [
            {
                "expectedPhone": h.expected,
                "observedEvidence": h.best_obs,
                "matchType": h.match_type,
                "score": round(100.0 * h.sim, 1),
                "confidence": round(h.posterior, 3),
                "similarity": round(h.sim, 3),
                "posterior": round(h.posterior, 3),
                "start": round(h.start_s, 3),
                "end": round(h.end_s, 3),
                "duration": round(h.end_s - h.start_s, 3),
                "topk": h.topk[:3],
                "evidenceSources": ["w2v2-espeak", "ctc_align", "soft_sim"],
            }
            for h in soft.hits
        ],
        "vad": {
            "speech_detected": r.vad.speech_detected,
            "segments": [{"start": s.start_s, "end": s.end_s} for s in r.vad.segments],
        },
        "alignment": {"method": soft.alignment_method},
        "acousticSummary": {
            "f0_mean": r.acoustic.f0_mean,
            "f1_mean": r.acoustic.f1_mean,
            "f2_mean": r.acoustic.f2_mean,
            "duration_s": r.acoustic.duration_s,
            "voiced_frac": r.acoustic.f0_voiced_frac,
        },
        "target": {
            "text": r.target.text,
            "arpabet": r.target.arpabet,
            "canon": [h.expected for h in soft.hits],
            "dict_version": r.target.dict_version,
        },
        "model_versions": {
            **r.model_versions,
            "soft_phone": soft.model,
            "inventory": INV_VERSION,
        },
        "warnings": r.warnings,
        "timings": {"total_s": round(time.perf_counter() - t0, 3),
                    "pipeline_s": r.total_processing_s,
                    "soft_s": soft.processing_s},
        "population_label": r.population_label,
        # explicit: ASR is supporting only
        "asr_is_not_pronunciation_judge": True,
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--audio")
    ap.add_argument("--target")
    ap.add_argument("--out")
    ap.add_argument("--enable-openpronounce", action="store_true", default=True)
    ap.add_argument("--no-openpronounce", action="store_true")
    args = ap.parse_args()
    req = {
        "audio": args.audio,
        "target": args.target,
        "enable_openpronounce": False if args.no_openpronounce else True,
        "population_label": "cli",
    }
    resp = handle(req)
    text = json.dumps(resp, ensure_ascii=False, indent=2)
    out = args.out or r"D:\speech-lab\protocol_v2_last.json"
    Path(out).write_text(text, encoding="utf-8")
    print("OK" if resp.get("ok") else "ERR",
          "score=", resp.get("score"), "conf=", resp.get("confidence"),
          "src=", resp.get("primary_source"),
          "asr=", (resp.get("recognized_text") or "")[:30], flush=True)
    print("WROTE", out, flush=True)


if __name__ == "__main__":
    main()
