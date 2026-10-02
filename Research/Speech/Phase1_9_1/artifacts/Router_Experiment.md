# Router Experiment — Gate 5

Hypothesis (research only, **not production constants**):

```
if continuous_energy_ratio > C AND energy_contrast < X:
    use hybrid_score
else:
    use silero
```

## File features
| File | cont_energy | energy_contrast |
|---|---:|---:|
| IMG_0639 | 0.973 | 0.308 |
| NEW | 0.234 | 0.877 |

## Grid
C ∈ {0.7,0.75,0.8,0.85,0.9}, X ∈ {0.3,0.35,0.4,0.45,0.5} → 25 pairs

| File | frac choose hybrid |
|---|---:|
| IMG_0639 | **0.80** (20/25) |
| NEW | **0.00** (0/25) |

## Stability
- Across this grid, router **always** keeps NEW on Silero.
- IMG mostly routes to hybrid except when C very high and X very low simultaneously (stricter).

## Overfit warning
**Only 2 real files.** Separation looks perfect → high **overfit-to-file** risk.  
Do **not** lock C=0.8, X=0.4 as production.

Artifact: `Results/router_results.json`
