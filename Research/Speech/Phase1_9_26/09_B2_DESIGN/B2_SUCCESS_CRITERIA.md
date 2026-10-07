# B2 SUCCESS CRITERIA (FROZEN BEFORE TRAINING) — WP-1.9.26 Part 19

> **Tóm tắt (VI):** Tiêu chí thành công khóa trước khi huấn luyện: FRR-first (recall ≥ production,
> FRR không tăng), FAR, AUC, final-consonant recall/FP, /r/, speaker-disjoint, child-age, safety,
> robustness, calibration, failure-case replay. Định nghĩa SUCCESS / PARTIAL / FAILURE /
> INCONCLUSIVE. Không đổi tiêu chí sau khi thấy kết quả.

## Criteria (evaluated on a speaker-disjoint test set with HUMAN-LISTENING labels)

1. **FRR-first**: present recall ≥ production baseline recall − 0 (no FRR increase); FRR reported.
2. **FAR**: false PRESENT rate ≤ production FAR; no new strong false accepts.
3. **AUC**: target-evidence AUC ≥ production (where meaningful).
4. **Final-consonant recall**: ≥ production recall per phone class (liquids reported separately).
5. **Final-consonant false acceptance**: not increased.
6. **/r/**: /r/ recall ≥ production; /r/ false accepts not increased (n ≥ 30 required).
7. **Speaker-disjoint generalization**: dev→test drop ≤ 5 points.
8. **Child-age generalization**: performance stable across age bands (4–6 vs 7–9).
9. **Assessability safety**: no increase in confidently-wrong decisions on NOT_ASSESSABLE audio.
10. **Robustness**: no degradation > 5 points between mic conditions (where available).
11. **Calibration**: reliability curve within ±0.1 of the diagonal for the decision band.
12. **Failure-case replay**: the 4 TYPE-B cases and the weak-present set explicitly reported.

## Outcome definitions

- **SUCCESS** — criteria 1–3 and 6 pass, and ≥6 of the remaining pass.
- **PARTIAL SUCCESS** — criterion 1 passes, criteria 2–6 pass at least partially, ≥3 others pass.
- **FAILURE** — criterion 1 fails (FRR-first violation) or criteria 2–6 mostly fail.
- **INCONCLUSIVE** — the test set is too small (<30 per side per phone class) or labels are not
  trustworthy (single reviewer, kappa unavailable).

## Gate rule

No B2 training starts until (a) the minimum label set exists with two reviewers or an explicit
single-reviewer limitation, and (b) the data/license route for the intended scope is signed or the
pilot uses local-only data. Criteria are frozen in this file before any training run.
