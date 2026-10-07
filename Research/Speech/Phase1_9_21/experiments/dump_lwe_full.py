"""Dump the LWE 28 blind labels on the primary (full) window for the report."""
from __future__ import annotations

import csv
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import p21_lib as L  # noqa: E402

CACHE = L.REPO / "Research/Speech/Phase1_9_21/artifacts/frame_cache"


def main():
    rows = list(csv.DictReader(open(CACHE / "token_evidence_lwe.csv", encoding="utf-8")))
    rows = [r for r in rows if r["window_type"] == "full" and r["human_label"]]
    rows.sort(key=lambda r: (r["human_present"], r["token_id"]))
    print(f"{'token':16s} {'ph':3s} {'label':36s} {'base':5s} {'meanA':>7s} {'maxD':>7s} "
          f"{'peak':>7s} {'w':>2s} {'promC':>7s} {'TS':>7s} {'daMax':>7s} {'earlier':>7s}")
    for r in rows:
        print(f"{r['token_id']:16s} {r['target_phone']:3s} {r['human_label']:36s} "
              f"{r['baseline_match']:5s} {r['mean_A']:>7s} {r['max_D']:>7s} {r['peak']:>7s} "
              f"{r['cluster_width']:>2s} {r['prom_comp']:>7s} {r['temporal_support']:>7s} "
              f"{r['da_max']:>7s} {r['max_masked_earlier']:>7s}")


if __name__ == "__main__":
    main()
