# B2-D EVALUATION PROTOCOL (FROZEN) — WP-1.9.27 Part 14

> **Tóm tắt (VI):** Đánh giá B2-D FRR-first trên test speaker-disjoint với nhãn HUMAN-LISTENING:
> báo cáo missing/false evidence, agreement, AUC (nếu đủ n), per-speaker (mean/median/worst/best/
> variance), /r/ riêng, assessability gate, failure replay 4 TYPE-B. <30 token/side = INCONCLUSIVE.

## Inputs

- `artifacts/pilot_features.csv` (546 tokens, both encoders) joined to Pack P labels by `token_id`.
- Frozen split from `artifacts/pilot_split.json`; test = speakers 1075, 1076 (99 tokens).
- Human labels only (HUMAN-LISTENING class); EXPERT/AUTOMATIC never enter metrics.

## Procedure

1. Join labels; drop NOT_ASSESSABLE with an explicit count (assessability gate).
2. For each variant in `B2_D_ABLATIONS.md`: compute evidence on dev (inspection) and test (readout).
3. Readout metrics (FRR-first):
   - missing-evidence rate on labelled PRESENT (max_A < 0.02);
   - false-evidence rate on labelled ABSENT (max_A >= 0.30);
   - FRR / FAR at the frozen production operating point and at research gates;
   - agreement between encoders at 0.10;
   - AUC where both classes have >=30 labels;
   - per-speaker metrics: mean, median, worst, best, variance (never pooled-only);
   - /r/ subset reported separately (under-labelled until 15+15 exist);
   - failure replay: the 4 TYPE-B cases + strongest false accepts + weak labelled presents.
4. Repeat with the assessability gate on (label NOT_ASSESSABLE excluded from credit).
5. Results are reported as measured; the pre-registered success criteria in `09_SUCCESS_CRITERIA/`
   decide the outcome.

## Inconclusive rules

- < 30 labelled tokens per side on the test split, or labels with neither kappa nor an explicit
  single-reviewer limitation: INCONCLUSIVE.
- If a variant wins only via training speakers or only via one test speaker: not a pass (report).
- No threshold, gate or label may be changed after seeing test results.
