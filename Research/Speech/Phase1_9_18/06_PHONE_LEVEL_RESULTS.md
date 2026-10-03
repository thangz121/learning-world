# 06 — PHONE-LEVEL RESULTS (STEP 8)

**Script:** `experiments/analyze_results.py` · **Artifact:** `phone_results.json`

Analysis joins baseline and B1 per-phone rows on the identical test tokens
(`joined_tokens = 7,853`, `missing_in_b1 = 0`) and computes per-phone FRR/FAR.
`INSUFFICIENT_SAMPLE` is flagged when n_good < 30.

---

## Watch-list phones

| phone | n_good | baseline FRR | B1 FRR | delta |
|---|---|---|---|---|
| T | 586 | 0.0495 | 0.1587 | **+0.109** |
| D | 291 | 0.1340 | 0.2440 | **+0.110** |
| N | 571 | 0.0403 | 0.1891 | **+0.149** |
| S | 429 | 0.1305 | 0.1678 | +0.037 |
| Z | 198 | 0.1869 | 0.3131 | **+0.126** |
| L | 328 | 0.0762 | 0.2165 | **+0.140** |
| R | 227 | 0.1145 | 0.2115 | **+0.097** |
| M | 277 | 0.1191 | 0.1841 | +0.065 |
| NG | 81 | 0.2099 | 0.3210 | +0.111 |
| TH | 41 | 0.6585 | 0.7317 | +0.073 |
| SH | 62 | 0.4194 | 0.4355 | +0.016 |
| CH | 23 | 0.2609 | 0.3043 | INSUFFICIENT_SAMPLE (n<30) |

## Overall pattern

B1 **raises FRR on every watch-list phone with sufficient sample**. The largest
regressions are on high-frequency alveolar phones (N, L, D, T). No phone shows a
credible FRR improvement. Frequent-phone deltas dominate the aggregate, so this
is not a small-phone artifact.

Full per-phone table (all 39 base phones) in `phone_results.json`.
