# PER-MDD — Technical Evidence (E2)

## Pipeline (verbatim-level detail from paper)
1. Pool construction: from a labeled set with phoneme time alignments, segment utterances into
   frames; map each frame through pretrained ASR f(·) to embeddings; store phoneme-span
   embeddings (mid-frame pooling).
2. Inference: frame embeddings of the test utterance; cosine similarity against the pool.
3. Candidate retrieval: top-k (k=10) + threshold (τ=0.7).
4. Label assignment: frame label = blank if no candidate passes threshold, else mode of labels.
5. Post-processing: collapse consecutive duplicates, remove blanks → predicted phoneme sequence.
6. Alignment with canonical phoneme sequence → detection + diagnosis (substitution/deletion).

## Results (L2-ARCTIC; 6 test speakers incl. Vietnamese TLV)
| metric | value |
|---|---|
| F1 | 69.60% (SOTA +6.56%) |
| FRR | **4.43%** (best among compared) |
| FAR | 32.44% |
| DA (diagnosis accuracy) | 91.57% |
| PER | 104.08 (insertion-heavy; acknowledged limitation) |

## Design principle
"FRR is regarded as the most critical metric in MDD" — false rejection of correct phones
frustrates learners and harms learning experience.

## Relevance to LWE
- Zero-training path to phoneme evidence using a pretrained ASR + a small retrieval pool —
  attractive given our tiny child data; could be built per-word for LWE vocabulary.
- FRR-first evaluation philosophy matches child-confidence concerns.
