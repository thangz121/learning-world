# SYSTEM 04 — Microsoft Pronunciation Assessment (Azure Speech)

STATUS: **BLOCKED** at execution (Azure key required; signup requires payment method).
Public REST/SDK documentation is unusually complete (E1) — the full result schema is public.

Key E1 findings:
- Scores: AccuracyScore, FluencyScore, CompletenessScore, ProsodyScore, PronScore
- Granularity: Phoneme / Syllable (en-US) / Word / FullText
- **ErrorType: None | Omission | Insertion | Mispronunciation | UnexpectedBreak | MissingBreak | Monotone**
- **NBestPhonemes**: per expected phoneme, top-5 spoken-phoneme candidates with confidence
  (e.g., expected `ɛ` score 47, top alternative `ə` score 100)
- Offset/Duration (100 ns units) for phoneme/syllable/word alignment
- Scripted vs unscripted (ASR text + assessment); prosody (en-US)
