# SESSION REPORT — Phase 1.9.15 (POPULATION CALIBRATION + INDEPENDENT CHILD VALIDATION)

> **Tóm tắt (VI):** Hiệu chỉnh soft-v2 trên SIAK với split speaker-disjoint (train 2.094/113sp,
> valid 493/27, test 482/27; 0 overlap, 0 duplicate audio); `hgb_noage` đạt test Pearson 0.454,
> MAE 19.76, FRR 12.2%→2.0%; age bị loại; external LWE AUC 0.659→0.706 nhưng FAR tăng mạnh.
> P1: pack mù mới 115 clip (loại mọi cặp bé+từ đã review), reviewer `human_maynode` (1 người):
> v1 false-gate 17/103 (16.5%), **v2 0/103**, nhưng v2 bỏ sót refusal 5/8 ca not-assessable.
> **Quyết định B = CALIBRATION_PROMISING_BUT_INSUFFICIENT** (+P4: child phone model research justified).
> Commits `4412c2f`, `019249f`, `c362558`, `3f62ef4`.

- **Date:** 2026-10-03
- **Machine:** ASUS (+ review on maynode over Tailscale)
- **Base:** Phase 1.9.14 `d594821`
- **Branch:** `ux/math-arenas-hotfix-20260930`
- **Scope:** research-only; no production change; no training
- **Flags:** all five false.

## 1. Mission
Can LWE learn from a population of children without learning the quirks of the children and
reviewers it already has? (P1 independent assessability validation; P2/P3 speaker-disjoint
calibration and external validation; P4 bottleneck analysis.)

## 2. P0 — Data leakage audit + split
- Script: `experiments/split_audit.py`
- SIAK expansion: 20 utt/speaker → **3,168 utt / 172 speakers** (`siak_expand.py`, reused 1,074
  rows from 1.9.14, scored 2,965 new; ~13 min CPU).
- Primary population ages 7–12: train **2,094 utt / 113 spk**, validation **493 / 27**, test
  **482 / 27**; ages 4–6 held out (**99 / 5**) as limited external.
- Leakage: speaker overlap ∅ (all pairs), file overlap ∅, duplicate audio **0 groups**, missing
  scores 0; target overlap 155 words reported as expected (speakers disjoint).

## 3. P1 — Independent fidelity validation (new blind set)
- Selection: `experiments/build_fidelity_review.py` — 115 recordings from Zenodo, **excluding every
  previously reviewed (speaker,target) pair**; 12 pre-declared strata (NO_SPEECH, VERY_SHORT,
  NEAR_SILENCE, SOFT, LOUD, NOISY, ASR_EMPTY 14, ASR_WRONG 10, PHONE_LOW 8, ORDINARY_HIGH/MID,
  SENTENCE, FREE_SPEECH); model scores hidden; Moonshine ASR + soft-v2 used only for stratification.
- Serving: `serve_review.py` on port 8769 (LAN + Tailscale); localStorage progress; reviewer
  submitted `Results/fidelity_v2_StageA_Filled.csv` (115 rows, 2026-10-03).
- **Results** (`merge_fidelity_review.py`):
  - valid attempts n=103 (strict 99); human NOT_ASSESSABLE 8; 0 UNCERTAIN labels (decisive).
  - **v1 false gate 17/103 (16.5%)** [FREE_SPEAK 9, UNINTELLIGIBLE 9]; **v2 0/103 (0%)**.
  - dangerous accept strict: v1 2/8 (25%), **v2 5/8 (62.5%)**; loose 75% / 87.5%.
  - v2 failures: four VERY_SHORT tokens, one ORDINARY_HIGH soft-100 but human-unintelligible,
    two free-speech → POSSIBLE (LOW).
  - single reviewer documented; rules not tuned on the set.
- Behavior audit before labels (`behavior_audit.py`): v1 would gate 20/115; v2 only 1; 35 ASR-empty
  → VALID/ASSESSABLE; 0 free-speech scored MEDIUM/HIGH.

## 4. P2 — Population calibration (speaker-disjoint)
- Script: `experiments/calibrate.py` — 13 candidates (identity → affine → isotonic → linear
  ablations → depth-3 tree → shallow HGB), fit on SIAK train only.
- **Test (n=482):**

| candidate | Pearson | Spearman | MAE | FRR (≥80→<50) | FAR (≥80 on <50) |
|---|---:|---:|---:|---:|---:|
| identity | 0.307 | 0.272 | 27.27 | 12.2% | 26.5% |
| affine | 0.307 | 0.272 | 21.38 | 2.6% | 0.0% |
| isotonic | 0.315 | 0.279 | 21.20 | 2.6% | 0.0% |
| linear +evidence | 0.355 | 0.335 | 20.92 | 1.5% | 0.7% |
| **hgb_noage** | **0.454** | **0.402** | **19.76** | 2.0% | 0.0% |

- Validation confirmed the ordering; predictions compressed (pred_std 7.7–10.1 vs true 26.9).
- **Age conditioning rejected**: validation neutral/worse; tree ignores age; hgb_noage ≥ hgb_full.
- Ages 4–6 (5 spk): identity P 0.184 / FRR 42.9% → hgb_noage 0.336 / 17.9% (weakly supported).

## 5. P3 — External LWE validation (fit SIAK only)
- Script: `experiments/external_lwe.py`; features recomputed on the 80 studio tokens.
- n=48 labeled (29 correct / 13 incorrect / 6 uncertain):
  - identity FRR 31.0% / FAR 38.5% / AUC 0.659; affine 13.8/69.2/0.659; isotonic 10.3/69.2/0.659;
    hgb_noage 3.4/84.6/**0.706**.
  - Matched-FRR ≤0.31: only hgb improves FAR (30.8% vs 38.5%); at FRR ≤0.10 no candidate usable.
- Fidelity-gate context: 7/29 human-correct are `POSSIBLE_ATTEMPT (LOW)`.

## 6. P4 — Bottleneck + forensics
- Script: `experiments/forensics.py` → 73 disagreement cases (44 SIAK low/model high, 9 high/low,
  11 human-incorrect high, 9 human-correct low).
- Evidence: 12/42 PHONE_MODEL_ERROR; SIAK monotone rank ceiling 0.279; LWE AUC ceiling 0.71;
  deletion-aware/GOP contradicted in 1.9.14 → child phone model adaptation justified as next phase.

## 7. Decision
**B = CALIBRATION_PROMISING_BUT_INSUFFICIENT** + P4 answer: child phone model research justified
(separate phase; not trained here). Production flags unchanged.

## 8. Artifacts (committed)
```
Research/Speech/Phase1_9_15/
  PHASE_1_9_15_REPORT.md, INDEPENDENT_FIDELITY_VALIDATION.md, POPULATION_CALIBRATION.md,
  EXTERNAL_LWE_VALIDATION.md, DISAGREEMENT_FORENSICS.md, DATA_LEAKAGE_AUDIT.md,
  MODEL_SELECTION.md, FRR_ANALYSIS.md
  experiments/ (10 scripts incl. review builders/server), artifacts/{siak,calibration,external,
  fidelity,forensics}, HumanReview/ (115 clips + HTML), Results/ (submission), manifests/
```

## 9. Git
- `4412c2f` calibrate child evidence and validate generalization
- `c362558` unlabeled system-behavior audit
- `019249f` independent fidelity validation analyzer
- `3f62ef4` independent fidelity validation and final calibration decision
- Pushed to branch; later merged to `main` (fast-forward `e8d96da`).

## 10. Environment
- Review server: port 8769 (LAN 192.168.50.89, Tailscale 100.102.186.96); reviewer `human_maynode`.
- Same frozen model/pipeline; no training anywhere in the calibration (fits on SIAK only).
