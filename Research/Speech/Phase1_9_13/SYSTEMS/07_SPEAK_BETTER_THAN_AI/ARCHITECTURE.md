# speak-better-than-ai — Architecture (E3)

```
TARGET TEXT ──espeak-ng WASM──▶ expected phoneme tokens
REFERENCE ── Kokoro/Supertonic/PocketTTS ──▶ ref audio
MIC ──RMS EOS (0.025 ×3; 1300ms hangover)──▶ 16k mono clip
   ├─ learner clip ─▶ wav2vec2 CTC (q8, worker) ─▶ observed tokens + conf
   └─ ref audio   ─▶ same model ─▶ tokens (fairness)
        ↓
DP align(exp, obs): sub cost 1−sim; ins/del 0.8; deletion sim=0
        ↓
score = 100·Σsim/len(expected); per-word mean; color feedback
```

All in-browser; no server; audio never uploaded.
