"""Phase 1.9.16 — Experiment H (assessability interaction) + J (recording stress).

H: run baseline and adapted evidence on the 115 independent fidelity-review clips
   and compare evidence strength on human ASSESSABLE vs NOT_ASSESSABLE speech
   (does the adapted head hallucinate confident phones on unintelligible audio?).

J: synthetic stress conditions (gain / noise / telephone band) on a fixed sample
   of LWE tokens; reported separately as stress tests, not real-child validation.

Outputs: artifacts/ab/assessability_ab.csv/json, artifacts/stress/stress_tests.csv/json
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

import numpy as np
import soundfile as sf
import torch

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter  # noqa: E402
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2  # noqa: E402
from eval_ab import AdaptedPEV, find_source  # noqa: E402

AB = REPO / "Research/Speech/Phase1_9_16/artifacts/ab"
HR = REPO / "Research/Speech/Phase1_9_15/HumanReview"
FID = REPO / "Research/Speech/Phase1_9_15/artifacts/fidelity/fidelity_independent.csv"
ST = REPO / "Research/Speech/Phase1_9_16/artifacts/stress"
ST.mkdir(parents=True, exist_ok=True)
P1914_P1 = REPO / "Research/Speech/Phase1_9_14/artifacts/p1/p1_evidence_table.csv"


def build_arpabet(tgt, target):
    arpa = []
    for w in target.split():
        try:
            st = tgt.build(w)
            arpa.extend([str(p) for p in st.arpabet])
        except Exception:  # noqa: BLE001
            pass
    return arpa


def experiment_h(pev, apev):
    meta = json.loads((HR / "review_metadata.json").read_text(encoding="utf-8"))
    labels = {r["case_id"]: r for r in csv.DictReader(FID.open(encoding="utf-8"))}
    tgt = CmuDictTargetAdapter()
    rows = []
    for it in meta["items"]:
        if it["task"] not in ("number_counting", "predefined_sentence"):
            continue
        arpa = build_arpabet(tgt, it["target"])
        if not arpa:
            continue
        clip = HR / it["clip"]
        sm_b = pev.soft_match(str(clip), arpa)
        sm_a = apev.soft_match(str(clip), arpa)
        lab = labels.get(it["case_id"], {})
        rows.append({
            "case_id": it["case_id"], "stratum": it["selected_stratum"],
            "human_attempt": lab.get("human_attempt", ""),
            "human_assessability": lab.get("human_assessability", ""),
            "human_content": lab.get("human_content", ""),
            "baseline_soft": sm_b.soft_score_0_100, "adapted_soft": sm_a.soft_score_0_100,
            "baseline_conf": sm_b.confidence_0_1, "adapted_conf": sm_a.confidence_0_1,
            "baseline_post": sm_b.mean_posterior, "adapted_post": sm_a.mean_posterior,
        })
    with open(AB / "assessability_ab.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)

    def block(rs, field):
        if not rs:
            return None
        return {"n": len(rs), "mean": round(float(np.mean([r[field] for r in rs])), 2),
                "conf_ge_0.05": round(float(np.mean([r["adapted_conf"] >= 0.05 for r in rs])), 3)}

    valid = [r for r in rows if r["human_attempt"] in ("VALID_ATTEMPT", "POSSIBLE_ATTEMPT", "INCOMPLETE")]
    not_assess = [r for r in rows if r["human_assessability"] == "NOT_ASSESSABLE"]
    out = {
        "n_runnable_items": len(rows),
        "human_valid_attempts": {f: block(valid, f) for f in ("baseline_soft", "adapted_soft")},
        "human_not_assessable": {f: block(not_assess, f) for f in ("baseline_soft", "adapted_soft")},
        "adapted_confident_evidence_on_not_assessable_n": sum(
            1 for r in not_assess if r["adapted_conf"] >= 0.05),
        "baseline_confident_evidence_on_not_assessable_n": sum(
            1 for r in not_assess if r["baseline_conf"] >= 0.05),
        "note": "free-speech items have no canonical target and are excluded from H",
    }
    (AB / "assessability_ab.json").write_text(json.dumps(out, indent=2), encoding="utf-8")
    print(json.dumps(out, indent=2))
    return out


def conditions(x, sr, rng):
    conds = {"clean": x}
    for g in (-12, 6):
        conds[f"gain_{g}db"] = np.clip(x * (10 ** (g / 20)), -1, 1).astype(np.float32)
    for snr in (20, 10):
        p = np.mean(x ** 2) + 1e-20
        noise = rng.normal(0, np.sqrt(p / (10 ** (snr / 10))), len(x)).astype(np.float32)
        conds[f"noise_snr{snr}"] = np.clip(x + noise, -1, 1).astype(np.float32)
    try:
        from scipy.signal import butter, sosfiltfilt
        sos = butter(4, [300, 3400], btype="bandpass", fs=sr, output="sos")
        conds["telephone_band"] = sosfiltfilt(sos, x).astype(np.float32)
    except Exception:  # noqa: BLE001
        pass
    return conds


def experiment_j(pev, apev):
    base = list(csv.DictReader(P1914_P1.open(encoding="utf-8")))[:24]
    tgt = CmuDictTargetAdapter()
    rng = np.random.RandomState(1516)
    rows = []
    tmp = ST / "_tmp.wav"
    for r in base:
        src = find_source(r["speaker_id"], r["target"])
        if src is None:
            continue
        x, sr = sf.read(str(src))
        if x.ndim > 1:
            x = x.mean(axis=1)
        x = x.astype(np.float32)
        if sr != 16000:
            n = int(len(x) * 16000 / sr)
            x = np.interp(np.linspace(0, 1, n, endpoint=False),
                          np.linspace(0, 1, len(x), endpoint=False), x).astype(np.float32)
            sr = 16000
        st = tgt.build(r["target"])
        for cname, cx in conditions(x, sr, rng).items():
            sf.write(str(tmp), cx, sr)
            sm_b = pev.soft_match(str(tmp), st.arpabet)
            sm_a = apev.soft_match(str(tmp), st.arpabet)
            rows.append({"case_id": f"{r['speaker_id']}_{r['target']}", "condition": cname,
                         "baseline_soft": sm_b.soft_score_0_100, "adapted_soft": sm_a.soft_score_0_100,
                         "baseline_conf": sm_b.confidence_0_1, "adapted_conf": sm_a.confidence_0_1,
                         "baseline_final": sm_b.hits[-1].match_type if sm_b.hits else "",
                         "adapted_final": sm_a.hits[-1].match_type if sm_a.hits else ""})
    if tmp.exists():
        tmp.unlink()
    with open(ST / "stress_tests.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)
    summary = {}
    for cname in sorted(set(r["condition"] for r in rows)):
        rs = [r for r in rows if r["condition"] == cname]
        summary[cname] = {
            "n": len(rs),
            "baseline_mean": round(float(np.mean([r["baseline_soft"] for r in rs])), 1),
            "adapted_mean": round(float(np.mean([r["adapted_soft"] for r in rs])), 1),
            "baseline_mean_delta_vs_clean": None,
            "baseline_final_recall": round(float(np.mean(
                [r["baseline_final"] in ("exact", "soft") for r in rs])), 3),
            "adapted_final_recall": round(float(np.mean(
                [r["adapted_final"] in ("exact", "soft") for r in rs])), 3),
        }
    clean = summary.get("clean", {})
    for k, v in summary.items():
        if clean and "baseline_mean" in clean:
            v["baseline_mean_delta_vs_clean"] = round(v["baseline_mean"] - clean["baseline_mean"], 1)
            v["adapted_mean_delta_vs_clean"] = round(v["adapted_mean"] - clean["adapted_mean"], 1)
    (ST / "stress_summary.json").write_text(json.dumps(
        {"note": "synthetic perturbations; stress only, not real-child validation",
         "conditions": summary}, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))


def main():
    pev = PhoneEvidenceV2()
    pev._ensure()
    apev = AdaptedPEV(AB / "head.pt")
    experiment_h(pev, apev)
    experiment_j(pev, apev)


if __name__ == "__main__":
    main()
