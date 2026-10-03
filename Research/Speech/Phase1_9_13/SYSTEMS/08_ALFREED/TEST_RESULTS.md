# Alignment-Free — Test Results

| system | test | result | evidence |
|---|---|---|---|
| VoxTutor | `python -m evals.harness` | reproduced README table (gop 1.000/1.000/0.999) | **E4** |
| GOP-AF | — | NOT_RUN (no public code) | E2 |
| WavLM-DTW / surprisal / zero-shot HuBERT | — | NOT_RUN (no public code found) | E2 |

Note: VoxTutor uses **synthetic** frames; it validates the scoring principle, not child audio.
No E6 on our corpus for this system.
