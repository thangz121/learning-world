# Phase 1.9.13 — Pronunciation System Forensics (Technology Harvest)

Research-only audit of external pronunciation systems relevant to LWE.
No production changes. Flags: production_vad=false, router_locked=false, unity_integrated=false,
scorer_modified=false, production_window_locked=false.

## Structure
- `SYSTEMS/01..10` — one directory per system (profile, playtest, technical evidence, reuse)
- `TEST_CORPUS.md` — common corpus used across systems
- `SYSTEM_MATRIX.md`, `LICENSE_MATRIX.md`, `DO_NOT_REINVENT.md`, `REMAINING_GAPS.md`,
  `REUSE_CANDIDATES.md`, `TEST_MATRIX.csv` — synthesis (built as systems complete)
- `RAW_OUTPUT/`, `RUN_LOGS/`, `BENCHMARKS/` — evidence

## Evidence levels
E0 marketing · E1 documentation · E2 paper · E3 source inspection · E4 runnable ·
E5 product observed · E6 product + controlled audio

## Status
| System | Status |
|---|---|
| 01 Speech Blubs | BLOCKED (mobile-only, no web/API) — documented |
| 02 AI Speak / M-Speak | pending |
| 03 ELSA | pending |
| 04 Microsoft PA | pending |
| 05 Speechace | pending |
| 06 OpenPronounce | pending |
| 07 speak-better-than-ai | pending |
| 08 ALFreeD | pending |
| 09 additional child system | pending |
| 10 additional open research | pending |
