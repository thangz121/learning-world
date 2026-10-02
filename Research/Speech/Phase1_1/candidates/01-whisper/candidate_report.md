# Candidate 01-whisper

## Source
openai/whisper

## Version
20250625 pip, model tiny 75MB (MIT weights)

## License
MIT

## Model License
MIT (code+weights)

## Dependency License
torch/numba/tiktoken(MIT)/ffmpeg-CLI

## Intended Role
ASR baseline

## Installation
pip install openai-whisper + torch CPU; model auto-download to D:\speech-lab\models

## Runtime
CPU-only ASUS, no GPU

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
6/6: Red/blue/Cat/Red Apple/Apple exact, silence->empty. ~0.4s/file, load 0.4s.

## Baseline Comparison
Same corpus. Baseline LWE has NO transcript engine, so any transcript is new capability.

## Improvements
First transcript-capable baseline; honest empty on silence.

## Problems
None observed at tiny scale.

## Unique Capability
Reference fp32 accuracy anchor for int8/quantized comparisons.

## Retention Decision
RETAIN

## Future Combination
STT accuracy anchor; compare distill/quant variants against it.
