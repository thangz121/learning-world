# Phonological-Feature MDD — Technical Evidence (E2)

## Attributes (arXiv 2311.07037)
Manners: consonant, sonorant, fricative, nasal, stop, approximant, affricate, liquid, vowel,
semivowel, continuant. Places: alveolar, palatal, dental, glottal, labial, velar, mid, high,
low, front, back, central, anterior, posterior, retroflex, bilabial, coronal, dorsal. Others:
long, short, monophthong, diphthong, round, voiced. → 35 binary attributes; each phoneme has a
unique binary code; 71 outputs (35 +, 35 −, 1 blank).

## Training insight
- Speech-attribute models can be trained on **correctly pronounced (native) speech only** —
  no mispronounced-data annotation needed.
- Multi-label CTC (SCTC-SB) handles non-mutually-exclusive attributes in one model.

## Diagnosis behavior
- Errors are described as attribute changes (e.g., /r/→/ah/: −liquid/+vowel;
  /z/→/s/, /jh/→/ch/: −voiced).
- Oxford CAPT: diagnosis = max-deviation feature + direction → corrective instruction
  ("increase VOICE", "add STRIDENT/hissing").

## Vietnamese relevance (SLATE 2025)
- L2-ARCTIC test set includes 3 Vietnamese-L1 speakers; AF-enhanced ART models reduce DER most
  for subtle distortion errors — the class phoneme models miss.

## Unknown
- Child performance (explicitly listed as future work in arXiv 2311.07037).
- Latency on CPU for LWE's offline target (not reported).
