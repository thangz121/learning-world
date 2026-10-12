"""WP-1.9.31 — evaluation readout: baseline + B2-D variants + B2-E fixed-threshold curves.

Frozen protocol: 00_PREREGISTRATION.md sections 6-8.
Readout happens ONCE. Test = SO762 official test children (bench) and the frozen pilot test
split; calibration only on non-test speakers.
"""
from __future__ import annotations

import csv
import hashlib
import json
import random
import sys
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np

import p31_common as C

EV = C.ART / "eval"
EV.mkdir(parents=True, exist_ok=True)
P27 = C.P27
SEED = 1927

# evidence score names -> lambda row -> float | None
VARIANTS = {
    "D0_production_decision": None,  # special: decision field
    "D1_prod_max": lambda r: num(r.get("target_max")),
    "D3_alt_max": lambda r: num(r.get("alt_max")),
    "D4_alt_mean": lambda r: num(r.get("alt_mean")),
    "D5_or_max": lambda r: maxn(num(r.get("target_max")), num(r.get("alt_max"))),
    "D5_and_min": lambda r: minn(num(r.get("target_max")), num(r.get("alt_max"))),
}
E_GROUPS = {"E_ENC_target_max": lambda r: num(r.get("target_max")),
            "E_ACO_rms_ratio": lambda r: num(r.get("rms_ratio")),
            "E_TMP_temporal_support": lambda r: num(r.get("temporal_support")),
            "E_ENC_ACO_conjunct": lambda r: minn(num(r.get("target_max")), num(r.get("rms_ratio")))}


def num(v):
    if v in ("", None):
        return None
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def maxn(a, b):
    vals = [v for v in (a, b) if v is not None]
    return max(vals) if vals else None


def minn(a, b):
    vals = [v for v in (a, b) if v is not None]
    return min(vals) if vals else None


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def load_rows(feat_path, lab_path, decision_path=None):
    feats = {r["token_id"]: r for r in csv.DictReader(open(feat_path, encoding="utf-8"))}
    if decision_path and Path(decision_path).exists():
        for r in csv.DictReader(open(decision_path, encoding="utf-8")):
            if r["token_id"] in feats:
                feats[r["token_id"]].update({k: v for k, v in r.items()
                                             if k in ("match_type", "decision", "best_obs", "best_sim")})
    labs = {r["token_id"]: r for r in csv.DictReader(open(lab_path, encoding="utf-8"))
            if r["label"]}
    rows = []
    for tid, lab in labs.items():
        f = feats.get(tid)
        if f is None:
            continue
        rows.append((tid, lab, f))
    return rows


def metrics(rows, thr_by_variant):
    """rows = list of (tid, lab, feat). Returns dict of per-variant metrics."""
    out = {}
    n_p = sum(1 for _, l, _ in rows if l["label"] == "PRESENT")
    n_a = sum(1 for _, l, _ in rows if l["label"] == "ABSENT")
    out["n_present"] = n_p
    out["n_absent"] = n_a
    for name, fn in list(VARIANTS.items()) + list(E_GROUPS.items()):
        thr = thr_by_variant.get(name, 0.0)
        miss = false = 0
        ev_p, ev_a = [], []
        for _, lab, f in rows:
            if name == "D0_production_decision":
                s = num(f.get("target_max"))
            else:
                s = fn(f)
            if lab["label"] == "PRESENT":
                if s is not None:
                    ev_p.append(s)
                    miss += int(s < 0.02)
            else:
                if s is not None:
                    ev_a.append(s)
                    false += int(s >= 0.30)
        m = {"thr": thr if name != "D0_production_decision" else None,
             "decision_FRR": round(_frr_decision(rows, name, thr), 4) if n_p else None,
             "missing_evidence": round(miss / n_p, 4) if n_p else None,
             "FAR": round(_far(rows, name, thr), 4) if n_a else None,
             "false_evidence": round(_false_ev(rows, name, thr) / n_a, 4) if n_a else None,
             "median_evidence_present": round(float(np.median(ev_p)), 4) if ev_p else None,
             "median_evidence_absent": round(float(np.median(ev_a)), 4) if ev_a else None}
        out[name] = m
    # AUC for score variants on this set
    out["AUC"] = {}
    for name, fn in VARIANTS.items():
        if name == "D0_production_decision":
            continue
        xs, ys = [], []
        for _, lab, f in rows:
            s = fn(f)
            if s is None:
                continue
            xs.append(s)
            ys.append(1 if lab["label"] == "PRESENT" else 0)
        out["AUC"][name] = round(_auc(xs, ys), 4) if len(set(ys)) == 2 else None
    return out


def _frr_decision(rows, name, thr):
    bad = tot = 0
    for _, lab, f in rows:
        if lab["label"] != "PRESENT":
            continue
        tot += 1
        if name == "D0_production_decision":
            if str(f.get("decision", "")) != "1":
                bad += 1
        else:
            s = VARIANTS[name](f) if name in VARIANTS else E_GROUPS[name](f)
            if s is None or s < thr:
                bad += 1
    return bad / tot if tot else 0.0


def _far(rows, name, thr):
    bad = tot = 0
    for _, lab, f in rows:
        if lab["label"] != "ABSENT":
            continue
        tot += 1
        if name == "D0_production_decision":
            if str(f.get("decision", "")) == "1":
                bad += 1
        else:
            s = VARIANTS[name](f) if name in VARIANTS else E_GROUPS[name](f)
            if s is not None and s >= thr:
                bad += 1
    return bad / tot if tot else 0.0


def _false_ev(rows, name, thr):
    bad = 0
    for _, lab, f in rows:
        if lab["label"] != "ABSENT":
            continue
        s = (num(f.get("target_max")) if name == "D0_production_decision"
             else (VARIANTS[name](f) if name in VARIANTS else E_GROUPS[name](f)))
        if s is not None and s >= 0.30:
            bad += 1
    return bad


def _auc(xs, ys):
    from sklearn.metrics import roc_auc_score
    return float(roc_auc_score(ys, xs))


def _missing_at(rows, name, thr):
    bad = tot = 0
    for _, lab, f in rows:
        if lab["label"] != "PRESENT":
            continue
        tot += 1
        s = (num(f.get("target_max")) if name == "D0_production_decision"
             else (VARIANTS[name](f) if name in VARIANTS else E_GROUPS[name](f)))
        if s is None or s < thr:
            bad += 1
    return bad / tot if tot else 0.0


def _false_at(rows, name, thr):
    bad = tot = 0
    for _, lab, f in rows:
        if lab["label"] != "ABSENT":
            continue
        tot += 1
        s = (num(f.get("target_max")) if name == "D0_production_decision"
             else (VARIANTS[name](f) if name in VARIANTS else E_GROUPS[name](f)))
        if s is not None and s >= thr:
            bad += 1
    return bad / tot if tot else 0.0


def evaluate_outcome(res):
    """Apply the frozen success/failure numbers to the pre-registered readout."""
    bt = res["bench"]["test"]
    base = bt["D0_production_decision"]
    pilot_spk = res["pilot"]["per_speaker_test"]
    variants = ["D1_prod_max", "D3_alt_max", "D4_alt_mean", "D5_or_max", "D5_and_min"]
    table = {}
    best = None
    for v in variants:
        m = bt[v]
        rel = (base["missing_evidence"] - m["missing_evidence"]) / base["missing_evidence"] \
            if base["missing_evidence"] else None
        crit1 = m["decision_FRR"] <= base["decision_FRR"] + 0.05
        crit2 = rel is not None and rel >= 0.25
        crit3 = m["FAR"] <= base["FAR"] + 0.05
        # pilot-test speaker sign consistency (speakers 1075/1076)
        spk_sign = {}
        for spk, row in pilot_spk.get(v, {}).items():
            brow = pilot_spk["D0_production_decision"].get(spk, {})
            if row["frr"] is not None and brow.get("frr") is not None:
                spk_sign[spk] = "better" if row["frr"] < brow["frr"] else (
                    "worse" if row["frr"] > brow["frr"] else "tie")
        crit4 = len(spk_sign) == 2 and all(s == "better" for s in spk_sign.values())
        table[v] = {"FRR": m["decision_FRR"], "FRR_delta": round(m["decision_FRR"] - base["decision_FRR"], 4),
                    "missing_rel_reduction": round(rel, 4) if rel is not None else None,
                    "FAR": m["FAR"], "crit1_frr_guard": crit1, "crit2_missing_reduction_25pct": crit2,
                    "crit3_far_guard": crit3, "crit4_pilot_both_speakers": crit4,
                    "pilot_speaker_sign": spk_sign}
        score = (crit1, crit3, rel)
        if best is None or (rel is not None and rel > best[0]):
            best = (rel if rel is not None else -1, v)
    success_variant = next((v for v in variants if table[v]["crit1_frr_guard"]
                            and table[v]["crit2_missing_reduction_25pct"]
                            and table[v]["crit3_far_guard"]), None)
    partial_variant = next((v for v in variants if table[v]["crit1_frr_guard"]
                            and table[v]["missing_rel_reduction"] is not None
                            and table[v]["missing_rel_reduction"] > 0), None)
    if success_variant:
        gate = "B2_PILOT_PROMISING_DATA_REQUIRED"
        note = (f"{success_variant} passes the frozen numeric criteria on the external standard; "
                "authorizes only the next research/data decision, never training or production.")
    elif partial_variant:
        gate = "B2_INCONCLUSIVE_MORE_LABELS_REQUIRED"
        note = (f"{partial_variant} shows a directionally correct but sub-threshold reduction "
                "(PARTIAL_SUCCESS per the frozen definitions); repeat with more labels/evidence.")
    elif not base["n_absent"] or not base["n_present"]:
        gate = "B2_INCONCLUSIVE_MORE_LABELS_REQUIRED"
        note = "underpowered test sides."
    else:
        gate = "B2_NOT_JUSTIFIED"
        note = "no variant improves missing-evidence FRR-first; the frozen criteria fail."
    return {"gate": gate, "note": note, "success_variant": success_variant,
            "best_reduction_variant": best[1] if best else None, "variant_table": table,
            "criteria_source": "Research/Speech/Phase1_9_27/09_SUCCESS_CRITERIA/ (unchanged)",
            "assessability_criterion": "N/A — external label source has no NOT_ASSESSABLE class (frozen) ",
            "random_control_criterion": "N/A — no fitted variant is runnable (train+dev ABSENT=34<60, frozen)"}


def match_threshold(calib_rows, baseline_far, name):
    fn = VARIANTS[name] if name in VARIANTS else E_GROUPS[name]
    absents = [fn(f) for _, l, f in calib_rows if l["label"] == "ABSENT"]
    absents = [s for s in absents if s is not None]
    if not absents:
        return None
    best = None
    for tau in np.round(np.arange(0.0, 0.601, 0.001), 3):
        far = sum(1 for s in absents if s >= tau) / len(absents)
        d = abs(far - baseline_far)
        if best is None or d < best[0] or (d == best[0] and tau > best[1]):
            best = (d, float(tau))
    return best[1]


def baseline_far(rows):
    a = [r for r in rows if r[1]["label"] == "ABSENT"]
    if not a:
        return None
    return sum(1 for _, _, f in a if str(f.get("decision", "")) == "1") / len(a)


def per_speaker(rows, name, thr):
    by = defaultdict(lambda: [0, 0, 0, 0])  # p_bad, p_tot, a_bad, a_tot
    for _, lab, f in rows:
        spk = f.get("speaker_id", "")
        s = (num(f.get("target_max")) if name == "D0_production_decision"
             else (VARIANTS[name](f) if name in VARIANTS else E_GROUPS[name](f)))
        if lab["label"] == "PRESENT":
            by[spk][1] += 1
            if s is None or (str(f.get("decision", "")) != "1" if name == "D0_production_decision" else s < thr):
                by[spk][0] += 1
        else:
            by[spk][3] += 1
            if s is not None and (s >= thr if name != "D0_production_decision" else str(f.get("decision", "")) == "1"):
                by[spk][2] += 1
    return {spk: {"frr": round(v[0] / v[1], 4) if v[1] else None,
                  "far": round(v[2] / v[3], 4) if v[3] else None,
                  "n_p": v[1], "n_a": v[3]} for spk, v in sorted(by.items())}


def age_bands(rows, thr):
    out = {}
    t5 = thr.get("D5_or_max", 0.001)
    for band, pred in (("6-7", lambda a: a <= 7), ("8-10", lambda a: 8 <= a <= 10),
                       ("11-15", lambda a: 11 <= a <= 15)):
        sub = [(t, l, f) for t, l, f in rows
               if pred(int(float(f.get("age") or 0)))]
        n_p = sum(1 for _, l, _ in sub if l["label"] == "PRESENT")
        n_a = sum(1 for _, l, _ in sub if l["label"] == "ABSENT")
        out[band] = {"n_present": n_p, "n_absent": n_a,
                     "D0_FRR": round(_frr_decision(sub, "D0_production_decision", 0.0), 4) if n_p else None,
                     "D5or_FRR": round(_frr_decision(sub, "D5_or_max", t5), 4) if n_p else None,
                     "D0_FAR": round(_far(sub, "D0_production_decision", 0.0), 4) if n_a else None,
                     "D5or_FAR": round(_far(sub, "D5_or_max", t5), 4) if n_a else None}
    return out


def class_breakdown(rows, thr):
    """D0 vs D5_or per phone class and per phone (test readout)."""
    def frr_far(sub, name):
        t = thr.get(name) or 0.0
        sfn = (lambda f: num(f.get("target_max")) if name == "D0_production_decision"
               else (VARIANTS[name](f) if name in VARIANTS else E_GROUPS[name](f)))
        n_p = sum(1 for _, l, _ in sub if l["label"] == "PRESENT")
        n_a = sum(1 for _, l, _ in sub if l["label"] == "ABSENT")
        miss = sum(1 for _, l, f in sub if l["label"] == "PRESENT"
                   and (sfn(f) is None or sfn(f) < 0.02))
        frr = round(_frr_decision(sub, name, t), 4) if n_p else None
        missr = round(miss / n_p, 4) if n_p else None
        far = round(_far(sub, name, t), 4) if n_a else None
        return {"n_p": n_p, "n_a": n_a, "FRR": frr, "missing_evidence": missr, "FAR": far}

    out = {"by_class": {}, "by_phone": {}}
    for cls in ("stop", "fricative", "nasal", "liquid", "affricate", "other"):
        sub = [r for r in rows if r[2].get("phone_class") == cls]
        if sub:
            out["by_class"][cls] = {"D0": frr_far(sub, "D0_production_decision"),
                                    "D5_or": frr_far(sub, "D5_or_max")}
    phones = sorted({r[2].get("target_phone", "") for r in rows})
    for ph in phones:
        if not ph:
            continue
        sub = [r for r in rows if r[2].get("target_phone") == ph]
        if len(sub) >= 5:
            out["by_phone"][ph] = {"D0": frr_far(sub, "D0_production_decision"),
                                   "D5_or": frr_far(sub, "D5_or_max")}
    return out


def main():
    prov = {
        "wp": "WP-1.9.31", "seed": SEED,
        "scores_detail_sha256": sha(C.SO_DETAIL),
        "pilot_manifest_sha256": sha(P27 / "06_PILOT_DATA/PILOT_DATA_MANIFEST.csv"),
        "pilot_features_sha256": sha(P27 / "artifacts/pilot_features.csv"),
        "expert_labels_pilot_sha256": sha(C.ART / "labels/expert_labels_pilot.csv"),
        "expert_labels_bench_train_sha256": sha(C.ART / "labels/expert_labels_bench_train.csv"),
        "expert_labels_bench_test_sha256": sha(C.ART / "labels/expert_labels_bench_test.csv"),
        "bench_train_features_sha256": sha(C.ART / "features/bench_train_features.csv"),
        "bench_test_features_sha256": sha(C.ART / "features/bench_test_features.csv"),
    }
    res = {"provenance": prov}

    # ---------------- BENCH ----------------
    bench_train = load_rows(C.ART / "features/bench_train_features.csv",
                            C.ART / "labels/expert_labels_bench_train.csv")
    bench_test = load_rows(C.ART / "features/bench_test_features.csv",
                           C.ART / "labels/expert_labels_bench_test.csv")
    train_spk = sorted({f.get("speaker_id", "") for _, _, f in bench_train})
    rnd = random.Random(SEED)
    dev_spk = set(rnd.sample(train_spk, k=max(1, round(0.2 * len(train_spk)))))
    calib = [r for r in bench_train if r[2].get("speaker_id", "") in dev_spk]
    print(f"bench train={len(bench_train)} calib(dev)={len(calib)} dev_spk={sorted(dev_spk)}")

    base_far_calib = baseline_far(calib) or 0.0
    thr = {"D0_production_decision": None}
    for name in VARIANTS:
        if name == "D0_production_decision":
            continue
        thr[name] = match_threshold(calib, base_far_calib, name)
    for name in E_GROUPS:
        if name == "BASE":
            continue
        thr[name] = match_threshold(calib, base_far_calib, name)

    res["bench"] = {"dev_speakers": sorted(dev_spk), "baseline_far_calib": base_far_calib,
                    "calibrated_thresholds": {k: v for k, v in thr.items() if v is not None},
                    "test": metrics(bench_test, thr),
                    "train_all": metrics(bench_train, thr)}
    res["bench"]["per_speaker_test"] = {n: per_speaker(bench_test, n, thr.get(n) or 0.0)
                                        for n in ("D0_production_decision", "D1_prod_max", "D3_alt_max",
                                                  "D4_alt_mean", "D5_or_max")}
    res["bench"]["age_bands_test"] = age_bands(bench_test, thr)
    res["bench"]["per_class_test"] = class_breakdown(bench_test, thr)

    # /r/ subset (lenient produced set), bench test
    r_sub = [(t, l, f) for t, l, f in bench_test if l["target_phone"] == "ɹ"]
    res["bench"]["r_subset_test"] = metrics(r_sub, thr) if r_sub else None

    # ---------------- PILOT ----------------
    split = json.loads((P27 / "artifacts/pilot_split.json").read_text(encoding="utf-8"))["split"]
    pilot = load_rows(P27 / "artifacts/pilot_features.csv",
                      C.ART / "labels/expert_labels_pilot.csv",
                      decision_path=C.ART / "features/pilot_decisions_features.csv")
    by_split = {"train": [], "dev": [], "test": []}
    split_sets = {k: {str(s) for s in v} for k, v in split.items()}
    for r in pilot:
        spk = str(r[2].get("speaker_id", ""))
        for k, ss in split_sets.items():
            if spk in ss:
                by_split[k].append(r)
    base_far_pilot = baseline_far(by_split["train"]) or 0.0
    thr_p = {}
    for name in list(VARIANTS) + list(E_GROUPS):
        if name in ("D0_production_decision", "BASE"):
            continue
        thr_p[name] = match_threshold(by_split["train"], base_far_pilot, name)
    res["pilot"] = {"calib_split": "train", "baseline_far_calib": base_far_pilot,
                    "calibrated_thresholds": {k: v for k, v in thr_p.items() if v is not None},
                    "train": metrics(by_split["train"], thr_p),
                    "dev": metrics(by_split["dev"], thr_p),
                    "test": metrics(by_split["test"], thr_p),
                    "test_speakers": sorted({f.get("speaker_id", "") for _, _, f in by_split["test"]})}
    # per-speaker pilot test consistency for the main variants
    res["pilot"]["per_speaker_test"] = {n: per_speaker(by_split["test"], n, thr_p.get(n) or 0.0)
                                        for n in ("D0_production_decision", "D1_prod_max", "D3_alt_max",
                                                  "D4_alt_mean", "D5_or_max")}
    res["pilot"]["per_class_test"] = class_breakdown(by_split["test"], thr_p)

    # CSV companions
    ps_rows = []
    for name, table in res["bench"]["per_speaker_test"].items():
        for spk, row in table.items():
            ps_rows.append({"set": "bench_test", "variant": name, "speaker_id": spk, **row})
    for name, table in res["pilot"]["per_speaker_test"].items():
        for spk, row in table.items():
            ps_rows.append({"set": "pilot_test", "variant": name, "speaker_id": spk, **row})
    C.write_csv(EV / "per_speaker.csv", ps_rows,
                ["set", "variant", "speaker_id", "frr", "far", "n_p", "n_a"])
    pc_rows = []
    for ph, d0d5 in res["bench"]["per_class_test"]["by_phone"].items():
        pc_rows.append({"phone": ph, "n_p": d0d5["D0"]["n_p"], "n_a": d0d5["D0"]["n_a"],
                        "D0_FRR": d0d5["D0"]["FRR"], "D0_missing": d0d5["D0"]["missing_evidence"],
                        "D0_FAR": d0d5["D0"]["FAR"], "D5or_FRR": d0d5["D5_or"]["FRR"],
                        "D5or_missing": d0d5["D5_or"]["missing_evidence"],
                        "D5or_FAR": d0d5["D5_or"]["FAR"]})
    C.write_csv(EV / "per_phone_bench_test.csv", pc_rows,
                ["phone", "n_p", "n_a", "D0_FRR", "D0_missing", "D0_FAR",
                 "D5or_FRR", "D5or_missing", "D5or_FAR"])

    # ---------------- E-group fixed-grid curves (pre-registered section 6) ----------------
    GRID = [0.02, 0.05, 0.10, 0.15, 0.30]
    def grid_curves(rows, names):
        out = {}
        for name in names:
            out[name] = []
            for tau in GRID:
                row = {"tau": tau,
                       "FRR": round(_frr_decision(rows, name, tau), 4),
                       "FAR": round(_far(rows, name, tau), 4),
                       "missing_evidence": round(_missing_at(rows, name, 0.02), 4),
                       "false_evidence": round(_false_at(rows, name, 0.30), 4)}
                out[name].append(row)
        return out

    res["bench"]["test_fixed_grid"] = grid_curves(
        bench_test, ["E_ENC_target_max", "E_ACO_rms_ratio", "E_TMP_temporal_support",
                     "E_ENC_ACO_conjunct"])
    res["bench"]["test_fixed_grid_sensitivity_D"] = grid_curves(
        bench_test, ["D1_prod_max", "D3_alt_max", "D4_alt_mean", "D5_or_max", "D5_and_min"])
    res["pilot"]["test_fixed_grid"] = grid_curves(
        by_split["test"], ["E_ENC_target_max", "E_ACO_rms_ratio", "E_TMP_temporal_support",
                           "E_ENC_ACO_conjunct"])

    # ---------------- full-train calibration sensitivity (post-hoc, marked) ----------------
    base_far_train = baseline_far(bench_train) or 0.0
    thr_sens = {}
    for name in ("D1_prod_max", "D3_alt_max", "D4_alt_mean", "D5_or_max", "D5_and_min"):
        thr_sens[name] = match_threshold(bench_train, base_far_train, name)
    sens_rows = {}
    for name in thr_sens:
        t = thr_sens[name]
        sens_rows[name] = {"thr_train": t,
                           "FRR_test": round(_frr_decision(bench_test, name, t), 4),
                           "FAR_test": round(_far(bench_test, name, t), 4),
                           "missing_evidence_test": round(_missing_at(bench_test, name, 0.02), 4)}
    res["bench"]["posthoc_fulltrain_calibration_sensitivity"] = {
        "baseline_far_train": base_far_train, "variants": sens_rows}

    # ---------------- criteria + outcome (prereg section 8) ----------------
    res["outcome"] = evaluate_outcome(res)
    C.write_json(EV / "eval_readout.json", res)

    # ---------------- failure replay ----------------
    replay = []
    for t, l, f in bench_test:
        if l["label"] == "ABSENT":
            replay.append((t, l["word"], l["target_phone"], f.get("target_max"),
                           f.get("alt_max"), f.get("decision"), f.get("age")))
    replay.sort(key=lambda x: -(num(x[3]) or 0))
    res["failure_replay_top_false_accepts"] = [
        {"token_id": t, "word": w, "phone": p, "target_max": tm, "alt_max": am,
         "decision": d, "age": a} for t, w, p, tm, am, d, a in replay[:25]]
    weak = []
    for t, l, f in bench_test:
        if l["label"] == "PRESENT" and num(f.get("target_max")) is not None:
            weak.append((t, l["word"], l["target_phone"], num(f.get("target_max"))))
    weak.sort(key=lambda x: x[3])
    res["failure_replay_weakest_presents"] = [
        {"token_id": t, "word": w, "phone": p, "target_max": tm} for t, w, p, tm in weak[:25]]

    C.write_json(EV / "eval_readout.json", res)

    # console summary
    bt = res["bench"]["test"]
    print("BENCH test: n_p", bt["n_present"], "n_a", bt["n_absent"])
    for n in ("D0_production_decision", "D1_prod_max", "D3_alt_max", "D4_alt_mean",
              "D5_or_max", "D5_and_min"):
        m = bt[n]
        print(f"  {n:24s} FRR={m['decision_FRR']} miss={m['missing_evidence']} "
              f"FAR={m['FAR']} false_ev={m['false_evidence']} thr={m['thr']}")
    print("  AUC:", bt["AUC"])
    pt = res["pilot"]["test"]
    print("PILOT test:", pt["n_present"], "P/", pt["n_absent"], "A",
          "baseline FRR", pt["D0_production_decision"]["decision_FRR"])
    print("DONE p31_eval")


if __name__ == "__main__":
    main()
