# Candidate 18-kidwhisper

## Source
SatwikDutta/kid-whisper-tiny-en-myst (LiteChildASR, MyST fine-tune, WER 15.9%)

## Version
not downloaded

## License
Apache-2.0 (repo)

## Model License
repo Apache; checkpoint behind HF gated terms

## Dependency License
whisper stack

## Intended Role
child-tuned tiny

## Installation
repo listed (safetensors+config); weight download 401 GATED -> access request needed

## Runtime
n/a

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
NOT RUN. Published metrics only: WER 15.9% (11.8 filtered), RTF 0.23-0.41 Pi.

## Baseline Comparison
Direct child-tuned drop-in for whisper-tiny when access granted.

## Improvements
None measured.

## Problems
HOLD (gated access)

## Unique Capability
A/B vs 01/17 on child audio in Phase 1.2.

## Retention Decision
REVIEW_REQUIRED (gated terms)

## Future Combination
-
