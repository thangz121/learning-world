"""Phase 1.9.15 — P3 external validation on LWE real child speech (RESEARCH ONLY).

Calibration models are fitted ONLY on SIAK train speakers (ages 7-12). LWE
real-child human-reviewed data is external validation: it is never used for
fitting. Features are recomputed with the frozen soft-v2 pipeline on the same
80 studio number tokens used by phases 1.9.8-1.9.12 so the comparison is
apples-to-apples.

Outputs:
  artifacts/external/external_lwe_features.csv
  artifacts/external/external_lwe_validation.csv
  artifacts/external/external_lwe_metrics.json
"""
from __future__ import annotations

import csv
import hashlib
import json
import re
import sys
from pathlib import Path

import numpy as np
import soundfile as sf

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
sys.path.insert(0, str(REPO))
sys.path.insert(0, str(HERE))

import calibrate as CAL  # noqa: E402
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter  # noqa: E402
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2  # noqa: E402

OUT = REPO / "Research/Speech/Phase1_9_15"
SIAK_RES = OUT / "artifacts" / "siak"
EXT = OUT / "artifacts" / "external"
EXT.mkdir(parents=True, exist_ok=True)
P198 = REPO / "Research/Speech/Phase1_9_8/Results"
P1914 = REPO / "Research/Speech/Phase1_9_14/artifacts/p1/p1_evidence_table.csv"
ZEN = REPO / "Research/Speech/ExternalData/zenodo_200495"
DER = ZEN / "derived_16k"

ORDINAL = {"CLEAR_CORRECT": 5, "PROBABLY_CORRECT": 4, "AMBIGUOUS": 3,
           "PROBABLY_INCORRECT": 2, "CLEAR_INCORRECT": 1}
CORRECT = {"CLEAR_CORRECT", "PROBABLY_CORRECT", "HUMAN_CONFIRMED_CORRECT"}
INCORRECT = {"CLEAR_INCORRECT", "PROBABLY_INCORRECT", "HUMAN_CONFIRMED_ERROR", "HUMAN_TRUE_ERROR"}

MODELS = ["identity", "affine_soft", "isotonic_soft", "linear_soft_conf_evidence", "hgb_noage"]


def sha256_file(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def load_mono16(path: Path):
    key = sha256_file(path)[:16] + "_" + re.sub(r"[^A-Za-z0-9_.-]+", "_", path.name)[:60]
    cache = DER / f"{key}.wav"
    if cache.exists():
        x, sr = sf.read(str(cache))
        if x.ndim > 1:
            x = x.mean(axis=1)
        return x.astype(np.float32), int(sr), cache
    x, sr = sf.read(str(path))
    if x.ndim > 1:
        x = x.mean(axis=1)
    x = x.astype(np.float32)
    if sr != 16000:
        n = int(len(x) * 16000 / sr)
        t0 = np.linspace(0, 1, len(x), endpoint=False)
        t1 = np.linspace(0, 1, n, endpoint=False)
        x = np.interp(t1, t0, x).astype(np.float32)
        sr = 16000
    DER.mkdir(parents=True, exist_ok=True)
    sf.write(str(cache), x, sr)
    return x, sr, cache


def find_source(external_root: Path, speaker_id: str, target: str):
    m = re.match(r"child_(\d+)", speaker_id or "")
    if not m:
        return None
    nn = m.group(1)
    base = external_root / "english_words_sentences"
    for sp in base.iterdir():
        if sp.name.startswith(nn + "_"):
            for mic in ("studio_mic", "port_mic", "nao_mic"):
                c = sp / mic / "numbers" / f"{target}.wav"
                if c.exists():
                    return c
            hits = list(sp.rglob(f"numbers/{target}.wav"))
            if hits:
                return hits[0]
    return None


def main():
    train_rows = CAL.load_rows(SIAK_RES / "calibration_train.csv")
    models = CAL.train_candidates(train_rows)
    print(f"SIAK train rows for fitting: {len(train_rows)}", flush=True)

    # recompute frozen soft-v2 features on the 80 studio tokens
    tgt = CmuDictTargetAdapter()
    pev = PhoneEvidenceV2()
    base = list(csv.DictReader((P198 / "pronunciation_results.csv").open(encoding="utf-8-sig")))
    feat = {}
    for r in base:
        sid, word = r["speaker_id"], r["target"]
        src = find_source(ZEN / "extracted/english_children", sid, word)
        if src is None:
            continue
        _, _, cache16 = load_mono16(src)
        sm = pev.soft_match(str(cache16), tgt.build(word).arpabet)
        from collections import Counter
        mt = Counter(h.match_type for h in sm.hits)
        nh = max(1, len(sm.hits))
        feat[(sid, word)] = {
            "speaker_id": sid, "target": word, "recording_id": r["recording_id"],
            "soft_full": sm.soft_score_0_100, "confidence_0_1": sm.confidence_0_1,
            "n_hits": len(sm.hits), "n_exact": mt.get("exact", 0), "n_soft": mt.get("soft", 0),
            "n_miss": mt.get("miss", 0), "mean_sim": sm.mean_sim,
            "mean_posterior": sm.mean_posterior, "duration_s": None,
            "exact_ratio": mt.get("exact", 0) / nh, "miss_ratio": mt.get("miss", 0) / nh,
            "archived_soft_full": float(r["soft_full"]) if r.get("soft_full") else None,
        }
    # durations from inventory
    inv = {r["recording_id"]: r for r in csv.DictReader((P198 / "recording_inventory.csv").open(encoding="utf-8-sig"))}
    for k, v in feat.items():
        v["duration_s"] = float(inv.get(v["recording_id"], {}).get("duration") or 0.0)
    print(f"recomputed features for {len(feat)} LWE tokens", flush=True)

    # join human labels + assessability states from 1.9.14 p1 evidence table
    labels = {}
    for r in csv.DictReader(P1914.open(encoding="utf-8")):
        labels[(r["speaker_id"], r["target"])] = {
            "human_verdict": r["human_verdict"], "human_class": r["human_class"],
            "human_verdict_source": r["human_verdict_source"],
            "assessability_v2": r["assessability_v2"], "fidelity_state_v2": r["fidelity_state_v2"],
        }

    rows = []
    for key, f in sorted(feat.items()):
        lab = labels.get(key, {})
        row = dict(f)
        row.update(lab)
        row["human_ordinal"] = ORDINAL.get(lab.get("human_verdict", ""), "")
        rows.append(row)

    # apply calibration models
    for name in MODELS:
        pred = CAL.apply_model(models.get(name), rows, name)
        for r, p in zip(rows, pred):
            r[f"pred_{name}"] = float(p)

    # persistent ordered fieldnames
    fields = ["speaker_id", "target", "recording_id", "soft_full", "confidence_0_1",
              "n_hits", "n_exact", "n_soft", "n_miss", "mean_sim", "mean_posterior",
              "duration_s", "exact_ratio", "miss_ratio", "archived_soft_full",
              "human_verdict", "human_class", "human_ordinal", "human_verdict_source",
              "fidelity_state_v2", "assessability_v2"] + [f"pred_{n}" for n in MODELS]
    with open(EXT / "external_lwe_features.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)

    labeled = [r for r in rows if r["human_ordinal"] != ""]
    correct = [r for r in labeled if r["human_verdict"] in CORRECT]
    incorrect = [r for r in labeled if r["human_verdict"] in INCORRECT]
    uncertain = [r for r in labeled if r["human_verdict"] == "AMBIGUOUS"]
    print(f"labeled {len(labeled)}: correct {len(correct)} incorrect {len(incorrect)} uncertain {len(uncertain)}", flush=True)

    def block(rs):
        n = len(rs)
        return {
            "n": n,
            "frr_lt50": float(np.mean([r["pred"] < 50 for r in rs])) if n else None,
            "frr_lt40": float(np.mean([r["pred"] < 40 for r in rs])) if n else None,
            "far_ge50": float(np.mean([r["pred"] >= 50 for r in rs])) if n else None,
            "far_ge80": float(np.mean([r["pred"] >= 80 for r in rs])) if n else None,
            "mean_pred": float(np.mean([r["pred"] for r in rs])) if n else None,
            "mean_soft": float(np.mean([r["soft_full"] for r in rs])) if n else None,
        }

    out_rows = []
    metrics = {"phase": "1.9.15", "experiment": "P3 external LWE validation",
               "fit_population": "SIAK train speakers (ages 7-12)", "candidates": MODELS,
               "n_labeled": len(labeled), "n_human_correct": len(correct),
               "n_human_incorrect": len(incorrect), "n_human_uncertain": len(uncertain),
               "per_candidate": {}}
    def auc_binary(rs, field):
        pos = [r[field] for r in rs if r["human_verdict"] in CORRECT]
        neg = [r[field] for r in rs if r["human_verdict"] in INCORRECT]
        if not pos or not neg:
            return None
        wins = 0.0
        for a in pos:
            for b in neg:
                wins += 1.0 if a > b else (0.5 if a == b else 0.0)
        return wins / (len(pos) * len(neg))

    def matched_frr(rs, field, k):
        """min FAR at FRR<=k on human-correct."""
        best = None
        vals = sorted(set(r[field] for r in rs))
        for th in vals:
            frr = np.mean([r[field] < th for r in rs if r["human_verdict"] in CORRECT])
            if frr <= k + 1e-9:
                far = np.mean([r[field] >= th for r in rs if r["human_verdict"] in INCORRECT])
                if best is None or far < best[1]:
                    best = (th, float(far), float(frr))
        return best

    for name in MODELS:
        field = "soft_full" if name == "identity" else f"pred_{name}"
        for r in labeled:
            r["pred"] = r[field]
        c, i, u = block(correct), block(incorrect), block(uncertain)
        ords = [float(r["human_ordinal"]) for r in labeled]
        preds = [r["pred"] for r in labeled]
        ordinal = {"pearson_ordinal_all_labeled": CAL.pearson(ords, preds),
                   "spearman_ordinal_all_labeled": CAL.spearman(ords, preds)}
        extra = {
            "auc_correct_vs_incorrect": auc_binary(labeled, "pred"),
            "matched_frr_le_0.31": matched_frr(labeled, "pred", 0.31),
            "matched_frr_le_0.10": matched_frr(labeled, "pred", 0.10),
            "matched_frr_le_0.00": matched_frr(labeled, "pred", 0.0),
        }
        metrics["per_candidate"][name] = {"human_correct": c, "human_incorrect": i,
                                          "human_uncertain": u, "ordinal": ordinal, **extra}
        out_rows.append({"candidate": name, "subset": "human_correct", **c})
        out_rows.append({"candidate": name, "subset": "human_incorrect", **i})
        out_rows.append({"candidate": name, "subset": "human_uncertain", **u})

    # identity uses raw soft score for the FRR comparison
    for r in labeled:
        r["pred"] = r["soft_full"]
    metrics["per_candidate"]["identity_raw_soft"] = {
        "human_correct": block(correct), "human_incorrect": block(incorrect),
        "human_uncertain": block(uncertain)}

    # hypothetical fidelity gate: LOW assessability -> cannot assess (not a rejection)
    gated = [r for r in correct if r["assessability_v2"] == "LOW"]
    metrics["fidelity_gate_context"] = {
        "human_correct_low_assessability_n": len(gated),
        "human_correct_low_assessability_ids": [f"{r['speaker_id']}_{r['target']}" for r in gated],
        "note": "with the 1.9.14 v2 gate these would be CANNOT_ASSESS, not false rejections; "
                "engine scoring still computes internally",
    }

    with open(EXT / "external_lwe_validation.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(out_rows[0].keys()))
        w.writeheader()
        w.writerows(out_rows)
    metrics["provenance"] = {
        "features_script": "external_lwe.py (frozen PhoneEvidenceV2@1.4.0)",
        "p1_evidence_table_sha256": sha256_file(P1914),
        "fitted_on": "artifacts/siak/calibration_train.csv",
    }
    (EXT / "external_lwe_metrics.json").write_text(
        json.dumps(metrics, indent=2), encoding="utf-8")
    for name in MODELS:
        pc = metrics["per_candidate"][name]
        print(f"{name:28s} correct FRR50={pc['human_correct']['frr_lt50']:.3f} "
              f"incorrect FAR50={pc['human_incorrect']['far_ge50']:.3f} "
              f"P_ord={pc['ordinal']['pearson_ordinal_all_labeled']:.3f}")


if __name__ == "__main__":
    main()
