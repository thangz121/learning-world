"""WP-1.9.21 — temporally plausible support aggregation analysis (research-only).

Reads the reusable frame cache (artifacts/frame_cache), defines research-only
support variants over the position-masked candidate regions, selects at most one
small pre-declared family on speaker-disjoint SO762 train children, freezes it,
then evaluates on the external LWE blind labels, SO762 held-out test speakers,
negative controls, window stability, human-uncertain cases and the known-case
replay. No production code, no training, no Unity.
"""
from __future__ import annotations

import csv
import json
import math
import statistics as st
import sys
from collections import Counter, defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_21"
CACHE = OUT / "artifacts/frame_cache"
WINDOWS = L.WINDOWS
PRIMARY = "full"          # production construction: production_vad=false -> full recording
NUM_FIELDS = [
    "mean_A", "max_A", "region_top3_A", "mean_B", "max_B", "region_top3_B",
    "mean_C", "max_C", "region_top3_C", "mean_D", "max_D", "region_top3_D",
    "top1_D", "top2_D", "top3_D", "top5_D", "width1_D", "width2_D", "width3_D",
    "width5_D", "peak", "cluster_mean", "neighbor_support", "comp_peak",
    "blank_peak", "prom_comp", "prom_blank", "prom_neighbor", "temporal_support",
    "max_global", "max_masked_earlier", "max_later_same", "baseline_span_post",
    "da_max",
]
INT_FIELDS = ["peak_frame", "cluster_width", "n_frames_D", "n_earlier_same",
              "target_phone_index_utt", "argmax_global", "argmax_masked_earlier",
              "argmax_later_same", "da_frame", "baseline_present", "is_final_consonant",
              "span_A_start", "span_A_end", "region_C0", "region_C1"]

KNOWN_CASES = ["child_01_nine", "child_07_one", "child_01_seven", "child_02_ten",
               "child_04_four", "child_01_ten", "child_06_six", "child_01_four",
               "child_03_four", "child_06_four", "child_02_four", "child_07_seven",
               "child_03_six"]

# pre-declared settings grids (documented; no per-K search beyond these)
GRID = {
    "MAX": [("max", {"tau": t}) for t in (0.01, 0.02, 0.05, 0.10, 0.15, 0.20, 0.30,
                                          0.40, 0.50, 0.60)],
    "TOP2": [("top2", {"tau": t}) for t in (0.01, 0.02, 0.05, 0.10, 0.15, 0.20, 0.30,
                                            0.40, 0.50)],
    "TOP3": [("top3", {"tau": t}) for t in (0.01, 0.02, 0.05, 0.10, 0.15, 0.20, 0.30,
                                            0.40, 0.50)],
    "TOP5": [("top5", {"tau": t}) for t in (0.01, 0.02, 0.05, 0.10, 0.15, 0.20, 0.30,
                                            0.40, 0.50)],
    "LOCAL_CLUSTER": [("cluster", {"tp": p, "w": w, "tn": n})
                      for p in (0.20, 0.30, 0.40, 0.50)
                      for w in (1, 2)
                      for n in (0.0, 0.05)],
    "PEAK_PROMINENCE": [("prom", {"tp": p, "tm": m})
                        for p in (0.15, 0.25, 0.35)
                        for m in (0.0, 0.10, 0.20)],
    "TEMPORAL_SUPPORT": [("ts", {"tau": t})
                         for t in (0.01, 0.02, 0.05, 0.10, 0.15, 0.20, 0.25, 0.30, 0.40)],
    "CONSERVATIVE_SUPPORT": [("cons", {"thi": hi, "tlo": lo})
                             for hi in (0.15, 0.20, 0.25, 0.30)
                             for lo in (0.05, 0.10)
                             if lo < hi],
}


def to_float(v, default=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return default


def load_corpus(corpus):
    rows = list(csv.DictReader(open(CACHE / f"token_evidence_{corpus}.csv", encoding="utf-8")))
    for r in rows:
        for f in NUM_FIELDS:
            r[f] = to_float(r.get(f))
        for f in INT_FIELDS:
            r[f] = int(to_float(r.get(f), -1))
        hp = r.get("human_present", "")
        r["human_present"] = int(hp) if hp != "" else None
        r["truth"] = r["human_present"]  # LWE blind labels / so762 human phone score >= 0.5
    return rows


def by_token(rows):
    d = defaultdict(dict)
    for r in rows:
        d[r["token_id"]][r["window_type"]] = r
    return d


def decide_current(r):
    return "PRESENT" if r["baseline_present"] == 1 else "ABSENT"


def make_decision(family, params):
    if family == "MAX":
        return lambda r: "PRESENT" if r["max_D"] >= params["tau"] else "ABSENT"
    if family == "TOP2":
        return lambda r: "PRESENT" if r["top2_D"] >= params["tau"] else "ABSENT"
    if family == "TOP3":
        return lambda r: "PRESENT" if r["top3_D"] >= params["tau"] else "ABSENT"
    if family == "TOP5":
        return lambda r: "PRESENT" if r["top5_D"] >= params["tau"] else "ABSENT"
    if family == "LOCAL_CLUSTER":
        def f(r):
            return ("PRESENT" if (r["peak"] >= params["tp"]
                                  and r["cluster_width"] >= params["w"]
                                  and r["neighbor_support"] >= params["tn"]) else "ABSENT")
        return f
    if family == "PEAK_PROMINENCE":
        return lambda r: ("PRESENT" if (r["peak"] >= params["tp"]
                                        and r["prom_comp"] >= params["tm"]) else "ABSENT")
    if family == "TEMPORAL_SUPPORT":
        return lambda r: "PRESENT" if r["temporal_support"] >= params["tau"] else "ABSENT"
    if family == "CONSERVATIVE_SUPPORT":
        def f(r):
            if r["temporal_support"] >= params["thi"]:
                return "PRESENT"
            if r["temporal_support"] <= params["tlo"]:
                return "ABSENT"
            return "UNCERTAIN"
        return f
    raise ValueError(family)


def metrics(rows, dec):
    tp = fn = fp = tn = unc = 0
    unsupported = false_support = 0
    wrong_occ = 0
    for r in rows:
        if r["truth"] is None:
            continue
        d = dec(r)
        if d == "UNCERTAIN":
            unc += 1
            continue
        present = d == "PRESENT"
        y = r["truth"]
        if y == 1:
            tp += present
            fn += (not present)
        else:
            fp += present
            tn += (not present)
        if present:
            if r["peak"] < 0.15 or r["prom_comp"] <= 0:
                unsupported += 1
            if r["prom_comp"] <= 0:
                false_support += 1
        if r["max_masked_earlier"] >= 0.30:
            wrong_occ += 1
    n = tp + fn + fp + tn + unc
    dec_n = tp + fn + fp + tn
    return {
        "n": n, "present": tp + fn, "absent": fp + tn,
        "present_recall": round(tp / (tp + fn), 4) if (tp + fn) else None,
        "frr": round(fn / (tp + fn), 4) if (tp + fn) else None,
        "absent_detection": round(tn / (fp + tn), 4) if (fp + tn) else None,
        "far": round(fp / (fp + tn), 4) if (fp + tn) else None,
        "unsupported_present": unsupported,
        "unsupported_present_rate": round(unsupported / n, 4) if n else None,
        "false_support": false_support,
        "false_support_rate": round(false_support / n, 4) if n else None,
        "wrong_occurrence": wrong_occ,
        "wrong_occurrence_rate": round(wrong_occ / n, 4) if n else None,
        "uncertain": unc, "uncertain_rate": round(unc / n, 4) if n else None,
        "decision_coverage": round(dec_n / n, 4) if n else None,
    }


def auc(scores, labels):
    pos = [s for s, y in zip(scores, labels) if y == 1]
    neg = [s for s, y in zip(scores, labels) if y == 0]
    if not pos or not neg:
        return None
    wins = 0.0
    for a in pos:
        for b in neg:
            wins += 1.0 if a > b else (0.5 if a == b else 0.0)
    return round(wins / (len(pos) * len(neg)), 4)


def frr_rank(m, recall_floor):
    """FRR-first ranking: settings meeting the dev recall floor first (then max absent
    detection); if none meets the floor, the max-recall setting wins."""
    recall = m["present_recall"] or 0.0
    if recall >= recall_floor:
        return (1, m["absent_detection"] or 0.0, m["decision_coverage"] or 0.0, recall)
    return (0, recall, m["absent_detection"] or 0.0, m["decision_coverage"] or 0.0)


def select_family(family, dev_rows, recall_floor):
    best = None
    table = []
    for name, params in GRID[family]:
        dec = make_decision(family, params)
        m = metrics(dev_rows, dec)
        table.append({"family": family, "setting": name, "params": params, **m})
        if best is None or frr_rank(m, recall_floor) > frr_rank(best[3], recall_floor):
            best = (name, params, dec, m)
    return best, table


def cluster_bounds(p, D, tstar):
    """Half-height contiguous cluster around the peak (same rule as the cache)."""
    peak = p[tstar]
    floor = max(0.5 * peak, 0.05)
    lo = tstar
    while lo - 1 >= 0 and D[lo - 1] and p[lo - 1] >= floor:
        lo -= 1
    hi = tstar
    while hi + 1 < len(p) and D[hi + 1] and p[hi + 1] >= floor:
        hi += 1
    return lo, hi


def main():
    dev = load_corpus("so762_dev")
    absdev = load_corpus("so762_absent_dev")
    test = load_corpus("so762_test")
    lwe = load_corpus("lwe")

    dev_all = dev + absdev
    dev_primary = [r for r in dev_all if r["window_type"] == PRIMARY]
    test_primary = [r for r in test if r["window_type"] == PRIMARY]
    lwe_labeled = [r for r in lwe if r["window_type"] == PRIMARY and r["truth"] is not None]
    lwe_unc = [r for r in lwe if r["window_type"] == PRIMARY
               and r["truth"] is None and r["human_verdict"] == "AMBIGUOUS"]
    lwe_unlabeled = [r for r in lwe if r["window_type"] == PRIMARY
                     and r["truth"] is None and r["human_verdict"] != "AMBIGUOUS"]

    results = {
        "phase": "1.9.21",
        "primary_window": PRIMARY,
        "primary_window_rationale": "production_vad=false -> the production construction "
                                    "scores the full recording; raw/pad100/pad250 are the "
                                    "WP-1.9.19 stability windows",
        "datasets": {
            "dev": {"source": "so762 train children (dev12) + absent-enriched train children",
                    "tokens": len(dev_primary),
                    "present": sum(1 for r in dev_primary if r["truth"] == 1),
                    "absent": sum(1 for r in dev_primary if r["truth"] == 0)},
            "test": {"source": "so762 test children (speaker-disjoint)",
                     "tokens": len(test_primary),
                     "present": sum(1 for r in test_primary if r["truth"] == 1),
                     "absent": sum(1 for r in test_primary if r["truth"] == 0),
                     "ground_truth_note": "so762 score 0 can mean incorrect OR missed; "
                                          "test set here is present-heavy"},
            "lwe": {"source": "LWE blind final-consonant labels (WP-1.9.12)",
                    "labeled": len(lwe_labeled),
                    "present": sum(1 for r in lwe_labeled if r["truth"] == 1),
                    "absent": sum(1 for r in lwe_labeled if r["truth"] == 0),
                    "human_uncertain_ambiguous": len(lwe_unc),
                    "unlabeled_no_verdict": len(lwe_unlabeled)},
        },
        "flags": {"production_vad": False, "router_locked": False, "unity_integrated": False,
                  "scorer_modified": False, "production_window_locked": False},
    }

    # ---- baseline (production-style span mean decision) ----
    base_dev = metrics(dev_primary, decide_current)
    base_lwe = metrics(lwe_labeled, decide_current)
    base_test = metrics(test_primary, decide_current)
    # FRR-first floor: dev present recall must not fall below the production dev recall
    recall_floor = base_dev["present_recall"] or 0.0
    results["current_mean"] = {
        "definition": "production soft_match decision on the production span "
                      "(exact|soft via span mean + top-1 identity); NOT a mean-posterior "
                      "threshold - its recall is top-1-identity driven",
        "dev": base_dev, "lwe_external": base_lwe, "test_speaker_disjoint": base_test,
        "recall_floor_for_selection": round(recall_floor, 4),
    }
    # pure mean-posterior control curve (the actual "mean aggregation" targeted by 1.9.20)
    results["mean_a_control"] = {}
    for tau in (0.02, 0.05, 0.10, 0.15, 0.20, 0.30):
        dec = lambda r, t=tau: "PRESENT" if r["mean_A"] >= t else "ABSENT"
        results["mean_a_control"][f"tau{tau}"] = {
            "lwe_external": metrics(lwe_labeled, dec),
            "dev": metrics(dev_primary, dec),
        }

    # ---- dev selection ----
    family_tables = {}
    selected = {}
    for fam in GRID:
        best, table = select_family(fam, dev_primary, recall_floor)
        family_tables[fam] = table
        selected[fam] = {"setting": best[0], "params": best[1], "dev": best[3]}
    results["dev_grids"] = family_tables
    # overall best: FRR-first ranking on the selected dev point; ties -> the more
    # temporally constrained family wins (pre-declared preference order)
    order = ["TEMPORAL_SUPPORT", "LOCAL_CLUSTER", "PEAK_PROMINENCE",
             "CONSERVATIVE_SUPPORT", "TOP2", "TOP3", "TOP5", "MAX"]
    best_fam = max(order, key=lambda f: (frr_rank(selected[f]["dev"], recall_floor),
                                         -order.index(f)))
    results["dev_selection"] = {
        "rule": "pre-declared small grids; FRR-first: require dev present recall >= "
                f"{round(recall_floor,4)} (the production dev recall - no FRR increase); then "
                "maximise absent detection, then coverage; settings that cannot reach the "
                "floor fall back to their max-recall point (flagged in dev_grids)",
        "selected_family": best_fam,
        "selected_setting": selected[best_fam],
        "per_family_selected": {f: selected[f] for f in GRID},
    }

    # ---- evaluate all variants at frozen settings ----
    variant_rows = {}
    for fam in GRID:
        dec = make_decision(fam, selected[fam]["params"])
        variant_rows[fam] = {
            "dev": metrics(dev_primary, dec),
            "lwe_external": metrics(lwe_labeled, dec),
            "test_speaker_disjoint": metrics(test_primary, dec),
            "decisions_lwe": [{"token_id": r["token_id"], "decision": dec(r),
                               "truth": r["truth"]} for r in lwe_labeled],
        }
    results["variants"] = variant_rows
    results["variant_auc"] = {}
    for name, field in (("CURRENT_MEAN", "mean_A"), ("MAX", "max_D"), ("TOP3", "top3_D"),
                        ("TOP5", "top5_D"), ("LOCAL_CLUSTER", "peak"),
                        ("TEMPORAL_SUPPORT", "temporal_support")):
        scores_dev = [r[field] for r in dev_primary if r["truth"] is not None]
        labels_dev = [r["truth"] for r in dev_primary if r["truth"] is not None]
        scores_lwe = [r[field] for r in lwe_labeled]
        labels_lwe = [r["truth"] for r in lwe_labeled]
        results["variant_auc"][name] = {"dev": auc(scores_dev, labels_dev),
                                        "lwe_external": auc(scores_lwe, labels_lwe)}

    # full threshold curves on LWE (external) for every pre-declared setting
    curves = {}
    for fam, settings in GRID.items():
        rows_c = []
        for name, params in settings:
            dec = make_decision(fam, params)
            m = metrics(lwe_labeled, dec)
            rows_c.append({"setting": name, "params": params,
                           "present_recall": m["present_recall"], "far": m["far"],
                           "uncertain_rate": m["uncertain_rate"],
                           "false_support": m["false_support"],
                           "unsupported_present": m["unsupported_present"]})
        curves[fam] = rows_c
    results["lwe_threshold_curves"] = curves
    # matched-recall comparison: best MAX point with LWE recall >= production recall
    prod_recall = base_lwe["present_recall"] or 1.0
    match_rows = []
    for tau in (0.005, 0.01, 0.015, 0.02, 0.03, 0.05, 0.075, 0.10, 0.15, 0.20, 0.30,
                0.40, 0.50, 0.60):
        dec = lambda r, t=tau: "PRESENT" if r["max_D"] >= t else "ABSENT"
        m = metrics(lwe_labeled, dec)
        match_rows.append({"tau": tau, "present_recall": m["present_recall"], "far": m["far"],
                           "unsupported_present": m["unsupported_present"]})
    eligible = [r for r in match_rows if (r["present_recall"] or 0) >= prod_recall]
    results["matched_recall_max"] = {
        "production_lwe_recall": prod_recall,
        "production_lwe_far": base_lwe["far"],
        "points": match_rows,
        "best_at_matched_recall": (min(eligible, key=lambda r: r["far"]) if eligible else None),
    }

    best_dec = make_decision(best_fam, selected[best_fam]["params"])

    # ---- negative controls ----
    neg = {"lwe_absent": {}, "so762_absent_enriched": {}, "unmasked_control": {}}
    lwe_absent = [r for r in lwe_labeled if r["truth"] == 0]
    abs_only = [r for r in dev_primary if r["truth"] == 0]
    for fam in GRID:
        dec = make_decision(fam, selected[fam]["params"])
        neg["lwe_absent"][fam] = metrics(lwe_absent, dec)
        neg["so762_absent_enriched"][fam] = metrics(abs_only, dec)
    # unmasked global-max control (NOT a candidate; shows the wrong-occurrence trap)
    for tau in (0.20, 0.30, 0.50):
        dec = lambda r, t=tau: "PRESENT" if r["max_global"] >= t else "ABSENT"
        neg["unmasked_control"][f"MAX_GLOBAL_TAU{tau}"] = {
            "lwe_absent": metrics(lwe_absent, dec),
            "lwe_labeled": metrics(lwe_labeled, dec),
            "wrong_occurrence_cases": [r["token_id"] for r in lwe_labeled
                                       if r["max_masked_earlier"] >= 0.30
                                       and r["max_global"] == r["max_masked_earlier"]],
        }
    results["negative_controls"] = neg

    # ---- window stability ----
    stab = {}
    lwe_by_tok = by_token([r for r in lwe if r["truth"] is not None])
    test_by_tok = by_token(test)
    dev_by_tok = by_token(dev_all)
    for tag, toks in (("lwe_external", lwe_by_tok), ("so762_test", test_by_tok),
                      ("so762_dev", dev_by_tok)):
        fam_dec = {}
        for fam in GRID:
            dec = make_decision(fam, selected[fam]["params"])
            counts = Counter()
            flips = 0
            ts_var = []
            for tid, wins in toks.items():
                ds = []
                for w in WINDOWS:
                    r = wins.get(w)
                    if r is None or r["truth"] is None:
                        continue
                    ds.append(dec(r))
                if not ds:
                    continue
                if all(d == "PRESENT" for d in ds):
                    counts["stable_present"] += 1
                elif all(d == "ABSENT" for d in ds):
                    counts["stable_absent"] += 1
                elif any(d == "UNCERTAIN" for d in ds):
                    counts["uncertain_mixed"] += 1
                else:
                    counts["window_sensitive"] += 1
                if len(set(ds)) > 1:
                    flips += 1
                vals = [wins[w]["temporal_support"] for w in WINDOWS if w in wins]
                if len(vals) > 1:
                    ts_var.append(st.stdev(vals))
            n = sum(counts.values())
            fam_dec[fam] = {"n": n, **counts, "flips": flips,
                            "flip_rate": round(flips / n, 4) if n else None,
                            "support_std_median": round(st.median(ts_var), 4) if ts_var else None}
        stab[tag] = fam_dec
    results["window_stability"] = stab

    # ---- sparse peak analysis ----
    def peak_profile(rows):
        widths = [r["cluster_width"] for r in rows]
        return {
            "n": len(rows),
            "width_1": sum(1 for w in widths if w == 1),
            "width_2": sum(1 for w in widths if w == 2),
            "width_ge3": sum(1 for w in widths if w >= 3),
            "width_median": st.median(widths) if widths else None,
            "peak_median": round(st.median([r["peak"] for r in rows]), 4) if rows else None,
            "ts_median": round(st.median([r["temporal_support"] for r in rows]), 4) if rows else None,
        }
    sparse = {
        "dev_present": peak_profile([r for r in dev_primary if r["truth"] == 1]),
        "dev_absent": peak_profile([r for r in dev_primary if r["truth"] == 0]),
        "lwe_present": peak_profile([r for r in lwe_labeled if r["truth"] == 1]),
        "lwe_absent": peak_profile([r for r in lwe_labeled if r["truth"] == 0]),
        "auc_width_scores_dev": {},
        "auc_width_scores_lwe": {},
    }
    for w in (1, 2, 3, 5):
        f = f"width{w}_D"
        sparse["auc_width_scores_dev"][f] = auc(
            [r[f] for r in dev_primary if r["truth"] is not None],
            [r["truth"] for r in dev_primary if r["truth"] is not None])
        sparse["auc_width_scores_lwe"][f] = auc([r[f] for r in lwe_labeled],
                                                [r["truth"] for r in lwe_labeled])
    results["sparse_peak"] = sparse

    # ---- human-uncertain analysis ----
    unc_report = {}
    for fam in list(GRID) + ["CURRENT_MEAN"]:
        dec = decide_current if fam == "CURRENT_MEAN" else make_decision(fam, selected[fam]["params"])
        unc_report[fam] = dict(Counter(dec(r) for r in lwe_unc))
    results["human_uncertain"] = {
        "n_ambiguous": len(lwe_unc),
        "tokens": [r["token_id"] for r in lwe_unc],
        "decisions": unc_report,
        "unlabeled_no_verdict_n": len(lwe_unlabeled),
        "unlabeled_decisions": dict(Counter(best_dec(r) for r in lwe_unlabeled)),
        "note": "human word-level AMBIGUOUS is never used as binary ground truth; the system "
                "is not rewarded for confident decisions here",
    }

    # ---- known-case replay ----
    lwe_wins = by_token(lwe)
    lwe_frames = defaultdict(list)
    for x in csv.DictReader(open(CACHE / "frames_lwe.csv", encoding="utf-8")):
        lwe_frames[(x["token_id"], x["window_type"])].append(x)
    case_rows = []
    trace_rows = []
    reclass_rows = []
    prev_cause = {}
    for r in csv.DictReader(open(L.REPO / "Research/Speech/Phase1_9_20/"
                                 "ALIGNMENT_FAILURE_RECLASSIFICATION.csv", encoding="utf-8")):
        prev_cause[r["case_id"]] = r.get("new_class", r.get("root_cause", ""))
    for cid in KNOWN_CASES:
        wins = lwe_wins.get(cid)
        if not wins:
            continue
        r = wins.get(PRIMARY)
        if r is None:
            continue
        decs = {}
        for fam in list(GRID) + ["CURRENT_MEAN"]:
            dec = decide_current if fam == "CURRENT_MEAN" else make_decision(fam, selected[fam]["params"])
            decs[fam] = dec(r)
        # region bounds from the frames of the primary window
        fr = sorted(lwe_frames.get((cid, PRIMARY), []), key=lambda x: int(x["frame"]))
        if not fr:
            continue
        reg_frames = [int(x["frame"]) for x in fr if x["in_region_D"] == "1"]
        r_start = min(reg_frames) if reg_frames else -1
        r_end = max(reg_frames) if reg_frames else -1
        p = [to_float(x["target_posterior"]) for x in fr]
        D = [x["in_region_D"] == "1" for x in fr]
        tstar = r["peak_frame"]
        if 0 <= tstar < len(p) and D[tstar]:
            clo, chi = cluster_bounds(p, D, tstar)
        else:
            clo, chi = -1, -1
        win_dec = [decs[best_fam]]
        ds_all = []
        for w in WINDOWS:
            rr = wins.get(w)
            if rr is not None:
                ds_all.append(best_dec(rr))
        if all(d == "PRESENT" for d in ds_all):
            wstab = "STABLE_PRESENT"
        elif all(d == "ABSENT" for d in ds_all):
            wstab = "STABLE_ABSENT"
        elif any(d == "UNCERTAIN" for d in ds_all):
            wstab = "UNCERTAIN_MIXED"
        else:
            wstab = "WINDOW_SENSITIVE"
        in_a = r["span_A_start"] <= tstar <= r["span_A_end"] if tstar >= 0 else False
        in_c = (r["region_C0"] <= tstar <= r["region_C1"]) if tstar >= 0 else False
        final_pos = 1 if (in_a or in_c) else 0
        if r["peak"] < 0.05:
            support_class = "NO_SUPPORT"
        elif r["prom_comp"] <= 0:
            support_class = "COMPETITOR_DOMINATED"
        elif r["peak"] >= 0.5 and r["cluster_width"] >= 2:
            support_class = "STRONG_COHERENT"
        elif r["peak"] >= 0.5:
            support_class = "STRONG_SPARSE"
        elif r["cluster_width"] >= 2:
            support_class = "WEAK_COHERENT"
        else:
            support_class = "WEAK_SPARSE"
        current_dec = decide_current(r)
        research_dec = decs[best_fam]
        fixed = int(current_dec == "ABSENT" and research_dec == "PRESENT" and r["truth"] == 1)
        regressed = int(current_dec == "PRESENT" and research_dec == "ABSENT" and r["truth"] == 1)
        wrong_occ = int(r["max_masked_earlier"] >= 0.30)
        false_sup = int(r["prom_comp"] <= 0)
        case_rows.append({
            "case_id": cid, "speaker_id": r["speaker_id"], "word": r["word"],
            "target_phone": r["target_phone"], "human_label": r["human_label"],
            "candidate_region_start": r_start, "candidate_region_end": r_end,
            "current_mean": round(r["mean_A"], 5), "max_peak": round(r["peak"], 5),
            "top2_mean": round(r["top2_D"], 5), "top3_mean": round(r["top3_D"], 5),
            "top5_mean": round(r["top5_D"], 5), "cluster_peak": round(r["peak"], 5),
            "cluster_width": r["cluster_width"],
            "peak_prominence": round(r["prom_comp"], 5),
            "competitor_peak": round(r["comp_peak"], 5),
            "blank_peak": round(r["blank_peak"], 5),
            "final_position_score": final_pos,
            "temporal_plausibility": round(r["temporal_support"], 5),
            "window_stability": wstab,
            "wrong_occurrence": wrong_occ, "false_support": false_sup,
            "support_class": support_class,
            "current_decision": current_dec, "research_decision": research_dec,
            "fixed": fixed, "regressed": regressed,
            "uncertain": int(research_dec == "UNCERTAIN"),
            "root_cause": prev_cause.get(cid, "NOT_ALIGNMENT"),
            "notes": f"baseline={r['baseline_match']}; da_1919={r['da_max']:.4f}; "
                     f"earlier_same={r['earlier_same_spans']}; masked_earlier_max="
                     f"{r['max_masked_earlier']:.4f}; "
                     f"plateau_MAX_tau0.3="
                     f"{'PRESENT' if r['max_D'] >= 0.3 else 'ABSENT'}",
        })
        # frame trace for the primary window
        for x in fr:
            t = int(x["frame"])
            trace_rows.append({
                "case_id": cid, "frame": t, "time_ms": x["time_ms"],
                "target_posterior": x["target_posterior"],
                "top1_phone": x["top1_phone"], "top1_posterior": x["top1_posterior"],
                "competitor_posterior": x["best_competitor_posterior"],
                "blank_posterior": x["blank_posterior"],
                "final_region": int(x["in_region_D"] == "1"),
                "current_span": int(x["in_current_span"] == "1"),
                "peak_member": int(t == tstar),
                "cluster_member": int(clo <= t <= chi) if clo >= 0 else 0,
                "support_member": int(x["in_region_D"] == "1"),
            })
        # reclassification
        if wrong_occ and r["max_masked_earlier"] >= 0.30:
            new_cause = "WRONG_OCCURRENCE_MASKED"
        elif r["prom_comp"] <= 0:
            new_cause = "FALSE_PEAK_COMPETITOR"
        elif r["peak"] < 0.05:
            new_cause = "ENCODER_NO_EVIDENCE"
        elif r["cluster_width"] == 1 and r["peak"] >= 0.3:
            new_cause = "TRUE_SPARSE_SUPPORT"
        elif r["peak"] >= 0.3:
            new_cause = "TRUE_COHERENT_SUPPORT"
        else:
            new_cause = "WEAK_INCONCLUSIVE"
        reclass_rows.append({
            "case_id": cid, "previous_root_cause": prev_cause.get(cid, ""),
            "new_root_cause": new_cause,
            "aggregation_fixed": int(fixed),
            "wrong_occurrence": wrong_occ,
            "window_dependency": wstab,
            "false_support": false_sup,
            "encoder_gap": int(r["peak"] < 0.05),
            "final_status": f"{current_dec}->{research_dec}",
            "evidence": f"peak={r['peak']:.4f} prom={r['prom_comp']:.4f} "
                        f"w={r['cluster_width']} TS={r['temporal_support']:.4f} "
                        f"human={r['human_label']}",
        })

    results["known_cases"] = {"n": len(case_rows),
                              "fixed": sum(c["fixed"] for c in case_rows),
                              "regressed": sum(c["regressed"] for c in case_rows),
                              "rows": case_rows}

    # ---- label sufficiency audit (bounded; no reviewer in-session) ----
    label_candidates = []
    lwe_all = [r for r in lwe if r["window_type"] == PRIMARY]
    labeled_ids = {r["token_id"] for r in lwe_labeled}
    for r in sorted(lwe_all, key=lambda x: (x["target_phone"] != "ɹ", -(x["max_D"]))):
        if r["token_id"] in labeled_ids:
            continue
        if not r["is_final_consonant"]:
            continue
        label_candidates.append({
            "candidate_id": r["token_id"], "speaker_id": r["speaker_id"],
            "word": r["word"], "target_phone": r["target_phone"],
            "current_support_peak": round(r["max_D"], 5),
            "current_temporal_support": round(r["temporal_support"], 5),
            "human_verdict": r["human_verdict"],
            "review_status": "NOT_REVIEWED (no human listener available in this session)",
        })
    label_candidates = label_candidates[:20]
    results["label_audit"] = {
        "existing_blind_labels": len(lwe_labeled),
        "new_confident_present_labels_collected": 0,
        "reviewer_count": 0,
        "candidate_pack": len(label_candidates),
        "r_present_labels_existing": sum(1 for r in lwe_labeled
                                         if r["target_phone"] == "ɹ" and r["truth"] == 1),
        "r_absent_labels_existing": sum(1 for r in lwe_labeled
                                        if r["target_phone"] == "ɹ" and r["truth"] == 0),
        "note": "no additional human final-consonant labels exist in the repo beyond the 28 "
                "blind LWE labels; other human reviews (1.9.12 window clarity, 1.9.14 p1 "
                "word verdicts, 1.9.15 assessability) are not final-consonant presence "
                "labels and were not converted. Review tooling: "
                "Research/Speech/Phase1_9_15/experiments/serve_review.py; candidate list "
                "written to LABEL_CANDIDATES.csv (no audio copied into the repo).",
    }

    # ---- write outputs ----
    write = L.write_rows
    write(OUT / "SUPPORT_CASE_ANALYSIS.csv", case_rows)
    write(OUT / "SUPPORT_FRAME_TRACE.csv", trace_rows)
    write(OUT / "SUPPORT_FAILURE_RECLASSIFICATION.csv", reclass_rows)
    write(OUT / "LABEL_CANDIDATES.csv", label_candidates)
    write(OUT / "NEW_HUMAN_LABELS.csv", [], fields=[
        "label_id", "speaker_id", "word", "target_phone", "human_label", "confidence",
        "reviewer_count", "review_notes", "source_corpus", "research_only"])
    (OUT / "SUPPORT_VARIANT_RESULTS.json").write_text(
        json.dumps(results, indent=2, ensure_ascii=False), encoding="utf-8")

    # ---- console summary ----
    print("CURRENT_MEAN dev:", base_dev["present_recall"], "lwe:", base_lwe["present_recall"],
          base_lwe["far"], "test:", base_test["present_recall"])
    print("selected:", best_fam, selected[best_fam]["params"])
    for fam in order:
        m = variant_rows[fam]["lwe_external"]
        print(f"  {fam:22s} lwe recall={m['present_recall']} far={m['far']} "
              f"unc={m['uncertain_rate']} unsupported={m['unsupported_present']}")
    print("LWE decisions best:", dict(Counter(d["decision"] for d in variant_rows[best_fam]["decisions_lwe"])))
    mr = results["matched_recall_max"]
    print("matched-recall MAX:", mr["best_at_matched_recall"],
          "| production far", mr["production_lwe_far"])
    for fam in ("MAX", "TOP3", "TEMPORAL_SUPPORT"):
        print(f"  curve {fam}:", [(c["params"], c["present_recall"], c["far"])
                                  for c in curves[fam]])
    print("DONE analyze_support")


if __name__ == "__main__":
    main()
