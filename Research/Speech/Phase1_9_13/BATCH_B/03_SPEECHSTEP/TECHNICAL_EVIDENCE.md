# SpeechStep — Technical Evidence (E0/E1)

## Method claims (product pages)
- "SpeechStep listens with a wav2vec2 phoneme recogniser and Goodness-of-Pronunciation scoring —
  the same family of methods used in pronunciation-assessment research."
- "It is built to name what went wrong, not just hand back a number: a frontal lisp is separated
  from a lateral lisp by diagnosing the actual substitution in the sound."
- **"a profile it cannot judge is declined rather than guessed at"** — explicit refusal-to-score.
- Honest limitation (COPPA page): "We do not have a version of the real scorer that runs inside
  the browser, and we will not describe one until we do." (cloud scoring service)
- No-score mode: before recording consent, "the practice loop runs a local demo scorer so the
  exercise still gives feedback, which is a demonstration rather than a measurement".

## Age-band policy (product pages)
| Band | Ages | Feedback policy |
|---|---|---|
| Nest | 1–3 | AI silent on purpose; no scores on toddlers |
| Garden | 4–8 | AI scoring under the hood; "zero numbers on kids" |
| Quest | 9–12 | honest mastery scores |
| Studio | 13–16 | private practice; scores |

## Diagnosis + practice design
- Lisp type via substitution (phonological-feature-like distinction).
- Word-position practice: beginning / middle / end.
- Sound mastery tracking (e.g., S 82%, R 43%, L 67%), therapist-ready reports.
- 11 speech sounds, milestone charts by age (2–5y), word generator by age+sound.

## Child privacy (COPPA page)
- Recording off until parent consent; no upload/storage before consent.
- Age in months (not DOB); random identifier (no child name).
- Two consents: recording (cloud feedback) vs optional model-improvement.
- No third-party ad/analytics; cookieless page counts; explicit "no recording/score travels with
  analytics"; parent review/delete/withdraw; 30-day response.

## Unknown
- Actual model, GOP implementation, calibration, child model specifics, thresholds.
