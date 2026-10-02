# Phase 1.2 FINAL REPORT — Speaking Foundation / Research Stack Integration

Date: 2026-10-02. Machine: ASUS CPU-only.  
Phase 1.1 freeze commit: `f4b9dac`.  
Population labels are explicit. **No claim of 4-year-old performance.**

---

## 1. Scope
Headless pipeline only. No Unity, no English gameplay, no NPC/quest work.  
Goal: wire Phase 1.1 retained components into one inspectable audio→SpeakingResult path and measure.

## 2. Phase 1.1 baseline (unchanged)
Retained roles: moonshine STT, silero VAD, w2v2-espeak phone, whisperX timing (optional), OpenPronounce + scorer-v1, CMUdict target, Parselmouth acoustic.  
Scorer-v1 anchors: red 91.1/0.911; silence 0/0; wrong-target 1.8/0.

## 3. Integrated architecture
See `Phase1_2_Architecture.md`.

```
WAV → VAD → ASR → Target(CMUdict) → Phone → [Align optional] → Acoustic → Scorers → SpeakingResult
```

## 4. Data models
`Schemas/speaking_schemas.py`: SpeakingTarget, VadResult, AsrResult, PhoneEvidence, AlignmentResult, AcousticFeatures, PhonemeDiagnostic, PronunciationResult, SpeakingResult.  
**SCORE and CONFIDENCE are separate fields always.**

## 5. Adapter results

| Adapter | Status | Notes |
|---|---|---|
| Silero VAD | RUN | silence/noise → no speech |
| Moonshine ASR | RUN | red/blue/cat/apple ok; some empty ASR on book |
| CMUdict target | RUN | full LWE probe list resolved |
| w2v2 phone | RUN | IPA phones + conf |
| Parselmouth | RUN | F0/F1/F2/intensity |
| scorer-v1 | RUN | primary scorer |
| OpenPronounce | OPTIONAL | CLI path; LWE run used s1-only when OP slow/skip |
| WhisperX | DEFAULT OFF | optional flag; not required for acceptance path |

## 6. End-to-end LWE benchmark
`Results/lwe_summary.json` (adult-TTS/synthetic):

| file | target | ASR | score | conf | s1 PER |
|---|---|---|---|---|---|
| sapi_red | red | Red | **91.1** | **0.911** | 0.0 |
| sapi_blue | blue | Blue. | 11.8 | 0.000 | 1.333 |
| sapi_cat | cat | Cat | 68.9 | 0.518 | 0.333 |
| sapi_red_apple | red apple | Red apple | 49.0 | 0.341 | 0.429 |
| pregen_apple | apple | Apple, | 29.6 | 0.121 | 0.75 |
| stress_silence | red | (empty) | **0.0** | **0.0** | 1.0 |
| stress_noise | red | (empty) | **0.0** | **0.0** | 1.0 |
| sapi_big | big | Big | 35.2 | 0.281 | 0.667 |
| sapi_book | book | (empty) | 30.6 | 0.000 | 1.0 |
| sapi_dog | dog | Dog. | 54.8 | 0.202 | 0.667 |

First-run total ~15s (model load); subsequent ~1–2s/file on CPU.

## 7. Speechocean762 results
- **License:** CC BY 4.0 (OpenSLR 101). Downloaded 520MB, extracted under `D:\speech-lab\data\speechocean762` (not git).
- **Population:** adult L2 non-native English. **NOT_4yo: true.**
- **n=30 test utterances**, scorer-v1 only, human `total` 0–10 mapped ×10 for MAE only; rank metrics scale-free.

| metric | value |
|---|---|
| Pearson (s1 vs human×10) | **0.116** |
| Spearman | **0.034** |
| MAE (s1 vs human×10) | **20.76** |
| mean s1 | 62.3 |
| mean human×10 | 83.0 |

**Interpretation (measured, not marketing):** uncalibrated phone-Levenshtein scorer does **not** yet track sentence-level human total on this 30-utt slice. Bias: system mean lower than human. Phase 1.3 must calibrate (regression / phone-weighted mapping) and/or evaluate at word-level human scores (`scores-detail`).

## 8. Controlled / speaker-invariance
`Results/invariance_summary.json`:

| case | score | conf | PER | ASR |
|---|---|---|---|---|
| baseline red | 91.1 | 0.911 | 0.0 | Red |
| loud | 90.7 | 0.907 | 0.0 | Red |
| soft | 90.7 | 0.907 | 0.0 | Red |
| pitch_down | 79.5 | 0.795 | 0.0 | Red. |
| pitch_up (crude) | 59.8 | 0.504 | 0.333 | Red |
| noise snr5 | 55.6 | 0.306 | 0.333 | Rest. |
| noise snr0 | 33.7 | 0.165 | 0.667 | Right. |
| wrong target blue | **1.8** | **0.0** | 1.0 | Red |
| silence | **0.0** | **0.0** | 1.0 | (empty), VAD=false |

**Property held on this set:** speaker volume/pitch-down keep PER=0; phoneme/target error and silence collapse score+conf.

## 9–11. Acoustic / alignment contribution
- Acoustic runs every file; unvoiced whole-file applies **confidence penalty only** (not score rewrite).
- WhisperX default off in Phase 1.2 acceptance path (optional adapter present).
- No claim that F1/F2 yet improves human correlation (not fused into primary score beyond conf gate).

## 12. Score vs confidence
Maintained. Examples from live pipeline:
- correct clean → high/high
- cat mid → 68.9 / 0.518
- silence → 0 / 0
- wrong target → 1.8 / 0

## 13. Confidence calibration
Not fully binned vs human correctness yet (Speechocean correlation too weak for meaningful reliability bins).  
**Gap carried to Phase 1.3.**

## 14. LWE vocabulary coverage
`Results/lwe_phone_coverage.json`: 25 words resolved, **0 missing**, **27 unique ARPAbet bases** in Active-ish set.

## 15. Runtime / performance (ASUS CPU)
- Cold start (first file): ~15 s (load phone+ASR+VAD)
- Steady: ~1.0–1.7 s/file end-to-end without OpenPronounce
- Models external: `D:\speech-lab\models` (moonshine, w2v2, cmudict, silero via pip)

## 16. Failure cases
- OpenPronounce not always on PATH in bench → combined path may stay s1-only (documented).
- Phone model weak on some words (blue/apple/book) → low score even when ASR text correct (**ASR ≠ pronunciation** principle held).
- Speechocean sentence-level human total poorly correlated with uncalibrated phone PER scorer.
- Crude pitch_up still damages signal (not true VTLN).

## 17. Components retained for Phase 1.3
Pipeline as integrated; primary path:
**silero + moonshine + cmudict + w2v2-phone + parselmouth + scorer-v1**.  
OpenPronounce optional second vote. WhisperX optional timing.

## 18. Optional
OpenPronounce, WhisperX, NeMo/ESPnet cross-check ASRs (not required for headless path).

## 19. Remaining gaps
- Human calibration (word-level SO762 detail scores)
- Phone-local formants after alignment
- Real preschool validation (§22)
- Combined OP+s1 stable CLI packaging
- Confidence reliability bins

## 20. 4-year-old validation gap
**Still open.** No preschool audio in Phase 1.2 metrics. Speechocean762 ≠ children.

## 21. Phase 1.3 recommendation
1. Calibrate scorer-v1 to Speechocean762 **word-level** accuracy labels (not only utterance total).
2. Fit confidence bins on held-out SO762.
3. Phone-segmented F1/F2 for vowel pairs.
4. Package `SpeakingPipeline` as offline process protocol for future Unity adapter (still no gameplay).
5. Real-child pilot protocol (consent) as validation-only layer.

---

## Acceptance checklist

| Criterion | Status |
|---|---|
| A E2E pipeline audio→SpeakingResult | **PASS** |
| B Intermediate stages inspectable | **PASS** (JSON) |
| C SCORE ≠ CONFIDENCE | **PASS** |
| D Canonical target ≠ child voice | **PASS** (CMUdict) |
| E Controlled errors change score | **PASS** (wrong target / silence / noise) |
| F Speaker variation tests | **PASS** (soft/loud/pitch) |
| G Speechocean762 integrated | **PASS** (CC BY, n=30) |
| H Human correlation measured | **PASS** (weak; reported honestly) |
| I LWE coverage measured | **PASS** |
| J ASUS CPU performance measured | **PASS** |
| K Failures documented | **PASS** |
| L Artifacts reproducible | **PASS** (scripts + Results/) |
| M No false 4yo claim | **PASS** |

## Artifacts
- Code: `Research/Speech/Phase1_2/`
- Results: `Research/Speech/Phase1_2/Results/*.json`
- External data/models: `D:\speech-lab\` (not git)
