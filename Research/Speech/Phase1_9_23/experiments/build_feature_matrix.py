"""WP-1.9.23 Part 1 — per-token research feature vector (research-only).

Builds the feature matrix from EXISTING artifacts only:
  - WP-1.9.22 spanmean_decision_<corpus>.csv (production decision path, primary window)
  - WP-1.9.21 token_evidence_<corpus>.csv (aggregation + all 4 windows)
  - WP-1.9.21 frames_<corpus>.csv (per-frame evidence)
  - WP-1.9.22 WINDOW_IDENTITY_ANALYSIS.csv (identity stability)
No encoder rerun; no production change.
"""
from __future__ import annotations

import csv
import json
import statistics as st
import sys
from collections import defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_23"
ART = OUT / "artifacts"
P21 = L.REPO / "Research/Speech/Phase1_9_21/artifacts/frame_cache"
P22 = L.REPO / "Research/Speech/Phase1_9_22"
CORPORA = ["lwe", "so762_dev", "so762_test", "so762_absent_dev"]
PRIMARY = "full"
WINDOWS = L.WINDOWS

FIELDS = [
    "token_id", "corpus", "speaker_id", "word", "target_phone", "is_final_consonant",
    "human_label", "truth", "human_verdict", "production_decision", "match_type",
    "identity_credit", "target_rank_in_top5", "top1_identity", "top5_identity",
    "identity_quality", "target_post_mean", "target_max_A", "target_peak_in_span",
    "target_p50_span", "target_p90_span", "top1_phone", "top1_post",
    "competitor_phone", "competitor_post", "competitor_mean_span", "competitor_max_span",
    "competitor_peak", "identity_margin_mean", "identity_ratio_mean", "peak_margin",
    "competitor_wins_peak", "competitor_dominates_frac",
    "blank_mean_span", "blank_max_span", "blank_at_peak",
    "blank_ge05_frac", "blank_ge08_frac", "blank_ge09_frac",
    "support_count_05", "support_count_10", "longest_run_05", "longest_run_10",
    "support_runs_05", "support_concentration_05", "peak_pos_rel",
    "peak_dist_left", "peak_dist_right", "support_before_peak", "support_after_peak",
    "peak_D", "max_D", "cluster_width_D", "temporal_support_D", "prom_comp_D",
    "identity_windows_present", "decision_windows_present", "support_std_windows",
    "identity_class", "neighbor_share_full",
]


def num(v, d=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return d


def contiguous_runs(flags):
    runs, cur = [], 0
    for f in flags:
        if f:
            cur += 1
        elif cur:
            runs.append(cur)
            cur = 0
    if cur:
        runs.append(cur)
    return runs


def main():
    span = {}
    for c in CORPORA:
        for r in csv.DictReader(open(P22 / "artifacts" / f"spanmean_decision_{c}.csv",
                                     encoding="utf-8")):
            span[r["token_id"]] = r
    ev = {}
    for c in CORPORA:
        for r in csv.DictReader(open(P21 / f"token_evidence_{c}.csv", encoding="utf-8")):
            ev[(r["token_id"], r["window_type"])] = r
    win_id = {}
    for r in csv.DictReader(open(P22 / "WINDOW_IDENTITY_ANALYSIS.csv", encoding="utf-8")):
        win_id[r["token_id"]] = r

    frames = defaultdict(list)
    for c in CORPORA:
        for r in csv.DictReader(open(P21 / f"frames_{c}.csv", encoding="utf-8")):
            if r["window_type"] == PRIMARY:
                frames[r["token_id"]].append(r)

    rows = []
    for tid, s in sorted(span.items()):
        fr = sorted(frames.get(tid, []), key=lambda x: int(x["frame"]))
        span_frames = [f for f in fr if f["in_current_span"] == "1"]
        s0, s1 = int(s["span_start"]), int(s["span_end"])
        e = ev[(tid, PRIMARY)]
        rank = int(s["target_rank_in_top5"])
        ident = int(s["identity_credit"])
        ident_q = ("IDENTITY_RANK1" if ident and rank == 0
                   else "IDENTITY_RANK2_5" if ident
                   else "IDENTITY_NOT_TOP5")
        p_span = [num(f["target_posterior"]) for f in span_frames]
        comp_span = [num(f["best_competitor_posterior"]) for f in span_frames]
        blank_span = [num(f["blank_posterior"]) for f in span_frames]
        n = max(1, len(p_span))
        pk = int(s["peak_frame"])
        pk_local = pk - s0
        support_flags_05 = [p >= 0.05 for p in p_span]
        support_flags_10 = [p >= 0.10 for p in p_span]
        runs05 = contiguous_runs(support_flags_05)
        runs10 = contiguous_runs(support_flags_10)
        total05 = sum(p_span)
        longest_mass = 0.0
        if runs05:
            # locate the longest run and its target-posterior mass
            best_len, best_start, cur, cur_start = 0, 0, 0, 0
            for i, f in enumerate(support_flags_05):
                if f:
                    if cur == 0:
                        cur_start = i
                    cur += 1
                    if cur > best_len:
                        best_len, best_start = cur, cur_start
                else:
                    cur = 0
            longest_mass = sum(p_span[best_start:best_start + best_len])
        # window stability
        id_win = sum(1 for w in WINDOWS
                     if ev.get((tid, w)) and ev[(tid, w)]["baseline_best_obs"] == s["target_phone"])
        dec_win = sum(1 for w in WINDOWS
                      if ev.get((tid, w)) and int(ev[(tid, w)]["baseline_present"]) == 1)
        maxd = [num(ev[(tid, w)]["max_D"]) for w in WINDOWS if (tid, w) in ev]
        rows.append({
            "token_id": tid, "corpus": s["corpus"], "speaker_id": s["speaker_id"],
            "word": s["word"], "target_phone": s["target_phone"],
            "is_final_consonant": int(s["target_phone"] not in L.VOWELS),
            "human_label": s["human_label"], "truth": s["human_present"],
            "human_verdict": e.get("human_verdict", ""),
            "production_decision": int(s["production_decision"]), "match_type": s["match_type"],
            "identity_credit": ident, "target_rank_in_top5": rank,
            "top1_identity": int(rank == 0), "top5_identity": int(rank >= 0),
            "identity_quality": ident_q,
            "target_post_mean": round(num(s["target_post_mean"]), 6),
            "target_max_A": round(num(s["target_max_A"]), 6),
            "target_peak_in_span": round(num(s["peak_post"]), 6),
            "target_p50_span": round(st.median(p_span), 6) if p_span else 0.0,
            "target_p90_span": round(sorted(p_span)[int(0.9 * (len(p_span) - 1))], 6)
            if p_span else 0.0,
            "top1_phone": s["top1_phone"], "top1_post": round(num(s["top1_post"]), 6),
            "competitor_phone": s["competitor_phone"],
            "competitor_post": round(num(s["competitor_post"]), 6),
            "competitor_mean_span": round(sum(comp_span) / n, 6),
            "competitor_max_span": round(max(comp_span), 6) if comp_span else 0.0,
            "competitor_peak": round(num(s["peak_competitor"]), 6),
            "identity_margin_mean": round(num(s["identity_margin_mean"]), 6),
            "identity_ratio_mean": round(num(s["identity_ratio_mean"]), 4),
            "peak_margin": round(num(s["peak_margin"]), 6),
            "competitor_wins_peak": int(num(s["peak_margin"]) < 0),
            "competitor_dominates_frac": round(
                sum(1 for t, c in zip(p_span, comp_span) if c > t) / n, 4),
            "blank_mean_span": round(num(s["blank_mean_span"]), 6),
            "blank_max_span": round(max(blank_span), 6) if blank_span else 0.0,
            "blank_at_peak": round(num(s["peak_blank"]), 6),
            "blank_ge05_frac": round(sum(1 for b in blank_span if b >= 0.5) / n, 4),
            "blank_ge08_frac": round(sum(1 for b in blank_span if b >= 0.8) / n, 4),
            "blank_ge09_frac": round(sum(1 for b in blank_span if b >= 0.9) / n, 4),
            "support_count_05": sum(support_flags_05),
            "support_count_10": sum(support_flags_10),
            "longest_run_05": max(runs05) if runs05 else 0,
            "longest_run_10": max(runs10) if runs10 else 0,
            "support_runs_05": len(runs05),
            "support_concentration_05": round(longest_mass / total05, 4) if total05 else 0.0,
            "peak_pos_rel": round(pk_local / max(1, s1 - s0), 4),
            "peak_dist_left": pk - s0, "peak_dist_right": s1 - pk,
            "support_before_peak": round(sum(p_span[:max(0, pk_local)]), 6),
            "support_after_peak": round(sum(p_span[pk_local + 1:]), 6),
            "peak_D": round(num(e["peak"]), 6), "max_D": round(num(e["max_D"]), 6),
            "cluster_width_D": int(num(e["cluster_width"])),
            "temporal_support_D": round(num(e["temporal_support"]), 6),
            "prom_comp_D": round(num(e["prom_comp"]), 6),
            "identity_windows_present": id_win, "decision_windows_present": dec_win,
            "support_std_windows": round(st.stdev(maxd), 6) if len(maxd) > 1 else 0.0,
            "identity_class": win_id.get(tid, {}).get("classification", ""),
            "neighbor_share_full": win_id.get(tid, {}).get("neighbor_share_full", ""),
        })
    L.write_rows(ART / "feature_matrix.csv", rows, FIELDS)
    print("rows", len(rows), "->", ART / "feature_matrix.csv")
    # quick distributions for dev (rule grid derivation)
    dev = [r for r in rows if r["corpus"] in ("so762_dev", "so762_absent_dev")
           and r["truth"] != ""]
    print("dev tokens", len(dev),
          "present", sum(1 for r in dev if r["truth"] == "1"),
          "absent", sum(1 for r in dev if r["truth"] == "0"))
    for f in ("target_max_A", "target_post_mean", "identity_margin_mean", "blank_mean_span",
              "blank_ge09_frac", "longest_run_05", "peak_D"):
        vals = sorted(num(r[f]) for r in dev)
        q = lambda p: vals[int(p * (len(vals) - 1))]
        print(f"  dev {f:22s} q10={q(0.10):.5f} q25={q(0.25):.5f} q50={q(0.50):.5f} "
              f"q75={q(0.75):.5f} q90={q(0.90):.5f}")


if __name__ == "__main__":
    main()
