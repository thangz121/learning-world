# SpeechSuper — Architecture (documented only, E1)

```
AUDIO (mic) → [age group: 3~6 | 6~12 | >12] + [slack −1..+1] + accent + IPA/KK
  ↓
RECOGNITION + ALIGNMENT (per scripted mode)
  ↓
WORD-LEVEL SCORES + PHONEME-LEVEL EVIDENCE
   phoneme: matched | sounds-like /y/ | omitted | inserted-before | inserted-after
  ↓
SENTENCE METRICS: pronunciation, fluency, completeness/integrity, rhythm, speed, tone, linking
  ↓
SCORE SCALE (precision 1/0.5/0.1/0.01) → display
```

Explicit documented semantics: omission and insertion at phoneme level; age and leniency
controls exposed to integrators.
