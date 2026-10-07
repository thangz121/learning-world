# BASELINE REPORT - WP-1.9.28 Part 13

> **Tóm tắt (VI):** Baseline KHÔNG chạy: không có nhãn người cho test split. Đây là hành vi đúng
> theo kế hoạch (Part 3). Không có kết quả nào được tạo; status = NOT_EXECUTED.

## Status

`NOT_EXECUTED` - see `BASELINE_RESULTS.json`.

## Why

- Baseline evaluation requires HUMAN-LISTENING labels on the frozen test split (99 tokens,
  speakers 1075/1076).
- Pack P coverage: 0/99; zero reviewers; gates false (see `04_LABEL_GATE/`).
- Running the baseline without labels would produce numbers that cannot be interpreted and would
  violate the frozen preregistration.

## Planned baseline (frozen, unchanged from WP-1.9.27)

- Production frozen decision path at its operating point; no optimization.
- Report: FRR, FAR, final-consonant recall/rejection, /r/ (separate), per-speaker, assessability.
- Exact config schema: `Phase1_9_27/10_REPRODUCIBILITY/EXPERIMENT_CONFIG_SCHEMA.md`.

## Trigger

As soon as `label_analysis.py` (combined pack) reports the pilot minimum, run the baseline once and
record `BASELINE_RESULTS.json` with real values, then proceed to B2-D/B2-E per the frozen order.
