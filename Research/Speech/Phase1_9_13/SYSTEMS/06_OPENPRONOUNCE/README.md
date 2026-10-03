# SYSTEM 06 — OpenPronounce (Halleck45) — RUN SUCCESSFULLY

STATUS: **RAN LOCALLY (E4/E6)** — MIT, CPU, isolated venv `D:\speech-lab\venvs\op13`.

Pipeline: Wav2Vec2 (`facebook/wav2vec2-large-960h`) embeddings + DTW vs reference,
word transcription, phone recognition (`facebook/wav2vec2-lv-60-espeak-cv-ft`),
score = 0.3·acoustic + 0.4·phonemes + 0.3·words (WORD_ERROR_THRESHOLD 0.4), prosody F0/energy.

Workarounds used (documented):
- espeak-ng via `espeakng-loader` (phonemizer backend)
- gTTS reference replaced by local Windows SAPI WAVs (gTTS rate-limited; not a product change)

16/16 cases ran. Key results:
- adult clean words: red 100, cat 100, red apple 100; blue 23.2, book 25.5, apple 53.3
- wrong word (red audio vs "blue"): 6.8; silence: 0.0
- **child missing final /r/ ("four"): 20.5 / 24.7 / 12.2 → detected as errors**
- **child human-correct token (child_07 one): 2.46 → FALSE LOW (child failure also here)**
