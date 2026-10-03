# DATA LEAKAGE AUDIT — SIAK SPEAKER-DISJOINT SPLIT

**Script:** `experiments/split_audit.py`
**Artifacts:** `artifacts/siak/split_manifest.json`, `calibration_train.csv`,
`calibration_valid.csv`, `calibration_test.csv`, `calibration_ages46.csv`

---

## 1. Sample and splits

Deterministic sample of 20 utterances/speaker (3,168 utterances, 172 speakers).
Primary population ages 7–12; ages 4–6 held out.

| split | utterances | speakers | ages (utt) | unique targets |
|---|---:|---:|---|---:|
| train | 2,094 | 113 | 7:120, 8:394, 9:748, 10:511, 11:301, 12:20 | 296 |
| validation | 493 | 27 | 7:40, 8:100, 9:177, 10:87, 11:80, 12:9 | 181 |
| test | 482 | 27 | 7:40, 8:93, 9:165, 10:105, 11:59, 12:20 | 164 |
| ages 4–6 external | 99 | 5 | 4:20, 5:20, 6:59 | 77 |

## 2. Leakage checks

| check | result |
|---|---|
| speaker overlap train×test | **∅** |
| speaker overlap train×validation | **∅** |
| speaker overlap validation×test | **∅** |
| file overlap across splits | **∅** (each file appears in exactly one split) |
| duplicate audio (SHA-1 over all 3,168 selected FLAC files) | **0 duplicate groups** |
| missing scores | 0 |
| ages 4–6 used for fitting | **no** (marked `external`) |
| LWE tokens used for fitting | **no** (external validation only) |

## 3. Target overlap (informational, not leakage)

155 target words appear in both train and test (shared SIAK vocabulary). This is expected:
speakers are disjoint, so the same word uttered by different children cannot leak speaker
identity. It is reported because the spec asks, not because it invalidates the split.

## 4. Split mechanism

- Speakers assigned to train/validation/test per age stratum (70/15/15 nearest integer,
  deterministic SHA-1(seed 1515 + speaker_id) ordering).
- Ages 4–6 speakers are never assigned to train/validation/test.
- The SIAK release already provides a train/test speaker split in filenames; this audit uses
  its own 3-way split and does not mix them, preserving speaker disjointness either way.

## 5. Known limits

- 20 utterances/speaker is a sample; per-speaker utterance counts in the full release are
  much larger (train001: 420). Sampling is deterministic and documented, so the experiment
  is reproducible.
- Ages 4–6 have only 5 speakers; no claim of statistical generalization is made there.
- Physical identity across anonymised speaker IDs cannot be excluded by construction.
