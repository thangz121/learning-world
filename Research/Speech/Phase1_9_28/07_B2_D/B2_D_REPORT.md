# B2-D REPORT - WP-1.9.28 Parts 14/16

> **Tóm tắt (VI):** B2-D KHÔNG chạy: thiếu nhãn test. Không có hyperparameter/threshold search.
> Khi đủ nhãn, chạy đúng cấu hình đóng băng D0/D1/D3/D4/D5 và so trực tiếp với baseline.

## Status

`NOT_EXECUTED` (`B2_D_RESULTS.json`). Gate conditions not met: baseline NOT_EXECUTED, Pack P test
labels 0/99.

## Frozen execution (unchanged from WP-1.9.27)

| variant | evidence | aggregation |
|---|---|---|
| D0 | production encoder | span mean (baseline) |
| D1 | production encoder | span max (control) |
| D3 | alt encoder (lv-60) | span max |
| D4 | alt encoder (lv-60) | span mean |
| D5 | both encoders | OR/AND of strong evidence |

- No hyperparameter search, no threshold search, no post-hoc optimization.
- Measurement code and span definition identical across encoders (frozen).
- Metrics: missing evidence, false evidence, final consonants, /r/, speaker-disjoint performance,
  assessability, per-speaker; direct comparison to baseline.
- Outputs prepared: `B2_D_GAIN_LOSS.csv`, `B2_D_PER_SPEAKER.csv`, `B2_D_PER_PHONE.csv` (schemas
  only until execution).

## Known prior evidence (reference, not a result)

609-token diagnostic (WP-1.9.26): production missing 16.48% / false 17.24% vs alt missing 18.75% /
false 13.79%; agreement 82.76%; 50 gains / 55 losses; no aggregate winner.

## Refusal conditions

Will not run if: test labels <30/side (INCONCLUSIVE), leakage fails, freeze fails, or the baseline
cannot run (`PILOT_BLOCKED_BASELINE`).
