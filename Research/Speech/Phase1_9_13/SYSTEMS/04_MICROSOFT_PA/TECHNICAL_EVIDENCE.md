# Microsoft PA — Technical Evidence (E1, official docs)

## Scripted result fields
| Field | Meaning | Granularity |
|---|---|---|
| AccuracyScore | phoneme match to native; aggregated up with refinement | phoneme → full text |
| FluencyScore | silent-break similarity to native | full text |
| CompletenessScore | ratio of pronounced words to reference | full text |
| ProsodyScore | stress/intonation/speed/rhythm naturalness | full text |
| PronScore | weighted combination | full text |
| ErrorType | None/Omission/Insertion/Mispronunciation/UnexpectedBreak/MissingBreak/Monotone | word |
| Mispronunciation rule | word AccuracyScore below 60 → Mispronunciation | word |

## Phoneme detail (the key part for LWE)
- Each expected phoneme gets AccuracyScore AND `NBestPhonemes` (top-5 spoken candidates with
  confidence scores). Example from docs: expected `ɛ` scored 47, top candidate `ə` scored 100.
- This is **explicit substitution evidence with ranked alternatives** — something our soft-v2
  pipeline lacks (we get best_obs + sim, but not a calibrated candidate distribution).
- Offset/Duration (100 ns) allow syllable↔phoneme alignment.

## Fidelity / attempt detection
- No separate "fidelity" score. Attempt validity is handled implicitly: unscripted mode returns
  ASR text; scripted mode uses CompletenessScore (ratio of pronounced words) and miscue
  (Omission/Insertion). Silence → no recognized text / zero completeness.
- Not directly comparable to Speechace "fidelity" (see SYSTEM 05).

## Final consonants
- Omission at word level + low phoneme AccuracyScore would flag deleted final consonants.
- No child-specific behavior documented; adult/native reference model.

## Streaming/continuous
- Continuous mode for >30s; miscue disabled there (compare recognized vs reference manually).

## Privacy
- Audio sent to Azure service; Microsoft data/privacy terms apply. No on-device mode in the
  standard cloud API. Child-data implications require Microsoft DPA review.

## Relevance to LWE
- The schema is the closest public "reference design" for what our evidence layer should expose:
  per-phoneme accuracy + ranked alternatives + word-level error taxonomy.
- DO_NOT_REINVENT candidate: our own final-consonant deletion problem is a solved category
  (`Omission`) in this API.
