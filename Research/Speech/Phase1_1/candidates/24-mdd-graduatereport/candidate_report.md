# Candidate 24-mdd-graduatereport

## Source
GraduateReport/pronunciation-assessment @ 9ee5f58 (2026-03-24), clone D:\speech-lab\src-mdd2 (README clone URL points to trungkien1511/pronunciation-assessment fork)

## Version
9ee5f58 2026-03-24 "Refactor README structure and content"; venv p0 py3.14.7 torch 2.14.1+cpu

## License
NO LICENSE FILE in repo (git ls-files: .gitignore/README/app.py/phoneme_assessment/*/scripts/*, no LICENSE/COPYING; README has no license section).

## Model License
wav2vec2-l2arctic_finetuned/ ships ONLY config.json + preprocessor_config.json, NO pytorch_model.bin/model.safetensors. Base referenced facebook/wav2vec2-base (Apache-2.0) but finetuned weights absent; default vocab path `d:/test/dataset_splits/vocab.json` absent.

## Dependency License
Declared: textgrid/librosa/pandas/datasets/soundfile/tqdm. Undeclared but imported: torch/transformers/g2p_en/Levenshtein/nltk. All already in venv p0 (g2p_en 2.1.0 installed for #23, Levenshtein 0.27.5 preinstalled). No repo install.

## Intended Role
Phoneme-level MDD via finetuned Wav2Vec2-CTC phone recognizer + G2P reference + Levenshtein align -> sub/del/ins report + 0-100 score.

## Installation
`git clone https://github.com/GraduateReport/pronunciation-assessment D:\speech-lab\src-mdd2` OK. No pip needed. Win console needs `$env:PYTHONUTF8=1` (emoji prints crash under cp1258 otherwise).

## Runtime
CPU-only ASUS, no GPU (code auto-selects cuda if available, else cpu).

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates. Focus pair sapi_red_apple.wav vs pregen_apple_normal.wav (see #23 for verified shapes).

## Results
FULL INFERENCE BLOCKED. No score/phoneme numbers for sapi_red_apple.wav vs pregen_apple_normal.wav (none fabricated).
- Tried: `python app.py --audio sapi_red_apple.wav --text "red apple" --model_dir wav2vec2-l2arctic_finetuned` -> `LOI: Error no file named model.safetensors, or pytorch_model.bin, found in directory D:\speech-lab\src-mdd2\wav2vec2-l2arctic_finetuned.` (with PYTHONUTF8=1; without it: UnicodeEncodeError cp1258 on emoji).
- Partial (no audio model): `PronunciationAligner().text_to_phonemes('red apple')` -> `['R','EH','D','AE','P','AH','L']` OK; `('apple')` -> `['AE','P','AH','L']`. Synthetic (NOT audio) deletion demo ref 7 phones vs mock pred 6 phones -> 1 deletion (L), mock score 85.71/100. Real `L2ArcticInference.predict(audio)` never ran (no weights/vocab).

## Baseline Comparison
No runnable recognizer so no baseline delta. No published PER/F1 in README.

## Improvements
None measured (blocked). Aligner logic verified on synthetic strings only.

## Problems
No license (cannot copy code, idea-only); hardcoded `d:/test/...` paths (app.py model_dir, inference.py vocab_path, scripts); missing vocab.json + training metadata; finetune pipeline needs gated L2-ARCTIC TextGrids; emoji logging breaks default Win console.

## Unique Capability
Minimal G2P(Levenshtein-editops)->grade pipeline that RUNS without weights (alignment.py: text_to_phonemes + align_and_grade sub/del/ins + score=100-err/total*100). Good SPEC for Phase 1.2 aligner (reimplement cleanly; pair with licensed CTC phone recognizer e.g. allophant for real pred_phonemes).

## Retention Decision
BLOCKED (no license + no runnable weights). Keep as alignment-logic spec only.

## Future Combination
Do not vendor. Reimplement editops grading from scratch; source real phoneme recognizer elsewhere; require license + weights audit before any ship.
