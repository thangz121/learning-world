# PER-MDD — System Profile

| Field | Value | Evidence |
|---|---|---|
| Paper | arXiv 2511.20107 (Nov 2025), Vietnamese author group | E2 |
| Method | Retrieval over pretrained ASR embeddings (RAG-inspired) | E2 |
| Base model | facebook/hubert-large-ls960-ft | E2 |
| Pool | 500 training files; mid-frame pooling of phoneme spans | E2 |
| Retrieval | cosine similarity; top-k=10; threshold 0.7 | E2 |
| Output | predicted phoneme sequence → alignment with canonical → MDD | E2 |
| Training | **none** (no phoneme model, no scorer) | E2 |
| Results | F1 69.60%, FRR 4.43%, FAR 32.44%, DA 91.57% (L2-ARCTIC) | E2 |
| Weakness | insertion errors (PER 104.08) | E2 |
| Code | not found publicly | E2 |
| Child relevance | not evaluated on children | E2 |
