# 07 — DELETION / FINAL-CONSONANT RESULTS (STEP 9)

**Script:** `experiments/analyze_results.py` · **Artifact:** `deletion_results.json`

This matters because 1.9.14–1.9.15 identified deletion / final-consonant
behavior as the bottleneck.

---

## Explicit `<DEL>` records (SO762 tier)

| metric | frozen baseline | B1 |
|---|---|---|
| true-deletion tokens | 10 | 10 |
| rejected (sim < 0.5) | 4 | 9 |
| deletion-reject rate | 0.40 | **0.90** |

`<DEL>` is carried as its own error category and is **never converted to a
phone**. B1 rejects true deletions more often (good direction) but the sample is
tiny (n=10) and this is not a valid improvement claim on its own.

## Final-consonant classes (n ≥ 30)

| phone | n | baseline reject | B1 reject | delta |
|---|---|---|---|---|
| T | 593 | 0.0523 | 0.1653 | **+0.113** |
| N | 576 | 0.0417 | 0.1944 | **+0.153** |
| S | 434 | 0.1382 | 0.1751 | +0.037 |
| L | 333 | 0.0841 | 0.2282 | **+0.144** |
| K | 308 | 0.1201 | 0.1299 | +0.010 |
| D | 291 | 0.1340 | 0.2440 | **+0.110** |
| M | 277 | 0.1191 | 0.1841 | +0.065 |
| R | 227 | 0.1145 | 0.2115 | +0.097 |
| Z | 198 | 0.1869 | 0.3131 | +0.126 |
| B | 158 | 0.3671 | 0.3797 | +0.013 |
| F | 149 | 0.1544 | 0.1544 | 0.000 |
| V | 121 | 0.1488 | 0.2149 | +0.066 |
| P | 110 | 0.1364 | 0.1909 | +0.055 |
| G | 97 | 0.2577 | 0.2784 | +0.021 |
| NG | 81 | 0.2099 | 0.3210 | +0.111 |
| SH | 62 | 0.4194 | 0.4355 | +0.016 |
| TH | 41 | 0.6585 | 0.7317 | +0.073 |

## Verdict

On final consonants B1 **rejects more** across the board. It does not fix the
final-consonant bottleneck — it aggravates the false-rejection side of it. Only
F is unchanged. No final-consonant class shows a credible improvement.
