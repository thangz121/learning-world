# SPEAKER SPLIT REPORT - WP-1.9.28 Part 10

> **Tóm tắt (VI):** Split đóng băng nguyên vẹn: train 6 / dev 2 / test 2; tuổi 6-7; 20 utt/speaker;
> test (1075, 1076) không xuất hiện ở train/dev/adaptation; 99 token test. Không có thay đổi nào
> từ WP-1.9.27.

## Frozen split (verified this WP)

| split | speakers | utterances | Pack P tokens | ages |
|---|---|---|---:|---|
| train | 1469, 0131, 0133, 0149, 1042, 1044 | 120 | 337 | 6 (1469), 7 (5) |
| dev | 1046, 1061 | 40 | 110 | 7 |
| test | 1075, 1076 | 40 | 99 | 7 |

- Seed 1927; file `Phase1_9_27/artifacts/pilot_split.json` (hash recorded).
- Speaker-disjoint from all WP-1.9.x evaluation sets (verified against the 45 used speakers).
- 546 tokens total; 8 /r/.

## Invariants verified

- train∩dev = 0; train∩test = 0; dev∩test = 0.
- 20 utterances per speaker (120/40/40).
- audio files resolve (40 per speaker).
- token-id sets equal between Pack P and the feature table (546/546).
- test speakers never appear in any training/adaptation material in this repository.

## Age limitation (Part 11, explicit)

The pilot is **6-7 years old (1 speaker age 6, 9 speakers age 7)**. It is NOT an age-4-6 pilot and
NOT a Vietnamese-L1 study. Any result may be described only as evidence from this 6-7 local sample;
`age 4-6 validation` and `Vietnamese-L1 validation` are prohibited claims.

## Test-set definition

The frozen evaluation set = Pack P test split: 99 tokens from speakers 1075 and 1076. Changing this
set (adding/removing tokens or speakers) requires a new preregistration and invalidates the current
one.
