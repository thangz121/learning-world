# OpenPronounce — Source Audit (E3)

| FILE | FUNCTION | LICENSE | PURPOSE | INPUT | OUTPUT | REUSE_STATUS |
|---|---|---|---|---|---|---|
| openpronounce/speech.py | `compare_audio_with_text` | MIT | full pipeline | waveform + text | dict score/differences/prosody | ADAPT |
| openpronounce/speech.py | `compute_pronunciation_score` | MIT | weighted score | acoustic/phoneme/word errors | 0–100 | REFERENCE_ONLY (LWE has own scorer) |
| openpronounce/speech.py | `align_sequences_dtw` / `extract_embeddings` | MIT | DTW vs reference | embeddings | distance/path | ADAPT (alignment-free idea) |
| openpronounce/speech.py | `extract_f0` / `extract_energy` | MIT | prosody | waveform | contours | DIRECT_REUSE possible |
| openpronounce/phones.py | `recognize_phones` / `compare_phones` | MIT | CTC phone recognition + comparison + leniency pairs | waveform + text | phones, PER, errors | ADAPT (child research) |
| openpronounce/audio.py | `load` / `text2speech` | MIT | IO + gTTS reference | path/text | waveform/path | text2speech → replace locally |
| openpronounce/tts.py | gTTS wrapper | MIT | reference audio | text | mp3 | BLOCKED at runtime (rate limit) |
| openpronounce/cli.py | CLI | MIT | command line | args | console/JSON | REFERENCE_ONLY |
| openpronounce/languages.py | language config | MIT | per-lang models | code | config | REFERENCE_ONLY |

Dependencies licenses: torch (BSD-3), transformers (Apache-2.0), librosa (ISC), phonemizer
(GPL-3.0 — note!), gTTS (MIT), fastdtw (MIT), Levenshtein (GPL-2.0 — note!), scikit-learn (BSD).
**License caution:** `phonemizer` is GPL-3.0 and `Levenshtein` is GPL-2.0; commercial bundling
needs legal review even though OpenPronounce itself is MIT.
Model licenses: wav2vec2 models on HF — verify model card (commonly Apache-2.0); NOT assumed.
