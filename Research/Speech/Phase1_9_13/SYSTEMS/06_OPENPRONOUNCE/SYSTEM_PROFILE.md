# OpenPronounce — System Profile

| Field | Value | Evidence |
|---|---|---|
| Repo | github.com/Halleck45/OpenPronounce (also PyPI `openpronounce` 0.3.0) | E3 |
| License | MIT | E3 |
| Python | >=3.10; ran on 3.14 (torch 2.14.1+cpu) | E4 |
| Models | embeddings: facebook/wav2vec2-large-960h; phones: facebook/wav2vec2-lv-60-espeak-cv-ft | E3 |
| Deps | torch, transformers, librosa, phonemizer(+espeak-ng), gTTS, fastdtw, Levenshtein, scikit-learn | E3 |
| Languages | English default; FR/ES/DE/IT/PT/NL experimental | E1/E3 |
| Output | score 0–100, transcription, per-word errors (expected vs actual IPA), phoneme error rate, prosody f0/energy | E3/E4 |
| Reference | TTS of target text (gTTS by default) | E3 |
| Score weights | acoustic 0.3 / phonemes 0.4 / words 0.3 | E3 |
| Confusion leniency | built-in pairs e.g. tense/lax, ɔ/ɑ, ɾ/t | E3 |
| Runtime | warm 1.3–1.8 s/case CPU; cold ~36 s (model load) | E4 |
| Offline | yes after model download (except gTTS reference; replaced locally here) | E4 |
| Child-specific | none documented; adult-trained Wav2Vec2 | E3 |
