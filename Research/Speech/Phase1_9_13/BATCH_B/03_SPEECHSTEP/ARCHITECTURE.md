# SpeechStep — Architecture (documented only, E0/E1)

```
CHILD AUDIO (mic, consent-gated)
  ↓
CLOUD SCORING SERVICE (wav2vec2 phoneme recognizer + GOP)
  ↓
DIAGNOSIS (substitution-level; e.g., lisp type)
  ↓
ASSESSABILITY DECISION ("declined rather than guessed" for unjudgeable profiles)
  ↓
AGE-BAND FEEDBACK POLICY:
  Nest(1-3): no scores | Garden(4-8): zero numbers on kids | Quest(9-12): mastery | Studio(13-16)
  ↓
PRACTICE LOOP (missions, garden, streaks) + PARENT/THERAPIST VIEW (mastery %, reports)
```

No-consent path: local demo scorer → "demonstration rather than measurement" (explicit).
Word-position targeting: beginning / middle / end.
