# Candidate 10-allophant

## Source
kgnlp/allophant

## Version
1.0.0 sdist (checkpoint Apache-2.0, base wav2vec2-xls-r-300m Apache)

## License
MIT (package)

## Model License
Apache-2.0 checkpoint verified on HF (only allophant.pt published; needs package code for tokenizer)

## Dependency License
torch/torchaudio/transformers/epitran(MIT)/panphon(MIT)/phonemizer(GPL-3.0!)/stanza + maturin+Rust build (absent)

## Intended Role
phoneme recognition

## Installation
pip build fails (pandas 1.5.3 no cp312 wheel, marshmallow pins, maturin needs cargo+MSVC absent); source import chain resolved to phonemes Rust ext

## Runtime
Windows ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
NOT RUN end-to-end. Checkpoint license + dep chain verified by read.

## Baseline Comparison
Phoneme-recognition design (unseen inventory) documented for Phase 1.2.

## Improvements
None measured.

## Problems
Blocked on Rust/MSVC toolchain; phonemizer GPL dep already flags distribution anyway.

## Unique Capability
HOLD (TECHNICALLY VALUABLE / INTEGRATION BLOCKED)

## Retention Decision
Future phoneme engine if toolchain + G2P license solved.

## Future Combination
RESTRICTED (phonemizer GPL-3.0 in chain)
