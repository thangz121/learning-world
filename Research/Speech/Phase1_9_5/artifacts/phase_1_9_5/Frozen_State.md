# Phase 1.9.5 Gate 0 — Frozen State

Date: 2026-10-02 (local)

## Actual git checkpoint on this machine

| Item | Value |
|---|---|
| Requested Phase 1.9.4 commit | `fbd50b8` |
| **Present on this checkout** | **NOT FOUND** |
| Actual HEAD at start of 1.9.5 | `1aef969` (`phase1.9.1: validate hybrid VAD with human review and robustness`) |
| Hybrid VAD code commit | `fd0c5f4` |
| Real-audio A/B baseline | `9749b92` (Phase 1.8) |

**Honest freeze:** algorithms and evidence from **Phase 1.8 + 1.9 + 1.9.1** as present under `Research/Speech/`. Phase 1.9.2–1.9.4 artifacts (`Frozen_State_Audit.md`, normalized router, etc.) are **not** in this tree. This phase does **not** invent them.

## Frozen algorithms (do not modify)

| Component | Location | Freeze rule |
|---|---|---|
| Silero thr=0.5 | `Phase1_9/HybridVAD/hybrid_vad.py` | baseline |
| hybrid_score | same | difficult continuous-energy candidate |
| energy / spectral | same | research modes only |
| HybridVAD API | `HybridVAD().run(path, mode=..., silero_thr=0.5, energy_margin_db=6.0)` | unchanged |
| Pronunciation scorer | Phase 1.2–1.6 pipeline | **do not modify**; may be env-blocked |
| CMUdict / phonemes / score formula | prior phases | unchanged |
| Unity | — | **no touch** |

## Frozen known failures / status (from 1.9.1)

- IMG_0639: Silero **0** segs; hybrid_score **28** segs; energy **75**
- NEW: Silero **38** segs
- IMG human labels: **0** filled
- Router: research-only grid; **no production lock**; 2-file overfit risk
- Decision 1.9.1: **B. HYBRID PROMISING BUT INSUFFICIENT**
- Downstream: prefer FULL or padded crop; no scorer contract change

## Hash authority (full SHA-256, not prefixes)

| Asset | Full SHA-256 |
|---|---|
| IMG_0639.mp3 (original) | `E4CDF24D2B5140B91EF637676CD5FA1B9A7F6B6A629F62AE9AC2657CB171A6F7` |
| IMG derived 16k mono wav | `9A662E8639D7C9694CFD997A221C541900BE39BEA26C3B961CFC2DF3162E5BCB` |
| NEW original mp3 | `17E3DA86267D1B7B864EA862A6C5C3B12AF4E49FD649F2FE140652A53DDF33FF` |
| NEW derived 16k mono wav | `7281C131C777F5B590BC3583B771DED300434E6D09306292D8DDEF8587CCF4EE` |

Note: prefix `9A662E86…` is the **derived wav**, not the original mp3.

## Required incoming evidence (this phase)

A. Original `IMG_0639.mp3` with full SHA match  
B. ≥2 real long recordings (≥10s each), distinct, not synthetic  
C. Human review packages prepared; labels **not** auto-filled  

## Explicit non-goals

No detector/router/scorer/Unity changes. No Gates 6–9 frozen validation runs. No fabricated labels.
