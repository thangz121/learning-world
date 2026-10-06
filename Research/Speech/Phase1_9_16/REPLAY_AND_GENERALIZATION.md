# HISTORICAL FAILURE REPLAY (Experiment E) + SPEAKER GENERALIZATION (Experiment I)

**Script:** `experiments/failure_replay.py`
**Artifacts:** `artifacts/replay/` (`failure_replay.csv`, `replay_summary.json`),
`artifacts/generalization/` (per-speaker CSVs + summary)

---

## 1. Experiment E — replay of the 1.9.9/1.9.10 failure cases

Classification rules (transparent): PHONE_MODEL_ERROR — Δscore ≥ +10 FIXED, +3..+10
PARTIALLY_FIXED, −3..+3 UNCHANGED, −10..−3 PARTIALLY_REGRESSED, ≤ −10 REGRESSED.
SCORER_MISS — adapted <50 while baseline ≥50 FIXED; drop ≥15 while still ≥50 PARTIALLY_FIXED;
rise ≥10 REGRESSED; else UNCHANGED.

Summary: 18 cases replayed, 0 INCONCLUSIVE (all had audio in the 80-token corpus).

| group | FIXED | PARTIAL + | UNCHANGED | PARTIAL − | REGRESSED |
|---|---:|---:|---:|---:|---:|
| PHONE_MODEL_ERROR (12) | 5 | 2 | 3 | 1 | 1 |
| SCORER_MISS (6) | 0 | 0 | 5 | 0 | 1 |

### PHONE_MODEL_ERROR highlights

| case | human | word | baseline → adapted | outcome |
|---|---|---|---|---|
| pme_02 child_06 two | CLEAR_CORRECT | two | 0.0 → **100.0** (final miss→exact) | FIXED |
| pme_07 child_01 seven | CLEAR_CORRECT | seven | 20.0 → **80.0** | FIXED |
| pme_08 child_06 six | CLEAR_CORRECT | six | 25.0 → **50.0** | FIXED |
| pme_09 child_09 five | CLEAR_CORRECT | five | 33.4 → **72.5** (final miss→soft) | FIXED |
| pme_10 child_01 five | PROBABLY_CORRECT | five | 33.5 → **72.5** | FIXED |
| pme_11 child_03 three | CLEAR_CORRECT | three | 39.2 → **6.7** | REGRESSED |
| pme_06 child_04 three | CLEAR_CORRECT | three | 11.7 → 6.7 | PART. REGRESSED |
| pme_01/03/05 child_03/07/08 eight | (prob.) correct | eight | 0.0 → 0.0 | UNCHANGED |

Fix pattern: vowel/schwa-heavy words (two, five, six, seven) improve sharply; θ/r words
(three) regress; the /t/-final "eight" cases remain at 0.

### SCORER_MISS highlights

| case | human | word | baseline → adapted | outcome |
|---|---|---|---|---|
| sm_04/05 (four, deleted /r/) | TRUE_ERROR | four | 100.0 → 100.0 (final stays exact) | UNCHANGED |
| sm_06 (four) | TRUE_ERROR | four | 66.8 → 73.3 (final miss→exact) | UNCHANGED (trend worse) |
| sm_02 (two) | UNCERTAIN | two | 50.0 → **100.0** | REGRESSED |
| sm_01/03 | CONFLICTED/UNCERTAIN | six/eight | 50.0 → 50.0 | UNCHANGED |

**The deleted-final /r/ class is not fixed by the adapted model** — consistent with 1.9.14:
the failure is representational (forced alignment cannot express deletion), not only acoustic.

## 2. Experiment I — speaker generalization

Per-speaker files: `lwe_per_speaker.csv`, `siak_test_per_speaker.csv`, `so762_per_speaker.csv`.

- **LWE (11 children):** final-consonant recall improved for 0 speakers and degraded for 3;
  no catastrophic single-speaker collapse detected beyond the known difficult children
  (child_07 one, child_03/04 three).
- **SIAK test (24 speakers with ≥10 utt):** Pearson up for 12, down for 12 — the adapted
  correlation gain is not uniform; it is a population average, with half the speakers
  unchanged or worse.
- **so762 child test (61 speakers ≥30 phones):** exact rate up for 37 speakers, down for 27;
  best +0.154 (spk 1050, age 8), worst −0.074 (spk 0093, age 6).

No accent-specific or age-specific collapse is claimed: age-6 speakers appear in both the
improved and degraded groups; LWE speakers are too few for subgroup claims.

## 3. What this means

- The adapted head redistributes errors rather than uniformly improving them.
- The single most robust negative finding across replay + in-domain + external tests is the
  **word-final consonant regression**, which is the class the phase was created to fix.
- Therefore the frozen model stays the reference and B1 is reported as a falsified
  improvement for the LWE final-consonant objective.
