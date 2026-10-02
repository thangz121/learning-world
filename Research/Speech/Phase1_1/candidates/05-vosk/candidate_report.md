# Candidate 05-vosk

## Source
alphacep/vosk-api

## Version
0.3.45, small-en-us-0.15 40MB (Apache)

## License
Apache-2.0

## Model License
Apache-2.0 (pinned model; other models on page include AGPL/GPL/NC -> avoid)

## Dependency License
self-contained C++ + cffi, no ML deps

## Intended Role
streaming/offline ASR

## Installation
pip install vosk; model zip via urllib; KaldiRecognizer loopback-style chunk feed

## Runtime
CPU ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
5.5/6: blue/cat/red apple/apple/silence/noise correct; red->'read' homophone. ~0.4s/file, load 0.4s.

## Baseline Comparison
Smallest footprint ASR with streaming API; homophone error is grammar-fixable (constrained vocab).

## Improvements
Streaming partial-result API + 40MB offline model; empty output on silence AND noise.

## Problems
One-word homophone confusion without grammar; test constrained-grammar mode in Phase 1.2.

## Unique Capability
RETAIN

## Retention Decision
Streaming fallback / low-RAM path; grammar-constrained single-word recognizer.

## Future Combination
CLEAR conditional on pinning Apache-2.0 models
