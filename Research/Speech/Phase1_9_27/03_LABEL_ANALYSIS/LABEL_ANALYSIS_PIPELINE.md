# LABEL ANALYSIS PIPELINE — WP-1.9.27 Parts 7–9

> **Tóm tắt (VI):** Pipeline sẵn sàng tiêu thụ kết quả reviewer: nhận JSONL server hoặc CSV export;
> xuất consensus từng ca, bất đồng, agreement/kappa, danh sách adjudication và gate độ đủ nhãn.
> Hiện tại: 0 reviewer, output rỗng có schema; không có nhãn giả.

## Script

`experiments/label_analysis.py` (tested by `experiments/test_label_analysis.py`, PASS).

```
python label_analysis.py \
  [--reviews artifacts/reviews] \
  [--out 03_LABEL_ANALYSIS] \
  [--pack <candidate pack csv>] \
  [--csv REV-A=revA.csv --csv REV-B=revB.csv]
```

- `--reviews`: directory of server JSONL stores (one per reviewer).
- `--csv`: external exports (CSV from the server's export button or equivalent). Each mapping is
  `reviewer_id=path.csv` with header `blind_id,label,confidence,assessable,note,ts`.
- `--pack`: candidate pack that maps `blind_id` -> token metadata. Default = Pack R
  (`Phase1_9_26/01_HUMAN_LABEL_ACQUISITION/REVIEW_CANDIDATES.csv`); use
  `06_PILOT_DATA/PILOT_REVIEW_CANDIDATES.csv` for the pilot label round.

## Outputs (all currently empty/zero — no reviewer yet)

| file | content |
|---|---|
| HUMAN_LABEL_RESULTS.csv | per case x reviewer + consensus, confidence, assessability, notes |
| REVIEWER_AGREEMENT.csv | raw/Cohen/weighted/Fleiss + per-subset agreement rows |
| ADJUDICATION_RESULTS.csv | DISAGREEMENT/UNCERTAIN cases with blank adjudicator columns |
| label_sufficiency.json | TYPE-B and /r/ gate values + rationale |

## Consensus rules

- 2 reviewers: agree -> that label; disagree -> DISAGREEMENT (no majority vote).
- >=3 reviewers: 2/3 majority with the disagreement flag preserved; ties -> DISAGREEMENT.
- UNCERTAIN is preserved as its own consensus; never collapsed into ABSENT.

## Current run (2026-10-07)

- reviewers = 0; labelled cases = 0; adjudication = 0.
- `LABELS_SUFFICIENT_FOR_TYPE_B=false` (0/4), `R_LABELS_SUFFICIENT=false` (0 PRESENT / 0 ABSENT /
  0 speakers).
- The pipeline is exercised with synthetic data and deleted (no labels fabricated).
