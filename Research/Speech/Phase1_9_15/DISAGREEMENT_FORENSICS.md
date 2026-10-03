# DISAGREEMENT FORENSICS + P4 BOTTLENECK EVIDENCE

**Scripts:** `experiments/forensics.py`
**Artifacts:** `artifacts/forensics/disagreement_cases.csv` (73 cases),
`artifacts/forensics/forensics_summary.json`

Rule: no cause is claimed without evidence. Cases where no rule matches are marked
`unknown`. Model-inferred causes are labelled as such.

---

## 1. Case mix

| dataset | direction | n |
|---|---|---:|
| SIAK (expert rating) | SIAK_LOW_MODEL_HIGH (rating <50, calibrated pred ≥65) | 44 |
| SIAK (expert rating) | SIAK_HIGH_MODEL_LOW (rating ≥70, calibrated pred <50) | 9 |
| LWE (human review) | HUMAN_INCORRECT_MODEL_HIGH (pred ≥50) | 11 |
| LWE (human review) | HUMAN_CORRECT_MODEL_LOW (pred <60) | 9 |

## 2. SIAK pattern: the model under-uses the rating range

44/53 SIAK disagreement cases are the model predicting ~65–76 for utterances the expert rated
1–49. This is the compression seen in `POPULATION_CALIBRATION.md` §7: calibrated predictions
span a narrow band because the evidence explains only a fraction of the rating. Likely cause
classification (model-inferred from signals only): `n_miss ≥ 1` present in many cases; the
rest match no signal rule and remain `unknown`. The honest summary is:

- The model cannot distinguish "expert says very poor" from "acceptable" on a large share of
  low-rated utterances.
- This is consistent with (a) the SIAK rating capturing dimensions (effort, intelligibility,
  target word, noise) beyond the phone evidence, and (b) evidence limits — not fixable by
  calibration alone.

## 3. LWE pattern

- `HUMAN_INCORRECT_MODEL_HIGH` (11 cases): the frozen pipeline had already scored these high
  (SCORER_MISS class) or calibration lifted them above the error threshold. Documented causes
  from 1.9.9–1.9.12 (single reviewer): forced alignment cannot represent deleted final
  phones ("four" /r/ class), soft-match permissiveness, window sensitivity.
- `HUMAN_CORRECT_MODEL_LOW` (9 cases): mostly `PHONE_MODEL_ERROR` (1.9.9 diagnostic) —
  human hears a correct word, the phone model does not. These are the false rejections that
  the calibration reduces but cannot remove.

## 4. P4 — is the phone model the dominant error source?

Evidence assembled:

| evidence | number | source |
|---|---|---|
| 1.9.9 diagnostic PHONE_MODEL_ERROR (human correct, phone wrong) | 12 / 42 | frozen human review |
| 1.9.9 SCORER_MISS (human incorrect, score high) | 6 / 42 | frozen human review |
| Human-correct tokens with frozen soft <50 (80-token set) | 9 | 1.9.8 + 1.9.14 artifacts |
| SIAK test rank ceiling with monotone calibration | Spearman 0.279 (isotonic); 0.402 with rich features | this phase |
| LWE external AUC ceiling | 0.706 (hgb_noage) | this phase |
| Deletion-aware/GOP fix under FRR-first | contradicted (1.9.14 P2) | previous phase |

Interpretation (supported, not proven):
1. The **evidence layer** (phone posteriors + CTC alignment + soft aggregation) limits both
   SIAK and LWE rankings. A monotone transform of the soft score cannot exceed Spearman
   0.279 on SIAK; richer features reach 0.40; external LWE reaches AUC 0.71.
2. Calibration fixes bias/MAE/FRR but cannot create separation the evidence lacks.
3. The measured PHONE_MODEL_ERROR rate (12/42 = 28.6% of reviewed LWE tokens) and the
   deletion/alignment failures indicate the child phone evidence itself is a principal
   bottleneck, alongside assessability and single-reviewer label limits.

**Conclusion:** there is sufficient evidence to justify a **separate child phone model
adaptation research phase** (not performed here). There is not sufficient evidence to claim
the phone model is *the only* bottleneck; assessability, rating semantics and annotation
remain material.

## 5. What would falsify this

- A calibration model with access to new, non-CTC evidence (e.g. independent acoustic
  features, template matching) that lifts SIAK/LWE ranking substantially would point away
  from phone adaptation.
- More human reviewers on more tokens could shift the PHONE_MODEL_ERROR estimate; the
  current evidence is single-reviewer.
