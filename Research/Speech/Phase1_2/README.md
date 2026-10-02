# Phase 1.2 — Speaking Foundation / Research Stack Integration

Headless pronunciation-assessment pipeline from Phase 1.1 retained components.

## Freeze
Phase 1.1 snapshot commit: see git log `docs(speech): freeze Phase 1.1/1.1.2`.

## Run (ASUS, external models in D:\speech-lab)
```
D:\speech-lab\venvs\p0\Scripts\python Research\Speech\Phase1_2\Benchmarks\run_lwe_bench.py
D:\speech-lab\venvs\p0\Scripts\python Research\Speech\Phase1_2\Benchmarks\run_invariance.py
```

## Architecture
See `Phase1_2_Architecture.md`. Schemas: `Schemas/speaking_schemas.py`.

## Principles
- SCORE ≠ CONFIDENCE
- Canonical target = CMUdict (not child voice)
- Population labels explicit (no silent 4yo claims)
- Intermediate stages always inspectable in SpeakingResult JSON
