# PER-MDD — Architecture (E2)

```
OFFLINE: labeled speech + phoneme alignments
  → frame embeddings (HuBERT ASR)
  → phoneme embedding POOL (mid-frame)

ONLINE: learner audio
  → frame embeddings (same model)
  → cosine similarity vs pool → top-k + threshold
  → frame label = mode(labels) | blank
  → collapse duplicates / remove blanks → predicted phoneme sequence
  → align with canonical → substitution/deletion diagnosis
```

No phoneme classifier training; no scoring model training.
