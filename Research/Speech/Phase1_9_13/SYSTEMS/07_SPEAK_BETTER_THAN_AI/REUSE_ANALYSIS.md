# speak-better-than-ai — Reuse Analysis

| Item | Type | Status | Reason |
|---|---|---|---|
| EOS constants (RMS 0.025, 3 frames, 1300 ms) | ALGORITHM | REFERENCE_ONLY | Compare with our VAD hangover |
| Deletion-aware DP scoring (del sim=0) | ALGORITHM | ADAPT (research) | Direct final-consonant penalty |
| Same-model grading of reference + learner | ARCHITECTURE_PATTERN | REFERENCE_ONLY | Fairness idea |
| wav2vec2-lv-60-espeak-cv-ft ONNX q8 pipeline (transformers.js) | DIRECT_CODE | ADAPT | Browser-local inference pattern |
| espeak-ng WASM G2P | DIRECT_CODE | ADAPT | Local phonemization |
| Kokoro/Supertonic/PocketTTS local TTS | DIRECT_CODE | REFERENCE | Local reference generation |
| Full app | DIRECT_CODE | ADAPT (MIT) | Browser game; needs UX adaptation |

REUSE_COST: LOW (MIT, small code)
EXPECTED_VALUE: MEDIUM (EOS + deletion scoring + local stack)
