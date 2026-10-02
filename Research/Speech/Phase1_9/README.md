# Phase 1.9 — Hybrid VAD

## Decision
**SILERO_PLUS_HYBRID_FRONT_END**
- Default: Silero thr=0.5
- Continuous-energy files (IMG-like): `hybrid_score` (not thr lowering; not adaptive_then_silero alone)

## One-line result
Thr 0.3–0.7 cannot fix IMG (0 segs). `hybrid_score` yields 28 segs on IMG; `adaptive_then_silero` F1=0.997 on NEW but 0 on IMG.
