# SpeakStar — Technical Evidence (E1)

## Verbatim product description (sakura5.app)
- "English speaking practice for Vietnamese children — free and fully offline."
- "The child taps the mic and reads the sentence aloud. Each word is coloured — heard, nearly
  heard, or not heard — with 0 to 3 stars for the attempt, all worked out on the device."
- "24 lessons, four levels … listen to a short dialogue, say every sentence and get scored, then
  read a short passage aloud."
- "All 44 English sounds … full IPA chart with an animated mouth, and one practice sentence
  loaded with each sound. The reference recordings are by phoneticians, not a text-to-speech
  voice."
- "Read-aloud passages and six short songs with word-by-word highlighting — both work with no
  microphone at all."
- "Every instruction, label and translation is in Vietnamese."
- "Nothing is collected. No account, no ads, no analytics, no third-party SDKs, no network
  requests. All 476 recordings are bundled, so the whole app works with the internet switched
  off."

## Interpretation (marked as inference)
- The "heard / nearly heard / not heard" tri-state + stars is a child-facing decision layer,
  not a 0–100 score — consistent with SpeechStep/Speech Blubs patterns.
- On-device scoring with 476 bundled recordings implies a small local model (architecture not
  disclosed).
- No explicit "cannot assess" state documented (unlike SpeechStep's "declined").

## Unknown
- Model, thresholds, language of scorer, whether it refuses to score.
