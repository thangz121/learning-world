"""WP-1.9.18 — decision-layer variants v2 evaluated from saved evidence (no model rerun).

Evidence used per final phone (already computed, frozen logits):
  E       = da_span_max  (max class posterior at the deletion-aware assigned
                          final-phone frame(s); candidates are monotonic in word order)
  E_alt   = frame_max    (global peak, conflates repeated phones; reported for contrast)
  margin  = deletion-aware LLR per frame (global comparison; negative even with
            local evidence when trailing silence/blank is strong — documented)
  free    = da_free_present

Variants:
  A baseline production decision
  B deletion-aware free presence
  C recall-first  : PRESENT iff E >= tp ; ABSENT iff E < ta ; else UNCERTAIN
  D safety-first  : PRESENT iff E >= tp ; ABSENT iff E < ta AND free deleted AND margin <= 0
                    ; else UNCERTAIN

Thresholds selected on dev (so762 train children) with two pre-registered
objectives: C recall >= 0.95 (min absent-false-present), D absent-false-present
= 0 (max recall). Evaluated on held-out so762 test speakers and LWE external.
"""
from __future__ import annotations

import csv
import json
from collections import Counter
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
OUT = REPO / "Research/Speech/Phase1_9_18"
ART = OUT / "artifacts"


def load(name):
    return list(csv.DictReader((ART / name).open(encoding="utf-8")))


def fnum(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return 0.0


def truth_lwe(r):
    hf = r["human_final_label"]
    if hf.endswith("PRESENT"):
        return 1
    if hf.endswith("ABSENT"):
        return 0
    return ""


def truth_so(r):
    s = fnum(r["human_phone_score"])
    return 1 if s >= 0.5 else 0


def evaluate(rows, truth, decide):
    tp = fn = fp = tn = 0
    unc_p = unc_a = 0
    for r in rows:
        y = truth(r)
        if y == "":
            continue
        d = decide(r)
        if d == "UNCERTAIN":
            if y == 1:
                unc_p += 1
            else:
                unc_a += 1
            continue
        pr = (d == "PRESENT")
        if y == 1:
            tp += pr
            fn += (not pr)
        else:
            fp += pr
            tn += (not pr)
    n_p = tp + fn + unc_p
    n_a = fp + tn + unc_a
    dec = tp + fn + fp + tn
    return {
        "n": n_p + n_a, "present": n_p, "absent": n_a,
        "present_recall": round(tp / n_p, 4) if n_p else None,
        "absent_false_present": round(fp / n_a, 4) if n_a else None,
        "absent_decided": round(tn / n_a, 4) if n_a else None,
        "uncertain_present_rate": round(unc_p / n_p, 4) if n_p else None,
        "uncertain_absent_rate": round(unc_a / n_a, 4) if n_a else None,
        "decision_coverage": round(dec / (n_p + n_a), 4) if (n_p + n_a) else None,
        "tp": tp, "fn": fn, "fp": fp, "tn": tn, "unc_p": unc_p, "unc_a": unc_a,
    }


def make_C(tp, ta):
    def f(r):
        e = fnum(r["da_span_max"])
        if e >= tp:
            return "PRESENT"
        if e < ta:
            return "ABSENT"
        return "UNCERTAIN"
    return f


def make_D(tp, ta):
    def f(r):
        e = fnum(r["da_span_max"])
        if e >= tp:
            return "PRESENT"
        if e < ta and r["da_free_present"] == "0" and fnum(r["da_margin_per_frame"]) <= 0:
            return "ABSENT"
        return "UNCERTAIN"
    return f


def frontier(dev, truth, maker, taus):
    out = []
    for tp in taus:
        for ta in taus:
            if ta > tp:
                continue
            m = evaluate(dev, truth, maker(tp, ta))
            out.append({"tp": tp, "ta": ta, **m})
    return out


def main():
    dev = load("so762_dev_finals.csv")
    test = load("so762_test_finals.csv")
    lwe = load("lwe_phone_evidence.csv")
    for r in lwe:
        r["human_present"] = truth_lwe(r)

    taus = [0.01, 0.02, 0.05, 0.10, 0.15, 0.20, 0.30, 0.40, 0.50, 0.60, 0.70]
    fC = frontier(dev, truth_so, make_C, taus)
    fD = frontier(dev, truth_so, make_D, taus)

    # C: recall >= 0.95, then min absent_false_present, then max coverage
    okC = [g for g in fC if g["present_recall"] >= 0.95]
    bestC = min(okC, key=lambda g: (g["absent_false_present"], -g["decision_coverage"])) if okC else None
    # D: absent_false_present == 0, then max recall, then max coverage
    okD = [g for g in fD if g["absent_false_present"] == 0.0]
    bestD = max(okD, key=lambda g: (g["present_recall"], g["decision_coverage"])) if okD else None

    results = {"variants_v2": {"selection": {"C_recall_first": bestC, "D_safety_first": bestD},
                               "frontier_C": fC, "frontier_D": fD}}
    if bestC:
        cf = make_C(bestC["tp"], bestC["ta"])
        results["variants_v2"]["C_recall_first"] = {
            "dev": evaluate(dev, truth_so, cf),
            "test_speaker_disjoint": evaluate(test, truth_so, cf),
            "lwe_external": evaluate(lwe, truth_lwe, cf)}
    if bestD:
        df = make_D(bestD["tp"], bestD["ta"])
        results["variants_v2"]["D_safety_first"] = {
            "dev": evaluate(dev, truth_so, df),
            "test_speaker_disjoint": evaluate(test, truth_so, df),
            "lwe_external": evaluate(lwe, truth_lwe, df)}

    # reference A/B on the same three sets
    A = lambda r: "PRESENT" if r["baseline_present"] == "1" else "ABSENT"
    B = lambda r: "PRESENT" if r["da_free_present"] == "1" else "ABSENT"
    for name, fn in (("A_baseline", A), ("B_delaware_free", B)):
        results["variants_v2"][name] = {
            "dev": evaluate(dev, truth_so, fn),
            "test_speaker_disjoint": evaluate(test, truth_so, fn),
            "lwe_external": evaluate(lwe, truth_lwe, fn)}

    # diagnostic points (NOT tuned on these sets): floor at 0.20/0.30 with the two rules
    diag = {}
    for tag, tp, ta in (("C_floor_0.20", 0.20, 0.20), ("C_floor_0.30", 0.30, 0.30),
                        ("D_floor_0.20_0.05", 0.20, 0.05), ("D_floor_0.30_0.10", 0.30, 0.10)):
        maker = make_C if tag.startswith("C") else make_D
        fn = maker(tp, ta)
        diag[tag] = {"tp": tp, "ta": ta,
                     "dev": evaluate(dev, truth_so, fn),
                     "test_speaker_disjoint": evaluate(test, truth_so, fn),
                     "lwe_external": evaluate(lwe, truth_lwe, fn),
                     "note": "diagnostic only; thresholds not selected on evaluation sets"}
    results["variants_v2"]["diagnostic_points"] = diag

    # E distributions (diagnostic)
    def dist(rows, truth):
        p = sorted(fnum(r["da_span_max"]) for r in rows if truth(r) == 1)
        a = sorted(fnum(r["da_span_max"]) for r in rows if truth(r) == 0)
        def q(v, x):
            return round(v[int(x * (len(v) - 1))], 4) if v else None
        return {"n_present": len(p), "n_absent": len(a),
                "present_E_p5_p25_med_p75": [q(p, 0.05), q(p, 0.25), q(p, 0.5), q(p, 0.75)],
                "absent_E_p50_p75_p90_p100": [q(a, 0.5), q(a, 0.75), q(a, 0.9), q(a, 1.0)]}
    results["variants_v2"]["E_distributions"] = {
        "dev_so762": dist(dev, truth_so),
        "test_so762": dist(test, truth_so),
        "lwe_external": dist(lwe, truth_lwe)}

    p = OUT / "EXPERIMENT_RESULTS.json"
    j = json.loads(p.read_text(encoding="utf-8"))
    j.update(results)
    p.write_text(json.dumps(j, indent=2), encoding="utf-8")

    print("C selection:", {k: bestC[k] for k in ("tp", "ta", "present_recall",
                                                 "absent_false_present", "decision_coverage")} if bestC else None)
    print("D selection:", {k: bestD[k] for k in ("tp", "ta", "present_recall",
                                                 "absent_false_present", "decision_coverage")} if bestD else None)
    for ds in ("dev", "test_speaker_disjoint", "lwe_external"):
        for name in ("A_baseline", "B_delaware_free", "C_recall_first", "D_safety_first"):
            if name in results["variants_v2"]:
                m = results["variants_v2"][name][ds]
                print(f"{ds:22s} {name:18s} n={m['n']:5d} rec={m['present_recall']} "
                      f"afp={m['absent_false_present']} absdet={m['absent_decided']} "
                      f"uncP={m['uncertain_present_rate']} uncA={m['uncertain_absent_rate']} "
                      f"cov={m['decision_coverage']}")
    print("E dist:", json.dumps(results["variants_v2"]["E_distributions"], indent=1))


if __name__ == "__main__":
    main()
