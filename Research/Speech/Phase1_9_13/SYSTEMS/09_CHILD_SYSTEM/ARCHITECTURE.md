# SIAK — Architecture (E2)

2017:
```
UNITY GAME ──audio stream──▶ SERVER
  ├─ features → GMM-HMM forced alignment (Aalto ASR) → frame→phoneme mapping
  ├─ bilingual DNN phone classifier per segment
  ├─ VAD → end of speech
  └─ score = classified phones vs aligned phones → 1–5 points
```

2023 (best variant):
```
AUDIO → CTC phoneme recognizer (small GRU) + speech-event outputs
  → validation gate (error-rate threshold; phone or event output)
  → phonological-feature predictor → DTW vs ideal feature vectors (or PWLD)
  → SVM / linear regression → 0–5 star score
```

Rejected class handled during annotation (not necessarily at inference).
