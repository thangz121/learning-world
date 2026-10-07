"""Quick inspection of the WP-1.9.21 frame cache (research-only helper)."""
from __future__ import annotations

import argparse
import csv
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import p21_lib as L  # noqa: E402

CACHE = L.REPO / "Research/Speech/Phase1_9_21/artifacts/frame_cache"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--corpus", default="lwe")
    ap.add_argument("--token", default="")
    ap.add_argument("--windows", default="full,raw,pad100,pad250")
    ap.add_argument("--frames", action="store_true")
    args = ap.parse_args()

    wins = set(args.windows.split(","))
    rows = list(csv.DictReader(open(CACHE / f"token_evidence_{args.corpus}.csv", encoding="utf-8")))
    for r in rows:
        if r["window_type"] not in wins:
            continue
        if args.token and r["token_id"] != args.token:
            continue
        print(f"{r['token_id']:18s} {r['window_type']:6s} "
              f"A={r['span_A_start']}-{r['span_A_end']} prev={r['prev_span']:>6s} "
              f"next={r['next_span']:>6s} C={r['region_C0']}-{r['region_C1']} "
              f"nD={r['n_frames_D']:>3s} maxD={r['max_D']:>7s} peak={r['peak']:>7s} "
              f"w={r['cluster_width']:>2s} promC={r['prom_comp']:>8s} "
              f"TS={r['temporal_support']:>7s} da={r['da_span']:>6s} "
              f"daMax={r['da_max']:>7s} base={r['baseline_match']:5s} "
              f"earlier={r['earlier_same_spans']:>8s} human={r['human_label']}")
    if args.frames and args.token:
        frows = list(csv.DictReader(open(CACHE / f"frames_{args.corpus}.csv", encoding="utf-8")))
        for r in frows:
            if r["token_id"] != args.token or r["window_type"] not in wins:
                continue
            print(f"  f={r['frame']:>3s} t={r['time_ms']:>7s} p={r['target_posterior']:>7s} "
                  f"top1={r['top1_phone']:>3s} {r['top1_posterior']:>7s} "
                  f"comp={r['best_competitor_posterior']:>7s} "
                  f"blank={r['blank_posterior']:>7s} "
                  f"A={r['in_current_span']} B={r['in_region_B']} C={r['in_region_C']} "
                  f"D={r['in_region_D']} M={r['in_earlier_same_class']}")


if __name__ == "__main__":
    main()
