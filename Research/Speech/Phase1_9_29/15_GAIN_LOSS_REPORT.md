# 15 - GAIN/LOSS REPORT (WP-1.9.29 Part 15)

**NOT EXECUTED**

- **REASON:** Gain/loss requires baseline and B2 decisions per pilot token; neither exists without
  labels.
- **REQUIRED GATE:** baseline + B2-D/B2-E evaluated on the frozen test split.
- **CURRENT STATUS:** Table schema prepared (`15_GAIN_LOSS_TABLE.csv`, header only):

  transitions baseline correct -> B2 correct / baseline wrong -> B2 correct / baseline correct ->
  B2 wrong / baseline wrong -> B2 wrong, with speaker, token, target phone, human label, baseline
  score, B2 score, encoder/acoustic/temporal evidence, production decision, B2 decision.

Rules preserved: diagnosis only - no optimization from this table; the frozen success criteria
remain the sole basis for a promising/not-promising judgment.
