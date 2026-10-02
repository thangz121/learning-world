# Candidate 12-speechbrain

## Source
speechbrain/speechbrain

## Version
1.1.1, asr-wav2vec2-commonvoice-en (1.3GB)

## License
Apache-2.0

## Model License
per-checkpoint (this one: wav2vec2 + commonvoice terms)

## Dependency License
torch/torchaudio/sentencepiece; HF symlink privilege workaround (manual DL)

## Intended Role
toolkit ASR

## Installation
pip install; manual urllib DL of 4 files (HF cache symlink WinError 1314); local from_hparams

## Runtime
CPU ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
Words ok (READ homophone like vosk, RED APPLE, APPLE); silence -> garbage repeat. 0.3-1.3s/file, load 95s.

## Baseline Comparison
Confirms homophone + silence-repeat patterns across architectures (evidence, not ranking).

## Improvements
Toolkit breadth (embeddings/ASR/VAD recipes) for Phase 1.2 experiments.

## Problems
1.3GB for tiny-vocab quality below smaller models; silence handling same gap as sherpa.

## Unique Capability
RETAIN

## Retention Decision
Recipe source (speaker embeddings, enhancement) more than ASR engine.

## Future Combination
CLEAR (code); per-model check at use
