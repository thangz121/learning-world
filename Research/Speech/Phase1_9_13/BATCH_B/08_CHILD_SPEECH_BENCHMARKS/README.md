# BATCH B · SYSTEM 08 — Child Speech Benchmarks (NOCASA + speechocean762 + others)

STATUS: **documented (E2); datasets inspected for license/access** (NOCASA needs signed EULA;
speechocean762 is free for commercial + non-commercial download).

## NOCASA (assigned system)
- IEEE MLSP 2025 data competition: "Non-native Children's Automatic Speech Assessment".
- Children 5–12 repeating 1 of 205 Norwegian words; L1 + beginner L2 + no-exposure children.
- Human expert 1–5 star ratings (stars that should be given in the game); rating 0
  (unintelligible/noisy/silent) **excluded**.
- Final data: **7,857 train / 1,460 test**; 44 train speakers / 8 test speakers; controlled
  distribution (score, gender, age, language background); filenames anonymized.
- Baselines (GitHub `aalto-speech/nocasa-baselines`): SVM on ComParE_16 features (interpretable)
  and **multi-task wav2vec 2.0** (best UAR **36.37%**); metrics UAR (primary), ACC, MAE;
  inference speed + explainability noted.
- Access: Zenodo 14018511 after EULA filled/signed/sent (human step).

## Bonus (high relevance): speechocean762
- **English**, 5,000 utterances, 250 non-native (Mandarin L1) speakers, **half children**.
- Annotated by **5 experts** at **sentence, word, and phoneme** level (accuracy, stress,
  completeness, fluency, prosody).
- **Free for commercial AND non-commercial use**; OpenSLR download; Kaldi GOP recipe released.
- This is the most directly usable English child+L2 benchmark for LWE calibration.

## Other corpora noted by NOCASA (E2)
CMU Kids, CSLU Kids, My Science Tutor (English, native); Dutch corpus; Mandarin; German.
