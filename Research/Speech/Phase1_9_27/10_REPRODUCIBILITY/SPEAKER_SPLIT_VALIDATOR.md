# SPEAKER SPLIT VALIDATOR — WP-1.9.27 Part 30

> **Tóm tắt (VI):** Validator cho split: đọc split đóng băng + manifest, xác nhận speaker-disjoint,
> không rò rỉ audio/utt, đủ token mỗi split, tuổi/speaker khớp split file. Chạy trước mọi lần đánh
> giá; fail là dừng.

## Validations

```
experiments/pilot_runner.py --stage validate     # split + manifest + labels-ready check
experiments/pilot_checks.py                      # leakage + pool + power
```

`stage validate` currently returns: rows 200, PASS 200, train_dev_overlap 0, train_test_overlap 0,
dev_test_overlap 0, human_labels_present false, leakage_pass true.

## Additional invariants (checked in review of this WP)

| invariant | value |
|---|---|
| speakers per split match `pilot_split.json` exactly | 6/2/2 (1469 0131 0133 0149 1042 1044 / 1046 1061 / 1075 1076) |
| utterances per speaker | 20 (120/40/40) |
| Pack P tokens per split | 337 / 110 / 99 = 546 |
| token_id sets: Pack P vs pilot_features.csv | equal (546/546) |
| audio references resolve | 546/546 |
| speakers in split never appear in WP-1.9.x used-speaker list | verified in `pilot_split.json` |

## Rules

- A split change is a new frozen file with new date+seed; downstream results invalidated.
- The validator must run and pass before any test readout; a failed validation stops the experiment.
- Do not "repair" a split by moving utterances between speakers; the speaker is the unit.
