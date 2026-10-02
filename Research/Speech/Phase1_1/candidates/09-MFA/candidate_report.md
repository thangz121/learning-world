# Candidate 09-MFA

## Source
MontrealCorpusTools/Montreal-Forced-Aligner

## Version
3.4.2 pip (models CC-BY-4.0, kaldi Apache)

## License
MIT

## Model License
CC-BY-4.0 models (attribution)

## Dependency License
kaldi via _kalpy (NO Windows wheel -> BLOCKED)

## Intended Role
forced alignment (offline tool)

## Installation
pip install ok; CLI dead on import _kalpy; kalpy has no PyPI distribution

## Runtime
Windows ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
NOT RUN: blocked before first audio. Evidence of block recorded.

## Baseline Comparison
None measured; concept value stands (offline phone-boundary tool).

## Improvements
None measured.

## Problems
Hard-blocked on this machine; needs conda/WSL path in Phase 1.2.

## Unique Capability
HOLD (TECHNICALLY VALUABLE / INTEGRATION BLOCKED)

## Retention Decision
Offline alignment tool, never runtime.

## Future Combination
REVIEW_REQUIRED (CC-BY attribution + thirdparty audit)
