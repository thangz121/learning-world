# FINAL CONSONANT REPORT - WP-1.9.28 Part 18

> **Tóm tắt (VI):** Chưa có nhãn → không đánh giá được phụ âm cuối. Khi có nhãn, báo cáo theo từng
> điện thoại (ɹ s z t d k p m n ŋ f v θ ð) nếu đủ n; nhóm nhỏ chỉ báo count thô + bất định; không
> xếp hạng n=1 như nhóm lớn.

## Status

`NOT_EXECUTED` - `FINAL_CONSONANT_RESULTS.csv` is schema-only.

## Planned reporting (frozen metrics)

Per phone where sample size permits: n_present, n_absent, recall, rejection, FRR, FAR. For tiny n:
raw counts + uncertainty (Wilson interval), no ranking. Liquids (/r/, /l/) reported separately per
the /r/ gate. Isolated-peak and low-evidence subsets reported explicitly.

## Pilot phone inventory (from the frozen Pack P)

| phone | tokens |
|---|---:|
| T | 95 |
| N | 90 |
| Z | 81 |
| S | 64 |
| D | 46 |
| K | 40 |
| M | 39 |
| L | 26 |
| NG | 21 |
| V | 10 |
| R | 8 |
| F | 2 |
| CH | 1 |
| TH | 5 |
| JH | 2 |
| B | 1 |
| SH | 3 |
| DH | 1 |
| G | 5 |
| P | 6 |

Phones with n<10 (F, CH, B, JH, TH, G, P, DH, SH) may receive raw counts only.
