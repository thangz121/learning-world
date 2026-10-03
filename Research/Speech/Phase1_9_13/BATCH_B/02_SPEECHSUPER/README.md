# BATCH B · SYSTEM 02 — SpeechSuper

STATUS: **BLOCKED at execution** (mic-only browser demo with internal key; free trial key by
form/expert contact). Demo UI + docs inspected (E1).

Key E1 findings (demo page HTML inspected):
- **Age group control exists in the public demo: `3~6` / `6~12` / `>12` years old**
  (`settingForm.agegroup` = 1/2/3).
- **Strict↔lenient slider exists: `settingForm.slack` ∈ [−1, +1], step 0.1** — the exact
  strict/lenient control this phase wants to study.
- Accent: General/British/American/Australian/NZ/Indian; phonetic set IPA/KK; score precision
  1 / 0.5 / 0.1 / 0.01.
- Output UI: overall, pronunciation, fluency, completeness ("integrity"), rhythm, speed (wpm);
  word-level table; **phoneme-level analysis**: `/x/ omitted`, `/x/ sounds like /y/`,
  `extra /x/ spoken before/after`, `/x/ matched`; end-of-sentence tone (falling/rising);
  speech-to-text recognition text.
- Docs matrix: word / short text / long text / semi-scripted / unscripted; **phoneme score**;
  **phonics score (EN)**; **repetition and deletion (EN)**; linking & loss of plosion (EN);
  IELTS speaking scores (unscripted).
- Free trial: form → expert contact (1 business day).
