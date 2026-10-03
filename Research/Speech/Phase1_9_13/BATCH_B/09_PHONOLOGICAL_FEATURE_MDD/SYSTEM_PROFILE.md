# Phonological-Feature MDD — System Profile

| Paper | Representation | Model | Training data | Key result | Evidence |
|---|---|---|---|---|---|
| arXiv 2311.07037 | 35 speech attributes (manner/place/others), 71 outputs | wav2vec2 + multi-label CTC (SCTC-SB) | native speech only | FAR<30%, DER<10% (vs 57%/31% phoneme) | E2 |
| SLATE 2025 (wei25) | AF categories (vowel 3, consonant 3) | Conformer CTC; fine-tuned XLSR | LibriSpeech AF classifiers + L2-ARCTIC | ART DER 18.62% vs PHN 25.12% (−25.88% rel.) | E2 |
| Oxford CAPT | 18 phonological features | multi-task DNN (shared repr.) + active learning | German/Italian L1 English | >11% rel. EER improvement; diagnosis by max-deviation feature | E2 |
| arXiv 2306.01845 | articulatory aux tasks, multi-view | mono/multilingual encoders + CTC | L2-ARCTIC | PER −11.13%; F1 +5.89% | E2 |

Child relevance: not child-specific in these papers; SLATE 2025 includes Vietnamese-L1 speakers.
Computational cost: wav2vec2/Conformer-scale; AF classifier heads are small.
