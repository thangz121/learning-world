# Alignment-Free — Reuse Analysis

| Item | Type | Status | Reason |
|---|---|---|---|
| GOP-AF (no committed segmentation; deletion-aware) | ALGORITHM | ADAPT (research) | Published fix for our 1.9.12 CTC-span contamination |
| VoxTutor harness (2×2 dissociation) | DIRECT_CODE (MIT) | DIRECT_REUSE for research validation | numpy-only; runs offline |
| Forced alignment + GOP normalization pairing | ALGORITHM | REFERENCE_ONLY | Confirms both needed; we have alignment but no GOP norm |
| Discrete-token surprisal | ALGORITHM | REFERENCE_ONLY | Zero-resource alternative |
| WavLM-DTW templates | ALGORITHM | REFERENCE_ONLY | Text-free; ~5 templates enough |
| GOP-AF code | — | NOT_AVAILABLE | No public implementation found |

REUSE_COST: LOW (VoxTutor) / MEDIUM (implementing GOP-AF from paper)
EXPECTED_VALUE: HIGH (directly targets our measured failure mechanism)
