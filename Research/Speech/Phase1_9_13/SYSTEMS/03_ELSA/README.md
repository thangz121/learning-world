# SYSTEM 03 — ELSA Speak / ELSA API

STATUS: **BLOCKED** at real playtest/API (partner-gated token; app/browser requires account+mic;
no file-upload path for external audio). Rich public technical evidence (E2) exists.

Key E2 sources:
- Anguera & Van, Interspeech 2016 (demo): client-server, Kaldi + custom DNN, streaming audio,
  server-side endpointing, phoneme-level feedback, L1-specific acoustic models.
- Anguera et al., SLATE 2023: Speech Analyzer — end-to-end ASR (fine-tuned on 100h non-native),
  speaker-ID filtering, pronunciation (phonetic deviation), intonation, fluency, grammar,
  vocabulary; weighted overall score.
- API docs: partner-gated (Authorization: ELSA <token>); batch + WebSocket scoring.
