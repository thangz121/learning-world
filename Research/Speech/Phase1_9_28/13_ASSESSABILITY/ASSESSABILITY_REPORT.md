# ASSESSABILITY REPORT - WP-1.9.28 Part 20

> **Tóm tắt (VI):** Chưa có nhãn → chưa đánh giá. Gate assessability đã đóng băng: model không được
> ghi điểm khi đoán trên ca NOT_ASSESSABLE; nếu confidence tăng trên các ca này, tiêu chí thất bại
> đóng băng sẽ được áp dụng.

## Status

`NOT_EXECUTED` - `ASSESSABILITY_RESULTS.csv` schema-only.

## Frozen requirements

- Separate ASSESSABLE vs NOT_ASSESSABLE; never give credit for guessing on unintelligible audio.
- Report: assessability distribution, model behavior on NOT_ASSESSABLE, false-confidence rate.
- All primary metrics are re-run with NOT_ASSESSABLE excluded; a gain that disappears under this
  exclusion is not a valid gain (frozen FAILURE criterion 6).
- Reviewer NOT_ASSESSABLE labels are preserved as labels; they are not converted to ABSENT and not
  treated as QC exclusions.
