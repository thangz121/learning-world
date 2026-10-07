"""Inspect WP-1.9.23 rule-search outputs (research helper)."""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_23"


def main():
    which = sys.argv[1] if len(sys.argv) > 1 else "frontier"
    if which == "frontier":
        rows = list(csv.DictReader(open(OUT / "ACCEPTANCE_PARETO_FRONTIER.csv",
                                        encoding="utf-8")))
        front = [r for r in rows if r["pareto_optimal_dev"] == "1"]
        front.sort(key=lambda r: -float(r["dev_recall"]))
        print("PARETO-OPTIMAL ON DEV (recall desc):")
        for r in front:
            print(f"  {r['rule_id']:34s} dev r={r['dev_recall']:>7s} far={r['dev_far']:>7s} "
                  f"unc={r['dev_uncertain']:>7s} | lwe r={r['lwe_recall']:>7s} "
                  f"far={r['lwe_far']:>7s} | test r={r['test_recall']:>7s}")
    elif which == "families":
        rows = list(csv.DictReader(open(OUT / "ACCEPTANCE_RULE_VARIANTS.csv",
                                        encoding="utf-8")))
        by = {}
        for r in rows:
            if r["dataset"] == "dev":
                by.setdefault(r["family"], []).append(r)
        for fam, rs in by.items():
            rs.sort(key=lambda r: (float(r["far"]) if r["far"] else 1,
                                   -(float(r["present_recall"]) if r["present_recall"] else 0)))
            print(f"== {fam} (top 4 by dev FAR) ==")
            for r in rs[:4]:
                print(f"  {r['rule_id']:34s} dev r={r['present_recall']:>7s} far={r['far']:>7s} "
                      f"unc={r['uncertain_rate']:>7s} changed={r['decisions_changed_from_production']:>3s} "
                      f"unsup={r['unsupported_present']:>3s} strongFP={r['strong_encoder_false_present']:>2s}")
    elif which == "best_lwe":
        rows = list(csv.DictReader(open(OUT / "ACCEPTANCE_RULE_VARIANTS.csv",
                                        encoding="utf-8")))
        best = {}
        for r in rows:
            if r["dataset"] == "dev" and r["present_recall"]:
                best[r["rule_id"]] = float(r["present_recall"])
        # rules with dev recall >= 0.85 sorted by LWE FAR
        lwe = [r for r in rows if r["dataset"] == "lwe"
               and best.get(r["rule_id"], 0) >= 0.85]
        lwe.sort(key=lambda r: (float(r["far"]) if r["far"] else 1,
                                -float(r["present_recall"])))
        print("LWE points with dev recall >= 0.85 (top 12 by LWE FAR):")
        for r in lwe[:12]:
            print(f"  {r['rule_id']:34s} devR={best[r['rule_id']]:.4f} "
                  f"lwe r={r['present_recall']:>7s} far={r['far']:>7s} "
                  f"unsup={r['unsupported_present']:>3s} strongFP={r['strong_encoder_false_present']:>2s}")
    elif which == "false":
        rows = list(csv.DictReader(open(OUT / "FALSE_ACCEPT_RECLASSIFICATION.csv",
                                        encoding="utf-8")))
        for r in rows:
            print(f"{r['token_id']:18s} {r['failure_type']:18s} {r['acceptance_subtype']:18s} "
                  f"rank={r['identity_rank']:>2s} maxA={r['target_max_A']:>8s} "
                  f"blank={r['blank_mean_span']:>8s} margin={r['identity_margin_mean']:>9s} "
                  f"bestRemoved={r['removed_by_best_rule']} rank1={r['removed_by_rank1_only']} "
                  f"mar0={r['removed_by_margin0']} blank90={r['removed_by_blank90']}")
    elif which == "cases":
        d = json.load(open(OUT / "EXPERIMENT_RESULTS.json", encoding="utf-8"))
        for r in d["known_cases"]:
            print(f"{r['token_id']:16s} {r['target_phone']:3s} truth={r['truth']} "
                  f"prod={r['production_decision']} final={r['final_candidate_decision']:8s} "
                  f"changed={r['decision_changed']} rank={str(r['identity_rank']):>2s} "
                  f"maxA={str(r['target_max']):>8s} margin={str(r['identity_margin']):>9s} "
                  f"blank={str(r['blank_mean']):>8s} fail={r['failure_type']}")
    elif which == "fals":
        d = json.load(open(OUT / "EXPERIMENT_RESULTS.json", encoding="utf-8"))
        print(json.dumps(d["falsification"], indent=1))
    elif which == "rule":
        wanted = sys.argv[2].split(",") if len(sys.argv) > 2 else [
            "A1_rank1_only", "B_rank_aware_t0.05_0.2_0.5", "C1_identity_margin_mean0.0",
            "D2_identity_blank_occ1.0", "E_id_sup0.05_mar0.0",
            "F_main_sup0.1_mar0.01_blank0.95", "G1_identity_run2", "H1_class_adaptive"]
        rows = list(csv.DictReader(open(OUT / "ACCEPTANCE_RULE_VARIANTS.csv",
                                        encoding="utf-8")))
        for rid in wanted:
            print(f"== {rid} ==")
            for r in rows:
                if r["rule_id"] != rid or r["dataset"] not in (
                        "dev", "lwe", "test", "lwe_absent", "dev_absent"):
                    continue
                print(f"  {r['dataset']:10s} n={r['n']:>4s} r={str(r['present_recall']):>7s} "
                      f"far={str(r['far']):>7s} ad={str(r['absent_detection']):>7s} "
                      f"unc={str(r['uncertain_rate']):>7s} chg={r['decisions_changed_from_production']:>4s} "
                      f"unsup={r['unsupported_present']:>4s} confl={r['competitor_conflict_present']:>4s} "
                      f"blankdom={r['blank_dominated_present']:>4s} strongFP={r['strong_encoder_false_present']:>2s}")
    elif which == "subgroups":
        rows = list(csv.DictReader(open(OUT / "PHONE_SUBGROUP_ANALYSIS.csv", encoding="utf-8")))
        for r in rows:
            print(f"{r['group_type']:10s} {r['group']:16s} n={r['n']:>3s} P={r['present']:>3s} "
                  f"A={r['absent']:>3s} prodR={str(r['production_recall']):>7s} "
                  f"prodFAR={str(r['production_far']):>7s} bestR={str(r['best_rule_recall']):>7s} "
                  f"bestFAR={str(r['best_rule_far']):>7s} bestU={str(r['best_rule_uncertain_rate']):>7s}")


if __name__ == "__main__":
    main()
