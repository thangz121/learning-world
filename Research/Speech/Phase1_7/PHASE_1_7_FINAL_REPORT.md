# Phase 1.7 FINAL REPORT — Real audio pilot (IMG_0639.mp3)

Date: 2026-10-02. ASUS CPU-only.  
Frozen baseline: Phase 1.6 **`bdc5851`**.  
**Original `IMG_0639.mp3` untouched. Not committed to git. No invented age/language/target.**

---

## 1. Scope
Confront the speech stack with the first real-world root audio artifact. Inspect, preserve, segment, classify research role, run applicable pipeline stages, prepare scalable real-audio ingestion. **Not** a calibration fit from one file.

## 2. Frozen Phase 1.6 baseline
Protocol `lwe-speaking-protocol-1.6.0`; word_ctc_forced_v1; soft-v2; SO762 54 test speakers. Unchanged.

## 3. IMG_0639 metadata

| field | value |
|---|---|
| path | `D:\Vscode\little-world-english\IMG_0639.mp3` |
| size | 5,264,528 bytes |
| SHA-256 | `E4CDF24D2B5140B91EF637676CD5FA1B9A7F6B6A629F62AE9AC2657CB171A6F7` |
| container | mp3 |
| codec | mp3 |
| sample_rate | **48000** |
| channels | **2 (stereo)** |
| duration | **219.284 s** |
| bit_rate | 192000 |
| derived | 16 kHz mono PCM WAV (copy only) |
| external copy | `D:\speech-lab\data\real-audio\` |
| original modified | **NO** |

## 4. Audio quality analysis

| metric | value |
|---|---|
| rms | 0.094 |
| peak | 0.790 |
| clip_frac | ~0 |
| silence_ratio (\|x\|<0.01) | 0.129 |
| snr_proxy_db | ~19.6 |
| energy 1s windows | continuous ~0.05–0.15 (no clear speech/silence islands) |

**Finding:** near-continuous energy profile unlike clean TTS benchmarks.

## 5. Speech segmentation (VAD)

| setting | result |
|---|---|
| Silero default (Phase pipeline) | **0 segments**, speech_detected=False |
| threshold 0.1 | 1 short span ~1.3–1.7 s |
| threshold 0.05 | 1 span ~0.7–1.7 s |

**Failure class: VAD**  
Real continuous-energy audio defeats default Silero settings tuned on clean short TTS clips.

Fallback analysis used **fixed 8s/10s windows** (not claimed as ground-truth speech segments).

## 6. Language / content analysis

| probe | result | reliability |
|---|---|---|
| Moonshine-tiny-en (EN-only) fixed 8s | 4/28 nonempty; texts often repetitive/hallucinated style | EN-only; cannot confirm VI |
| faster-whisper tiny 10s LID | lang mix en/ja/zh/ko/vi; many empty; low p often | **LOW** |

**language_global = UNKNOWN**  
One window tagged `vi` p≈0.67 is **insufficient** to label the full file Vietnamese.  
Human listen required for definitive language ID.

## 7. Speaker-age status
**UNKNOWN** — no authorized metadata; no age inference claimed.

## 8. Pipeline results
- Full Phase 1.6 pronunciation path **not applied** as primary: **no authorized known English target**.
- ASR-hypothesis score probes: **not used as GT** (policy explicit).
- Stages run: quality, VAD, fixed-window ASR, optional LID, whole-file acoustics, synthetic compare.

## 9. Real vs synthetic compare (sapi_red)

| property | IMG_0639 | sapi_red TTS |
|---|---|---|
| duration | 219 s | ~1.2 s |
| energy profile | continuous | speech then silence |
| Silero VAD default | **0 segs** | detects speech |
| RMS | 0.094 | lower, gated |
| pipeline comfort | fails default VAD | works |

## 10. Failure forensics

| layer | status |
|---|---|
| decode | OK |
| VAD | **FAIL** default on continuous energy |
| EN ASR | sparse/possibly hallucinated |
| LID | unreliable tiny model |
| pronunciation score | **NOT RUN** (no known target) |
| target invention | **STOPPED** (correct) |

## 11. Acoustic baseline
Whole-file Parselmouth extracted (f0/f1/f2/intensity) — usable as **recording-condition / speaker-unknown acoustic snapshot**, not English phone targets.

## 12. Pronunciation eligibility
**False** without authorized target text.  
Do not score Vietnamese/unknown content against English CMUdict.

## 13. Research role classification (evidence-based)
- REAL_SPEAKER_UNKNOWN_AGE  
- REAL_MICROPHONE_OR_DEVICE_RECORDING_CONDITION_SAMPLE  
- VAD_STRESS_CASE_CONTINUOUS_ENERGY  
- PIPELINE_REAL_AUDIO_FAILURE_DISCOVERY  
- NOT_CANONICAL_REFERENCE  
- NOT_GOLDEN_CHILD_VOICE  
- NOT_ENGLISH_PRONUNCIATION_BENCHMARK_WITHOUT_KNOWN_TARGET  

## 14–15. Real-child readiness & recording protocol
Docs under `RealChildPilot/` and `Protocol/`:
- imitation / elicitation / natural / native-language baseline  
- compact phoneme set  
- human review schema  
- privacy: no git commit of real audio  

## 16. Privacy / consent
- `consent_status`: **unknown**  
- `privacy_sensitive`: true  
- gitignore: `IMG_0639.mp3`, `AudioDerived/`  

## 17. Runtime (ASUS)
- VAD full file ~3.6 s then 0 segs  
- Fixed-window Moonshine ~28×8s windows  
- faster-whisper tiny ~22×10s windows  
- Total analysis on order of minutes (CPU)

## 18. Remaining gaps
- Human language identification listen  
- Consent/metadata for speaker age  
- VAD retune/retrain path for continuous real audio  
- Real-child consented corpus still absent  

## 19. Phase 1.8 recommendation
1. Human-listen pass on IMG_0639 (language/content labels) without claiming GT pronunciation.  
2. VAD robustness work for continuous real mic energy.  
3. If EN targets become known for segments → optional pronunciation run.  
4. Collect multi-child consented pilot under existing protocol.  
5. Only then expand Unity adapter beyond smoke.

---

## Acceptance checklist

| ID | Criterion | Status |
|---|---|---|
| A | Located + hashed | **PASS** |
| B | Original untouched | **PASS** |
| C | Metadata documented | **PASS** |
| D | Speech activity assessed | **PASS** (VAD fail + energy) |
| E | Language without unsupported assumption | **PASS** (UNKNOWN) |
| F | Pipeline run where applicable | **PASS** |
| G | Pron score only with valid target | **PASS** (not run) |
| H | VI not EN-scored | **PASS** (no EN score on unknown) |
| I | Not canonical truth | **PASS** |
| J | No false child claim | **PASS** |
| K | Child pilot infra ready | **PASS** |
| L | Future modes A–D documented | **PASS** |
| M | Sensitive audio not in git | **PASS** (gitignore) |
| N | Failures classified | **PASS** |
| O | Phase 1.6 reproducible | **PASS** |

## Artifacts
- `Research/Speech/Phase1_7/Results/IMG_0639_analysis.json`
- `Results/fixed_window_asr.json`, `faster_whisper_windows.json`
- Derived WAV gitignored; original root mp3 gitignored
- External: `D:\speech-lab\data\real-audio\`
