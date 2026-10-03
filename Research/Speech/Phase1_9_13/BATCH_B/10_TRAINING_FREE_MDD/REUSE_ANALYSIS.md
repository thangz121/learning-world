# PER-MDD — Reuse Analysis

| Item | Type | Status | Reason |
|---|---|---|---|
| Retrieval-based phoneme evidence (no training) | ALGORITHM | ADAPT (research) | Fits LWE's small-data constraint; build per-word pool |
| FRR-first evaluation philosophy | EVALUATION_METHOD | **DIRECT_REUSE** | Child scoring should avoid false rejection |
| HuBERT-large-ls960-ft as embedding model | DIRECT_MODEL | ADAPT (license check) | HF model; verify license |
| Pool construction + mid-frame pooling | ALGORITHM | ADAPT | Simple to reimplement |
| Code | — | NOT_AVAILABLE | Paper only |

REUSE_COST: LOW-MEDIUM (reimplementation; no training)
EXPECTED_VALUE: HIGH for a no-training child evidence layer
