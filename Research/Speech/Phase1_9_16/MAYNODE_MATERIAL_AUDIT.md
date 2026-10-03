# MAYNODE MATERIAL AUDIT — Phase 1.9.16 (2026-10-03)

**Machine-readable companion:** `MAYNODE_MATERIAL_MANIFEST.json`
**Branch:** `phase1-9-16-recovery` · **Base:** `3f62ef4` (Phase 1.9.15, decision B)
**Rule applied:** every REQUIRED missing/incomplete item was downloaded/rebuilt again
(user-authorized). Nothing verified-complete was deleted or duplicated.

---

## 1. MAYNODE MATERIAL STATUS

| # | material | verdict | detail |
|---|---|---|---|
| A | Base phone model (`wav2vec2-xlsr-53-espeak-cv-ft` @ `2c73378…`) | **VERIFIED_COMPLETE** | DOWNLOADED (pinned rev) → `D:\speech-lab\models`; forward smoke `[1,99,392]`, 392 phones, head train-step + checkpoint roundtrip OK, CPU load 4.1 s |
| B | LWE pipeline code (`PhoneEvidenceV2@1.4.0`, inventory v1.4.0) | **VERIFIED_COMPLETE** | REUSED from git; inventory cross-check vs vocab + CMUdict passed (`PHONE_INVENTORY_VERIFIED.json`) |
| C | SIAK full release | **VERIFIED_COMPLETE** | DOWNLOADED (`rkarhila/SIAK`) → `ExternalData/SIAK`; **byte-identical to ASUS inputs** (3/3 provenance hashes); 12,308+4,000 rows, 16,308 flac / 278 MB, 172 speakers, split overlap [], 0 missing/dupes, 21/21 decode probe OK |
| C2 | CMUdict ARPAbet targets | **VERIFIED_COMPLETE** | DOWNLOADED (cmudict master, 3.6 MB) → `D:\speech-lab\models\cmudict.dict` |
| D | LWE real-child data | **PARTIAL / OPTIONAL** | Committed derivatives VERIFIED (115 fidelity clips, 210 1.9.9 clips, review CSVs, control-word artifacts). Raw `ExternalData` audio: **EXTERNAL_LWE_AUDIO_UNAVAILABLE** on MAYNODE — replay uses committed derivatives only; nothing fabricated |
| E | Child phone training data (gold phone labels) | **MISSING** | No corpus with reliable phone-level labels exists on MAYNODE or public HF (SIAK = ratings, not phones; MyST/PF-STAR = restricted access). **Not substituted, not faked** |
| F | Moonshine ASR | **OPTIONAL** | Deferred; not required for phone adaptation |
| G | License posture | **MIXED** | Model Apache-2.0 CLEAR; SIAK CC-BY-ND-4.0 **BLOCKED_PENDING_LICENSE_REVIEW** for training use (open since 1.9.14) |

DOWNLOADED: base model snapshot, SIAK full release, CMUdict.
REUSED: pipeline code, inventory, all committed LWE derivatives.
REDOWNLOADED: SIAK + model cache (ASUS copies lost with the machine; SIAK verified byte-identical).
MISSING: gold child phone labels. BLOCKED: MyST/PF-STAR access; SIAK training license.

## 2. INTEGRITY METHOD (no trust in file counts alone)

- SIAK: `experiments/verify_siak.py` — provenance SHA-256 match (3/3), row counts,
  flac count, speaker-disjointness of the release splits, duplicate IDs, missing
  audio refs, missing scores, total bytes, 21-file decode probe. ALL PASSED.
- Model: pinned-revision download + SHA-256 recorded (`pytorch_model.bin`
  `04366b6c…`, `config.json` `4609fb49…`) + live `transformers` load + forward +
  optimizer-step + checkpoint roundtrip (`experiments/smoke_model.py`). PASSED.
- CMUdict: size + content probe; full-file SHA-256 recorded (`81917843…`).
- Anonymous-HF warning surfaced (no token); completion was not rate-limited.
- LWE clips/CSVs: presence counts from git (115/115 fidelity, 210 1.9.9 clips,
  12 control-word artifacts); per-file audio re-decode deferred to baseline
  reproduction, which reads them.

## 3. TRAINING READINESS GATE

- **BASELINE_READY = TRUE.** Frozen baseline reproduction (zero-shot SIAK subset +
  LWE tokens, no training) is fully materialized and is the correct immediate next step.
- **MAYNODE_TRAINING_READY (adaptation) = FALSE. DO NOT TRAIN.** Blockers:
  1. `GOLD_CHILD_PHONE_LABELS = MISSING` — supervised B1 (trainable phone head)
     has no phone-level targets. SIAK ratings must NOT be converted into phone
     labels (§12). Path forward (research decision, not taken here): pseudo-phone
     targets from frozen forced alignment (MODEL-DERIVED, circularity measured) or
     institutional MyST/PF-STAR access.
  2. `SIAK_LICENSE_FOR_TRAINING = BLOCKED_LICENSE` — ND-clause review still open;
     fine-tuning on ND data may constitute a derivative.
- Hardware note (for when the gate passes): CPU-only (Xeon, 16 GB RAM, no CUDA;
  AMD GPU unusable). Feasible path is frozen-encoder feature caching + small head
  (B1); full fine-tuning on CPU is not credible without a hardware plan. A 1-batch
  smoke (forward/backward/step/save) already passes.

## 4. WHAT THIS UNBLOCKS (ordered, per §14/§22 sequence)

1. Frozen baseline reproduction → `BASELINE_PHONE_EVALUATION.md` (+ `DATA_AUDIT.md`).
2. Speaker-disjoint split construction from the verified release → `split_manifest.json`.
3. Design decision on adaptation targets (pseudo-label vs external corpus) with the
   license review — only then can the gate be re-evaluated.
4. No production, scorer, VAD, router, assessability, or Unity code was touched
   (locks remain all-false).

## 5. ASUS ACCOUNTING (honest)

- RECOVERED (git): entire 1.9.15 chain, baseline definition, failure forensics.
- LOST (ASUS-local, never pushed): any 1.9.16 drafts; ASUS SIAK copy; ASUS model cache.
- REPRODUCED/RERUN: SIAK re-download (byte-identical, stronger than rerun);
  model snapshot re-download (same pinned revision, smoke-verified).
- NEWLY DOWNLOADED: CMUdict (ASUS had it; MAYNODE did not).
- EXPERIMENTS RERUN: none yet — baseline reproduction is next, training stays barred.
