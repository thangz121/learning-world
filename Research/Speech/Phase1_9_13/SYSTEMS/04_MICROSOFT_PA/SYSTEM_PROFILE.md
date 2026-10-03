# Microsoft Pronunciation Assessment — System Profile

| Field | Value | Evidence |
|---|---|---|
| Vendor | Microsoft Azure AI Speech | E1 |
| Type | Cloud API + Speech SDK (many languages) | E1 |
| Target | General language learners; no child-specific mode documented | E1 |
| Pricing | Same as speech-to-text (standard/commitment tier); free tier exists for STT but signup requires account | E1 |
| Free access | Azure free account needs payment method → BLOCKED per policy | — |
| Pronunciation model | "specific version of the speech-to-text model" for consistent assessment | E1 |
| Grading | FivePoint (0–5) or HundredMark (0–100) | E1 |
| Granularity | Phoneme, Syllable (en-US), Word, FullText | E1 |
| Scripted | Reference text; EnableMiscue adds Omission/Insertion at word level | E1 |
| Unscripted | No reference; different STT model; ASR text + scores | E1 |
| Streaming | Continuous mode (unlimited duration); >30s use continuous (miscue unsupported) | E1 |
| Prosody | EnableProsodyAssessment (en-US only; SDK ≥1.35.0); stress/intonation/speed/rhythm | E1 |
| Phoneme alphabet | IPA or SAPI (en-US; SAPI also zh-CN) | E1 |
| Spoken phoneme | NBestPhonemes (en-US) — confidence-ranked alternatives | E1 |
| Offline | Speech SDK can run with containers (not assessed here) | E1 |

## Child relevance
No child-specific mode documented. However, ErrorType Omission + NBestPhonemes are directly
relevant to our final-consonant deletion findings (Phase 1.9.12).
