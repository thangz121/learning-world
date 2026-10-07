"""WP-1.9.26 Part 2 — TYPE-B reassessment with the expanded encoder evidence."""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_26"
TB = OUT / "02_TYPE_B"
P24 = L.REPO / "Research/Speech/Phase1_9_24"

FIELDS = [
    "case_id", "corpus", "word", "target_phone", "human_label_status", "human_confidence",
    "label_source", "production_max_A", "production_mean_A", "peak_frame", "peak_width",
    "neighbors", "blank_at_peak", "peak_margin", "alt_max_A", "alt_delta",
    "representation_dependent", "isolated_peak", "label_limited", "acceptance_logic_limited",
    "fals_A", "fals_B", "fals_C", "fals_D", "fals_E", "fals_F", "fals_G",
    "final_classification", "notes",
]


def num(v, d=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return d


def main():
    rows = list(csv.DictReader(open(P24 / "ENCODER_TYPE_B_CASES.csv", encoding="utf-8")))
    alt = {r["token_id"]: num(r["alt_max_A"]) for r in csv.DictReader(
        open(OUT / "artifacts/alt_encoder_all.csv", encoding="utf-8"))}
    cls = {"child_07_seven": "MIXED", "014180143_15": "LABEL_LIMITED",
           "014190172_7": "MIXED", "014350146_16": "LABEL_LIMITED"}
    out = []
    for r in rows:
        tid = r["case_id"]
        a = alt.get(tid, 0.0)
        delta = round(a - float(r["target_max_A"]), 5)
        out.append({
            "case_id": tid, "corpus": r["corpus"], "word": r["word"],
            "target_phone": r["target_phone"], "human_label_status": r["human_label"],
            "human_confidence": r["human_confidence"], "label_source": r["human_label_source"],
            "production_max_A": r["target_max_A"], "production_mean_A": r["target_mean"],
            "peak_frame": r["peak_frame"], "peak_width": r["cluster_width_D"],
            "neighbors": f"{r['neighbor_prev']}/{r['neighbor_next']}",
            "blank_at_peak": r["blank_at_peak"], "peak_margin": r["peak_margin"],
            "alt_max_A": round(a, 5), "alt_delta": delta,
            "representation_dependent": int(abs(delta) >= 0.30),
            "isolated_peak": int(r["fals_C_neighbor_support"]) == 0,
            "label_limited": int(r["fals_G_confident_label"]) == 0,
            "acceptance_logic_limited": 0,
            "fals_A": r["fals_A_strong"], "fals_B": r["fals_B_window_stable"],
            "fals_C": r["fals_C_neighbor_support"], "fals_D": r["fals_D_position"],
            "fals_E": r["fals_E_beats_competitor"], "fals_F": r["fals_F_not_blank"],
            "fals_G": r["fals_G_confident_label"],
            "final_classification": cls.get(tid, "INCONCLUSIVE"),
            "notes": r["verdict_reason"],
        })
    L.write_rows(TB / "TYPE_B_CASES.csv", out, FIELDS)
    # evidence matrix (one row per case, compact)
    mfields = ["case_id", "human_label_status", "human_confidence", "production_max_A",
               "alt_max_A", "alt_delta", "representation_dependent", "isolated_peak",
               "label_limited", "acceptance_logic_limited", "final_classification", "notes"]
    L.write_rows(TB / "TYPE_B_EVIDENCE_MATRIX.csv",
                 [{k: r[k] for k in mfields} for r in out], mfields)
    summary = {"n": len(out), "classifications": {c: sum(1 for r in out
                                                         if r["final_classification"] == c)
                                                   for c in set(r["final_classification"]
                                                                for r in out)},
               "isolated_peak": sum(r["isolated_peak"] for r in out),
               "label_limited": sum(r["label_limited"] for r in out),
               "representation_dependent": sum(r["representation_dependent"] for r in out)}
    (OUT / "artifacts" / "typeb_summary.json").write_text(
        json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=1))
    print("DONE build_typeb_26")


if __name__ == "__main__":
    main()
