"""Phase 1.9.17 — classification v2 + mechanism counts (research only, no model runs).

Reads artifacts/case_evidence.csv (produced by del_alignment_diagnostic.py) and:
  - appends window-inversion / span-mismatch / unsupported-acceptance flags
  - classifies each case (final-consonant labels authoritative; word-verdict
    inferences marked separately; human uncertainty kept separate)
  - counts mechanism prevalence over all 80 tokens
  - rewrites FAILURE_CASE_ANALYSIS.csv and updates EXPERIMENT_RESULTS.json

Classification rules (documented, applied in order):
  ASSESSABILITY_FAILURE : fidelity v2 refusal state (none in this corpus)
  NON_FINAL_CONSONANT   : final expected phone is a vowel
  HUMAN_UNCERTAIN       : no final label and human verdict uncertain/conflicted
  OK                    : human and baseline agree (flag OK_UNSUPPORTED if accepted
                          with evidence_class == NONE)
  present rejected      : window inversion -> ALIGNMENT
                          STRONG evidence -> SCORING (THRESHOLD if 40<=soft<50)
                          WEAK -> MIXED ; NONE -> ACOUSTIC
  absent accepted       : NONE -> DELETION ; WEAK/STRONG -> MIXED
"""
from __future__ import annotations

import csv
import json
from collections import Counter
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
OUT = REPO / "Research/Speech/Phase1_9_17"
ART = OUT / "artifacts"
REFUSAL = {"NO_SPEECH", "UNINTELLIGIBLE", "FREE_SPEAK", "INCOMPLETE"}
CORRECT_VERDICTS = {"CLEAR_CORRECT", "PROBABLY_CORRECT", "HUMAN_CONFIRMED_CORRECT"}
ERROR_VERDICTS = {"CLEAR_INCORRECT", "PROBABLY_INCORRECT", "HUMAN_TRUE_ERROR",
                  "HUMAN_CONFIRMED_ERROR"}
UNCERTAIN_VERDICTS = {"AMBIGUOUS", "HUMAN_UNCERTAIN", "HUMAN_CONFLICTED", "UNCERTAIN"}


def fnum(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def main():
    rows = list(csv.DictReader((ART / "case_evidence.csv").open(encoding="utf-8")))
    for r in rows:
        full, raw = fnum(r["window_full"]), fnum(r["window_raw"])
        r["window_inversion"] = int(full is not None and raw is not None
                                    and ((full < 50 <= raw) or (raw < 50 <= full)))
        r["span_mismatch"] = int(fnum(r["span_max_post"]) is not None
                                 and fnum(r["da_span_max_post"]) is not None
                                 and fnum(r["span_max_post"]) < 0.10
                                 and fnum(r["da_span_max_post"]) >= 0.30)
        r["unsupported_acceptance"] = int(r["baseline_final_present"] == "1"
                                          and r["evidence_class"] == "NONE")
        r["da_vs_base_disagreement"] = int(int(r["da_present_free"])
                                           != int(r["baseline_final_present"]))
        # human source + inferred presence for PME/SM without final labels
        r["human_final_source"] = "final_label" if r["human_present"] != "" else ""
        r["human_present_inferred"] = ""
        if r["human_present"] == "" and r["is_final_consonant"] == "1":
            v = r["human_verdict"]
            if v in CORRECT_VERDICTS:
                r["human_present_inferred"] = "1"
                r["human_final_source"] = "word_verdict_correct"
            elif v in ERROR_VERDICTS:
                r["human_present_inferred"] = "0"
                r["human_final_source"] = "word_verdict_error"

    def classify(r):
        if r["fidelity_state_v2"] in REFUSAL:
            return "ASSESSABILITY_FAILURE", "v2 refusal state"
        if r["is_final_consonant"] != "1":
            return "NON_FINAL_CONSONANT_CASE", "expected final phone is a vowel"
        hp = r["human_present"] if r["human_present"] != "" else r["human_present_inferred"]
        if hp == "":
            return "HUMAN_UNCERTAIN", "no final label / uncertain word verdict"
        bp = int(r["baseline_final_present"])
        ev = r["evidence_class"]
        soft = fnum(r["baseline_soft"]) or 0
        inferred = r["human_final_source"] != "final_label"
        tag = " (inferred from word verdict)" if inferred else ""
        if hp == "1" and bp == 1:
            if ev == "NONE":
                return "OK_UNSUPPORTED", "human present; accepted with no acoustic support" + tag
            return "OK", "human present, accepted"
        if hp == "0" and bp == 0:
            return "OK", "human absent, correctly rejected"
        if hp == "1" and bp == 0:
            if r["window_inversion"] == 1:
                return "ALIGNMENT", "present rejected; raw window accepts (window/alignment)" + tag
            if ev == "STRONG":
                return ("THRESHOLD", f"strong evidence, score near boundary ({soft})" + tag) \
                    if 40 <= soft < 50 else ("SCORING", "strong evidence but rejected" + tag)
            if ev == "WEAK":
                return "MIXED", "weak evidence; present rejected" + tag
            return "ACOUSTIC", "no encoder evidence for the present phone" + tag
        # absent accepted
        if ev == "NONE":
            return "DELETION", "absent phone accepted with no acoustic support" + tag
        return "MIXED", f"absent phone accepted with {ev.lower()} evidence" + tag

    failures = []
    for r in rows:
        r["classification"], r["classification_reason"] = classify(r)
        if r["classification"] in ("ACOUSTIC", "ALIGNMENT", "DELETION", "SCORING",
                                   "THRESHOLD", "MIXED", "OK_UNSUPPORTED",
                                   "ASSESSABILITY_FAILURE"):
            failures.append(r)

    # primary = final-consonant labels authoritative; decision failures only
    primary_fail = [r for r in failures if r["human_final_source"] == "final_label"
                    and r["classification"] != "OK_UNSUPPORTED"]
    primary = [r for r in failures if r["human_final_source"] == "final_label"]
    inferred = [r for r in failures if r["human_final_source"] != "final_label"]
    ok_unsupported = [r for r in rows if r["classification"] == "OK_UNSUPPORTED"]

    def dist(rs):
        c = Counter(r["classification"] for r in rs)
        n = len(rs)
        return {"n": n, "distribution": dict(c),
                "percentages": {k: round(100 * v / n, 1) for k, v in c.items()}}

    mechanisms = {
        "window_inversion_n": sum(r["window_inversion"] for r in rows),
        "window_inversion_ids": [r["case_id"] for r in rows if r["window_inversion"] == 1],
        "span_mismatch_n": sum(r["span_mismatch"] for r in rows),
        "span_mismatch_ids": [r["case_id"] for r in rows if r["span_mismatch"] == 1],
        "unsupported_acceptance_n": sum(r["unsupported_acceptance"] for r in rows),
        "unsupported_acceptance_ids": [r["case_id"] for r in rows
                                       if r["unsupported_acceptance"] == 1],
        "da_vs_base_disagreement_n": sum(r["da_vs_base_disagreement"] for r in rows),
        "greedy_has_final_but_base_miss_ids": [r["case_id"] for r in rows
                                               if r["greedy_has_final"] == "1"
                                               and r["baseline_final_present"] == "0"],
    }

    # rewrite FAILURE_CASE_ANALYSIS.csv (all classified rows with context)
    fields = list(rows[0].keys())
    with open(OUT / "FAILURE_CASE_ANALYSIS.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(rows)

    # evidence-floor counterfactuals on the labeled consonant finals (same logits/alignment)
    lab = [r for r in rows if r["human_final_source"] == "final_label"
           and r["is_final_consonant"] == "1"]
    n_present = sum(1 for r in lab if r["human_present"] == "1")
    n_absent = sum(1 for r in lab if r["human_present"] == "0")

    def sweep(field, thresholds):
        out = []
        for th in thresholds:
            tp = fn = fp = tn = 0
            for r in lab:
                pred = (fnum(r[field]) or 0) >= th
                hp = r["human_present"]
                if hp == "1":
                    tp += pred
                    fn += (not pred)
                else:
                    fp += pred
                    tn += (not pred)
            out.append({"threshold": round(float(th), 4),
                        "present_recall": round(tp / n_present, 4),
                        "absent_detection": round(tn / n_absent, 4),
                        "tp": tp, "fn": fn, "fp": fp, "tn": tn})
        return out

    span_sweep = sweep("span_max_post", [0.01, 0.02, 0.05, 0.08, 0.10, 0.15, 0.20, 0.30, 0.50])
    da_sweep = sweep("da_span_max_post", [0.01, 0.02, 0.05, 0.10, 0.15, 0.20, 0.30, 0.50, 0.80])

    def best_at_recall(sw, min_recall):
        ok = [s for s in sw if s["present_recall"] >= min_recall - 1e-9]
        return max(ok, key=lambda s: s["absent_detection"]) if ok else None

    evidence_floor = {
        "baseline": {"present_recall": round(sum(1 for r in lab if r["baseline_final_present"] == "1"
                                                 and r["human_present"] == "1") / n_present, 4),
                     "absent_detection": round(sum(1 for r in lab
                                                   if r["baseline_final_present"] == "0"
                                                   and r["human_present"] == "0") / n_absent, 4),
                     "n_present": n_present, "n_absent": n_absent},
        "span_post_floor_sweep": span_sweep,
        "da_span_post_floor_sweep": da_sweep,
        "best_span_floor_at_baseline_recall": best_at_recall(span_sweep, 0.9375),
        "best_da_floor_at_baseline_recall": best_at_recall(da_sweep, 0.9375),
        "interpretation": "a scoring-only posterior floor is viable if it keeps baseline recall "
                          "while raising absent detection",
    }

    results = json.loads((OUT / "EXPERIMENT_RESULTS.json").read_text(encoding="utf-8"))
    results["classification_v2"] = {
        "primary_final_labels": dist(primary),
        "primary_decision_failures_only": dist(primary_fail),
        "inferred_from_word_verdict": dist(inferred),
        "ok_unsupported_acceptance": dist(ok_unsupported),
        "ok_unsupported_ids": [r["case_id"] for r in ok_unsupported],
        "assessability_failures": [r["case_id"] for r in rows
                                   if r["classification"] == "ASSESSABILITY_FAILURE"],
        "human_uncertain_kept_separate": [r["case_id"] for r in rows
                                          if r["classification"] == "HUMAN_UNCERTAIN"],
    }
    results["alignment_mechanisms"] = mechanisms
    results["evidence_floor_counterfactual"] = evidence_floor
    results["root_cause_v2"] = {
        "primary_failure_distribution": dist(primary_fail)["distribution"],
        "primary_failure_percentages": dist(primary_fail)["percentages"],
        "by_phone_primary": dict(Counter(f"{r['final_phone']}:{r['classification']}"
                                         for r in primary_fail)),
        "note": "deletion+alignment mechanisms are process-level and reported separately "
                "from human-labeled failure classification",
    }
    results["flags"] = {"production_vad": False, "router_locked": False,
                        "unity_integrated": False, "scorer_modified": False,
                        "production_window_locked": False}
    (OUT / "EXPERIMENT_RESULTS.json").write_text(json.dumps(results, indent=2), encoding="utf-8")

    print(json.dumps({
        "primary": dist(primary), "inferred": dist(inferred),
        "ok_unsupported": dist(ok_unsupported),
        "mechanisms": {k: (v if not isinstance(v, list) else f"{len(v)}: {v[:8]}")
                       for k, v in mechanisms.items()},
    }, indent=2, ensure_ascii=True))


if __name__ == "__main__":
    main()
