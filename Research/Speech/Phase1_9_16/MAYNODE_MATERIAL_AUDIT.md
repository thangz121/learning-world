# MAYNODE MATERIAL AUDIT — Phase 1.9.16 (RE-VERIFIED 2026-10-04, 2026-10-05)

**Machine:** MAYNODE · **Branch:** `phase1-9-16-recovery` · **HEAD at audit:** `cbf9e81`
**Machine-readable companion:** `MAYNODE_MATERIAL_MANIFEST.json`
**Rule applied:** every REQUIRED missing/incomplete item downloaded/rebuilt again
(user-authorized). Nothing verified-complete was deleted or duplicated. File counts
were never trusted alone — every dataset was re-probed.

> NOTE: This machine **is** MAYNODE (`hostname`=Maynode, `whoami`=maynode\admin).
> The original 1.9.16 recovery ran on a MAYNODE and this continues it.

---

## 1. MAYNODE MATERIAL STATUS (re-verified this session)

| # | material | status | evidence |
|---|---|---|---|
| A | Base phone model `wav2vec2-xlsr-53-espeak-cv-ft@2c73378…` | **VERIFIED_COMPLETE** | snapshot 8 files incl. 1.26 GB weights; `smoke_model.py` forward `[1,99,392]`, 392 phones, head train-step + checkpoint roundtrip OK, load 2.7 s |
| B | LWE pipeline (`PhoneEvidenceV2@1.4.0`, inventory v1.4.0) | **VERIFIED_COMPLETE** | `PHONE_INVENTORY_VERIFIED.json`: 389/392 mapped, all 7 control words covered |
| C | SIAK full release | **VERIFIED_COMPLETE** | `verify_siak.py` ALL PASSED: 16,308 flac, 12,308+4,000 rows, 172 spk (129/43, overlap []), 0 missing/dupes/corrupt, 278 MB, provenance 3/3 byte-identical |
| C2 | CMUdict ARPAbet targets | **VERIFIED_COMPLETE** | 3,618,488 B, sha256 `81917843…` |
| E | Child phone-tier corpus **SO762** | **VERIFIED_COMPLETE (NEW materialization)** | `mispeech/speechocean762@06385584…`; snapshot now fully cached (615 MB parquet); recomputed offline: 5,000 rows / 250 spk / 2,440 child ≤15; 5-expert phone tier |
| D | Zenodo-200495 age-4 English child audio | **VERIFIED_COMPLETE (RECOVERED this session)** | CC-BY-4.0, public; downloaded 607 MB, **MD5 matches** `1a4fd611…`; extracted 671 wav (417 words/sentences + 254 free speech), 11 children age M=4.9y, studio+portable mics, numbers 1–10 + 5 sentences |
| D2 | LWE committed derivatives | **VERIFIED** | 115/210/24/38/128 review clips + CSVs + control-word artifacts |
| F | Moonshine ASR | **OPTIONAL** | not required for phone adaptation |
| G | License posture | **MIXED** | model Apache-2.0 CLEAR; SIAK CC-BY-ND training BLOCKED; SO762 CC BY 4.0 / apache-2.0 discrepancy recorded |

## 2. INTEGRITY METHOD

- **SIAK:** `experiments/verify_siak.py` — SHA-256 provenance match (3/3), row counts,
  flac count, speaker-disjoint release splits, duplicate IDs, missing audio refs,
  missing scores, total bytes, 21-file decode probe. ALL PASSED.
- **Model:** pinned revision + SHA-256 (`pytorch_model.bin 04366b6c…`, `config.json
  4609fb49…`, `vocab.json d732ab24…`) + live load + forward + optimizer step +
  checkpoint roundtrip. PASSED.
- **CMUdict:** SHA-256 `81917843…`.
- **SO762:** parquet row/speaker/age counts recomputed offline (`pyarrow`) — matches
  the 1.9.17 audit exactly (5,000 / 250 / 2,440 child).
- **Anonymous-HF warning** surfaced (no token); completion not rate-limited.
- **Symlink warning** (no Developer Mode): HF cache works in degraded copy mode (extra
  disk only).

## 3. TRAINING READINESS GATE

- **BASELINE_READY = TRUE.**
- **MATERIAL_MANIFEST = VERIFIED_COMPLETE** for every REQUIRED item.
- **MAYNODE_TRAINING_READY = FALSE. DO NOT TRAIN.** Blockers:
  1. `SIAK_LICENSE_FOR_TRAINING` unresolved (CC-BY-ND-4.0): fine-tuning on ND material
     may constitute a derivative; 1.9.14 logged the open legal review.
  2. The gold phone tier now exists **only** at SO762, whose population is
     **Mandarin-L1, ages 6–15** — NOT the LWE target (Vietnamese-L1, ~age 4).
  3. 1.9.18 already ran the smallest adaptation (B1 head-only) on SO762 and it
     **regressed FRR** (0.227 → 0.299). B2 is not justified by that evidence.
- Hardware: CPU-only (Xeon, 16 GB RAM, no CUDA; AMD GPU unusable). Frozen-feature +
  small-head is CPU-feasible; full fine-tuning is not credible without a hardware plan.
  1-batch smoke (forward/backward/step/save) passes.

## 4. WHAT THIS UNBLOCKS

1. Frozen baseline reproduction (done in 1.9.16, `482/482`, `baseline_metrics.json`).
2. Speaker-disjoint split (done, `split_manifest.json`).
3. Child phone-tier evaluation (done in 1.9.18 on SO762, decision E).
4. No production/scorer/VAD/router/assessability/Unity code touched (locks all false).

## 5. ACCOUNTING (honest)

- RECOVERED (git): entire 1.9.15 chain, baseline definition, failure forensics.
- LOST (prior-machine-local, never pushed): any 1.9.16 drafts; prior SIAK/model cache.
- REPRODUCED/RERUN: SIAK re-download (byte-identical), model snapshot (same pinned rev,
  smoke-verified), SO762 snapshot materialized this session.
- NEWLY DOWNLOADED: CMUdict (prior recovery); SO762 parquet (this session);
  **Zenodo-200495 English child audio (this session, MD5-verified)**.
- EXPERIMENTS: baseline + B1 already rerun and reported in 1.9.16/1.9.18.

## 5b. CHANGE SINCE THE ORIGINAL AUDIT (important)

The original 1.9.16 audit recorded `EXTERNAL_LWE_AUDIO_UNAVAILABLE` and
`NO gold child phone labels`. Both gaps are now **materially changed**:

- **Gold child phone tier:** SO762 is now fully cached locally (5-expert per-phone).
- **Age-4 English child audio:** Zenodo-200495 is now on disk (CC-BY-4.0, 671 wav,
  11 children age M=4.9y).

Neither closes the LWE *Vietnamese-L1* gap (no Vietnamese child corpus exists), but the
**evaluation material no longer blocks an age-4 English child transfer/replay study**.

## 6. RESULT

`MATERIAL_MANIFEST = VERIFIED_COMPLETE`. Training stays gated by **license +
target-population relevance + prior B1 failure**, not by missing material.

---

## 7. INDEPENDENT RE-VERIFICATION (2026-10-05, this session)

Every REQUIRED item was re-probed from scratch this session (no trust in the
prior docs, per the "file counts are not proof" rule). Nothing was re-downloaded
because every required item passed integrity. Raw child audio was never committed.

| item | command (fresh) | result |
|---|---|---|
| Environment | `python -c "import torch,transformers,datasets,..."` | Python 3.13.2 · torch 2.14.1+cpu · transformers 5.18.0 · datasets 5.0.1 · hub 1.33.0 · sklearn 1.9.1 · numpy 2.5.3 · soundfile 0.14.0 · pyarrow 25.0.1 · `cuda_available=false` |
| SIAK | `verify_siak.py` | **VERIFIED_COMPLETE**: 3/3 provenance SHA-256 byte-identical; train 12,308 / test 4,000; 16,308 flac; 172 spk (129/43, overlap `[]`); 0 missing refs / 0 dup ids / 0 missing scores; 21-file decode probe 0 corrupt; 277,954,460 B (278 MB) |
| Base model | `smoke_model.py` | **VERIFIED_COMPLETE**: pinned `2c73378…`, load 2.9 s, forward `[1,99,392]`, 392 phones, head train-step + checkpoint roundtrip OK |
| Base model files | `verify_external_materials.py` | snapshot `2c73378…` present with all 6 required files (config, preprocessor, `pytorch_model.bin` 1,263,535,127 B, tokenizer_config, special_tokens_map, vocab); 0 missing |
| Phone pipeline | `verify_phone_inventory.py` | **VERIFIED_COMPLETE**: inventory `phone-inventory-v1.4.0`; 389/392 symbols mapped; all 8 control-word targets covered |
| Splits | `build_splits.py` | **VERIFIED_COMPLETE**: seed 1515; train 2,094/113 spk · valid 493/27 · test 482/27 · ages4-6-ext 99/5; overlap `[]`; 0 dup ids; 0 missing files |
| SO762 phone tier | `verify_external_materials.py` | **VERIFIED_COMPLETE**: rev `06385584…`; 5,000 rows; 250 spk; 122 child ≤15; `phones-accuracy` tier present |
| Zenodo-200495 | `verify_external_materials.py` | **VERIFIED_COMPLETE**: `english_children.zip` MD5 `1a4fd611…` matches; 671 extracted wav |
| LWE committed derivatives | filesystem + git | present: 1.9.9–1.9.12 human-review clips + CSVs + `Phase1_2/Results/lwe_*` (answer key, never trained on) |

Reproducible verifier added this session:
`experiments/verify_external_materials.py` (SO762 + Zenodo + base-model snapshot;
the 1.9.4 paths in `verify_siak.py` / `smoke_model.py` / `verify_phone_inventory.py`).

Notes (honest):
- The model cache also contains a **stale, empty** second snapshot dir
  `3e836924…` (no files) alongside the pinned `2c73378…`. Harmless leftover;
  `verify_external_materials.py` pins the revision, so it is not used. Not deleted
  (do not destroy caches), recorded here.
- `ExternalData/SIAK` also holds HF Xet `.metadata` sidecars (16,312) + `.cache`;
  that is why a raw `Get-ChildItem` count is 32,627 files, not 16,308. The FLAC
  count is exactly 16,308.
- Disk pressure: C: 8.1 GB free, D: 11.4 GB free, E: 29.5 GB free. Sufficient for
  the current (already-downloaded) material; a **new** full-corpus download would
  not fit without freeing space. No download was needed this session.

## 8. D. LWE REAL-CHILD AUDIO — EXTERNAL_LWE_AUDIO_UNAVAILABLE (raw)

Raw privacy-sensitive LWE real-child recordings are **not present on MAYNODE and
are not recoverable** (searched the working tree and the alternate clones
`D:\Vscode\little-world-english`, `E:\lw`, `E:\lw2`, `E:\LearningWorld`,
`D:\LearningWorld`; `.gitignore:98-99` excludes `real-audio/` and `real-child/`,
and none exist). Per the rule, nothing was fabricated.

- **Available:** committed *derivatives* only — 1.9.9/1.9.10 blind-review clips +
  human labels, 1.9.11/1.9.12 window-review clips, Phase1_2 `lwe_*` control word
  captures, Phase1_9_5 hybrid IMG review clips (all git-tracked, aggregate only).
- **BLOCKED by this:** validation that needs the *raw* full-length child sources
  (re-VAD of the original IMG recordings, re-segmentation from source, any
  recording not represented by a committed derivative). The committed clips
  remain sufficient for the 12 PHONE_MODEL_ERROR + 6 SCORER_MISS replay and the
  frozen control-word AB.
- **Not a blocker for material completeness:** the evaluation corpora
  (SIAK + SO762 + Zenodo-200495) are all present and verified.

## 9. GATE (unchanged conclusion, re-confirmed)

```
MATERIAL_MANIFEST      = VERIFIED_COMPLETE
BASELINE_READY         = TRUE
EXTERNAL_LWE_AUDIO     = EXTERNAL_LWE_AUDIO_UNAVAILABLE (raw only)
MAYNODE_TRAINING_READY = FALSE   -> DO NOT TRAIN
```

Gate is held by **license + target-population relevance + prior B1 FRR
regression**, not by missing material.
