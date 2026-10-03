# CHIVOX — Architecture (documented layers only, E1)

```
AUDIO
  ↓
AUDIO QUALITY GATE (clipping / background speech / missing audio / incomplete response)
  ↓  (bad audio → re-record, NOT pronunciation feedback)
ATTEMPT / VALID-VOICE CHECK ("post proc failed" → no valid voice)
  ↓
RECOGNITION + ALIGNMENT (kernel-dependent)
  ↓
PHONEME-LEVEL EVIDENCE (+ GOP adjustment parameter exposed)
  ↓
DIAGNOSTICS: omissions / insertions / misreads / repetitions / stress / pauses / liaison
  ↓
SCORING: overall / accuracy / fluency / integrity (+ phonics / word / sentence / paragraph)
  ↓
AGE/LEVEL CALIBRATION (thresholds, feedback language, retry length)
  ↓
CHILD-FACING FEEDBACK (one age-appropriate next step; never low score as judgment)
```

Explicitly documented: quality gate before feedback; age/level calibration; correction taxonomy.
Unknown: model internals, how "dedicated child models" differ technically.
