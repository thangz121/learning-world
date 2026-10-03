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
| 01 Speech Blubs | BLOCKED (mobile-only) — E1/E5 documented |
| 02 AI Speak / M-Speak | BLOCKED (app-only) — E0/E1 documented |
| 03 ELSA | BLOCKED (partner token) — E2 architecture documented |
| 04 Microsoft PA | BLOCKED (Azure key) — E1 full schema captured |
| 05 Speechace | BLOCKED (key by request) — E1 fidelity taxonomy captured |
| 06 OpenPronounce | **RAN LOCALLY (E4/E6)** — 16 cases, raw JSON archived |
| 07 speak-better-than-ai | SOURCE AUDIT (E3, MIT) |
| 08 ALFreeD | NOT FOUND; alignment-free line + VoxTutor RAN (E4) |
| 09 SIAK child system | DATASET ACQUIRED (E4, 16,308 utt) |
| 10 slip (GOP-CTC) | SOURCE AUDIT (E3, no license) |
