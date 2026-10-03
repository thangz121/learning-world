# ELSA — Technical Evidence

## Architecture (E2 — published by ELSA authors)

2016 (Interspeech demo):
- Client-server; audio streamed to server during speech; server starts processing early.
- Beep → user speaks → **server-side endpointing** ends the trial.
- Kaldi + custom-trained DNN acoustic models; L1/L2 language-pair models loaded per node;
  AWS ELB scaling; multiple regions.
- Feedback: per-phoneme color code + phonetic hints; intonation exercise (syllable stress,
  sentence intonation/rhythm); conversation exercise (word-level feedback).

2023 (SLATE — Speech Analyzer, spontaneous speech):
- Own end-to-end ASR fine-tuned on 100+ hours of real learner speech (40% relative WER reduction
  in-house).
- Speaker identification: voice embedding from sign-up + segmentation clustering (diarization-like)
  to exclude other speakers (privacy-preserving).
- Pronunciation score: ASR transcript → compare to audio → detect phonetic deviations from native
  speech, focusing on intelligibility-affecting errors; amount + severity feed the score.
- Intonation: pitch/energy contours + word prominence (expected vs spoken).
- Fluency: pacing (WPM + variation), pausing (optional vs mandatory pauses), hesitations.
- Grammar: neural GEC + range; Vocabulary: CEFR word levels.
- Overall score = weighted combination; feedback per dimension.

## API (E1)
- Scripted: sentence/word/phoneme scores, mispronunciation hints, word stress, reading speed,
  pausing, fluency, intonation. Unscripted: transcript + pronunciation/prosody/fluency/grammar/vocab.
- Schemas are partner-gated; not modeled publicly.

## Fidelity / attempt detection
- No separate "fidelity" score documented. Endpointing + (scripted) expected-script scoring gate
  the sample; unscripted mode accepts spontaneous speech and ASR-transcribes it.
- In practice: scripted mode compares against expected text; silence/short audio likely handled
  by server endpointing + scoring floor. NOT verified on our corpus.

## Child-specific handling
- None documented. Models trained on adult non-native speech (100h real interactions).

## Final consonants
- No explicit final-consonant handling published; phoneme-level deviation detection would cover
  them in principle (E2, inferred).

## Relevance to LWE
- Strong reference architecture for streaming + server endpointing + phoneme-deviation scoring.
- Confirms L1-specific modeling as a mature technique (not needed for LWE mono-L1 audience).
- No reusable code/model; API is B2B-gated.
