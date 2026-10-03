# Alignment-Free — Technical Evidence

## GOP-AF / GOP-SA (E2, arXiv 2507.16838)
- Standard GOP requires pre-segmentation; mispronunciations make segmentation unreliable; CTC
  activations are not aligned to acoustic boundaries.
- GOP-SA: align GOP to the model's own activations.
- **GOP-AF: log posterior of target phoneme given the full observation sequence and context —
  no committed segmentation**; includes insertion/deletion errors; normalized by activation
  length; robust to model peakiness.
- Results: best MDD on CMU Kids and SpeechOcean762; feature vectors from GOP-AF achieve SOTA
  phoneme-level assessment on SpeechOcean762.
- **Why it matters to LWE:** this is the published fix for exactly the failure we measured in
  Phase 1.9.12 — acoustic features anchored on CTC spans inherit alignment error. GOP-AF avoids
  committing to a span and can represent deletions (our missing final consonants).

## VoxTutor (E4 reproduced)
- 2×2 ablation with known ground truth: segmentation (fixed vs DTW) × normalization
  (raw vs GOP-likelihood-ratio).
- Forced alignment fixes rate-warp; GOP normalization fixes noise; both needed (table in
  PLAYTEST.md).
- Synthetic feature frames (not real audio) — the dissociation is the contribution.

## Discrete-token surprisal (E2, arXiv 2606.19910)
- K-means tokens from SSL encoder; native n-gram LM surprisal; Text2DUnit–DTW alignment in the
  same discrete space; ridge regression; no forced aligner/ASR.
- SpeechOcean762 PCC 0.60→0.66 with transcript guidance.

## Zero-shot HuBERT APA (E2, arXiv 2305.19563)
- Masked token recovery on HuBERT + k-means; ASR-free; comparable to supervised baseline on
  SpeechOcean762.

## WavLM-DTW template scoring (E2, alphaXiv 2607.13721)
- Template-based DTW over WavLM-Large; text-free; phone/rhythm/intonation; exceeds human
  agreement on sentence-level phonetic scoring; ~5 native templates suffice (95%).
