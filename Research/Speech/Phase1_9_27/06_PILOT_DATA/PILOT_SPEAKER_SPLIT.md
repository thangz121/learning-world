# PILOT SPEAKER SPLIT — WP-1.9.27 Part 11

> **Tóm tắt (VI):** Split speaker-disjoint đã đóng băng trước khi có bất kỳ nhãn/kết quả nào:
> train 6 / dev 2 / test 2 (1469,0131,0133,0149,1042,1044 | 1046,1061 | 1075,1076), seed 1927,
> chọn theo quy tắc "SO762 child speaker chưa dùng trong 1.9.x, sort (age, id), 10 speaker đầu có
> >=12 utterance". Test không bao giờ vào train/adaptation.

## Frozen split (`artifacts/pilot_split.json`)

| split | speakers | utterances | tokens (Pack P) |
|---|---|---|---|
| train | 1469, 0131, 0133, 0149, 1042, 1044 | 120 | 337 |
| dev | 1046, 1061 | 40 | 110 |
| test | 1075, 1076 | 40 | 99 |

- Selection rule: SO762 child speakers NOT used in any WP-1.9.x evaluation set, sorted by
  (age, speaker_id), first 10 with >=12 utterances.
- Seed: 1927. Frozen: true. Age: 1469 is 6; the other nine are 7.
- Used-speaker exclusion list (45 speakers) is recorded in `pilot_split.json`.

## Documented per speaker

speaker id, age (6/7), split, utterances (20 each), hours (split totals: train 0.117 h /
dev 0.039 h / test 0.040 h; total 0.196 h), target-phone distribution (`pilot_data_summary.json`,
`PILOT_REVIEW_CANDIDATES.csv`). Gender is not collected in SO762 for this subset; not reported.

## Validation (must stay green)

- train∩dev = 0; train∩test = 0; dev∩test = 0.
- duplicate audio paths = 0; duplicate speaker+text pairs = 0; utt overlap train/test = 0.
- No label fields and no machine-score fields inside the manifest.
- `pilot_checks.py` re-runs these checks; current result: `leakage_pass = true` (200/200 manifest
  rows PASS).

## Rules

- The test speakers are evaluation-only: they may not be used for feature selection, calibration
  fitting, threshold tuning, or adaptation.
- If the split must change (e.g. a speaker proves unusable), the change is a new frozen file with a
  new seed/date and all downstream results are invalidated; no silent edits.
