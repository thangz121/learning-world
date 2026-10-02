# Candidate: 32-wav2vec2-phoneme

## Source
- HF: facebook/wav2vec2-xlsr-53-espeak-cv-ft (phoneme CTC, en-us, espeak backend)
- HF: jonatasgrosman/wav2vec2-large-xlsr-53-english (grapheme/orthographic CTC control — NOT phoneme)
- Code: transformers Wav2Vec2ForCTC + Wav2Vec2FeatureExtractor / Wav2Vec2Processor, torch CPU greedy CTC (argmax, no LM)

## Version
- Date tested: 2026-10-02
- transformers 5.17.0, torch 2.14.1+cpu, phonemizer 3.4.0, soundfile 0.14.0, numpy 2.5.3, Python 3.14.7
- facebook/wav2vec2-xlsr-53-espeak-cv-ft snapshot SHA: 2c733782da5604684829819a5eb744c193fe9398 (HF sha reported by model_info)
- jonatasgrosman/wav2vec2-large-xlsr-53-english snapshot SHA: 569a6236e92bd5f7652a0420bfe9bb94c5664080
- Local snapshots under D:\speech-lab\models\models--facebook--wav2vec2-xlsr-53-espeak-cv-ft\snapshots\ and models--jonatasgrosman--wav2vec2-large-xlsr-53-english\snapshots\

## License
- transformers: Apache 2.0. torch CPU: Apache-2.0 AND BSD/MIT/BSL mix (pip show License-Expression).
- phonemizer 3.4.0: GPL-3.0 (pip metadata ships full GPL text).

## Model License
- facebook/wav2vec2-xlsr-53-espeak-cv-ft: apache-2.0 (HF cardData license field, verified via huggingface_hub model_info 2026-10-02)
- jonatasgrosman/wav2vec2-large-xlsr-53-english: apache-2.0 (HF cardData license field, same verification)
- Status: CLEAR (both apache-2.0), but espeak binary dependency has its own licensing/availability issue on Windows (see Problems).

## Dependency License
- huggingface_hub cache under D:\speech-lab\models (symlink warning on Windows without Developer Mode — degraded copy, still works).
- espeak system binary: NOT INSTALLED on this Windows machine — this is the blocker for the standard tokenizer path, not a Python license issue.

## Intended Role
Phoneme (CTC phoneme recognizer for pronunciation scoring). jonatas model is STT-grapheme control only.

## Installation
Venv (CPU-only, pre-existing): D:\speech-lab\venvs\p0\Scripts\python
No new pip installs needed for Part A (torch/transformers/soundfile already present).
Cache env used:
```
$env:HF_HOME='D:\speech-lab\models'
$env:HF_HUB_CACHE='D:\speech-lab\models'
```
Standard path (BLOCKED for espeak model):
```python
from transformers import Wav2Vec2Processor, Wav2Vec2ForCTC
processor = Wav2Vec2Processor.from_pretrained("facebook/wav2vec2-xlsr-53-espeak-cv-ft", cache_dir=CACHE)
```
Workaround path (WORKS — bypasses phonemizer):
```python
from transformers import Wav2Vec2FeatureExtractor, Wav2Vec2ForCTC
fe = Wav2Vec2FeatureExtractor.from_pretrained(MID, cache_dir=CACHE)
model = Wav2Vec2ForCTC.from_pretrained(MID, cache_dir=CACHE)
# vocab.json loaded manually (392 entries), greedy: softmax(logits).max(-1), collapse blank=0 dedup
```
Control path (WORKS):
```python
processor = Wav2Vec2Processor.from_pretrained("jonatasgrosman/wav2vec2-large-xlsr-53-english", cache_dir=CACHE)
model = Wav2Vec2ForCTC.from_pretrained(same)
```

## Runtime
- OS: Microsoft Windows 11 Pro 10.0.26200, CPU AMD Ryzen 5 150 (6C/12T), RAM 16 GB (16369967104 bytes), torch threads 6, CPU-only, no GPU.
- espeak-model greedy infer per file (FE+model, CPU): sapi_red ~0.42 s, sapi_cat ~0.27 s, pregen_apple_normal ~0.20 s (short utterances; not a formal benchmark).
- Model sizes: espeak-cv-ft vocab 392, hidden 1024 (XLSR-53 large); jonatas vocab 33 (XLSR large, 315M params per HF safetensors metadata).

## Tests
Corpus (16 kHz mono PCM_16, verified via soundfile):
- sapi_red.wav — 1.219 s (19511 samples)
- sapi_cat.wav — 1.259 s (20151 samples)
- pregen_apple_normal.wav — 0.840 s (13440 samples)
Decode: greedy CTC argmax, confidence per-frame = max softmax prob. Mean_all = mean over all frames, mean_nb = mean over non-blank frames (blank=<pad> id 0), frac_nb = fraction non-blank. Token run-max = max prob inside each collapsed token run.

## Results
### A1. facebook/wav2vec2-xlsr-53-espeak-cv-ft — standard transformers path: BLOCKED
`Wav2Vec2Processor.from_pretrained(...)` raises:
```
RuntimeError: espeak not installed on your system
  at transformers/models/wav2vec2_phoneme/tokenization_wav2vec2_phoneme.py:136 init_backend
  -> phonemizer/backend/espeak/base.py:77
```
FeatureExtractor and model weights load fine; only `Wav2Vec2PhonemeCTCTokenizer` init needs the espeak system binary (tokenizer_config: phonemizer_lang en-us, backend espeak, word_delimiter "|"). No phoneme string via standard path on this Windows box.

### A2. Same model — manual vocab workaround: RAN (true phoneme output)
vocab_size 392, blank <pad> id 0. Key ids verified: 27=ɹ (U+0279), 14=ɛ (U+025B), 12=d, 11=k, 6=t, 73=aː (a U+0061 + ː U+02D0), 18=p, 19=o.

| file | T_frames | greedy phonemes (ids) | mean_all | mean_nonblank | frac_nb | min / max | per-frame max-softmax first10 | token run-max conf (run_len) |
|---|---|---|---|---|---|---|---|---|
| sapi_red.wav | 60 | ɹ ɛ d ([27,14,12]) | 0.9891 | 0.9107 | 0.0500 | 0.6266 / 0.9999 | [0.9998,0.9997,0.9997,0.9998,0.9991,0.9997,0.9989,0.9071,0.9995,0.9996] | ɹ 0.9071 (1), ɛ 0.8685 (1), d 0.9565 (1) |
| sapi_cat.wav | 62 | k ɛ t ([11,14,6]) | 0.9883 | 0.7773 | 0.0484 | 0.3874 / 0.9999 | [0.9998,0.9998,0.9997,0.9999,0.9999,0.9998,0.9748,0.9975,0.9963,0.9876] | k 0.9748 (1), ɛ 0.3874 (1), t 0.9697 (1) |
| pregen_apple_normal.wav | 41 | aː p o ([73,18,19]) | 0.9394 | 0.5960 | 0.0976 | 0.2841 / 0.9999 | [0.9998,0.9996,0.9996,0.9801,0.9252,0.9968,0.4310,0.9982,0.9993,0.9981] | aː 0.4310 (1), p 0.9279 (2), o 0.2841 (1) |

Notes (measured, not interpreted away): `sapi_cat` ɛ frame conf is low (0.3874); `pregen_apple` aː (0.4310) and o (0.2841) are low — model is uncertain here. `apple` decodes as 3 phoneme tokens `aː p o`, not a full æ-p-ə-l sequence; recorded as-is. Most frames are blank (frac_nb 0.05–0.10), expected for CTC on short isolated words.

### A3. jonatasgrosman/wav2vec2-large-xlsr-53-english — RAN but NOT phoneme
vocab_size 33, blank <pad> id 0. id map: 0 <pad>, 1 <s>, 2 </s>, 3 <unk>, 4 | (word delimiter), 5 ', 6 -, 7–32 a–z. Orthographic English, no IPA.

| file | T_frames | greedy text (processor.decode) | mean_all | mean_nonblank | frac_nb | min / max | first10 max-softmax |
|---|---|---|---|---|---|---|---|
| sapi_red.wav | 60 | red | 0.9918 | 0.9992 | 0.0833 | 0.5387 / 1.0000 | [1.0000,1.0000,1.0000,0.9998,0.9990,0.9998,0.9927,1.0000,0.9986,1.0000] |
| sapi_cat.wav | 62 | cat | 0.9975 | 0.9999 | 0.0645 | 0.9385 / 1.0000 | [0.9942,0.9833,0.9757,0.9385,0.9737,0.9997,1.0000,1.0000,1.0000,1.0000] |
| pregen_apple_normal.wav | 41 | apple | 0.9797 | 0.9164 | 0.1707 | 0.6354 / 1.0000 | [0.9809,0.9665,0.9516,0.9633,0.9299,0.9873,0.9855,0.9936,1.0000,0.9996] |

CTC nuance (verified): apple collapsed ids are [7,22,22,18,11,4] = a p p l e | — the double-p is two `p` runs separated by blank. Naive dedup without blank handling yields misleading "aple"; `processor.decode` correctly returns "apple". Do not use the naive collapsed string for this model.

## Baseline Comparison
Same audio for all runs above. Phoneme model (A2) vs grapheme control (A3): both recover the expected word identity on all 3 files (ɹɛd/kɛt/aːpo vs red/cat/apple), but only A2 yields phoneme-level tokens + per-phoneme confidence usable for pronunciation scoring. A3 gives no vowel/consonant IPA and cannot serve as phoneme evidence.

## Improvements
- Phoneme-level CTC output on CPU without espeak install via FE + manual vocab.json mapping (measured above); per-frame and per-token confidences available for GOP-style scoring experiments.
- Grapheme control confirms corpus is clean/decodable (all 3 words correct with mean_all ≥ 0.97).

## Problems
- Standard `Wav2Vec2Processor` path for the espeak phoneme model is BLOCKED on Windows by missing espeak binary (exact RuntimeError above). Workaround bypasses pretty-printing/normalization the tokenizer would do — phoneme strings are raw vocab ids, no espeak G2P validation.
- Low token confidences observed (cat-ɛ 0.3874, apple-aː 0.4310, apple-o 0.2841) — must not be hidden; scorer must handle uncertainty, not threshold it away.
- jonatas model is frequently mistaken for a phoneme model — it is NOT (a-z + | only).

## Unique Capability
Only candidate so far producing direct IPA phoneme tokens (ɹ, ɛ, k, t, d, aː, p, o) with per-frame/per-token CTC confidence on CPU via transformers, no GPU, no external server.

## Retention Decision
RETAIN (license status CLEAR — both models apache-2.0) as phoneme-CTC reference, with the espeak-binary BLOCKED noted for the standard load path. Future work must either install espeak (Windows build + license check) or keep the manual-vocab path with explicit documentation.

## Future Combination
Phoneme CTC (this candidate) for frame/phone posteriors + Parselmouth (candidate 33) formant/F0 acoustic evidence + VAD (cand. 06/07) front-end; scorer in Phase 1.2 compares CTC phone conf vs formant targets on the same audio.
