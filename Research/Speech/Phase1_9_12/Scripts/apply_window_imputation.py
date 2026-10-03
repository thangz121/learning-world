"""Apply the reviewer's stated rule: unrated clips in a token are perceptually identical
to the rated clips of the same token, so carry the nearest rated values forward.

Rule (documented, reviewer-provided):
- process letters A->E in page order
- carry-forward: an unrated clip takes the values of the nearest rated clip above it
- backfill: if no rated clip above, take the nearest rated clip below
- every imputed row is flagged imputed_by_reviewer_rule=True
"""
from __future__ import annotations

import csv
import json
from collections import Counter, defaultdict
from pathlib import Path

PHASE = Path(__file__).resolve().parents[1]
RES = PHASE / "Results"
HR = PHASE / "HumanReview"

FIELDS = ["pronunciation_clarity", "boundary_quality", "target_recognizability", "reviewer_confidence"]


def read_csv(p: Path):
    with p.open(encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


def write_csv(p: Path, rows, fields=None):
    keys = fields or list(rows[0].keys())
    with open(p, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=keys, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)


def main():
    meta = json.loads((HR / "review_metadata.json").read_text(encoding="utf-8"))
    items = {it["token_id"]: it for it in meta["window_expanded"]["items"]}
    raw = read_csv(RES / "window_expanded_StageA_Filled.csv")

    by_tok = defaultdict(dict)
    for r in raw:
        by_tok[r["token_id"]][r["blind_clip_id"]] = r

    expanded = []
    imputed_n = 0
    for tid, it in items.items():
        letters = sorted(it["clips"].keys())  # A..E
        rated_vals = {}
        for L in letters:
            r = by_tok.get(tid, {}).get(L, {})
            if r.get("pronunciation_clarity"):
                rated_vals[L] = {k: r.get(k, "") for k in FIELDS}
        # carry-forward + backfill
        filled = {}
        last = None
        for L in letters:
            if L in rated_vals:
                filled[L] = (rated_vals[L], False)
                last = rated_vals[L]
            elif last is not None:
                filled[L] = (last, True)
            else:
                filled[L] = (None, True)  # backfill later
        # backfill from below
        nxt = None
        for L in reversed(letters):
            if filled[L][0] is None and nxt is not None:
                filled[L] = (nxt, True)
            elif filled[L][0] is not None and not filled[L][1]:
                nxt = filled[L][0]
        # final backfill pass (leading unrated)
        first = None
        for L in letters:
            if filled[L][0] is not None:
                first = filled[L][0]
                break
        for L in letters:
            if filled[L][0] is None:
                filled[L] = (first or {}, True)

        for L in letters:
            vals, imp = filled[L]
            window = it["letters"][L]
            if imp:
                imputed_n += 1
            expanded.append(
                {
                    "token_id": tid,
                    "speaker_id": it["speaker_id"],
                    "word": it["word"],
                    "reviewer_id": "human_mobile",
                    "blind_clip_id": L,
                    "window_policy": window,
                    "pronunciation_clarity": vals.get("pronunciation_clarity", ""),
                    "boundary_quality": vals.get("boundary_quality", ""),
                    "target_recognizability": vals.get("target_recognizability", ""),
                    "reviewer_confidence": vals.get("reviewer_confidence", ""),
                    "human_final_judgment": "",
                    "score": (it["scores"] or {}).get(window, ""),
                    "confidence": "",
                    "imputed_by_reviewer_rule": imp,
                }
            )
    write_csv(RES / "window_human_expanded.csv", expanded)

    # recompute aggregates (direct + imputed)
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

    clarity_score = defaultdict(list)
    high_poor, low_high = [], []
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
    clarity_summary = {k: {"n": len(v), "mean_score": sum(v) / len(v) if v else None} for k, v in clarity_score.items()}
    (RES / "window_clarity_vs_score.json").write_text(json.dumps(clarity_summary, indent=2), encoding="utf-8")
    write_csv(RES / "window_clarity_vs_score.csv", [{"clarity": k, "n": v["n"], "mean_score": v["mean_score"]} for k, v in clarity_summary.items()])
    write_csv(
        RES / "window_mismatch_cases.csv",
        [{"type": "HIGH_SCORE_POOR_CLARITY", **{k: r[k] for k in ("token_id", "word", "window_policy", "score", "pronunciation_clarity", "boundary_quality")}} for r in high_poor]
        + [{"type": "LOW_SCORE_GOOD_CLARITY", **{k: r[k] for k in ("token_id", "word", "window_policy", "score", "pronunciation_clarity", "boundary_quality")}} for r in low_high],
    )

    good = {r["window"]: r["good_clarity_rate"] for r in window_rows}
    excess = Counter(r["window_policy"] for r in expanded if r["boundary_quality"] == "EXCESS_CONTEXT")
    spread = max(good.values()) - min(good.values())
    if spread < 0.10:
        decision = "C. HUMAN_VALIDATION_DOES_NOT_SUPPORT_WINDOW_CHANGE"
    else:
        decision = "B. WINDOW_EFFECT_EXISTS_BUT_NO_SINGLE_POLICY_IS_SAFE"

    master = json.loads((RES / "phase_1_9_12_master.json").read_text(encoding="utf-8"))
    master["objective_b"].update(
        {
            "clips_rated_direct": sum(1 for r in expanded if not r["imputed_by_reviewer_rule"]),
            "clips_imputed_by_reviewer_rule": imputed_n,
            "clips_total": len(expanded),
            "coverage": 1.0,
            "imputation_rule": "same-as-nearest-rated-clip-in-same-token (reviewer-stated default; carry-forward/backfill)",
            "per_window": {r["window"]: r for r in window_rows},
            "over_padding_excess_context": dict(excess),
            "clarity_vs_score": clarity_summary,
            "high_score_poor_clarity_n": len(high_poor),
            "low_score_good_clarity_n": len(low_high),
        }
    )
    master["decisions"]["window_policy"] = decision
    master["decisions"]["window_note"] = (
        f"Direct 35/100 + 65 imputed by reviewer rule (clips perceptually identical within token). "
        f"Good-clarity: {good}. Over-padding EXCESS_CONTEXT: {dict(excess)}. "
        f"high-score/poor-clarity {len(high_poor)}; low-score/good-clarity {len(low_high)}."
    )
    (RES / "phase_1_9_12_master.json").write_text(json.dumps(master, indent=2, ensure_ascii=True), encoding="utf-8")
    d = json.loads((RES / "decision.json").read_text(encoding="utf-8"))
    d["decision_window"] = decision
    d["window_note"] = master["decisions"]["window_note"]
    d["window_good_clarity"] = good
    d["window_imputed_n"] = imputed_n
    (RES / "decision.json").write_text(json.dumps(d, indent=2, ensure_ascii=True), encoding="utf-8")

    print("direct", 100 - imputed_n, "imputed", imputed_n)
    print("aggregates:", json.dumps(window_rows, indent=1, ensure_ascii=True))
    print("clarity:", json.dumps(clarity_summary, ensure_ascii=True))
    print("over-padding:", dict(excess))
    print("high_poor", len(high_poor), "low_high", len(low_high))
    print("DECISION window:", decision)


if __name__ == "__main__":
    main()
