# REVIEWER AGREEMENT PROTOCOL — WP-1.9.27 Parts 7–8

> **Tóm tắt (VI):** Sau khi có >=2 reviewer: tính raw agreement, Cohen kappa (2 người), weighted
> kappa, Fleiss (>=3), theo confidence, theo /r/, TYPE-B, phụ âm cuối, diagnostic vs random
> control. Một reviewer: ghi SINGLE_REVIEWER_LIMITATION, không tính kappa giả.

## Inputs

- Reviewer JSONL stores (`artifacts/reviews/*.jsonl`) or exported CSVs imported with
  `label_analysis.py --csv reviewer=path.csv`.
- The candidate pack used for metadata: Pack R default (`--pack`), Pack P for the pilot.

## Computation (implemented in `experiments/label_analysis.py`)

| statistic | rule |
|---|---|
| consensus (2 reviewers) | agreement -> that label; else DISAGREEMENT (no majority vote) |
| consensus (>=3) | 2/3 majority kept, disagreement flag preserved; ties -> DISAGREEMENT |
| raw agreement | fraction of common cases with equal labels |
| Cohen's kappa | 2 raters, 3 classes |
| weighted kappa | ordinal order ABSENT=0 < UNCERTAIN=1 < PRESENT=2 |
| Fleiss' kappa | >=3 raters on common cases |
| confidence-stratified agreement | agreement within HIGH / MEDIUM / LOW meta-groups |
| /r/ subset | target_phone = ɹ |
| TYPE-B subset | the 4 decisive token_ids |
| final-consonant subset | pools A/B/D/E/I/J/K/L |
| diagnostic vs random control | purpose field |

Command (after both exports are delivered):

```
python label_analysis.py --reviews artifacts/reviews \
  --csv REV-A=revA_packR.csv --csv REV-B=revB_packR.csv
```

## Rules

- Preserve disagreements; never majority-vote systematic disagreement away.
- UNCERTAIN cases are reported separately; do not collapse them into ABSENT.
- With one reviewer: report `SINGLE_REVIEWER_LIMITATION`; do not compute inter-rater statistics.
- With two reviewers, the LWE-style historical single review remains a limitation to report.
