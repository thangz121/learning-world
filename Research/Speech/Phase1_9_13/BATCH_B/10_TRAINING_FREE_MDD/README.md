# BATCH B · SYSTEM 10 — Training-Free / Retrieval MDD (PER-MDD)

STATUS: **research audited (E2)**; no public code found (paper only). Vietnamese author group;
L2-ARCTIC includes Vietnamese-L1 speakers.

Key findings (arXiv 2511.20107):
- **Phoneme Embedding Retrieval MDD (PER-MDD)**: pretrained ASR (HuBERT-large-ls960-ft)
  frame embeddings; build a phoneme embedding pool from 500 training files; cosine similarity
  retrieval (top-k=10, threshold 0.7); frame label = mode of retrieved labels; collapse
  duplicates → predicted phoneme sequence → align with canonical for detection/diagnosis.
- **No phoneme-specific model training and no scoring model training.**
- Results (L2-ARCTIC): **F1 69.60%**, **FRR 4.43% (best)**, FAR 32.44%, DA 91.57%;
  insertion errors remain high (PER 104.08) — acknowledged.
- Design principle stated in the paper: **FRR is the most critical metric** — false rejections
  of correct phones frustrate learners.
