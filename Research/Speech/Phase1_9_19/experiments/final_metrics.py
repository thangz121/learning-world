"""WP-1.9.19 — final aggregate metrics (inversions, stability rates, recovery)."""
from __future__ import annotations

import csv
import json
from collections import Counter, defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
OUT = REPO / "Research/Speech/Phase1_9_19"
ART = OUT / "artifacts"


def load(p):
    return list(csv.DictReader(p.open(encoding="utf-8")))


def fnum(v, d=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return d


def main():
    lwe = load(ART / "window_rows_lwe.csv")
    stab = load(OUT / "WINDOW_STABILITY.csv")
    p17 = {r["case_id"]: r for r in load(REPO / "Research/Speech/Phase1_9_17/FAILURE_CASE_ANALYSIS.csv")}

    by = defaultdict(dict)
    for r in lwe:
        by[r["token_id"]][r["window_type"]] = r

    # inversions from 1.9.17
    inv_ids = [cid for cid, r in p17.items() if r.get("window_inversion") == "1"]
    explained = []
    for tid in inv_ids:
        w = by.get(tid)
        if not w:
            explained.append({"token_id": tid, "explained": False, "why": "no window data"})
            continue
        ef = fnum(w["full"]["deletion_aware_evidence"])
        er = fnum(w["raw"]["deletion_aware_evidence"])
        df = w["full"]["research_decision"]
        dr = w["raw"]["research_decision"]
        maxe = max(fnum(x["deletion_aware_evidence"]) for x in w.values())
        why = []
        if abs(ef - er) >= 0.2:
            why.append("evidence_delta")
        if df != dr:
            why.append("decision_flip")
        if ef < 0.1 and maxe >= 0.3:
            why.append("context_recovery")
        explained.append({"token_id": tid, "E_full": ef, "E_raw": er, "max_E": round(maxe, 3),
                          "decision_full": df, "decision_raw": dr,
                          "explained": bool(why), "why": ",".join(why)})
    with open(ART / "inversion_explained.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(explained[0].keys()))
        w.writeheader()
        w.writerows(explained)

    def rate(rows, key):
        v = [int(r[key]) for r in rows]
        return round(sum(v) / len(v), 4) if v else None

    lwe_all = [r for r in stab if r["corpus"] == "lwe"]
    lwe_lab = [r for r in lwe_all if r["human_present"] != ""]
    so_test = [r for r in stab if r["corpus"] == "so762_test"]
    so_dev = [r for r in stab if r["corpus"] == "so762_dev"]
    so_adev = [r for r in stab if r["corpus"] == "so762_absent_dev"]

    present = [r for r in lwe_lab if r["human_present"] == "1"]
    absent = [r for r in lwe_lab if r["human_present"] == "0"]
    efull = {r["token_id"]: fnum(r["deletion_aware_evidence"]) for r in lwe
             if r["window_type"] == "full"}
    recovered = [r["token_id"] for r in present
                 if efull.get(r["token_id"], 0) < 0.1 and fnum(r["max_evidence"]) >= 0.3]
    unsupported = [r["token_id"] for r in present if fnum(r["max_evidence"]) < 0.1]
    absent_gain = [r["token_id"] for r in absent if fnum(r["max_evidence"]) >= 0.3
                   and fnum(r["min_evidence"]) < 0.1]

    summary = {
        "window_inversions": {
            "n_from_1_9_17": len(inv_ids),
            "explained_by_window_evidence": sum(1 for e in explained if e["explained"]),
            "details": explained},
        "stability_rates": {
            "lwe_all_token_window_sensitive": rate(lwe_all, "window_sensitive"),
            "lwe_labeled_window_sensitive": rate(lwe_lab, "window_sensitive"),
            "lwe_labeled_alignment_sensitive": rate(lwe_lab, "alignment_sensitive"),
            "so762_test_window_sensitive": rate(so_test, "window_sensitive"),
            "so762_dev_window_sensitive": rate(so_dev, "window_sensitive"),
            "so762_absent_dev_window_sensitive": rate(so_adev, "window_sensitive"),
        },
        "present_finals": {
            "n": len(present),
            "recovered_by_boundary_control": len(recovered), "recovered_ids": recovered,
            "still_unsupported_all_windows": len(unsupported), "unsupported_ids": unsupported,
        },
        "absent_finals": {
            "n": len(absent),
            "false_gain_by_context": len(absent_gain), "false_gain_ids": absent_gain,
        },
        "root_cause_labeled_present": dict(Counter(r["root_cause"] for r in present)),
        "root_cause_labeled_absent": dict(Counter(r["root_cause"] for r in absent)),
        "tokens_of_interest": sorted(set(recovered + unsupported + absent_gain)),
    }
    p = OUT / "EXPERIMENT_RESULTS.json"
    j = json.loads(p.read_text(encoding="utf-8"))
    j["final_aggregates"] = summary
    p.write_text(json.dumps(j, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
