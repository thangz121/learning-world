# Speech Blubs — Technical Evidence

## Verified (E1, primary sources)
- App is "voice-controlled": exercises trigger on child speech; on-device voice detection
  feature (privacy policy §2: "WE WILL NOT SAVE NOR COLLECT ANY VOICE RECORDS OR ANY PHOTOS").
- Mic + camera permission required; face filters via Apple TrueDepth (on-device).
- Research page cites a Proof-of-Concept design whitepaper and a MARS quality assessment;
  no ASR/pronunciation model paper published.
- No developer docs, no SDK, no API, no GitHub org found.

## Claims (E0, not verified)
- "Voice-activated functionality that provides a fun, interactive learning experience"
- Rewards for trying (ABA-inspired positive reinforcement) — implies attempt detection may be
  lenient: the product rewards attempts, not correct pronunciation.

## Unknown
- ASR engine and pronunciation scoring internals
- Whether any pronunciation correctness is assessed beyond speech presence
- Child-specific acoustic handling

## Relevance to LWE
- Confirms a mature child product can operate with **on-device voice activity only** and no
  cloud audio — a privacy pattern LWE could mirror (E1).
- UX pattern: reward for attempt (retry-friendly), not a 0–100 score (E0).
