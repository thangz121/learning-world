# 07 - PILOT INTEGRITY (WP-1.9.30 Part 14)

> **Tóm tắt (VI):** Kiểm tra toàn vẹn chạy lại: PASS toàn bộ (runner validate, leakage, mapping
> 546/546, split 6/2/2 seed 1927, manifest 200/200, audio 546/546). Không thay đổi split.

## Results (executed this WP)

| # | check | result |
|---|---|---|
| 1 | speaker disjointness | overlaps 0/0/0 |
| 2 | utterance leakage | 0 |
| 3 | audio hash leakage | duplicate audio paths 0 |
| 4 | token leakage | Pack P token ids unique; 546/546 match with features |
| 5 | reviewer leakage | none possible (0 reviewer files) |
| 6 | label leakage | no label columns in manifest/features; no labels exist |
| 7 | feature leakage | feature table is machine evidence only; frozen hash matched |
| 8 | target-phone validity | phone_class consistent for all 546 |
| 9 | split seed | 1927 (frozen true) |
| 10 | split shape | 6/2/2 speakers |
| 11 | corpus size | 200 utterances (120/40/40) |
| 12 | final-consonant inventory | 546 tokens (train 337 / dev 110 / test 99) |
| 13 | expected token counts | match |
| 14 | audio references | 546/546 resolve |
| 15 | feature rows | 546/546 token-id match |

Supporting runs: `pilot_runner.py --stage validate` (PASS; labels absent correctly reported),
`pilot_checks.py` (`leakage_pass = true`), `infra_health_1929.py` (all hashes match).

## Verdict

`PILOT_INTEGRITY = PASS`. No change was made to the split, tokens, or features; evaluation remains
blocked only by the absence of genuine human labels.
