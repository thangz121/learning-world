"""Inspect reconstructed span-mean decisions (research helper)."""
from __future__ import annotations

import csv
import sys
from collections import Counter
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

ART = L.REPO / "Research/Speech/Phase1_9_22/artifacts"


def load(corpus):
    return list(csv.DictReader(open(ART / f"spanmean_decision_{corpus}.csv", encoding="utf-8")))


def main():
    which = sys.argv[1] if len(sys.argv) > 1 else "lwe"
    rows = load(which)
    if which == "lwe":
        rows = [r for r in rows if r["human_label"]]
    print(f"{'token':16s} {'ph':3s} {'truth':5s} {'match':5s} {'post':>8s} {'blank':>8s} "
          f"{'rank':>4s} {'idc':>3s} {'top1':>4s} {'top1p':>8s} {'comp':>4s} {'compp':>8s} "
          f"{'margin':>9s} {'bestobs':>7s}")
    for r in rows:
        truth = r["human_present"] if r["human_present"] != "" else "-"
        print(f"{r['token_id']:16s} {r['target_phone']:3s} {truth:5s} {r['match_type']:5s} "
              f"{r['target_post_mean']:>8s} {r['blank_mean_span']:>8s} "
              f"{r['target_rank_in_top5']:>4s} {r['identity_credit']:>3s} "
              f"{r['top1_phone']:>4s} {r['top1_post']:>8s} {r['competitor_phone']:>4s} "
              f"{r['competitor_post']:>8s} {r['identity_margin_mean']:>9s} {r['best_obs']:>7s}")
    print("\nsummary:", Counter((r["match_type"], r["identity_credit"]) for r in rows))
    print("rank distribution:", Counter(r["target_rank_in_top5"] for r in rows))


if __name__ == "__main__":
    main()
