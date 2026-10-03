# VoxTutor — offline benchmark results

Seeds: **12** · utterances/cell: **40** · each cell is the mean AUROC of recovering the known injected mispronunciations (1.0 = perfect, 0.5 = chance). No acoustic model, no audio downloads, no API keys.

## AUROC by scorer and regime

| scorer | ingredients | clean (control) | warped | noisy |
|---|---|--:|--:|--:|
| random | baseline | 0.491 | 0.509 | 0.508 |
| naive | fixed + raw | 1.000 | 0.620 | 0.730 |
| aligned | align + raw | 1.000 | 1.000 | 0.728 |
| normalized | fixed + gop-norm | 1.000 | 0.662 | 0.999 |
| gop | align + gop-norm | 1.000 | 1.000 | 0.999 |

## Effect 1 — forced alignment beats speaking-rate variation

On `warped`, phonemes have unequal durations, so a uniform segmentation drifts and mis-assigns frames. Fixed-segmentation scorers fall (`normalized` 1.000 clean to **0.662**), while forced alignment holds (`aligned` **1.000**, `gop` **1.000**) — DTW re-segments the frames against the canonical sequence.

## Effect 2 — GOP normalization beats channel noise

On `noisy`, a subset of phonemes is recorded at much higher variance, inflating the raw frame-to-template distance even when the phoneme is correct. Raw scorers fall (`aligned` **0.728**), while centroid / likelihood-ratio normalization holds (`normalized` **0.999**, `gop` **0.999**) — it subtracts the within-phone variance and keeps only the genuine error.

Only **gop** — forced alignment *and* GOP normalization — is robust to both distortions; each single-ingredient ablation collapses on the regime it cannot handle, and `naive` (neither) collapses on both.

## Scrambled-label null (sanity)

Shuffle the ground-truth labels and every scorer collapses to chance, so the AUROC above is not an artefact of the metric.

| scorer | null AUROC |
|---|--:|
| random | 0.504 |
| naive | 0.495 |
| aligned | 0.498 |
| normalized | 0.507 |
| gop | 0.507 |

> Reproduce: `python -m evals.harness` (writes this file), `python -m evals.gate` (asserts the dissociation).
