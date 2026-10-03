# Alignment-Free — Playtest

## VoxTutor (RAN, E4)
Command (from repo root, isolated venv): `python -m evals.harness`
Output (reproduced the README table exactly):

| scorer | clean | warped | noisy |
|---|---:|---:|---:|
| random | 0.491 | 0.509 | 0.508 |
| naive (fixed+raw) | 1.000 | 0.620 | 0.730 |
| aligned (DTW+raw) | 1.000 | 1.000 | 0.728 |
| normalized (fixed+gop) | 1.000 | 0.662 | 0.999 |
| **gop (DTW+gop)** | **1.000** | **1.000** | **0.999** |

Interpretation (their design, reproduced): speaking-rate variation breaks naive segmentation;
channel noise breaks raw distance; forced alignment + GOP normalization together are robust.

## GOP-AF (NOT RUN)
No public implementation found. Paper-level (E2): alignment-free GOP over CTC models,
explicit insertion/deletion handling, evaluated on CMU Kids + SpeechOcean762.
