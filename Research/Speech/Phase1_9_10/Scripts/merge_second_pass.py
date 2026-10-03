"""Merge human second-pass submissions and revise Phase 1.9.10 conclusions.

Interpretation (confirmed by reviewer):
- Stage B = TRUE_PRONUNCIATION_ERROR  -> child actually mispronounced (low score justified)
- Stage B = UNCERTAIN (or anything else) -> pronunciation ACCEPTABLE;
  the low score is system-side (boundary/window/phone-model evidence), not the child.
"""
from __future__ import annotations

import csv
import json
from collections import Counter
from pathlib import Path

PHASE = Path(__file__).resolve().parents[1]
RES = PHASE / "Results"
SUBS = RES / "submissions"


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


def main():
    stage_a = read_csv(RES / "Human_SecondPass_StageA_Filled.csv")
    stage_b = read_csv(RES / "Human_SecondPass_StageB_Filled.csv")
    a_by = {r["case_id"]: (r.get("stage_a_pronunciation") or "").strip() for r in stage_a}
    b_by = {r["case_id"]: (r.get("stage_b_most_responsible") or "").strip() for r in stage_b}

    cases = read_csv(RES / "phone_model_error_cases.csv")
    roots = read_csv(RES / "phone_model_error_root_causes.csv")
    master = json.loads((RES / "phase_1_9_10_master.json").read_text(encoding="utf-8"))
    by_case_root = {r["case_id"]: r for r in roots}

    merged = []
    for c in cases:
        cid = c["case_id"]
        a = a_by.get(cid, "")
        b = b_by.get(cid, "")
        verdict = (
            "TRUE_PRONUNCIATION_ERROR"
            if b == "TRUE_PRONUNCIATION_ERROR"
            else "SYSTEM_SIDE_ACCEPTABLE_LOW_SCORE"
        )
        measured = by_case_root.get(cid, {}).get("root_cause", "")
        merged.append(
            {
                "case_id": cid,
                "speaker_id": c["speaker_id"],
                "target": c["target"],
                "full_score": c["full_score"],
                "raw_score": c.get("raw_score", ""),
                "stage_a_pronunciation": a,
                "stage_b_most_responsible": b,
                "human_verdict": verdict,
                "measured_mechanism": measured,
                "revised_root_cause": (
                    "TRUE_PRONUNCIATION_ERROR" if verdict == "TRUE_PRONUNCIATION_ERROR" else measured
                ),
                "reviewer_id": "human_mobile",
                "review_date": "2026-10-03",
            }
        )
    write_csv(RES / "human_second_pass.csv", merged)

    # update root-cause table with human columns
    for r in roots:
        m = next(x for x in merged if x["case_id"] == r["case_id"])
        r["human_stage_a"] = m["stage_a_pronunciation"]
        r["human_stage_b"] = m["stage_b_most_responsible"]
        r["human_verdict"] = m["human_verdict"]
        r["revised_root_cause"] = m["revised_root_cause"]
    write_csv(RES / "phone_model_error_root_causes.csv", roots)

    sys_n = sum(1 for m in merged if m["human_verdict"] == "SYSTEM_SIDE_ACCEPTABLE_LOW_SCORE")
    true_n = sum(1 for m in merged if m["human_verdict"] == "TRUE_PRONUNCIATION_ERROR")
    n = len(merged)

    # revised distribution: system-side cases keep measured mechanism; true errors override
    dist = Counter(m["revised_root_cause"] for m in merged)

    # revised Phase 1.9.9 metric: 4 of the 12 previously human-OK low-score cases are true errors
    old_human_ok = 29
    old_low_ok = 12
    revised_human_ok = old_human_ok - true_n
    revised_low_ok = old_low_ok - true_n
    revised_rate = revised_low_ok / revised_human_ok if revised_human_ok else None

    # scoring-window-related share among system-side cases
    window_related = sum(
        1
        for m in merged
        if m["human_verdict"] == "SYSTEM_SIDE_ACCEPTABLE_LOW_SCORE"
        and m["measured_mechanism"] in ("BOUNDARY_ERROR", "CTC_ALIGNMENT_ERROR")
    )

    master["human_second_pass"] = {
        "submitted": True,
        "n": n,
        "system_side_acceptable_low_score": sys_n,
        "true_pronunciation_error": true_n,
        "system_side_rate": sys_n / n,
        "true_error_rate": true_n / n,
        "window_related_among_system_side": window_related,
        "revised_root_cause_distribution": dict(dist),
        "revised_1_9_9_human_correct_low_score_rate": revised_rate,
        "reviewer_note": "Stage B UNCERTAIN = pronunciation acceptable; low score is system-side",
    }
    master["decision"] = "A. ROOT_CAUSE_SUFFICIENTLY_IDENTIFIED"
    master["decision_note"] = (
        "Human second pass: 8/12 acceptable pronunciation with low scores (system-side); "
        "4/12 true pronunciation errors. Among system-side cases, 5/8 are scoring-window "
        "related (boundary + full-file alignment) with measured evidence; 2 realization "
        "variations; 1 attractor collapse. A targeted research-only fix (score the padded "
        "VAD window / multi-variant evidence) can now be designed, but NOT implemented."
    )
    master["scorer_modification_justified"] = "PARTIALLY"
    master["scorer_modified"] = False
    (RES / "phase_1_9_10_master.json").write_text(
        json.dumps(master, indent=2, ensure_ascii=True), encoding="utf-8"
    )
    (RES / "decision.json").write_text(
        json.dumps(
            {
                "decision": "A. ROOT_CAUSE_SUFFICIENTLY_IDENTIFIED",
                "note": master["decision_note"],
                "human_second_pass": master["human_second_pass"],
                "scorer_modification_justified": "PARTIALLY",
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


if __name__ == "__main__":
    main()
