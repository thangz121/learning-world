# SpeakStar — Architecture (documented, E1)

```
BUNDLED CONTENT (476 recordings; phonetician references; 44-sound chart; lessons; songs)
  ↓ (no network)
ON-DEVICE SCORING (mic → local model → per-word decision)
  ↓
WORD COLOURING: heard | nearly heard | not heard
  ↓
ATTEMPT REWARD: 0–3 stars
  ↓
LESSON FLOW: dialogue listen → sentence scoring → passage read-aloud (+ sing-along, mic-free)
```

Vietnamese UI throughout; zero data collection; offline by design.
