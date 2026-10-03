# speak-better-than-ai — Technical Evidence (E3, source)

## EOS / end-of-speech (main.js:244–261)
- AnalyserNode `fftSize=128`; RMS from time-domain frames.
- Speech start: `rms > 0.025` for **3 consecutive frames** (`voiceFrames>=3`).
- Speech end: **1300 ms** since `lastVoiceAt` → `recorder.stop()`.
- No pre-roll buffer; no adaptive noise floor; fixed threshold (contrast: SpeakFlow's
  auto-calibrating VAD, E1).

## Phoneme recognition (phoneme-worker.js)
- `AutoModelForCTC` + `AutoProcessor` for `onnx-community/wav2vec2-lv-60-espeak-cv-ft-ONNX`
  with `dtype:'q8'`; greedy CTC decode; blank = `<pad>`.
- Confidence per token = `1 / Σ exp(logit − max)` (rough; not calibrated).

## Alignment + scoring (main.js:389–392)
- `align(exp, obs)`: DP over phoneme tokens; substitution cost `1 − charSimilarity(a,b)`;
  insert/delete cost 0.8; backtrace emits `{exp, obs, sim}` or `{exp, obs:'', sim:0}`.
- `scoreTokens = round(100 * Σsim / len(expected))` → **deletions contribute 0** (missing
  final consonant reduces the score proportionally).
- Word scores = mean sim per word; UI thresholds: >0.72 good, >0.4 close, else practice.

## Audio
- Decode via AudioContext at 16 kHz; mono channel; worker inference keeps UI responsive.

## Relevance to LWE
- Simple, fully local reference implementation of phoneme-level scoring with explicit
  deletion penalty — directly relevant to our final-consonant finding.
- EOS constants are a concrete comparison point for our VAD/window work.
- No fidelity/attempt classifier; silence simply yields low score.
