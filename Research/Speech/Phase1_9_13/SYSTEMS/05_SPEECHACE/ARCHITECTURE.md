# Speechace — Architecture Reconstruction (E1)

```
AUDIO + REFERENCE TEXT
  ↓
ACOUSTIC MODEL (audio → phonemes)
  ↓
FIDELITY MODEL → fidelity_class:
     CORRECT | NO_SPEECH | INCOMPLETE | FREE_SPEAK
  ↓ (only if CORRECT is scorable; else reduce/flag)
SCORING MODEL:
  phoneme quality_score + sound_most_like + stress
  syllable / word / utterance aggregation
  (+ fluency metrics, intonation on request)
  ↓
RUBRIC MAPPING (Speechace / IELTS / PTE / TOEIC / CEFR)
```

Spontaneous path: ASR transcript → relevance/insufficient rejection → same scoring stack
(+ grammar/vocabulary/coherence).
