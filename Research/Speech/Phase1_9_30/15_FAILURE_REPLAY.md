# 15 - FAILURE REPLAY (WP-1.9.30 Part 22)

**NOT EXECUTED**

- **REASON:** replay requires baseline/B2 readouts against human labels; none exist.
- **REQUIRED GATE:** frozen baseline + B2 readouts.
- **CURRENT STATUS:** historical failure corpus preserved and not re-selected: 4 TYPE-B cases; weak
  true-present failures (child_06_six, child_07_one, child_01_nine); presentation-side cases
  (child_07_one, child_01_seven, child_02_ten, child_04_four, child_01_ten, child_01_four,
  child_03_four, child_02_four); 14 false accepts (7 IDENTITY_WEAK, 4 IDENTITY_STRONG,
  3 SOFT_SIMILARITY); isolated-peak / encoder-disagreement cases. Categories: fixed / partial /
  unchanged / regressed with mechanism separation (PHONE_MODEL_ERROR, SCORER_MISS, WINDOW_ERROR,
  ENCODER_NO_EVIDENCE, FALSE_ENCODER_EVIDENCE, ALIGNMENT, MIXED, ASSESSABILITY).
