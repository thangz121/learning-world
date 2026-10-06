"""WP-1.9.18 — export FAILURE_CASE_ANALYSIS.csv (E-based decisions + reason codes).

Decisions exported per LWE token:
  baseline (production), B = deletion-aware free presence,
  C_diag = diagnostic evidence floor E >= 0.30 (not tuned on LWE),
  D_diag = E >= 0.30 PRESENT / E < 0.10 & free-deleted & margin<=0 ABSENT / else UNCERTAIN.
Cleary labelled diagnostic because the pre-registered dev selection found no valid
recall>=0.95 point.
"""
from __future__ import annotations

import csv
from collections import Counter
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
OUT = REPO / "Research/Speech/Phase1_9_18"
ART = OUT / "artifacts"


def fnum(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return 0.0


def acoustic_evidence(e):
    if e >= 0.5:
        return "STRONG"
    if e >= 0.2:
        return "MODERATE"
    if e >= 0.05:
        return "WEAK"
    return "NONE"


def decide_B(r):
    return "PRESENT" if r["da_free_present"] == "1" else "ABSENT"


def decide_C(r):
    return "PRESENT" if fnum(r["da_span_max"]) >= 0.30 else "ABSENT"


def decide_D(r):
    e, m, free = fnum(r["da_span_max"]), fnum(r["da_margin_per_frame"]), r["da_free_present"]
    if e >= 0.30:
        return "PRESENT"
    if e < 0.10 and free == "0" and m <= 0:
        return "ABSENT"
    return "UNCERTAIN"


def reason(r, dec):
    e, m, free = fnum(r["da_span_max"]), fnum(r["da_margin_per_frame"]), r["da_free_present"]
    if dec == "PRESENT":
        return "PRESENT_SUPPORTED" if e >= 0.5 else "PRESENT_WEAK"
    if dec == "ABSENT":
        if free == "0" and m < 0:
            return "ABSENT_DELETION"
        return "ABSENT_SUPPORTED"
    if e < 0.10:
        return "UNCERTAIN_NO_EVIDENCE"
    if m < 0:
        return "UNCERTAIN_COMPETING_SPANS"
    return "UNCERTAIN_ALIGNMENT"


def root_cause(r):
    base_ok = int(r["baseline_present"])
    y = int(r["human_present"]) if r["human_present"] != "" else ""
    e = fnum(r["da_span_max"])
    if y == "":
        return "UNKNOWN"
    base_wrong = base_ok != y
    d_ok = (decide_D(r) == ("PRESENT" if y else "ABSENT"))
    if not base_wrong:
        return "OK_UNSUPPORTED" if (base_ok == 1 and e < 0.1) else "OK"
    if d_ok:
        return "RESOLVED_DELETION_AWARE"
    if y == 1 and e < 0.1:
        return "ACOUSTIC_LIMITATION"
    if y == 0 and e >= 0.3:
        return "MIXED"
    if int(r["window_inversion"]) if "window_inversion" in r else 0:
        return "ALIGNMENT"
    return "UNKNOWN"


def main():
    lwe = list(csv.DictReader((ART / "lwe_phone_evidence.csv").open(encoding="utf-8")))
    p1917 = {r["case_id"]: r for r in csv.DictReader(
        (REPO / "Research/Speech/Phase1_9_17/FAILURE_CASE_ANALYSIS.csv").open(encoding="utf-8"))}
    rows = []
    for r in lwe:
        hf = r["human_final_label"]
        r["human_present"] = 1 if hf.endswith("PRESENT") else (0 if hf.endswith("ABSENT") else "")
        r["window_inversion"] = p1917.get(r["token_id"], {}).get("window_inversion", 0)
        r["dec_B"] = decide_B(r)
        r["dec_C_diag"] = decide_C(r)
        r["dec_D_diag"] = decide_D(r)
        r["reason_D"] = reason(r, r["dec_D_diag"])
        r["acoustic_evidence"] = acoustic_evidence(fnum(r["da_span_max"]))
        r["root_cause_1918"] = root_cause(r)
        if r["human_present"] != "":
            y = int(r["human_present"])
            r["fixed_by_D"] = int(int(r["baseline_present"]) != y and r["dec_D_diag"] == ("PRESENT" if y else "ABSENT"))
            r["regressed_by_D"] = int(int(r["baseline_present"]) == y and r["dec_D_diag"] not in ("PRESENT" if y else "ABSENT", "UNCERTAIN"))
            r["uncertain_by_D"] = int(r["dec_D_diag"] == "UNCERTAIN")
        else:
            r["fixed_by_D"] = r["regressed_by_D"] = ""
            r["uncertain_by_D"] = int(r["dec_D_diag"] == "UNCERTAIN")
        rows.append(r)

    cols = ["token_id", "speaker_id", "word", "target_canon", "human_final_label",
            "human_present", "baseline_match", "baseline_present", "baseline_span",
            "baseline_span_post", "da_free_present", "da_margin_per_frame", "da_span",
            "da_span_max", "da_gop_ratio", "frame_max", "sustained", "best_span",
            "window_inversion", "acoustic_evidence", "dec_B", "dec_C_diag", "dec_D_diag",
            "reason_D", "fixed_by_D", "regressed_by_D", "uncertain_by_D",
            "root_cause_1918"]
    with open(OUT / "FAILURE_CASE_ANALYSIS.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=cols, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)

    print("rows:", len(rows))
    print("labeled decisions D:", Counter(r["dec_D_diag"] for r in rows if r["human_present"] != ""))
    print("root causes (labeled):", Counter(r["root_cause_1918"] for r in rows if r["human_present"] != ""))
    print("fixed/regressed/uncertain by D:",
          sum(int(r["fixed_by_D"]) for r in rows if r["fixed_by_D"] != ""),
          sum(int(r["regressed_by_D"]) for r in rows if r["regressed_by_D"] != ""),
          sum(int(r["uncertain_by_D"]) for r in rows if r["human_present"] != ""))
    print("failures detail:")
    for r in rows:
        if r["human_present"] != "" and int(r["baseline_present"]) != int(r["human_present"]):
            print("  %-20s y=%s base=%s B=%s C=%s D=%s(%s) E=%s ac=%s rc=%s" % (
                r["token_id"], r["human_present"], r["baseline_match"], r["dec_B"],
                r["dec_C_diag"], r["dec_D_diag"], r["reason_D"], r["da_span_max"],
                r["acoustic_evidence"], r["root_cause_1918"]))


if __name__ == "__main__":
    main()
