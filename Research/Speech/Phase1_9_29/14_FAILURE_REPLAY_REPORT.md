# 14 - FAILURE REPLAY REPORT (WP-1.9.29 Part 14)

**NOT EXECUTED**

- **REASON:** The replay compares baseline / B2-D / B2-E against human labels; none exist.
- **REQUIRED GATE:** frozen baseline + B2 readouts (i.e., sufficient labels).
- **CURRENT STATUS:** The historical failure corpus is preserved and cannot be re-selected after
  seeing results. Corpus (frozen reference): the 4 TYPE-B cases; weak true-present failures
  (child_06_six, child_07_one, child_01_nine); presentation-side cases (child_07_one,
  child_01_seven, child_02_ten, child_04_four, child_01_ten, child_01_four, child_03_four,
  child_02_four); the 14 false accepts from WP-1.9.22/23 (7 IDENTITY_WEAK, 4 IDENTITY_STRONG,
  3 SOFT_SIMILARITY); isolated-peak and encoder-disagreement cases (Pack R pools G/H).

Planned per-case fields: historical category, production result, human label (if available),
baseline result, B2-D result, B2-E result, fixed/partial/unchanged/regressed. Mechanism separation:
PHONE_MODEL_ERROR / SCORER_MISS / WINDOW_ERROR / ENCODER_NO_EVIDENCE / FALSE_ENCODER_EVIDENCE /
ALIGNMENT / MIXED / ASSESSABILITY. Mixed evidence stays mixed.

No failure was replayed; no case was re-categorized.
