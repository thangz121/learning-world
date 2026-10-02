# Phase 1.1.2 DEEP SPEECH / PRONUNCIATION — Final Report

Date: 2026-10-02. Machine: ASUS CPU-only (no NVIDIA). Repo HEAD: cc4a259.
Corpus: Research/Speech/Phase1_1/audio (27 WAV, 16 kHz mono; SAPI + pregen + stress).
Label: adult-TTS / synthetic. **NOT real preschool child evidence.**
Principle §22 enforced: canonical target = CMUdict/IPA, never a child waveform.

---

## 1. Current project speech audit (evidence)

| Capability | Status in LWE production | Evidence |
|---|---|---|
| Mic capture PC | REAL code | `UnityMicrophoneCapture` 16 kHz |
| Mic capture phone | REAL transport | gateway WSS→TCP 8451, CT-P14 |
| VAD | REAL energy 0.004 only | no ML-VAD in production |
| Buffering | REAL | NetworkMicrophoneCapture 10 s cap |
| Recording | REAL | MediaRecordingService |
| STT transcript | **NONE shipped** | LocalAcousticProvider `Transcript=""`; Azure stubs |
| Pronunciation | SIMULATED DTW vs synth | AcousticAnalysis; child NOT PROVEN |
| Phoneme inventory | 6 words V1 only | TargetPronunciationCatalog |
| Forced alignment | NONE | — |
| Score 0–100 | NONE production | policy thresholds only |
| Confidence separate | partial (policy conf) | no phoneme conf |
| TTS | REAL Cloudflare worker | + pregen L2 |
| Recognizer wired to game | **NOT** | only CT-P12..P15 `new` |

Full Phase 1.1 audit + registry still valid.

---

## 2–5. Candidate inventory / executed / blocked

**Executed with audio numbers (Phase 1.1 + 1.1.2):**  
01 whisper, 02 whisper.cpp, 03 faster-whisper, 04 sherpa, 05 vosk, 06 silero-vad, 07 webrtcvad, 08 whisperX, 11 pron-bench features, 12 speechbrain, 13 espnet, 14 nemo, 16 phonemizer, 17 moonshine, 19 openpronounce, 20 CUPE, 32 wav2vec2-phoneme, 33 parselmouth, 34 cmudict, 36 score-conf-v1.

**Partial (method verified, no LWE audio score):** 28 gop-improved (byte-identical on repo sample).

**Blocked / Hold (reason):**
| ID | Reason |
|----|--------|
| 09 MFA | `_kalpy` missing Windows |
| 10 allophant | Rust/maturin + GPL chain |
| 15 kaldi full | no MSVC/cmake build |
| 18 kid-whisper | HF gated 401 |
| 21 BabAR | non-commercial license |
| 22 IF-MDD | no LICENSE file |
| 23 mdd-quang | no weights + AGPL |
| 24 mdd-grad | no weights + no LICENSE |
| 25 modelz-mdd | model host dead + no LICENSE |
| 26/27 gop-ft/pykaldi | PyKaldi Windows impossible |
| 29 kid_align | no MFA v1 binary + no LICENSE |
| 30 OGI-kids-phoneme | no checkpoint + no LICENSE |
| 31 kids corpora | LDC paid/sign-off, not downloaded |
| 35 ElevenLabs | no API key in env |

---

## 6. Capability matrix (measured only)

| Capability | Strongest measured evidence | ID |
|---|---|---|
| STT short-word accuracy | 6/6 exact + Red not "read" | 17 moonshine |
| STT speed | 0.02–0.04 s/file | 17 moonshine |
| STT + confidence logit | hyp scores on all 6 | 14 nemo |
| STT streaming API | KaldiRecognizer partials | 05 vosk |
| VAD soft/noise/tone | speech seg; noise+tone empty | 06 silero |
| VAD zero-cost baseline | noise FP 100% (contrast) | 07 webrtcvad |
| Word timestamps | Red 0.15–0.32 s | 08 whisperX |
| Phone CTC (IPA) | red→ɹ ɛ d conf~0.91 | 32 w2v2-phone |
| Phone CTC (CUPE) | red/cat/apple sequences | 20 CUPE (GPL) |
| Pronunciation 0–100 | 98.86 vs 6.79 wrong-word | 19 openpronounce |
| SCORE≠CONF + PER ops | red 91.1/0.911; silence 0/0; wrong-tgt 1.8 | 36 scorer-v1 |
| Acoustic F0/F1/F2 | tone 440.0004 Hz PASS | 33 parselmouth |
| Canonical ARPAbet | full LWE vocab exact | 34 cmudict |
| G2P IPA | espeak IPA all probes | 16 phonemizer (GPL) |
| G2P ARPAbet | g2p_en matches CMUdict | 13 espnet-g2p |
| GOP formula | improved-GOP byte-identical | 28 (method only) |
| Child-domain model | published WER only | 18 HOLD gated |
| MDD trained weights | none runnable | 23–25 BLOCKED |

---

## 7. Child-speech findings
- **No real 4-year-old audio in this lab.** All numbers = adult SAPI / game pregen / synth.
- Child-specific models (kid_align, OGI-kids-phoneme, LiteChildASR) blocked by license/weights/gated access.
- OGI/CMU Kids = LDC, research agreements, not downloaded.
- Speaker-variation proxy (pitch/amp on adult "red"): soft/loud/pitch-down keep PER=0; crude pitch-up and noise degrade — **proxy only, not child evidence**.

## 8. Phoneme-recognition findings
- w2v2-espeak-cv-ft: strong on `red`, weaker on `cat` (ɛ conf 0.39), weak on `apple` (aː p o).
- CUPE-2i: produces phone sequences, ~3 s/file, GPL, silence FP.
- allophant / OGI-kids-phoneme: not runnable here.

## 9. Forced-alignment findings
- whisperX: only working word-level aligner on this machine.
- MFA / kid_align: blocked (`_kalpy` / missing MFA v1 binary).

## 10. Acoustic-analysis findings
- Parselmouth F0 validated on 440 Hz tone.
- Whole-file F1/F2 means include silence → **need phone-segmented formants** before vowel-contrast claims.
- LWE AcousticAnalysis remains synthetic-calibrated only.

## 11. Pronunciation-scoring findings
- OpenPronounce discriminates correct vs wrong word on same audio family.
- Scorer-v1 (CMUdict + phone CTC + Levenshtein) produces score AND confidence separately; silence and wrong-target collapse correctly.
- apple/blue still weak on this phone model → multi-signal needed.

## 12. GOP findings
- Classical + improved GOP formulas documented; improved-GOP scorer core verified on bundled sample.
- Cannot compute GOP on LWE audio without Kaldi AM (Windows PyKaldi blocked).
- Retain formulas as Phase 1.2 signals if ONNX phone-posterior AM appears.

## 13. VAD findings
- silero >> webrtcvad on noise/tone rejection.
- Must gate STT (sherpa repetition; speechbrain silence garbage).

## 14. ASR findings
- Moonshine-tiny primary offline candidate (speed × accuracy × MIT).
- ESPNet/NeMo accuracy ties; NeMo adds hyp scores; heavier.
- Whisper family solid baseline; vosk streaming + small RAM with grammar opportunity.

## 15. Human-calibration findings
- **NOT RUN.** speechocean762 not downloaded (manual openslr).  
- Phase 1.2 required: PCC/Spearman/MAE vs human on speechocean762 + later real-child pilot.

## 16. 0–100 strategies tested
| Strategy | Status |
|---|---|
| OpenPronounce DTW+phone | RUN (98.86/6.79) |
| Phone-CTC × (1−PER) × conf | RUN scorer-v1 |
| Nemo hyp score as confidence | RUN (scores present) |
| GOP posterior | formula only |
| Formant distance | F0/F1/F2 measured; not yet in score |
| Embedding regression (pron-bench) | features run; no trained head on LWE |
| Waveform vs TTS | **REJECTED by §22** (not tested as score) |

## 17. Score vs confidence design findings
Mandatory separation proven:
- High score + high conf: red clean (91.1 / 0.911)
- Mid score + mid conf: cat (68.9 / 0.518)
- Low score + low conf: silence, wrong target
- Corruption can drop conf faster than collapsing PER (noise paths)

## 18. Canonical pronunciation profile findings
Proposed SpeakingTarget v1 (versioned):
```
{
  text, normalizedText,
  arpa: CMUdict[],           // stress digits kept
  ipa: optional RESEARCH_ONLY phonemizer,
  variants: close → S/Z etc.,
  stressPattern,
  dictVersion: "cmudict.dict@2026-10-02",
  phoneModelVersion, scoringProfileVersion,
  referenceAudioPath: cached TTS (Cloudflare/ElevenLabs), // teaching only
}
```
Child never writes this object.

## 19. Child-normalization findings
- Soft/loud amplitude: PER stayed 0 on red (good direction).
- True vocal-tract-length / formant scaling: **NOT implemented**; need phone-local F1/F2 after alignment.
- Do not use adult formant hard thresholds.

## 20. Composition experiments
| Combo | Result |
|---|---|
| silero → sherpa | PARTIAL (phrase fixed; single-word still repeats) |
| CMUdict + phone-CTC + Lev → score/conf | RUN (candidate 36) |
| OpenPronounce alone | RUN discrimination |
| VAD → ASR → phone → align → GOP | NOT fully runnable (GOP AM missing) |
| phone + F1/F2 + duration | F1/F2 available; not fused yet |

## 21. Components worth retaining
**Production-path research shortlist:**  
17 moonshine, 06 silero, 03 faster-whisper (conf), 08 whisperX (timing), 19 openpronounce, 32 phone-CTC, 34 cmudict, 36 scorer-v1, 33 parselmouth, 05 vosk (streaming/grammar), 02 whisper.cpp (native shape), 14 nemo (conf research), 13 espnet (acc+G2P), 28 gop-improved formula.

**RESEARCH_ONLY:** 16 phonemizer, 20 CUPE (GPL).

## 22. Discard / do-not-build-now
- Waveform-similarity-to-TTS as score.
- Full Kaldi/MFA runtime on ASUS without conda/WSL plan.
- AGPL MDD without weights (23).
- Unlicensed repos as code dependencies (22, 24, 25, 29, 30).

## 23. Deeper research next
1. Phone-segmented formants (align → F1/F2 per vowel).
2. True pitch-shift (Phase vocoder) for speaker-invariance test.
3. speechocean762 human calibration.
4. LiteChildASR after HF access.
5. ONNX phone-posterior for GOP without PyKaldi.
6. Grammar-constrained vosk on Active-15.
7. Real-child pilot (consent) as **validation only** (§22).

## 24. Recommended research architecture for Phase 1.2
```
MIC → silero-VAD → preprocess
        ├→ moonshine ASR (text gate / keyword, NOT score)
        ├→ phone-CTC (w2v2-espeak or CUPE research)
        ├→ whisperX word times (optional)
        └→ parselmouth on aligned vowels
              ↓
        CMUdict SpeakingTarget
              ↓
        scorer-v1 (+ OpenPronounce vote + future GOP)
              ↓
        {score 0-100, confidence, phone_diagnostics[]}
```
Adapters only; no Unity production lock-in.

## 25. Known gaps for 4-year-olds
- Zero real preschool audio in metrics.
- No age-conditioned AM runnable.
- Immature articulation variants not in CMUdict.
- Soft child + room noise not measured (only synthetic soft).
- Vietnamese-accented English not in corpus yet.

## 26. Exact next experiments before implementation
1. Obtain speechocean762 → calibrate scorer-v1 + OpenPronounce (Pearson/Spearman/MAE).
2. Build phone-aligned formant features; retest /ɪ/-/iː/ with controlled synth.
3. Request HF access LiteChildASR; A/B vs moonshine on same files.
4. Add Vietnamese-accented adult probes (legal).
5. If key present: ElevenLabs cache-once reproducibility test.
6. Real-child pilot protocol (ethics/consent) — validation layer only.
7. Unity research adapter skeleton (not production): VAD+moonshine+scorer-v1 offline process.

---

## Artifacts
- Registry: `Research/Speech/Phase1_1/SpeechResearchRegistry.md`
- Reports: `candidates/01-..36-*/candidate_report.md`
- Experiments: `Experiments/score_vs_confidence_v1.json`
- Bench scripts: repo `Research/Speech/Phase1_1/bench_*.py` + `D:\speech-lab\bench_*.py`
- Models/venvs: `D:\speech-lab\` (outside git)

**No production architecture locked. No "best library" claim. No child-as-ground-truth.**
