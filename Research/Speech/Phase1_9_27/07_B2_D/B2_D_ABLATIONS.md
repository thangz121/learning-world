# B2-D ABLATIONS (FROZEN) — WP-1.9.27 Part 16

> **Tóm tắt (VI):** Các biến thể B2-D khóa trước kết quả: baseline production (mean), max trong
> span (control), rank-restricted, encoder thay thế (max/mean), kết hợp chéo (prod span + alt
> evidence). Chỉ chạy các biến thể có ý nghĩa thống kê với 546 token; nếu cắt bớt phải có lý do.

| id | evidence source | aggregation | acceptance for analysis | purpose |
|---|---|---|---|---|
| D0 | production encoder | span mean | production baseline | frozen baseline |
| D1 | production encoder | span max | research gate sweep | max control (not a solution) |
| D2 | production encoder | span mean + rank | rank-restricted | identity contribution |
| D3 | alt encoder | span max | same sweep as D1 | alternative representation |
| D4 | alt encoder | span mean | same sweep as D0 | alt baseline |
| D5 | both encoders | OR / AND of strong evidence | research gate | representation complementarity |
| D6 | both encoders | mean + rank + blank/margin | research gate | combination control |

Coverage rule: with 546 pilot tokens (99 test-split tokens), only D0, D1, D3, D4, D5 are
pre-registered as primary; D2/D6 are secondary and must be reported as exploratory if the labelled
test subset is < 60 tokens. No per-phone threshold fitting in B2-D.

No variant may be tuned on the test split; dev is for inspection only; final readout is
speaker-disjoint test with human labels.
