"""WP-1.9.26 Part 20 — controlled encoder comparison (production xlsr vs lv-60).

Reads the WP-1.9.23 feature matrix (production evidence) and the WP-1.9.26
alt_encoder_all.csv (second local espeak encoder). Computes missing-evidence and
false-evidence rates, /r/ and final-consonant behaviour, isolated peaks and
speaker variation. No training, no downloads.
"""
from __future__ import annotations

import csv
import json
import statistics as st
import sys
from collections import Counter, defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_26"
ART = OUT / "artifacts"
P23 = L.REPO / "Research/Speech/Phase1_9_23"

FIELDS = ["metric", "production_xlsr53", "alt_lv60", "notes"]


def num(v, d=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return d


def main():
    feat = list(csv.DictReader(open(P23 / "artifacts/feature_matrix.csv", encoding="utf-8")))
    alt = {r["token_id"]: r for r in csv.DictReader(
        open(ART / "alt_encoder_all.csv", encoding="utf-8"))}
    rows = []
    for f in feat:
        a = alt.get(f["token_id"])
        if a is None:
            continue
        rows.append({"f": f, "a": a, "pm": num(f["target_max_A"]),
                     "am": num(a["alt_max_A"]), "truth": f["truth"],
                     "ph": f["target_phone"], "spk": f["speaker_id"]})

    def rate(rs, cond_p, cond_a):
        return round(sum(1 for r in rs if cond_p(r) and cond_a(r)) / len(rs), 4) if rs else None

    def metrics(rs, tag):
        present = [r for r in rs if r["truth"] == "1"]
        absent = [r for r in rs if r["truth"] == "0"]
        return {
            "n": len(rs), "present": len(present), "absent": len(absent),
            "prod_missing_evidence_present_rate": round(
                sum(1 for r in present if r["pm"] < 0.02) / len(present), 4)
            if present else None,
            "alt_missing_evidence_present_rate": round(
                sum(1 for r in present if r["am"] < 0.02) / len(present), 4)
            if present else None,
            "prod_false_evidence_absent_rate": round(
                sum(1 for r in absent if r["pm"] >= 0.30) / len(absent), 4)
            if absent else None,
            "alt_false_evidence_absent_rate": round(
                sum(1 for r in absent if r["am"] >= 0.30) / len(absent), 4)
            if absent else None,
            "prod_median_max_A_present": round(st.median([r["pm"] for r in present]), 5)
            if present else None,
            "alt_median_max_A_present": round(st.median([r["am"] for r in present]), 5)
            if present else None,
        }

    out_rows = []
    overall = metrics(rows, "all")
    r_rows = [r for r in rows if r["ph"] == "ɹ"]
    fc = [r for r in rows if r["f"]["is_final_consonant"] == "1"]
    for name, rs in (("all_tokens", rows), ("final_consonants", fc), ("r_phoneme", r_rows)):
        m = metrics(rs, name)
        for k, v in m.items():
            if k in ("n", "present", "absent"):
                continue
            out_rows.append({"metric": f"{name}.{k}",
                             "production_xlsr53": v, "alt_lv60": None, "notes": ""})
    # pairwise agreement
    agree = sum(1 for r in rows if (r["pm"] >= 0.10) == (r["am"] >= 0.10)) / len(rows)
    gained = sum(1 for r in rows if r["am"] >= 0.10 > r["pm"])
    lost = sum(1 for r in rows if r["pm"] >= 0.10 > r["am"])
    spk_var_prod = {}
    spk_var_alt = {}
    for r in rows:
        spk_var_prod.setdefault(r["spk"], []).append(r["pm"])
        spk_var_alt.setdefault(r["spk"], []).append(r["am"])
    out = {
        "overall": overall,
        "agreement_at_0.10": round(agree, 4),
        "alt_gains_evidence": gained, "alt_loses_evidence": lost,
        "speakers": len(spk_var_prod),
        "prod_speaker_median_spread": round(st.median([
            max(v) - min(v) for v in spk_var_prod.values() if len(v) > 1]), 5)
        if any(len(v) > 1 for v in spk_var_prod.values()) else None,
        "alt_speaker_median_spread": round(st.median([
            max(v) - min(v) for v in spk_var_alt.values() if len(v) > 1]), 5)
        if any(len(v) > 1 for v in spk_var_alt.values()) else None,
        "isolated_peak_present_prod": round(sum(
            1 for r in rows if r["truth"] == "1" and int(num(r["f"]["cluster_width_D"], 1)) <= 1)
            / max(1, sum(1 for r in rows if r["truth"] == "1")), 4),
    }
    (ART / "encoder_comparison.json").write_text(json.dumps(out, indent=2), encoding="utf-8")
    L.write_rows(OUT / "09_B2_DESIGN" / "ENCODER_COMPARISON.csv", out_rows, FIELDS)
    print(json.dumps(out, indent=1))
    print("DONE encoder_comparison")


if __name__ == "__main__":
    main()
