# SpeechLP — Technical Evidence (E1)

## Target-selection algorithm (official article)
Source: Crowe, K. & McLeod, S. (2020). Children's English Consonant Acquisition in the United
States: A Review. SpeechLP applies:

1. **Age norms** (table in README).
2. **Latest-consonant rule**: word's minimum age = latest-developing consonant present.
3. **Multisyllabic +1 year** (cap 6+).
4. **Position filter**: initial / medial / final.
5. **Blends filter**: initial & final clusters.
6. **Phonological process filter** (select words that target/avoid patterns):
   - substitution: stopping, fronting, gliding, depalatalization, affrication/deaffrication,
     vocalization, denasalization, labialization, alveolarization
   - syllable structure: cluster reduction, **final consonant deletion (FCD)**,
     weak syllable deletion (WSD), initial consonant deletion (ICD)
   - assimilatory: labial/nasal/velar assimilation, prevocalic voicing, postvocalic devoicing,
     coalescence, glottal replacement
7. **Age-relative difficulty** (Easy/Medium/Hard) from 4 dimensions: late-developing sound load,
   mid-level sound presence, syllable count, complexity flags (clusters/morphological load).

## Attempt detection (E1)
- "Smart voice detection separates their phonetic speech from the game's own music and sounds,
  so every score reflects a real attempt, and real wins." → explicit attempt/fidelity concern in
  a child product.

## Clinical framing
- Screener output example: Target sound | Word | Result (Keep practicing / Clear response);
  "A useful first signal, not a diagnosis"; SLP keeps assessment/planning.

## Research cited
- JSLHR: children with real-time acoustic feedback improved 2.4× faster than traditional
  motor-based treatment alone (app store claim, E0).
