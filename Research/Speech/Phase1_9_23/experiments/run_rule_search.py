"""WP-1.9.23 — research-only acceptance rule redesign + falsification.

Reads artifacts/feature_matrix.csv (built from existing WP-1.9.21/1.9.22 evidence;
no encoder rerun) and evaluates interpretable acceptance-rule families with
PRESENT / ABSENT / UNCERTAIN outputs. Rule selection uses SO762 dev only; LWE is
external validation; SO762 test is held out. No production change, no B2.
"""
from __future__ import annotations

import csv
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_23"
ART = OUT / "artifacts"
KNOWN_CASES = ["child_01_eight", "child_01_nine", "child_01_seven", "child_01_ten",
               "child_02_ten", "child_04_four", "child_06_six", "child_07_one",
               "child_01_four", "child_02_four", "child_03_four", "child_07_seven"]

# pre-declared grids (ranges derived from the dev feature distribution, Part 1 output)
SUPPORT_TAUS = (0.02, 0.05, 0.10, 0.20, 0.30, 0.50)
MARGINS = (-0.01, 0.0, 0.005, 0.01, 0.02)
BLANKS = (0.99, 0.95, 0.90, 0.85)
BLANK_OCC = (1.0, 0.95, 0.90)
RUN_MIN = (1, 2)
RANK1_TAUS = (0.02, 0.05, 0.10)
RANK2_TAUS = (0.10, 0.20, 0.30)
RANK35_TAUS = (0.20, 0.30, 0.50)


def num(v, d=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return d


def load():
    rows = list(csv.DictReader(open(ART / "feature_matrix.csv", encoding="utf-8")))
    for r in rows:
        for k in ("target_post_mean", "target_max_A", "target_peak_in_span", "competitor_post",
                  "competitor_mean_span", "competitor_max_span", "competitor_peak",
                  "identity_margin_mean", "identity_ratio_mean", "peak_margin",
                  "competitor_dominates_frac", "blank_mean_span", "blank_max_span",
                  "blank_at_peak", "blank_ge05_frac", "blank_ge08_frac", "blank_ge09_frac",
                  "support_concentration_05", "peak_pos_rel", "support_before_peak",
                  "support_after_peak", "peak_D", "max_D", "temporal_support_D", "prom_comp_D",
                  "support_std_windows"):
            r[k] = num(r[k])
        for k in ("identity_credit", "target_rank_in_top5", "top1_identity", "top5_identity",
                  "production_decision", "support_count_05", "support_count_10",
                  "longest_run_05", "longest_run_10", "support_runs_05", "cluster_width_D",
                  "identity_windows_present", "decision_windows_present",
                  "competitor_wins_peak", "is_final_consonant"):
            r[k] = int(num(r[k], -1))
        r["truth"] = int(r["truth"]) if r["truth"] != "" else None
        r["weak"] = 1 if r["target_max_A"] < 0.15 else 0
        r["strong_evidence"] = 1 if r["target_max_A"] >= 0.30 else 0
    return rows


# ---------------- rule families ----------------

def rule_specs():
    """Return list of (rule_id, family, params, decide(r)->PRESENT/ABSENT/UNCERTAIN)."""
    rules = []

    def add(rid, fam, params, fn):
        rules.append((rid, fam, params, fn))

    # A. strict identity
    add("A1_rank1_only", "A_STRICT_IDENTITY", {},
        lambda r: "PRESENT" if r["top1_identity"] == 1 else "ABSENT")
    for t in SUPPORT_TAUS:
        add(f"A2_rank1_support{t}", "A_STRICT_IDENTITY", {"tau": t},
            (lambda t: lambda r: "PRESENT" if (r["top1_identity"] == 1
                                               and r["target_max_A"] >= t) else "ABSENT")(t))
    for b in BLANKS:
        add(f"A3_rank1_blank{b}", "A_STRICT_IDENTITY", {"beta": b},
            (lambda b: lambda r: "PRESENT" if (r["top1_identity"] == 1
                                               and r["blank_mean_span"] <= b) else "ABSENT")(b))

    # B. rank-aware
    for t1, t2, t35 in ((0.02, 0.20, 0.50), (0.05, 0.20, 0.50), (0.05, 0.30, 0.50),
                        (0.10, 0.30, 0.50), (0.05, 0.20, 0.30)):
        def bfn(r, t1=t1, t2=t2, t35=t35):
            rank = r["target_rank_in_top5"]
            if rank < 0:
                return "ABSENT"
            tau = t1 if rank == 0 else (t2 if rank == 1 else t35)
            return "PRESENT" if r["target_max_A"] >= tau else "ABSENT"
        add(f"B_rank_aware_t{t1}_{t2}_{t35}", "B_RANK_AWARE",
            {"t1": t1, "t2": t2, "t35": t35}, bfn)

    # C. margin-aware
    for m in MARGINS:
        add(f"C1_identity_margin_mean{m}", "C_MARGIN_AWARE", {"m": m},
            (lambda m: lambda r: "PRESENT" if (r["identity_credit"] == 1
                                               and r["identity_margin_mean"] >= m) else "ABSENT")(m))
    for m in (0.0, 0.05, 0.10):
        add(f"C2_identity_peak_margin{m}", "C_MARGIN_AWARE", {"mp": m},
            (lambda m: lambda r: "PRESENT" if (r["identity_credit"] == 1
                                               and r["peak_margin"] >= m) else "ABSENT")(m))

    # D. blank-aware
    for b in BLANKS:
        add(f"D1_identity_blank_mean{b}", "D_BLANK_AWARE", {"beta": b},
            (lambda b: lambda r: "PRESENT" if (r["identity_credit"] == 1
                                               and r["blank_mean_span"] <= b) else "ABSENT")(b))
    for g in BLANK_OCC:
        add(f"D2_identity_blank_occ{g}", "D_BLANK_AWARE", {"gamma": g},
            (lambda g: lambda r: "PRESENT" if (r["identity_credit"] == 1
                                               and r["blank_ge09_frac"] <= g) else "ABSENT")(g))
    for b in (0.9, 0.8, 0.7):
        add(f"D3_identity_blank_peak{b}", "D_BLANK_AWARE", {"bp": b},
            (lambda b: lambda r: "PRESENT" if (r["identity_credit"] == 1
                                               and r["blank_at_peak"] <= b) else "ABSENT")(b))

    # E. identity + support + margin
    for t in (0.05, 0.10, 0.20, 0.30):
        for m in (-0.01, 0.0, 0.005):
            add(f"E_id_sup{t}_mar{m}", "E_IDENTITY_SUPPORT_MARGIN", {"tau": t, "m": m},
                (lambda t, m: lambda r: "PRESENT" if (r["identity_credit"] == 1
                                                      and r["target_max_A"] >= t
                                                      and r["identity_margin_mean"] >= m)
                 else "ABSENT")(t, m))

    # F. identity + support + margin + blank, explicit UNCERTAIN
    for t in (0.10, 0.20, 0.30):
        for m in (0.0, 0.005, 0.01):
            for b in (0.95, 0.90):
                def ffn(r, t=t, m=m, b=b):
                    if r["identity_credit"] != 1:
                        return "ABSENT"
                    if (r["target_max_A"] >= t and r["identity_margin_mean"] >= m
                            and r["blank_mean_span"] <= b):
                        return "PRESENT"
                    if (r["target_max_A"] >= 0.02 and r["identity_margin_mean"] >= -0.01
                            and r["blank_mean_span"] <= 0.99):
                        return "UNCERTAIN"
                    return "ABSENT"
                add(f"F_main_sup{t}_mar{m}_blank{b}", "F_IDENTITY_SUPPORT_MARGIN_BLANK",
                    {"tau": t, "m": m, "beta": b}, ffn)

    # G. temporal consistency
    add("G1_identity_run2", "G_TEMPORAL_CONSISTENCY", {"run": 2},
        lambda r: "PRESENT" if (r["identity_credit"] == 1
                                and r["longest_run_05"] >= 2) else "ABSENT")
    add("G2_identity_count2", "G_TEMPORAL_CONSISTENCY", {"count": 2},
        lambda r: "PRESENT" if (r["identity_credit"] == 1
                                and r["support_count_05"] >= 2) else "ABSENT")
    add("G3_support_or_run2", "G_TEMPORAL_CONSISTENCY", {"tau": 0.30, "run": 2},
        lambda r: "PRESENT" if (r["identity_credit"] == 1
                                and (r["target_max_A"] >= 0.30
                                     or r["longest_run_05"] >= 2)) else "ABSENT")

    # H. phone-class adaptive (exploratory; per-class support tau fitted on dev medians)
    add("H1_class_adaptive", "H_PHONE_CLASS_ADAPTIVE", {"fit": "dev_q25_by_class"},
        lambda r: _class_adaptive(r))
    return rules


_CLASS_TAU = {}


def fit_class_adaptive(dev):
    """Exploratory per-class support threshold = dev q25 of target_max_A (flagged overfit-prone)."""
    by = defaultdict(list)
    for r in dev:
        if r["truth"] == 1:
            by[r["target_phone"]].append(r["target_max_A"])
    for ph, vals in by.items():
        vals.sort()
        _CLASS_TAU[ph] = vals[int(0.25 * (len(vals) - 1))] if vals else 0.0
    _CLASS_TAU["__default__"] = 0.10


def _class_adaptive(r):
    tau = _CLASS_TAU.get(r["target_phone"], _CLASS_TAU.get("__default__", 0.10))
    if r["identity_credit"] != 1:
        return "ABSENT"
    return "PRESENT" if r["target_max_A"] >= tau else "ABSENT"


def metrics(rows, decide):
    tp = fn = fp = tn = unc = 0
    unsupported = conflict = blankdom = strong_fp = 0
    changed = 0
    for r in rows:
        d = decide(r)
        changed += (d != ("PRESENT" if r["production_decision"] == 1 else "ABSENT"))
        if d == "UNCERTAIN":
            unc += 1
            continue
        present = d == "PRESENT"
        y = r["truth"]
        if y is None:
            continue
        if present:
            if r["target_max_A"] < 0.15 and r["target_post_mean"] < 0.25:
                unsupported += 1
            if r["identity_margin_mean"] < 0:
                conflict += 1
            if r["blank_mean_span"] >= 0.90 and r["target_post_mean"] < 0.05:
                blankdom += 1
            if y == 0 and r["target_max_A"] >= 0.30:
                strong_fp += 1
        if y == 1:
            tp += present
            fn += (not present)
        else:
            fp += present
            tn += (not present)
    n = tp + fn + fp + tn + unc
    dec_n = tp + fn + fp + tn
    return {
        "n": n, "present": tp + fn, "absent": fp + tn,
        "present_recall": round(tp / (tp + fn), 4) if (tp + fn) else None,
        "frr": round(fn / (tp + fn), 4) if (tp + fn) else None,
        "far": round(fp / (fp + tn), 4) if (fp + tn) else None,
        "absent_detection": round(tn / (fp + tn), 4) if (fp + tn) else None,
        "uncertain": unc, "uncertain_rate": round(unc / n, 4) if n else None,
        "decision_coverage": round(dec_n / n, 4) if n else None,
        "false_present": fp, "unsupported_present": unsupported,
        "competitor_conflict_present": conflict, "blank_dominated_present": blankdom,
        "strong_encoder_false_present": strong_fp,
        "decisions_changed_from_production": changed,
    }


def main():
    rows = load()
    labeled = [r for r in rows if r["truth"] is not None]
    dev = [r for r in labeled if r["corpus"] in ("so762_dev", "so762_absent_dev")]
    lwe = [r for r in labeled if r["corpus"] == "lwe"]
    test = [r for r in labeled if r["corpus"] == "so762_test"]
    lwe_abs = [r for r in lwe if r["truth"] == 0]
    dev_abs = [r for r in dev if r["truth"] == 0]
    fit_class_adaptive(dev)
    rules = rule_specs()

    # production baseline
    prod = lambda r: "PRESENT" if r["production_decision"] == 1 else "ABSENT"
    base = {"dev": metrics(dev, prod), "lwe": metrics(lwe, prod), "test": metrics(test, prod),
            "lwe_absent": metrics(lwe_abs, prod), "dev_absent": metrics(dev_abs, prod)}
    base_dev_recall = base["dev"]["present_recall"]
    base_dev_far = base["dev"]["far"]

    # evaluate all rules on all datasets
    all_rows = []
    per_rule = {}
    for rid, fam, params, fn in rules:
        m = {"dev": metrics(dev, fn), "lwe": metrics(lwe, fn), "test": metrics(test, fn),
             "lwe_absent": metrics(lwe_abs, fn), "dev_absent": metrics(dev_abs, fn)}
        per_rule[rid] = {"family": fam, "params": params, "fn": fn, "metrics": m}
        for ds, mm in m.items():
            all_rows.append({"rule_id": rid, "family": fam,
                             "params": json.dumps(params, sort_keys=True), "dataset": ds, **mm})

    # Pareto frontier on dev (recall vs FAR)
    points = []
    for rid, pr in per_rule.items():
        m = pr["metrics"]["dev"]
        if m["present_recall"] is None or m["far"] is None:
            continue
        points.append({"rule_id": rid, "family": pr["family"],
                       "params": json.dumps(pr["params"], sort_keys=True),
                       "dev_recall": m["present_recall"], "dev_far": m["far"],
                       "dev_uncertain": m["uncertain_rate"],
                       "lwe_recall": pr["metrics"]["lwe"]["present_recall"],
                       "lwe_far": pr["metrics"]["lwe"]["far"],
                       "test_recall": pr["metrics"]["test"]["present_recall"],
                       "dev_typeA_removed": None, "dev_typeB_remaining": None})
    pareto = []
    for p in points:
        dominated = any((q["dev_recall"] >= p["dev_recall"] and q["dev_far"] <= p["dev_far"]
                         and (q["dev_recall"] > p["dev_recall"] or q["dev_far"] < p["dev_far"]))
                        for q in points if q is not p)
        p["pareto_optimal_dev"] = int(not dominated)
        p["frr_first_safe"] = int(p["dev_recall"] >= base_dev_recall and p["dev_far"] < base_dev_far)
        pareto.append(p)

    # TYPE A / TYPE B false-accept reclassification (production false accepts)
    decomp = {r["token_id"]: r for r in csv.DictReader(
        open(L.REPO / "Research/Speech/Phase1_9_22/FALSE_ACCEPTANCE_DECOMPOSITION.csv",
             encoding="utf-8"))}
    feature = {r["token_id"]: r for r in rows}
    false_rows = []
    for tid, d in decomp.items():
        f = feature.get(tid)
        if f is None:
            continue
        mech = d["primary_mechanism"]
        ftype = "TYPE_B_ENCODER" if mech == "IDENTITY_STRONG" else "TYPE_A_ACCEPTANCE"
        false_rows.append({"token_id": tid, "corpus": d["corpus"],
                           "target_phone": d["target_phone"],
                           "human_label": d["human_label"], "match_type": d["match_type"],
                           "identity_rank": d["target_rank_in_top5"],
                           "target_max_A": d["target_max_A"], "peak_D": d["peak_D"],
                           "blank_mean_span": d["blank_mean_span"],
                           "identity_margin_mean": d["identity_margin_mean"],
                           "acceptance_subtype": mech, "failure_type": ftype,
                           "removed_by_best_rule": "", "removed_by_rank1_only": "",
                           "removed_by_margin0": "", "removed_by_blank90": "",
                           "notes": d["evidence"]})

    # best research rule (FRR-first ladder, dev only; documented in the report)
    safe = [p for p in pareto if p["frr_first_safe"]]
    ladder = ""
    if safe:
        best = min(safe, key=lambda p: (p["dev_far"], -p["dev_recall"]))
        ladder = "frr_first_safe"
    else:
        c1 = [p for p in pareto if p["dev_recall"] >= base_dev_recall - 0.02]
        if c1:
            best = min(c1, key=lambda p: (p["dev_far"], -p["dev_recall"]))
            ladder = "min_far_at_recall_loss_le_2pts"
        else:
            c2 = [p for p in pareto if p["dev_recall"] >= base_dev_recall - 0.05]
            if c2:
                best = min(c2, key=lambda p: (p["dev_far"], -p["dev_recall"]))
                ladder = "min_far_at_recall_loss_le_5pts"
            else:
                best = max(pareto, key=lambda p: (p["dev_recall"] - p["dev_far"]))
                ladder = "max_recall_minus_far"
    best_rule = per_rule[best["rule_id"]]
    best_fn = best_rule["fn"]

    # apply best rule to false accepts + type accounting
    weak_removed = strong_remaining = 0
    rank1 = [fn for rid, fam, pa, fn in rules if rid == "A1_rank1_only"][0]
    margin0 = [fn for rid, fam, pa, fn in rules if rid == "C1_identity_margin_mean0.0"][0]
    blank90 = [fn for rid, fam, pa, fn in rules if rid == "D1_identity_blank_mean0.9"][0]
    for fr in false_rows:
        f = feature[fr["token_id"]]
        fr["removed_by_best_rule"] = int(best_fn(f) != "PRESENT")
        fr["removed_by_rank1_only"] = int(rank1(f) != "PRESENT")
        fr["removed_by_margin0"] = int(margin0(f) != "PRESENT")
        fr["removed_by_blank90"] = int(blank90(f) != "PRESENT")
        if fr["failure_type"] == "TYPE_A_ACCEPTANCE" and fr["removed_by_best_rule"]:
            weak_removed += 1
        if fr["failure_type"] == "TYPE_B_ENCODER" and fr["removed_by_best_rule"] == 0:
            strong_remaining += 1

    # representative rules per family for the per-token table (A..H)
    fam_rep = {}
    for fam in ("A_STRICT_IDENTITY", "B_RANK_AWARE", "C_MARGIN_AWARE", "D_BLANK_AWARE",
                "E_IDENTITY_SUPPORT_MARGIN", "F_IDENTITY_SUPPORT_MARGIN_BLANK",
                "G_TEMPORAL_CONSISTENCY", "H_PHONE_CLASS_ADAPTIVE"):
        cands = [(rid, per_rule[rid]) for rid in per_rule if per_rule[rid]["family"] == fam]
        # representative = dev-max recall among family (ties -> lower FAR)
        rid, pr = max(cands, key=lambda x: (x[1]["metrics"]["dev"]["present_recall"] or 0,
                                            -(x[1]["metrics"]["dev"]["far"] or 1)))
        fam_rep[fam] = (rid, pr["fn"])

    per_token = []
    for r in rows:
        d = {
            "token_id": r["token_id"], "word": r["word"],
            "target_phone": r["target_phone"], "human_label": r["human_label"],
            "truth": r["truth"], "corpus": r["corpus"],
            "production_decision": r["production_decision"],
            "identity_rank": r["target_rank_in_top5"], "identity_credit": r["identity_credit"],
            "target_mean": r["target_post_mean"], "target_max": r["target_max_A"],
            "target_peak": r["target_peak_in_span"], "competitor_mean": r["competitor_mean_span"],
            "competitor_max": r["competitor_max_span"], "identity_margin": r["identity_margin_mean"],
            "blank_mean": r["blank_mean_span"], "blank_peak": r["blank_at_peak"],
            "blank_fraction_ge09": r["blank_ge09_frac"],
            "temporal_support": r["temporal_support_D"],
            "longest_support_run": r["longest_run_05"],
            "support_count": r["support_count_05"],
            "window_stability": r["identity_class"],
            "candidate_A": fam_rep["A_STRICT_IDENTITY"][1](r),
            "candidate_B": fam_rep["B_RANK_AWARE"][1](r),
            "candidate_C": fam_rep["C_MARGIN_AWARE"][1](r),
            "candidate_D": fam_rep["D_BLANK_AWARE"][1](r),
            "candidate_E": fam_rep["E_IDENTITY_SUPPORT_MARGIN"][1](r),
            "candidate_F": fam_rep["F_IDENTITY_SUPPORT_MARGIN_BLANK"][1](r),
            "candidate_G": fam_rep["G_TEMPORAL_CONSISTENCY"][1](r),
            "candidate_H": fam_rep["H_PHONE_CLASS_ADAPTIVE"][1](r),
            "final_candidate_decision": best_fn(r),
            "best_rule_id": best["rule_id"],
            "decision_changed": int(best_fn(r) != ("PRESENT" if r["production_decision"] == 1
                                                   else "ABSENT")),
            "failure_type": "",
            "notes": "",
        }
        if r["truth"] == 0 and r["production_decision"] == 1:
            d["failure_type"] = ("TYPE_B_ENCODER" if r["target_max_A"] >= 0.30
                                 else "TYPE_A_ACCEPTANCE")
        if r["token_id"] in KNOWN_CASES:
            d["notes"] = "known decisive case"
        per_token.append(d)

    # subgroup analysis
    subgroups = []
    def add_sub(group_type, name, sel):
        dd = [r for r in dev + lwe if sel(r)]
        if not dd:
            return
        pm = metrics(dd, prod)
        bm = metrics(dd, best_fn)
        subgroups.append({"group_type": group_type, "group": name, "n": len(dd),
                          "present": pm["present"], "absent": pm["absent"],
                          "production_recall": pm["present_recall"],
                          "production_far": pm["far"],
                          "best_rule_recall": bm["present_recall"],
                          "best_rule_far": bm["far"],
                          "best_rule_uncertain_rate": bm["uncertain_rate"],
                          "notes": ""})
    for ph in ("ɹ", "t", "k", "s", "z", "n", "m", "p", "d", "l", "v", "f", "b", "ŋ"):
        add_sub("phone", ph, lambda r, ph=ph: r["target_phone"] == ph)
    add_sub("subgroup", "final_consonant", lambda r: r["is_final_consonant"] == 1)
    add_sub("subgroup", "non_final", lambda r: r["is_final_consonant"] == 0)
    add_sub("evidence", "weak_present", lambda r: r["truth"] == 1 and r["target_max_A"] < 0.15)
    add_sub("evidence", "strong_present", lambda r: r["truth"] == 1 and r["target_max_A"] >= 0.30)
    add_sub("evidence", "weak_absent", lambda r: r["truth"] == 0 and r["target_max_A"] < 0.15)
    add_sub("evidence", "strong_absent", lambda r: r["truth"] == 0 and r["target_max_A"] >= 0.30)
    add_sub("identity", "rank1", lambda r: r["identity_credit"] == 1
            and r["target_rank_in_top5"] == 0)
    add_sub("identity", "rank2_5", lambda r: r["identity_credit"] == 1
            and r["target_rank_in_top5"] > 0)
    add_sub("identity", "not_top5", lambda r: r["top5_identity"] == 0)
    add_sub("blank", "blank_ge_0.90", lambda r: r["blank_mean_span"] >= 0.90)
    add_sub("blank", "blank_lt_0.90", lambda r: r["blank_mean_span"] < 0.90)
    add_sub("temporal", "one_frame_support", lambda r: r["longest_run_05"] <= 1)
    add_sub("temporal", "multi_frame_support", lambda r: r["longest_run_05"] >= 2)

    # falsification tests (best rule + production, on labeled rows)
    def acc_count(rows_, fn):
        return sum(1 for r in rows_ if fn(r))
    fals = {}
    accepted = lambda r: best_fn(r) == "PRESENT"
    fals["TEST1_blank_dominated_accepted"] = {
        "production": acc_count(labeled, lambda r: r["production_decision"] == 1
                                and r["blank_mean_span"] >= 0.90),
        "best_rule": acc_count(labeled, lambda r: accepted(r)
                               and r["blank_mean_span"] >= 0.90)}
    fals["TEST2_rank25_low_support_accepted"] = {
        "production": acc_count(labeled, lambda r: r["production_decision"] == 1
                                and r["identity_credit"] == 1 and r["target_rank_in_top5"] > 0
                                and r["target_max_A"] < 0.05),
        "best_rule": acc_count(labeled, lambda r: accepted(r)
                               and r["identity_credit"] == 1 and r["target_rank_in_top5"] > 0
                               and r["target_max_A"] < 0.05)}
    fals["TEST3_competitor_stronger_accepted"] = {
        "production": acc_count(labeled, lambda r: r["production_decision"] == 1
                                and r["identity_margin_mean"] < 0),
        "best_rule": acc_count(labeled, lambda r: accepted(r)
                               and r["identity_margin_mean"] < 0)}
    fals["TEST4_one_frame_spike_accepted"] = {
        "production": acc_count(labeled, lambda r: r["production_decision"] == 1
                                and r["longest_run_05"] <= 1),
        "best_rule": acc_count(labeled, lambda r: accepted(r) and r["longest_run_05"] <= 1)}
    fals["TEST5_strong_encoder_false_peak_accepted"] = {
        "production": acc_count(labeled, lambda r: r["production_decision"] == 1
                                and r["truth"] == 0 and r["target_max_A"] >= 0.30),
        "best_rule": acc_count(labeled, lambda r: accepted(r)
                               and r["truth"] == 0 and r["target_max_A"] >= 0.30)}
    fals["TEST6_true_weak_present_destroyed"] = {
        "production_accepted": acc_count(labeled, lambda r: r["production_decision"] == 1
                                         and r["truth"] == 1 and r["target_max_A"] < 0.15),
        "best_rule_rejected": acc_count(labeled, lambda r: r["truth"] == 1
                                        and r["target_max_A"] < 0.15 and not accepted(r))}
    fals["TEST7_lwe_vs_so762"] = {
        "lwe_recall": per_rule[best["rule_id"]]["metrics"]["lwe"]["present_recall"],
        "dev_recall": per_rule[best["rule_id"]]["metrics"]["dev"]["present_recall"],
        "lwe_far": per_rule[best["rule_id"]]["metrics"]["lwe"]["far"],
        "dev_far": per_rule[best["rule_id"]]["metrics"]["dev"]["far"]}
    r_rows = [r for r in labeled if r["target_phone"] == "ɹ"]
    fals["TEST8_r_collapse"] = {
        "n_r": len(r_rows),
        "production_recall": metrics(r_rows, prod)["present_recall"],
        "best_rule_recall": metrics(r_rows, best_fn)["present_recall"],
        "best_rule_uncertain_rate": metrics(r_rows, best_fn)["uncertain_rate"]}
    blank_hi = [r for r in labeled if r["blank_mean_span"] >= 0.90]
    fals["TEST9_conservative_when_blank_high"] = {
        "n_blank_high": len(blank_hi),
        "uncertain_rate": metrics(blank_hi, best_fn)["uncertain_rate"],
        "recall": metrics(blank_hi, best_fn)["present_recall"]}
    fals["TEST10_excessive_uncertain"] = {
        "dev_uncertain_rate": per_rule[best["rule_id"]]["metrics"]["dev"]["uncertain_rate"],
        "lwe_uncertain_rate": per_rule[best["rule_id"]]["metrics"]["lwe"]["uncertain_rate"],
        "test_uncertain_rate": per_rule[best["rule_id"]]["metrics"]["test"]["uncertain_rate"]}

    # write outputs
    L.write_rows(OUT / "ACCEPTANCE_RULE_VARIANTS.csv", all_rows)
    L.write_rows(OUT / "ACCEPTANCE_PARETO_FRONTIER.csv", pareto)
    L.write_rows(OUT / "FALSE_ACCEPT_RECLASSIFICATION.csv", false_rows)
    L.write_rows(OUT / "PHONE_SUBGROUP_ANALYSIS.csv", subgroups)
    L.write_rows(OUT / "ACCEPTANCE_RULE_PER_TOKEN.csv", per_token)

    results = {
        "phase": "1.9.23",
        "production_baseline": base,
        "best_rule": {"rule_id": best["rule_id"], "family": best["family"],
                      "params": best["params"], "selection_ladder": ladder,
                      "dev": per_rule[best["rule_id"]]["metrics"]["dev"],
                      "lwe_external": per_rule[best["rule_id"]]["metrics"]["lwe"],
                      "test_speaker_disjoint": per_rule[best["rule_id"]]["metrics"]["test"],
                      "lwe_absent": per_rule[best["rule_id"]]["metrics"]["lwe_absent"],
                      "dev_absent": per_rule[best["rule_id"]]["metrics"]["dev_absent"]},
        "frr_first_safe_rules": [p["rule_id"] for p in pareto if p["frr_first_safe"]],
        "pareto_optimal_count": sum(p["pareto_optimal_dev"] for p in pareto),
        "false_accept_accounting": {
            "n_false_accepts": len(false_rows),
            "type_a_acceptance": sum(1 for r in false_rows
                                     if r["failure_type"] == "TYPE_A_ACCEPTANCE"),
            "type_b_encoder": sum(1 for r in false_rows
                                  if r["failure_type"] == "TYPE_B_ENCODER"),
            "type_a_removed_by_best": weak_removed,
            "type_b_remaining": strong_remaining,
        },
        "falsification": fals,
        "known_cases": [r for r in per_token if r["token_id"] in KNOWN_CASES],
        "flags": {"production_vad": False, "router_locked": False, "unity_integrated": False,
                  "scorer_modified": False, "production_window_locked": False},
    }
    (OUT / "EXPERIMENT_RESULTS.json").write_text(
        json.dumps(results, indent=2, ensure_ascii=False), encoding="utf-8")

    # console
    print("baseline dev:", base["dev"]["present_recall"], base["dev"]["far"],
          "| lwe:", base["lwe"]["present_recall"], base["lwe"]["far"])
    print("rules:", len(rules), "| frr_first_safe:", results["frr_first_safe_rules"])
    print("best:", best["rule_id"], "ladder:", ladder)
    print("  dev:", per_rule[best["rule_id"]]["metrics"]["dev"])
    print("  lwe:", per_rule[best["rule_id"]]["metrics"]["lwe"])
    print("  test:", per_rule[best["rule_id"]]["metrics"]["test"])
    print("false accounting:", results["false_accept_accounting"])
    print("falsification keys:", list(fals.keys()))
    print("DONE run_rule_search")


if __name__ == "__main__":
    main()
