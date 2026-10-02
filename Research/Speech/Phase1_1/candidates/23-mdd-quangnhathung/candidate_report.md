# Candidate 23-mdd-quangnhathung

## Source
quangnhathung/AI-Mispronunciation-Detection-and-Diagnosis @ cac54c9 (2026-05-23), clone D:\speech-lab\src-mdd1

## Version
cac54c9 2026-05-23 "chore: create file requirement for import library"; venv D:\speech-lab\venvs\p0 py3.14.7 torch 2.14.1+cpu transformers 5.17.0

## License
AGPL-3.0 (LICENSE file = FSF AGPL v3 text, 661 lines). Copyleft + Sec.13 remote-network source-offer clause.

## Model License
facebook/wav2vec2-base-960h base (Apache-2.0) + custom head; FINETUNED CHECKPOINT NOT SHIPPED. No checkpoints/ dir, no *.pt in repo (git ls-files: no checkpoint/*.pt/best). README publishes no weight link.

## Dependency License
transformers/soundfile/torchaudio(Apache/MIT/BSD) + tgt/g2p_en/nltk/inflect/distance (mixed MIT/Apache) + matplotlib/tensorboard/sounddevice. Installed to venv only: `pip install g2p_en tgt` OK (g2p_en 2.1.0, tgt 1.5, nltk 3.10.3). Nothing installed into repo.

## Intended Role
Phoneme-level MDD scorer: wav2vec2 audio x canonical phonemes -> per-phoneme P(correct) via cross-attention + MLP.

## Installation
`git clone https://github.com/quangnhathung/AI-Mispronunciation-Detection-and-Diagnosis D:\speech-lab\src-mdd1` OK. `pip install g2p_en tgt` to venv p0 OK. No model download attempted (no ckpt to load per task rule model->D:\speech-lab\models).

## Runtime
CPU-only ASUS, no GPU. Code has cpu fallback (`device if torch.cuda.is_available() else 'cpu'`) but never reached (ckpt gate first).

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates. Focus pair: sapi_red_apple.wav (23590 samples, 16kHz) vs pregen_apple_normal.wav (13440 samples, 16kHz) — verified via soundfile.

## Results
NOT RUN on audio. No score/phoneme numbers for sapi_red_apple.wav vs pregen_apple_normal.wav (none fabricated).
- Tried (workdir D:\speech-lab\src-mdd1): `MDDPredictor(model_path='./checkpoints/best_mdd_model_v4.pt', device='cpu')` -> `FileNotFoundError: Khong tim thay file trong so mo hinh tai: ./checkpoints/best_mdd_model_v4.pt` (predict.py:29 guard). Same path hardcoded in src/application/app.py:16 + train predict TEST_WAV=`C:/Users/quang/Downloads/voice/...` (absent).
- Partial (no audio, no weights): `G2p()('red apple')` -> `['R','EH1','D',' ','AE1','P','AH0','L']`; dictionary 46 phones (`ARPABET_PHONEMES`, PAD/UNK/SIL/SP + 42) maps R->36 EH->12 D->23 OK.

## Baseline Comparison
No runnable scorer so no baseline delta. README claims no published F1/PER; only local logs/training_history.csv epochs 1-9 (train_loss 0.81->0.59, val F1 error-class ~0.43-0.48). L2-ARCTIC gated data required to reproduce.

## Improvements
None measured (blocked before audio).

## Problems
Missing weights (fatal); L2-ARCTIC data not in repo (data/processed/mock.txt only); AGPL-3.0 blocks commercial ship/vendor without relicense review; GUI (tkinter MDDApp) needs display; torchaudio 2.11 vs torch 2.14 minor mismatch (not hit).

## Unique Capability
Clean reference ARCHITECTURE for Phase 1.2 scorer: frozen wav2vec2-base-960h (10 layers) + phoneme embedding + Bi-GRU context + Multihead cross-attn + LayerNorm + MLP scoring head (src/model/mmd_model_v2.py), 46-phone ARPABET dict + IPA map. Idea-only (do not copy code under AGPL).

## Retention Decision
BLOCKED (no runnable weights + AGPL copyleft). Keep as design reference, not runtime.

## Future Combination
Do not vendor. If Phase 1.2 builds GOP-style scorer, reimplement Bi-GRU+attn idea from scratch under permissive license; audit wav2vec2 weight license per checkpoint before ship.
