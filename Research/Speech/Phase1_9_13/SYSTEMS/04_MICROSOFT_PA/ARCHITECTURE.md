# Microsoft PA — Architecture (documented, E1)

```
AUDIO (file or mic stream)
  ↓
AZURE SPEECH (pronunciation-specific STT model)
  ↓
ASR + forced phonetic alignment (phoneme/syllable/word offsets in 100ns)
  ↓
PER-PHONEME ACCURACY vs native reference
  + NBestPhonemes (top-5 spoken candidates with confidence)
  ↓
WORD AGGREGATION (AccuracyScore; ErrorType via miscue vs reference:
  Omission / Insertion / Mispronunciation)
  ↓
FULL-TEXT SCORES: Accuracy, Fluency (silent breaks), Completeness (word ratio),
  Prosody (stress/intonation/speed/rhythm; en-US), PronScore (weighted)
```

Unknown: acoustic model size, training data, child handling, weight values in PronScore.
