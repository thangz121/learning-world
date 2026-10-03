# SYSTEM 09 — SIAK ("Say It Again, Kid!") — child-specific system + dataset

STATUS: **DATASET ACQUIRED + INVENTORIED (E3/E4)**; scoring models not released (paper E2).

- Karhila et al., SLATE 2023: pronunciation scoring embedded in children's L2 English games,
  ages 6–12 (data includes younger), single-expert labels, 0–5 stars mapping.
- Dataset: HF `rkarhila/SIAK` — **downloaded and verified locally** (16,308 flac, 279 MB).
- License: **CC-BY-ND-4.0 with explicit note**: audio not for unrelated derivative works, but
  **commercial use for building/evaluating speech technology models is not prohibited**.
- Known rejected-class in training (silence, interrupted, wrong word, spoken noise, lack of
  effort) — attempt detection again.
- 2017 Interspeech paper: Unity game + server; GMM-HMM forced alignment + bilingual DNN phone
  classifier; server VAD for end-of-speech; score <1 s; authors note players can say unrelated
  words and still score well; planned restricted-grammar decoder to confirm real effort.
