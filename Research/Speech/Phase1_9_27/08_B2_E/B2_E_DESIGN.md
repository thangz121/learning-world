# B2-E DESIGN (FROZEN — NO TRAINING) — WP-1.9.27 Part 15

> **Tóm tắt (VI):** B2-E = hybrid acoustic + phonetic (research-only): posterior encoder + temporal
> support + energy/voicing + formants (F1/F2/F3, F3-F2) + competitor margin + alignment position +
> duration + transition. Chuẩn hóa theo speaker/utterance, phân tách train/dev/test speaker-disjoint,
> mô hình công suất thấp (logistic/GBM nhỏ), baseline = CTC-only + production decision. Không được
> trở thành scorer production ngầm.

## Hypothesis

Landmark/phonetic features recover weak child final consonants (and /r/ rhoticity) that CTC
posteriors miss, while a low-capacity classifier plus an explicit UNCERTAIN band avoids the
blank-dominated false accepts.

## Feature groups (all from `pilot_features.csv` unless marked)

| group | features | ablation id |
|---|---|---|
| encoder-only | target_mean, target_max, top1_post, target_rank, margin_mean, peak_margin | E_ENC |
| temporal | cluster_width, longest_run_05, temporal_support, span_ms | E_TMP |
| energy/voicing | rms_span, rms_ratio | E_ACO |
| formants | f1_median, f2_median, f3_median (+ derived F3-F2, F3/F2) | E_PHO |
| competitor/blank | competitor_post, blank_mean, blank_at_peak | E_ENC+ |
| alternative encoder | alt_max, alt_mean, alt_delta | E_ALT |
| context | phone_class, age, split (never a feature: split) | – |
| transition | preceding-vowel-to-final transition cue (to be defined in B2-E implementation; research-only) | E_TMP |

`B2_E_FEATURE_SCHEMA.csv` carries the full mapping and normalization rules.

## Model and normalization (frozen)

- Model family: low-capacity only — regularized logistic regression or a shallow GBM; no deep
  heads, no stacking; a random-feature control must be run.
- Normalization: formants/energy z-scored per speaker where speaker data allows; posteriors used
  raw (they are comparable across speakers); no test statistics may enter normalization.
- Output classes: PRESENT / ABSENT / UNCERTAIN, with UNCERTAIN as an explicit, reported band.
- Minimum data: fitting only if >= 60 PRESENT + 60 ABSENT in train+dev; otherwise run unfitted
  (threshold-only) and mark INCONCLUSIVE.
- The hybrid must beat or tie the CTC-only baseline FRR-first; it must NOT be optimized to maximize
  recall alone.

## Why this is not a hidden production scorer

- Research-only code under `Phase1_9_27/experiments/`; production flags untouched.
- No deployment, no thresholds written back, no model file exported for the product.
- The pilot's purpose is GO/NO-GO for a future B2, not a shipped component.

## Non-goals

- No encoder fine-tuning (that is B2-C, GPU + licensed data, not approved).
- No claim about speakers outside the frozen local set and no Vietnamese-L1 claim.
