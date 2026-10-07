# AGREEMENT REPORT - WP-1.9.28 Part 7

> **Tóm tắt (VI):** Chưa có reviewer thật nên không tính được agreement/kappa. Dry-run xác nhận
> pipeline trả `no_agreement / NO_REVIEWER_AVAILABLE`. Khi có >=2 reviewer, pipeline tính đủ raw
> agreement, Cohen/weighted/Fleiss, theo confidence, /r/, TYPE-B, phụ âm cuối, diagnostic vs
> control; bất đồng được giữ nguyên, phân loại và chuyển adjudication.

## Current status

| item | value |
|---|---|
| reviewers | 0 |
| cases with >=2 reviews | 0 |
| raw agreement | not computable |
| Cohen kappa / weighted kappa | not computable |
| Fleiss kappa (>=3) | not computable |
| confidence-stratified agreement | not computable |
| /r/ agreement | not computable |
| TYPE-B agreement | not computable |
| final-consonant agreement | not computable |
| diagnostic vs control agreement | not computable |

Dry-run output (`REVIEWER_AGREEMENT.csv`): `no_agreement,0,0,,,..,NO_REVIEWER_AVAILABLE`.

## Prepared pipeline (frozen; tested PASS with synthetic data, deleted)

Command when >=2 reviewer exports exist:

```
python Research/Speech/Phase1_9_27/experiments/label_analysis.py \
  --pack Research/Speech/Phase1_9_28/02_REVIEW_IMPORT/PACK_COMBINED.csv \
  --csv REV-A=.../cleaned/REV-A.csv --csv REV-B=.../cleaned/REV-B.csv \
  --out Research/Speech/Phase1_9_28/03_AGREEMENT
```

Outputs: `HUMAN_LABEL_RESULTS.csv` (consensus per case), `REVIEWER_AGREEMENT.csv` (all scopes),
`ADJUDICATION_RESULTS.csv` (DISAGREEMENT/UNCERTAIN cases), `label_sufficiency.json`.

## Disagreement handling

- 2 reviewers: agreement -> that label; disagreement -> `DISAGREEMENT` (no majority vote).
- >=3 reviewers: 2/3 majority with the disagreement flag preserved; ties -> `DISAGREEMENT`.
- UNCERTAIN is preserved, never collapsed into ABSENT.
- `DISAGREEMENT_CASES.csv` (this directory) is the human-readable registry of disagreement cases;
  `ADJUDICATION_RESULTS.csv` is the pipeline-generated version with adjudicator columns.
- Adjudication happens only after raw labels are frozen; the third listener never sees raw labels.

## Disagreement classification (applied at adjudication time)

acoustic ambiguity / reviewer uncertainty / assessability / phonetic interpretation difference /
target ambiguity / recording problem. Classifications are recorded per case; none exist yet.

## Reviewer isolation (Part 6)

- System-level isolation: validated (QA checks 8, 9, 18; fresh reviewer sees nothing; blind payload
  contains no machine prediction).
- Result-level isolation: **NOT APPLICABLE** (no reviewer files exist). When results arrive:
  verify separate JSONL/CSV per reviewer ID and timestamps; if isolation cannot be established,
  mark the affected results `REVIEW_INTEGRITY_COMPROMISED` and exclude from independent evidence.
