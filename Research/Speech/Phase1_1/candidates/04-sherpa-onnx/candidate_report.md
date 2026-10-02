# Candidate 04-sherpa-onnx

## Source
k2-fsa/sherpa-onnx

## Version
1.13.8, whisper tiny.en int8 (118MB pkg)

## License
Apache-2.0

## Model License
MIT (OpenAI tiny.en)

## Dependency License
onnxruntime(MIT), self-contained wheel

## Intended Role
full local runtime

## Installation
pip install; model tarball via urllib; NOTE: accept_waveform needs float32 [-1,1], int16 input gives garbage

## Runtime
CPU int8, 4 threads

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
Words correct BUT repetition on silence-padded files (red x7, Red Apple x4). 0.07-0.14s/file (fastest). Silence->[ Silence ].

## Baseline Comparison
Speed best-in-sweep; repetition is model-30s-window behavior, not input bug (VAD-gate fixes phrase, not single word).

## Improvements
Fastest runtime; repetition finding directly motivates VAD-gating + repetition-penalty work.

## Problems
tiny.en int8 repeats on padded silence; fp32 same. Needs no_speech handling.

## Unique Capability
RETAIN

## Retention Decision
Speed reference + multi-model container (moonshine/zipformer proven same runtime).

## Future Combination
REVIEW_REQUIRED (per-checkpoint weights audit before ship)
