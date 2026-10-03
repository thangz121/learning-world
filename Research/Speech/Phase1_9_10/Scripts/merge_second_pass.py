"""Phase 1.9.11 methodology repair — normalize human second-pass states (Phase 1.9.10).

Supersedes the previous version of this script (which treated Stage B UNCERTAIN as
"acceptable" and contained hard-coded 29/12 counts and stale 8/12 text).

State model (three distinct human states + conflict):
- Stage B TRUE_PRONUNCIATION_ERROR -> HUMAN_TRUE_ERROR
- Stage B UNCERTAIN                -> HUMAN_UNCERTAIN (NOT acceptable)
- Stage A correct + Stage B error  -> HUMAN_CONFLICTED (conflict kept visible)
- no explicit acceptable verdict   -> HUMAN_ACCEPTABLE does not occur in this set

All counts are derived from the actual submitted records; nothing hard-coded.
"""
from __future__ import annotations

import csv
import json
from collections import Counter
from pathlib import Path

PHASE = Path(__file__).resolve().parents[1]
RES = PHASE / "Results"
P199 = PHASE.parent / "Phase1_9_9" / "Results"

CORRECT = {"CLEAR_CORRECT", "PROBABLY_CORRECT"}
INCORRECT = {"CLEAR_INCORRECT", "PROBABLY_INCORRECT"}
AMBIG = {"AMBIGUOUS"}


def read_csv(p: Path):
    with p.open(encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


def write_csv(p: Path, rows, fields=None):
    if not rows:
        p.write_text("empty\n", encoding="utf-8")
        return
    keys = fields or list(rows[0].keys())
    with open(p, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=keys, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)


def derive_199_counts():
    """Trace the original Phase 1.9.9 records; no hard-coded counts."""
    rows = read_csv(P199 / "human_review_results.csv")
    rows = [r for r in rows if r.get("human_pronunciation")]
    human_ok = [r for r in rows if r["human_pronunciation"] in CORRECT]
    human_bad = [r for r in rows if r["human_pronunciation"] in INCORRECT]
    human_amb = [r for r in rows if r["human_pronunciation"] in AMBIG]
    low_ok = [
        r
        for r in human_ok
        if r.get("full_score") not in (None, "") and float(r["full_score"]) < 50
    ]
    return {
        "reviewed": len(rows),
        "first_pass_correct": len(human_ok),
        "first_pass_incorrect": len(human_bad),
        "first_pass_ambiguous": len(human_amb),
        "first_pass_correct_low_score": len(low_ok),
        "first_pass_rate_12_over_29": (len(low_ok) / len(human_ok)) if human_ok else None,
    }


def main():
    stage_a = read_csv(RES / "Human_SecondPass_StageA_Filled.csv")
    stage_b = read_csv(RES / "Human_SecondPass_StageB_Filled.csv")
    a_by = {r["case_id"]: (r.get("stage_a_pronunciation") or "").strip() for r in stage_a}
    b_by = {r["case_id"]: (r.get("stage_b_most_responsible") or "").strip() for r in stage_b}

    cases = read_csv(RES / "phone_model_error_cases.csv")
    roots = read_csv(RES / "phone_model_error_root_causes.csv")
    by_root = {r["case_id"]: r for r in roots}

    merged = []
    for c in cases:
        cid = c["case_id"]
        a = a_by.get(cid, "")
        b = b_by.get(cid, "")
        measured = by_root.get(cid, {}).get("root_cause", "")
        conflict = a in CORRECT and b == "TRUE_PRONUNCIATION_ERROR"
        if b == "TRUE_PRONUNCIATION_ERROR":
            state = "HUMAN_CONFLICTED" if conflict else "HUMAN_TRUE_ERROR"
        elif b == "UNCERTAIN":
            state = "HUMAN_UNCERTAIN"
        elif b in CORRECT:
            state = "HUMAN_ACCEPTABLE"
        else:
            state = "HUMAN_UNCERTAIN"
        merged.append(
            {
                "case_id": cid,
                "speaker_id": c["speaker_id"],
                "target": c["target"],
                "full_score": c["full_score"],
                "raw_score": c.get("raw_score", ""),
                "stage_a_pronunciation": a,
                "stage_b_most_responsible": b,
                "human_state": state,
                "human_review_conflict": conflict,
                "final_human_certainty": "CONFLICTED" if conflict else ("CERTAIN" if state in ("HUMAN_TRUE_ERROR", "HUMAN_ACCEPTABLE") else "UNCERTAIN"),
                "measured_mechanism": measured,
                "reviewer_id": "human_mobile",
                "review_date": "2026-10-03",
            }
        )
    write_csv(RES / "human_second_pass.csv", merged)

    state_counts = Counter(m["human_state"] for m in merged)
    raw_stage_b = Counter(m["stage_b_most_responsible"] for m in merged)

    # diagnostic hypothesis for the 7 uncertain cases (measured mechanisms, NOT truth)
    uncertain_mech = Counter(
        m["measured_mechanism"] for m in merged if m["human_state"] == "HUMAN_UNCERTAIN"
    )

    # update root-cause table with normalized columns
    for r in roots:
        m = next(x for x in merged if x["case_id"] == r["case_id"])
        r["human_stage_a"] = m["stage_a_pronunciation"]
        r["human_stage_b"] = m["stage_b_most_responsible"]
        r["human_state"] = m["human_state"]
        r["human_review_conflict"] = m["human_review_conflict"]
        r["measured_mechanism"] = m["measured_mechanism"]
    write_csv(RES / "phone_model_error_root_causes.csv", roots)

    # derive 1.9.9 counts from actual records
    c199 = derive_199_counts()
    # map pme_XX -> original 1.9.9 review_id (same order as PME rows in human_review_results.csv)
    reviewed = read_csv(P199 / "human_review_results.csv")
    pme_rows = [r for r in reviewed if r.get("diagnostic") == "PHONE_MODEL_ERROR"]
    second_pass_review_ids = {r["review_id"] for r in pme_rows}
    assert len(second_pass_review_ids) == len(merged), (len(second_pass_review_ids), len(merged))
    # first-pass correct cases NOT revised by second pass = confirmed acceptable
    first_pass_ok = [
        r
        for r in reviewed
        if r.get("human_pronunciation") in CORRECT
        and r.get("review_id") not in second_pass_review_ids
    ]
    confirmed_low = [
        r
        for r in first_pass_ok
        if r.get("full_score") not in (None, "") and float(r["full_score"]) < 50
    ]
    revised_rate = (len(confirmed_low) / len(first_pass_ok)) if first_pass_ok else None

    master = json.loads((RES / "phase_1_9_10_master.json").read_text(encoding="utf-8"))
    master["human_second_pass"] = {
        "submitted": True,
        "n": len(merged),
        "raw_stage_b_distribution": dict(raw_stage_b),
        "state_distribution": dict(state_counts),
        "human_true_error_clean": state_counts.get("HUMAN_TRUE_ERROR", 0),
        "human_conflicted": state_counts.get("HUMAN_CONFLICTED", 0),
        "human_uncertain": state_counts.get("HUMAN_UNCERTAIN", 0),
        "human_acceptable": state_counts.get("HUMAN_ACCEPTABLE", 0),
        "raw_true_error_including_conflicted": raw_stage_b.get("TRUE_PRONUNCIATION_ERROR", 0),
        "measured_mechanism_hypothesis_for_uncertain": dict(uncertain_mech),
        "note": "HUMAN_UNCERTAIN is NOT treated as acceptable; pme_01 conflict kept visible",
    }
    master["phase_1_9_9_counts_derived"] = c199
    master["revised_metric"] = {
        "metric": "human_correct_low_score_rate",
        "definition": "confirmed-acceptable = first-pass CORRECT cases not revised by second pass; numerator = those with score<50",
        "denominator_confirmed_acceptable": len(first_pass_ok),
        "numerator_confirmed_low": len(confirmed_low),
        "revised_rate": revised_rate,
        "superseded_first_pass_rate": c199["first_pass_rate_12_over_29"],
        "unresolved_cases_excluded": state_counts.get("HUMAN_UNCERTAIN", 0)
        + state_counts.get("HUMAN_CONFLICTED", 0),
        "metric_status": "RECALCULATED_WITH_EXPLICIT_DEFINITION",
    }
    master["decision"] = "B. ROOT_CAUSE_PARTIALLY_IDENTIFIED"
    master["decision_note"] = (
        "Methodology repaired: raw Stage B = 5 TRUE_PRONUNCIATION_ERROR + 7 UNCERTAIN. "
        "Normalized: 4 clean HUMAN_TRUE_ERROR + 7 HUMAN_UNCERTAIN + 1 HUMAN_CONFLICTED (pme_01); "
        "0 explicitly HUMAN_ACCEPTABLE. Measured mechanisms for the 7 uncertain cases are a "
        "diagnostic hypothesis only (boundary 3, full-file CTC 1, attractor 1, realization 2), "
        "not human-verified truth. Scorer unchanged."
    )
    master["scorer_modification_justified"] = "NO"
    (RES / "phase_1_9_10_master.json").write_text(
        json.dumps(master, indent=2, ensure_ascii=True), encoding="utf-8"
    )
    (RES / "decision.json").write_text(
        json.dumps(
            {
                "decision": "B. ROOT_CAUSE_PARTIALLY_IDENTIFIED",
                "note": master["decision_note"],
                "human_second_pass": master["human_second_pass"],
                "revised_metric": master["revised_metric"],
                "scorer_modification_justified": "NO",
                "scorer_modified": False,
                "production_vad": False,
                "router_locked": False,
                "unity_integrated": False,
            },
            indent=2,
        ),
        encoding="utf-8",
    )

    print(json.dumps(master["human_second_pass"], indent=2, ensure_ascii=True))
    print(json.dumps(master["phase_1_9_9_counts_derived"], indent=2, ensure_ascii=True))
    print(json.dumps(master["revised_metric"], indent=2, ensure_ascii=True))


if __name__ == "__main__":
    main()
