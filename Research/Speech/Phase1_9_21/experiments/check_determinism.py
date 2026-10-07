"""Compare cached evidence against WP-1.9.19 rows (determinism / provenance check)."""
from __future__ import annotations

import csv
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import p21_lib as L  # noqa: E402

CACHE = L.REPO / "Research/Speech/Phase1_9_21/artifacts/frame_cache"
CORPORA = {
    "lwe": "window_rows_lwe.csv",
    "so762_dev": "window_rows_so762_dev.csv",
    "so762_test": "window_rows_so762_test.csv",
    "so762_absent_dev": "window_rows_so762_absent_dev.csv",
}


def main():
    only = sys.argv[1] if len(sys.argv) > 1 else None
    for corpus, src in CORPORA.items():
        if only and corpus != only:
            continue
        tp = CACHE / f"token_evidence_{corpus}.csv"
        if not tp.exists():
            continue
        ref = {}
        for r in L.read_rows(L.P1919 / src):
            ref[(r["token_id"], r["window_type"])] = r
        rows = list(csv.DictReader(open(tp, encoding="utf-8")))
        n = mism = 0
        details = []
        for r in rows:
            key = (r["token_id"], r["window_type"])
            rr = ref.get(key)
            if rr is None:
                continue
            n += 1
            da_ok = abs(float(r["da_max"]) - float(rr["deletion_aware_evidence"])) <= 2e-3
            base_ok = r["baseline_match"] == rr["baseline_match"]
            span_ok = (int(r["span_A_start"]) == int(rr["span_start"])
                       and int(r["span_A_end"]) == int(rr["span_end"]))
            if not (da_ok and base_ok and span_ok):
                mism += 1
                if len(details) < 25:
                    details.append(f"{key[0]} {key[1]} da {r['da_max']} vs {rr['deletion_aware_evidence']} "
                                   f"base {r['baseline_match']} vs {rr['baseline_match']} "
                                   f"span {r['span_A_start']}-{r['span_A_end']} vs "
                                   f"{rr['span_start']}-{rr['span_end']}")
        print(f"{corpus}: {n - mism}/{n} match, {mism} mismatch")
        for d in details:
            print("   ", d)


if __name__ == "__main__":
    main()
