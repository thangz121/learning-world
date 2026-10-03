# LWE FAILURE REPLAY — Phase 1.9.16 (baseline side complete)

**Script:** `experiments/build_analyses.py` · **Artifact:** `lwe_failure_replay.csv`
(23 rows: the required 12 PHONE_MODEL_ERROR + 6 SCORER_MISS + 5
TRUE_PRONUNCIATION_ERROR context rows from `scorer_failure_cases.csv`.)

---

## 1. Baseline side (RECOVERED_FROM_GIT + RERUN-verified pipeline)

Every case carries: human judgment, baseline phones (`phone_evidence`), baseline
soft/confidence, diagnostic class. The frozen pipeline that produced them is the
bit-reproduced 1.9.16 baseline above — so these rows are a valid standing
baseline for any future child model.

## 2. Child side

**NOT_AVAILABLE — no child model was trained in this phase** (gate FALSE: no
phone labels + SIAK ND license block; see `PHASE_1_9_16_REPORT.md`, decision D).
All 23 cases are therefore classified:

- **INCONCLUSIVE (child model does not exist)** — 23/23.
- FIXED / PARTIALLY_FIXED / UNCHANGED / REGRESSED: 0 (nothing to compare).
- No human label was altered; no child output was synthesized or imputed.

## 3. Frozen controls

Baseline phone evidence for `red, cat, apple, blue, big, book, dog, red apple,
silence, noise` is committed (`Phase1_2/Results/lwe_*`, RECOVERED_FROM_GIT);
the same-audio/same-target/same-alignment/same-soft-v2 harness is implemented
in `run_baseline.py` + `PhoneEvidenceV2.soft_match` and validated on 482 SIAK
utterances. The control table awaits only the child-model column.

## 4. Meaning

The replay harness is complete and the baseline half is frozen. The experiment
is executable the moment a legitimately trained child model exists — which this
phase's evidence shows cannot be produced from current materials.
