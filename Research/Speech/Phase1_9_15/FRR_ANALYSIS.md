# FRR-FIRST ANALYSIS — Phase 1.9.15

**Data:** `artifacts/calibration/metrics.csv`, `artifacts/external/external_lwe_metrics.json`,
`artifacts/external/external_lwe_validation.csv`, `artifacts/forensics/disagreement_cases.csv`

FRR = false rejection of **human-confirmed-correct child speech**. All thresholds are stated
explicitly; FRR is never averaged away against correlation.

---

## 1. SIAK (speaker-disjoint test, ages 7–12, n=482)

FRR proxy = P(pred < 50 | SIAK rating ≥ 80). FAR proxies are also shown.

| candidate | FRR (<50 on ≥80) | FRR (<40 on ≥80) | FAR (≥80 on <50) | FAR (≥50 on <50) |
|---|---:|---:|---:|---:|
| identity (raw soft) | 12.2% | 10.7% | 26.5% | 60.5% |
| affine_soft | 2.6% | 0.0% | 0.0% | 85.7% |
| isotonic_soft | 2.6% | 0.0% | 0.0% | 87.8% |
| linear_soft_conf_evidence | 1.5% | 0.0% | 0.7% | 87.1% |
| hgb_noage | 2.0% | 0.5% | 0.0% | 74.8% |

The `≥50` FAR rises because calibration compresses predictions toward the middle/upper band —
the same mechanism that lowers FRR. **FRR improvement alone is not evidence of a better
system; it is half of a trade.**

## 2. Ages 4–6 external (99 utterances, 5 speakers, never used for fitting)

| candidate | FRR (<50 on ≥80) | bias | MAE |
|---|---:|---:|---:|
| identity | 42.9% | +4.0 | 31.8 |
| affine_soft | 14.3% | +10.4 | 26.2 |
| linear_soft_conf_evidence | 7.1% | +9.0 | 25.1 |
| hgb_noage | 17.9% | +6.8 | 24.2 |

Directionally, calibration reduces rejection of human-good 4–6yo tokens, but this is
**5 speakers** and cannot be called statistical evidence. The +7 to +10 score bias shows the
7–12-trained mapping over-predicts for the youngest group (age conditioning was rejected as
a model feature, so this bias is left explicit, not hidden).

## 3. LWE real-child external validation (human verdicts; n=29 correct, 13 incorrect, 6 uncertain)

At a fixed threshold of 50 on the calibrated score:

| candidate | FRR on human-correct | FAR on human-incorrect | AUC(correct vs incorrect) | ordinal Pearson |
|---|---:|---:|---:|---:|
| identity (raw soft) | 31.0% | 38.5% | 0.659 | 0.158 |
| affine_soft | 13.8% | 69.2% | 0.659 | 0.158 |
| isotonic_soft | 10.3% | 69.2% | 0.659 | 0.205 |
| linear_soft_conf_evidence | 0.0% | 92.3% | 0.642 | 0.107 |
| hgb_noage | 3.4% | 84.6% | **0.706** | **0.261** |

Matched-FRR frontier (min FAR at a given FRR cap):

| candidate | FAR at FRR ≤ 0.31 | FAR at FRR ≤ 0.10 | FAR at FRR = 0 |
|---|---:|---:|---:|
| identity | 38.5% | 100% | 100% |
| affine_soft | 38.5% | 100% | 100% |
| isotonic_soft | 38.5% | 100% | 100% |
| linear_soft_conf_evidence | 38.5% | 76.9% | 92.3% |
| hgb_noage | **30.8%** | 84.6% | 100% |

Reading (external, small n):
- Calibration **reduces false rejection of correct LWE tokens** but at a large FAR cost; at
  FRR ≤ 0.31 only `hgb_noage` slightly improves FAR (30.8% vs 38.5%).
- At strict FRR ≤ 0.10 no candidate gives usable FAR (77–100%).
- AUC improves modestly: 0.659 → 0.706 (hgb_noage); the ordinal correlation is weak
  (0.10–0.26) on 48 labeled tokens.
- **Fidelity-gate context:** 7 of the 29 human-correct tokens are `POSSIBLE_ATTEMPT (LOW)` in
  the Phase 1.9.14 v2 assessability layer. With the recommended gate they become
  CANNOT_ASSESS rather than false rejections; the engine may still compute internally.

## 4. Verdict under the FRR-first rule

- A candidate that lowers FRR while raising FAR this much is **not automatically an
  improvement**. On SIAK the trade is dominated by variance compression; on LWE the external
  gain is small and the error-detection ability at useful FRR is absent.
- What survives: calibration is useful for **bias/MAE correction and for avoiding rejection
  of correct speech**; it is not yet a validated pronunciation-error detector for LWE.
- Nothing here justifies changing the production scorer, thresholds, or child-facing verdicts.
