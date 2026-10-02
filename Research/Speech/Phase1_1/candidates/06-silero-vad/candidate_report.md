# Candidate 06-silero-vad

## Source
snakers4/silero-vad

## Version
6.2.3 (torch, ~2MB)

## License
MIT (code)

## Model License
stated MIT, badge mismatch CC BY-NC seen -> verify before redistribute

## Dependency License
torch/onnxruntime

## Intended Role
VAD

## Installation
pip install silero-vad; get_speech_timestamps on corpus

## Runtime
CPU ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
6/6: speech localized (0.1-0.8s), silence/noise/tone -> empty. 0.01-0.15s/file.

## Baseline Comparison
Only candidate that rejects noise AND tone while keeping soft speech segments.

## Improvements
Robust VAD gate for every STT in the pool (fixes sherpa phrase repetition in combo test).

## Problems
None observed; weights redistribution needs re-verification (badge conflict).

## Unique Capability
RETAIN

## Retention Decision
Front-end gate for all STT; endpoint detector for SpeakingExerciseRunner.

## Future Combination
REVIEW_REQUIRED (weights badge conflict)
