"""Print every number needed for the WP-1.9.21 report (research helper)."""
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
    print("== datasets ==")
    print(json.dumps(d["datasets"], indent=1))
    print("\n== current_mean ==")
    for k in ("dev", "lwe_external", "test_speaker_disjoint"):
        m = d["current_mean"][k]
        print(f"{k:24s} recall={m['present_recall']} far={m['far']} "
              f"absent_det={m['absent_detection']} unsupported={m['unsupported_present']} "
              f"false_support={m['false_support']}")
    print("\n== selected ==")
    print(d["dev_selection"]["selected_family"], d["dev_selection"]["selected_setting"]["params"])
    print("\n== variants (dev / lwe / test) ==")
    for f, v in d["variants"].items():
        dev, lwe, test = v["dev"], v["lwe_external"], v["test_speaker_disjoint"]
        print(f"{f:22s} dev r={dev['present_recall']} ad={dev['absent_detection']} | "
              f"lwe r={lwe['present_recall']} far={lwe['far']} unc={lwe['uncertain_rate']} "
              f"unsup={lwe['unsupported_present']} fs={lwe['false_support']} | "
              f"test r={test['present_recall']} unsup={test['unsupported_present']}")
    print("\n== matched recall MAX ==")
    print(json.dumps(d["matched_recall_max"], indent=1)[:1200])
    print("\n== mean_A control ==")
    for k, v in d["mean_a_control"].items():
        m = v["lwe_external"]
        print(f"{k:8s} lwe recall={m['present_recall']} far={m['far']}")
    print("\n== sparse peak ==")
    print(json.dumps(d["sparse_peak"], indent=1))
    print("\n== stability flips (best=MAX) ==")
    for tag in ("lwe_external", "so762_test", "so762_dev"):
        s = d["window_stability"][tag]["MAX"]
        print(f"{tag:14s} n={s['n']} stableP={s.get('stable_present')} "
              f"stableA={s.get('stable_absent')} sensitive={s.get('window_sensitive')} "
              f"flips={s['flips']} rate={s['flip_rate']}")
    print("\n== known cases ==")
    print("fixed", d["known_cases"]["fixed"], "regressed", d["known_cases"]["regressed"])
    for c in d["known_cases"]["rows"]:
        print(f"  {c['case_id']:16s} human={c['human_label'][:30]:30s} "
              f"cur={c['current_decision']:7s} res={c['research_decision']:7s} "
              f"peak={c['max_peak']} class={c['support_class']:22s} "
              f"wrong_occ={c['wrong_occurrence']} stab={c['window_stability']}")
    print("\n== label audit ==")
    print(json.dumps(d["label_audit"], indent=1))
    print("\n== negative control unmasked ==")
    uc = d["negative_controls"]["unmasked_control"]
    for k, v in uc.items():
        print(f"  {k}: lwe labeled r={v['lwe_labeled']['present_recall']} "
              f"far={v['lwe_labeled']['far']} wrong_occ_cases={v['wrong_occurrence_cases']}")
    print("\n== so762 absent-enriched control (selected variants) ==")
    for f in ("MAX", "TOP3", "TEMPORAL_SUPPORT", "LOCAL_CLUSTER"):
        m = d["negative_controls"]["so762_absent_enriched"][f]
        print(f"  {f:20s} absent_det={m['absent_detection']} far={m['far']} "
              f"false_support={m['false_support']}")


if __name__ == "__main__":
    main()
