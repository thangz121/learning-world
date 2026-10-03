# 05 — B1 TRAINING RESULTS (STEP 5, test STEP 6/7)

**Artifacts:** `b1_results.json` (history), `b1_test_metrics.json` (test),
`evidence/b1_per_phone.csv`, `run_b1_eval.py`

---

## Training certificate (validation AUC)

| item | value |
|---|---|
| best validation AUC | 0.7999 (epoch 37) |
| epochs run | 46 (early stop at patience 8) |
| train loss (epoch 0 → 37) | 0.0199 → converged, see `b1_results.json` |
| checkpoint | `checkpoints/b1_head.pt` sha256 `44d86379…da3e6` |

Training completed successfully. **That is not scientific success.**

## Test: B1 vs frozen baseline on the SAME 480 utterances / 7,853 tokens

| metric | frozen baseline | B1 head-only | delta |
|---|---|---|---|
| **FRR (expert-correct)** | **0.2272** | **0.2988** | **+0.0716 (WORSE)** |
| FAR (expert-bad) | 0.2708 | 0.1042 | −0.1667 |
| rejected-good (count) | 1,756 | 2,309 | +553 |
| accepted-bad (count) | 13 | 5 | −8 |

## Verdict

B1 **lowered FAR by turning the operating point toward rejection**, which
directly **raised FRR on human-correct child speech**. Under the FRR-first rule
this is a regression. A model that rejects more correct speech is not an
improvement even when it catches more errors.

## What B1 actually learned

With a 60:1 pos:neg imbalance and a small head over fixed encoder features, the
head mostly re-learns the baseline's own posterior geometry and shifts its
threshold. It does not add new phone evidence. See `14_GENERALIZATION.md`.
