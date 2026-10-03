# Speech Blubs — Architecture Reconstruction

Evidence-backed (E1) minimum pipeline:

```
MIC (permission)
  ↓
ON-DEVICE VOICE DETECTION (voice-controlled trigger; no audio saved/collected)
  ↓
TASK GATE (child repeats modeled word; likely speech-presence/attempt based)
  ↓
REWARD / VIDEO / STICKERS (progress)
```

- ASR: UNKNOWN (no public evidence)
- Pronunciation scoring: UNKNOWN — no phoneme-level feedback documented; rewards appear tied
  to attempt/participation (E0)
- Alignment: UNKNOWN
- Cloud audio: none per policy (E1)

Do not assume a pronunciation-assessment stack behind the product.
