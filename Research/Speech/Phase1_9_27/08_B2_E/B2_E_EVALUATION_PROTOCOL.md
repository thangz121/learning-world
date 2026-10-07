# B2-E EVALUATION PROTOCOL (FROZEN) — WP-1.9.27 Part 15

> **Tóm tắt (VI):** Đánh giá B2-E FRR-first trên test speaker-disjoint: recall/FRR/FAR, AUC, theo
> phone class, /r/ riêng, calibration, assessability gate, failure replay; báo cáo per-speaker
> (mean/median/worst/best/variance); dev→test drop <=5 điểm; <30/side = INCONCLUSIVE.

## Inputs

- Labels: Pack P HUMAN-LISTENING labels joined by `token_id` to `artifacts/pilot_features.csv`.
- Split: frozen 6/2/2 (`pilot_split.json`); test speakers 1075/1076.
- Baseline: production decision path re-expressed as the frozen acceptance operating point (no
  modification to production code).

## Metrics (FRR-first)

1. PRESENT recall and FRR on labelled PRESENT at the frozen operating point.
2. FAR / false PRESENT rate on labelled ABSENT.
3. Missing-evidence rate (max_A < 0.02) before vs after the hybrid gate.
4. AUC per phone class where both sides >=30 labels (else report n and skip).
5. /r/ recall/rejection separately (under-labelled until 15+15 exist; report coverage).
6. Per-speaker: recall/FRR/FAR mean, median, worst, best, variance; require dev->test drop <=5
   points (criterion from `09_SUCCESS_CRITERIA/`).
7. Assessability gate: all metrics re-run with NOT_ASSESSABLE excluded; a model may not gain credit
   on audio a human could not assess.
8. Calibration: reliability within +/-0.1 of the diagonal in the decision band.
9. Failure replay: 4 TYPE-B cases, weak labelled presents, strongest false accepts.

## Procedural rules

- No test-driven tuning; dev inspection only; test readout once per frozen variant.
- Report UNCERTAIN counts separately; never merge UNCERTAIN into ABSENT.
- Fitted variants require the random-feature control; if the control matches the real variant, the
  result is reported as no evidence of feature value.
- Outcome labels (SUCCESS / PARTIAL_SUCCESS / FAILURE / INCONCLUSIVE) come only from the frozen
  criteria in `09_SUCCESS_CRITERIA/`.
