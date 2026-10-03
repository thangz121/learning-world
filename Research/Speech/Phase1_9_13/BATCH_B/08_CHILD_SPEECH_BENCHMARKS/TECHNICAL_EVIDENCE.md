# Child Speech Benchmarks — Technical Evidence (E2)

## NOCASA baselines (arXiv 2504.20678)
- SVM + ComParE_16 (6,373 OpenSMILE features), one-vs-rest with class weighting — interpretable
  via SVM weights.
- Multi-task wav2vec 2.0 — best: UAR 36.37% on test; ASR WER 10.63% / CER 4.10% on utterances
  rated 4–5.
- Conclusion: "leaving plenty of room for improvement"; latency and explainability are explicit
  considerations.

## speechocean762 (Interspeech 2021)
- 5,000 English utterances; 250 Mandarin-L1 speakers (half children); 20 sentences per speaker;
  mobile-phone recordings in a quiet room.
- 5 experts per utterance; phoneme-level accuracy; word accuracy+stress; sentence
  accuracy/completeness/fluency/prosody; the paper notes word accuracy ≠ simple mean of phone
  accuracy (e.g., /b/→/k/ is worse than /2/→/A/).
- Kaldi GOP recipe released; corpus free for commercial + non-commercial use.

## Relevance to LWE
- Gives LWE a legitimate **English** child+L2 calibration benchmark with human phoneme labels.
- NOCASA supplies the evaluation methodology (UAR, class imbalance, zero-rating removal) and the
  sobering baseline ceiling for child APA.
