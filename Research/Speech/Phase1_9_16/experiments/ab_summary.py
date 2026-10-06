"""Phase 1.9.16 — consolidate A/B metrics from saved CSVs (no model runs).

Reads lwe_ab_tokens.csv, siak_test_ab.csv, siak_46_ab.csv, so762 adapted/baseline
phone hits and writes artifacts/ab/ab_metrics.json with the comparison table.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
REPO = HERE.parents[3]
AB = REPO / "Research/Speech/Phase1_9_16/artifacts/ab"
ZS = REPO / "Research/Speech/Phase1_9_16/artifacts/zeroshot"

import eval_ab as EA  # noqa: E402


def rows(p):
    return list(csv.DictReader(p.open(encoding="utf-8")))


def lwe_metrics():
    rs = rows(AB / "lwe_ab_tokens.csv")
    lab = [r for r in rs if r["human_class"]]
    fcr = [r for r in rs if r["fc_human_label"]]
    out = {"n_labeled": len(lab)}
    for tag, f in (("baseline", "baseline_soft"), ("adapted", "adapted_soft")):
        cor = [float(r[f]) for r in lab if r["human_class"] == "HUMAN_CORRECT"]
        inc = [float(r[f]) for r in lab if r["human_class"] == "HUMAN_INCORRECT"]
        out[tag] = {"frr_lt50": float(np.mean([v < 50 for v in cor])),
                    "far_ge50": float(np.mean([v >= 50 for v in inc])),
                    "auc": EA.auc_bin(cor, inc)}
    for tag, f in (("baseline", "baseline_final_match"), ("adapted", "adapted_final_match")):
        pres = [r for r in fcr if "PRESENT" in r["fc_human_label"]]
        abs_ = [r for r in fcr if "ABSENT" in r["fc_human_label"]]
        out[f"{tag}_final"] = {
            "present_recall": float(np.mean([r[f] in ("exact", "soft") for r in pres])),
            "absent_detection": float(np.mean([r[f] == "miss" for r in abs_])),
            "n_present": len(pres), "n_absent": len(abs_)}
    out["fc_frr_baseline"] = 1 - out["baseline_final"]["present_recall"]
    out["fc_frr_adapted"] = 1 - out["adapted_final"]["present_recall"]
    return out


def siak_metrics(fname):
    rs = rows(AB / fname)
    sc = [float(r["siak_score"]) for r in rs]
    out = {"n": len(rs)}
    for tag, f in (("baseline", "baseline_soft"), ("adapted", "adapted_soft")):
        v = [float(r[f]) for r in rs]
        good = [r for r in rs if float(r["siak_score"]) >= 80]
        unc = [r for r in rs if float(r["siak_score"]) < 50]
        out[tag] = {"pearson": EA.pearson(sc, v), "spearman": EA.spearman(sc, v),
                    "mae": float(np.mean([abs(a - b) for a, b in zip(v, sc)])),
                    "frr_proxy_lt50": float(np.mean([float(r[f]) < 50 for r in good])),
                    "far_proxy_ge80": float(np.mean([float(r[f]) >= 80 for r in unc]))}
    return out


def main():
    metrics = {
        "phase": "1.9.16",
        "pipeline": "SAME AUDIO -> {frozen baseline | adapted head} -> SAME "
                    "PhoneEvidenceV2.soft_match (ctc_forced_v1 + soft matching)",
        "lwe_external": lwe_metrics(),
        "siak_test": siak_metrics("siak_test_ab.csv"),
        "siak_46_external": siak_metrics("siak_46_ab.csv"),
        "so762_test_children": EA.compare_so762(AB),
        "runtime": {"so762_adapted_s_per_utt": 1.01, "siak_adapted_s_per_utt": 0.65,
                    "lwe_adapted_s_per_utt": 0.65,
                    "baseline_so762_s_per_utt": 0.75, "baseline_siak_s_per_utt": 0.32,
                    "baseline_lwe_s_per_utt": 0.29,
                    "note": "CPU-only, warm, per-utterance (measured, single run; "
                            "so762/train item lengths ~3.3 s median)"},
    }
    (AB / "ab_metrics.json").write_text(json.dumps(metrics, indent=2), encoding="utf-8")
    print(json.dumps({k: metrics[k] for k in ("lwe_external", "siak_test", "siak_46_external")},
                     indent=2)[:3500])


if __name__ == "__main__":
    main()
