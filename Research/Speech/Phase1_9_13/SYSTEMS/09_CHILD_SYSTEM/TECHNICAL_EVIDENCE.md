# SIAK — Technical Evidence

## 2017 (E2)
- Unity game + network server; audio streamed; server starts analysis on first packets.
- Forced alignment via Aalto ASR (GMM-HMM); per-phoneme classification via bilingual DNN
  (target + native language); score = comparison of classified phones vs forced-aligned phones.
- **VAD on server to detect when the player stops talking.**
- Score returned <1 s; 1–5 points for proper attempts.
- Known weakness (authors): segmenter hard to tune; sometimes unrelated speech still scores
  well; planned restricted-grammar decoder "to confirm that there is real effort in the
  utterance" — i.e., an explicit fidelity/attempt plan.

## 2023 (E2, SLATE)
- Data: 24 UK native children 6–12 (2,434 enuk files locally) + 148 Finnish learners 5–12
  (17,969 utterances); single annotator, 0–100 → 0–5 stars; 1,489 items rejected
  (silence, interrupted, wrong word, spoken noise, lack of effort).
- Systems: (1) HMM-GMM segmenter + 4-layer LSTM phone classifier + SVM regressor (corr 0.59);
  (2) CTC multi-task (phone + 4 broad event classes) + phonological features + DTW + SVM
  (0.59, 0.2 s); (3) CTC + phonetically weighted Levenshtein (PWLD) + SVM 0.61 / linear 0.54.
- Speaker-disjoint splits; learning experiment showed measurable benefit.

## Child-specific handling
- YES: child speech is the core target; child + native data; age metadata; short single words.

## Relevance to LWE
- The closest published child-speech pronunciation pipeline with human scores and an explicit
  rejected/attempt class; dataset available for future child calibration with license caveats.
- Confirms our experience: authors also found unrelated speech can slip through scoring.
