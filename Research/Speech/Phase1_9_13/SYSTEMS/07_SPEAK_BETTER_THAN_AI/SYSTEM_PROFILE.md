# speak-better-than-ai — System Profile

| Field | Value | Evidence |
|---|---|---|
| Repo | github.com/ldenoue/speak-better-than-ai | E3 |
| License | MIT | E3 (LICENSE) |
| Type | Browser game (Vite, no server) | E3 |
| Stack | @huggingface/transformers 3.x, onnxruntime-web, kokoro-js, espeak-ng WASM | E3 |
| Phoneme model | onnx-community/wav2vec2-lv-60-espeak-cv-ft-ONNX (q8) | E3 |
| Reference TTS | Kokoro / Supertonic v3 / PocketTTS | E3 |
| EOS | RMS>0.025 ×3 frames start; 1300 ms silence stop | E3 |
| Alignment | custom DP (char-similarity substitution; ins/del 0.8; deletion sim=0) | E3 |
| Score | 100·Σsim/len(expected); word colors 0.72/0.4 | E3 |
| Child-specific | none documented; adult model | E3 |
| Privacy | 100% local (audio never uploaded) | E3 |
| Runtime | in-browser WASM (worker); model cached after first download | E3 |
| Run here | NOT POSSIBLE (needs a real browser + mic) | — |
