"""Protocol v4 + Unity-facing offline process adapter (headless harness only)."""
from __future__ import annotations
import argparse, json, sys, time
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_2.Schemas.speaking_schemas import PIPELINE_VERSION
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2
from Research.Speech.Phase1_4.PhoneInventory.inventory import VERSION as INV_VERSION
from Research.Speech.Phase1_6.Alignment.word_aligner import WordAlignerV1

PROTOCOL = "lwe-speaking-protocol-1.6.0"
DISAGREE = 30.0


def handle(req: dict) -> dict:
    t0 = time.perf_counter()
    audio, target = req.get("audio"), req.get("target")
    if not audio or not target or not Path(audio).exists():
        return {"ok": False, "error": {"code": "INVALID_REQUEST"}, "protocol_version": PROTOCOL}

    enable_op = bool(req.get("enable_openpronounce", True))
    pipe = SpeakingPipeline(enable_openpronounce=enable_op)
    pev = PhoneEvidenceV2()
    aligner = WordAlignerV1()
    r = pipe.run(str(audio), str(target), population_label=req.get("population_label", "unspecified"))
    words = str(target).split()
    soft = pev.soft_match(str(audio), r.target.arpabet)
    soft_s = soft.soft_score_0_100 if r.vad.speech_detected else 0.0
    soft_c = soft.confidence_0_1 if r.vad.speech_detected else 0.0
    spans, ameta = ([], {})
    if r.vad.speech_detected:
        spans, ameta = aligner.align_words(str(audio), words)

    op = r.scores.get("openpronounce")
    op_ok = op and "not_found" not in "".join(op.warnings or [])
    op_s = op.score_0_100 if op_ok else None

    reasons = list(soft.conf_reasons)
    conflicts = []
    if not r.vad.speech_detected:
        reasons.append("VAD_NO_SPEECH")
    if op_ok and abs(soft_s - op_s) >= DISAGREE:
        conflicts.append("PHONE_VS_OPENPRONOUNCE")
        reasons.append("OPENPRONOUNCE_DISAGREEMENT")
        reasons.append("CONFLICTING_EVIDENCE")
        # Phase 1.6: still expose all ensemble candidates; primary = soft pending larger human-held-out
        # Do NOT silently min without labeling
        score = soft_s
        conf = round(soft_c * 0.75, 3)
        src = "soft_v2_primary_with_op_conflict_flagged"
    elif op_ok:
        score = round(0.5 * soft_s + 0.5 * op_s, 1)
        conf = soft_c
        src = "ensemble_mean"
    else:
        score, conf, src = soft_s, soft_c, "soft_v2_only"
    if score >= 80 and conf < 0.25:
        conflicts.append("HIGH_SCORE_LOW_CONFIDENCE")

    return {
        "ok": True,
        "protocol_version": PROTOCOL,
        "pipeline_version": PIPELINE_VERSION + "+p16",
        "inventory_version": INV_VERSION,
        "recognized_text": r.asr.text,
        "score": score,
        "confidence": conf,
        "primary_source": src,
        "confidenceSemantics": "research: soft conf related to phone posterior; calibrated P(|err|<=10) is adult-L2 only when conf_map present",
        "conflicts": sorted(set(conflicts)),
        "confidenceReasons": sorted(set(reasons)),
        "wordDiagnostics": [
            {"word": s.word, "start": round(s.start_s, 3), "end": round(s.end_s, 3),
             "score": s.soft_score, "confidence": s.soft_conf, "phones": s.phone_hits}
            for s in spans
        ],
        "phonemeDiagnostics": [
            {"expectedPhone": h.expected, "observedEvidence": h.best_obs, "matchType": h.match_type,
             "score": round(100 * h.sim, 1), "confidence": round(h.posterior, 3),
             "start": round(h.start_s, 3), "end": round(h.end_s, 3)}
            for h in soft.hits
        ],
        "scoreEvidence": {
            "phoneEvidence": {"value": soft_s, "confidence": soft_c, "source": soft.model},
            "openPronounceEvidence": {"value": op_s, "source": "openpronounce" if op_ok else None},
            "asrEvidence": {"value": r.asr.text, "role": "supporting_only_not_pronunciation_judge"},
            "vadEvidence": {"speech_detected": r.vad.speech_detected},
            "alignmentEvidence": {"method": ameta.get("method"), "n_words": len(spans)},
            "acousticEvidence": {"f0": r.acoustic.f0_mean, "f1": r.acoustic.f1_mean, "f2": r.acoustic.f2_mean},
            "ensembleCandidates": {
                "soft": soft_s, "op": op_s,
                "mean": round(0.5 * soft_s + 0.5 * op_s, 1) if op_s is not None else None,
                "min": round(min(soft_s, op_s), 1) if op_s is not None else None,
            },
        },
        "asr_is_not_pronunciation_judge": True,
        "population_label": r.population_label,
        "timings": {"wall_s": round(time.perf_counter() - t0, 3)},
    }


def main():
    ap = argparse.ArgumentParser(description="LWE Speaking Protocol 1.6 / Unity offline harness")
    ap.add_argument("--request", help="JSON request file")
    ap.add_argument("--audio")
    ap.add_argument("--target")
    ap.add_argument("--out", default=r"D:\speech-lab\protocol_v4_last.json")
    args = ap.parse_args()
    if args.request:
        req = json.loads(Path(args.request).read_text(encoding="utf-8"))
    else:
        req = {"audio": args.audio, "target": args.target}
    resp = handle(req)
    Path(args.out).write_text(json.dumps(resp, ensure_ascii=False, indent=2), encoding="utf-8")
    print("OK" if resp.get("ok") else "ERR", "score=", resp.get("score"), "conf=", resp.get("confidence"),
          "src=", resp.get("primary_source"), "words=", len(resp.get("wordDiagnostics") or []), flush=True)
    print("WROTE", args.out, flush=True)


if __name__ == "__main__":
    main()
