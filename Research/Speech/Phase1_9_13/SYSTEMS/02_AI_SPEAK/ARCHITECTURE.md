# AI Speak / M-Speak — Architecture Reconstruction

Documented behavior only (E1), internals UNKNOWN:

```
MIC (app)
  ↓
PROMPT (word/phrase/story page)
  ↓
RECORD + END DETECTION (UNKNOWN)
  ↓
M-SPEAK AI (UNKNOWN: ASR + scoring; on-device vs cloud NOT stated)
  ↓
SCORE (stars 1-3) + COMMENTS (pronunciation / intonation / fluency / word stress)
  ↓
REWARD (coins/stickers) + parent progress report
```

- ASR: advertised (Speech-to-Text), engine UNKNOWN
- Phoneme/syllable: claimed both; UNKNOWN
- Alignment: UNKNOWN
- Calibration vs humans: no public data
- Audio retention: not stated in policy

Do not assume the architecture from marketing copy.
