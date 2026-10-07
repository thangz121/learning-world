"""Inspect SUPPORT_VARIANT_RESULTS.json sections (research helper)."""
from __future__ import annotations

import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import p21_lib as L  # noqa: E402


def main():
    d = json.load(open(L.REPO / "Research/Speech/Phase1_9_21/SUPPORT_VARIANT_RESULTS.json",
                       encoding="utf-8"))
    which = sys.argv[1] if len(sys.argv) > 1 else "selection"
    if which == "selection":
        for f, s in d["dev_selection"]["per_family_selected"].items():
            print(f"{f:22s} {s['setting']:8s} {str(s['params']):42s} "
                  f"dev recall={s['dev']['present_recall']} "
                  f"absent_det={s['dev']['absent_detection']} "
                  f"cov={s['dev']['decision_coverage']}")
        print("selected family:", d["dev_selection"]["selected_family"])
        for fam in ("MAX", "TEMPORAL_SUPPORT", "LOCAL_CLUSTER", "CONSERVATIVE_SUPPORT"):
            print(f"\n{fam} dev grid:")
            for g in d["dev_grids"][fam]:
                print(f"   {str(g['params']):42s} recall={g['present_recall']} "
                      f"absent_det={g['absent_detection']} cov={g['decision_coverage']} "
                      f"unc={g['uncertain_rate']}")
    elif which == "stability":
        print(json.dumps(d["window_stability"], indent=1))
    elif which == "controls":
        print(json.dumps(d["negative_controls"], indent=1))
    elif which == "cases":
        print(json.dumps(d["known_cases"], indent=1))
    elif which == "auc":
        print(json.dumps(d["variant_auc"], indent=1))
        print(json.dumps(d["mean_a_control"], indent=1))
    elif which == "labels":
        print(json.dumps(d["label_audit"], indent=1))
    else:
        print(json.dumps(d.get(which), indent=1)[:4000])


if __name__ == "__main__":
    main()
