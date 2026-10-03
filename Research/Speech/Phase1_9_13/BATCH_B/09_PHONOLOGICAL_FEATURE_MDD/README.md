# BATCH B · SYSTEM 09 — Phonological-Feature MDD (research line)

STATUS: **research audited (E2)**; no single runnable package found for this exact line.

Key findings:
- **Phonological Level wav2vec2 MDD** (Shahin/Epps/Ahmed, arXiv 2311.07037):
  35 speech attributes (manner/place + others), **multi-label CTC (SCTC-SB)**, wav2vec2 core.
  Trained **only on native speech** (no mispronounced data needed). Results: FAR<30%, DER<10%
  vs phoneme-level FAR 57%/DER 31%; /th/→/s/ FAR cut 72%→37% via the *dental* attribute;
  voicing attribute outperforms phoneme models for /d/–/t/.
- **SLATE 2025** (Wei et al.): articulatory features (vowel: backness/height/roundness;
  consonant: manner/place/voicing) fused into Conformer-CTC and fine-tuned XLSR; ART
  (subsegmental) framework reduced DER by 25.88% relative vs phoneme framework. Test set includes
  **3 native Vietnamese speakers** (L2-ARCTIC) — directly relevant to LWE's L1.
- **Oxford CAPT** (multi-task DNN + active learning): 18 features; diagnosis = feature with max
  deviation + direction ("increase VOICE", "add STRIDENT") — formative feedback.
- **Multi-view multi-task** (arXiv 2306.01845): articulatory auxiliary tasks; F1 +5.89%.

Why it matters to LWE: this line explains *how the error was made* (which articulatory
attribute failed) rather than only which phoneme was substituted — matching our need for
diagnosis (e.g., final-consonant deletion, voicing errors) and our child-audio constraints
(native-only training).
