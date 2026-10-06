"""WP-1.9.19 — window stability, causal classification, separation analysis.

Reads artifacts/window_rows_{lwe,so762_dev,so762_test}.csv and writes:
  WINDOW_EXPERIMENT_RESULTS.csv    token x window rows (+ flags + root cause)
  WINDOW_STABILITY.csv             per token stability
  FINAL_CONSONANT_WINDOW_ANALYSIS.csv  per phone aggregates
  EXPERIMENT_RESULTS.json          machine-readable complete results
"""
from __future__ import annotations

import csv
import json
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np

REPO = Path(__file__).resolve().parents[4]
OUT = REPO / "Research/Speech/Phase1_9_19"
ART = OUT / "artifacts"
FLOOR = 0.30
E_LOW = 0.10


def load(name):
    p = ART / name
    if not p.exists():
        return []
    return list(csv.DictReader(p.open(encoding="utf-8")))


def fnum(v, d=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return d


def auc(pos, neg):
    if not pos or not neg:
        return None
    wins = sum(1.0 if a > b else (0.5 if a == b else 0.0) for a in pos for b in neg)
    return wins / (len(pos) * len(neg))


def evaluate_at_floor(rows, truth_key):
    tp = fn = fp = tn = 0
    for r in rows:
        if r.get(truth_key, "") == "":
            continue
        pred = fnum(r["deletion_aware_evidence"]) >= FLOOR
        y = int(r[truth_key])
        if y == 1:
            tp += pred
            fn += (not pred)
        else:
            fp += pred
            tn += (not pred)
    n_p = tp + fn
    n_a = fp + tn
    return {"n_present": n_p, "n_absent": n_a,
            "present_recall": round(tp / n_p, 4) if n_p else None,
            "absent_false_present": round(fp / n_a, 4) if n_a else None,
            "absent_detection": round(tn / n_a, 4) if n_a else None}


def stability(rows):
    """rows: all windows of one token."""
    ev = [fnum(r["deletion_aware_evidence"]) for r in rows]
    dec = [1 if r["baseline_match"] in ("exact", "soft") else 0 for r in rows]
    pos = [fnum(r["target_time_abs"]) for r in rows]
    raw_hp = rows[0].get("human_present", "")
    hp = int(raw_hp) if raw_hp != "" else ""
    out = {
        "n_windows": len(rows),
        "min_evidence": round(min(ev), 4), "max_evidence": round(max(ev), 4),
        "median_evidence": round(float(np.median(ev)), 4),
        "evidence_range": round(max(ev) - min(ev), 4),
        "decision_flip_count": int(sum(1 for i in range(1, len(dec)) if dec[i] != dec[i - 1])),
        "alignment_flip_count": int(sum(1 for i in range(1, len(pos))
                                        if abs(pos[i] - pos[i - 1]) > 0.10)),
        "present_absent_separation": None,
    }
    recovered = max(ev) >= FLOOR
    low_all = max(ev) < E_LOW
    if hp == 1:
        if low_all:
            cls = "evidence-absent"
        elif all(e >= FLOOR for e in ev):
            cls = "stable-present"
        elif recovered and min(ev) < E_LOW:
            cls = "boundary-sensitive"
        elif recovered:
            cls = "mixed"
        else:
            cls = "boundary-sensitive" if max(ev) - min(ev) > 0.2 else "mixed"
    elif hp == 0:
        if max(ev) >= FLOOR:
            cls = "absent-false-gain"
        elif max(ev) - min(ev) > 0.2:
            cls = "boundary-sensitive"
        else:
            cls = "stable-absent"
    else:
        cls = "unlabeled"
    out["stable_class"] = cls
    out["window_sensitive"] = int(out["evidence_range"] >= 0.30 or out["decision_flip_count"] >= 2)
    out["alignment_sensitive"] = int(out["alignment_flip_count"] >= 3 and max(ev) >= FLOOR)
    return out


def causal_class(rowset, stab):
    """Per-token causal label from window evidence + alignment behaviour."""
    ev = {r["window_type"]: fnum(r["deletion_aware_evidence"]) for r in rowset}
    raw_hp = rowset[0].get("human_present", "")
    hp = int(raw_hp) if raw_hp != "" else ""
    full = ev.get("full", 0.0)
    raw = ev.get("raw", 0.0)
    max_ev = max(ev.values())
    if hp == "":
        return "UNKNOWN"
    full_row = next((r for r in rowset if r["window_type"] == "full"), None)
    if hp == 1:
        # alignment: evidence present in the FULL context but the production span misses it
        if full_row and fnum(full_row["deletion_aware_evidence"]) >= FLOOR \
                and fnum(full_row["span_post"]) < 0.05:
            return "ALIGNMENT"
        if max_ev < E_LOW:
            return "ENCODER"
        if (full < E_LOW or raw < E_LOW) and max_ev >= FLOOR:
            return "WINDOW"
        if max_ev >= FLOOR:
            return "MIXED"
        return "MIXED" if max_ev >= 0.2 else "ENCODER"
    # human absent
    if full >= FLOOR:
        return "ABSENT_CONFLICT"       # strong evidence at full despite human ABSENT
    if max_ev >= FLOOR:
        return "ABSENT_FALSE_GAIN"     # context creates evidence on an absent phone
    return "STABLE_ABSENT"


def main():
    lwe = load("window_rows_lwe.csv")
    dev = load("window_rows_so762_dev.csv")
    test = load("window_rows_so762_test.csv")
    adev = load("window_rows_so762_absent_dev.csv")

    # ---- per-token stability ----
    by_token = defaultdict(list)
    for r in lwe:
        by_token[("lwe", r["token_id"])].append(r)
    for r in dev:
        by_token[("so762_dev", r["token_id"])].append(r)
    for r in test:
        by_token[("so762_test", r["token_id"])].append(r)
    for r in adev:
        by_token[("so762_absent_dev", r["token_id"])].append(r)

    stab_rows = []
    causal = {}
    for (corpus, tid), rs in sorted(by_token.items()):
        st = stability(rs)
        cc = causal_class(rs, st)
        causal[(corpus, tid)] = cc
        st2 = {"corpus": corpus, "token_id": tid, "target_phone": rs[0]["target_phone"],
               "human_present": rs[0].get("human_present", ""), **st, "root_cause": cc}
        stab_rows.append(st2)
    with open(OUT / "WINDOW_STABILITY.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(stab_rows[0].keys()), extrasaction="ignore")
        w.writeheader()
        w.writerows(stab_rows)

    # ---- token x window export ----
    exp_rows = []
    for corpus, rs in (("lwe", lwe), ("so762_dev", dev), ("so762_test", test),
                       ("so762_absent_dev", adev)):
        for r in rs:
            hp = r.get("human_present", "")
            exp_rows.append({
                "corpus": corpus, "token_id": r["token_id"], "speaker_id": r["speaker_id"],
                "word": r.get("word", ""), "target_phone": r["target_phone"],
                "human_label": r.get("human_label", r.get("human_phone_score", "")),
                "window_type": r["window_type"],
                "left_context_ms": r["left_context_ms"], "right_context_ms": r["right_context_ms"],
                "audio_start": r["audio_start"], "audio_end": r["audio_end"],
                "duration_ms": r["duration_ms"], "target_frame": r["target_frame"],
                "span_start": r["span_start"], "span_end": r["span_end"],
                "target_posterior": r["target_posterior"],
                "top1_phone": r["top1_phone"], "top1_posterior": r["top1_posterior"],
                "margin": r["margin"],
                "deletion_aware_evidence": r["deletion_aware_evidence"],
                "baseline_decision": "PRESENT" if r["baseline_match"] in ("exact", "soft") else "ABSENT",
                "research_decision": r["research_decision"],
                "boundary_sensitive": causal[(corpus, r["token_id"])] == "WINDOW",
                "alignment_sensitive": causal[(corpus, r["token_id"])] == "ALIGNMENT",
                "root_cause": causal[(corpus, r["token_id"])],
            })
    with open(OUT / "WINDOW_EXPERIMENT_RESULTS.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(exp_rows[0].keys()))
        w.writeheader()
        w.writerows(exp_rows)

    # ---- LWE window-family separation (28 labels) ----
    lwe_lab = [r for r in lwe if r.get("human_present", "") != ""]
    per_window = {}
    for cond in sorted({r["window_type"] for r in lwe_lab}):
        rs = [r for r in lwe_lab if r["window_type"] == cond]
        pos = [fnum(r["deletion_aware_evidence"]) for r in rs if int(r["human_present"]) == 1]
        neg = [fnum(r["deletion_aware_evidence"]) for r in rs if int(r["human_present"]) == 0]
        m = evaluate_at_floor(rs, "human_present")
        per_window[cond] = {"n": len(rs), "auc": round(auc(pos, neg), 4) if auc(pos, neg) else None,
                            **m}

    # ---- speaker-disjoint window selection on dev (absent-enriched) ----
    dev_full = [r for r in adev if r.get("human_present", "") != ""] or \
        [r for r in dev if r.get("human_present", "") != ""]
    dev_by_window = {}
    for cond in sorted({r["window_type"] for r in dev_full}):
        rs = [r for r in dev_full if r["window_type"] == cond]
        pos = [fnum(r["deletion_aware_evidence"]) for r in rs if int(r["human_present"]) == 1]
        neg = [fnum(r["deletion_aware_evidence"]) for r in rs if int(r["human_present"]) == 0]
        a = auc(pos, neg)
        dev_by_window[cond] = {"n": len(rs), "auc": round(a, 4) if a else None,
                               **evaluate_at_floor(rs, "human_present")}
    sel_cond = max(dev_by_window, key=lambda k: (dev_by_window[k]["auc"] or 0)) if dev_by_window else "full"
    def window_block(rows, cond):
        rs = [r for r in rows if r.get("human_present", "") != "" and r["window_type"] == cond]
        pos = [fnum(r["deletion_aware_evidence"]) for r in rs if int(r["human_present"]) == 1]
        neg = [fnum(r["deletion_aware_evidence"]) for r in rs if int(r["human_present"]) == 0]
        a = auc(pos, neg)
        return {"n": len(rs), "auc": round(a, 4) if a else None, **evaluate_at_floor(rs, "human_present")}
    selection = {"selected_on_dev_by_auc": sel_cond,
                 "dev_all_windows": dev_by_window,
                 "test_selected": window_block(test, sel_cond),
                 "test_full": window_block(test, "full"),
                 "test_raw": window_block(test, "raw"),
                 "lwe_selected": window_block(lwe, sel_cond),
                 "lwe_full": window_block(lwe, "full"),
                 "lwe_raw": window_block(lwe, "raw")}

    # ---- negative check: absent false-gain ----
    def gain_stats(rows):
        byt = defaultdict(list)
        for r in rows:
            byt[r["token_id"]].append(r)
        present_gain = absent_gain = 0
        present_n = absent_n = 0
        deltas_p, deltas_a = [], []
        for tid, rs in byt.items():
            hp = rs[0].get("human_present", "")
            if hp == "":
                continue
            ev = [fnum(r["deletion_aware_evidence"]) for r in rs]
            raw = next((fnum(r["deletion_aware_evidence"]) for r in rs if r["window_type"] == "raw"), min(ev))
            delta = max(ev) - raw
            if int(hp) == 1:
                present_n += 1
                deltas_p.append(delta)
                present_gain += int(max(ev) >= FLOOR and raw < E_LOW)
            else:
                absent_n += 1
                deltas_a.append(delta)
                absent_gain += int(max(ev) >= FLOOR and raw < E_LOW)
        return {"present_n": present_n, "absent_n": absent_n,
                "present_recovered_by_context": present_gain,
                "absent_falsely_gained_by_context": absent_gain,
                "present_delta_mean": round(float(np.mean(deltas_p)), 3) if deltas_p else None,
                "absent_delta_mean": round(float(np.mean(deltas_a)), 3) if deltas_a else None}
    negative_check = {"lwe": gain_stats(lwe), "so762_test": gain_stats(test),
                      "so762_absent_dev": gain_stats(adev)}

    # ---- final-consonant aggregation per phone (LWE labels) ----
    phone_rows = defaultdict(lambda: {"n_present": 0, "n_absent": 0, "rec_full": 0,
                                      "rec_best": 0, "abd_full": 0, "abd_best": 0,
                                      "var": [], "bnd": 0, "gap": 0, "n_tokens": 0})
    for tid in sorted({r["token_id"] for r in lwe if r.get("human_present", "") != ""}):
        rs = [r for r in lwe if r["token_id"] == tid]
        hp = int(rs[0]["human_present"])
        ph = rs[0]["target_phone"]
        ev = {r["window_type"]: fnum(r["deletion_aware_evidence"]) for r in rs}
        full = ev.get("full", 0.0)
        best = max(ev.values())
        d = phone_rows[ph]
        d["n_tokens"] += 1
        d["var"].append(max(ev.values()) - min(ev.values()))
        if hp == 1:
            d["n_present"] += 1
            d["rec_full"] += int(full >= FLOOR)
            d["rec_best"] += int(best >= FLOOR)
        else:
            d["n_absent"] += 1
            d["abd_full"] += int(full < FLOOR)
            d["abd_best"] += int(best < FLOOR)
        if causal[("lwe", tid)] == "WINDOW":
            d["bnd"] += 1
        if causal[("lwe", tid)] == "ENCODER":
            d["gap"] += 1
        if causal[("lwe", tid)] == "ALIGNMENT":
            d.setdefault("align", 0)
            d["align"] += 1
    agg_rows = []
    for ph, d in sorted(phone_rows.items()):
        agg_rows.append({
            "phone": ph, "n_tokens": d["n_tokens"],
            "n_present": d["n_present"], "n_absent": d["n_absent"],
            "present_recall_full": round(d["rec_full"] / d["n_present"], 3) if d["n_present"] else None,
            "present_recall_best_window": round(d["rec_best"] / d["n_present"], 3) if d["n_present"] else None,
            "absent_detection_full": round(d["abd_full"] / d["n_absent"], 3) if d["n_absent"] else None,
            "absent_detection_best_window": round(d["abd_best"] / d["n_absent"], 3) if d["n_absent"] else None,
            "evidence_variance": round(float(np.mean(d["var"])), 4) if d["var"] else None,
            "boundary_sensitive_rate": round(d["bnd"] / d["n_tokens"], 3) if d["n_tokens"] else None,
            "encoder_gap_rate": round(d["gap"] / d["n_tokens"], 3) if d["n_tokens"] else None,
        })
    with open(OUT / "FINAL_CONSONANT_WINDOW_ANALYSIS.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(agg_rows[0].keys()))
        w.writeheader()
        w.writerows(agg_rows)

    # ---- root cause distribution (LWE labeled) ----
    causes = Counter(causal[("lwe", tid)] for tid in
                     {r["token_id"] for r in lwe if r.get("human_present", "") != ""})
    cause_all = Counter(v for (c, t), v in causal.items() if c == "lwe")

    results = {
        "phase": "1.9.19",
        "research_question": "Are final-consonant evidence failures encoder-caused or "
                             "window/boundary/context-caused?",
        "windows": {"lwe": sorted({r["window_type"] for r in lwe}),
                    "so762": sorted({r["window_type"] for r in dev})},
        "counts": {"lwe_rows": len(lwe), "lwe_tokens": len({r["token_id"] for r in lwe}),
                   "so762_dev_tokens": len({r["token_id"] for r in dev}),
                   "so762_test_tokens": len({r["token_id"] for r in test})},
        "lwe_window_separation": per_window,
        "window_selection": selection,
        "negative_check": negative_check,
        "root_cause_lwe_labeled": dict(causes),
        "root_cause_lwe_all": dict(cause_all),
        "final_consonant_aggregation": agg_rows,
        "flags": {"production_vad": False, "router_locked": False, "unity_integrated": False,
                  "scorer_modified": False, "production_window_locked": False},
    }
    (OUT / "EXPERIMENT_RESULTS.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(json.dumps({"counts": results["counts"],
                      "lwe_window_separation": per_window,
                      "window_selection": {"selected": sel_cond,
                                           "dev": dev_by_window,
                                           "test_selected": selection["test_selected"],
                                           "lwe_selected": selection["lwe_selected"]},
                      "negative_check": negative_check,
                      "root_cause_lwe_labeled": dict(causes)}, indent=2))


if __name__ == "__main__":
    main()
