# EXTERNAL LWE VALIDATION

**Script:** `experiments/external_lwe.py`
**Artifacts:** `artifacts/external/external_lwe_features.csv`,
`external_lwe_validation.csv`, `external_lwe_metrics.json`

---

## 1. Protocol

- Calibration models are fitted **only on SIAK train speakers** (ages 7–12, 2,094
  utterances). No LWE label was used for fitting at any point.
- External data: LWE real-child studio number tokens, human-reviewed in 1.9.9–1.9.12.
  Features were **recomputed** with the frozen soft-v2 pipeline on the same 80 tokens so
  comparisons are apples-to-apples (48 tokens have a human overall verdict: 29 correct,
  13 incorrect, 6 uncertain).
- Human labels remain authoritative. Uncertain is never forced to correct/wrong.

## 2. Results (threshold 50 on calibrated score)

| candidate | FRR (human-correct → pred<50) | FAR (human-incorrect → pred≥50) | AUC(correct vs incorrect) | ordinal Pearson |
|---|---:|---:|---:|---:|
| identity (raw soft) | 31.0% | 38.5% | 0.659 | 0.158 |
| affine_soft | 13.8% | 69.2% | 0.659 | 0.158 |
| isotonic_soft | 10.3% | 69.2% | 0.659 | 0.205 |
| linear_soft_conf_evidence | 0.0% | 92.3% | 0.642 | 0.107 |
| hgb_noage | 3.4% | 84.6% | 0.706 | 0.261 |

Matched-FRR frontier (see `FRR_ANALYSIS.md`): only hgb_noage improves at FRR ≤ 0.31
(FAR 30.8% vs 38.5%); at FRR ≤ 0.10 no candidate yields usable FAR.

## 3. Interpretation

- **The SIAK-trained calibration transfers only partially.** It fixes the *scale bias*
  (mean prediction for human-incorrect rises from 41.8 raw to 57.7–59.8 calibrated while
  human-correct stays ~62–64) but does not separate correct from incorrect much better than
  the raw score (AUC 0.659 → 0.706 at best).
- The LWE human verdict and the SIAK expert rating encode different things: LWE reviewers
  judged pronunciation of the requested word with a specific error rule (e.g. deleted final
  sound = fail), while SIAK rates general pronunciation quality. A calibration learned on one
  is not the other; this is a **categorical** generalization gap, not a bug.
- Human-uncertain LWE tokens (6) all receive mid-range calibrated scores; the calibration
  does not manufacture confidence on them, which is the desired behaviour.
- **Externally validated conclusion:** calibration is not validated as an error detector for
  LWE. It is validated only as a bias-reducing transform within the SIAK population.

## 4. Fidelity-gate integration context (not a production proposal)

7 of the 29 human-correct tokens are `POSSIBLE_ATTEMPT (LOW)` under the 1.9.14 v2
assessability rules (`child_01_seven`, `child_01_ten`, `child_03_eight`, `child_04_three`,
`child_06_six`, `child_06_two`, `child_07_one`). Under the proposed gating these would be
CANNOT_ASSESS, not false rejections. This is exactly why assessability and pronunciation
must remain separate layers: the LWE false-rejection problem is partly an assessability
problem, partly an evidence problem (P4).

## 5. Caveats

- n = 48 labeled tokens, 13 incorrect; single reviewer.
- Recomputation confirms the frozen pipeline, but the external features are not from the
  live game path (research reconstruction).
- No score is proposed for children by this document.
