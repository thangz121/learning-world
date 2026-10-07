# /r/ SPECIFIC AUDIT — WP-1.9.25 Phase 4

> **Tóm tắt (VI):** 24 ca /r/ (9 LWE + 15 so762). LWE: 1 PRESENT (LOW), 5 ABSENT (HIGH), 3 chưa gán
> nhãn; max_A median 0,041; 8/9 đỉnh cô lập; 0 ca max_A ≥ 0,3; production accept 7/9. so762: max_A
> median 0,206, 12/15 đỉnh cô lập. Encoder thứ hai chỉ phủ 6 ca LWE (median 0,015). **Không kết luận
> tổng quát về /r/ từ mẫu nhỏ này.**

Source: `R_CASES.csv` (all /r/ tokens across LWE + SO762 from the WP-1.9.23 feature matrix and the
WP-1.9.24 alt-encoder artifact). No new encoder runs.

## 1. Inventory

| group | n | speakers | label status | max_A median | max_A ≥ 0.3 | max_A < 0.1 | isolated peak | window-sensitive |
|---|---:|---:|---|---:|---:|---:|---:|---:|
| LWE | 9 | 9 | 1 PRESENT (LOW), 5 ABSENT (HIGH), 3 unlabelled | 0.041 | 0 | 8 | 8 | 3 |
| SO762 (score-labelled) | 15 | 10 | 11 with score 2.0, 1 score 0.0, 3 intermediate | 0.206 | 7 | 6 | 12 | 0 (not in artifact) |
| **total** | 24 | 19 | – | – | 7 | 14 | 20 | 3 |

- SO762 /r/ speakers are children (ages 6–15 in the manifest); age is not joined into `R_CASES.csv`
  (limitation).
- Production accepts 7/9 LWE /r/ tokens despite near-zero evidence (identity credit), and the /r/
  subgroup FAR is 0.833 in the labelled pool (WP-1.9.24 confusion profile).
- Alternative-encoder coverage: only the 6 labelled LWE /r/ tokens; median alt max_A 0.015
  (child_04_four is the exception: 0.090 → 0.414 under the second encoder).
- /l/ (the other liquid) is even weaker: median max_A 0.005 (n=7 labelled).

## 2. Confidence distribution (LWE /r/ only)

| label | n | confidence |
|---|---:|---|
| PRESENT | 1 | LOW |
| ABSENT | 5 | HIGH (5) |
| unlabelled | 3 | – |

## 3. Failure rate

- LWE /r/: production recall 1.0 (7 accepted incl. the 3 unlabelled? recall counts only labelled:
  1/1 PRESENT accepted), FAR 0.833 (5/6? the labelled absent set is 5, accepted 5 → FAR 1.0 in the
  LWE subset; the 0.833 figure comes from the mixed pool with so762).
- No general /r/ conclusion is drawn: n=1 PRESENT (LOW) cannot support one.

## 4. What the audit shows (observed only)

- /r/ is the weakest-evidence final consonant class in the frozen encoder (median max_A 0.041 in LWE,
  0.135 in the mixed labelled pool; 20/24 isolated peaks).
- The alternative encoder finds strong /r/ evidence for `child_04_four` (human PROBABLY_PRESENT,
  0.09 → 0.41) and for `child_01_seven` /n/ (0.06 → 0.47), i.e. the weak-liquid pattern is at least
  partly representation-dependent.
- Nothing here can separate "encoder limitation" from "label ambiguity" for /r/ without the
  R-specific labels listed in `B2_LABEL_REQUIREMENTS.csv` (15 PRESENT + 15 ABSENT /r/ across
  speakers).
- The best external /r/ resource audited, PERCEPT-R (32.5 h, 280 speakers, rhotic/derhotic labels),
  is **non-commercial only** and ages 6–17 (see `DATASET_LICENSE_AUDIT.csv`).
