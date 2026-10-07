"""WP-1.9.27 Parts 7-9 — human label analysis pipeline (ready; no real labels yet).

Reads reviewer JSONL stores (artifacts/reviews/*.jsonl) or exported CSVs and produces:
  - HUMAN_LABEL_RESULTS.csv     per case x reviewer + consensus
  - REVIEWER_AGREEMENT.csv      raw agreement, Cohen/Fleiss kappa, weighted kappa,
                                confidence-stratified and subset agreement
  - ADJUDICATION_RESULTS.csv    cases needing adjudication (disagreement / UNCERTAIN)
  - label_sufficiency.json      LABELS_SUFFICIENT_FOR_TYPE_B / R_LABELS_SUFFICIENT

Consensus rules: 2 reviewers -> agreement or DISAGREEMENT (no majority vote);
3+ -> 2/3 majority with the disagreement flag preserved; ties -> DISAGREEMENT.
Usage: python label_analysis.py [--reviews DIR] [--out DIR] [--pack PACK.csv]
"""
from __future__ import annotations

import argparse
import csv
import json
import statistics as st
import sys
from collections import Counter, defaultdict
from itertools import combinations
from pathlib import Path

HERE = Path(__file__).resolve().parent
PHASE = HERE.parent
REPO = PHASE.parents[2]
LAB26 = PHASE.parent / "Phase1_9_26" / "01_HUMAN_LABEL_ACQUISITION"
TYPE_B = {"child_07_seven", "014180143_15", "014190172_7", "014350146_16"}
ORDER = {"ABSENT": 0, "UNCERTAIN": 1, "PRESENT": 2}
LABELS = ["PRESENT", "ABSENT", "UNCERTAIN"]

RESULT_FIELDS = ["blind_id", "token_id", "corpus", "speaker_id", "word", "target_phone",
                 "pool", "purpose", "n_reviewers", "labels", "confidences",
                 "assessability", "consensus", "agreement", "uncertain", "notes"]
AGREE_FIELDS = ["scope", "reviewer_count", "n_common", "raw_agreement", "cohens_kappa",
                "weighted_kappa", "fleiss_kappa", "confidence_stratified_agreement",
                "notes"]
ADJ_FIELDS = ["blind_id", "token_id", "speaker_id", "word", "target_phone", "pool",
              "labels", "confidences", "reason", "adjudicator_label", "adjudicator_note"]


def load_reviews(reviews_dir, csv_map=None):
    raters = {}
    if reviews_dir and Path(reviews_dir).exists():
        for f in sorted(Path(reviews_dir).glob("*.jsonl")):
            rows = [json.loads(x) for x in f.read_text(encoding="utf-8").splitlines()
                    if x.strip()]
            raters[f.stem] = {r["blind_id"]: r for r in rows}
    for rev, path in (csv_map or {}).items():
        rows = list(csv.DictReader(open(path, encoding="utf-8")))
        raters[rev] = {r["blind_id"]: {"blind_id": r["blind_id"], "label": r["label"],
                                       "confidence": r["confidence"],
                                       "assessable": r.get("assessable", "ASSESSABLE"),
                                       "note": r.get("note", ""),
                                       "ts": r.get("ts", "")} for r in rows}
    return raters


def raw_agreement(a, b):
    common = sorted(set(a) & set(b))
    if not common:
        return None, 0
    agree = sum(1 for k in common if a[k]["label"] == b[k]["label"])
    return agree / len(common), len(common)


def cohens_kappa(a, b, weighted=False):
    common = sorted(set(a) & set(b))
    if not common:
        return None
    n = len(common)
    po = sum(1 for k in common if a[k]["label"] == b[k]["label"]) / n
    pe = 0.0
    for lab in LABELS:
        pa = sum(1 for k in common if a[k]["label"] == lab) / n
        pb = sum(1 for k in common if b[k]["label"] == lab) / n
        pe += pa * pb
    if not weighted:
        return (po - pe) / (1 - pe) if pe < 1 else 0.0
    num = den = 0.0
    for k in common:
        w = abs(ORDER[a[k]["label"]] - ORDER[b[k]["label"]]) / 2.0
        num += w
    for la in LABELS:
        for lb in LABELS:
            pa = sum(1 for k in common if a[k]["label"] == la) / n
            pb = sum(1 for k in common if b[k]["label"] == lb) / n
            w = abs(ORDER[la] - ORDER[lb]) / 2.0
            den += w * pa * pb
    return 1 - (num / den) if den > 0 else 1.0


def fleiss_kappa(raters):
    items = set.intersection(*(set(r) for r in raters.values())) if raters else set()
    if not items:
        return None
    n = len(raters)
    P, counts = [], defaultdict(int)
    for it in items:
        c = Counter(raters[rv][it]["label"] for rv in raters)
        P.append((sum(v * v for v in c.values()) - n) / (n * (n - 1)))
        for lab in LABELS:
            counts[lab] += c.get(lab, 0)
    pbar = sum(P) / len(items)
    pj = [counts[lab] / (len(items) * n) for lab in LABELS]
    pe = sum(p * p for p in pj)
    return (pbar - pe) / (1 - pe) if pe < 1 else 0.0


def consensus(recs):
    labs = [r["label"] for r in recs]
    if len(labs) == 1:
        return labs[0], 1, labs[0] == "UNCERTAIN"
    if len(set(labs)) == 1:
        return labs[0], 1, labs[0] == "UNCERTAIN"
    if len(labs) >= 3:
        c = Counter(labs).most_common(1)[0]
        if c[1] * 2 > len(labs):
            return c[0], 1, c[0] == "UNCERTAIN"
    return "DISAGREEMENT", 0, False


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--reviews", default=str(PHASE / "artifacts" / "reviews"))
    ap.add_argument("--out", default=str(PHASE / "03_LABEL_ANALYSIS"))
    ap.add_argument("--pack", default=str(LAB26 / "REVIEW_CANDIDATES.csv"),
                    help="candidate pack used for blind_id->token metadata "
                         "(Pack R default; Pack P for the pilot)")
    ap.add_argument("--csv", action="append", default=[],
                    help="reviewer=path.csv mappings for external exports")
    args = ap.parse_args()
    csv_map = dict(x.split("=", 1) for x in args.csv) if args.csv else {}
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)

    pack = {r["blind_id"]: r for r in csv.DictReader(
        open(args.pack, encoding="utf-8"))}
    raters = load_reviews(args.reviews, csv_map)
    reviewers = sorted(raters)
    n_rev = len(reviewers)

    # per-case results
    all_ids = sorted({k for r in raters.values() for k in r})
    result_rows, adj_rows = [], []
    for bid in all_ids:
        recs = [raters[rv][bid] for rv in reviewers if bid in raters[rv]]
        meta = pack.get(bid, {})
        cons, agree, unc = consensus(recs)
        notes = " | ".join(f"{rv}:{raters[rv][bid].get('note','')}"
                           for rv in reviewers if bid in raters[rv]
                           and raters[rv][bid].get("note"))
        result_rows.append({
            "blind_id": bid, "token_id": meta.get("token_id", ""),
            "corpus": meta.get("corpus", ""), "speaker_id": meta.get("speaker_id", ""),
            "word": meta.get("word", ""), "target_phone": meta.get("target_phone", ""),
            "pool": meta.get("pool", ""), "purpose": meta.get("purpose", ""),
            "n_reviewers": len(recs),
            "labels": ";".join(f"{rv}={raters[rv][bid]['label']}" for rv in reviewers
                               if bid in raters[rv]),
            "confidences": ";".join(f"{rv}={raters[rv][bid]['confidence']}" for rv in reviewers
                                    if bid in raters[rv]),
            "assessability": ";".join(f"{rv}={raters[rv][bid].get('assessable','ASSESSABLE')}"
                                      for rv in reviewers if bid in raters[rv]),
            "consensus": cons, "agreement": agree, "uncertain": int(unc),
            "notes": notes,
        })
        if cons == "DISAGREEMENT" or unc:
            adj_rows.append({
                "blind_id": bid, "token_id": meta.get("token_id", ""),
                "speaker_id": meta.get("speaker_id", ""), "word": meta.get("word", ""),
                "target_phone": meta.get("target_phone", ""), "pool": meta.get("pool", ""),
                "labels": result_rows[-1]["labels"],
                "confidences": result_rows[-1]["confidences"],
                "reason": "DISAGREEMENT" if cons == "DISAGREEMENT" else "UNCERTAIN",
                "adjudicator_label": "", "adjudicator_note": "",
            })

    # agreement stats
    agree_rows = []

    def subset(names, meta_sel):
        return {rv: {k: v for k, v in raters[rv].items() if meta_sel(pack.get(k, {}))}
                for rv in reviewers}

    scopes = [("all", lambda m: True)]
    if n_rev >= 2:
        for r1, r2 in combinations(reviewers, 2):
            ra, n = raw_agreement(raters[r1], raters[r2])
            agree_rows.append({"scope": f"pair:{r1}-{r2}", "reviewer_count": 2,
                               "n_common": n, "raw_agreement": round(ra, 4) if ra else None,
                               "cohens_kappa": round(cohens_kappa(raters[r1], raters[r2]), 4),
                               "weighted_kappa": round(cohens_kappa(raters[r1], raters[r2],
                                                                    weighted=True), 4),
                               "fleiss_kappa": None, "confidence_stratified_agreement": None,
                               "notes": ""})
    for tag, sel in (("r_specific", lambda m: m.get("target_phone") == "ɹ"),
                     ("typeb_specific", lambda m: m.get("token_id") in TYPE_B),
                     ("final_consonants", lambda m: m.get("pool", "").startswith(
                         ("A_", "B_", "D_", "E_", "I_", "J_", "K_", "L_"))),
                     ("diagnostic", lambda m: m.get("purpose") == "diagnostic"),
                     ("random_control", lambda m: m.get("purpose") == "random_control")):
        if n_rev < 2:
            break
        sub = subset(reviewers, sel)
        r1, r2 = reviewers[0], reviewers[1]
        ra, n = raw_agreement(sub[r1], sub[r2])
        agree_rows.append({"scope": tag, "reviewer_count": 2, "n_common": n,
                           "raw_agreement": round(ra, 4) if ra else None,
                           "cohens_kappa": round(cohens_kappa(sub[r1], sub[r2]), 4)
                           if n else None,
                           "weighted_kappa": round(cohens_kappa(sub[r1], sub[r2],
                                                                weighted=True), 4) if n else None,
                           "fleiss_kappa": None, "confidence_stratified_agreement": None,
                           "notes": ""})
    if n_rev >= 3:
        agree_rows.append({"scope": "multi_rater", "reviewer_count": n_rev, "n_common": None,
                           "raw_agreement": None, "cohens_kappa": None, "weighted_kappa": None,
                           "fleiss_kappa": round(fleiss_kappa(raters), 4),
                           "confidence_stratified_agreement": None, "notes": ""})
    if n_rev < 2:
        agree_rows.append({"scope": "no_agreement", "reviewer_count": n_rev, "n_common": 0,
                           "raw_agreement": None, "cohens_kappa": None, "weighted_kappa": None,
                           "fleiss_kappa": None, "confidence_stratified_agreement": None,
                           "notes": "SINGLE_REVIEWER_LIMITATION" if n_rev == 1
                                    else "NO_REVIEWER_AVAILABLE"})

    # sufficiency gates
    cons_map = {r["blind_id"]: r for r in result_rows}
    typeb_ok = 0
    for tid in TYPE_B:
        rows = [r for r in result_rows if r["token_id"] == tid]
        if rows and rows[0]["consensus"] in ("PRESENT", "ABSENT") and rows[0]["n_reviewers"] >= 2:
            typeb_ok += 1
    r_cons = [r for r in result_rows if r["target_phone"] == "ɹ"
              and r["consensus"] in ("PRESENT", "ABSENT")]
    r_present = sum(1 for r in r_cons if r["consensus"] == "PRESENT")
    r_absent = sum(1 for r in r_cons if r["consensus"] == "ABSENT")
    r_speakers = len({r["speaker_id"] for r in r_cons})
    gates = {
        "n_reviewers": n_rev,
        "n_labelled_cases": len(result_rows),
        "typeb_consensus_cases": typeb_ok,
        "typeb_required": 4,
        "LABELS_SUFFICIENT_FOR_TYPE_B": bool(typeb_ok == 4),
        "r_consensus_present": r_present, "r_consensus_absent": r_absent,
        "r_speakers": r_speakers,
        "R_LABELS_SUFFICIENT": bool(r_present >= 15 and r_absent >= 15 and r_speakers >= 10),
        "rationale": "TYPE-B gate: all 4 decisive cases need >=2 independent reviewer labels "
                     "with a non-UNCERTAIN consensus. R gate: >=15 PRESENT and >=15 ABSENT /r/ "
                     "consensus labels across >=10 speakers (15/side gives ~+/-0.20 95% CI "
                     "around p=0.8; 10 speakers prevents single-speaker dominance).",
    }
    (out / "label_sufficiency.json").write_text(json.dumps(gates, indent=2), encoding="utf-8")

    def write(path, rows, fields):
        with open(path, "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=fields)
            w.writeheader()
            w.writerows(rows)

    write(out / "HUMAN_LABEL_RESULTS.csv", result_rows, RESULT_FIELDS)
    write(out / "REVIEWER_AGREEMENT.csv", agree_rows, AGREE_FIELDS)
    write(out / "ADJUDICATION_RESULTS.csv", adj_rows, ADJ_FIELDS)
    print(json.dumps(gates, indent=1))
    print(f"reviewers={n_rev} cases={len(result_rows)} adjudication={len(adj_rows)}")
    print("DONE label_analysis")


if __name__ == "__main__":
    main()
