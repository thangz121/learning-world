# 04 - AGREEMENT (WP-1.9.30 Part 10) — UPDATED AFTER SESSION 1

> **Tóm tắt (VI):** Chỉ có 1 reviewer thật (REV-A) → không tính được inter-rater. Pipeline đã chạy
> và ghi đúng `SINGLE_REVIEWER_LIMITATION`. Không tạo kappa giả.

## Status

| item | value |
|---|---|
| genuine reviewers | 1 (REV-A) |
| labelled candidates | 276 (all Pack R) |
| raw agreement / Cohen / weighted / Fleiss | NOT ESTIMABLE |
| agreement row written | `no_agreement,1,0,,...,SINGLE_REVIEWER_LIMITATION` |
| adjudication candidates | 16 (UNCERTAIN cases, awaiting second reviewer then adjudication) |

Outputs: `artifacts/agreement/` (HUMAN_LABEL_RESULTS.csv 276 rows, REVIEWER_AGREEMENT.csv,
ADJUDICATION_RESULTS.csv 16 rows, label_sufficiency.json).

## Gate effects (single reviewer)

- TYPE-B gate: **not met** — requires >=2 independent reviewers; REV-A alone cannot make the 4
  decisive cases count, even at HIGH confidence.
- /r/ gate: PRESENT side met (22 vs 15), ABSENT side not (1 vs 15).
- No consensus/majority claims are made from one reviewer; UNCERTAIN preserved (16).

## Next

REV-B reviews Pack R (same 276) independently; then agreement (Cohen's kappa, weighted kappa,
per-subset) runs on the two genuine reviewers with the frozen implementation.
