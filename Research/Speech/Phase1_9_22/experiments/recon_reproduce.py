"""WP-1.9.22 recon — verify the cached production decisions reproduce the frozen
WP-1.9.12 archived LWE decisions (research-only)."""
from __future__ import annotations

import csv
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

CACHE = L.REPO / "Research/Speech/Phase1_9_21/artifacts/frame_cache"
ARCH = L.REPO / "Research/Speech/Phase1_9_12/Results/final_consonant_human_vs_model.csv"


def main():
    rows = list(csv.DictReader(open(CACHE / "token_evidence_lwe.csv", encoding="utf-8")))
    rows = {r["token_id"]: r for r in rows if r["window_type"] == "full"}
    arch = {}
    for r in csv.DictReader(open(ARCH, encoding="utf-8-sig")):
        tid = r["token_id"]
        if tid.startswith("fc_"):
            tid = tid[3:]
        arch[tid] = r
    n = mism = 0
    print(f"{'token':16s} {'cache':6s} {'arch':6s} {'cache_post':>10s} {'arch_post':>10s} "
          f"{'cache_span_s':>14s} {'arch_span_s':>16s} {'obs':>4s} {'sim':>6s}")
    for tid, a in arch.items():
        if tid not in rows:
            print("MISSING_IN_CACHE", tid)
            continue
        r = rows[tid]
        n += 1
        fs = float(r["frame_s"])
        s, e = int(r["span_A_start"]), int(r["span_A_end"])
        span_s = f"{s * fs:.4f}-{(e + 1) * fs:.4f}"
        arch_span = f"{float(a['ctc_final_start']):.4f}-{float(a['ctc_final_end']):.4f}"
        ok = (r["baseline_match"] == a["phone_final_match"]
              and abs(float(r["baseline_span_post"]) - float(a["phone_final_posterior"])) < 2e-3)
        if not ok:
            mism += 1
            print(f"{tid:16s} {r['baseline_match']:6s} {a['phone_final_match']:6s} "
                  f"{float(r['baseline_span_post']):10.5f} {float(a['phone_final_posterior']):10.5f} "
                  f"{span_s:>14s} {arch_span:>16s} {r['baseline_best_obs']:>4s} "
                  f"{float(r['baseline_sim']):6.3f}")
    print(f"\nLWE reproduction: {n - mism}/{n} exact, {mism} mismatch")
    # identity-credit census for the 28 (primary window)
    ident = sum(1 for tid in arch if rows[tid]["baseline_best_obs"] == rows[tid]["target_phone"])
    print(f"identity-credit (best_obs==target) on LWE 28: {ident}/28")


if __name__ == "__main__":
    main()
