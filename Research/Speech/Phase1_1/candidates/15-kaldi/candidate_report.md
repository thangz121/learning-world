# Candidate 15-kaldi

## Source
kaldi-asr/kaldi

## Version
sparse checkout (decoder sources + egs/wsj/s5 + windows props), no build

## License
Apache-2.0 + custom legal notices (OpenFST Apache, JAMA public-domain)

## Model License
per-corpus under egs (LDC/restricted mostly)

## Dependency License
ATLAS/MKL/OpenBLAS + MSVC (absent -> BLOCKED-build)

## Intended Role
decoding/lexicon/alignment primitives

## Installation
sparse clone; audited faster-decoder/grammar-fst sources + VS props; kaldialign 0.12.0 demo

## Runtime
Windows ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
kaldialign: RED->READ sub=1/err 0.5 proven (error-localization primitive usable by LWE keyword match). No audio decode (no build).

## Baseline Comparison
Kaldi methodology already runs in-repo through VOSK (kaldi-based, candidate 05).

## Improvements
Text-alignment primitive for scoring; recipe knowledge for lexicon/FST work.

## Problems
Full build not attempted (toolchain + corpora); never propose Kaldi as runtime.

## Unique Capability
HOLD (TECHNICALLY VALUABLE / INTEGRATION BLOCKED)

## Retention Decision
Primitive/algorithm source only.

## Future Combination
REVIEW_REQUIRED (tools/extras + corpora at use)
