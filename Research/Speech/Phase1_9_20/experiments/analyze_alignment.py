"""WP-1.9.20 — variant selection on dev, held-out evaluation, reclassification, outputs."""
from __future__ import annotations

import csv
import json
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
OUT = REPO / "Research/Speech/Phase1_9_20"
ART = OUT / "artifacts"
FLOOR = 0.30


def fnum(v, d=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return d


def span_pair(s):
    if not s:
        return None
    a, b = s.split("-")
    return int(a), int(b)


def main():
    rows = list(csv.DictReader((ART / "alignment_rows.csv").open(encoding="utf-8")))
    variants = sorted({c[:-4] for c in rows[0] if c.endswith("_max") and c != "old_span_max"})
    dev = [r for r in rows if r["corpus"] == "so762_dev"]
    test = [r for r in rows if r["corpus"] == "so762_test"]
    adev = [r for r in rows if r["corpus"] == "so762_absent_dev"]
    lwe = [r for r in rows if r["corpus"] == "lwe"]

    def is_present(r):
        if r["corpus"] == "lwe":
            return 1 if r["human_label"].endswith("PRESENT") else (
                0 if r["human_label"].endswith("ABSENT") else "")
        return 1 if fnum(r["human_score"], -1) >= 0.5 else 0

    dev_alignable = [r for r in dev if is_present(r) == 1 and fnum(r["old_span_max"]) < 0.1
                     and fnum(r["evidence_outside_old"]) >= 0.3]
    dev_abs = [r for r in dev if is_present(r) == 0] + [r for r in adev if is_present(r) == 0]

    selection = {}
    for v in variants:
        rec = [r for r in dev_alignable if fnum(r[f"{v}_max"]) >= FLOOR]
        false_rec = [r for r in dev_abs if fnum(r[f"{v}_max"]) >= FLOOR]
        selection[v] = {
            "recovery": round(len(rec) / len(dev_alignable), 4) if dev_alignable else None,
            "n_alignable": len(dev_alignable), "n_recovered": len(rec),
            "false_recovery_rate": round(len(false_rec) / len(dev_abs), 4) if dev_abs else None,
            "n_absent": len(dev_abs), "n_false_recovered": len(false_rec),
        }
    best = max((v for v in variants if selection[v]["n_recovered"] > 0),
               key=lambda v: (selection[v]["recovery"] - 0.5 * selection[v]["false_recovery_rate"]),
               default="A_current")

    def decisions(rs, v):
        tp = fn = fp = tn = unc = 0
        for r in rs:
            y = is_present(r)
            if y == "":
                continue
            d = r.get(f"{v}_decision", "ABSENT")
            if d == "UNCERTAIN":
                unc += 1
                continue
            pr = d == "PRESENT"
            if y == 1:
                tp += pr
                fn += (not pr)
            else:
                fp += pr
                tn += (not pr)
        return {"present_recall": round(tp / (tp + fn), 4) if (tp + fn) else None,
                "absent_false_present": round(fp / (fp + tn), 4) if (fp + tn) else None,
                "unsupported_present": None, "uncertain_rate": round(unc / len(rs), 4) if rs else None,
                "coverage": round((tp + fn + fp + tn) / len(rs), 4) if rs else None}

    lwe_lab = [r for r in lwe if is_present(r) != ""]
    # old (production) decision from 1.9.19 baseline: reconstruct via old span max? use archived match
    wrows = {r["token_id"]: r for r in csv.DictReader(
        (REPO / "Research/Speech/Phase1_9_19/artifacts/window_rows_lwe.csv").open(encoding="utf-8"))
        if r["window_type"] == "full"}
    old = {"present_recall": None, "absent_false_present": None}
    tp = fn = fp = tn = 0
    for r in lwe_lab:
        w = wrows.get(r["token_id"])
        if not w:
            continue
        pr = 1 if w["baseline_match"] in ("exact", "soft") else 0
        y = is_present(r)
        if y == 1:
            tp += pr
            fn += (1 - pr)
        else:
            fp += pr
            tn += (1 - pr)
    old = {"present_recall": round(tp / (tp + fn), 4) if (tp + fn) else None,
           "absent_false_present": round(fp / (fp + tn), 4) if (fp + tn) else None}

    results = {
        "phase": "1.9.20",
        "recompute_note": "no frame-level logits cached in 1.9.17-19; frozen encoder re-run for the "
                          "exact 1.9.19 evaluation sets only (80 LWE + 214 so762 utt)",
        "variants": {
            "A_current": "production ctc_align",
            "B_blank": "2L+1 blank-aware, no skips",
            "C_skip_d*": "blank-aware + deletion skips, penalty delta (normalized emissions)",
            "D_evid_l*": "C + lambda * GOP(target vs competitor)",
            "E_cons": "C d=1 + conservative support rule (0.5 present)",
        },
        "selection_on_dev": selection,
        "selected_variant": best,
        "lwe_old_production": old,
        "lwe_new_selected": decisions(lwe_lab, best),
        "lwe_new_A_current": decisions(lwe_lab, "A_current"),
        "so762_test_selected": decisions(test, best),
        "so762_test_current": decisions(test, "A_current"),
    }

    # per-case export for LWE (required CSV)
    exp = []
    for r in lwe:
        old_sp = span_pair(r["old_span"])
        new_sp = span_pair(r.get(f"{best}_span", ""))
        y = is_present(r)
        recovered = int(fnum(r["old_span_max"]) < 0.1 and fnum(r["evidence_outside_old"]) >= 0.3
                        and fnum(r[f"{best}_max"]) >= FLOOR)
        false_rec = int(y == 0 and fnum(r[f"{best}_max"]) >= FLOOR)
        exp.append({
            "case_id": r["token_id"], "speaker_id": r["speaker_id"], "word": r["word"],
            "target_phone": r["target_phone"], "human_label": r["human_label"],
            "old_alignment_start": old_sp[0] if old_sp else "", "old_alignment_end": old_sp[1] if old_sp else "",
            "old_span_ms": r["old_span_ms"], "old_span_posterior": r["old_span_max"],
            "old_evidence": r["old_span_max"],
            "new_alignment_type": best,
            "new_span_start": new_sp[0] if new_sp else "", "new_span_end": new_sp[1] if new_sp else "",
            "new_span_ms": r.get(f"{best}_frames", ""), "new_evidence": r.get(f"{best}_max", ""),
            "new_margin": "", "blank_fraction": "", "displacement_ms": r.get("displacement_ms", ""),
            "deletion_status": "DELETED" if new_sp is None else "PRESENT_PATH",
            "old_decision": "PRESENT" if wrows.get(r["token_id"], {}).get("baseline_match")
            in ("exact", "soft") else "ABSENT",
            "new_decision": r.get(f"{best}_decision", ""),
            "alignment_recovered": recovered, "false_recovery": false_rec,
            "root_cause": "",
            "notes": "blank_fraction/new_margin not computed (documented limitation)",
        })
    with open(OUT / "ALIGNMENT_CASE_ANALYSIS.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(exp[0].keys()))
        w.writeheader()
        w.writerows(exp)

    # reclassification of the 9 ALIGNMENT cases from 1.9.19
    p19 = {r["token_id"]: r for r in csv.DictReader(
        (REPO / "Research/Speech/Phase1_9_19/WINDOW_STABILITY.csv").open(encoding="utf-8"))}
    align_ids = [tid for tid, r in p19.items()
                 if r["corpus"] == "lwe" and r["root_cause"] == "ALIGNMENT"]
    recl = []
    for r in lwe:
        if r["token_id"] not in align_ids:
            continue
        outside = fnum(r["evidence_outside_old"]) >= 0.3
        newm = fnum(r.get(f"{best}_max"))
        fixed = int(outside and newm >= FLOOR)
        partial = int(outside and 0.15 <= newm < FLOOR)
        new_class = ("FIXED_ALIGNMENT" if fixed else
                     "PARTIALLY_FIXED" if partial else
                     "NOT_ALIGNMENT" if not outside else "INCONCLUSIVE")
        recl.append({
            "case_id": r["token_id"], "old_class": "ALIGNMENT", "new_class": new_class,
            "fixed": fixed, "partial": partial, "regressed": 0,
            "evidence": f"old_span_max={r['old_span_max']} outside={r['evidence_outside_old']} "
                        f"new_max={newm} displacement_ms={r.get('displacement_ms','')}",
            "reason": "evidence existed within monotone region outside production span"
                      if outside else "no evidence outside the production span",
        })
    with open(OUT / "ALIGNMENT_FAILURE_RECLASSIFICATION.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(recl[0].keys()))
        w.writeheader()
        w.writerows(recl)

    # negative control: absent tokens
    absent = [r for r in lwe_lab if is_present(r) == 0] + dev_abs
    neg = {
        "n_absent": len(absent),
        "old_false": sum(1 for r in absent if fnum(r["old_span_max"]) >= FLOOR),
        "new_false": sum(1 for r in absent if fnum(r[f"{best}_max"]) >= FLOOR),
    }
    results["negative_control"] = neg
    results["reclassification"] = {
        "n_alignment_cases": len(recl),
        "FIXED_ALIGNMENT": sum(1 for r in recl if r["new_class"] == "FIXED_ALIGNMENT"),
        "PARTIALLY_FIXED": sum(1 for r in recl if r["new_class"] == "PARTIALLY_FIXED"),
        "NOT_ALIGNMENT": sum(1 for r in recl if r["new_class"] == "NOT_ALIGNMENT"),
        "rows": recl,
    }
    results["flags"] = {"production_vad": False, "router_locked": False,
                        "unity_integrated": False, "scorer_modified": False,
                        "production_window_locked": False}
    (OUT / "ALIGNMENT_VARIANT_RESULTS.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(json.dumps({k: results[k] for k in
                      ("selected_variant", "lwe_old_production", "lwe_new_selected",
                       "lwe_new_A_current", "negative_control", "reclassification")},
                     indent=2)[:4000])


if __name__ == "__main__":
    main()
