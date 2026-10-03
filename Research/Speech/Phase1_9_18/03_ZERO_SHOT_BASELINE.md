# 03 — ZERO-SHOT FROZEN BASELINE (STEP 4)

**Script:** `experiments/run_zero_shot.py` · **Artifacts:** `zero_shot_baseline.json`,
`evidence/zero_shot_per_phone.csv`, `evidence/zero_shot_utterances.csv`
**Model (frozen, unchanged):** `facebook/wav2vec2-xlsr-53-espeak-cv-ft@2c73378`
**Pipeline:** `PhoneEvidenceV2@1.4.0` — the exact 1.9.15/1.9.16 call path.

---

## Result on SO762 child test (24 speakers, 480 utterances, 7,853 phone tokens)

| metric | value |
|---|---|
| utterances scored / errors | 480 / 0 |
| mean soft score (0–100) | 75.78 |
| mean confidence | 0.0763 (low, the known evidence weakness) |
| mean processing | 0.944 s/utt (CPU) |
| wall | 780.9 s |

### FRR-first (per phone token)

| metric | value |
|---|---|
| expert-correct tokens (expert acc ≥ 1.0) | 7,728 |
| expert-bad tokens (expert acc < 0.5) | 48 |
| **FRR (rejected good)** | **0.2272** (1,756 rejected) |
| **FAR (accepted bad)** | **0.2708** (13 accepted) |

Decision boundary mirrors the frozen scorer: per-phone sim < 0.5 ⇒ rejected.

## What is NOT computed (declared, not faked)

- **PER / confusion matrix: NOT_COMPUTABLE.** SO762 provides expert *scores* +
  sparse observed errors, not a dense observed-phone transcript. A model-derived
  pseudo-transcript would be circular.
- FAR denominator is only 48 tokens — reported with that caveat; FAR deltas are
  not treated as robust.

This is the fixed reference that B1 must beat, and it did not.
