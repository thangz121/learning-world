# REVIEW IMPORT REPORT - WP-1.9.28 Part 5

> **Tóm tắt (VI):** Validator import đã được xây và kiểm thử: chấp nhận đúng schema, loại bỏ
> unknown label/confidence/ID, thiếu trường, trùng reviewer+candidate — không sửa nhãn. Dry-run
> hiện tại: 0 reviewer, 822 ứng viên, 0 covered, status NO_REVIEW_DATA. Không có nhãn nào được tạo.

## Tool

`experiments/import_review_exports.py`

```
python import_review_exports.py \
  --pack Phase1_9_26/.../REVIEW_CANDIDATES.csv \
  --pack Phase1_9_27/06_PILOT_DATA/PILOT_REVIEW_CANDIDATES.csv \
  --csv REV-A=revA.csv --csv REV-B=revB.csv \
  --out 02_REVIEW_IMPORT
```

Outputs: `cleaned/<reviewer>.csv` (consumable by `label_analysis.py --csv`),
`REVIEW_REJECTED_ROWS.csv`, `REVIEW_COVERAGE.csv`, `PACK_COMBINED.csv` (822 blind_ids -> metadata),
`import_summary.json`.

## Validation rules (frozen schema)

- Required: `blind_id`, `label`, `confidence`; `assessable` defaults to ASSESSABLE; `note`/`ts`
  optional.
- Allowed labels: PRESENT / ABSENT / UNCERTAIN / NOT_ASSESSABLE.
- Allowed confidence: HIGH / MEDIUM / LOW. Allowed assessability: ASSESSABLE / NOT_ASSESSABLE.
- `blind_id` must exist in Pack R or Pack P; duplicates `(reviewer, blind_id)` are rejected.
- No silent repair: every invalid row goes to `REVIEW_REJECTED_ROWS.csv` with a reason.

## Verification evidence

1. **Synthetic malformed test** (in `%TEMP%`, deleted after): 7 rows -> 3 valid, 4 rejected with
   reasons `UNKNOWN_LABEL`, `UNKNOWN_BLIND_ID`, `MISSING_REQUIRED_FIELD`,
   `DUPLICATE_REVIEWER_CANDIDATE`. Cleaned file contained exactly the 3 valid rows.
2. **Dry-run with no reviewer files** (this run): status `NO_REVIEW_DATA`;
   candidates_total 822; candidates_covered 0; records_valid 0; records_rejected 0.
   `PACK_COMBINED.csv` written with 822 rows; `cleaned/` empty.
3. **REVIEW_COVERAGE.csv**: 19 groups (Pack R 15 pools + Pack P 3 splits) all zero coverage.
4. `label_analysis.py` dry-run (Part 7/9): `no_agreement NO_REVIEWER_AVAILABLE`; gates false.

## Current state

- Genuine human records imported: **0**.
- Rejected rows: 0 (no input files).
- No labels fabricated; no score-derived label promoted.

## Exact command when real exports arrive

```
# 1. validate + clean + combine
python Research/Speech/Phase1_9_28/experiments/import_review_exports.py \
  --pack Research/Speech/Phase1_9_26/01_HUMAN_LABEL_ACQUISITION/REVIEW_CANDIDATES.csv \
  --pack Research/Speech/Phase1_9_27/06_PILOT_DATA/PILOT_REVIEW_CANDIDATES.csv \
  --csv REV-A=<revA.csv> --csv REV-B=<revB.csv> \
  --out Research/Speech/Phase1_9_28/02_REVIEW_IMPORT

# 2. agreement + consensus + sufficiency over BOTH packs
python Research/Speech/Phase1_9_27/experiments/label_analysis.py \
  --pack Research/Speech/Phase1_9_28/02_REVIEW_IMPORT/PACK_COMBINED.csv \
  --csv REV-A=Research/Speech/Phase1_9_28/02_REVIEW_IMPORT/cleaned/REV-A.csv \
  --csv REV-B=Research/Speech/Phase1_9_28/02_REVIEW_IMPORT/cleaned/REV-B.csv \
  --out Research/Speech/Phase1_9_28/03_AGREEMENT
```
