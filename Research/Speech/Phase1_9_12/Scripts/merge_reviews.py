"""Phase 1.9.12 — merge both human packs, recompute evidence, update decisions."""
from __future__ import annotations

import csv
import json
from collections import Counter, defaultdict
from pathlib import Path

PHASE = Path(__file__).resolve().parents[1]
RES = PHASE / "Results"
HR = PHASE / "HumanReview"

PRESENT = {"FINAL_CONSONANT_CLEARLY_PRESENT", "FINAL_CONSONANT_PROBABLY_PRESENT"}
ABSENT = {"FINAL_CONSONANT_CLEARLY_ABSENT", "FINAL_CONSONANT_PROBABLY_ABSENT"}


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


def phone_presence(m):
    return "PRESENT" if str(m).lower() in ("exact", "soft") else "ABSENT"


def main():
    # ---------------- final consonant ----------------
    fc = read_csv(RES / "final_consonant_StageA_Filled.csv")
    labels = {r["case_id"]: (r.get("human_final_label") or "").strip() for r in fc}
    conf = {r["case_id"]: (r.get("reviewer_confidence") or "").strip() for r in fc}

    hvm = read_csv(RES / "final_consonant_human_vs_model.csv")
    value = Counter()
    class_stats = defaultdict(lambda: Counter())
    for r in hvm:
        tid = r["token_id"]
        lab = labels.get(tid, "")
        r["human_final_label"] = lab
        r["reviewer_confidence"] = conf.get(tid, "")
        r["human_label_source"] = "final_consonant_blind_human" if lab else r.get("human_label_source", "")
        if lab:
            human_present = lab in PRESENT
            phone_correct = (phone_presence(r["phone_final_match"]) == "PRESENT") == human_present
            ac_correct = (r["acoustic_indicator"] == "PRESENT") == human_present
            if phone_correct and ac_correct:
                value["BOTH_RIGHT"] += 1
            elif (not phone_correct) and ac_correct:
                value["PHONE_WRONG_ACOUSTIC_RIGHT"] += 1
            elif phone_correct and (not ac_correct):
                value["PHONE_RIGHT_ACOUSTIC_WRONG"] += 1
            else:
                value["BOTH_WRONG"] += 1
            cls = r["final_phone_class"]
            if lab in PRESENT:
                class_stats[cls]["present"] += 1
            elif lab in ABSENT:
                class_stats[cls]["absent"] += 1
            else:
                class_stats[cls]["ambiguous"] += 1
    write_csv(RES / "final_consonant_human_vs_model.csv", hvm)

    value_rows = [
        {"metric": k, "count": v, "note": "real human final-consonant labels (blind)"}
        for k, v in value.items()
    ]
    value_rows.append({"metric": "N_HUMAN_LABELED", "count": sum(1 for r in hvm if r["human_final_label"]), "note": ""})
    value_rows.append({"metric": "N_PENDING", "count": sum(1 for r in hvm if not r["human_final_label"]), "note": "outside the 28-item sample"})
    for cls, c in sorted(class_stats.items()):
        value_rows.append({"metric": f"CLASS_{cls}", "count": c["present"] + c["absent"] + c["ambiguous"], "note": f"present={c['present']} absent={c['absent']} ambiguous={c['ambiguous']}"})
    write_csv(RES / "acoustic_support_value.csv", value_rows)

    # update final_consonant_human_review.csv with real labels
    review = read_csv(RES / "final_consonant_human_review.csv")
    for r in review:
        tid = r["case_id"]
        if tid in labels:
            r["human_final_label"] = labels[tid]
            r["reviewer_confidence"] = conf.get(tid, "")
            r["reviewer_id"] = "human_mobile"
            r["review_date"] = "2026-10-03"
    write_csv(RES / "final_consonant_human_review.csv", review)

    # ---------------- window expanded ----------------
    wrows = read_csv(RES / "window_expanded_StageA_Filled.csv")
    meta = json.loads((HR / "review_metadata.json").read_text(encoding="utf-8"))
    items = {it["token_id"]: it for it in meta["window_expanded"]["items"]}

    expanded = []
    for r in wrows:
        tid = r["token_id"]
        letter = r["blind_clip_id"]
        it = items.get(tid, {})
        window = (it.get("letters") or {}).get(letter, "")
        score = (it.get("scores") or {}).get(window, "")
        expanded.append(
            {
                "token_id": tid,
                "speaker_id": it.get("speaker_id", ""),
                "word": it.get("word", ""),
                "reviewer_id": "human_mobile",
                "blind_clip_id": letter,
                "window_policy": window,
                "pronunciation_clarity": r.get("pronunciation_clarity", ""),
                "boundary_quality": r.get("boundary_quality", ""),
                "target_recognizability": r.get("target_recognizability", ""),
                "reviewer_confidence": r.get("reviewer_confidence", ""),
                "human_final_judgment": "",
                "score": score,
                "confidence": "",
            }
        )
    write_csv(RES / "window_human_expanded.csv", expanded)

    # consensus (single reviewer = their labels)
    consensus = []
    for tid, it in items.items():
        rows = [r for r in expanded if r["token_id"] == tid]
        byw = {r["window_policy"]: r for r in rows}
        consensus.append(
            {
                "token_id": tid,
                "speaker_id": it["speaker_id"],
                "word": it["word"],
                "stratum": it["stratum"],
                "consensus_clarity": "SINGLE_REVIEWER",
                "consensus_boundary": "SINGLE_REVIEWER",
                "consensus_recognizability": "SINGLE_REVIEWER",
                "note": ";".join(f"{w}:{byw.get(w,{}).get('pronunciation_clarity','')}" for w in ("FULL", "RAW", "PAD100", "PAD250", "PAD500")),
            }
        )
    write_csv(RES / "window_consensus.csv", consensus)
    write_csv(
        RES / "window_reviewer_agreement.csv",
        [{"reviewers": 1, "metric": "N/A", "note": "single reviewer; inter-rater agreement not computable"}],
    )

    # per-window aggregates
    per_window = defaultdict(lambda: Counter())
    for r in expanded:
        w = r["window_policy"]
        per_window[w]["n"] += 1
        per_window[w]["clarity_" + r["pronunciation_clarity"]] += 1
        per_window[w]["boundary_" + r["boundary_quality"]] += 1
        per_window[w]["recog_" + r["target_recognizability"]] += 1
    window_rows = []
    for w in ("FULL", "RAW", "PAD100", "PAD250", "PAD500"):
        c = per_window[w]
        n = c["n"] or 1
        window_rows.append(
            {
                "window": w,
                "n": c["n"],
                "clarity_clear": c["clarity_CLEAR"],
                "clarity_mostly_clear": c["clarity_MOSTLY_CLEAR"],
                "clarity_ambiguous": c["clarity_AMBIGUOUS"],
                "clarity_poor": c["clarity_POOR"],
                "good_clarity_rate": (c["clarity_CLEAR"] + c["clarity_MOSTLY_CLEAR"]) / n,
                "boundary_clean": c["boundary_CLEAN"],
                "boundary_cut": c["boundary_SLIGHTLY_CUT"] + c["boundary_STRONGLY_CUT"],
                "boundary_excess_context": c["boundary_EXCESS_CONTEXT"],
                "recog_clear": c["recog_CLEAR"],
                "recog_probable": c["recog_PROBABLE"],
                "recog_not_recognizable": c["recog_NOT_RECOGNIZABLE"],
            }
        )
    write_csv(RES / "window_aggregates.csv", window_rows)

    # clarity vs score
    clarity_score = defaultdict(list)
    high_poor = []
    low_high = []
    for r in expanded:
        try:
            s = float(r["score"])
        except (TypeError, ValueError):
            continue
        clarity_score[r["pronunciation_clarity"]].append(s)
        if s >= 70 and r["pronunciation_clarity"] in ("POOR", "AMBIGUOUS"):
            high_poor.append(r)
        if s < 50 and r["pronunciation_clarity"] in ("CLEAR", "MOSTLY_CLEAR"):
            low_high.append(r)
    clarity_summary = {
        k: {"n": len(v), "mean_score": sum(v) / len(v) if v else None}
        for k, v in clarity_score.items()
    }
    (RES / "window_clarity_vs_score.json").write_text(json.dumps(clarity_summary, indent=2), encoding="utf-8")
    write_csv(
        RES / "window_clarity_vs_score.csv",
        [{"clarity": k, "n": v["n"], "mean_score": v["mean_score"]} for k, v in clarity_summary.items()],
    )
    write_csv(
        RES / "window_mismatch_cases.csv",
        [
            {"type": "HIGH_SCORE_POOR_CLARITY", **{k: r[k] for k in ("token_id", "word", "window_policy", "score", "pronunciation_clarity", "boundary_quality")}}
            for r in high_poor
        ]
        + [
            {"type": "LOW_SCORE_GOOD_CLARITY", **{k: r[k] for k in ("token_id", "word", "window_policy", "score", "pronunciation_clarity", "boundary_quality")}}
            for r in low_high
        ],
    )

    # over-padding check
    excess = Counter(r["window_policy"] for r in expanded if r["boundary_quality"] == "EXCESS_CONTEXT")
    over_padding = dict(excess)

    # decisions
    good = {r["window"]: r["good_clarity_rate"] for r in window_rows}
    ac_decision = "C. ACOUSTIC_SUPPORT_IS_PROMISING_BUT_INSUFFICIENT"
    # if acoustic correct on majority of human-labeled absent/present and beats phone
    n_lab = sum(1 for r in hvm if r["human_final_label"])
    if n_lab >= 20:
        phone_right = value.get("BOTH_RIGHT", 0) + value.get("PHONE_RIGHT_ACOUSTIC_WRONG", 0)
        ac_right = value.get("BOTH_RIGHT", 0) + value.get("PHONE_WRONG_ACOUSTIC_RIGHT", 0)
        if ac_right > phone_right + 3:
            ac_decision = "A. ACOUSTIC_SUPPORT_SHOWS_INDEPENDENT_VALUE"
        elif ac_right <= phone_right:
            ac_decision = "B. ACOUSTIC_SUPPORT_IS_REDUNDANT"
    window_decision = "B. WINDOW_EFFECT_EXISTS_BUT_NO_SINGLE_POLICY_IS_SAFE"
    # if clarity roughly equal everywhere -> C
    spread = max(good.values()) - min(good.values()) if good else 0
    if spread < 0.10:
        window_decision = "C. HUMAN_VALIDATION_DOES_NOT_SUPPORT_WINDOW_CHANGE"

    master = json.loads((RES / "phase_1_9_12_master.json").read_text(encoding="utf-8"))
    master["objective_a"]["human_reviewed_final_consonants"] = n_lab
    master["objective_a"]["real_label_value_counts"] = dict(value)
    master["objective_a"]["class_present_absent"] = {k: dict(v) for k, v in class_stats.items()}
    master["objective_b"] = {
        "tokens_reviewed": len(items),
        "clips_reviewed": len(expanded),
        "reviewers": 1,
        "per_window": {r["window"]: r for r in window_rows},
        "over_padding_excess_context": over_padding,
        "clarity_vs_score": clarity_summary,
        "high_score_poor_clarity_n": len(high_poor),
        "low_score_good_clarity_n": len(low_high),
    }
    master["decisions"]["acoustic_support"] = ac_decision
    master["decisions"]["window_policy"] = window_decision
    (RES / "phase_1_9_12_master.json").write_text(json.dumps(master, indent=2, ensure_ascii=True), encoding="utf-8")
    (RES / "decision.json").write_text(
        json.dumps(
            {
                "decision_acoustic": ac_decision,
                "decision_window": window_decision,
                "acoustic_value_counts": dict(value),
                "n_human_labeled_final_consonants": n_lab,
                "window_good_clarity": good,
                "over_padding_excess_context": over_padding,
                "production_vad": False,
                "router_locked": False,
                "unity_integrated": False,
                "scorer_modified": False,
                "production_window_locked": False,
            },
            indent=2,
        ),
        encoding="utf-8",
    )

    print("FC value:", dict(value))
    print("FC classes:", {k: dict(v) for k, v in class_stats.items()})
    print("window aggregates:", json.dumps(window_rows, indent=1, ensure_ascii=True))
    print("clarity vs score:", json.dumps(clarity_summary, indent=1))
    print("over-padding:", over_padding)
    print("high_score_poor_clarity:", len(high_poor), "low_score_good_clarity:", len(low_high))
    print("DECISION acoustic:", ac_decision)
    print("DECISION window:", window_decision)


if __name__ == "__main__":
    main()
