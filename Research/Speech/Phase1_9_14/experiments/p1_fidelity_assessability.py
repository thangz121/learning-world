"""Phase 1.9.14 — P1 fidelity / assessability evaluator (RESEARCH ONLY).

Two rule versions are evaluated side by side on the same evidence:
  v1 = literal first-cut translation of external taxonomy into our signals
       (ASR empty/wrong treated as target-validity evidence)
  v2 = child-safe revision (ASR is supporting evidence only; refusal requires
       converging acoustic evidence; pronunciation weakness never becomes a
       fidelity failure)

The v1 vs v2 comparison is the experiment: it tests whether a naive fidelity
layer converts "no evidence" into "wrong attempt" on real human-labeled child
speech (it does), and whether a conservative rule set avoids that.

Signals: audio duration, Silero segments/speech ratio, RMS, ZCR, existing
pronunciation confidence/score, ASR status/text, expected target, human labels
from 1.9.9/1.9.10/1.9.11/1.9.12 (labels used only for evaluation, never as rule
inputs).

Outputs (derived only; no production change):
  artifacts/p1/p1_evidence_table.csv
  artifacts/p1/p1_state_by_human.csv          (v2)
  artifacts/p1/p1_v1_vs_v2_summary.json
  artifacts/p1/p1_cases.csv                   (per labeled case with states)
  manifests/p1_provenance.json
"""
from __future__ import annotations

import csv
import hashlib
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
P198 = REPO / "Research/Speech/Phase1_9_8/Results"
P199 = REPO / "Research/Speech/Phase1_9_9/Results"
P1910 = REPO / "Research/Speech/Phase1_9_10/Results"
P1911 = REPO / "Research/Speech/Phase1_9_11/Results"
P1912 = REPO / "Research/Speech/Phase1_9_12/Results"
OUT = REPO / "Research/Speech/Phase1_9_14"

CORRECT = {"CLEAR_CORRECT", "PROBABLY_CORRECT", "HUMAN_CONFIRMED_CORRECT"}
INCORRECT = {"CLEAR_INCORRECT", "PROBABLY_INCORRECT", "HUMAN_CONFIRMED_ERROR", "HUMAN_TRUE_ERROR"}
UNCERTAIN = {"AMBIGUOUS", "HUMAN_UNCERTAIN", "HUMAN_CONFLICTED", "UNCERTAIN"}

VAD_RATIO_FLOOR = 0.05
MIN_DUR_S = 0.15
RMS_NOISE_FLOOR = 0.002
RMS_ENERGY_PRESENT = 0.01
CONF_VALID = 0.05
CONF_POSSIBLE = 0.005

ALL_STATES = ["NO_SPEECH", "UNINTELLIGIBLE", "INCOMPLETE", "FREE_SPEAK",
              "POSSIBLE_ATTEMPT", "VALID_ATTEMPT", "ASSESSABLE"]
REFUSAL_STATES = {"NO_SPEECH", "UNINTELLIGIBLE", "FREE_SPEAK", "INCOMPLETE"}


def sha256_file(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def read_csv(p: Path):
    return list(csv.DictReader(p.open(encoding="utf-8-sig")))


def fnum(v, default=None):
    try:
        return float(v)
    except (TypeError, ValueError):
        return default


def load_labels():
    labels: dict[tuple, dict] = {}
    for r in read_csv(P199 / "human_review_results.csv"):
        labels[(r["speaker_id"], r["target"])] = {
            "verdict": r["human_pronunciation"], "verdict_source": "1.9.9",
            "audio_quality": r["audio_quality"], "boundary": r["boundary_label"],
            "review_confidence": r["review_confidence"], "diagnostic_199": r["diagnostic"],
        }
    for r in read_csv(P1910 / "human_second_pass.csv"):
        labels.setdefault((r["speaker_id"], r["target"]), {}).update(
            {"verdict": r["stage_a_pronunciation"], "verdict_source": "1.9.10",
             "human_state": r["human_state"], "certainty": r["final_human_certainty"]})
    for r in read_csv(P1911 / "scorer_miss_human_review.csv"):
        v = {"HUMAN_TRUE_ERROR": "CLEAR_INCORRECT", "HUMAN_CONFLICTED": "AMBIGUOUS",
             "HUMAN_UNCERTAIN": "AMBIGUOUS"}.get(r["human_state"], r["stage_a_pronunciation"])
        labels.setdefault((r["speaker_id"], r["target"]), {}).update(
            {"verdict": v, "verdict_source": "1.9.11_scorer_miss", "human_state": r["human_state"]})
    for r in read_csv(P1911 / "window_human_spot_check.csv"):
        v = {"HUMAN_CONFIRMED_CORRECT": "CLEAR_CORRECT",
             "HUMAN_CONFIRMED_ERROR": "CLEAR_INCORRECT",
             "HUMAN_UNCERTAIN": "AMBIGUOUS"}.get(r["human_state"], r["stage_a_pronunciation"])
        labels.setdefault((r["speaker_id"], r["target"]), {}).update(
            {"verdict": v, "verdict_source": "1.9.11_window", "human_state": r["human_state"]})
    fc = {(r["speaker_id"], r["word"]): r["human_final_label"]
          for r in read_csv(P1912 / "final_consonant_human_review.csv")}
    return labels, fc


def verdict_class(v: str) -> str:
    if not v:
        return ""
    if v in CORRECT:
        return "HUMAN_CORRECT"
    if v in INCORRECT:
        return "HUMAN_INCORRECT"
    if v in UNCERTAIN:
        return "HUMAN_UNCERTAIN"
    return f"OTHER:{v}"


def fidelity_v1(row: dict) -> dict:
    """Literal first-cut: treat ASR emptiness/wrongness as validity evidence."""
    reasons = []
    dur, nseg, ratio = row.get("duration_s"), row.get("silero_n"), row.get("speech_ratio")
    rms, zcr = row.get("rms_mean"), row.get("zcr_proxy")
    conf, soft, asr = row.get("full_confidence"), row.get("soft_full"), row.get("asr_status", "")

    if dur is not None and dur < MIN_DUR_S:
        return {"state": "NO_SPEECH", "assessability": "NONE", "reasons": ["AUDIO_TOO_SHORT"]}
    if nseg is not None and nseg == 0:
        reasons.append("VAD_NO_SEGMENT")
    if ratio is not None and ratio < VAD_RATIO_FLOOR:
        reasons.append("VAD_LOW_SPEECH_RATIO")
    if reasons:
        if rms is not None and rms >= RMS_ENERGY_PRESENT:
            return {"state": "POSSIBLE_ATTEMPT", "assessability": "LOW",
                    "reasons": reasons + ["ENERGY_PRESENT_DESPITE_VAD"]}
        return {"state": "NO_SPEECH", "assessability": "NONE", "reasons": reasons}
    if rms is not None and rms < RMS_NOISE_FLOOR and (zcr is None or zcr < 0.02):
        return {"state": "UNINTELLIGIBLE", "assessability": "NONE", "reasons": ["ENERGY_FLOOR"]}
    if asr == "ASR_CORRECT":
        if conf is not None and conf >= CONF_VALID:
            return {"state": "ASSESSABLE", "assessability": "HIGH", "reasons": ["ASR_TARGET_MATCH"]}
        return {"state": "VALID_ATTEMPT", "assessability": "MEDIUM",
                "reasons": ["ASR_TARGET_MATCH_LOW_CONF"]}
    if asr == "ASR_WRONG":
        if conf is not None and conf >= CONF_VALID:
            return {"state": "VALID_ATTEMPT", "assessability": "MEDIUM",
                    "reasons": ["ASR_WRONG_PHONE_EVIDENCE_OK"]}
        return {"state": "FREE_SPEAK", "assessability": "NONE", "reasons": ["ASR_WRONG"]}
    if asr == "ASR_EMPTY":
        if conf is not None and conf >= CONF_VALID:
            return {"state": "VALID_ATTEMPT", "assessability": "MEDIUM",
                    "reasons": ["ASR_EMPTY", "PHONE_EVIDENCE_PRESENT"]}
        if conf is not None and conf >= CONF_POSSIBLE:
            return {"state": "POSSIBLE_ATTEMPT", "assessability": "LOW", "reasons": ["ASR_EMPTY"]}
        return {"state": "UNINTELLIGIBLE", "assessability": "NONE",
                "reasons": ["ASR_EMPTY", "NO_TARGET_EVIDENCE"]}
    if conf is not None and conf >= CONF_VALID:
        return {"state": "VALID_ATTEMPT", "assessability": "MEDIUM", "reasons": ["NO_ASR_SIGNAL"]}
    return {"state": "POSSIBLE_ATTEMPT", "assessability": "LOW", "reasons": ["NO_ASR_SIGNAL"]}


def fidelity_v2(row: dict) -> dict:
    """Child-safe: ASR is supporting evidence only; refusal needs acoustic proof."""
    reasons = []
    dur, nseg, ratio = row.get("duration_s"), row.get("silero_n"), row.get("speech_ratio")
    rms, zcr = row.get("rms_mean"), row.get("zcr_proxy")
    conf, asr = row.get("full_confidence"), row.get("asr_status", "")

    if dur is not None and dur < MIN_DUR_S:
        return {"state": "NO_SPEECH", "assessability": "NONE", "reasons": ["AUDIO_TOO_SHORT"]}
    vad_negative = (nseg == 0) or (ratio is not None and ratio < VAD_RATIO_FLOOR)
    if nseg == 0:
        reasons.append("VAD_NO_SEGMENT")
    if ratio is not None and ratio < VAD_RATIO_FLOOR:
        reasons.append("VAD_LOW_SPEECH_RATIO")
    if vad_negative:
        if rms is not None and rms < RMS_NOISE_FLOOR:
            return {"state": "NO_SPEECH", "assessability": "NONE",
                    "reasons": reasons + ["ENERGY_FLOOR"]}
        return {"state": "POSSIBLE_ATTEMPT", "assessability": "LOW",
                "reasons": reasons + ["ENERGY_PRESENT_DESPITE_VAD"]}
    if rms is not None and rms < RMS_NOISE_FLOOR and (zcr is None or zcr < 0.02):
        return {"state": "UNINTELLIGIBLE", "assessability": "NONE", "reasons": ["ENERGY_FLOOR"]}
    target_strong = (asr == "ASR_CORRECT") and conf is not None and conf >= CONF_VALID
    target_any = (asr == "ASR_CORRECT") or (conf is not None and conf >= CONF_POSSIBLE)
    if target_strong:
        return {"state": "ASSESSABLE", "assessability": "HIGH",
                "reasons": reasons + ["ASR_TARGET_MATCH", "CONF_OK"]}
    if target_any:
        rs = reasons + (["ASR_TARGET_MATCH_LOW_CONF"] if asr == "ASR_CORRECT" else ["PHONE_EVIDENCE_PRESENT"])
        if asr in ("ASR_EMPTY", "ASR_WRONG"):
            rs.append(f"{asr}_SUPPORTING_ONLY")
        return {"state": "VALID_ATTEMPT", "assessability": "MEDIUM", "reasons": rs}
    rs = reasons + ["WEAK_TARGET_EVIDENCE"]
    if asr in ("ASR_EMPTY", "ASR_WRONG"):
        rs.append(f"{asr}_SUPPORTING_ONLY")
    return {"state": "POSSIBLE_ATTEMPT", "assessability": "LOW", "reasons": rs}


def main():
    RES = OUT / "artifacts" / "p1"
    RES.mkdir(parents=True, exist_ok=True)
    labels, fc_labels = load_labels()

    pron = read_csv(P198 / "pronunciation_results.csv")
    vad = {r["recording_id"]: r for r in read_csv(P198 / "vad_results.csv") if r["mode"] == "silero"}
    inv = {r["recording_id"]: r for r in read_csv(P198 / "recording_inventory.csv")
           if r["role"] == "CHILD_NORMAL"}
    asr = {(r["speaker_id"], r["target"]): r for r in read_csv(P199 / "asr_phone_conflicts.csv")}

    table = []
    for r in pron:
        rid = r["recording_id"]
        key = (r["speaker_id"], r["target"])
        vrow, irow, arow = vad.get(rid, {}), inv.get(rid, {}), asr.get(key, {})
        lab = labels.get(key, {})
        row = {
            "recording_id": rid, "speaker_id": r["speaker_id"], "target": r["target"], "mic": r["mic"],
            "duration_s": fnum(irow.get("duration")),
            "silero_n": fnum(vrow.get("n_segments"), fnum(r.get("silero_n"))),
            "hybrid_n": fnum(r.get("hybrid_n")),
            "speech_ratio": fnum(vrow.get("speech_ratio")),
            "speech_duration_s": fnum(vrow.get("speech_duration_s")),
            "rms_mean": fnum(vrow.get("rms_mean")), "zcr_proxy": fnum(vrow.get("zcr_proxy")),
            "soft_full": fnum(r.get("soft_full")), "full_confidence": fnum(r.get("conf_full")),
            "asr_status": arow.get("asr_status", ""), "asr_text": arow.get("asr_text", ""),
            "human_verdict": lab.get("verdict", ""), "human_class": verdict_class(lab.get("verdict", "")),
            "human_verdict_source": lab.get("verdict_source", ""),
            "human_audio_quality": lab.get("audio_quality", ""), "human_boundary": lab.get("boundary", ""),
            "human_review_confidence": lab.get("review_confidence", ""),
            "human_final_consonant": fc_labels.get(key, ""), "diagnostic_199": lab.get("diagnostic_199", ""),
        }
        for name, fn in (("v1", fidelity_v1), ("v2", fidelity_v2)):
            st = fn(row)
            row[f"fidelity_state_{name}"] = st["state"]
            row[f"assessability_{name}"] = st["assessability"]
            row[f"fidelity_reasons_{name}"] = ";".join(st["reasons"])
        table.append(row)

    # IMG stress: human-verified speech (1.9.6 R2 28/28), Silero=0, energy present.
    img = {
        "recording_id": "IMG_0639", "speaker_id": "hosting_show", "target": "SPEECH_REFERENCE_STRESS",
        "mic": "img", "duration_s": None, "silero_n": 0.0, "hybrid_n": 28.0, "speech_ratio": 0.0,
        "speech_duration_s": None, "rms_mean": 0.0835, "zcr_proxy": None, "soft_full": None,
        "full_confidence": None, "asr_status": "", "asr_text": "",
        "human_verdict": "HUMAN_CONFIRMED_CORRECT", "human_class": "HUMAN_CORRECT",
        "human_verdict_source": "1.9.6_round2_28/28_SPEECH", "human_audio_quality": "",
        "human_boundary": "", "human_review_confidence": "HIGH", "human_final_consonant": "",
        "diagnostic_199": "",
    }
    for name, fn in (("v1", fidelity_v1), ("v2", fidelity_v2)):
        st = fn(img)
        img[f"fidelity_state_{name}"] = st["state"]
        img[f"assessability_{name}"] = st["assessability"]
        img[f"fidelity_reasons_{name}"] = ";".join(st["reasons"])
    table.append(img)

    with open(RES / "p1_evidence_table.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(table[0].keys()))
        w.writeheader()
        w.writerows(table)

    lab_rows = [t for t in table if t["human_class"]]
    eval_out = {}
    for version in ("v1", "v2"):
        matrix = defaultdict(Counter)
        for t in lab_rows:
            matrix[t[f"fidelity_state_{version}"]][t["human_class"]] += 1
        matrix_rows = []
        for s in ALL_STATES:
            row = {"fidelity_state": s}
            for h in ("HUMAN_CORRECT", "HUMAN_INCORRECT", "HUMAN_UNCERTAIN"):
                row[h] = matrix[s][h]
            row["total"] = sum(row[h] for h in ("HUMAN_CORRECT", "HUMAN_INCORRECT", "HUMAN_UNCERTAIN"))
            matrix_rows.append(row)
        hc = [t for t in lab_rows if t["human_class"] == "HUMAN_CORRECT"]
        hi = [t for t in lab_rows if t["human_class"] == "HUMAN_INCORRECT"]
        hu = [t for t in lab_rows if t["human_class"] == "HUMAN_UNCERTAIN"]
        false_gate = [t for t in hc if t[f"fidelity_state_{version}"] in REFUSAL_STATES]
        low_score_correct = [t for t in hc if t["soft_full"] is not None and t["soft_full"] < 50]
        low_correct_false_verdict = [t for t in low_score_correct
                                     if t[f"fidelity_state_{version}"] == "FREE_SPEAK"]
        ae = [t for t in lab_rows if t["asr_status"] == "ASR_EMPTY"]
        ae_correct = [t for t in ae if t["human_class"] == "HUMAN_CORRECT"]
        deletion = [t for t in lab_rows if t["diagnostic_199"] == "SCORER_MISS"]
        refusal = [t for t in hu if t[f"fidelity_state_{version}"] in REFUSAL_STATES]
        eval_out[version] = {
            "matrix": matrix_rows,
            "n_human_correct": len(hc), "n_human_incorrect": len(hi), "n_human_uncertain": len(hu),
            "false_gate_on_human_correct": len(false_gate),
            "false_gate_ids": [f"{t['speaker_id']}_{t['target']}" for t in false_gate],
            "low_score_correct_n": len(low_score_correct),
            "low_score_correct_free_speak_n": len(low_correct_false_verdict),
            "asr_empty_human_correct_n": len(ae_correct),
            "asr_empty_human_correct_states": dict(Counter(t[f"fidelity_state_{version}"] for t in ae_correct)),
            "asr_empty_human_correct_never_free_speak": all(
                t[f"fidelity_state_{version}"] != "FREE_SPEAK" for t in ae_correct),
            "scorer_miss_deletion_states": dict(Counter(t[f"fidelity_state_{version}"] for t in deletion)),
            "scorer_miss_deletion_valid_attempt_or_better": sum(
                1 for t in deletion if t[f"fidelity_state_{version}"] in {"VALID_ATTEMPT", "ASSESSABLE"}),
            "human_uncertain_refused": len(refusal),
        }

    with open(RES / "p1_state_by_human.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["fidelity_state", "HUMAN_CORRECT", "HUMAN_INCORRECT",
                                          "HUMAN_UNCERTAIN", "total"])
        w.writeheader()
        w.writerows(eval_out["v2"]["matrix"])

    with open(RES / "p1_cases.csv", "w", newline="", encoding="utf-8") as f:
        fields = ["recording_id", "speaker_id", "target", "duration_s", "silero_n", "speech_ratio",
                  "rms_mean", "soft_full", "full_confidence", "asr_status", "asr_text",
                  "human_verdict", "human_class", "human_audio_quality", "human_boundary",
                  "human_final_consonant", "diagnostic_199",
                  "fidelity_state_v1", "assessability_v1", "fidelity_reasons_v1",
                  "fidelity_state_v2", "assessability_v2", "fidelity_reasons_v2"]
        w = csv.DictWriter(f, fieldnames=fields, extrasaction="ignore")
        w.writeheader()
        w.writerows(lab_rows)

    summary = {
        "phase": "1.9.14",
        "experiment": "P1 fidelity/assessability rules v1 vs v2",
        "thresholds": {"VAD_RATIO_FLOOR": VAD_RATIO_FLOOR, "MIN_DUR_S": MIN_DUR_S,
                       "RMS_NOISE_FLOOR": RMS_NOISE_FLOOR, "RMS_ENERGY_PRESENT": RMS_ENERGY_PRESENT,
                       "CONF_VALID": CONF_VALID, "CONF_POSSIBLE": CONF_POSSIBLE},
        "n_labeled": len(lab_rows),
        "v1": eval_out["v1"],
        "v2": eval_out["v2"],
        "img_stress": {"state_v1": img["fidelity_state_v1"], "state_v2": img["fidelity_state_v2"],
                       "reasons_v2": img["fidelity_reasons_v2"],
                       "human": img["human_verdict_source"]},
        "states_not_emitted_v2": ["INCOMPLETE", "FREE_SPEAK"],
        "states_not_emitted_note": "No confirmed free-speech/incomplete-attempt recordings exist "
                                   "in the labeled child corpus; emitting those states from the current "
                                   "signal set would be fabrication, so they remain defined but unused.",
        "production_vad": False, "router_locked": False, "unity_integrated": False,
        "scorer_modified": False, "production_window_locked": False,
    }
    (RES / "p1_v1_vs_v2_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")

    prov = {
        "script": "p1_fidelity_assessability.py",
        "inputs": {str(p): sha256_file(p) for p in [
            P198 / "pronunciation_results.csv", P198 / "vad_results.csv",
            P198 / "recording_inventory.csv", P199 / "human_review_results.csv",
            P199 / "asr_phone_conflicts.csv", P1910 / "human_second_pass.csv",
            P1911 / "scorer_miss_human_review.csv", P1911 / "window_human_spot_check.csv",
            P1912 / "final_consonant_human_review.csv"]},
        "python": sys.version,
    }
    (OUT / "manifests" / "p1_provenance.json").write_text(json.dumps(prov, indent=2), encoding="utf-8")
    print(json.dumps({"n_labeled": len(lab_rows),
                      "v1": {k: eval_out["v1"][k] for k in ("false_gate_on_human_correct",
                                                            "low_score_correct_free_speak_n",
                                                            "asr_empty_human_correct_never_free_speak",
                                                            "scorer_miss_deletion_states")},
                      "v2": {k: eval_out["v2"][k] for k in ("false_gate_on_human_correct",
                                                            "low_score_correct_free_speak_n",
                                                            "asr_empty_human_correct_never_free_speak",
                                                            "scorer_miss_deletion_states")},
                      "img": summary["img_stress"]}, indent=2))


if __name__ == "__main__":
    main()
