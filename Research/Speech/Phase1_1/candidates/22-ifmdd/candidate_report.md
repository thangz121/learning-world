# Candidate 22-ifmdd

## Source
Secondtonumb/IF-MDD (Interspeech 26, F1 57.52 PER 14.30)

## Version
not run

## License
NO LICENSE FILE in repo

## Model License
checkpoints Haopeng/* (terms unverified)

## Dependency License
CTC + Llama3.2-1B variants

## Intended Role
mispronunciation detection

## Installation
audit only (metrics + inference.py recorded)

## Runtime
n/a

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
NOT RUN.

## Baseline Comparison
Published MDD numbers to beat for our GOP-style scorer.

## Improvements
None measured.

## Problems
Repo has no license file -> cannot use code until clarified; gated EpaDB data.

## Unique Capability
HOLD (license + weight)

## Retention Decision
Benchmark target for Phase 1.2 scorer.

## Future Combination
BLOCKED until license clarified
