# PHASE 1.9.18 — FINAL REPORT (RECONSTRUCTION, EVIDENCE-BASED)

**Date:** 2026-10-04 · **Machine:** ASUS · **Branch:** `phase1-9-16-recovery`
**HEAD at reconstruction:** `584eb2d` (local == `origin/phase1-9-16-recovery`, 0/0)
**Type:** Research reconstruction only. No Unity, scorer, VAD, router, or production change.
**Production locks (verified in every phase artifact):**
`production_vad=false · router_locked=false · unity_integrated=false ·
scorer_modified=false · production_window_locked=false`

---

## 1. Executive decision

Across Phase 1.9.14 → 1.9.18 the LWE child-pronunciation research program:

- **Established** that a fidelity/assessability separation is needed and that ASR must not
  judge pronunciation (1.9.14 v1 vs v2; independently re-tested in 1.9.15).
- **Failed to justify** the specific scorer changes proposed in 1.9.13 (deletion-aware /
  GOP evidence) under FRR-first (1.9.14 P2).
- **Measured** that the frozen scorer correlates only weakly with child human scores
  (SIAK speaker-disjoint Pearson 0.307, calibration ceiling 0.454) and **fails to
  generalize externally** (LWE AUC 0.706, no usable FAR at FRR ≤ 0.10) — 1.9.15 P2/P3.
- **Blocked** a supervised child phone adaptation in 1.9.16 for lack of a genuine phone
  tier (decision D = DATA_INSUFFICIENT).
- **Unblocked the data question** in 1.9.17: SpeechOcean762 provides a 5-expert phone tier
  (decision A = SO762_PROVIDES_A_GENUINE_CHILD_PHONE_TIER).
- **Ran the first B1 head-only adaptation on SO762** in 1.9.18 and it **regressed** the
  FRR-first metric (decision **E = B1_REGRESSES_HUMAN_CORRECT_CHILD_SPEECH**).

**Net:** no child pronunciation model, calibration, or scorer change is evidence-backed
for production. **Production integration is NOT allowed. PHASE 5 (Unity) must not start.**

---

## 2. Phase timeline (evidence-based)

| Phase | Commits | Date | Objective | Outcome (measured) | Decision |
|---|---|---|---|---|---|
| 1.9.14 | `d594821` | 2026-10-03 | Fidelity/assessability layer + deletion-aware evidence + SIAK calibration | P1 v2 false-gate 0/30; P2 deletion FRR-worse; P3 SIAK Pearson 0.226 | **B = RESEARCH_SUPPORTS_PARTIAL_CHANGE** |
| 1.9.15 | `4412c2f`,`019249f`,`c362558`,`3f62ef4` | 2026-10-03 | Population calibration + independent child validation | Independent v2 false-gate 0/103 (survived); refusal 5/8 fail; SIAK calib 0.454 in-pop; LWE transfer weak (AUC 0.706) | **B = CALIBRATION_PROMISING_BUT_INSUFFICIENT** |
| 1.9.16 | `1a8cafd`,`6eb0788`,`a88a198` | 2026-10-03 | Child phone model adaptation attempt (recovery) | No phone labels anywhere; baseline 482/482 reproduced; **no training run** | **D = DATA_INSUFFICIENT_FOR_CHILD_ADAPTATION** |
| 1.9.17 | `d45d3ed`,`584eb2d` | 2026-10-04 | SO762 phone-tier audit | 5,000 utt / 250 spk / 122 child spk / 67 phones mapped 67/67; 3,403 bad tokens | **A = SO762_PROVIDES_A_GENUINE_CHILD_PHONE_TIER** |
| 1.9.18 | `2bb9707` | 2026-10-04 | Zero-shot baseline + B1 head-only adaptation | Baseline FRR 0.227; B1 FRR 0.299 (**+0.072 worse**) | **E = B1_REGRESSES_HUMAN_CORRECT_CHILD_SPEECH** |

Note: 1.9.15–1.9.18 ran on branch `phase1-9-16-recovery` (base `3f62ef4`), not the older
`ux/math-arenas-hotfix-20260930` branch that 1.9.13 and 1.9.14 used.

---

## 3. Evidence matrix

Status key: **PASS** = demonstrated on the stated data · **FAIL** = contradicts the claim
or regressed · **PARTIAL** = direction shown, statistically weak/incomplete ·
**BLOCKED** = data/license/access unavailable · **NOT RUN** = never executed.

| # | Experiment | Phase | Artifact | Measured result | Status |
|---|---|---|---|---|---|
| 1 | Fidelity v2 false-gate on human-valid child speech | 1.9.14/1.9.15 | `Phase1_9_15/artifacts/fidelity/independent_metrics.json` | 0/103 (independent) | **PASS** |
| 2 | Fidelity v2 refusal detection | 1.9.15 | same | 5/8 NOT-ASSESSABLE accepted (dangerous) | **FAIL** |
| 3 | Deletion-aware Viterbi (zero penalty) FRR | 1.9.14 | `Phase1_9_14/artifacts/p2/p2_frr_analysis.json` | FRR 1/16 → 7/16 | **FAIL** |
| 4 | Deletion margin (ranking) | 1.9.14 | same | AUC 0.839 vs 0.760, no improving operating point | **PARTIAL** |
| 5 | GOP-ratio final-consonant evidence | 1.9.14 | same | AUC 0.740; FAR 91.7% at FRR-matched | **FAIL** |
| 6 | SIAK speaker-disjoint calibration (in-population) | 1.9.15 | `Phase1_9_15/artifacts/calibration/metrics.json` | Pearson 0.307→0.454, FRR 12.2%→2.0% | **PARTIAL** (variance compression) |
| 7 | Age conditioning | 1.9.15 | same | rejected (no out-of-sample gain) | **FAIL** (as a feature) |
| 8 | External LWE transfer of SIAK calibration | 1.9.15 | `artifacts/external/external_lwe_metrics.json` | AUC 0.659→0.706; FAR 77–100% at FRR≤0.10 | **FAIL** (not usable) |
| 9 | Supervised child phone adaptation (B1) | 1.9.16 | `Phase1_9_16/CHILD_ADAPTATION_RESULTS.md` | NOT RUN (no phone labels) | **BLOCKED** |
| 10 | Zero-shot frozen baseline reproduction (SIAK) | 1.9.16 | `baseline_metrics.json` | 482/482 exact, FRR-proxy 0.1224 | **PASS** |
| 11 | SO762 phone-tier existence + inventory mapping | 1.9.17 | `so762_phone_tier_audit.json` | 67/67 mapped, 122 child spk, 514 child bad tokens | **PASS** |
| 12 | SO762 audio-format compatibility | 1.9.17 | `so762_format_probe.json` | 40/40 mono 16k/16-bit PCM | **PASS** |
| 13 | Zero-shot frozen baseline on SO762 child test | 1.9.18 | `zero_shot_baseline.json` | 480/480 scored; FRR 0.2272 / FAR 0.2708 | **PASS** (as measurement) |
| 14 | B1 head-only adaptation on SO762 child test | 1.9.18 | `b1_test_metrics.json` | FRR 0.2988 (**+0.0716**); FAR 0.1042 | **FAIL** |
| 15 | LWE transfer of B1 | 1.9.18 | `lwe_transfer.json` | raw child audio gitignored | **BLOCKED** |
| 16 | SIAK transfer of B1 | 1.9.18 | `siak_transfer.json` | ND review open | **BLOCKED** |

---

## 4. SO762 findings

- **What subset:** the HF mirror `mispeech/speechocean762` (OpenSLR SLR101),
  revision `06385584fad212b26134c656fdd3ccf9f093f33e`, 5,000 utt / 250 spk.
  Phase 1.9.18 used a **speaker-disjoint child-only split** (seed 1515): train 80 spk/1,600
  utt, valid 18 spk/360, test 24 spk/480; 128 adult speakers excluded.
- **Adult vs child composition:** corpus = 122 child (≤15) / 128 adult (>15) speakers;
  2,440 child / 2,560 adult utterances. Child ages **6–15** (no 4–5). Mandarin-L1.
- **Labels:** 5-expert per-phone accuracy 0/1/2 (averaged → floats), plus observed
  substitution/deletion/unknown records **only** when score < 0.5. Not a dense observed
  transcript.
- **Target evaluated:** per-canonical-phone expert correctness (expert acc ≥ 1.0) vs
  expert-bad (< 0.5) via the frozen pipeline's per-phone similarity.
- **Model/pipeline:** frozen `wav2vec2-xlsr-53-espeak-cv-ft@2c73378` through
  `PhoneEvidenceV2@1.4.0` (unchanged).
- **Baseline:** frozen zero-shot; **FRR 0.2272 / FAR 0.2708** on 7,728 correct / 48 bad
  child phone tokens.
- **Speaker-disjoint:** yes (verified overlap `[]`, leakage `[]`).
- **Statistically meaningful?** FRR on 7,728 correct tokens: yes for FRR. FAR on **48**
  bad tokens: **no** — FAR deltas are not robust and are not treated as such.
- **What SO762 validates:** (a) a genuine expert phone tier exists; (b) SO762 audio is
  natively compatible with the pipeline; (c) a **frozen adult-trained pipeline still
  false-rejects 22.7%** of expert-correct Mandarin-L1 child phones; (d) B1 head-only over
  frozen features **worsens** this.
- **What SO762 does NOT validate:** LWE/Vietnamese-age-4 pronunciation; phone recognition
  BER; the production scorer; child pronunciation scoring for LWE.
- **Do not conflate:** ASR accuracy ≠ phone recognition ≠ pronunciation assessment ≠
  **child** pronunciation assessment ≠ **Vietnamese-L1 age-4** child assessment.

---

## 5. Child adaptation findings (STEP 4 audit)

Strict answers:

1. **Real child speech?** Yes — SO762 Mandarin-L1 children (toys/numbers/sentences).
2. **Ages?** 6–15. **No 4-year-olds.**
3. **Speakers?** 122 child speakers total; 80/18/24 train/valid/test.
4. **Speaker-disjoint evaluation?** Yes.
5. **Human pronunciation labels?** Expert cell scores, not dense phone transcriptions.
6. **Held-out test set?** Yes (24 speakers, 480 utt).
7. **Baseline?** Yes — frozen zero-shot (FRR 0.2272).
8. **Statistically meaningful comparison?** For FRR, yes (7,728 tokens). For FAR, no (48).
   No confidence intervals were computed; treat as directional.
9. **Did adaptation improve FRR/UAR/PCC/Spearman/MAE?** **No** — FRR worsened by +0.0716.
   PCC/Spearman/MAE were not computed against dense phone truth (not available).
10. **Did it improve human-correct child tokens?** **No** — 1,756 → 2,309 rejected.
11. **Did it reduce false rejection?** **No** — it increased it.
12. **Did it introduce false acceptance?** It **reduced** false acceptance
    (13 → 5), but only by rejecting more correct speech.
13. **Trained/adapted/calibrated or merely tested?** A small head **was trained**
    (validation AUC 0.7999) over a **frozen** encoder; the encoder was neither adapted nor
    calibrated. PhoneEvidenceV2@1.4.0 was not modified.
14. **Can it justify production use?** **No.** Decision E; B2 not justified.

**"Child pronunciation assessment solved" is NOT supported.** Training completed ≠ works.

---

## 6. Zero-shot findings

- Frozen pipeline reproduced bit-exactly on SIAK test (482/482, 1.9.16) and scored
  480/480 SO762 child-test utterances with 0 errors (1.9.18).
- SO762 child test: mean soft 75.78, mean confidence 0.0763 (low), **FRR 0.2272**.
- LWE human-reviewed reference (committed): human-correct low-score rate 0.3103 (n=29);
  human-incorrect high-score rate 0.3846 (n=13); AUC 0.659.
- The frozen scorer is **consistent** and **weak on child speech** — the exact problem
  the adaptation line tried (and failed) to fix.

---

## 7. Human-label evidence

- 1.9.9–1.9.12: single-reviewer labels (mobile) on LWE child tokens; 12 PHONE_MODEL_ERROR
  + 6 SCORER_MISS; final-consonant present/absent counts.
- 1.9.14 P1: 49 human-labeled child tokens + IMG stress → v1/v2 comparison.
- 1.9.15 P1: **new independent** 115-clip blind pack (`human_maynode`), 12 strata;
  v2 false-gate 0/103; refusal gap 5/8.
- All single-reviewer; **no inter-rater agreement (kappa) computed** — documented openly.
- Historical forensic replay in 1.9.18 was **NOT COMPUTABLE** because the underlying raw
  child audio is gitignored.

---

## 8. Metrics (consolidated)

| metric | value | source |
|---|---|---|
| SIAK zero-shot Pearson (speaker-disjoint) | 0.307 | 1.9.15 |
| SIAK calibrated Pearson (hgb_noage) | 0.454 | 1.9.15 |
| SIAK FRR-proxy frozen | 0.122 | 1.9.16 |
| LWE external AUC (frozen → best) | 0.659 → 0.706 | 1.9.15 |
| LWE human-correct low-score rate | 0.3103 | 1.9.15 |
| SO762 child test baseline FRR / FAR | 0.2272 / 0.2708 | 1.9.18 |
| SO762 child test B1 FRR / FAR | 0.2988 / 0.1042 | 1.9.18 |
| B1 validation AUC | 0.7999 | 1.9.18 |

No UAR/PCC/Spearman/MAE on dense phone truth exists — such labels do not exist in these
corpora (declared NOT_COMPUTABLE, never faked).

---

## 9. Runtime / CPU evidence

| item | value | source |
|---|---|---|
| frozen model size | ~1.27 GB | 1.9.16 |
| frozen inference | 0.405 s/utt (SIAK), 0.944 s/utt (SO762 child) | 1.9.16/1.9.18 |
| zero-shot SO762 wall | 780.9 s / 480 utt | 1.9.18 |
| B1 head training | 35.4 s (frozen features pre-cached) | 1.9.18 |
| hardware | CPU-only (Xeon), no CUDA | 1.9.16 |
| full fine-tuning on CPU | not credible without a hardware plan | 1.9.16 |

B1 was CPU-feasible; the blocker was never hardware.

---

## 10. License / model constraints

- **wav2vec2-xlsr-53-espeak-cv-ft**: Apache-2.0 — CLEAR.
- **CMUdict**: BSD-like — CLEAR.
- **SIAK**: CC-BY-ND-4.0 — training/evaluation rights review **open since 1.9.14**
  (BLOCKED). SIAK used for validation/calibration only; never as phone supervision.
- **SpeechOcean762 / OpenSLR SLR101**: permissive (OpenSLR states CC BY 4.0, free
  commercial; HF card front-matter declares apache-2.0). **Discrepancy recorded, not
  resolved.** Neither is ND.
- **LWE child audio (Zenodo 200495 + review clips)**: project-internal, privacy-sensitive,
  **never committed**; raw audio gitignored by policy.
- B1 checkpoint is a small head over permissive-license SO762 with no audio; retained as
  `CHECKPOINT_RETAINED_RESEARCH_ONLY`. No production checkpoint recommended.

---

## 11. Architecture implications (vs the 1.9.13 proposed pipeline)

```
CHILD AUDIO
 → AUDIO QUALITY            UNPROVEN   (no clipping/SNR metric ever tested)
 → VAD / EOS                BLOCKED(frozen) — VAD-negative ≠ no speech (IMG 28/28 speech); router not calibrated
 → ASSESSABILITY / FIDELITY JUSTIFIED  (needed: 26.7% v1 false-gate; independent 0/103 false-gate)
                            PARTIAL    (refusal side unproven: 5/8 dangerous accepts)
 → MULTI-EVIDENCE          UNPROVEN   (soft-v2 child calibration Pearson ≤0.31; no child model)
 → DELETION-AWARE EV/DIAG  REJECTED   (as scorer change: FRR-worse, no improving operating point)
 → DIAGNOSTIC FUSION       UNPROVEN   (deletion representable only as research)
 → CONFIDENCE              UNPROVEN   (low confidence ≠ bad pronunciation; mean conf 0.076 at SO762)
 → CHILD DECISION          UNPROVEN   (design only; no human spot-check)
 → PARENT MODE             UNPROVEN   (design only)
```

Multi-evidence phone adaptation — the intended fix — is **UNPROVEN/REJECTED for B1** and
**BLOCKED** for LWE transfer.

---

## 12. What LWE should adopt (research-grade, not production)

- The **rule** that fidelity/assessability precedes pronunciation feedback, and that
  **ASR is not a pronunciation judge** (PASS-backed, independent).
- **FRR-first** evaluation on human-correct child speech as mandatory practice.
- **NOTHING that touches production** — no scorer, VAD, router, window, or Unity change.

## 13. What LWE should NOT adopt

- Deletion-aware Viterbi / GOP-ratio as a scorer replacement (FAIL).
- Age conditioning in calibration (FAIL).
- SIAK-fitted calibration as an LWE error detector (FAIL external).
- B1 head-only adaptation (FAIL, FRR regression).
- Any claim that SO762 results transfer to Vietnamese-L1 age-4 (BLOCKED, unmeasured).
- Direct 0–100 score display to children (design concern; no validation).

---

## 14. Remaining unknowns

- No human-labeled **invalid** recordings → refusal states (NO_SPEECH/UNINTELLIGIBLE/
  FREE_SPEAK/INCOMPLETE) never validated.
- No **Vietnamese-L1** child dataset → LWE transfer unmeasurable in this environment.
- No **4–5-year-old** phone-tier data (SO762 starts at 6).
- Single reviewer for all human labels; no kappa.
- SO762 license metadata discrepancy unresolved.
- FAR denominators on SO762 are tiny (48); child pronunciation FAR is ~unmeasured.
- Full fine-tuning / encoder adaptation never attempted (CPU constraint + B1 failure).

---

## 15. Recommended next experiment

**Do not start B2.** If the line continues, the evidence points to two separate,
lower-risk steps:

1. **Raw-audio transfer harness on the machine that holds it** (MAYNODE):
   run B1 inference on the privacy-sensitive LWE/Zenodo child tokens using the committed
   `run_b1_eval.py` scoring path, speaker-disjoint, FRR-first — to convert the current
   `BLOCKED` into a real cross-L1/cross-age number. (B1 is already a failure candidate;
   this closes the transfer question honestly.)
2. **Assessability refusal set**: build a human-labeled set of invalid/short/free-speech
   recordings to validate the refusal states, which is the one direction backed by
   partial PASS evidence (no-false-gate) and is independent of the failed scorer path.

Neither is production work; both stay behind the gate below.

---

## 16. Production decision gate

Production integration is **NOT ALLOWED**. To open the gate, ALL must hold:

- A child model/calibration that **reduces FRR** on human-correct child speech vs the
  frozen baseline on a **speaker-disjoint, multi-reviewer** set, **without** exploding FAR.
- A demonstrated cross-L1 transfer to LWE Vietnamese-L1 child speech.
- A validated assessability refusal layer.
- A resolved license review for every dataset used in any recommendation.

None currently holds. Locks stay all-false; PHASE 5 must not start.

---

## 17. Honesty statement

Every number above is taken from a committed artifact (path cited). No result was
reconstructed from a commit title. Where evidence is absent, it is marked
**BLOCKED/NOT RUN/NOT RUN** or **NOT FOUND** — never invented. "Training completed" is not
"works"; "SO762 improved" is not claimed (it did not); "child model" is not
"age-4 Vietnamese solved".
