# POPULATION CALIBRATION — SIAK, SPEAKER-DISJOINT

**Scripts:** `experiments/siak_expand.py`, `split_audit.py`, `calibrate.py`
**Artifacts:** `artifacts/siak/siak_expanded_scored.csv` (3,168 rows),
`siak_expanded_merged.csv`, `calibration_train.csv` / `calibration_valid.csv` /
`calibration_test.csv` / `calibration_ages46.csv`, `split_manifest.json`,
`artifacts/calibration/metrics.csv`, `metrics.json`, `calibration_curve_rows.csv`

Research only. No production change.

---

## 1. Design

- Population: SIAK, deterministic sample of **20 utterances/speaker** across all 172 speakers
  (3,168 utterances). 1,074 rows reused from Phase 1.9.14; 2,965 re-scored here with the
  frozen soft-v2 pipeline (`wav2vec2-xlsr-53-espeak-cv-ft`, `PhoneEvidenceV2@1.4.0`).
- Primary population **ages 7–12**; a strict speaker-disjoint 70/15/15 assignment
  (age-stratified, deterministic seed 1515) — see `DATA_LEAKAGE_AUDIT.md`.
- Ages 4–6 (5 speakers, 99 utterances sampled) are **held out of fitting** and reported as a
  limited external analysis only.
- The target is the **SIAK expert rating** (single annotator, 0–100). It is not called
  ground truth.

| split | utterances | speakers | ages |
|---|---:|---:|---|
| train | 2,094 | 113 | 7–12 |
| validation | 493 | 27 | 7–12 |
| test | 482 | 27 | 7–12 |
| ages 4–6 external | 99 | 5 | 4–6 |

## 2. SIAK rating semantics (before modeling)

Test-split distribution: mean 61.9, std 26.9, min 1, max 99, 40.7% ≥80, 13.9% ≥90,
30.5% <50, no exact 100. The rating is wide and not dominated by a single ceiling value in
this split (24 exact-100 items exist in the full release but were not sampled into test).
The mapping to soft-v2 is **weak and roughly monotone but noisy** (identity Spearman 0.27).

## 3. Candidates (feature ablations, smallest first)

| candidate | features | notes |
|---|---|---|
| identity | frozen soft score | baseline, no fitting |
| affine_soft | soft | least squares on train |
| isotonic_soft | soft | monotone PAVA (no negative slope possible) |
| linear_soft_conf | soft + confidence | |
| linear_soft_age | soft + age | |
| linear_soft_conf_age | soft + confidence + age | |
| linear_soft_conf_evidence | soft + confidence + exact/miss ratios + mean_sim + duration + n_hits | evidence features are CTC-derived |
| tree3_soft / tree3_full / tree3_noage | depth-3 decision tree | small tree |
| hgb_full / hgb_noage | shallow gradient boosting (depth 2, 80 iters) | hgb_noage = evidence + confidence, **no age** |

No neural network was trained; the wav2vec2 phone model was not touched.

## 4. Test results (speaker-disjoint, ages 7–12, n=482)

| candidate | Pearson | Spearman | MAE | bias | FRR (SIAK≥80 → pred<50) | FAR (SIAK<50 → pred≥80) | calib err (10-bin) |
|---|---:|---:|---:|---:|---:|---:|---:|
| identity | 0.307 | 0.272 | 27.27 | +6.80 | 12.2% | 26.5% | 20.1 |
| affine_soft | 0.307 | 0.272 | 21.38 | +2.62 | 2.6% | 0.0% | 3.8 |
| isotonic_soft | 0.315 | 0.279 | 21.20 | +2.69 | 2.6% | 0.0% | 2.7 |
| linear_soft_conf | 0.304 | 0.268 | 21.40 | +2.63 | 2.6% | 0.0% | 4.3 |
| linear_soft_conf_age | 0.312 | 0.283 | 21.26 | +2.61 | 2.6% | 0.0% | 3.3 |
| linear_soft_conf_evidence | 0.355 | 0.335 | 20.92 | +2.51 | 1.5% | 0.7% | 5.6 |
| tree3_full | 0.412 | 0.360 | 20.31 | +3.06 | 3.6% | 0.0% | 4.4 |
| tree3_noage | 0.412 | 0.360 | 20.31 | +3.06 | 3.6% | 0.0% | 4.4 |
| **hgb_noage** | **0.454** | **0.402** | **19.76** | +2.49 | 2.0% | 0.0% | 4.0 |
| hgb_full | 0.444 | 0.398 | 19.83 | +2.38 | 2.6% | 0.0% | 4.2 |
| hgb_soft | 0.333 | 0.299 | 21.11 | +2.73 | 3.1% | 0.0% | 2.9 |

Validation-split ordering agrees (hgb variants 0.41–0.44 Pearson; identity 0.32), so the
best candidate was not chosen only on test.

### Age conditioning verdict — **rejected as a required feature**

- `linear_soft_conf` vs `+age`: validation 0.324 vs 0.320; test 0.304 vs 0.312 (no stable gain).
- `tree3_full` and `tree3_noage` are identical (the tree never selects age).
- `hgb_full` (0.444) is slightly **worse** than `hgb_noage` (0.454) on test.
- Conclusion: age does not improve speaker-disjoint generalization. It may remain a
  display/diagnostics variable, not a model input. This matches the spec's rejection rule.

## 5. Per-age test correlation (hgb_noage vs identity)

| age | n | identity P | hgb_noage P | hgb_noage MAE |
|---|---:|---:|---:|---:|
| 7 | 40 | 0.142 | 0.190 | 22.7 |
| 8 | 93 | 0.181 | 0.443 | 20.5 |
| 9 | 165 | 0.414 | 0.554 | 19.9 |
| 10 | 105 | 0.260 | 0.445 | 18.3 |
| 11 | 59 | 0.342 | 0.342 | 19.2 |
| 12 | 20 | 0.142 | 0.418 | 18.4 |

Calibration gains appear at all ages but the ranking signal stays weakest for the youngest
primary ages (7–8), consistent with the child-speech gap.

## 6. Ages 4–6 external analysis (5 speakers, 99 utterances, never used for fitting)

| candidate | Pearson | MAE | FRR (≥80 → <50) | mean pred |
|---|---:|---:|---:|---:|
| identity | 0.184 | 31.84 | 42.9% | — |
| affine_soft | 0.184 | 26.24 | 14.3% | +10.4 bias |
| isotonic_soft | 0.128 | 26.59 | 14.3% | |
| linear_soft_conf_evidence | 0.281 | 25.12 | 7.1% | |
| hgb_noage | 0.336 | 24.18 | 17.9% | +6.8 bias |

Weakly supported: calibration trained on 7–12 transfers partially to 4–6-year-olds
(Pearson 0.28–0.34 vs 0.18 raw), but with only **5 speakers** this is directional, not
statistical evidence. The 4–6 FRR gap (14–18% after calibration vs 43% raw) is the clearest
practical effect and is consistent with the LWE-target age band, but must not be overstated.

## 7. Ceiling / compression warning

Calibrated predictions have `pred_std` 7.7–10.1 against `true_std` 26.9: the mapping
legitimately shrinks predictions because the evidence explains only a modest share of the
rating variance. The FRR reductions are therefore partly **variance compression**, and both
FRR and FAR must always be reported together (see `FRR_ANALYSIS.md`). No precision is
manufactured: the model never claims a rating range the evidence cannot support.

## 8. What is demonstrated / weakly supported

- **Demonstrated (speaker-disjoint, SIAK):** a lightweight calibration improves MAE
  (27.3→19.8), bias (+6.8→+2.5), FRR on human-good tokens (12.2%→2.0%) and rank correlation
  (0.27→0.40 Spearman) across unseen SIAK speakers, with the best candidate (`hgb_noage`)
  confirmed on validation and test.
- **Demonstrated:** age conditioning does **not** generalize (rejected).
- **Weakly supported:** transfer of calibration to ages 4–6 (5 speakers only).
- **Not addressed here:** external transfer to LWE task recordings
  (`EXTERNAL_LWE_VALIDATION.md`) and the rating's semantics as an absolute truth.
