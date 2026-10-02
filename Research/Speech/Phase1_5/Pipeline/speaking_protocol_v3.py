"""Speaking protocol v3: provenance + conflicts + soft primary (no ASR boost)."""
from __future__ import annotations
import argparse, json, sys, time
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_2.Schemas.speaking_schemas import PIPELINE_VERSION
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2
from Research.Speech.Phase1_4.PhoneInventory.inventory import VERSION as INV_VERSION

PROTOCOL = "lwe-speaking-protocol-1.5.0"
THR_DISAGREE = 30.0


def handle(req):
    t0 = time.perf_counter()
    audio, target = req.get("audio"), req.get("target")
    if not audio or not Path(audio).exists() or not target:
        return {"ok": False, "error": {"code": "INVALID_REQUEST"}, "protocol_version": PROTOCOL}
    pipe = SpeakingPipeline(enable_openpronounce=bool(req.get("enable_openpronounce", True)))
    pev = PhoneEvidenceV2()
    r = pipe.run(str(audio), str(target), population_label=req.get("population_label", "unspecified"))
    soft = pev.soft_match(str(audio), r.target.arpabet)
    soft_s = soft.soft_score_0_100 if r.vad.speech_detected else 0.0
    soft_c = soft.confidence_0_1 if r.vad.speech_detected else 0.0
    op = r.scores.get("openpronounce")
    op_ok = op and "not_found" not in "".join(op.warnings or [])
    op_s = op.score_0_100 if op_ok else None

    reasons = list(soft.conf_reasons)
    conflicts = []
    if not r.vad.speech_detected:
        reasons.append("VAD_NO_SPEECH")
    if op_ok and abs(soft_s - op_s) >= THR_DISAGREE:
        conflicts.append("PHONE_VS_OPENPRONOUNCE")
        reasons.append("OPENPRONOUNCE_DISAGREEMENT")
        reasons.append("CONFLICTING_EVIDENCE")
        score = round(min(soft_s, op_s), 1)
        conf = round(soft_c * 0.7, 3)
        src = "conflict_conservative_min"
    elif op_ok:
        score = round(0.5 * soft_s + 0.5 * op_s, 1)
        conf = soft_c
        src = "ensemble_mean"
    else:
        score, conf, src = soft_s, soft_c, "soft_v2_only"
    if score >= 80 and conf < 0.25:
        conflicts.append("HIGH_SCORE_LOW_CONFIDENCE")
    if soft.mean_posterior < 0.15 and r.vad.speech_detected:
        reasons.append("PHONE_POSTERIOR_LOW")

    return {
        "ok": True,
        "protocol_version": PROTOCOL,
        "pipeline_version": PIPELINE_VERSION + "+p15",
        "inventory_version": INV_VERSION,
        "recognized_text": r.asr.text,
        "score": score,
        "confidence": conf,
        "primary_source": src,
        "conflicts": sorted(set(conflicts)),
        "confidenceReasons": sorted(set(reasons)),
        "scoreEvidence": {
            "phoneEvidence": {"value": soft_s, "confidence": soft_c, "source": soft.model,
                              "mean_sim": soft.mean_sim, "mean_posterior": soft.mean_posterior},
            "openPronounceEvidence": {"value": op_s, "source": "openpronounce" if op_ok else None},
            "asrEvidence": {"value": r.asr.text, "role": "supporting_only_not_pronunciation_judge"},
            "vadEvidence": {"speech_detected": r.vad.speech_detected},
            "acousticEvidence": {"f0": r.acoustic.f0_mean, "f1": r.acoustic.f1_mean, "f2": r.acoustic.f2_mean},
            "scorerV1Evidence": {"value": r.scores["scorer_v1"].score_0_100},
        },
        "phonemeDiagnostics": [
            {"expectedPhone": h.expected, "observedEvidence": h.best_obs, "matchType": h.match_type,
             "score": round(100 * h.sim, 1), "confidence": round(h.posterior, 3),
             "start": round(h.start_s, 3), "end": round(h.end_s, 3),
             "similarity": round(h.sim, 3), "posterior": round(h.posterior, 3)}
            for h in soft.hits
        ],
        "asr_is_not_pronunciation_judge": True,
        "population_label": r.population_label,
        "timings": {"wall_s": round(time.perf_counter() - t0, 3)},
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--audio", required=True)
    ap.add_argument("--target", required=True)
    ap.add_argument("--out", default=r"D:\speech-lab\protocol_v3_last.json")
    args = ap.parse_args()
    resp = handle({"audio": args.audio, "target": args.target})
    Path(args.out).write_text(json.dumps(resp, ensure_ascii=False, indent=2), encoding="utf-8")
    print("OK" if resp.get("ok") else "ERR", "score=", resp.get("score"), "conf=", resp.get("confidence"),
          "src=", resp.get("primary_source"), "conflicts=", resp.get("conflicts"), flush=True)
    print("WROTE", args.out, flush=True)


if __name__ == "__main__":
    main()
