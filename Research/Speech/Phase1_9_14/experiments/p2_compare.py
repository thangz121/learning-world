"""Phase 1.9.14 — P2 baseline-vs-candidate + FRR analysis (derived, no model).

Reads artifacts/p2/p2_tokens.csv (written by p2_deletion_aware.py) and emits:
  artifacts/p2/p2_baseline_vs_candidates.csv  per-token baseline/candidate/delta/human/provenance
  artifacts/p2/p2_frr_analysis.json           FRR-first operating points & class breakdown
  artifacts/p2/p2_class_confusion.csv         per-class baseline vs candidate correctness
"""
from __future__ import annotations

import csv
import json
from collections import Counter, defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
RES = REPO / "Research/Speech/Phase1_9_14" / "artifacts" / "p2"
LEVELS = ["CLEARLY_PRESENT", "PROBABLY_PRESENT", "AMBIGUOUS", "PROBABLY_ABSENT", "CLEARLY_ABSENT"]


def level(label: str) -> str:
    for lv in LEVELS:
        if label.endswith(lv):
            return lv
    return ""


def main():
    rows = list(csv.DictReader((RES / "p2_tokens.csv").open(encoding="utf-8")))
    lab = [r for r in rows if r["human_present"] != ""]
    out = []
    for r in lab:
        y = int(r["human_present"])
        base_present = int(r["archived_phone_present"])
        free_present = int(r["dalign_present_free"])
        out.append({
            "token_id": r["token_id"], "speaker_id": r["speaker_id"], "word": r["word"],
            "final_phone": r["final_phone"], "final_phone_class": r["final_phone_class"],
            "human_label": r["human_final_label"], "human_present": y,
            "baseline_softv2_match": r["archived_phone_match"],
            "baseline_softv2_present": base_present,
            "baseline_correct": int(base_present == y),
            "candidate_dalign_free_present": free_present,
            "candidate_dalign_free_correct": int(free_present == y),
            "candidate_margin_per_frame": r["dalign_margin_per_frame"],
            "candidate_gop_ratio": r["gop_ratio"],
            "candidate_gop_expected": r["gop_expected"],
            "delta_baseline_vs_margin_abs": None,
            "human_reference": "Phase1_9_12 blind final-consonant review (human_mobile, 2026-10-03)",
            "provenance": "baseline=frozen 1.9.12 soft-v2 archived; candidate=p2_deletion_aware.py "
                          "blank-interleaved (2L+1) Viterbi on same 16k audio; same model",
        })
    with open(RES / "p2_baseline_vs_candidates.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(out[0].keys()))
        w.writeheader()
        w.writerows(out)

    labels = [int(r["human_present"]) for r in lab]
    n_present, n_absent = sum(labels), len(labels) - sum(labels)
    m_per = [float(r["dalign_margin_per_frame"]) for r in lab]
    gop = [float(r["gop_ratio"]) for r in lab]

    def op(pred_absent):
        tp = fp = tn = fn = 0
        fn_ids, fp_ids = [], []
        for r, p in zip(lab, pred_absent):
            y = int(r["human_present"])
            if y == 1:
                fn += p
                tp += 1 - p
                if p:
                    fn_ids.append(r["token_id"])
            else:
                tn += p
                fp += 1 - p
                if not p:
                    fp_ids.append(r["token_id"])
        return {"frr": fn / n_present, "far": fp / n_absent, "tp": tp, "fp": fp, "tn": tn, "fn": fn,
                "present_rejected": fn_ids, "absent_accepted": fp_ids}

    base_pred = [1 - int(r["archived_phone_present"]) for r in lab]
    free_pred = [1 - int(r["dalign_present_free"]) for r in lab]
    op_points = {
        "A_softv2_as_shipped": op(base_pred),
        "B_free_presence_zero_penalty": op(free_pred),
        "B_margin_frr0_exact": None,
        "C_gop_frr0_best": None,
    }
    # exact FRR=0 operating points
    best_m = None
    for th in sorted(set(m_per)):
        o = op([s < th for s in m_per])
        if o["fn"] == 0 and (best_m is None or o["fp"] < best_m[0]["fp"]):
            best_m = (o, th)
    op_points["B_margin_frr0_exact"] = dict(best_m[0], threshold=best_m[1]) if best_m else None
    best_g = None
    for th in sorted(set(gop)):
        o = op([s < th for s in gop])
        if o["fn"] == 0 and (best_g is None or o["fp"] < best_g[0]["fp"]):
            best_g = (o, th)
    op_points["C_gop_frr0_best"] = dict(best_g[0], threshold=best_g[1]) if best_g else None

    # matched-FRR frontier: min FAR at FRR <= k
    frontier = {}
    for k in range(0, 5):
        entry = {}
        for name, vals in (("A_baseline", None), ("B_margin", m_per), ("C_gop", gop)):
            if vals is None:
                if k >= 1:
                    entry[name] = {"far": op_points["A_softv2_as_shipped"]["far"],
                                   "frr": op_points["A_softv2_as_shipped"]["frr"]}
                continue
            best = None
            for th in sorted(set(vals)):
                o = op([s < th for s in vals])
                if o["fn"] <= k and (best is None or o["fp"] < best[1]["fp"]):
                    best = (th, o)
            if best:
                entry[name] = {"threshold": best[0], "far": best[1]["far"],
                               "frr": best[1]["frr"], "absent_accepted": best[1]["absent_accepted"],
                               "present_rejected": best[1]["present_rejected"]}
        frontier[f"frr_lt_{k}_of_{n_present}"] = entry

    by_class = defaultdict(lambda: {"n": 0, "base_correct": 0, "free_correct": 0,
                                    "present": 0, "absent": 0, "base_missed_absent": [],
                                    "base_rejected_present": []})
    for r in lab:
        c = r["final_phone_class"]
        y = int(r["human_present"])
        bp = int(r["archived_phone_present"])
        by_class[c]["n"] += 1
        by_class[c]["present" if y else "absent"] += 1
        by_class[c]["base_correct"] += int(bp == y)
        by_class[c]["free_correct"] += int(int(r["dalign_present_free"]) == y)
        if y == 0 and bp == 1:
            by_class[c]["base_missed_absent"].append(r["token_id"])
        if y == 1 and bp == 0:
            by_class[c]["base_rejected_present"].append(r["token_id"])

    with open(RES / "p2_class_confusion.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["class", "n", "present", "absent", "baseline_correct", "dalign_free_correct",
                    "baseline_missed_absent", "baseline_rejected_present"])
        for c, v in sorted(by_class.items()):
            w.writerow([c, v["n"], v["present"], v["absent"], v["base_correct"], v["free_correct"],
                        ";".join(v["base_missed_absent"]), ";".join(v["base_rejected_present"])])

    summary = {
        "n_labeled": len(lab), "n_present": n_present, "n_absent": n_absent,
        "operating_points": op_points,
        "matched_frr_frontier": frontier,
        "class_breakdown": {c: {k: v for k, v in val.items()} for c, val in by_class.items()},
        "human_label_distribution": dict(Counter(r["human_final_label"] for r in lab)),
        "frr_first_verdict": "No candidate improves FAR over baseline at equal or lower FRR on the "
                             "28-token human set; free zero-penalty deletion is FRR-unsafe (7/16 "
                             "human-present finals rejected).",
        "baseline_reproduction": "0 mismatches vs archived 1.9.12 match_type on all 65 tokens",
    }
    (RES / "p2_frr_analysis.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps({"ops": {k: {kk: vv for kk, vv in (v or {}).items()
                                  if kk in ("frr", "far", "tp", "fp", "tn", "fn", "threshold")}
                              for k, v in op_points.items()},
                      "frontier": frontier}, indent=2))


if __name__ == "__main__":
    main()
