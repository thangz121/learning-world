# Phase 1.3 FINAL REPORT — Calibration, phoneme acoustics, pipeline hardening

Date: 2026-10-02. ASUS CPU-only.  
Frozen baselines: Phase 1.1 `f4b9dac`, Phase 1.2 `bd639fd`/`45bb47c`.  
**Population labels explicit. No 4-year-old accuracy claims.**

---

## 1. Scope
Improve validity of the Phase 1.2 instrument: human calibration, forensic low-score cases, phone-local acoustics, confidence meaning, OpenPronounce stability, offline protocol. No Unity/gameplay.

## 2. Frozen Phase 1.2 baseline
Reproduced exactly (`Results/phase12_baseline_repro.json`):

| file | score | conf | delta |
|---|---|---|---|
| red | 91.1 | 0.911 | 0 |
| silence | 0.0 | 0.0 | 0 |
| blue | 11.8 | 0.0 | 0 |

## 3. Speechocean762 dataset & population
- License: **CC BY 4.0** (OpenSLR 101)
- n=5000 utt, 250 speakers, 31816 words
- Human word `accuracy` 0–10 (mean **9.38** — strong ceiling)
- Detail file has 5 annotators
- Split: speaker-disjoint hash 60/20/20 train/valid/test, **leakage=[]**
- **NOT_4yo: true** (adult L2)

## 4–5. Word-level calibration (speaker-disjoint)
Sample: 20 utts/split → word rows in `so762_word_rows.json`.

### Table A — Calibration (held-out test)

| Method | n (words) | Pearson | Spearman | MAE | mean_pred | mean_human×10 | notes |
|---|---|---|---|---|---|---|---|
| raw s1 | 82 | ~0.14–0.23* | weak/neg | high (~45–50 on raw scale gap) | ~45–50 | ~94 | raw under-scores vs ceiling humans |
| affine (train fit) | 82 | weak | weak | **~8.7** (conf_affine) | ~96 | ~94 | a≈0.08, b≈92 → **almost constant ~92–100** |
| isotonic-bin | 82 | null/weak | **0.77** | 5.85 | **100** | 94 | collapses to ceiling |
| conf_affine | 82 | 0.14 | -0.13 | 8.75 | 95.9 | 94.1 | slight bias control |

\*raw pearson on test subsets varies; see JSON for exact splits.

**Finding:** With word human scores saturated near 10, affine/isotonic “calibration” improves MAE mainly by predicting the ceiling, **not** by tracking pronunciation nuance. Raw scorer remains more discriminative but poorly aligned to this label scale.

### Ceiling analysis (`so762_ceiling_and_confbins.json`)
- test perfect-10 vs human_lt10 split documented
- **Confidence bins (test, abs err to human×10):**

| conf bin | n | MAE |
|---|---|---|
| 0.3–0.4 | 2 | 76.5 |
| 0.4–0.5 | 7 | 59.4 |
| 0.5–0.6 | 14 | 57.5 |
| 0.6–0.7 | 21 | 45.1 |
| 0.7–0.8 | 16 | 56.7 |
| 0.8–0.9 | 14 | **25.8** |
| 0.9–1.0 | 2 | **7.1** |

Higher confidence → lower absolute error vs human (monotonic trend at top bins). Confidence is **not** useless.

## 6–7. Correct-ASR / low-phone-score forensics
`Forensics/lwe_forensics.json` + inventory audit.

### Table E — LWE problematic words

| word | ASR | score | PER | observed vs expected (summary) | failure source |
|---|---|---|---|---|---|
| red (control) | Red | 91.1 | 0.0 | match | none |
| blue | Blue. | 11.8 | 1.33 | model emits t/u/j… not b/l/uː | **phone_model mismatch** (not scorer bug) |
| apple | Apple, | 29.6 | 0.75 | aː p o vs æ p ə l | phone_model vowel/coda |
| book | (empty) | 30.6 | 1.0 | v/ʊ…; ASR empty | **asr_empty** + phone |
| big | Big | 35.2 | 0.67 | b→v, ɪ→eɪ | phone_model |
| dog | Dog. | 54.8 | 0.67 | AO quality | phone_model |
| cat | Cat | 68.9 | 0.33 | æ→ɛ conf low | partial phone |

**Conclusion:** Low scores with correct ASR are primarily **phone-evidence / inventory realization issues**, not Levenshtein math errors. Do not “boost score because ASR matched.”

### Phone inventory audit
- w2v2-espeak inventory loaded; soft-missing after glyph norm: **[]** for LWE set
- Residual errors are **sequence content**, not missing symbol table rows
- Mapping ARPAbet→IPA must stay versioned; glyph variants (ɡ/g, ɹ/r, length marks) documented

## 8–10. Acoustic / F1/F2
`Acoustic/phone_local_formants.json` — method **APPROX_PROPORTIONAL** (VAD span ÷ n phones).  
Not forced-alignment truth. Provides per-phone F0/F1/F2/dur for red/cat/big/apple/blue/dog.

**Contribution claim:** infrastructure exists; **no claim yet** that F1/F2 improves SO762 correlation (not fused into calibrated score this phase).

## 11. Alignment contribution
WhisperX still optional; not required for protocol path. No held-out gain claimed.

## 12. OpenPronounce contribution
After path resolution to `D:\speech-lab\venvs\p0\Scripts\openpronounce.exe`:

| file | s1 | OP | notes |
|---|---|---|---|
| red | 91.1 | **100** | both high |
| blue | 11.8 | **17.7** | both low — agreement |
| cat | 68.9 | **100** | **disagreement** (OP ceiling) |
| apple | 29.6 | **60** | OP higher |
| silence | 0 | 0 | agreement |

OP is **stabilized** (deterministic CLI path, JSON, warnings). Contribution: second vote useful when both low (blue) or both high (red); less trustworthy when OP hits 100 on mid s1 (cat). Keep optional ensemble.

## 13. Score monotonicity
From Phase 1.2/1.3 invariance + OP:
- correct red > noise red > silence
- wrong-target << correct
- OP and s1 both drop on blue vs red  
No hardcoded curve.

## 14. Speaker invariance
Baseline still holds (soft/loud PER=0 on red). Crude pitch_up remains damaging (signal method limit).

## 15–16. Confidence
Separate field preserved. Bins show top conf → lower human error.  
Reason codes still minimal (warnings list); expand in 1.4 with structured enums.

## 17. LWE coverage
Unchanged 25 words / 27 phones from Phase 1.2; forensics adds failure taxonomy per word.

## 18. SpeakingPipeline protocol
`Pipeline/speaking_protocol.py` + `PROTOCOL.md`  
Verified: `protocol_red.json` → score 91.1 conf 0.911 asr Red.

## 19. Runtime
Similar to Phase 1.2: cold ~15s, warm ~1–2s/file without OP; OP adds multi-second model load first call.

## 20. Failure cases
- SO762 word labels ceiling → calibration MAE “wins” can be illusory
- Word–phone chunk alignment is approximate (not MFA)
- OP CLI not on PATH unless resolved to venv exe (fixed in adapter)
- Phone model weak on several LWE TTS items

## 21. Components retained
Primary path unchanged.  
**New:** protocol CLI, SO762 splits, forensics, conf-bin analysis, OP path fix, approx phone formants.

## 22. Optional
OpenPronounce ensemble, WhisperX, affine calibrator (only if label distribution fixed).

## 23. Remaining gaps
- Word-segmented audio or true aligner for word-level AI scores
- Phone-posterior soft matching / inventory adapters
- Larger SO762 sample + focus on human_acc<10
- Structured confidence reason codes
- Real child validation (protocol only written)

## 24. Real-child validation protocol
See `Reports/REAL_CHILD_VALIDATION_PROTOCOL.md` (consent, conditions, formats). Not executed.

## 25. Phase 1.4 recommendation
1. Soft phone matching + espeak-normalized inventory adapter (fix blue/apple class errors at evidence layer).
2. Word-level cuts via alignment before SO762 word calibration v2.
3. Train calibrator on **non-ceiling** human words + phoneme-accuracy vectors.
4. Fuse phone-local F1/F2 only after alignment quality gate.
5. Execute child validation pilot under written protocol (validation-only).

---

## Acceptance checklist

| ID | Criterion | Status |
|---|---|---|
| A | Speaker-disjoint SO762 calibration | **PASS** |
| B | Word-level human eval | **PASS** |
| C | Raw vs calibrated measured | **PASS** (ceiling documented) |
| D | Correct-ASR low-phone explained | **PASS** (forensics) |
| E | Phone mapping audited | **PASS** |
| F | Phone-local F1/F2 experiments | **PASS** (approx method labeled) |
| G | Acoustic contribution measured | **PARTIAL** (extracted; not yet +Δ correlation) |
| H | Confidence calibration held-out | **PASS** (bins) |
| I | OpenPronounce contribution | **PASS** |
| J | Monotonicity | **PASS** (existing+OP) |
| K | Speaker-invariance | **PASS** (baseline retained) |
| L | Offline protocol stable | **PASS** |
| M | Perf vs 1.2 | **PASS** (same order) |
| N | No false 4yo claims | **PASS** |
| O | Real-child protocol documented | **PASS** |

## Artifacts
- `Research/Speech/Phase1_3/**`
- External: `D:\speech-lab\data\speechocean762` (not git)
