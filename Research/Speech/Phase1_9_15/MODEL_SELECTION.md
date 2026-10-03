# MODEL SELECTION — CALIBRATION MODEL (not pronunciation truth)

**Script:** `experiments/calibrate.py` · **Metrics:** `artifacts/calibration/metrics.csv`,
`metrics.json` · **Curves:** `artifacts/calibration/calibration_curve_rows.csv`

---

## 1. Principle

The artifact trained here is a **CALIBRATION MODEL**: a lightweight mapping from existing
frozen soft-v2 evidence to the **SIAK expert rating** (single annotator). It is not a
pronunciation-truth model, and no production scorer behavior is changed.

## 2. Progression (smallest first)

1. `identity` — no fitting; reference.
2. `affine_soft` — 1-parameter (2 with intercept) monotone affine.
3. `isotonic_soft` — monotone, non-parametric, 1-D; cannot invert the relationship.
4. `linear_*` — 2–7 features; tests confidence/age/evidence ablations.
5. `tree3_*` — depth-3 tree (max 8 leaves); tests whether non-linearity is needed.
6. `hgb_*` — shallow gradient boosting (depth 2, 80 iterations, L2=1.0); the largest model
   considered. No neural network; wav2vec2 was not fine-tuned.

## 3. Selection evidence

| question | result | decision |
|---|---|---|
| Does a monotone 1-D calibration help? | isotonic: Spearman 0.279 vs 0.272 identity; MAE 21.2 vs 27.3; FRR 2.6% vs 12.2% | yes, but limited |
| Do confidence + phone-evidence features help out-of-sample? | linear evidence: P 0.355 vs 0.304 (soft+conf); validation agrees (0.399 vs 0.324) | yes |
| Is a tree/boosting justified? | tree3_noage 0.412, hgb_noage 0.454 test; validation hgb 0.44 | yes for **hgb_noage** as best candidate |
| Does age help? | +age: validation neutral/slightly worse, test not consistently better; tree ignores age; hgb_noage ≥ hgb_full | **reject age as feature** |
| Does mean_sim / exact / miss / duration add signal? | linear_soft_conf_evidence gains over soft+conf on both validation and test | yes, but these are CTC-derived; dependency documented |
| Is a neural model needed? | boosting with depth 2 already gives +0.15 Pearson over identity; no evidence that bigger models are warranted | no |

## 4. Candidates carried forward to external validation

`identity`, `affine_soft`, `isotonic_soft`, `linear_soft_conf_evidence`, `hgb_noage`.

Rationale: identity (baseline), affine (simplest calibration), isotonic (respects
monotonicity, no negative slope), linear evidence (interpretable multi-feature), hgb_noage
(best speaker-disjoint result, does not use age).

## 5. Explicit limitations

- Model selection used validation, but the **test split is speaker-disjoint and was reported
  once**; the hgb family winning on both validation and test is the strongest claim here.
- Feature dependence: `exact_ratio`, `miss_ratio`, `mean_sim`, `n_hits` derive from the same
  CTC posteriors as `soft_full`, so the ensemble improves *use* of the evidence, not the
  *evidence itself*.
- Predictions are compressed (`pred_std` 7.7–10.1 vs `true_std` 26.9); this is intrinsic to a
  weak predictor and must not be read as precision.
- SIAK ratings are a single annotator's judgments; correlations are upper-bounded by both
  the evidence and the label noise.
- No hyperparameter search beyond a small, documented grid; no data from LWE was used for
  fitting at any point.
