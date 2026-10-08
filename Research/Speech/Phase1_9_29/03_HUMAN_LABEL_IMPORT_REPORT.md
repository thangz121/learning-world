# 03 - HUMAN LABEL IMPORT REPORT (WP-1.9.29 Part 3)

**NOT EXECUTED**

- **REASON:** No genuine reviewer exports exist (discovery: 0 reviewers, Pack R 0/276, Pack P 0/546).
  Importing anything would require fabricating labels, which is prohibited (Part 0).
- **REQUIRED GATE:** Genuine reviewer CSV/JSONL files traceable to the validated blind workflow, one
  file per reviewer ID.
- **CURRENT STATUS:** Import validator (`Phase1_9_28/experiments/import_review_exports.py`) is built
  and tested (synthetic malformed test: 3 valid / 4 rejected with reasons, deleted; dry-run status
  `NO_REVIEW_DATA`, 822 candidates, 0 covered). Raw-archive-first and no-silent-repair rules are
  implemented: raw records are kept in `cleaned/` only after validation; rejected rows go to
  `REVIEW_REJECTED_ROWS.csv` with an explicit reason.

## Import procedure when genuine labels arrive (frozen, unchanged)

```
python Research/Speech/Phase1_9_28/experiments/import_review_exports.py \
  --pack Research/Speech/Phase1_9_26/01_HUMAN_LABEL_ACQUISITION/REVIEW_CANDIDATES.csv \
  --pack Research/Speech/Phase1_9_27/06_PILOT_DATA/PILOT_REVIEW_CANDIDATES.csv \
  --csv REV-A=<exportA.csv> --csv REV-B=<exportB.csv> \
  --out Research/Speech/Phase1_9_29/artifacts/import
```

Retained per label: reviewer ID, pack, candidate ID, token ID, utterance ID, target phone,
label, assessability, confidence, timestamp, source file+row. Duplicates: raw copy kept in the
archive, duplicate excluded from analysis and recorded. Missing reviewer identity: rejected, never
inferred. Pack/token mismatch: rejected.

No row has been imported or repaired in this WP.
