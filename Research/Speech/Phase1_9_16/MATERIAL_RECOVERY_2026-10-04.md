# PHASE 1.9.16 — MAYNODE MATERIAL RECOVERY (2026-10-04)

**Machine:** MAYNODE (`hostname`=Maynode) · **Branch:** `phase1-9-16-recovery` · **HEAD:** `cbf9e81`
**Authorization:** user ("download mọi thứ cần thiết"; re-download any missing/incomplete required material).
**Rule:** audit → download/redownload → verify → manifest → gate. No training. No production change.
Raw child audio never committed (`.gitignore:131 Research/Speech/ExternalData/`).
**2026-10-05:** independently re-verified this session (`verify_siak.py`, `smoke_model.py`,
`verify_phone_inventory.py`, `build_splits.py`, new `verify_external_materials.py`) — all
`VERIFIED_COMPLETE`, 0 downloads needed. Raw LWE child audio recorded
`EXTERNAL_LWE_AUDIO_UNAVAILABLE` (see `MAYNODE_MATERIAL_AUDIT.md` §8).

---

## 1. MAYNODE MATERIAL STATUS

| material | status | action |
|---|---|---|
| Base model `wav2vec2-xlsr-53-espeak-cv-ft@2c73378…` | VERIFIED_COMPLETE | REUSED (smoke: forward `[1,99,392]`, 392 phones, train-step + ckpt roundtrip) |
| LWE pipeline `PhoneEvidenceV2@1.4.0` + inventory v1.4.0 | VERIFIED_COMPLETE | REUSED (389/392 mapped, 7/7 control words) |
| CMUdict `cmudict.dict` | VERIFIED_COMPLETE | REUSED (sha256 `81917843…`) |
| SIAK full release | VERIFIED_COMPLETE | REUSED (byte-identical; 16,308 flac, 172 spk, 0 missing/dupes/corrupt) |
| SO762 child phone tier | VERIFIED_COMPLETE | **MATERIALIZED this session** (615 MB parquet; 5,000 rows / 250 spk / 2,440 child) |
| **Zenodo-200495 age-4 English child audio** | **VERIFIED_COMPLETE** | **DOWNLOADED this session** (607 MB, MD5 match; 671 wav / 11 children age M=4.9y) |
| LWE committed derivatives | VERIFIED | REUSED (115/210/24/38/128 review clips + CSVs) |
| Moonshine ASR | OPTIONAL | not required |

## 2. DOWNLOADED / REUSED / REDOWNLOADED / MISSING / BLOCKED

- **DOWNLOADED (this session):** SO762 snapshot (parquet+README); Zenodo-200495 `english_children.zip` (MD5-verified) + extracted.
- **REUSED:** model snapshot, SIAK (byte-identical), CMUdict, pipeline code, committed derivatives.
- **REDOWNLOADED:** SO762 (was stream-only), Zenodo-200495 (was absent entirely).
- **MISSING:** Vietnamese-L1 child corpus (none public/repo); MyST/PF-STAR (restricted); NAO-mic subset of Zenodo (few combos absent by source).
- **BLOCKED:** SIAK training license (CC-BY-ND); SO762 license declaration ambiguity (CC BY 4.0 vs apache-2.0).

## 3. INTEGRITY EVIDENCE

- SIAK `verify_siak.py`: ALL PASSED (hashes, counts, split disjointness, decode probe).
- Model: pinned rev + SHA-256 + live forward + optimizer step + checkpoint roundtrip.
- SO762: recomputed offline via `pyarrow` — 5,000 rows / 250 spk / 2,440 child (matches 1.9.17).
- **Zenodo-200495: zip MD5 `1a4fd6116554593324a0a493e44a1eea` == source checksum**; 671 wav
  (417 words/sentences + 254 free speech), 11 children (5F/6M), numbers 1–10 + 5 sentences.
- End-to-end usability: frozen pipeline scored Zenodo child words after the standard
  44.1k→16k resample (soft 33.5–100 across sample tokens) — material is valid and loadable.

## 4. TRAINING READINESS GATE

```
MATERIAL_MANIFEST   = VERIFIED_COMPLETE
BASELINE_READY      = TRUE
MAYNODE_TRAINING_READY = FALSE
```

Blockers are **license + target-population relevance + prior failure**, not missing material:
1. SIAK training license unresolved (CC-BY-ND).
2. SO762 gold phone tier is Mandarin-L1 ages 6–15; 1.9.18 showed head-only B1 on it
   **regresses FRR** (0.227 → 0.299) → B2 not justified.

## 5. WHAT CHANGED vs THE ORIGINAL 1.9.16 AUDIT

Original audit: `EXTERNAL_LWE_AUDIO_UNAVAILABLE` + `NO gold child phone labels`.
Now:
- Gold child phone tier **present** (SO762, cached).
- Age-4 English child audio **present** (Zenodo-200495, MD5-verified).

⇒ The **evaluation-material gaps are closed** for an age-4 English child transfer/replay
study. The **Vietnamese-L1** target gap remains (no corpus exists).

## 6. FOOTPRINT

- `Research/Speech/ExternalData/SIAK` — 278 MB (gitignored).
- `Research/Speech/ExternalData/zenodo_200495` — 607 MB zip + extracted (gitignored).
- `C:\Users\PC\.cache\huggingface\hub\datasets--mispeech--speechocean762` — 615 MB (cache).
- `D:\speech-lab\models` — model snapshot + cmudict (gitignored).

## 7. NO PRODUCTION CHANGE

All locks still false (`production_vad · router_locked · unity_integrated ·
scorer_modified · production_window_locked` = false/false/false/false/false). No scorer/VAD/
router/window/Unity file touched. No training launched.
