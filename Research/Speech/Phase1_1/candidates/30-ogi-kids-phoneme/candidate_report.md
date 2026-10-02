# Candidate 30-ogi-kids-phoneme

## Source
https://github.com/OSU-slatelab/OGI-kids-phoneme-recognition — "SpeechBrain
recipe for training a phoneme recognizer on OGI kids' speech dataset" (one-line
README). Cloned to `D:\speech-lab\src-ogikids`.

## Version
HEAD `eb41cd4` dated 2021-03-25 ("Update to latest speechbrain version").
Unmaintained since 2021. Repo = 11 source files, zero weights: `train.py`,
`train.yaml`, `detect.py`, `detect.yaml`, `generate_alignments.py`,
`ogi_prepare.py`, `time_freq_crdnn.py`, `encoder.txt`, `requirements.txt`,
`README.md`, `.gitignore`. Verified by recursive listing 2026-10-02: no
`*.ckpt/*.pt/*.pth/*.bin/*.safetensors`, no `results/`, no checkpoint URL.

## License
NO LICENSE FILE in repo (root + recursive check). GitHub default: all rights
reserved. Status: BLOCKED for code reuse until clarified (same rule as 22-ifmdd).
Architecture facts below are reported, not copied, for that reason.

## Model License
NOT_AVAILABLE — no checkpoint exists in the repo or linked from it, so there is
nothing to license. The recipe's TRAINING target is the OGI/CSLU Kids corpus
(LDC2007S18, signed non-commercial research-only agreement + LDC fee — see
candidate 31), which we have NOT downloaded and do not hold. Any future weights
would inherit that data restriction.

## Dependency License
`requirements.txt` = `g2p_en` + `speechbrain`. In venv
`D:\speech-lab\venvs\p0`: speechbrain 1.1.1 present (matches candidate 12),
`hyperpyyaml` present, `g2p_en` NOT installed (`ModuleNotFoundError`, verified)
— data-prep path (`ogi_prepare.py` imports `G2p`) is unrunnable as-is.
SpeechBrain itself is Apache-2.0; `g2p_en`/CMUdict chain unaudited here since
never installed.

## Intended Role
Phoneme recognition (child-tuned CTC phoneme recognizer; training recipe +
word-detection extension; offline/research).

## Installation
1. `git clone https://github.com/OSU-slatelab/OGI-kids-phoneme-recognition
   D:\speech-lab\src-ogikids` — OK (HEAD eb41cd4, 2021-03-25).
2. Architecture audit (read, not run-as-training): `TimeFreqCRDNN`
   (time+frequency CNN blocks → GRU → DNN, file `time_freq_crdnn.py`); hyperparams
   in `train.yaml`; 39-phoneme inventory in `encoder.txt`; data layout in
   `ogi_prepare.py`; word-detection head in `detect.yaml`/`detect.py`; alignment
   exporter in `generate_alignments.py`. Details under Results-relevant fields below.
3. Import probe: `time_freq_crdnn.TimeFreqCRDNN` imports cleanly under installed
   speechbrain 1.1.1 (signature verified). `g2p_en` import fails — recorded.

## Runtime
ASUS Windows, CPU-only, venv `D:\speech-lab\venvs\p0` (Python 3.14.7,
torch 2.14.1+cpu, speechbrain 1.1.1). Forward smoke test CPU time not
benchmarked (random-init check only, single pass each file).

## Tests
Same-audio probes (NOT recognition evaluation — random weights, see Results):
- `sapi_cat.wav` (1.26 s, 20151 samples @16k): Fbank(40 mels) → 126 frames×40
  → TimeFreqCRDNN(cnn [64,128], GRU 256 bidir, DNN 256) → 32×256 → linear
  256→40 → logits 32×40. Greedy CTC (blank 0) → ids [5,30,36,17,30]
  (= N,P,CH,TH,P) — nonsense for "cat", as expected with untrained weights.
- `pregen_apple_normal.wav` (0.84 s): feats 85×40 → logits 22×40 → greedy ids
  [30,5,30] (= P,N,P) — nonsense for "apple", as expected.
- torchaudio load path failed in this env (torchcodec native backend broken);
  `soundfile` load path used instead — fixture reading is fine, loader gap noted.
Script kept at `C:\Users\ASUS\AppData\Local\Temp\opencode\ogi_probe.py` (outside
git). No training attempted (restricted data absent by policy).

## Results
Architecture: VGG-style TimeFreqCRDNN — `Fbank(n_mels=40, n_fft=400)` @16kHz →
2 CNN blocks ([64,128], time-conv + freq-conv summed, stride-2 downsample,
LayerNorm+LeakyReLU+Dropout2d) → GRU (defaults: 4 layers, 256 units,
bidirectional, dropout 0.2) → 2×DNN(256) → Linear 256→40. Output classes = 40
(blank 0 + 39 phonemes). Training: CTC loss (+ optional energy-based alignment
loss, `alignment_weight: 0.0` default), 50 epochs, batch 16, Adam 1e-4, NewBob
annealing, TimeDomainSpecAugment speeds [95,100,105]. Data grades 00–10 split by
speaker (3% valid / 3% test per grade), verify==3 utterances excluded, 16 kHz.
Inventory (`encoder.txt`): 39 stress-stripped CMU-style phonemes
(S,EH,V,AH,N,Y,L,OW,G,R,AW,W,UH,T,ZH,ER,TH,IY,IH,NG,SH,EY,D,Z,JH,F,AO,AA,K,P,AE,
AY,UW,M,HH,CH,DH,OY,B) + `<blank>` — note: NO stress digits, unlike 29's 47-phone
set and unlike our V1 ARPAbet table (kept WITH stress). G2P via `g2p_en`
(`words2phonemes` strips `012`).
Extensions (audited, not run): `detect.yaml`/`detect.py` = attentional
word-existence detector over frozen encoder (phoneme embedding 100 + BiLSTM +
location-aware GRU decoder, 2 s segments / 1 s hop); `generate_alignments.py` =
CTC-greedy word-boundary exporter (0.04 s frame step, edit-distance map).
Measured recognition output on our corpus: NOT_AVAILABLE (no trained checkpoint
exists; random-init decodes above are anti-evidence, proving exactly that).

## Baseline Comparison
Same files as all STT candidates (`sapi_cat.wav`, `pregen_apple_normal.wav`,
16k mono). Baselines on overlapping audio: 13-espnet 6/6 PERFECT (0.35 s/file),
01-whisper 6/6, 17-moonshine 6/6 PERFECT (0.03 s). This candidate yields no real
hypothesis (garbage ids above), so no WER/PER/error-localization comparison is
possible. Fair statement: word-level peers return words; this recipe promises
phonemes but ships no usable recognizer.

## Improvements
None measured (no trained model to compare).

## Problems
1. No license → code reuse BLOCKED.
2. No checkpoint + no inference script for arbitrary wav (`train.py` trains,
   `detect.py` needs alignments, `generate_alignments.py` needs a trained model)
   → inference on our two probe files is impossible in any honest sense; the
   greedy-id outputs above are random-weight artifacts, recorded as such.
3. Training requires the restricted OGI corpus (LDC2007S18) + `g2p_en` (missing)
   → not trainable in this lab under current policy/access.
4. 2021-vintage recipe vs speechbrain 1.1.1 (import works today; full
   train-loop compatibility UNVERIFIED — `sb.decoders.ctc_greedy_decode`,
   `NewBobScheduler`, `Pretrainer` APIs may have drifted).
5. Inventory mismatch risk: 39 stress-stripped phones vs our V1 ARPAbet WITH
   stress — mapping cost if ever adopted.

## Unique Capability
Only kid-trained phoneme-CTC recipe in scope (OGI grades K–10, scripted
prompted words/sentences/digits), with two rare extensions: energy-based
alignment loss + attentional word-existence detector + word-boundary exporter.
Conceptually the closest thing to a child Де phoneme assessor — but concept
without weights/license is not evidence. Child-data principle stands: OGI data
would be VALIDATION only, never correctness ground truth.

## Retention Decision
BLOCKED (code license BLOCKED — no license file; data path RESTRICTED —
LDC2007S18 signed agreement + fee; weights NOT_AVAILABLE). The forward-pass
smoke test passes but proves nothing about recognition.

## Future Combination
Revisit ONLY if: (a) upstream adds a license + publishes a checkpoint (then
re-run the SAME two probe files honestly and score PER vs 13/20); or (b) lab
obtains OGI access (see 31) AND license clears, then train per recipe and treat
output as validation signal beside scorer 11/19. Until then: design-reference
only (TimeFreqCRDNN dims, 40-mel/CTC setup, alignment-loss idea) — no code
copy. Never production; torch-only (no ONNX path shown).
