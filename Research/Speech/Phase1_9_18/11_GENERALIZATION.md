# 11 — OVERFITTING / GENERALIZATION (STEP 13, 14)

**Script:** `experiments/analyze_results.py` · **Artifacts:**
`generalization.json`, `age_l1_matrix.json`

---

## Age / L1 transfer matrix (STEP 13)

| population | L1 | age | baseline FRR | B1 FRR |
|---|---|---|---|---|
| SO762 children (within-domain) | Mandarin | 6–15 | 0.2272 | **0.2988** |
| LWE children (cross-L1/cross-age) | Vietnamese | ~4 | BLOCKED (raw audio gitignored) | NOT_COMPUTABLE |
| SIAK | Finnish/mixed | 7–12 | ND blocked | NOT_COMPUTABLE |

B1 **worsens** the only domain where it can be measured. No cross-domain claim
is possible.

## Per-age (SO762 test, expert-correct phones)

| age | n | baseline FRR | B1 FRR |
|---|---|---|---|
| 6 | 2,539 | 0.2867 | 0.3824 |
| 7 | 1,427 | 0.2740 | 0.3497 |
| 9 | 633 | 0.1943 | 0.2354 |
| 10 | 361 | 0.1607 | 0.2271 |
| 11 | 345 | 0.1652 | 0.2319 |
| 12 | 726 | 0.2080 | 0.2755 |
| 13 | 448 | 0.1629 | 0.2098 |
| 15 | 1,249 | 0.1401 | 0.1873 |

B1 raises FRR in **every** age band. No age-specific benefit.

## Per-speaker

All 24 test speakers are reported in `generalization.json`; no catastrophic
collapse is averaged away. The B1 regression is broad, not driven by one speaker.

## Overfitting reading

- Train loss → ~0.019 (converged), validation AUC 0.7999: the head fits the
  majority class and shifts the operating point.
- The head adds no new acoustic evidence — it reweights frozen-encoder posteriors.
- Conclusion: B1 does **not** memorize speakers (it is speaker-disjoint), but it
  also does not generalize — it **systematically shifts toward rejection**, which
  harms human-correct speech. Child-phone generalization is **not** demonstrated.
