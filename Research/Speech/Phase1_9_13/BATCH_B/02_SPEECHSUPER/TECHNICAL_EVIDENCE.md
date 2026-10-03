# SpeechSuper — Technical Evidence (E1)

## Demo UI controls (verbatim from page HTML)
- Accent radios: General / British (`en_br`) / American (`en_us`) / Australian / New Zealand /
  Indian.
- Phonetic sets: IPA88 / KK.
- Decimal places: 1 / 0.5 / 0.1 / 0.01.
- **Age group radios: `3~6 years old` (1) / `6~12 years old` (2) / `>12 years old` (3).**
- **Lenient/strict: slider `slack` min −1, max +1, step 0.1** (strict ← → lenient).

## Demo output UI
- Scores: overall; pronunciation; fluency; completeness (`integrity`); rhythm; speed (wpm).
- Word table: word, quality score, expandable phoneme rows.
- Phoneme diagnostics (verbatim templates):
  - `/x/ omitted`
  - `/x/ sounds like /y/`
  - `extra /x/ spoken before /y/` · `extra /x/ after /y/`
  - `/x/ matched`
- End-of-sentence tone: falling/rising.
- Speech-to-text recognition text shown.

## Docs matrix (docs.speechsuper.com)
- word: overall + phoneme + phonics + word/sentence scores + **repetition & deletion (EN)**.
- short text: + fluency + completeness + linking/loss of plosion.
- long text: word/sentence scores (no phoneme).
- unscripted: phoneme + phonics + IELTS speaking scores.

## Unknown
- What the age group actually changes (model/threshold/calibration) — the control exists, the
  mechanism is not documented.
- What `slack` changes internally (score shift vs threshold vs tolerance).
- Child model existence.
