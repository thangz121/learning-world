"""Inspect WP-1.9.22 outputs (research helper)."""
from __future__ import annotations

import csv
import json
import sys
from collections import Counter
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_22"
ART = OUT / "artifacts"


def main():
    which = sys.argv[1] if len(sys.argv) > 1 else "decomp"
    if which == "decomp":
        rows = list(csv.DictReader(open(OUT / "FALSE_ACCEPTANCE_DECOMPOSITION.csv",
                                        encoding="utf-8")))
        for r in rows:
            print(f"{r['token_id']:18s} {r['target_phone']:3s} mech={r['primary_mechanism']:18s} "
                  f"rank={r['target_rank_in_top5']:>2s} post={r['target_post_mean']:>9s} "
                  f"maxA={r['target_max_A']:>8s} peak={r['peak_D']:>8s} "
                  f"blank={r['blank_mean_span']:>8s} margin={r['identity_margin_mean']:>9s} "
                  f"flags(c{int(r['flag_competitor_conflict'])},b{int(r['flag_blank_dominated'])},"
                  f"w{int(r['flag_window_dependent'])})")
    elif which == "variants":
        rows = list(csv.DictReader(open(OUT / "ACCEPTANCE_VARIANT_RESULTS.csv",
                                        encoding="utf-8")))
        for r in rows:
            print(f"{r['variant']:32s} {r['params']:24s} {r['dataset']:10s} "
                  f"n={r['n']:>4s} r={str(r['recall']):>7s} frr={str(r['frr']):>7s} "
                  f"far={str(r['far']):>7s} ad={str(r['absent_detection']):>7s} "
                  f"unsup={r['unsupported_accepts']:>4s} chg={r['changed_from_production']:>4s} "
                  f"rejT={r['rejects_true_present']:>4s} newF={r['new_false_accepts']:>4s}")
    elif which == "present":
        rows = list(csv.DictReader(open(OUT / "ACCEPTANCE_VARIANT_RESULTS.csv",
                                        encoding="utf-8")))
        for r in rows:
            if r["dataset"] in ("lwe", "so762_dev", "so762_test"):
                print(f"{r['variant']:32s} {r['params']:24s} {r['dataset']:10s} "
                      f"r={r['recall']:>7s} far={r['far']:>7s} unsup={r['unsupported_accepts']:>3s} "
                      f"changed={r['changed_from_production']:>3s} "
                      f"rejTrue={r['rejects_true_present']:>3s} newFalse={r['new_false_accepts']}")
    elif which == "focus":
        rows = list(csv.DictReader(open(OUT / "ACCEPTANCE_VARIANT_RESULTS.csv",
                                        encoding="utf-8")))
        # focus cases from EXPERIMENT_RESULTS
        d = json.load(open(OUT / "EXPERIMENT_RESULTS.json", encoding="utf-8"))
        for r in d["experiment4_present_cost"]["rows"]:
            if r["focus_case"] == 1 or r["truth"] == 0:
                print(f"{r['token_id']:16s} {r['target_phone']:3s} truth={r['truth']} "
                      f"prod={r['production_decision']} {r['match_type']:5s} "
                      f"idc={r['identity_credit']} rank={str(r['target_rank_in_top5']):>2s} "
                      f"post={str(r['target_post_mean']):>9s} maxA={str(r['target_max_A']):>8s} "
                      f"rawsim={str(r['raw_best_sim']):>5s} B={r['B_no_identity']} "
                      f"D0.1={r['D_identity_support_0.10']} D0.3={r['D_identity_support_0.30']} "
                      f"cost={r['cost']}")
    elif which == "window":
        rows = list(csv.DictReader(open(OUT / "WINDOW_IDENTITY_ANALYSIS.csv", encoding="utf-8")))
        print(Counter(r["classification"] for r in rows))
        for r in rows:
            if r["classification"] not in ("IDENTITY_STABLE_PRESENT", "IDENTITY_STABLE_ABSENT"):
                print(f"{r['token_id']:18s} {r['target_phone']:3s} truth={r['truth']} "
                      f"prod={r['production_decision']} id(f/r/p1/p2)="
                      f"{r['identity_full']}/{r['identity_raw']}/{r['identity_pad100']}/"
                      f"{r['identity_pad250']} -> {r['classification']} "
                      f"nbr_full={r['neighbor_share_full']} prev={r['prev_phone']} next={r['next_phone']}")
    elif which == "census":
        # identity vs similarity acceptance census across all corpora
        span = {}
        for c in ("lwe", "so762_dev", "so762_test", "so762_absent_dev"):
            for r in csv.DictReader(open(ART / f"spanmean_decision_{c}.csv", encoding="utf-8")):
                span[r["token_id"]] = r
        n = Counter()
        for tid, r in span.items():
            if int(r["production_decision"]) == 0:
                continue
            if int(r["identity_credit"]) == 1:
                n["identity"] += 1
                n["identity_rank0" if int(r["target_rank_in_top5"]) == 0 else
                  "identity_rank_gt0"] += 1
            else:
                n["similarity"] += 1
            if float(r["target_post_mean"]) >= 0.25:
                n["posterior_path"] += 1
        print("acceptance census (all 609 primary tokens):", dict(n))
        print("total accepted:", sum(1 for r in span.values()
                                     if int(r["production_decision"]) == 1), "/", len(span))


if __name__ == "__main__":
    main()
