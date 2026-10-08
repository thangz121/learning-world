# 08 - BASELINE (WP-1.9.30 Part 15)

**NOT EXECUTED**

- **REASON:** No genuine human labels on the frozen test split (0/99). Baseline evaluation requires
  frozen HUMAN-LISTENING labels; running without them cannot produce a legitimate result.
- **REQUIRED GATE:** label sufficiency PASS + label freeze + integrity (integrity already PASS).
- **CURRENT STATUS:** baseline definition frozen and unchanged - production system exactly as-is
  (no scorer/threshold/acceptance/alignment/window/encoder/VAD/router change), FRR-first, speaker-
  disjoint test split. Planned metrics: PRESENT recall, FRR, ABSENT detection, FAR, precision,
  balanced accuracy, confusion matrix, per-phone, final consonant, /r/, assessability, per-speaker,
  frozen intervals.

No baseline number was computed, estimated, or inferred; no threshold was tuned.
