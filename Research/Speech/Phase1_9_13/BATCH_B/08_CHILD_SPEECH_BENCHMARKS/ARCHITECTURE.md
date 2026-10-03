# Child Speech Benchmarks — Evaluation Architecture (E2)

```
DATA COLLECTION (children repeating words; controlled room/device)
  ↓
HUMAN ANNOTATION (expert star ratings; phoneme-level for speechocean762)
  ↓
ZERO-RATING REMOVAL (unintelligible / noisy / silent)   ← assessability at data level
  ↓
SPEAKER-DISJOINT SPLIT (controlled distribution)
  ↓
BASELINES (SVM+ComParE_16; multi-task wav2vec2)
  ↓
METRICS: UAR (primary) + ACC + MAE + latency + explainability
```
