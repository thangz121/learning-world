# ELSA — Architecture Reconstruction (E2)

Scripted (2016):

```
MIC (app)
  ↓ stream audio (during speech)
SERVER
  ↓ beep prompt → server-side ENDPOINTING (EOS)
KALDI + custom DNN acoustic models (L1/L2 variants)
  ↓ phoneme alignment vs expected script
PHONEME-LEVEL DEVIATION → color-coded feedback + hints
  ↓
word/sentence scores; intonation/fluency dimensions
```

Unscripted (2023, Speech Analyzer):

```
MIC / meeting audio
  ↓ stream via WebSocket
END-TO-END ASR (fine-tuned on 100h non-native) → transcript
  ↓ speaker-ID (embedding + diarization) filters other speakers
DIMENSIONAL ANALYSIS:
  pronunciation (phonetic deviations vs native, intelligibility-weighted)
  intonation (pitch/energy/prominence)
  fluency (pacing/pausing/hesitations)
  grammar (GEC + range) · vocabulary (CEFR)
  ↓
WEIGHTED OVERALL SCORE + per-dimension feedback
```

Unknown: exact model sizes, phoneme inventory mapping, calibration datasets,
silence/noise handling thresholds.
