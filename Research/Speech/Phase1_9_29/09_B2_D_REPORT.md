# 09 - B2-D REPORT (WP-1.9.29 Part 9)

**NOT EXECUTED**

- **REASON:** Baseline is NOT EXECUTED (no labels); B2-D runs only after a frozen baseline, per the
  preregistered order.
- **REQUIRED GATE:** baseline frozen + sufficient genuine labels on the test split.
- **CURRENT STATUS:** Design frozen and unchanged. Permitted variants only: D0 (production mean,
  baseline), D1 (production max, control), D3 (alt max), D4 (alt mean), D5 (both encoders
  OR/AND of strong evidence). No D6 or any new variant; no hyperparameter/threshold search; no
  post-hoc tuning. Evaluation must include: overall FRR-first, FAR, present recall, absent
  detection, per-phone, final consonants, /r/, TYPE-B, missing-evidence, false-evidence,
  speaker-disjoint performance, gain/loss per token and per speaker.

Reference: `Phase1_9_27/07_B2_D/` (design, ablations, evaluation protocol; hashes verified this WP).
Prior 609-token diagnostic (WP-1.9.26) remains diagnostic only and is not a pilot result:
production missing 16.48% / false 17.24%; alt missing 18.75% / false 13.79%; agreement 82.76%;
50 gains / 55 losses; no aggregate winner.
