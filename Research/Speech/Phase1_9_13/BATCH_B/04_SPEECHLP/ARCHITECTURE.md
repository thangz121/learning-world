# SpeechLP — Architecture (documented, E1)

```
CHILD PROFILE (age, targets, processes)
  ↓
TARGET WORD SELECTION
  latest-consonant age rule (+1y multisyllabic)
  × position (initial/medial/final) × blends × phonological process × difficulty
  ↓
VOICE-CONTROLLED GAME
  smart voice detection (child speech vs game audio) → real attempt
  ↓
REAL-TIME ACOUSTIC FEEDBACK (sound-by-sound)
  ↓
PROGRESS (sound-by-sound mastery) + PARENT/SLP REPORT (target | word | result)
```

Not a pronunciation-assessment API; the reusable asset is the target-selection framework.
