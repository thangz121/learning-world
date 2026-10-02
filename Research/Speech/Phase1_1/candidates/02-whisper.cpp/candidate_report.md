# Candidate 02-whisper.cpp

## Source
ggml-org/whisper.cpp (runtime via pywhispercpp 1.5.1 wheel)

## Version
ggml-tiny.bin 74MB

## License
MIT

## Model License
MIT (OpenAI weights, ggml convert)

## Dependency License
none (prebuilt wheel); native build needs cmake/MSVC (absent -> BLOCKED-build)

## Intended Role
native/local runtime

## Installation
pip install pywhispercpp; ggml-tiny.bin via urllib (first auto-DL was truncated 8.6MB, redownloaded 77MB)

## Runtime
CPU-only ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
6/6 exact incl. silence->[BLANK_AUDIO]. ~0.33s/file.

## Baseline Comparison
Matches 01 accuracy natively, no torch needed at runtime.

## Improvements
Proves ggml runtime deployable on Windows without Python ML stack.

## Problems
pywhispercpp 1.5.1: redirect_log kwarg broken; auto-download unverified (manual re-DL needed).

## Unique Capability
Native CPU runtime with honest non-speech token.

## Retention Decision
RETAIN

## Future Combination
Deployment-shape reference for offline Windows (C++ embedding path stays future work).
