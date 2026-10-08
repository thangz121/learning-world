# 04 - AGREEMENT (WP-1.9.30 Part 10)

**NOT ESTIMABLE**

- **REASON:** Genuine labels: 0; genuine reviewers: 0. Agreement statistics require >=2 independent
  genuine reviewers over a common candidate set.
- **REQUIRED GATE:** two genuine reviewer exports (Pack R and/or Pack P).
- **CURRENT STATUS:** frozen agreement implementation ready and previously validated
  (`label_analysis.py`; WP-1.9.27 synthetic test PASS, deleted; WP-1.9.29 dry-run
  `no_agreement / NO_REVIEWER_AVAILABLE`).

## Planned statistics (frozen, unchanged)

raw agreement; Cohen's kappa (2 raters); weighted kappa (ordinal ABSENT<UNCERTAIN<PRESENT);
Fleiss' kappa (>=3); PRESENT/ABSENT/UNCERTAIN confusion; assessability agreement; per-phone, /r/,
TYPE-B, confidence agreement.

Rules: single-reviewer candidates are marked `SINGLE_REVIEWER` (no fake consensus); disagreements
are preserved as raw labels and marked `UNRESOLVED` when the frozen consensus protocol cannot
resolve them; no majority vote; UNCERTAIN never collapsed into ABSENT.
