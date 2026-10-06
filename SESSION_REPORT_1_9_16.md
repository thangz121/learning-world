# SESSION REPORT — Phase 1.9.16 (CHILD PHONE MODEL ADAPTATION RESEARCH)

> **Tóm tắt (VI):** Audit dữ liệu phát hiện **speechocean762** có sẵn (CC BY 4.0, 122 trẻ,
> điểm phoneme 0/1/2 do 5 chuyên gia) → đủ cho adaptation B1 (frozen encoder + head CTC, 3.6M
> params, quality-filtered). Kết quả A/B: SIAK test Pearson 0.307→0.355, FRR 12.2%→6.6%;
> SIAK 4–6 0.184→0.373; nhưng **LWE external AUC 0.659→0.625, phụ âm cuối present recall
> 93.8%→75%**; replay PME 5 fixed / 8 unchanged / 3 regressed; SCORER_MISS "four" không sửa được.
> **Quyết định B = ADAPTATION_PROMISING_BUT_INSUFFICIENT**; không adopt, frozen model vẫn là reference.
> Commit `e8d96da` (đã merge main).

- **Date:** 2026-10-03
- **Machine:** ASUS (CPU-only, no GPU)
- **Base:** Phase 1.9.15 `3f62ef4`
- **Branch:** `ux/math-arenas-hotfix-20260930`
- **Scope:** research-only; production untouched; B1 head-only training (no encoder fine-tune)
- **Flags:** all five false.

## 1. Feasibility audit (spec §2)
- Scripts: `experiments/feasibility_audit.py`, `so762_manifest.py`, `inventory_audit.py`,
  `quality_audit.py`.
- **speechocean762 (SLR101, found at `D:\speech-lab\data\speechocean762`)**: CC BY 4.0
  (openslr.org/101), 5,000 utt / 250 speakers; children 122 (ages 6–15); train children
  **58 spk / 1,160 utt**, test children **64 spk / 1,280 utt** (speaker-disjoint); 16 kHz mono;
  **94,445 per-phone human judgments** (score 2: 87,215 / 1: 3,827 / 0: 3,403) with canonical
  ARPAbet; notation `()`=0 `{}`=1 bare=2 `[]`=insertion.
- **SIAK**: rating-only (no phone labels) → auxiliary acoustic data with rating weighting.
- **Zenodo 200495 (LWE corpus)**: test only (never trained).
- No dataset provides human phone *transcriptions* → supervised phone recognition impossible;
  quality-filtered head adaptation (B1) supported. CPU-only rules out encoder fine-tuning.

## 2. Phone inventory + data quality
- Inventory: 41 ARPAbet entries all map in PhoneInventory v1.4.0; so762 child phones 38,
  SIAK targets 39, CMUdict 39; no unmapped tokens (SIL/SPN unused).
- Quality: so762 no clipping, 16 kHz, median 3.53 s; SIAK sample median 0.85 s, 94 files >1%
  clipped; Zenodo 44.1 kHz, 11 speakers, top-share 13.1%.
- Domination: so762 51.2% adults overall → training uses **children only**.

## 3. Zero-shot baseline (Experiment A/C)
- Script: `experiments/zeroshot_phone_benchmark.py`; artifacts `artifacts/zeroshot/`.
- so762 test children: canonical exact **75.4%**, exact+soft 81.9%, miss 18.1%; by human phone
  score: 2 → exact 77.7% / miss 16.3%; 1 → 41.9% / 43.4%; 0 → 23.9% / 61.1%;
  **AUC (posterior, score2 vs score0) 0.825**; word-final consonant score2 exact **87.3%**.
- SIAK test: exact 69.1%, miss 24.0%; Pearson(soft, SIAK) 0.312 (reproduces 1.9.15).
- Runtime: so762 ~0.75 s/utt, SIAK 0.32 s/utt.
- Confusion: largest actual confusion ə→ɑ (414); stops t/n dominant correct.

## 4. B1 adaptation (frozen encoder + CTC head)
- Script: `experiments/train_head.py`.
- Head: 2×Conv1d 1024→512→392, ~3.6M params (13.8 MB), CTC loss; encoder frozen.
- Fit: so762 train children (1,160 utt) + SIAK train ratings ≥60 (~1,300 utt); weights =
  mean human phone score/2 (so762) and rating/100 (SIAK); speaker-disjoint validation
  (10 held-out so762 train-child speakers); best epoch 2 (val CTC 16.37), later overfit.
- **LWE audio/labels never used in training or selection.**

## 5. A/B results (same audio → same soft-v2 pipeline)
- Scripts: `experiments/eval_ab.py`, `ab_summary.py`; artifacts `artifacts/ab/`.
- **LWE external (n=48):** baseline FRR 31.0 / FAR 38.5 / AUC 0.659 → adapted FRR 20.7 / FAR 53.9 /
  **AUC 0.625** (worse). Final consonants (n=28): present recall 93.8% → **75.0%**; absent
  detection 58.3% → 33.3%; final FRR 6.3% → 25%.
- **SIAK test:** Pearson 0.307 → **0.355**, Spearman 0.272 → 0.333, FRR-proxy 12.2% → **6.6%**.
- **SIAK ages 4–6 (5 spk):** 0.184 → **0.373**, FRR-proxy 42.9% → 10.7% (limited).
- **so762 test children:** score2 exact 77.7→78.5, score1 41.9→51.6, score0 23.9→32.2;
  **word-final consonant score2 exact 87.3→78.6 (regression)**; AUC 0.825→0.808.
- Per-phone: improved ə +49.8, ð +15.1, θ +13.7, ʊ +13.1, aɪ +12.0, ɪ +11.5, uː +9.9, ɹ +6.0;
  degraded ɑ −26.0, w −25.9, p −23.3, h −15.9, z −13.8, k −12.8, t −12.6, iː −11.3.

## 6. Failure replay + generalization (E/I)
- Replay 12 PME + 6 SCORER_MISS: PME **5 FIXED / 2 partial / 3 unchanged / 1 partial-regressed /
  1 regressed** (e.g., child_06_two 0→100, child_01_seven 20→80; child_03_three 39.2→6.7);
  SCORER_MISS 5 unchanged / 1 regressed — deleted /r/ "four" still 100 (alignment cannot express deletion).
- Per-speaker: SIAK speakers Pearson up 12 / down 12; so762 child speakers exact up 37 / down 27; no
  accent/age collapse claimed.

## 7. Assessability + robustness (H/J)
- H (115 review clips, runnable 94): adapted raises mean on human-not-assessable 57.2→77.3 (n=5,
  risk signal); conf≥0.05 count unchanged (2 vs 2). Assessability stays a separate layer.
- J synthetic stress (24 tokens): both gain-invariant at −12 dB; +6 dB clipping/noise/telephone
  degrade both; adapted keeps ~7–9 pts higher but final-consonant recall stays below baseline.

## 8. Runtime + license
- Model 1.18 GB, head 13.8 MB, cold load 13.6 s, warm 0.585 s/short word, peak 1.75 GB.
- Licenses: so762 CC BY 4.0 OK; wav2vec2 Apache-2.0; SIAK CC-BY-ND nuance (legal review open,
  auxiliary only); Zenodo test-only.

## 9. Decision
**B = ADAPTATION_PROMISING_BUT_INSUFFICIENT.**
- B1 does not beat the frozen model under FRR-first (final-consonant regression; LWE AUC drop).
- Next-phase conditions: phone-level ground truth / broader corpora (MyST/CSLU/OGI or
  Vietnamese-L1), encoder-level adaptation (GPU), category-balanced objective, deletion-aware
  alignment kept in the scorer path.

## 10. Artifacts (committed)
```
Research/Speech/Phase1_9_16/
  PHASE_1_9_16_REPORT.md, FEASIBILITY_AND_INVENTORY.md, ZERO_SHOT_BASELINE.md,
  AB_ADAPTATION_RESULTS.md, REPLAY_AND_GENERALIZATION.md, ASSESSABILITY_AND_ROBUSTNESS.md,
  RUNTIME_AND_LICENSE.md
  experiments/ (12 scripts incl. train_head, eval_ab), artifacts/{audit,inventory,quality,
  zeroshot,ab,replay,generalization,stress,runtime}, manifests/, artifacts/ab/head.pt
```

## 11. Git
- Commit `e8d96da` — "phase1.9.16: child phone adaptation audit and A/B falsification" —
  pushed and fast-forward merged into `main`.
- Feature caches (~520 MB) deleted after training; `train_head.py` regenerates them (~25 min CPU).
