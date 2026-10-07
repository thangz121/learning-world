# FAILURE CRITERIA (FROZEN) — WP-1.9.27 Part 17

> **Tóm tắt (VI):** Định nghĩa thất bại rõ trước kết quả: FRR tăng >0.05; không giảm
> missing-evidence; FAR tăng >0.05; lợi ích chỉ trên train hoặc 1 speaker; lợi ích biến mất khi
> kiểm soát assessability; control ngẫu nhiên ngang bằng; gain chỉ 1 phone. Không biện luận
> quanh co sau khi thấy kết quả.

The pilot is a FAILURE for a variant if any of the following holds on the frozen test split:

1. FRR on labelled PRESENT increases by more than 0.05 absolute vs the production baseline.
2. No reduction in the missing-evidence rate on labelled PRESENT (relative reduction <= 0).
3. FAR on labelled ABSENT increases by more than 0.05 absolute.
4. The improvement exists only on train/dev speakers and disappears on test speakers.
5. The improvement is driven by a single test speaker (the other regresses or ties).
6. The improvement disappears when NOT_ASSESSABLE tokens are excluded (assessability gate).
7. For fitted variants: the random-feature control performs equivalently (no evidence the features
   matter).
8. The improvement is confined to one phone class with n<10 and no mechanism.
9. The improvement requires tuning on the test split (any test-tuned threshold = invalid result).

## Consequences of FAILURE

- Do not pursue full B2 training on this evidence; report the negative result.
- Do not purchase MyST or start a collection on the strength of a failed pilot.
- The label round and the acceptance/UX-layer work continue independently of the pilot outcome.

## Notes

- A failed pilot is a successful experiment: it saves a 10–30 h data spend.
- "Partial" behavior (direction correct but below threshold) is PARTIAL_SUCCESS, not failure; it
  authorizes only a repeat with more labels, not training.
