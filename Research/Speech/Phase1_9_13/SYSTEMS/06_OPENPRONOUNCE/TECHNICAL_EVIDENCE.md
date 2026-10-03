# OpenPronounce — Technical Evidence

## E3/E4 — verified by source + local run
- Embedding model: `facebook/wav2vec2-large-960h`; phone model:
  `facebook/wav2vec2-lv-60-espeak-cv-ft`.
- Scoring: `score = 0.3*acoustic + 0.4*phonemes + 0.3*words`; `WORD_ERROR_THRESHOLD=0.4`.
- Acoustic component: FastDTW distance between learner and reference embeddings (normalized
  by path length); reference is TTS of the target text.
- Phone component: CTC phone recognition on learner audio, compared to phonemized expected text
  (espeak via phonemizer); error list with expected vs actual IPA per word.
- Built-in leniency pairs in phone comparison (e.g., tense/lax, ɔ/ɑ, ɾ/t).
- Prosody: F0 (interpolated) + energy contours returned.

## E4 — behavior on LWE corpus (see PLAYTEST.md)
- Clean adult words can hit 100; several adult words score low (blue 23, book 25) — the same
  words our own scorer finds hard; red-vs-blue and silence are correctly rejected (6.8 / 0.0).
- On child tokens, OpenPronounce is **harsh across the board**: all 6 child cases ≤ 24.7,
  including the human-confirmed-correct `child_07_one` at 2.46 with PER 1.33.
- The missing-final-/r/ "four" cases scored 12–25 → detected, but not separable from the
  false-low correct case by score alone on this tiny sample.

## Child-specific handling
None. Wav2Vec2 adult-trained; no child adaptation. Our result mirrors the LWE finding:
independent adult-oriented systems also degrade on ~4yo speech.

## Fidelity / attempt detection
No dedicated fidelity class. Silence → score 0 + word error (implicit). Wrong word → very low
score. No NO_SPEECH/INCOMPLETE/FREE_SPEAK taxonomy (contrast with Speechace E1).

## Privacy / offline
Runs fully local after model download. Default gTTS reference is a cloud call; replaceable with
a local reference (as done here).
