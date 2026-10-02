# Candidate 11-pron-bench

## Source
fjburgosf/pronunciation-scoring-benchmark

## Version
scripts, no LICENSE file (README claims MIT), data Apache/GATED

## License
MIT-claimed (file missing)

## Model License
wav2vec2-base Apache-2.0; speechocean762 Apache; L2-ARCTIC/EpaDB gated

## Dependency License
librosa/sklearn/torch/transformers

## Intended Role
scoring methodology

## Installation
cloned; ran 05-style handcrafted features + wav2vec2-base embeddings on OUR corpus (full scoring needs manual openslr download, skipped)

## Runtime
CPU ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
MFCC/RMS/ZCR separate speech/silence/noise (rms 0.0 vs 0.02-0.17); w2v2 768-d embeddings 0.07-0.27s/file.

## Baseline Comparison
Methodology executes on LWE audio; feature recipe reusable for LocalAcousticProvider v2.

## Improvements
Acoustic baseline recipe (handcrafted + self-supervised) adaptable to on-device scoring.

## Problems
Full PCC/MSE/MAE benchmark NOT run (dataset not downloaded); no LICENSE file = hygiene risk.

## Unique Capability
RETAIN

## Retention Decision
Scoring methodology source for Phase 1.2 (replicate GOP-style scorer on our vocab).

## Future Combination
REVIEW_REQUIRED (missing LICENSE file, gated datasets)
