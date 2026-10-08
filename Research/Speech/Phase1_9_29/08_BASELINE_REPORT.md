# 08 - BASELINE REPORT (WP-1.9.29 Part 8)

**NOT EXECUTED**

- **REASON:** No genuine human labels for the frozen pilot test split (0/99). The production
  baseline may only be evaluated against frozen human labels; running it without labels cannot
  produce a legitimate result.
- **REQUIRED GATE:** Label sufficiency PASS (Part 5) + pilot label freeze (Part 6) + integrity PASS
  (Part 7 - already PASS).
- **CURRENT STATUS:** Baseline definition frozen and unchanged: the production system exactly as-is
  (no scorer/threshold/acceptance/alignment/window/encoder/VAD/router modification), evaluated
  FRR-first on the speaker-disjoint test split. Planned metrics: PRESENT recall / FRR, ABSENT
  detection / FAR, confusion matrix, balanced accuracy, precision where meaningful, per-phone,
  final-consonant, /r/, assessability, per-speaker, intervals per the frozen protocol.

No baseline number was computed, estimated, or inferred. No threshold was tuned.
