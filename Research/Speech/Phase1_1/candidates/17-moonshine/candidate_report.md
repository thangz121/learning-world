# Candidate 17-moonshine

## Source
UsefulSensors/moonshine via sherpa-onnx 1.13.8 (tiny-en-int8, 108MB pkg)

## Version
sherpa runtime + moonshine tiny 27M

## License
MIT (Useful Sensors 2024, verified LICENSE in pkg)

## Model License
MIT

## Dependency License
same as 04

## Intended Role
small offline STT

## Installation
sherpa from_moonshine; float32 PCM input

## Runtime
CPU int8 ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
6/6 PERFECT incl. Red EXACT (fixes vosk/speechbrain homophone), silence->empty, NO repetition. 0.02-0.04s/file (10x whisper).

## Baseline Comparison
Best speedxaccuracy in sweep; smallest params (27M).

## Improvements
Primary offline Windows STT candidate: drop-in tiny-vocab engine.

## Problems
None observed on corpus; child-speech validity still NOT PROVEN (all candidates share this).

## Unique Capability
RETAIN

## Retention Decision
First-choice runtime for Phase 1.2 prototype adapter.

## Future Combination
CLEAR (this checkpoint; vi-variant has revenue-cap license -> avoid)
