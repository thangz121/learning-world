# Phonological-Feature MDD — Architecture (E2)

```
AUDIO → wav2vec2 / Conformer / XLSR encoder
  ↓ (multi-label CTC or AF-fusion)
SPEECH ATTRIBUTES: manner | place | voicing | nasality | ... (35 binary)
  ↓
PHONEME CODE = unique binary attribute combination
  ↓
DIAGNOSIS: compare attribute posteriors vs canonical code
  → max-deviation attribute + direction ("needs +voice", "needs +dental")
  ↓
FORMATIVE FEEDBACK (how to move articulators)
```

Two framework variants: PHN (segmental phoneme output) vs ART (subsegmental attribute output);
ART shows better diagnostic error rates, PHN slightly better raw detection.
