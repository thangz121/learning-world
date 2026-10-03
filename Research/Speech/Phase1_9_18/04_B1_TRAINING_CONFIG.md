# 04 — B1 TRAINING CONFIG (STEP 5)

**Script:** `experiments/run_b1_train.py` · **Config:** `b1_config.json`
**Checkpoint:** `checkpoints/b1_head.pt` (sha256 `44d86379…da3e6`)

---

## Architecture

FROZEN `wav2vec2-xlsr-53-espeak-cv-ft@2c73378` → frame logits `[T, 392]`
+ FROZEN CTC forced alignment (`ctc_forced_v1`, the exact 1.9.15 alignment)
+ encoder-only per-phone feature vector (8 dims, no label leakage):

| # | feature |
|---|---|
| 1 | expected-phone class posterior mass over span |
| 2 | top-1 token posterior |
| 3 | top-2 token posterior |
| 4 | top1−top2 margin |
| 5 | span length / T |
| 6 | mean frame entropy (span-averaged) |
| 7 | has-ids indicator |
| 8 | max posterior in span-averaged distribution |

+ TRAINABLE head: `Linear(8→32) → ReLU → Linear(32→32) → ReLU → Linear(32→1)`,
BCE-with-logits.

**The encoder weights are never changed.** PhoneEvidenceV2@1.4.0 is imported
read-only and never modified.

## Hyperparameters (all logged in `b1_config.json`)

| param | value |
|---|---|
| seed | 1515 |
| optimizer | AdamW |
| lr | 1e-3 |
| weight_decay | 1e-4 |
| batch_size | 64 |
| max_epochs | 60 |
| early stopping | on validation AUC (patience 8) |
| checkpoint selection | best validation AUC |
| loss | weighted BCEWithLogits |
| pos_weight | auto = n_neg/n_pos = 0.01664 |

## Auditable target (STEP 2)

- positive `y=1`: expert phone accuracy **≥ 1.0**
- negative `y=0`: expert phone accuracy **< 0.5**
- **excluded (ambiguous)**: `0.5 ≤ acc < 1.0` (neither pos nor neg)
- `<unk>` / `<DEL>` never become phones; observed substitutions are diagnostic
  only, never training targets.
- **No pseudo-labels** from the frozen model were used.

## Training set

- train tokens: 26,510 (pos 26,076 / neg 434)
- valid tokens: 5,999 (pos 5,967 / neg 32)
- epochs run: 46 · best epoch: 37 · **best validation AUC: 0.7999**

Hyperparameter note: the heavy class imbalance (60:1) is the dominant training
signal; the head largely learns the majority class and shifts its operating
point. This is documented, not hidden (`14`).
