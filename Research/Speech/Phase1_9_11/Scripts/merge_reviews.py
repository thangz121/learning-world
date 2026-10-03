"""Phase 1.9.11 — merge the two human review packs and update conclusions.

Pack 1 (scorer_miss): 6 cases, Stage A blind + Stage B factor.
Pack 2 (window_spot): 13 cases, Stage A blind + Stage B factor.
Reviewer rule recorded: missing ending sound -> fail.
"""
from __future__ import annotations

import csv
import json
from collections import Counter
from pathlib import Path

PHASE = Path(__file__).resolve().parents[1]
RES = PHASE / "Results"

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


def main():
    # ---------------- pack 1: scorer_miss ----------------
    a1 = {r["case_id"]: (r.get("stage_a_pronunciation") or "").strip() for r in read_csv(RES / "scorer_miss_StageA_Filled.csv")}
    b1 = {r["case_id"]: (r.get("stage_b_most_responsible") or "").strip() for r in read_csv(RES / "scorer_miss_StageB_Filled.csv")}
    forensics = read_csv(RES / "scorer_miss_forensics.csv")

    sm_rows = []
    for r in forensics:
        cid = r["case_id"]
        a, b = a1.get(cid, ""), b1.get(cid, "")
        if a in CORRECT and b == "TRUE_SCORER_MISS":
            state = "HUMAN_CONFLICTED"
        elif a in INCORRECT and b == "TRUE_SCORER_MISS":
            state = "HUMAN_TRUE_ERROR"
        else:
            state = "HUMAN_UNCERTAIN"
        sm_rows.append(
            {
                "case_id": cid,
                "speaker_id": r["speaker_id"],
                "target": r["target"],
                "stage_a_pronunciation": a,
                "stage_b_most_responsible": b,
                "human_state": state,
                "measured_mechanism": r["mechanism"],
                "full_score": r["full_score"],
                "raw_score": r["raw_score"],
                "score_range": r["score_range"],
                "observed_full": r["observed_full"],
                "canonical": r["canonical"],
                "reviewer_id": "human_mobile",
                "review_date": "2026-10-03",
            }
        )
    write_csv(RES / "scorer_miss_human_review.csv", sm_rows)
    sm_states = Counter(r["human_state"] for r in sm_rows)

    # confirmed ending-sound errors = four cases with human TRUE_ERROR
    ending_sound_cases = [r for r in sm_rows if r["human_state"] == "HUMAN_TRUE_ERROR"]
    sm_master = {
        "n": len(sm_rows),
        "state_distribution": dict(sm_states),
        "reviewer_rule": "missing ending sound -> fail",
        "confirmed_true_scorer_miss_n": sm_states.get("HUMAN_TRUE_ERROR", 0),
        "confirmed_true_scorer_miss_cases": [
            {
                "case_id": r["case_id"],
                "speaker_id": r["speaker_id"],
                "target": r["target"],
                "full_score": r["full_score"],
                "raw_score": r["raw_score"],
                "observed_full": r["observed_full"],
                "mechanism": "FINAL_CONSONANT_DELETION_NOT_REPRESENTED_BY_FORCED_ALIGNMENT",
            }
            for r in ending_sound_cases
        ],
        "uncertain_or_conflicted": [
            r["case_id"] for r in sm_rows if r["human_state"] != "HUMAN_TRUE_ERROR"
        ],
    }

    # ---------------- pack 2: window spot check ----------------
    a2 = {r["token_id"]: (r.get("stage_a_pronunciation") or "").strip() for r in read_csv(RES / "window_spot_StageA_Filled.csv")}
    b2 = {r["token_id"]: (r.get("stage_b_most_responsible") or "").strip() for r in read_csv(RES / "window_spot_StageB_Filled.csv")}
    spot = read_csv(RES / "window_human_spot_check.csv")
    ab = {r["token_id"]: r for r in read_csv(RES / "window_ab_80_tokens.csv")}

    spot_rows = []
    for r in spot:
        tid = r["token_id"]
        a, b = a2.get(tid, ""), b2.get(tid, "")
        if a in CORRECT:
            state = "HUMAN_CONFIRMED_CORRECT"
        elif a in INCORRECT:
            state = "HUMAN_CONFIRMED_ERROR"
        else:
            state = "HUMAN_UNCERTAIN"
        src = ab.get(tid, {})
        spot_rows.append(
            {
                "token_id": tid,
                "speaker_id": r["speaker_id"],
                "target": r["target"],
                "spot_group": r["spot_group"],
                "stage_a_pronunciation": a,
                "stage_b_most_responsible": b,
                "human_state": state,
                "full_score": r["full_score"],
                "raw_vad_score": r["raw_vad_score"],
                "pad250_score": r["pad250_score"],
                "score_range": r["score_range"],
                "reviewer_id": "human_mobile",
                "review_date": "2026-10-03",
            }
        )
    write_csv(RES / "window_human_spot_check.csv", spot_rows)
    spot_states = Counter(r["human_state"] for r in spot_rows)

    correct = [r for r in spot_rows if r["human_state"] == "HUMAN_CONFIRMED_CORRECT"]
    errors = [r for r in spot_rows if r["human_state"] == "HUMAN_CONFIRMED_ERROR"]
    uncertain = [r for r in spot_rows if r["human_state"] == "HUMAN_UNCERTAIN"]

    def pass_rate(rows, col):
        if not rows:
            return None
        return sum(1 for r in rows if float(r[col]) >= 50) / len(rows)

    window_confusion = {}
    for variant, col in (("full", "full_score"), ("raw", "raw_vad_score"), ("pad250", "pad250_score")):
        window_confusion[variant] = {
            "correct_pass_rate": pass_rate(correct, col),
            "error_pass_rate": pass_rate(errors, col),
            "correct_reject_n": sum(1 for r in correct if float(r[col]) < 50),
            "error_catch_n": sum(1 for r in errors if float(r[col]) < 50),
        }

    spot_master = {
        "n": len(spot_rows),
        "state_distribution": dict(spot_states),
        "window_confusion_on_human_checked": window_confusion,
        "note": "small sample (9 correct / 3 error / 1 uncertain); descriptive only",
    }

    # ---------------- update master + decision ----------------
    master = json.loads((RES / "phase_1_9_11_master.json").read_text(encoding="utf-8"))
    master["scorer_miss_human"] = sm_master
    master["window_spotcheck_human"] = spot_master
    master["scorer_formula_change"] = "RESEARCH_NEEDED"
    master["scorer_formula_change_note"] = (
        "3 human-confirmed TRUE_SCORER_MISS cases (final consonant deletion in 'four'); "
        "forced alignment cannot represent a deleted final phone and still assigns a match. "
        "A targeted final-phone acoustic-support check could be designed, but NOT implemented."
    )
    master["decision"] = "B. WINDOW_EFFECT_REAL_BUT_NOT_SAFE"
    master["decision_note"] = (
        "Human spot check (13 tokens) confirms: FULL misses 3/3 confirmed 'four' ending-sound errors "
        "while rejecting 3/9 correct; RAW catches 2/3 errors but rejects 4/9 correct; PAD250 misses 2/3 "
        "errors. No window/policy is safe; the ending-sound failure is a phone-model/forced-alignment "
        "issue, not fixable by window choice alone."
    )
    (RES / "phase_1_9_11_master.json").write_text(json.dumps(master, indent=2, ensure_ascii=True), encoding="utf-8")
    (RES / "decision.json").write_text(
        json.dumps(
            {
                "decision": "B. WINDOW_EFFECT_REAL_BUT_NOT_SAFE",
                "note": master["decision_note"],
                "scorer_formula_change": "RESEARCH_NEEDED",
                "scorer_formula_change_note": master["scorer_formula_change_note"],
                "scorer_miss_human": sm_master,
                "window_spotcheck_human": spot_master,
                "production_vad": False,
                "router_locked": False,
                "unity_integrated": False,
                "scorer_modified": False,
            },
            indent=2,
        ),
        encoding="utf-8",
    )

    print(json.dumps(sm_master, indent=2, ensure_ascii=True))
    print(json.dumps(spot_master, indent=2, ensure_ascii=True))


if __name__ == "__main__":
    main()
