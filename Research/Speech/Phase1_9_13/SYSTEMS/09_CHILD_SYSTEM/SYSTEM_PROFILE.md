# SIAK — System Profile

| Field | Value | Evidence |
|---|---|---|
| Project | Say It Again, Kid! (Aalto + Univ. Helsinki, 2014–2018) | E2 |
| Papers | Interspeech 2017 (game); SLATE 2023 (scoring + learning experiment) | E2 |
| Target | children learning L2 English (6–12 in SLATE; data has ages 4–12) | E2/E4 |
| Game | Unity (Windows/Android), network server for scoring | E2 |
| Pipeline (2017) | Aalto ASR GMM-HMM forced alignment + bilingual DNN phone classifier + VAD | E2 |
| Pipeline (2023) | HMM-GMM+LSTM+SVM → CTC+articulatory-DTW+SVM → CTC+PWLD (0.59/0.59/0.61 corr) | E2 |
| Labels | single expert, 0–100 → 0–5 stars | E2 |
| Rejected class | silence, interrupted, wrong word, spoken noise, lack of effort | E2 |
| Dataset | HF rkarhila/SIAK — 16,308 flac verified locally | E4 |
| Dataset license | CC-BY-ND-4.0 (+commercial model use not prohibited) | E3 |
| Scoring code | NOT released (games stripped of speech components) | E2/E3 |
| Runtime | ~0.2 s per utterance (CTC+PWLD gen, 2023) | E2 |

## Local dataset inventory (verified)
- Files: 16,308 flac (train 12,308 / test 4,000); 279 MB; CSV labels (file, utterance, score).
- Native language: fifi 13,161; enuk 2,434; othr 713.
- Ages: 4:44, 5:420, 6:130, 7:1,198, 8:3,973, 9:6,360, 10:2,647, 11:1,284, 12:252
  → **age 4–6 = 594 utterances** (target-adjacent for LWE).
- 320 distinct target words; score 0–100 distribution in CSV.
