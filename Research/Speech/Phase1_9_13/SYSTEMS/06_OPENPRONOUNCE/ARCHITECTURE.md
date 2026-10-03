# OpenPronounce — Architecture (E3)

```
LEARNER AUDIO (16k mono)
  ├─ Wav2Vec2-large-960h embeddings ──┐
  ├─ word transcription (wav2vec2)    │
  └─ phone recognition (wav2vec2-lv-60-espeak-cv-ft)
REFERENCE = TTS(target text) → wav2vec2 embeddings
  ↓
FastDTW(learner_emb, reference_emb) → acoustic distance
phonemize(target) vs heard phones → phoneme error rate + per-word errors
word transcription vs target → word error rate
  ↓
score = 0.3*acoustic + 0.4*phonemes + 0.3*words   (0–100)
  ↓
prosody: F0 + energy contours
```

Notes: no VAD stage inside the library (caller supplies the clip); no fidelity classifier;
phone comparison includes small leniency pairs.
