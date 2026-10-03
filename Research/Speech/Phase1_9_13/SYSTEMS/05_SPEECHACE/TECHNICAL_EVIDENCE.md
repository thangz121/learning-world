# Speechace — Technical Evidence (E1)

## Three-model architecture (official v5 blog, 2020)
1. **Acoustic Model** — audio → linguistic units (phonemes); robust across speakers/conditions.
2. **Scoring Model** — sentence/word/syllable/phoneme scores; intelligibility degree.
3. **Fidelity Model** — "classifies whether the speaker has faithfully attempted the expected
   utterance, indicating if this is a valid attempt for which a score should be recorded or
   whether the speaker should reattempt the utterance."

## Fidelity classes (official guide)
| Value | Meaning |
|---|---|
| CORRECT | faithful attempt → scorable |
| NO_SPEECH | no intelligible human speech |
| INCOMPLETE | recording cut off / timed out |
| FREE_SPEAK | free speech outside expected text |

Fluency API additionally emits `score_issue_list[]` warnings (e.g., `response_incomplete`,
"The response doesn't follow the script completely.") and **reduces overall scores** on
unfaithful attempts.

## Response detail (score/text v9)
- `phone_score_list[]`: expected phone, `quality_score`, **`sound_most_like`** (the phone actually
  produced), `stress_level` + `predicted_stress_level`, `extent` (10 ms units).
- `syllable_score_list[]`, `word_score_list[]` with quality scores.
- Fluency: speech_rate, articulation_rate, pause list, correct_word/syllable counts.
- Rubric mapping: Speechace/IELTS/PTE/TOEIC/CEFR.

## Why this matters to LWE
- The phase's special question A ("did the child attempt the target?") has a direct published
  answer: a **separate fidelity classifier** with explicit classes. LWE currently has no
  fidelity concept; our low-score confusion (Phase 1.9.9–1.9.12) partly stems from this gap.
- `sound_most_like` is the mature analog of our `best_obs`, but scoped per expected phone with
  a quality score — similar to our soft-v2 hits, yet with documented calibration.
- Adult-oriented; child behavior unverified (would need trial + child audio test).

## Unknown
- Model sizes/training data; patent specifics; child handling; calibration datasets.
