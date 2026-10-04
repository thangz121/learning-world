# HANDOFF — Speech/VAD Research (through Phase 1.9.18)

**Repo:** `thangz121/learning-world` · **Branch:** `phase1-9-16-recovery`
**HEAD at handoff:** `584eb2d` (local == `origin/phase1-9-16-recovery`)
**Date:** 2026-10-04
**Supersedes:** `HANDOFF_1_9_13.md` (documents only through 1.9.13).

> Read this file first. Do not switch to `main`, do not merge branches, do not modify
> frozen artifacts. Production flags stay all-false.

---

## 1. Phase timeline (1.9.13 → 1.9.18)

| Phase | Commit | Result |
|---|---|---|
| 1.9.13 | `3ab6c01` | External system forensics (20 systems); decision A = CLEAR_ARCHITECTURE_INSIGHTS |
| 1.9.14 | `d594821` | Fidelity/assessability + deletion-aware + SIAK; decision **B = RESEARCH_SUPPORTS_PARTIAL_CHANGE** |
| 1.9.15 | `3f62ef4` | Calibration + independent validation; decision **B = CALIBRATION_PROMISING_BUT_INSUFFICIENT** |
| 1.9.16 | `a88a198` | Child adaptation attempt (recovery); decision **D = DATA_INSUFFICIENT_FOR_CHILD_ADAPTATION** |
| 1.9.17 | `d45d3ed`,`584eb2d` | SO762 phone-tier audit; decision **A = SO762_PROVIDES_A_GENUINE_CHILD_PHONE_TIER** |
| 1.9.18 | `2bb9707` | Zero-shot baseline + B1 head-only adaptation; decision **E = B1_REGRESSES_HUMAN_CORRECT_CHILD_SPEECH** |

Full reconstruction: `Research/Speech/Phase1_9_18/PHASE_1_9_14_TO_1_9_18_STATUS.md` and
`Research/Speech/Phase1_9_18/PHASE_1_9_18_FINAL_REPORT.md`.

## 2. Current architecture status (all production behavior frozen)

- VAD: frozen (Silero 0.5 baseline). VAD-negative ≠ no speech (IMG stress 28/28 speech).
- Scorer: `PhoneEvidenceV2@1.4.0` **unchanged**. Frozen model
  `facebook/wav2vec2-xlsr-53-espeak-cv-ft@2c733782da5604684829819a5eb744c193fe9398`
  (Apache-2.0). Do not replace/re-pin.
- Router: not calibrated, not locked. Window behavior: not locked.
- `production_vad=false · router_locked=false · unity_integrated=false ·
  scorer_modified=false · production_window_locked=false` — keep all false.

## 3. What is evidence-backed now

- **PASS:** assessability/fidelity separation — v2 does not falsely gate human-valid child
  speech (1.9.14 0/30; independent 1.9.15 0/103). ASR must not judge pronunciation.
- **PASS (measurement):** frozen scorer is weak on child speech — SIAK speaker-disjoint
  Pearson 0.307; LWE AUC 0.659; SO762 child FRR 0.227.
- **PASS (data):** SO762 has a genuine 5-expert child phone tier (122 child spk, ages 6–15,
  Mandarin-L1), 67/67 inventory-mapped, native 16k PCM.

## 4. What failed / is blocked

- **FAIL:** deletion-aware Viterbi & GOP-ratio as scorer fixes (1.9.14).
- **FAIL:** age conditioning (1.9.15).
- **FAIL:** SIAK calibration as an LWE error detector (external FAR 77–100%).
- **FAIL:** B1 head-only adaptation on SO762 — FRR 0.227 → **0.299** (regression).
- **BLOCKED:** LWE adaptation transfer (raw child audio gitignored).
- **BLOCKED:** SIAK training/eval (CC-BY-ND review open since 1.9.14).
- **BLOCKED:** refusal-state validation (no human-labeled invalid recordings exist).

## 5. Open blockers

- SIAK legal review (CC-BY-ND) before any training/eval use.
- SO762 license metadata discrepancy (CC BY 4.0 vs apache-2.0) — record, not resolved.
- No Vietnamese-L1 child corpus → LWE cross-domain transfer unmeasurable here.
- No ages 4–5 in SO762 (youngest 6).
- Single reviewer for all human labels; no kappa.

## 6. Recommended next steps (NOT production, NOT PHASE 5)

1. Run B1 inference on the privacy-sensitive LWE child audio on the machine that holds it
   (MAYNODE), speaker-disjoint, FRR-first — to turn `LWE_TRANSFER_BLOCKED` into a real
   number. Do **not** start B2 (not justified; B1 regressed).
2. Build a human-labeled invalid/short/free-speech set to validate assessability refusal
   states (the only direction with partial PASS support).
3. If pursuing child adaptation again: require a **multi-reviewer, Vietnamese-L1 or
   age-4-matched** phone-tier corpus and a target formulation that can actually lower FRR.

## 7. Environment & how to run (verify on the working machine)

- Frozen pipeline venv: `D:\speech-lab\venvs\p0\Scripts\python` (torch/transformers,
  silero, soft-v2). Do not install new packages there.
- 1.9.18 ran under plain `python` (Python 3.13.2, torch 2.14.1+cpu, transformers 5.18.0,
  datasets 5.0.1, scikit-learn 1.9.1, numpy 2.5.3).
- SO762 streamed from HF mirror (revision `06385584…f093f33e`); raw child corpora are
  gitignored under `Research/Speech/ExternalData/`.

## 8. Rules (non-negotiable)

- Human listening is authority; ASR is not a pronunciation judge; energy is not GT.
- Never fabricate human labels or scores; declare NOT_COMPUTABLE / NOT FOUND instead.
- Raw child audio never committed.
- No secrets in repo.
- One experiment at a time; commit per completed unit; push after each. Commit style:
  `<phase id>: <short imperative summary>`.

## 9. START PROMPT for a new session (copy-paste)

```
Continue the Little World English Speech Research Lab.
Repo: E:\LWW\learning-world (branch phase1-9-16-recovery). Do NOT switch to main,
do NOT merge, do NOT start PHASE 5, do NOT modify production code.
Read: Research/Speech/HANDOFF_1_9_18.md first, then
Research/Speech/Phase1_9_18/PHASE_1_9_14_TO_1_9_18_STATUS.md and
Research/Speech/Phase1_9_18/PHASE_1_9_18_FINAL_REPORT.md.
Production flags stay false. 1.9.18 decision = E (B1 regressed FRR). Next experiment:
run B1/LWE transfer on the machine holding raw child audio (FRR-first, speaker-disjoint),
or build a human-labeled assessability-refusal set. Do NOT start B2 automatically.
```
