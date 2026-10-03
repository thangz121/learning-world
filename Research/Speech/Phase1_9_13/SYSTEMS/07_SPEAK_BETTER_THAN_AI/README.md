# SYSTEM 07 — speak-better-than-ai (ldenoue) — SOURCE AUDIT (E3)

STATUS: **CLONED + SOURCE-AUDITED** (MIT). Browser-only app; no headless run possible here
(no browser automation in this environment) → E3, not E4/E6.

Key extracted design (main.js, phoneme-worker.js):
- Phoneme model: `onnx-community/wav2vec2-lv-60-espeak-cv-ft-ONNX` (q8) via transformers.js
  + onnxruntime-web, in a Web Worker; same model grades AI reference and learner.
- Reference voice: Kokoro / Supertonic v3 / PocketTTS (local TTS workers) + espeak-ng WASM G2P.
- **EOS**: RMS (AnalyserNode) > 0.025 for 3 consecutive frames starts speech; stop after
  **1300 ms** of silence (hangover). Audio resampled to 16 kHz mono.
- **Scoring**: DP alignment (substitution cost = 1 − char-similarity; ins/del = 0.8);
  deletion → sim 0; `score = 100·Σsim / len(expected)`; word color thresholds 0.72/0.4.
- Privacy: audio never leaves the browser.
