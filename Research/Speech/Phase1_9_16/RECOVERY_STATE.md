# PHASE 1.9.16 — RECOVERY STATE (MAYNODE, 2026-10-03)

**Branch:** `phase1-9-16-recovery` @ verified base `3f62ef4`
**Upstream:** `origin/ux/math-arenas-hotfix-20260930`
**Companion:** `ASUS_RECOVERY_STATUS.md` (loss boundary, read first)

---

## 1. CURRENT_STATUS

Recovery audit COMPLETE. Phase 1.9.16 had **zero verifiable progress** (no commits, no
files, no checkpoints on any reachable machine or ref). Work resumes as a clean start
from the verified 1.9.15 checkpoint. No training was ever launched, so there is nothing
to deduplicate and nothing to continue evaluating.

## 2. LAST_VERIFIED_STEP

**Phase 1.9.15 COMPLETE, decision B = CALIBRATION_PROMISING_BUT_INSUFFICIENT**
(commit `3f62ef4`, RECOVERED FROM GIT):

- SIAK speaker-disjoint calibration: Pearson 0.307→0.454 (`hgb_noage`), FRR
  12.2%→2.0% in-population; age conditioning REJECTED.
- External LWE transfer WEAK: AUC 0.659→0.706, no usable FAR at FRR ≤ 0.10.
- Independent blind fidelity pack (115 clips, `human_maynode`): v2 false-gate 0/103
  survived; refusal detection failed (5/8 dangerous strict).
- Bottleneck verdict: phone evidence (posteriors + CTC alignment + aggregation) is a
  principal bottleneck → child phone model adaptation justified as NEXT experiment.
- Production locks (verified in `Phase1_9_15/artifacts/metrics.json`, STILL IN FORCE):
  `production_vad=false · router_locked=false · unity_integrated=false ·
  scorer_modified=false · production_window_locked=false`.
- Frozen baseline (UNCHANGED for 1.9.16): `facebook/wav2vec2-xlsr-53-espeak-cv-ft`
  snapshot `2c733782da5604684829819a5eb744c193fe9398` (Apache-2.0) via frozen
  `PhoneEvidenceV2@1.4.0`. Do not replace, overwrite, or silently re-pin.

## 3. RECOVERED_ARTIFACTS (all RECOVERED FROM GIT unless noted)

- `Phase1_9_15/` full chain: report, calibration/external/forensics/fidelity docs,
  `artifacts/{siak,calibration,external,fidelity,forensics}/`, `metrics.json`,
  `manifests/provenance.json`, 115-clip fidelity pack + reviewer submission.
- `Phase1_9_14/`: SIAK metadata (16,308 utt / 172 speakers, CC-BY-ND-4.0 Kaggle
  mirror), assessability + deletion-aware results, `LICENSE_AND_DATA_NOTES.md`.
- `Phase1_9_9`–`Phase1_9_12/`: 12 PHONE_MODEL_ERROR + 6 SCORER_MISS evidence, human
  review CSVs, window-policy decisions (evaluation answer key — NEVER train on it).
- `Phase1_1`–`Phase1_8`, `Phase1_2/Results/lwe_*`: baseline definition, LWE control
  words (`red, cat, apple, blue, big, book, dog, red apple, silence, noise`).
- Local alternate VAD line: RECOVERED FROM LOCAL → branch `maynode-vad-alt-backup`
  (`fccb23e`; 4 MAYNODE commits 1.9.2–1.9.5 with diverged layout). Deliberately NOT
  merged into this verified line pending human review.

## 4. MISSING_ARTIFACTS (must re-acquire before any training)

| missing on MAYNODE | re-acquisition path | status |
|---|---|---|
| SIAK raw release (`ExternalData/SIAK`, gitignored by design) | Kaggle mirror of SIAK; verify 12,308+4,000 rows, 16,308 flac, CC-BY-ND-4.0 | NOT STARTED |
| Zenodo-200495 child clips (`ExternalData/zenodo_200495`, gitignored) | original Zenodo source per 1.9.8 inventory (CC-BY-4.0) | NOT STARTED |
| wav2vec2 snapshot `2c73378…` + `cmudict.dict` + moonshine-tiny (were `D:\speech-lab\models`, absent) | HF pinned-revision download to local `models/` (Apache-2.0, no auth wall) | NOT STARTED |
| Python deps (`transformers`, `torchaudio`/`librosa`, `datasets`, `scikit-learn`); env is torch-2.14.1+CPU-only, Python 3.13.2 | `pip install`, CPU validation first | NOT STARTED |
| SIAK commercial/training-rights sign-off | 1.9.14 logged it as **open legal blocker**; ND clause needs review before any adaptation training counts as clear | BLOCKED_PENDING_LICENSE_REVIEW (research may proceed; production recommendation stays barred regardless) |

## 5. NEXT_ACTION (resume from LAST_VERIFIED_STEP + 1)

1. **Feasibility audit (STEP 1, no training):** install deps; download pinned model
   snapshot; verify hash `2c73378…`; re-acquire SIAK; reproduce hash checks against
   1.9.15 `provenance.json` inputs; record license verdict. → `DATA_AUDIT.md`
   (partial: environment + availability).
2. **Phone inventory audit:** eSpeak ↔ ARPAbet/CMUdict ↔ PhoneEvidenceV2 ↔ SIAK/LWE
   target symbols; version the mapping; flag final-consonant/stop/voicing/fricative
   coverage. → `PHONE_INVENTORY_AUDIT.md`.
3. **Frozen baseline reproduction:** run `PhoneEvidenceV2@1.4.0` zero-shot on
   re-acquired SIAK subset + LWE tokens; must match 1.9.14/1.9.15 zero-shot numbers
   before any adaptation is trusted. → `BASELINE_PHONE_EVALUATION.md`,
   `baseline_metrics.json`.
4. Child data audit (ratings ≠ phone labels) → `dataset_manifest.json`.
5. Speaker-disjoint train/valid/test splits, overlap=[] → `split_manifest.json`.
6. Zero-shot child baseline (PER/FRR/FAR/sub/del/ins) — the number B1 must beat.
7. Adaptation B1 (frozen encoder + trainable head) ONLY; B2 partial unfreeze only if
   B1 generalizes to unseen speakers.
8. Confusion forensics → `confusion_matrix.csv`.
9. LWE target-word A/B (same audio/target/alignment/scorer) + 12+6 historical replay
   (FIXED/PARTIALLY_FIXED/UNCHANGED/REGRESSED/INCONCLUSIVE) → `LWE_FAILURE_REPLAY.md`,
   `lwe_failure_replay.csv`.
10. FRR-first analysis (baseline vs child FRR on human-correct speech) → `FRR_FAR_ANALYSIS.md`,
    `frr_far.csv`; assessability kept separate → `ASSESSABILITY_INTERACTION.md`.
11. Speaker generalization + condition robustness + runtime + license →
    `SPEAKER_GENERALIZATION.md`, `RUNTIME_AND_LICENSE.md`.
12. `MODEL_SELECTION.md` + `PHASE_1_9_16_REPORT.md` ending in exactly one of A–F
    (A never authorizes production changes; locks stay).

## 6. DO_NOT_REPEAT / DO_NOT_DO

- Do NOT re-run 1.9.15 calibration/validation (verified; in git).
- Do NOT retrain anything to "catch up" — nothing was trained; there is no checkpoint
  to honor and no sunk cost to protect.
- Do NOT merge `maynode-vad-alt-backup` into this line without explicit human review
  (alternate layout; contamination risk to the verified chain).
- Do NOT train on the 28 final-consonant blind-review tokens, the 12
  PHONE_MODEL_ERROR cases, or the 6 SCORER_MISS cases (evaluation answer key).
- Do NOT turn SIAK pronunciation ratings into phone labels (§12).
- Do NOT fine-tune the full encoder first (§15: B1 head-only, then B2 only if
  justified). Do NOT trust a checkpoint without speaker-disjoint provenance (§8).
- Do NOT modify production code/scorer/VAD/router/window (all locks false = locked,
  not permission). Do NOT start Phase 1.9.17, Unity work, or claim completion early.
- Do NOT re-download blindly: reuse pinned revisions/hashes; prefer authenticated
  sessions only if a local snapshot cannot be reused (none exists yet — document it).
- NEVER invent an ASUS result. Every claim below this file is one of: RECOVERED FROM
  GIT / RECOVERED FROM LOCAL / REPRODUCED / RERUN / HUMAN-REVIEWED / MODEL-DERIVED /
  INFERRED / UNKNOWN-LOST — labeled as such.
