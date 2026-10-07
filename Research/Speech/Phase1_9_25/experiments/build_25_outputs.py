"""WP-1.9.25 Phase 3/4 — TYPE-B reassessment + /r/ summary from existing artifacts.

No encoder runs; reads WP-1.9.24 ENCODER_TYPE_B_CASES.csv, the alt-encoder CSV and
WP-1.9.25 R_CASES.csv.
"""
from __future__ import annotations

import csv
import json
import statistics as st
import sys
from collections import Counter
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_25"
ART = OUT / "artifacts"
P24 = L.REPO / "Research/Speech/Phase1_9_24"

TYPEB_FIELDS = [
    "case_id", "corpus", "target_word", "target_phone", "human_label_status",
    "human_confidence", "label_source", "production_max_A", "mean_A", "peak_frame",
    "peak_time_ms", "peak_width_frames", "longest_run_05", "temporal_location_rel",
    "forced_alignment_span", "competitor_phone", "peak_margin", "blank_at_peak",
    "isolated_peak", "plausible_phonetic_event", "alt_encoder_max_A", "alt_preserves",
    "label_limited", "representation_limited", "acceptance_logic_limited",
    "final_classification", "notes",
]


def num(v, d=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return d


def main():
    typeb = list(csv.DictReader(open(P24 / "ENCODER_TYPE_B_CASES.csv", encoding="utf-8")))
    alt = {r["token_id"]: num(r["alt_max_A"]) for r in csv.DictReader(
        open(P24 / "artifacts/alt_encoder_counterfactual.csv", encoding="utf-8"))}

    rows = []
    for r in typeb:
        tid = r["case_id"]
        isolated = int(r["fals_C_neighbor_support"]) == 0
        alt_v = alt.get(tid, "")
        alt_pres = int(alt_v != "" and float(alt_v) >= 0.30)
        label_ok = int(r["fals_G_confident_label"]) == 1
        label_limited = 0 if label_ok else 1
        rep_limited = int(alt_v != "" and float(alt_v) < float(r["target_max_A"]) * 0.6)
        if tid == "child_07_seven":
            cls = "MIXED"
            note = ("confident PROBABLY_ABSENT label, but evidence is an isolated one-frame "
                    "spike and the alternative encoder strengthens it (0.63->0.94): label vs "
                    "temporal/representation ambiguity")
        elif tid == "014180143_15":
            cls = "LABEL_LIMITED"
            note = ("score-0 only (not a listening label); alt encoder drops 0.68->0.25; "
                    "neighbor support present (0.27)")
        elif tid == "014190172_7":
            cls = "MIXED"
            note = ("score-0 only + isolated one-frame spike + alt drops 0.82->0.41")
        elif tid == "014350146_16":
            cls = "LABEL_LIMITED"
            note = ("score-0 only; temporal support present (neighbors 0.23/0.25); alt keeps "
                    "0.96->0.91")
        else:
            cls = "INCONCLUSIVE"
            note = ""
        rows.append({
            "case_id": tid, "corpus": r["corpus"], "target_word": r["word"],
            "target_phone": r["target_phone"], "human_label_status": r["human_label"],
            "human_confidence": r["human_confidence"], "label_source": r["human_label_source"],
            "production_max_A": r["target_max_A"], "mean_A": r["target_mean"],
            "peak_frame": r["peak_frame"], "peak_time_ms": r["peak_time_ms"],
            "peak_width_frames": r["cluster_width_D"],
            "longest_run_05": r["longest_run_05"],
            "temporal_location_rel": r["peak_rel_pos"],
            "forced_alignment_span": f"{r['span_start']}-{r['span_end']}",
            "competitor_phone": r["competitor_phone"],
            "peak_margin": r["peak_margin"], "blank_at_peak": r["blank_at_peak"],
            "isolated_peak": int(isolated),
            "plausible_phonetic_event": int(not isolated or float(r["blank_at_peak"]) < 0.5),
            "alt_encoder_max_A": alt_v, "alt_preserves": alt_pres,
            "label_limited": label_limited, "representation_limited": rep_limited,
            "acceptance_logic_limited": 0,
            "final_classification": cls, "notes": note,
        })
    L.write_rows(OUT / "TYPE_B_CASES.csv", rows, TYPEB_FIELDS)

    # ---- /r/ summary ----
    r_cases = list(csv.DictReader(open(OUT / "R_CASES.csv", encoding="utf-8")))
    lwe = [r for r in r_cases if r["corpus"] == "lwe"]
    so = [r for r in r_cases if r["corpus"] != "lwe"]
    def med(rs, field):
        vals = sorted(num(r[field]) for r in rs)
        return round(vals[len(vals) // 2], 5) if vals else None
    r_summary = {
        "n_total": len(r_cases),
        "n_lwe": len(lwe), "n_so762": len(so),
        "lwe_speakers": len({r["speaker_id"] for r in lwe}),
        "so762_speakers": len({r["speaker_id"] for r in so}),
        "so762_ages": dict(Counter(r["age"] for r in so)),
        "label_status": {
            "lwe_present_listening": sum(1 for r in lwe if "PRESENT" in r["human_label_or_score"]),
            "lwe_absent_listening": sum(1 for r in lwe if "ABSENT" in r["human_label_or_score"]),
            "lwe_unlabelled": sum(1 for r in lwe if r["human_label_or_score"] == "(unlabelled)"),
            "lwe_confidence": dict(Counter(r["human_confidence"] for r in lwe)),
            "so762_score_2": sum(1 for r in so if r["human_label_or_score"] == "score=2.0"),
            "so762_score_1": sum(1 for r in so if r["human_label_or_score"] == "score=1.0"),
            "so762_score_0": sum(1 for r in so if r["human_label_or_score"] == "score=0.0"),
        },
        "max_A_median_lwe": med(lwe, "max_A"), "max_A_median_so762": med(so, "max_A"),
        "max_A_ge_0.3_lwe": sum(1 for r in lwe if num(r["max_A"]) >= 0.30),
        "max_A_lt_0.1_lwe": sum(1 for r in lwe if num(r["max_A"]) < 0.10),
        "isolated_peak_lwe": sum(1 for r in lwe if int(num(r["cluster_width_D"], 1)) <= 1),
        "isolated_peak_so762": sum(1 for r in so if int(num(r["cluster_width_D"], 1)) <= 1),
        "production_accepted_lwe": sum(1 for r in lwe
                                       if int(num(r["production_decision"])) == 1),
        "alt_covered_lwe": sum(1 for r in lwe if r["alt_encoder_max_A"] != ""),
        "alt_median_lwe": (round(st.median([num(r["alt_encoder_max_A"]) for r in lwe
                                            if r["alt_encoder_max_A"] != ""]), 5)
                           if any(r["alt_encoder_max_A"] != "" for r in lwe) else None),
        "window_sensitive": sum(1 for r in r_cases
                                if r["identity_class"] not in
                                ("IDENTITY_STABLE_PRESENT", "IDENTITY_STABLE_ABSENT")),
    }
    (ART / "r_summary.json").write_text(json.dumps(r_summary, indent=2), encoding="utf-8")
    (ART / "typeb_summary.json").write_text(json.dumps(
        {"classifications": dict(Counter(r["final_classification"] for r in rows)),
         "isolated_peak": sum(r["isolated_peak"] for r in rows),
         "label_limited": sum(r["label_limited"] for r in rows),
         "alt_preserves": sum(r["alt_preserves"] for r in rows)}, indent=2), encoding="utf-8")

    print("TYPE-B classifications:", dict(Counter(r["final_classification"] for r in rows)))
    print("R summary:", json.dumps(r_summary, indent=1))
    print("DONE build_25_outputs")


if __name__ == "__main__":
    main()
