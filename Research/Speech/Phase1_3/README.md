# Phase 1.3 — Pronunciation scorer calibration & pipeline hardening

Baseline freeze: Phase 1.2 commits `bd639fd` / `45bb47c`. Phase 1.1 `f4b9dac`.

Primary path unchanged unless evidence forces a change:
Silero → Moonshine → CMUdict → w2v2-espeak → Parselmouth → scorer-v1

## Principles
- Canonical target = CMUdict (never child voice)
- SCORE ≠ CONFIDENCE
- Speechocean762 = adult L2, NOT 4yo
- No train/test speaker leakage
