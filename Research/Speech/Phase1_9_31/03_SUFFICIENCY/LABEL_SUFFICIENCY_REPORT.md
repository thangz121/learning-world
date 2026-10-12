# 03 — LABEL SUFFICIENCY (WP-1.9.31)

> **Tóm tắt (VI):** Với chuẩn ngoài: pilot 546 có 541P/5A (không đủ 60A — pilot tái đọc chỉ là
> phụ lục kế thừa); BENCH test có 4.232P/94A/64 speakers — đủ mọi ngưỡng hiệu lực (>=60P, >=60A,
> >=30/side test, >=10 speakers). /r/ produced: 220P/4A/91 speakers — phía ABSENT KHÔNG đạt (4<15);
> strict rhotic: 218/6 — cũng không đạt phía not-correct (6<15). TYPE-B: 3 ca SO762 có nhãn hội
> đồng chuyên gia (2 khớp REV-A, 1 ngược), 1 ca Zenodo còn `NO_EXPERT_LABELS`.

- **Script:** `experiments/p31_sufficiency.py`;
  **artifact:** `artifacts/sufficiency/label_sufficiency_p31.json`.

## Sub-gates under the external standard

| gate | count | required | status |
|---|---|---|---|
| BENCH test PRESENT | 4,232 | >=60 | met |
| BENCH test ABSENT | 94 | >=60 / >=30/side | met |
| BENCH test speakers | 64 | >=10 | met |
| pilot 546 ABSENT | 5 | >=60 | NOT met (pilot sub-readout only) |
| /r/ produced PRESENT | 220 / 91 spk | >=15 / >=10 spk | met |
| /r/ produced ABSENT | 4 | >=15 | **NOT met — gate unreachable with this corpus** |
| /r/ strict-correct / not | 218 / 6 | (new framing) | reported, not met on "not" side |
| TYPE-B expert consensus | 3/4 | 4/4 | 3 met; 1 label-limited (Zenodo) |

## TYPE-B (frozen 4 cases)

| case | expert marks (5) | consensus | vs REV-A (single) |
|---|---|---|---|
| `014180143_15` | 0 0 2 0 0 | ABSENT | ABSENT (agree) |
| `014190172_7` | 0 0 0 0 2 | ABSENT | PRESENT (diverge) |
| `014350146_16` | 2 0 0 0 0 | ABSENT | ABSENT (agree) |
| `child_07_seven` | — | NO_EXPERT_LABELS (Zenodo corpus has no phone labels) | PRESENT (REV-A only) |

## Honest notes (no gate moves)

- The frozen /r/ 15+15 produced-label gate is **not reachable** in the available local corpora:
  expert panels mark child /r/ errors mostly as accented (`{1}`) or sporadic zeros, so
  majority-ABSENT /r/ tokens are structurally rare (4 across all SO762 children). No reclassification
  is applied; the strict rhotic-correct framing is reported as an additional view, not a substitute.
- The pilot's 60P+60A minimum was a review-round design; under the external standard the powered
  set is the BENCH (per the pre-registration). The pilot 546 remains a continuity sub-readout.
