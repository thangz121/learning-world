# Candidate 03-faster-whisper

## Source
SYSTRAN/faster-whisper

## Version
1.2.1, tiny int8, ctranslate2 4.8.2

## License
MIT

## Model License
MIT chain (Systran CTranslate2 model)

## Dependency License
ctranslate2(MIT)/onnxruntime(MIT)/av19 (INCOMPAT: audio.py uses removed metadata_errors kwarg)

## Intended Role
optimized inference

## Installation
pip install; workaround: feed float32 PCM array instead of file path (av bypass)

## Runtime
CPU int8, ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
6/6 + language_probability 1.00. ~0.28s/file (fastest whisper).

## Baseline Comparison
Faster than 01 at same accuracy; adds usable confidence.

## Improvements
int8 CPU path with confidence scores for SpeakingPassPolicy Strong (>=0.75) gating.

## Problems
av>=14 breaks file input; PyAV FFmpeg bundle needs codec review before ship.

## Unique Capability
RETAIN

## Retention Decision
Low-latency STT service candidate; pair with VAD.

## Future Combination
REVIEW_REQUIRED (PyAV/FFmpeg bundle, CUDA/cuDNN if GPU)
