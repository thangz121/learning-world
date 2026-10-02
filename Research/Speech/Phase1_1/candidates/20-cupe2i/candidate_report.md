# Candidate 20-cupe2i

## Source
Tabahi/CUPE-2i (english ckpt, 30M)

## Version
repo scripts + HF ckpt

## License
GPL-3.0 (HF card + GitHub)

## Model License
n/a (own training)

## Dependency License
torch/torchaudio(+torchcodec; broken FFmpeg link -> soundfile patch used, documented)

## Intended Role
phoneme recognition (contextless)

## Installation
repo clone + example patched (soundfile load, float32); english ckpt via HF

## Runtime
CPU ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
Phonemes correct: red->rEd-ish, cat->k@t, apple->aelp, red apple full sequence. ~3s/file. Silence -> weak false-positive.

## Baseline Comparison
Contextless (no LM correction) acoustic-pure embeddings; 16ms frames + group outputs.

## Improvements
Light phoneme front-end alternative to wav2vec2-300M.

## Problems
GPL-3.0; slower than hoped (~3s); silence FP; torchcodec install broken on this FFmpeg.

## Unique Capability
RETAIN as RESEARCH_ONLY

## Retention Decision
Phoneme evidence source; compare vs allophant when unblocked.

## Future Combination
RESTRICTED (GPL-3.0)
