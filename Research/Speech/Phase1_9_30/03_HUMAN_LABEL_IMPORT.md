# 03 - HUMAN LABEL IMPORT (WP-1.9.30 Part 8) — UPDATED AFTER SESSION 1

> **Tóm tắt (VI):** Đã import thật phiên REV-A: 276 valid / 0 rejected / 0 duplicate; cleaned CSV
> sinh ra cho pipeline; raw được hash và giữ nguyên (không sửa). Pack P chưa có nhãn.

## Import ledger (session 1)

| metric | count |
|---|---:|
| total raw rows | 276 |
| valid (imported) | **276** |
| rejected | 0 |
| duplicates | 0 |
| QA / TEST / synthetic | 0 |
| schema mismatch | 0 |
| unknown reviewer / candidate / token | 0 |
| final usable labels | **276** (all Pack R) |

## Artifacts

- Source (authoritative): `Phase1_9_27/artifacts/reviews/REV-A.jsonl`
  (sha256 `4D702308A86641BEE6B3F8BF84080B7F244C4E3D14E46D4D4AB8D5B345E5BB11`).
- Export snapshot (auto-delivered): `.../reviews/exports/REV-A_20261008T053356Z.csv`.
- Root copy provided by user: `export.csv` (same content).
- Cleaned import: `artifacts/import/cleaned/REV-A.csv`; coverage `artifacts/import/REVIEW_COVERAGE.csv`;
  provenance `artifacts/review_raw/RAW_PROVENANCE.json`.
- Consensus table: `artifacts/agreement/HUMAN_LABEL_RESULTS.csv` (276 rows; single-reviewer consensus
  = the REV-A label; 16 UNCERTAIN cases flagged for adjudication).

## Rules applied

Raw data was not modified or normalized in place; validation only. No label was repaired, inferred,
or score-derived. NOT_ASSESSABLE was kept in the assessability field (3 cases), never converted to
ABSENT.
