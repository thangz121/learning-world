# DATA LEAKAGE CHECKS — WP-1.9.27 Part 30

> **Tóm tắt (VI):** Kiểm tra rò rỉ tự động cho pilot: speaker trùng split, audio trùng, gần-trùng,
> session trùng, leakage từ word/label/machine score/alignment. Kết quả hiện tại: PASS toàn bộ
> (train/dev/test overlap 0; 200/200 manifest PASS).

## Checks implemented (`experiments/pilot_checks.py`)

| check | method | current result |
|---|---|---|
| speaker across train/test | set intersection of speaker ids | 0 |
| speaker across train/dev | set intersection | 0 |
| speaker across dev/test | set intersection | 0 |
| duplicate audio paths | path count vs unique | 0 |
| duplicate speaker+text pairs | Counter over (speaker_id, text) | 0 |
| utterance across train/test | set intersection of utt ids | 0 |
| label fields in manifest | scan column names for "label" | none |
| machine-score fields in manifest | scan for "max_a"/"evidence" | none |

Aggregate: `leakage_pass = true` (`artifacts/pilot_checks.json`).

## Checks required at run time (B2-D/B2-E)

- Verify the split file hash and the feature/label file hashes match the config.
- Reject any run whose labelled tokens touch a train speaker at test time.
- Reject label rows whose `token_id` is not in the features file (join integrity).
- Never join machine scores into the label source of truth; they are separate columns.
- Alignment outputs (if generated) are AUTOMATIC evidence and may not be used as evaluation labels.

## Near-duplicate audio (to add when new corpora are admitted)

- Compute a cheap perceptual fingerprint (e.g., duration + spectral centroid vector) and flag pairs
  closer than a fixed threshold within and across splits; any hit fails the pilot gate.
- Same recording session across splits: sessions must map to one split; SO762 speakers are
  single-session, so the speaker rule covers the pilot; future corpora need an explicit session key.

## Failure rule

Any leakage failure invalidates the affected experiment, not just the row: fix the split, rerun the
checks, rerun the experiment with a new config.
