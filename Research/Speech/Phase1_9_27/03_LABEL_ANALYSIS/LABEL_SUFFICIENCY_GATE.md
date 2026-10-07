# LABEL SUFFICIENCY GATE — WP-1.9.27 Part 9

> **Tóm tắt (VI):** Định nghĩa gate độ đủ nhãn với lý do cụ thể: TYPE-B cần cả 4 ca có consensus
> non-UNCERTAIN từ >=2 reviewer; /r/ cần >=15 PRESENT + >=15 ABSENT trên >=10 speaker (không có
> ngưỡng tùy tiện — gắn với CI và chống 1 speaker chi phối). Hiện tại cả hai = false.

## Gate definitions (implemented in `label_analysis.py`)

`LABELS_SUFFICIENT_FOR_TYPE_B = true` iff all 4 decisive cases
(`child_07_seven`, `014180143_15`, `014190172_7`, `014350146_16`) have a non-UNCERTAIN consensus
from >=2 independent reviewers.

`R_LABELS_SUFFICIENT = true` iff /r/ consensus labels include:
- >=15 PRESENT and >=15 ABSENT,
- across >=10 distinct speakers.

## Why these thresholds (not arbitrary)

- 15 per side gives a 95% Wilson interval of roughly +/-0.20 around p=0.8 — enough to separate
  "mostly present" from "mostly absent", not enough to certify production accuracy.
- >=10 speakers prevents a single child from dominating the /r/ conclusion.
- TYPE-B: 2 independent reviewers is the minimum to separate "label ambiguity" from "encoder false
  evidence"; 1 reviewer cannot (the historical LWE labels are single-reviewer).

## Current status (2026-10-07)

| gate | value | evidence |
|---|---|---|
| LABELS_SUFFICIENT_FOR_TYPE_B | false | typeb_consensus_cases = 0/4 |
| R_LABELS_SUFFICIENT | false | r_consensus_present = 0, r_consensus_absent = 0, r_speakers = 0 |
| minimum overall set | not met | 0/60 PRESENT, 0/60 ABSENT |

## How the gate moves

1. Two reviewers complete Pack R (276) -> rerun `label_analysis.py` -> TYPE-B and /r/ gates update.
2. For the pilot: reviewers label Pack P (546 pilot tokens) -> the B2-D/B2-E evaluation set gets its
   own labelled subset (minimum 60 PRESENT + 60 ABSENT on the pilot set).
3. No gate is evaluated on score-derived labels (SO762/SIAK expert scores are not listening labels).
