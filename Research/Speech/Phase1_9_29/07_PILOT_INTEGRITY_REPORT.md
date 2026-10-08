# 07 - PILOT INTEGRITY REPORT (WP-1.9.29 Part 7)

> **Tóm tắt (VI):** Re-validate pilot sau WP-1.9.28 (không có nhãn để gắn, nhưng toàn bộ kiểm tra
> cấu trúc đã chạy lại): split speaker-disjoint, leakage, token/audio mapping, seed, kích thước,
> inventory - tất cả PASS. Không thay đổi split. PILOT INTEGRITY = PASS.

## Checks (executed this WP)

| # | check | result |
|---|---|---|
| 1 | train/dev/test speaker disjointness | 0 / 0 / 0 overlaps |
| 2 | utterance leakage | utt_overlap_train_test = 0 |
| 3 | token leakage | Pack P token ids unique; 546/546 match with features |
| 4 | audio hash leakage | not applicable at token level; duplicate audio paths 0 |
| 5 | duplicate candidate leakage | duplicate speaker+text 0; Pack P blind_ids unique 546 |
| 6 | reviewer leakage | none possible: 0 reviewer files |
| 7 | label leakage | no label columns in manifest/features; no labels exist |
| 8 | feature/token correspondence | 546/546 exact set match |
| 9 | target-phone correctness | phone_class mapping consistent for all 546 (checked via build script) |
| 10 | /r/ coverage | 8 pilot /r/ tokens present |
| 11 | final-consonant coverage | 546 tokens across 20 phones (T 95, N 90, Z 81, S 64, ...) |
| 12 | TYPE-B coverage | 4 decisive cases traceable in Pack R pool F |
| 13 | assessability metadata | not collected yet (no labels); schema ready |
| 14 | frozen split seed | 1927 (frozen true) |
| 15 | expected split | 6 / 2 / 2 speakers (1469 0131 0133 0149 1042 1044 / 1046 1061 / 1075 1076) |
| 16 | expected pilot corpus | 200 utterances (120/40/40), manifest 200/200 PASS |
| 17 | expected final-consonant inventory | 546 tokens (train 337 / dev 110 / test 99) |

## Supporting runs

- `pilot_runner.py --stage validate`: PASS; `human_labels_present = false` (correct).
- `pilot_checks.py`: `leakage_pass = true`.
- `infra_health_1929.py`: all frozen hashes match; Pack P served 546 blind; audio 546/546.

## Verdict

`PILOT_INTEGRITY = PASS`. The split was not altered; no speaker moved; no leakage condition exists.
Evaluation remains blocked solely by the absence of human labels.
