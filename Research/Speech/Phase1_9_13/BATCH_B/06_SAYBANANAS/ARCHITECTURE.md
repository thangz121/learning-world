# SayBananas — Architecture (E2)

```
PRE-GAME: record "correct" + "incorrect" template per word (child-specific)
  ↓
GAME LOOP (platformer; stars = mandatory speech triggers; ~100 trials target)
  ↓
CHILD RECORDING → compare vs templates (template matching)
  ↓
CLASSIFY correct-ish / incorrect-ish → "Good job!" / "Not quite"
  ↓
AUDIO QUALITY GATE: ~50% of recordings rejected as unanalyzable (study)
  ↓
CAREGIVER LOOP: correct / move on / try again (max 1 retry) + automatic metrics/reports
```

No phoneme-level model documented; personalised templates instead.
