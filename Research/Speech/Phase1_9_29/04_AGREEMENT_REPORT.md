# 04 - AGREEMENT REPORT (WP-1.9.29 Part 4)

**NOT EXECUTED - NOT COMPUTABLE**

- **REASON:** Genuine labels: 0. Agreement requires at least one candidate reviewed by >=2 genuine
  reviewers; none exists. Computing agreement on fewer reviewers or on synthetic/QA data is
  prohibited.
- **REQUIRED GATE:** >=2 independent reviewer exports over a common candidate set (Pack R and/or
  Pack P).
- **CURRENT STATUS:** Implementation ready and previously validated (WP-1.9.27 synthetic test PASS,
  deleted; WP-1.9.28 dry-run output `no_agreement / NO_REVIEWER_AVAILABLE`). Planned statistics are
  frozen: raw agreement, Cohen's kappa (2 raters), weighted kappa (ordinal ABSENT<UNCERTAIN<PRESENT),
  Fleiss' kappa (>=3), PRESENT/ABSENT/UNCERTAIN confusion, assessability agreement, per-phone,
  /r/, TYPE-B, confidence agreement.

## Rules that will apply

- Single-reviewer candidates are marked `SINGLE_REVIEWER`; no fake consensus, no fake kappa.
- Disagreements are preserved, classified (acoustic ambiguity / reviewer uncertainty / assessability
  / phonetic interpretation difference / target ambiguity / recording problem), and adjudicated only
  after raw labels are frozen.
- No majority vote; UNCERTAIN is never collapsed into ABSENT.

Command (frozen) when two exports exist: see `03_HUMAN_LABEL_IMPORT_REPORT.md` step 1 then
`label_analysis.py --pack .../PACK_COMBINED.csv --csv ... --out .../03_AGREEMENT`.
