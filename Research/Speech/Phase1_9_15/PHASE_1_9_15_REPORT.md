# PHASE 1.9.15 — POPULATION CALIBRATION + INDEPENDENT CHILD VALIDATION

**Date:** 2026-10-03 · **Machine:** ASUS (+ review on maynode over Tailscale) ·
**Branch:** `ux/math-arenas-hotfix-20260930`
**Base:** Phase 1.9.14 `d594821` · **HEAD at start:** `019249f`
**Flags (unchanged):** `production_vad=false · router_locked=false · unity_integrated=false ·
scorer_modified=false · production_window_locked=false`

## Decision

### B = CALIBRATION_PROMISING_BUT_INSUFFICIENT

**P4 answer (success criterion 7):** yes — the evidence justifies a **separate
child-phone-model adaptation research phase** (nothing was trained here). This is the
equivalent of decision D at the objective level, but the phase's overall decision is B
because the calibration itself is promising in-population and insufficient for external
generalization.

---

## 1. Mission

Can LWE learn from a population of children without learning the quirks of the children and
reviewers it already has? Two independent questions: (P1) does the 1.9.14 assessability
result survive on a NEW blind child set, and (P2/P3) can a lightweight calibration of the
frozen evidence generalize to unseen children.

## 2. What was run

| Objective | What | Data | Runtime |
|---|---|---|---|
| P0 | Leakage audit + strict speaker-disjoint split | SIAK 3,168 utterances / 172 speakers | s |
| P2 | 13 calibration candidates (identity → affine → isotonic → linear ablations → depth-3 tree → shallow boosting) | SIAK train 2,094 / valid 493 / test 482, ages 7–12 | s |
| P2b | Age-conditioning ablation (with vs without), per-age metrics | same | s |
| P3 | External validation of SIAK-fitted models (no LWE fitting) | LWE human-reviewed 48 tokens (29 correct / 13 incorrect / 6 uncertain) | ~2 min |
| P1 | NEW blind fidelity/assessability pack, 115 clips, 12 strata, LAN/Tailscale server | real-child corpus excluding all previously reviewed pairs | ~15 min compute + 1 review session |
| P4 | Disagreement forensics + phone-model bottleneck evidence | SIAK test + LWE + 1.9.9–1.9.14 artifacts | s |

## 3. Required comparison table (spec §19)

SIAK: FRR = P(pred<50 | rating≥80); FAR = P(pred≥80 | rating<50). LWE: threshold 50 on the
calibrated score. No candidate is ranked by a single "overall score".

| Candidate | Train population | Test population | Speaker-disjoint | Pearson | Spearman | MAE | FRR | FAR | Notes |
|---|---|---|---|---|---|---|---|---|---|
| identity (soft-v2) | — | SIAK test n=482 | yes | 0.307 | 0.272 | 27.27 | 12.2% | 26.5% | frozen baseline |
| affine_soft | SIAK train | SIAK test | yes | 0.307 | 0.272 | 21.38 | 2.6% | 0.0% | monotone; compression |
| isotonic_soft | SIAK train | SIAK test | yes | 0.315 | 0.279 | 21.20 | 2.6% | 0.0% | monotone |
| linear_soft_conf_evidence | SIAK train | SIAK test | yes | 0.355 | 0.335 | 20.92 | 1.5% | 0.7% | CTC-derived features |
| **hgb_noage** | SIAK train | SIAK test | yes | **0.454** | **0.402** | **19.76** | 2.0% | 0.0% | best; age not used |
| identity (soft-v2) | — | SIAK 4–6 n=99 | 5 speakers only | 0.184 | 0.195 | 31.84 | 42.9% | 15.7% | limited external |
| hgb_noage | SIAK 7–12 train | SIAK 4–6 | no (different age) | 0.336 | 0.313 | 24.18 | 17.9% | 0.0% | directional only |
| identity (soft-v2) | — | LWE human n=48 | external | — (AUC 0.659) | ord. 0.155 | — | 31.0% | 38.5% | frozen baseline |
| affine_soft | SIAK train | LWE human | external | — (AUC 0.659) | ord. 0.155 | — | 13.8% | 69.2% | bias shifted |
| isotonic_soft | SIAK train | LWE human | external | — (AUC 0.659) | ord. 0.148 | — | 10.3% | 69.2% | bias shifted |
| linear_soft_conf_evidence | SIAK train | LWE human | external | — (AUC 0.642) | ord. 0.108 | — | 0.0% | 92.3% | over-accepts |
| **hgb_noage** | SIAK train | LWE human | external | — (AUC **0.706**) | ord. **0.263** | — | 3.4% | 84.6% | best external AUC |

## 4. P1 — Independent assessability validation (NEW blind set)

115 new recordings (all previously reviewed (speaker,target) pairs excluded), single reviewer
(`human_maynode`), no model feedback in the UI; rules v1/v2 frozen from 1.9.14.

| metric | v1 | v2 |
|---|---:|---:|
| false gate on human-valid attempts (n=103) | 17 (16.5%) | **0 (0.0%)** |
| human NOT_ASSESSABLE (n=8) | — | — |
| dangerous accept strict (system VALID/ASSESSABLE) | 2/8 (25.0%) | 5/8 (62.5%) |

- **Survived:** v2 does not falsely gate human-valid child speech (0/103 independently
  confirmed; strictly 0/99). This is the first independent validation of the 1.9.14
  false-gate result, not a reuse of the same corpus.
- **Failed:** v2 under-refuses. 5/8 human-unintelligible recordings pass as `VALID_ATTEMPT`;
  four are very-short tokens, one is a soft-score-100 outlier the human could not understand
  (evidence failure, not rule ordering).
- Trade is explicit: v1 catches more unusable audio but rejects 16.5% of valid child speech;
  under FRR-first, v2's direction is right but it is **not** a validated assessability gate.
- Single reviewer, 0 UNCERTAIN labels; inter-rater agreement not computable (documented).
- Details: `INDEPENDENT_FIDELITY_VALIDATION.md`, `artifacts/fidelity/`.

## 5. P2/P3 — Calibration and external generalization

- Speaker-disjoint SIAK calibration works and was confirmed on validation **and** test:
  Pearson 0.307→0.454, Spearman 0.272→0.402, MAE 27.3→19.8, bias +6.8→+2.5, FRR 12.2%→2.0%.
- The gain comes partly from **variance compression** (pred_std 7.7–10.1 vs true_std 26.9);
  FRR/FAR must always be read together.
- **Age conditioning rejected**: validation neutral/slightly worse, tree ignores age,
  `hgb_noage` ≥ `hgb_full`; 4–6-year-olds show a +7 to +10 bias under a 7–12-trained mapping.
- **External LWE transfer is weak**: AUC 0.659→0.706; at FRR ≤ 0.10 no candidate gives usable
  FAR (77–100%). Calibration is not validated as an LWE error detector.
- Leakage audit: 0 speaker/file overlap, 0 duplicate audio, 0 missing scores; ages 4–6 and
  LWE never used for fitting.

## 6. P4 — Bottleneck

| evidence | value | source |
|---|---|---|
| PHONE_MODEL_ERROR among reviewed LWE tokens | 12/42 (28.6%) | 1.9.9 human review |
| SCORER_MISS | 6/42 | 1.9.9 |
| SIAK monotone rank ceiling (isotonic Spearman) | 0.279 | this phase |
| SIAK best rank with rich features | 0.402 | this phase |
| LWE external AUC ceiling | 0.706 | this phase |
| deletion-aware/GOP fix | contradicted under FRR-first | 1.9.14 |

Conclusion: the **evidence layer (phone posteriors + CTC alignment + aggregation) is a
principal bottleneck**, alongside the newly measured assessability refusal gap and
single-reviewer label limits. A separate **child phone model adaptation** phase is justified
as the next experiment; it was not started here.

## 7. Success-criteria answers

1. **Does 1.9.14's assessability result survive on a NEW blind child set?** Partially:
   no-false-gate survived (0/103); refusal detection did not (5/8 dangerous strict).
2. **Can soft-v2 be calibrated on SIAK without speaker leakage?** Yes — 0 overlap/duplicates;
   best candidate confirmed on validation and test.
3. **Does calibration generalize to unseen LWE child speech?** Insufficient: AUC 0.66→0.71,
   no usable error detection at FRR ≤ 0.10.
4. **Does it reduce bias without increasing FRR?** On SIAK yes (bias/MAE/FRR down), partly via
   compression; on LWE FRR drops but FAR rises to 69–92%.
5. **Is age conditioning genuinely useful out-of-sample?** No — rejected.
6. **Main remaining errors caused primarily by?** Phone evidence/alignment (P4), then the
   assessability refusal gap and annotation/single-reviewer limits.
7. **Enough evidence to justify a child-adapted phone model?** Yes, as a separate research
   experiment (not performed).
8. **Highest-value next experiment?** Child phone model adaptation + intelligibility/quality
   evidence for assessability refusal.

## 8. Honest limitations

- Single reviewer for both the historical labels and the new 115-item pack; no kappa.
- LWE external n = 48 with 13 incorrect; SIAK 4–6 = 5 speakers.
- `hgb_noage` beats validation and test, but the candidate family is small and chosen after
  seeing validation; calibration curves are provided raw (`calibration_curve_rows.csv`).
- SIAK is a single-annotator expert rating, not ground truth; all correlations are
  upper-bounded by label noise.
- The review pack had only 2 available near-silence items (the other 3 such recordings shared
  reviewed speaker+target pairs) — refusal-state coverage is thin.

## 9. Artifacts

```
Research/Speech/Phase1_9_15/
  PHASE_1_9_15_REPORT.md            this file
  INDEPENDENT_FIDELITY_VALIDATION.md
  POPULATION_CALIBRATION.md
  EXTERNAL_LWE_VALIDATION.md
  DISAGREEMENT_FORENSICS.md
  DATA_LEAKAGE_AUDIT.md
  MODEL_SELECTION.md
  FRR_ANALYSIS.md
  experiments/                      siak_expand, split_audit, calibrate, external_lwe,
                                    forensics, build_fidelity_review, behavior_audit,
                                    merge_fidelity_review, serve_review
  artifacts/siak/                   calibration_train/valid/test/ages46, split_manifest,
                                    siak_expanded_scored (3,168)
  artifacts/calibration/            metrics.csv/json, calibration_curve_rows.csv
  artifacts/external/               external_lwe_features/validation/metrics
  artifacts/fidelity/               fidelity_independent.csv, independent_metrics.json,
                                    system_behavior_audit, selection_table, candidate_pool
  artifacts/forensics/              disagreement_cases.csv, forensics_summary.json
  artifacts/metrics.json            consolidated machine-readable metrics
  manifests/provenance.json         input/output hashes
  HumanReview/                      fidelity_blind.html + 115 clips + review_metadata
  Results/                          fidelity_v2_StageA_Filled.csv (reviewer submission)
```

## 10. Language discipline

"Demonstrated/supported" = speaker-disjoint or independently human-labeled evidence;
"weakly supported" = 5 speakers / small n; "insufficient/not validated" = calibration on LWE
and refusal detection. Human review remains authoritative; no label was invented, no LLM was
used as an authority, and no child audio left the two project machines.
