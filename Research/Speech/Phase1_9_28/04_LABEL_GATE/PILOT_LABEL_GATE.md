# PILOT LABEL GATE - WP-1.9.28 Part 9

> **Tóm tắt (VI):** Pack P có 546 token, nhưng hiện 0 nhãn. Gate pilot: tối thiểu 60 PRESENT +
> 60 ABSENT trên bộ pilot; nhãn hợp lệ cho test split 99 token (2 speaker) là bắt buộc trước khi
> đánh giá; fitting (B2-E) chỉ chạy nếu >=60P+60A ở train+dev. Không dùng nhãn train làm nhãn test.

## Current status

| item | value |
|---|---|
| Pack P candidates | 546 (train 337 / dev 110 / test 99) |
| candidates covered | 0 |
| PRESENT / ABSENT / UNCERTAIN / NOT_ASSESSABLE | 0 / 0 / 0 / 0 |
| test-split coverage | 0 / 99 |
| minimum 60P+60A | not met |

Source: `02_REVIEW_IMPORT/REVIEW_COVERAGE.csv` rows for `PILOT_REVIEW_CANDIDATES`.

## Requirements (frozen)

1. **Evaluation prerequisite**: valid HUMAN-LISTENING labels for the frozen test split
   (speakers 1075, 1076; 99 tokens). Evaluation runs on test only for the primary readout.
2. **Sufficiency**: >=60 PRESENT + >=60 ABSENT among the 546 pilot tokens, with >=2 reviewers or an
   explicit single-reviewer limitation (which caps the outcome at INCONCLUSIVE for decisive claims).
3. **Fitting (B2-E only)**: fit only if >=60P+60A in train+dev; otherwise run threshold-only and
   mark INCONCLUSIVE. Test labels must never be used for fitting or threshold selection.
4. **/r/ pilot subset**: 8 /r/ tokens; directional reporting only (not proof).

## Reviewer priority (exact candidate list)

`14_DECISION/NEXT_REVIEW_BATCH.csv` (822 rows): pilot test split = P0; dev = P1; train = P2.
Pack R priorities are in the same file (TYPE-B and /r/ first).

## When the gate clears

Run the frozen evaluation exactly once per config (`PILOT_PREREGISTRATION.md`), then apply
`09_SUCCESS_CRITERIA/` mechanically. Until then: no baseline, no B2-D, no B2-E, no /r/ claims.
