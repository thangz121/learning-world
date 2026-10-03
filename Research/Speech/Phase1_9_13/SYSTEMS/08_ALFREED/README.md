# SYSTEM 08 — Alignment-Free Pronunciation Assessment ("ALFreeD" line)

STATUS: **name not found; alignment-free family documented + one runnable benchmark reproduced**

- "ALFreeD" as a named system does not exist in public sources (searched GitHub/papers).
  What exists is a coherent alignment-free research line; this audit covers it.
- **GOP-AF / GOP-SA** (arXiv 2507.16838, "Segmentation-free Goodness of Pronunciation"):
  alignment-free GOP for CTC acoustic models; explicitly handles insertion/deletion; evaluated on
  **CMU Kids** and SpeechOcean762. Directly relevant to our Phase 1.9.12 finding (CTC-span
  anchoring contaminates acoustic features).
- **VoxTutor** (github.com/ranafaraz/VoxTutor, MIT): runnable numpy-only benchmark proving
  forced alignment + GOP normalization are both required (2×2 dissociation). **RAN (E4)**.
- Related: discrete-token surprisal (arXiv 2606.19910), zero-shot HuBERT APA (2305.19563),
  WavLM-DTW template scoring (alphaXiv 2607.13721).
