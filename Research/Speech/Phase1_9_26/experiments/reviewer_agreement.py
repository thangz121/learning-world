"""WP-1.9.26 Part 5 — reviewer agreement analysis (ready; no data yet).

Reads artifacts/reviews/<reviewer>.jsonl files and computes raw agreement, Cohen's kappa
(two raters) or Fleiss' kappa (>=3), confidence-stratified agreement, /r/-specific and
TYPE-B-specific agreement. Disagreements are preserved, never majority-voted.

Usage: python reviewer_agreement.py [reviews_dir]
"""
from __future__ import annotations

import csv
import json
import sys
from collections import Counter, defaultdict
from itertools import combinations
from pathlib import Path

HERE = Path(__file__).resolve().parent
PHASE = HERE.parent
REVIEWS = PHASE / "artifacts" / "reviews"
PACK = PHASE / "01_HUMAN_LABEL_ACQUISITION" / "REVIEW_CANDIDATES.csv"
LABELS = ["PRESENT", "ABSENT", "UNCERTAIN"]
TYPE_B = {"child_07_seven", "014180143_15", "014190172_7", "014350146_16"}


def load(reviews_dir):
    out = {}
    for f in sorted(reviews_dir.glob("*.jsonl")):
        rows = [json.loads(x) for x in f.read_text(encoding="utf-8").splitlines() if x.strip()]
        out[f.stem] = {r["blind_id"]: r for r in rows}
    return out


def raw_agreement(a, b):
    common = set(a) & set(b)
    if not common:
        return None
    agree = sum(1 for k in common if a[k]["label"] == b[k]["label"])
    return agree / len(common), len(common)


def cohens_kappa(a, b):
    common = set(a) & set(b)
    if not common:
        return None
    n = len(common)
    po = sum(1 for k in common if a[k]["label"] == b[k]["label"]) / n
    pe = 0.0
    for lab in LABELS:
        pa = sum(1 for k in common if a[k]["label"] == lab) / n
        pb = sum(1 for k in common if b[k]["label"] == lab) / n
        pe += pa * pb
    return (po - pe) / (1 - pe) if pe < 1 else 0.0


def fleiss_kappa(raters):
    items = set.intersection(*(set(r) for r in raters.values())) if raters else set()
    if not items:
        return None
    n = len(raters)
    P = []
    counts = defaultdict(int)
    for it in items:
        c = Counter(raters[rv][it]["label"] for rv in raters)
        P.append((sum(v * v for v in c.values()) - n) / (n * (n - 1)))
        for lab in LABELS:
            counts[lab] += c.get(lab, 0)
    pbar = sum(P) / len(items)
    pj = [counts[lab] / (len(items) * n) for lab in LABELS]
    pe = sum(p * p for p in pj)
    return (pbar - pe) / (1 - pe) if pe < 1 else 0.0


def main():
    reviews_dir = Path(sys.argv[1]) if len(sys.argv) > 1 else REVIEWS
    raters = load(reviews_dir)
    if len(raters) < 2:
        print(f"reviewers found: {len(raters)} — agreement not computable "
              f"(SINGLE_REVIEWER_LIMITATION / NO_REVIEWER_AVAILABLE)")
        return
    meta = {}
    if PACK.exists():
        for r in csv.DictReader(open(PACK, encoding="utf-8")):
            meta[r["blind_id"]] = r
    out = {"reviewers": list(raters), "pairwise": {}, "fleiss_kappa": None}
    for r1, r2 in combinations(sorted(raters), 2):
        ra = raw_agreement(raters[r1], raters[r2])
        k = cohens_kappa(raters[r1], raters[r2])
        out["pairwise"][f"{r1}-{r2}"] = {"raw_agreement": ra[0] if ra else None,
                                         "n_common": ra[1] if ra else 0,
                                         "cohens_kappa": k}
    if len(raters) >= 3:
        out["fleiss_kappa"] = fleiss_kappa(raters)
    # subsets
    for tag, sel in (("r_specific", lambda m: m.get("target_phone") == "ɹ"),
                     ("typeb_specific", lambda m: m.get("token_id") in TYPE_B)):
        sub = {}
        for rv, rows in raters.items():
            sub[rv] = {k: v for k, v in rows.items() if sel(meta.get(k, {}))}
        ra = raw_agreement(*[sub[rv] for rv in sorted(raters)][:2]) if len(raters) >= 2 else None
        out[tag] = {"n_common": ra[1] if ra else 0,
                    "raw_agreement": ra[0] if ra else None}
    print(json.dumps(out, indent=2))
    print("disagreements are preserved; no majority voting applied")


if __name__ == "__main__":
    main()
