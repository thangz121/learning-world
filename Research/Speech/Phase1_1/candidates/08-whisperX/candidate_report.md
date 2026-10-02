# Candidate 08-whisperX

## Source
m-bain/whisperX

## Version
3.8.6 on Python 3.12 venv (needs <3.14; ctranslate2==4.4.0 pin)

## License
BSD-2-Clause

## Model License
mixed chain (faster-whisper + HF align models + pyannote diarization CC-BY-4.0 + HF agreement)

## Dependency License
ctranslate2/faster-whisper/transformers/torchcodec; triton linux-only

## Intended Role
alignment/timing

## Installation
separate py3.12 venv (torch CPU + whisperx); load_model tiny int8 + load_align_model en

## Runtime
CPU ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
Transcribe ok (Red Apple./Apple.); WORD TIMESTAMPS proven: Red 0.15-0.32, Apple 0.42-0.66. Align load 40s (one-time), 1.68s/file.

## Baseline Comparison
Only candidate proving word-level timing on our corpus.

## Improvements
Word timestamps for pronunciation error localization + demo choreography timing.

## Problems
'this is a cat' tiny heard nothing (model floor, not aligner fault); diarization NOT tested (agreement-gated).

## Unique Capability
RETAIN

## Retention Decision
Timing source for assessment + karaoke-style feedback.

## Future Combination
REVIEW_REQUIRED (RESTRICTED if bundling diarization commercially)
