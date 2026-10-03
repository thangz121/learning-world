# 13 — DECISION (STEP 16)

**Artifacts:** `decision.json`

---

## Decision

### E = B1_REGRESSES_HUMAN_CORRECT_CHILD_SPEECH

## Evidence

| metric | frozen baseline | B1 head-only | delta |
|---|---|---|---|
| **FRR (expert-correct SO762 child phones)** | **0.2272** | **0.2988** | **+0.0716 (worse)** |
| FAR (expert-bad) | 0.2708 | 0.1042 | −0.1667 |
| expert-correct tokens | 7,728 | 7,728 | — |
| expert-bad tokens | 48 | 48 | — |
| B1 validation AUC | — | 0.7999 | — |

- B1 lowers FAR **only** by rejecting more human-correct speech. FRR-first ⇒
  regression, because accepting all would trivially zero FRR while destroying FAR
  — the reverse gaming is equally disallowed.
- B1 raises FRR on **every** age band, on every watch-list phone with adequate
  sample, and on all final-consonant classes except F.
- B1 also lowers confidence on perfect adult-TTS controls → no pronunciation- or
  child-specific evidence was learned; the head shifted the operating point.

## Why not the other decisions

- Not A/B/C: there is no improvement to be promising about; the within-domain
  metric regressed.
- Not D (no-beat): B1 does not merely fail to beat the baseline — it **harms**
  human-correct child speech (FRR up). E is the accurate, stricter label.
- Not F: the pipeline ran cleanly (480/480 scored, 0 errors, deterministic).
- Not G: materials/license were sufficient for the SO762 experiment.

## B2 gate

**B2 is NOT justified** and MUST NOT be started. `b2_justified = false`.

## Locks (verified unchanged)

`production_vad=false · router_locked=false · unity_integrated=false ·
scorer_modified=false · production_window_locked=false`.
Production files are untouched (git check in `write_reports.py`:
non-Phase1.9.18 dirty paths = `[]`).

## Honest statement

Training completed ≠ B1 works. SO762 is not LWE. A child model that improves a
score is not a solved 4-year-old Vietnamese problem. B1 produced no evidence
beyond the frozen baseline; it produced a regression.
