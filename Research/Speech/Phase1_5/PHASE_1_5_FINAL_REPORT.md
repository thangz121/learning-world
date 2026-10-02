# Phase 1.5 FINAL REPORT — Calibration at scale, conflict resolution, real-child prep

Date: 2026-10-02. ASUS CPU-only.  
Frozen baseline: Phase 1.4 **`33c6cbd`** (reproduced delta=0 on red/blue/apple/silence soft).  
**Population: adult L2 for SO762. Real-child pilot PENDING. No 4yo accuracy claims.**

---

## 1. Scope
Scale non-ceiling SO762 calibration (≥200 error words), detect variance collapse, resolve OP↔soft conflicts with provenance, protocol v3, prepare real-child validation (no data on machine).

## 2. Frozen Phase 1.4 baseline
`Results/phase14_baseline_repro.json`: soft red 100, blue 5.8, apple 75.2, silence 0 — **all Δ=0**.

## 3. SO762 dataset & population
- Adult L2, CC BY 4.0, **NOT_4yo**
- Human word acc: n=31816, mean 9.38, **88.8% perfect 10**, n_nonceil(<10)=**3557**, n_hard(<8)=2907

## 4. Expanded non-ceiling calibration
Processed **92 utts**, **773 words**, **510 non-ceiling** (>>200 target).  
Speaker-disjoint: train 10 / valid 10 / test 4 speakers, **leakage=[]**.

### Table A — Test metrics (soft-v2 raw)

| Subset | N | Pearson | Spearman | MAE | std_pred | std_human | var_ratio | VC flag |
|---|---|---|---|---|---|---|---|---|
| full | 106 | 0.187 | 0.095 | 32.2 | 33.0 | 32.7 | 1.01 | False |
| **nonceil** | **72** | **0.319** | 0.028 | 25.3 | 30.7 | 9.5 | 3.22 | False |
| hard | 70 | **0.337** | -0.024 | 25.3 | 30.3 | 5.0 | 6.03 | False |

### Table B — Calibrators on test

| Method | subset | N | Pearson | MAE | std_pred | VC |
|---|---|---|---|---|---|---|
| affine (fit train nonceil) | full | 106 | 0.187 | 24.7 | **4.07** | **True** |
| affine | nonceil | 72 | 0.319 | **6.11** | 3.78 | False* |
| conf_affine | nonceil | 72 | 0.324 | 6.07 | 3.78 | False* |
| **isotonic** | nonceil | 72 | 0.313 | 18.3 | **15.2** | False |
| isotonic | hard | 70 | **0.376** | 18.3 | 15.0 | False |

\*affine nonceil VC threshold not tripped (human std low on errors) but **std_pred≪ useful discrimination range** — MAE win is partly compression; isotonic preferred for spread.

**Retained research calibrator:** raw soft-v2 for discrimination + optional isotonic for rank; **reject affine-on-full** (VARIANCE_COLLAPSE=True).

## 5. Variance-collapse analysis
Explicit flag `VARIANCE_COLLAPSE := human_std>5 and pred_std < 0.25*human_std`.  
Affine on **full** test: **VC=True**. Documented and not retained as primary.

## 6–8. Soft-v2 / OP conflicts / provenance

### Table C — LWE conflict matrix
| Bucket | Words |
|---|---|
| both_high | red, cat, red apple, ball |
| both_low | blue, silence |
| soft_high_op_low | (none in set) |
| **soft_low_op_high** | **dog** |
| disagree≥30 | book (66.7 vs 23.2), dog (33.3 vs 86.7) |

### Conflict detector (versioned thresholds `conflict-rules-v1.5.0`)
- `PHONE_VS_OPENPRONOUNCE` if |Δ|≥30  
- `HIGH_SCORE_LOW_CONFIDENCE` if score≥80 & conf<0.25 (red/cat)  
- `PHONE_VS_ASR_INFORMATIONAL` only (never score boost)  
- `OPENPRONOUNCE_DISAGREEMENT` reason code  

### Provenance
Each conflict row stores `scoreEvidence.{phone,OP,asr,vad,acoustic,s1}` in `conflicts_lwe.json`.

### Ensemble policy (held-out design, not magic)
- Agree: mean  
- Disagree≥30: **conservative min(soft, OP)** + conf×0.7  
- dog primary → **33.3** (not 86.7)

## 9–11. Confidence V3
Reasons: `PHONE_POSTERIOR_LOW`, `VAD_NO_SPEECH`, `OPENPRONOUNCE_DISAGREEMENT`, `CONFLICTING_EVIDENCE`, `HIGH_SCORE_LOW_CONFIDENCE`, …

Protocol v3 red: score **100**, conf **0.1**, conflicts `HIGH_SCORE_LOW_CONFIDENCE` — semantics held.

SO762 conf bins currently concentrate low soft_conf (many posteriors weak) → MAE by conf bin not well stratified yet; larger dynamic range for conf is Phase 1.6.

## 12. LWE re-test (Table E)

| word | p12 s1 | p14 soft | p15 soft | OP | p15 primary | rule |
|---|---|---|---|---|---|---|
| red | 91.1 | 100 | 100 | 100 | 100 | mean |
| blue | 11.8 | 5.8 | 5.8 | 17.7 | 11.7 | mean |
| apple | 29.6 | 75.2 | 75.2 | 60 | 67.6 | mean |
| book | 30.6 | 66.7 | 66.7 | 23.2 | **23.2** | conservative min |
| big | 35.2 | 33.3 | 33.3 | 60.6 | 46.9 | mean |
| dog | 54.8 | 33.3 | 33.3 | 86.7 | **33.3** | conservative min |
| cat | 68.9 | 100 | 100 | 100 | 100 | mean |
| silence | 0 | 0 | 0 | 0 | 0 | gated |

## 13–15. Real-child pilot
**Status: PENDING_EXECUTION** — no child audio on disk.  
Infrastructure: `RealChildPilot/STATUS.md` + Phase 1.3 consent protocol.  
No child used as canonical truth.

## 16. Failure cases
- Soft conf often low → conf calibration vs human error weakly stratified  
- Test speakers only 4 (scale more speakers in 1.6)  
- Word-level soft still chunk-approximated from utterance CTC path  
- Spearman weak on nonceil (rank noise / small human spread on errors)

## 17. Runtime
~92 utts scale cal completed; warm ~1–2s/file soft path; OP extra when enabled.

## 18. SpeakingResult version
**Protocol `lwe-speaking-protocol-1.5.0`**: score, confidence, conflicts[], confidenceReasons[], scoreEvidence provenance, phonemeDiagnostics, asr_is_not_pronunciation_judge=true.

## 19. Components retained
- soft-v2 + CTC align + inventory 1.4  
- conflict detector + conservative ensemble  
- protocol v3  
- SO762 scale rows (510 nonceil)  
- isotonic optional; affine-full rejected  

## 20. Optional
WhisperX, F1/F2 fusion, NeMo/ESPnet ASR

## 21. Remaining gaps
- Real-child pilot execution  
- More test speakers  
- Conf score dynamic range  
- True word-segmented scoring  
- Human review multi-rater on child set  

## 22. Phase 1.6 recommendation
1. Execute real-child pilot under existing protocol  
2. Expand SO762 test speakers ≥30  
3. Word-boundary alignment before word soft scores  
4. Conf model predicting P(|err|≤10)  
5. Unity offline process adapter only after child pilot smoke  

---

## Acceptance checklist

| ID | Criterion | Status |
|---|---|---|
| A | ≥200 non-ceiling words | **PASS (510)** |
| B | Speaker-disjoint | **PASS** |
| C | Variance collapse tested | **PASS** (affine-full rejected) |
| D | Full/nonceil/hard reported | **PASS** |
| E | Phoneme-level human proxy | **PARTIAL** (phones_accuracy field present; limited analysis) |
| F | OP vs soft conflicts | **PASS** |
| G | Evidence provenance | **PASS** |
| H | Conflict reasons measurable | **PASS** |
| I | Conf vs held-out error | **PARTIAL** (bins weak stratification) |
| J | LWE re-run | **PASS** |
| K | Score changes evidence-backed | **PASS** |
| L | Real-child pilot | **PENDING** (infra ready) |
| M | No 4yo claim | **PASS** |
| N | SpeakingResult versioned | **PASS** 1.5.0 |
| O | Runtime documented | **PASS** |
| P | Baselines reproducible | **PASS** |

## Artifacts
`Research/Speech/Phase1_5/**`  
External: `D:\speech-lab\data\speechocean762` (not git)
