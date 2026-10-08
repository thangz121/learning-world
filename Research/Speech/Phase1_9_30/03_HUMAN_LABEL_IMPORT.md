# 03 - HUMAN LABEL IMPORT (WP-1.9.30 Part 8)

**NOT EXECUTED**

- **REASON:** 0 genuine reviewer submissions (discovery: `01_REVIEW_DISCOVERY.md`). Importing would
  require fabricated or schema-mismatched data, both prohibited.
- **REQUIRED GATE:** genuine reviewer exports through the validated blind workflow, one file per
  reviewer ID (schema `blind_id,label,confidence,assessable,note,ts`).
- **CURRENT STATUS:** frozen import validator ready (`Phase1_9_28/experiments/import_review_exports.py`;
  previously validated: synthetic malformed test 3 valid / 4 rejected with explicit reasons, deleted;
  dry-run `NO_REVIEW_DATA`, 822 candidates, 0 covered).

## Import ledger (current)

| metric | count |
|---|---:|
| total raw rows | 0 |
| valid | 0 |
| rejected | 0 |
| duplicates | 0 |
| QA / TEST | 0 |
| schema mismatch | 0 for Pack R/P (1 historical StageA excluded, not imported) |
| unknown reviewer / candidate / token | 0 |
| final usable labels | **0** |

Raw-data preservation rule (ready): the genuine raw export is hashed and archived before
normalization; invalid rows are rejected with reasons, never repaired.
