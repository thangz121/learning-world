# /R/ LABEL GATE - WP-1.9.28 Part 9

> **Tóm tắt (VI):** Gate /r/ cần >=15 PRESENT + >=15 ABSENT consensus trên >=10 speaker. Hiện 0/0.
> Cảnh báo quan trọng: tổng nguồn /r/ local hiện có chỉ 32 ứng viên (Pack R 24 + Pack P 8) —
> vừa trên mức 30 nhãn cần; nếu phân bố consensus lệch, gate có thể không đạt cục bộ và cần
> PERCEPT-R (research-only) hoặc thu thập thêm.

## Current status

| item | value |
|---|---|
| /r/ consensus labels | 0 PRESENT / 0 ABSENT |
| distinct speakers with /r/ consensus | 0 |
| R_LABELS_SUFFICIENT | false |

## Supply analysis (the critical risk)

| source | /r/ candidates | notes |
|---|---:|---|
| Pack R pools A/B/C | 24 | 9 LWE (1 PRESENT LOW historical / 5 ABSENT HIGH / 3 unlabelled) + 15 SO762 (score-only) |
| Pack P | 8 | pilot tokens (`ɹ`), part of the frozen 546 |
| **total local supply** | **32** | the entire known local /r/ inventory (WP-1.9.26 audit: 24 pre-pilot cases) |

Gate requires 30 decisive consensus labels (15+15). With only 32 candidates and an unknown
PRESENT/ABSENT split, the /r/ gate may be unreachable locally even with 100% coverage. This is a
real limitation to report, not a reason to lower the gate.

## If the local supply proves insufficient

1. PERCEPT-R (PhonBank, research-only, non-commercial): 280 speakers, rhotic/derhotic labels —
   usable as a research benchmark only; negotiate access terms first.
2. Prospective /r/-dense collection (design only; no automatic collection) as part of a local
   child-speech session.
3. Do NOT weaken the 15+15 threshold; report `R_INCONCLUSIVE` instead.

## Reviewer instructions for /r/

A short or weakly rhotic production still counts as PRESENT if perceptible; do not require adult
canonical rhoticity; UNCERTAIN for undecidable weak cases (preserved). See
`Phase1_9_27/05_R/R_LABEL_REQUIREMENTS.md`.
