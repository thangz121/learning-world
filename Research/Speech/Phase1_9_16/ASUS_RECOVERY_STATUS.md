# ASUS RECOVERY STATUS — Phase 1.9.16 maynode recovery (2026-10-03)

**Machine:** MAYNODE · **Author:** recovery agent · **Branch for recovery work:**
`phase1-9-16-recovery` (from verified `3f62ef4`)

This file records the ASUS loss boundary. Nothing in it is inferred about ASUS-local
results. See `RECOVERY_STATE.md` for the resume plan.

---

## 1. Last known phase

- **Phase 1.9.15**, commit `3f62ef457a24fde061adfea7bdaeb88930ed44c6`
  (`phase1.9.15: independent fidelity validation and final calibration decision`),
  pushed to `origin/ux/math-arenas-hotfix-20260930`. VERIFIED FROM GIT.
- Phase 1.9.15 decision: **B = CALIBRATION_PROMISING_BUT_INSUFFICIENT**.
- Phase 1.9.16 was *specified* (child phone model adaptation) but **no 1.9.16 commit
  exists on any branch of `origin`** (searched all refs, grep `9.16`/`adaptation`:
  zero hits). RECOVERED FROM GIT (absence verified by fetch on 2026-10-03).

## 2. Last known terminal progress (ASUS 1.9.16)

- **UNKNOWN / LOST.** The ASUS machine is unreachable (suspected power loss). No terminal
  history, no logs, and no pushed artifacts describe any 1.9.16 execution.
- Per the governing rule, **no ASUS-local 1.9.16 result is claimed to exist**.

## 3. Last known dataset / experiment / checkpoint (ASUS)

- Dataset: SIAK 16,308 utt / 172 speakers (ASUS-local path
  `D:\Vscode\little-world-english\Research\Speech\ExternalData\SIAK`, gitignored).
  NOT RECOVERABLE from MAYNODE (path absent, no copy found on E: or D: clones).
- Experiment: none verifiable. NOT RECOVERABLE.
- Checkpoint (`model.safetensors` / `trainer_state.json` / adapters / tensorboard):
  **none found anywhere** — searched `Research/` on both MAYNODE clones, git history,
  and the `learning-world-BACKUP-2026-09-28` tree. Conclusion: **no evidence any
  training ever started**, on ASUS or elsewhere. NOT RECOVERABLE (nothing to recover).

## 4. Artifacts recovered

| artifact | provenance |
|---|---|
| Full 1.9.15 chain (reports, metrics, splits, fidelity pack, 115 clips, failure forensics) | RECOVERED FROM GIT (`origin`, commit `3f62ef4`) |
| Baseline definition: `facebook/wav2vec2-xlsr-53-espeak-cv-ft` snapshot `2c733782da5604684829819a5eb744c193fe9398`, `PhoneEvidenceV2@1.4.0` | RECOVERED FROM GIT |
| 12 PHONE_MODEL_ERROR + 6 SCORER_MISS evidence (`Phase1_9_9/Results/`, 1.9.10–1.9.12 reviews) | RECOVERED FROM GIT |
| LWE control-word + stress artifacts (`Phase1_2/Results/lwe_*`) | RECOVERED FROM GIT |
| Local alternate VAD line (4 commits 1.9.2–1.9.5, MAYNODE-authored, diverged layout) | RECOVERED FROM LOCAL, preserved on branch `maynode-vad-alt-backup` (`fccb23e`), NOT merged into verified line |
| Older snapshot clone `D:\Vscode\little-world-english` (at 1.9.9-era `8479d53`, stale remote-tracking ref) | RECOVERED FROM LOCAL (informational only; superseded by fetch) |

## 5. Artifacts potentially lost (ASUS-local, unverifiable)

- Any 1.9.16 feasibility notes, phone-inventory drafts, or split manifests created on
  ASUS after `3f62ef4` and never pushed.
- The SIAK raw release (re-acquirable from the Kaggle mirror; see `RECOVERY_STATE.md`).
- The ASUS model cache (`D:\speech-lab\models`, incl. the pinned wav2vec2 snapshot).
- Any in-flight training checkpoints (no evidence any existed).

## 6. Experiments that must be rerun (i.e. run for the first time, verifiably)

- **All of Phase 1.9.16.** There is no verified partial checkpoint to continue from:
  feasibility audit → phone inventory → frozen baseline reproduction → child data audit →
  speaker-disjoint splits → zero-shot child baseline → B1 adaptation → confusion
  forensics → LWE A/B → historical failure replay → FRR/FAR → assessability →
  speaker/condition generalization → runtime → license → decision A–F.
- **No retraining concern:** nothing was trained, so no checkpoint is skipped and no
  expensive work is duplicated.

## 7. Honesty statement

No result in this file is reconstructed from memory or assumption. Anything ASUS may
have done in 1.9.16 without pushing is treated as LOST. A slower clean rerun is
preferred to a false result.
