# Candidate 13-espnet

## Source
espnet/espnet

## Version
202610.post2, conformer5 librispeech (465MB, kamo-naoyuki)

## License
Apache-2.0

## Model License
model card license per-checkpoint (verify at ship)

## Dependency License
torch/torchaudio/sentencepiece/s3prl(optional); zoo downloader reads author-absolute paths (manual construct used)

## Intended Role
toolkit ASR + G2P

## Installation
pip install (py3.12, sentencepiece needs wheel); manual Speech2Text construct (config+pth+bpe+stats); 1D float input (2D breaks STFT)

## Runtime
CPU ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
6/6 PERFECT incl. silence->empty: RED/BLUE/CAT/RED APPLE/APPLE. ~0.35s/file. G2P: red apple -> R EH1 D / AE1 P AH0 L (ARPAbet+stress, matches LWE PhonemeTable).

## Baseline Comparison
Best one-word exactness in sweep (tied nemo); G2P directly feeds LWE phoneme targets.

## Improvements
High-accuracy conformer path + ARPAbet G2P for vocab pipeline.

## Problems
RNNT variant needs warprnnt (Windows-hostile); s3prl-frontend variant breaks on new torch; zoo downloader fragile on Windows.

## Unique Capability
RETAIN

## Retention Decision
Accuracy reference + G2P supplier; conformer-small fine-tune candidate.

## Future Combination
CLEAR (code); per-checkpoint at ship
