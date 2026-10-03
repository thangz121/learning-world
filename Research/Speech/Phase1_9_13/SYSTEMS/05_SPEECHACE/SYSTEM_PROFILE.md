# Speechace — System Profile

| Field | Value | Evidence |
|---|---|---|
| Vendor | Speechace (US) | E1 |
| Type | REST API (B2B); no consumer app | E1 |
| Endpoints | score/word, score/text, score/speech, score/task, score/writing | E1 |
| Scoring levels | phoneme, syllable, word, utterance | E1 |
| Fidelity | yes — `fidelity_class` (CORRECT/NO_SPEECH/INCOMPLETE/FREE_SPEAK) | E1 |
| Spoken-phone output | `sound_most_like` per expected phone | E1 |
| Stress | lexical stress score + predicted stress level | E1 |
| Fluency | speech/articulation rate, pauses, correct word counts | E1 |
| Rubrics | Speechace, IELTS, PTE, TOEIC, CEFR | E1 |
| Dialects | en-us, en-gb (multi-language broadly) | E1 |
| API key | free trial per plan; key by request/approval ("contact us") | E1 |
| CORS | disabled by default (backend calls only) | E1 |
| Patent | "patented technology" claimed | E0/E1 |
| Child-specific | none documented | E1 |
| Open source | none | E1 |

## Child relevance
Not documented as child-specific, but `fidelity_class` is the most direct published answer to
our "did the child actually attempt the target?" problem.
