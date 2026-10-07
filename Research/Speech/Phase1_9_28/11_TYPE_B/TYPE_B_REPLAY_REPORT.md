# TYPE-B REPLAY REPORT - WP-1.9.28 Part 21

> **Tóm tắt (VI):** Replay 4 ca TYPE-B KHÔNG chạy: không có nhãn nghe mới. `TYPE_B_REPLAY.csv` đã
> điền sẵn bằng chứng máy + trường nhãn để `pending`; trạng thái `NOT_EXECUTED_NO_HUMAN_LABELS`.

## Status

`NOT_EXECUTED_NO_HUMAN_LABELS` - wait for Pack R pool F review (P0 priority).

## Cases (evidence prefilled in TYPE_B_REPLAY.csv)

| case | word/phone | production max_A | alt max_A | delta | isolated | human label now |
|---|---|---:|---:|---:|---|---|
| child_07_seven | seven / n | 0.629 | 0.940 | +0.311 | yes | PROBABLY_ABSENT HIGH (historical, single reviewer) |
| 014180143_15 | june / n | 0.682 | 0.248 | -0.434 | no | score-0 only |
| 014190172_7 | name / m | 0.820 | 0.409 | -0.411 | yes | score-0 only |
| 014350146_16 | education / n | 0.956 | 0.910 | -0.046 | no | score-0 only |

## Allowed classifications (imported from the frozen TYPE-B design)

ENCODER_FALSE_EVIDENCE_SUPPORTED / ENCODER_FALSE_EVIDENCE_NOT_SUPPORTED / LABEL_LIMITED / MIXED /
REPRESENTATION_DEPENDENT / INCONCLUSIVE.

## What the replay will test

Whether >=2 independent listening labels resolve the historical ambiguity: if both reviewers agree
on non-UNCERTAIN labels, classify per the decision tree; otherwise keep LABEL_LIMITED/MIXED. No
classification may rest on score level alone.
