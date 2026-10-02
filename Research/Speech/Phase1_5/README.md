# Phase 1.5 — Calibration at scale, conflict resolution, real-child validation

Frozen baseline: Phase 1.4 commit `33c6cbd`.

## Principles
- No ASR→score boost
- No child-as-canonical-truth
- SCORE ≠ CONFIDENCE
- Reject variance collapse calibrators
- Real child = validation only

## Run
```
D:\speech-lab\venvs\p0\Scripts\python Research\Speech\Phase1_5\Calibration\run_scale_cal.py
D:\speech-lab\venvs\p0\Scripts\python Research\Speech\Phase1_5\Conflicts\run_conflicts.py
D:\speech-lab\venvs\p0\Scripts\python Research\Speech\Phase1_5\LWE\run_lwe_retest.py
```
