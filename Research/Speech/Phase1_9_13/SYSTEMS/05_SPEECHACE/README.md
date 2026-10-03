# SYSTEM 05 — Speechace (Fidelity Model)

STATUS: **BLOCKED** at execution (API key by request/approval; free trial exists but requires
signup/contact — a human step). Documentation is very complete (E1).

Key E1 findings:
- Three models under the hood (official v5 blog): **Acoustic Model + Scoring Model + Fidelity Model**
- **Fidelity Model**: classifies whether the speaker faithfully attempted the expected utterance;
  API field `fidelity_class` values:
  - `CORRECT` — faithful attempt, scorable
  - `NO_SPEECH` — no intelligible speech detected
  - `INCOMPLETE` — cut off / timed out
  - `FREE_SPEAK` — spoke freely outside expected text
- Score/text: word/syllable/phoneme `quality_score` (0–100) + **`sound_most_like`** (actual
  phone produced) + stress (lexical stress, predicted stress level) + extents (10 ms units)
- Fluency: speech rate, articulation rate, pause list, correct word/syllable counts
- Rubrics: Speechace / IELTS / PTE / TOEIC / CEFR
