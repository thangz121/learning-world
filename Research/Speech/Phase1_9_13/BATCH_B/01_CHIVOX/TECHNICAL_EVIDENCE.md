# CHIVOX — Technical Evidence (E1)

## Young-learner positioning (official page)
- "Children are not simply quieter adults. Their pitch, articulation and timing differ as speech
  develops."
- Dedicated young-learner models account for: higher pitch, longer pauses, developing
  articulation, common repetitions or omissions — "not automatic failure signals".
- Age-appropriate scoring: "Calibrate scoring for early-years and primary learners".
- Diagnostics: "phoneme and word evidence, accuracy, fluency, completeness, stress, intonation,
  pauses, insertions, omissions and repetitions".
- **Quality gate first**: "Check clipping, background speech, missing audio and incomplete
  responses before presenting any pronunciation feedback."
- Product rule: "Never present a low or uncertain score as a judgment about a child. Use neutral
  recording retries, age-appropriate language and adult review where the evidence needs context."
- Age/level calibration controls: thresholds, feedback language, retry length.

## Kernel docs (E1)
- `en.word.score`: word + per-phoneme scores; syllable stress; UK/US accent; **"adaptive to
  children and adults"**; `voiced` (accept only voiced pronunciation by default);
  `gop_adjust` [-1,1] exposed; `rank` 4/100.
- `en.word.pron`: correction output — superfluous reading, missed reading, misreading.
- `en.sent.score`: overall, fluency, accuracy, integrity; stress tone, rising tone, pauses,
  loss of plosion, liaison detection.
- `en.sent.pron`: per-word correction (missed/misread/superfluous).
- `en.nsp.score`: phonics kernel.

## Assessability behavior (E1)
- `post proc failed` returned when "the audio was not successfully recorded or the engine did
  not detect the user's valid voice" → product should prompt re-record.
- Audio-quality checks documented before feedback.

## Unknown (proprietary)
- Model architecture, training data, child model specifics, calibration data, pitch normalization
  implementation, internal confidence model.
