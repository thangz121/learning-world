# FAILURE REPLAY REPORT - WP-1.9.28 Part 22

> **Tóm tắt (VI):** Chưa chạy replay (thiếu nhãn). Bộ failure corpus lịch sử được giữ nguyên định
> nghĩa từ 1.9.22/1.9.23; khi có nhãn, mỗi ca được phân fixed / partially improved / unchanged /
> regressed / newly introduced, kèm phân loại nguyên nhân.

## Status

`NOT_EXECUTED` - `FAILURE_REPLAY.csv` schema-only.

## Failure corpus (frozen reference, not replaced by newly selected cases)

- TYPE-B 4 cases (encoder-vs-label ambiguity).
- Weak true-present failures: child_06_six, child_07_one, child_01_nine (WP-1.9.22 list).
- Presentation side cases: child_07_one, child_01_seven, child_02_ten, child_04_four, child_01_ten,
  child_01_four, child_03_four, child_02_four (WP-1.9.22 Part 4 list).
- False accepts from WP-1.9.22/23 (14 total; 7 IDENTITY_WEAK, 4 IDENTITY_STRONG, 3 SOFT_SIMILARITY).
- Isolated-peak and encoder-disagreement cases (Pack R pools G/H).

## Planned classification per case

fixed / partially improved / unchanged / regressed / newly introduced, plus mechanism:
PHONE_MODEL_ERROR / SCORER_ERROR / ALIGNMENT / WINDOW / ASSESSABILITY / LABEL_AMBIGUITY / OTHER.

## Constraint

The historical corpus is not re-selected or pruned to flatter a variant; every case is reported with
its raw label and confidence.
