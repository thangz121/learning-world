# Phase 1.2 Architecture

```
WAV 16k mono
  → SileroVadAdapter          → VadResult
  → MoonshineAsrAdapter       → AsrResult          (text gate, NOT score)
  → CmuDictTargetAdapter      → SpeakingTarget     (canonical, speaker-independent)
  → Wav2Vec2PhoneAdapter      → PhoneEvidence
  → WhisperXAlignAdapter      → AlignmentResult    (optional / default off)
  → ParselmouthAcousticAdapter→ AcousticFeatures
  → ScorerV1Adapter           → PronunciationResult (score + conf + phone diags)
  → OpenPronounceAdapter      → PronunciationResult (optional)
  → combined                  → primary score/conf
  → SpeakingResult JSON
```

Adapters are independently replaceable. No Unity coupling.
