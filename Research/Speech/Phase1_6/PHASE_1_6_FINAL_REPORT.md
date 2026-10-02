# Phase 1.6 FINAL REPORT — Word alignment, SO762 speaker scale, confidence reliability, Unity harness

Date: 2026-10-02. ASUS CPU-only.  
Frozen: Phase 1.5 **`e051ba2`**, Phase 1.4 `33c6cbd`.  
**Real-child pilot: BLOCKED_NO_DATA. No 4yo accuracy claims. No ASR score boosts.**

---

## 1. Scope
True word-boundary alignment; SO762 ≥30 test speakers; confidence → P(|err|≤10); conflict classification; Unity offline smoke harness. Real-child execution blocked by absence of consented data.

## 2. Frozen Phase 1.5 baseline
Prior soft anchors held (red 100, blue 5.8, apple 75.2, silence 0). Conflict policy from 1.5 treated as heuristic only.

## 3. True word alignment
**Method:** `word_ctc_forced_v1`  
CTC force-align full phone sequence → group contiguous phone spans by CMUdict per-word phone counts.

### Evidence (LWE)
`red apple` wordDiagnostics:
- **red**: t0=0.000 → t1=**0.343** s, score 100
- **apple**: t0=**0.343** → t1=1.474 s, score 100  

Not `duration/n_words`. Method labeled; not MFA ground-truth timing.

### Table C
| method | status | CPU | note |
|---|---|---|---|
| proportional VAD÷n | deprecated | — | Phase 1.3 |
| phone CTC only | Phase 1.4 | — | utterance phones |
| **word_ctc_forced_v1** | **RUN** | +small | word spans from phone CTC |

## 4. Word-level scoring
Each word gets soft score/conf from its phone hits only. Multiword utterances produce `wordDiagnostics[]`.

## 5. SO762 expanded speakers

### Table A
| split | speakers | utts selected | words | nonceil |
|---|---|---|---|---|
| train | **142** | 40 | | |
| valid | **54** | 40 | | |
| **test** | **54** (≥30) | 40 | **297** test words | **185** nonceil |
| total scored | | 120 | **964** | **623** |
| leakage | **[]** | | | |

### Table B — Test calibration (word-aligned soft)
| subset | N | Pearson | Spearman | MAE | std_pred | std_human | VC | P(\|e\|≤10) |
|---|---|---|---|---|---|---|---|---|
| full | 297 | **0.327** | **0.334** | 31.1 | 35.6 | 34.3 | False | 0.286 |
| **nonceil** | **185** | **0.407** | **0.285** | 25.5 | 32.1 | 13.2 | False | 0.227 |
| hard | 177 | **0.404** | 0.243 | 25.4 | 31.3 | 8.7 | False | 0.232 |

**Improvement vs Phase 1.5 test nonceil (n=72, P=0.32, S≈0.03):** larger N, stronger Spearman, more test speakers.

## 6–8. Confidence calibration
**Semantics (adult-L2 only):** isotonic map conf bin → empirical P(|score−human|≤10).  
Brier ≈ **0.193**; mean predicted P 0.281 vs actual rate 0.286.

### Table E (test)
| conf bin | N | P(≤5) | P(≤10) | P(≤15) | MAE |
|---|---|---|---|---|---|
| 0.0–0.1 | 259 | 0.17 | 0.24 | 0.26 | 33.4 |
| 0.1–0.2 | 30 | 0.63 | 0.63 | 0.63 | 16.2 |
| 0.2–0.3 | 6 | 0.50 | 0.50 | 0.50 | 17.8 |
| 0.3–0.4 | 2 | 1.00 | 1.00 | 1.00 | 0.0 |

Higher conf bins → higher reliability (small N at top). **Not transferred to children.**

## 9–10. Conflicts & provenance

### Table D (LWE)
| word | soft | OP | class | word spans |
|---|---|---|---|---|
| dog | 33.3 | 86.7 | **SOFT_LIKELY_WEAK_PHONE_MODEL** | dog full span |
| book | 66.7 | 23.2 | ASR_EMPTY_SOFT_NONZERO | |
| big | 33.3 | 60.6 | BOTH_UNCERTAIN | |
| red/cat | 100 | 100 | AGREE + HIGH_SCORE_LOW_CONF | |
| blue | 5.8 | 17.7 | AGREE low | |

**Policy:** expose ensemble candidates; primary remains soft with conflict **flagged** (not silent min). Min/mean retained as candidates pending larger human-held-out OP labels (OP not run on full SO762 scale for CPU).

## 11. LWE forensic continuity
Word-align retest consistent with Phase 1.4/1.5 soft numbers; multiword segmentation verified.

## 12. Controlled phoneme
Prior pair matrix + soft matching retained; no new synthetic matrix this phase (CPU focused on SO762 scale).

## 13–16. Real-child pilot
**Status: BLOCKED_NO_DATA / PENDING_CONSENT**  
No child WAV on machine. Infrastructure ready.  
**No child-as-canonical-truth. No 4yo accuracy number.**

Human multi-rater: N/A until data exists.

## 17. SpeakingResult V4
Protocol **`lwe-speaking-protocol-1.6.0`**: wordDiagnostics, scoreEvidence.ensembleCandidates, confidenceSemantics, conflicts, asr_is_not_pronunciation_judge.

## 18. Unity offline adapter
`UnityAdapter/run_smoke_harness.py` — headless process call:

| case | ok | score | conf | words |
|---|---|---|---|---|
| red | True | 100 | 0.1 | 1 |
| red apple | True | 99.4 | 0.12 | **2** |
| silence | True | 0 | 0 | 0 |

**No Unity Editor / no gameplay.**

## 19. Runtime
~120 utts SO762 word-align completed; warm path similar order to 1.5 (+ word group overhead small).

## 20. Failure cases
- Conf still massed in 0.0–0.1 (posterior weak)  
- OP not scored on full SO762 (cost) — conflict policy not fully human-validated at scale  
- Word align depends on CMUdict phone counts matching CTC hit count  
- Real child blocked  

## 21. Components retained
- word_ctc_forced_v1  
- soft-v2 + inventory  
- SO762 54 test speakers eval  
- conf isotonic P(|e|≤10) adult-L2  
- protocol 1.6 + Unity smoke harness  
- conflict classification  

## 22. Optional
WhisperX timing GT, OP-full SO762, F1/F2 fusion, real-child when consented

## 23. Remaining gaps
- Real-child pilot execution  
- OP human-held-out policy selection  
- Conf dynamic range  
- MFA/WhisperX boundary error vs CTC word spans when GT exists  

## 24. Phase 1.7 recommendation
1. Collect consented multi-child pilot  
2. Multi-rater human review  
3. Select conflict policy on SO762 with OP features if cost allows  
4. Wire Unity production caller only after child smoke  

---

## Acceptance checklist

| ID | Criterion | Status |
|---|---|---|
| A | True word-boundary alignment | **PASS** (word_ctc_forced_v1) |
| B | Word-level soft on real spans | **PASS** |
| C | ≥30 test speakers | **PASS (54)** |
| D | leakage=[] | **PASS** |
| E | Full/nonceil/hard reported | **PASS** |
| F | Variance not collapsed | **PASS** |
| G | Conf reliability interpretation | **PASS** (adult-L2 P≤10) |
| H | Conf held-out eval | **PASS** |
| I | OP vs soft analyzed | **PASS** |
| J | Conflict evidence-based | **PARTIAL** (classified; full SO762 OP policy pending) |
| K | Provenance | **PASS** |
| L | LWE re-eval | **PASS** |
| M | Real-child pilot | **BLOCKED_NO_DATA** |
| N | Child ≠ canonical | **PASS** |
| O | Multi-rater | **N/A** (no child data) |
| P | No false 4yo claim | **PASS** |
| Q | SpeakingResult versioned | **PASS** 1.6.0 |
| R | Unity adapter smoke | **PASS** (headless) |
| S | Baselines reproducible | **PASS** |

## Artifacts
`Research/Speech/Phase1_6/**`  
External data/models: `D:\speech-lab\`
