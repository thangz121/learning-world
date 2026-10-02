# Candidate 25-mdd-modelz

## Source
teinhonglo/modelz-mdd @ a381543 (2024-11-19, [WIP] E2E-MDD service), clone D:\speech-lab\src-mdd3

## Version
a381543 2024-11-19 "clean up"; venv p0 py3.14.7 torch 2.14.1+cpu (repo pins torch==1.13.1+cu116 / py3.8 — incompatible)

## License
NO LICENSE FILE in repo (git ls-files: Dockerfile/README/app.py/client.py/decoder.py/utils.py/wer.py/conf/*+/local/*, no LICENSE/COPYING).

## Model License
models/mdd/ ABSENT. README Step 2: download `model.zip` from `https://140.122.184.167:5567/sharing/qRaWMnSBC` -> `models/mdd/` + `unzip wav2vec2-mdd.zip`. Tested 2026-10-02: TCP 140.122.184.167:5567 OPEN but HTTPS returns Synology DSM login HTML (HTTP 200, NAS page), not a zip — link effectively dead/expired. Custom AutoMDDModel/AutoDualMDDModel/AutoProtoMDDModel weights unauditable. Base likely wav2vec2-large-lv60 class (see local/models/wav2vec2_model.py).

## Dependency License
Declared requirements.txt: mosec + msgspec only. Actually imported: torch/torchaudio/transformers/datasets/soundfile/g2p_en/pysndfx/ctcdecode/six/Levenshtein + mosec server + OpenAI LLM (conf/llm_config.yaml model gpt-4o, OPENAI_API_KEY placeholder, feedback zh-tw). Pinned CUDA stack unusable on CPU-only Win/py3.14.

## Intended Role
E2E-MDD service: wav2vec-CTC phone recognizer + prompt-conditioned AudioTextEncoder + CTC-GOP scoring + Dictate/Transcript/Feedback/GOP JSON + LLM teaching suggestions (mosec server).

## Installation
`git clone https://github.com/teinhonglo/modelz-mdd D:\speech-lab\src-mdd3` OK. `pip install mosec` to venv p0 FAILED: `metadata-generation-failed`, `Preparing metadata (pyproject.toml) ... error`, Rust/cargo bootstrap, `cp314-win_amd64` (mosec 0.9.7 sdist, no py3.14 wheel). `import app` FAILED: `ModuleNotFoundError: No module named 'pysndfx'` (utils.py:11) + SyntaxWarning invalid escape `\,`. ctcdecode (`git clone --recursive parlance/ctcdecode + pip install .`) NOT attempted (compile toolchain risk on Win/py3.14). Nothing installed into repo.

## Runtime
Designed for `cuda:1` + `python app.py --timeout 12000` (mosec Server: Validation/Preprocess/Inference, max_batch_size 8) + `python client.py` msgpack POST to localhost:8000/inference. Dockerfile base nvidia/cuda:11.6.2 + conda py3.10. CPU-only lab cannot run as designed.

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates. Server never started (no model + no mosec), so NO audio sent.

## Results
NOT RUN on audio. No score/phoneme/GOP numbers for sapi_red_apple.wav vs pregen_apple_normal.wav (none fabricated).
- Tried: model-link fetch -> DSM HTML, not zip (see above). `pip install mosec` -> fail (see above). `import app` -> pysndfx missing.
- Partial pure-python (no audio/model): `GreedyDecoder(['<pad>','a','b']).decode(randn(1,10,3))` -> `[['a a b a']]` OK; `wer.calc_wer` identical 7-phone strings -> `WER: 0.00%`, drop-one -> `WER: 14.29%` (synthetic strings, NOT audio). `compute_gop/ctc_loss_denom` code read but never executed (needs logits + prompt_ids from missing model).

## Baseline Comparison
No runnable service so no baseline delta. Design references Cao et al. Interspeech 2024 CTC-GOP + Zhong et al. 2024 LLM feedback (papers cited, not measured here).

## Improvements
None measured (blocked). Example wav `example/Something_good_just_happened.wav` present but unused without weights.

## Problems
WIP + no license; dead model link; GPU/Docker + py3.8 pin vs lab py3.14 CPU; mosec Rust build fail; ctcdecode compile risk; LLM needs paid key + zh-tw guideline (`conf/feature: 請使用zh-tw正體中文進行回覆`); Before-ship weight/deps audit impossible.

## Unique Capability
Richest E2E-MDD SPEC in sweep (even though unrunnable): prompt-conditioned AudioTextEncoder (cat/add/parallel fusion) + PredictionHead/ConvBank + detection heads + CTC-GOP (`compute_gop`, `ctc_loss`, `ctc_loss_denom`) + PER/dictate + GOP-per-word JSON + LLM feedback hook. Best paper-to-code map for Phase 1.2 GOP scorer + feedback design (idea-only, do not copy — no license).

## Retention Decision
BLOCKED (no license + no model + GPU/server stack + py3.14 incompat). Keep as spec/paper reference only.

## Future Combination
Do not vendor. If Phase 1.2 needs GOP, reimplement CTC-GOP math from Cao et al. under clean license with a licensed phone recognizer; treat LLM feedback as separate key-gated feature, not core scorer.
