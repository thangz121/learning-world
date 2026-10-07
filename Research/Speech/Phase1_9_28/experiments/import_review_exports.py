"""WP-1.9.28 Part 5 — review-export import validator (research-only).

Validates reviewer exports (CSV) against the frozen review schema and the frozen
candidate packs (Pack R + Pack P), without ever repairing or inventing a label.

Usage:
  python import_review_exports.py \
    --pack <PackR.csv> --pack <PackP.csv> \
    --csv REV-A=revA.csv [--csv REV-B=revB.csv] \
    --out <02_REVIEW_IMPORT dir>

Outputs:
  <out>/cleaned/<reviewer>.csv      cleaned records (consumable by label_analysis.py --csv)
  <out>/REVIEW_REJECTED_ROWS.csv    rejected rows with reason (never silently repaired)
  <out>/REVIEW_COVERAGE.csv         candidate coverage per pack x group
  <out>/PACK_COMBINED.csv           blind_id -> metadata across both packs (for label_analysis --pack)
  <out>/import_summary.json         machine summary incl. NO_REVIEW_DATA classification

Status classification (Part 2):
  NO_REVIEW_DATA                     no reviewer file supplied
  REVIEW_DATA_PRESENT_INVALID        reviewer file(s) supplied but zero valid records
  PARTIAL_REVIEW_DATA                some candidates covered
  COMPLETE_REVIEW_DATA               every candidate covered by >=1 valid record
"""
from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path

LABELS = {"PRESENT", "ABSENT", "UNCERTAIN", "NOT_ASSESSABLE"}
CONFIDENCE = {"HIGH", "MEDIUM", "LOW"}
ASSESSABLE = {"ASSESSABLE", "NOT_ASSESSABLE"}
CLEAN_FIELDS = ["pack", "reviewer_id", "blind_id", "token_id", "label", "confidence",
                "assessable", "note", "ts"]
REJECT_FIELDS = ["file", "csv_row", "reviewer_id", "blind_id", "label", "confidence",
                 "assessable", "reason"]
COVERAGE_FIELDS = ["pack", "group", "candidates", "covered_unique", "records", "PRESENT",
                   "ABSENT", "UNCERTAIN", "NOT_ASSESSABLE", "reviewers"]
PACK_FIELDS = ["blind_id", "token_id", "pack", "corpus", "speaker_id", "group", "word",
               "target_phone"]


def load_packs(paths):
    meta, order = {}, []
    for p in paths:
        for r in csv.DictReader(open(p, encoding="utf-8-sig")):
            bid = r["blind_id"]
            if bid in meta:
                raise SystemExit(f"FATAL: duplicate blind_id across packs: {bid}")
            m = {"blind_id": bid, "token_id": r.get("token_id", ""),
                 "pack": Path(p).stem, "corpus": r.get("corpus", ""),
                 "speaker_id": r.get("speaker_id", ""),
                 "group": r.get("pool") or r.get("split", ""),
                 "word": r.get("word", ""), "target_phone": r.get("target_phone", "")}
            meta[bid] = m
            order.append(m)
    return meta, order


def validate_row(rec, pack_meta, seen, csv_row, fname, reviewer):
    bid = rec.get("blind_id", "")
    label = rec.get("label", "")
    conf = rec.get("confidence", "")
    assess = rec.get("assessable", "") or "ASSESSABLE"
    if not bid or not label or not conf:
        return None, "MISSING_REQUIRED_FIELD"
    if label not in LABELS:
        return None, "UNKNOWN_LABEL"
    if conf not in CONFIDENCE:
        return None, "UNKNOWN_CONFIDENCE"
    if assess not in ASSESSABLE:
        return None, "UNKNOWN_ASSESSABLE"
    if bid not in pack_meta:
        return None, "UNKNOWN_BLIND_ID"
    key = (reviewer, bid)
    if key in seen:
        return None, "DUPLICATE_REVIEWER_CANDIDATE"
    seen.add(key)
    meta = pack_meta[bid]
    clean = {"pack": meta["pack"], "reviewer_id": reviewer, "blind_id": bid,
             "token_id": meta["token_id"], "label": label, "confidence": conf,
             "assessable": assess, "note": rec.get("note", ""), "ts": rec.get("ts", "")}
    return clean, ""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--pack", action="append", required=True)
    ap.add_argument("--csv", action="append", default=[],
                    help="reviewer=path.csv (repeatable)")
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    (out / "cleaned").mkdir(exist_ok=True)
    pack_meta, order = load_packs(args.pack)

    with open(out / "PACK_COMBINED.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=PACK_FIELDS)
        w.writeheader()
        w.writerows(order)

    reviewers, valid_rows, rejected = [], [], []
    for mapping in args.csv:
        if "=" not in mapping:
            raise SystemExit(f"FATAL: bad --csv mapping: {mapping}")
        reviewer, path = mapping.split("=", 1)
        reviewers.append(reviewer)
        seen = set()
        for i, r in enumerate(csv.DictReader(open(path, encoding="utf-8-sig")), start=2):
            rec = {k: (v or "").strip() for k, v in r.items()}
            clean, reason = validate_row(rec, pack_meta, seen, i, path, reviewer)
            if clean:
                valid_rows.append(clean)
            else:
                rejected.append({"file": path, "csv_row": i, "reviewer_id": reviewer,
                                 "blind_id": rec.get("blind_id", ""),
                                 "label": rec.get("label", ""),
                                 "confidence": rec.get("confidence", ""),
                                 "assessable": rec.get("assessable", ""),
                                 "reason": reason})

    receive = {}
    for row in valid_rows:
        receive.setdefault((row["reviewer_id"], row["pack"]), []).append(row)
    for rev in dict.fromkeys(reviewers):
        rows = [r for r in valid_rows if r["reviewer_id"] == rev]
        with open(out / "cleaned" / f"{rev}.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=CLEAN_FIELDS)
            w.writeheader()
            w.writerows(rows)

    with open(out / "REVIEW_REJECTED_ROWS.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=REJECT_FIELDS)
        w.writeheader()
        w.writerows(rejected)

    covered = {r["blind_id"] for r in valid_rows}
    groups = {}
    for m in order:
        groups.setdefault((m["pack"], m["group"]), []).append(m)
    coverage = []
    for (pack, group), members in sorted(groups.items()):
        ids = {m["blind_id"] for m in members}
        recs = [r for r in valid_rows if r["blind_id"] in ids]
        coverage.append({
            "pack": pack, "group": group, "candidates": len(ids),
            "covered_unique": len(ids & covered), "records": len(recs),
            "PRESENT": sum(1 for r in recs if r["label"] == "PRESENT"),
            "ABSENT": sum(1 for r in recs if r["label"] == "ABSENT"),
            "UNCERTAIN": sum(1 for r in recs if r["label"] == "UNCERTAIN"),
            "NOT_ASSESSABLE": sum(1 for r in recs if r["label"] == "NOT_ASSESSABLE"),
            "reviewers": len({r["reviewer_id"] for r in recs}),
        })
    with open(out / "REVIEW_COVERAGE.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=COVERAGE_FIELDS)
        w.writeheader()
        w.writerows(coverage)

    n_candidates = len(order)
    if not args.csv:
        status = "NO_REVIEW_DATA"
    elif valid_rows and len(covered) >= n_candidates:
        status = "COMPLETE_REVIEW_DATA"
    elif valid_rows:
        status = "PARTIAL_REVIEW_DATA"
    else:
        status = "REVIEW_DATA_PRESENT_INVALID"
    summary = {
        "status": status, "packs": [str(p) for p in args.pack],
        "candidates_total": n_candidates, "candidates_covered": len(covered),
        "reviewers": reviewers, "records_valid": len(valid_rows),
        "records_rejected": len(rejected),
        "rejected_reasons": {r: sum(1 for x in rejected if x["reason"] == r)
                             for r in sorted({x["reason"] for x in rejected})},
        "label_counts": {lab: sum(1 for r in valid_rows if r["label"] == lab)
                         for lab in sorted(LABELS)},
    }
    (out / "import_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=1))
    print("DONE import_review_exports")


if __name__ == "__main__":
    main()
