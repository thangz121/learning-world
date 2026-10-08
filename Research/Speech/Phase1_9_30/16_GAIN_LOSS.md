# 16 - GAIN / LOSS (WP-1.9.30 Part 23)

**NOT EXECUTED**

- **REASON:** requires baseline and B2 decisions per token; neither exists without labels.
- **REQUIRED GATE:** baseline + B2-D/B2-E on the frozen test split.
- **CURRENT STATUS:** table schema frozen (transitions BASELINE_CORRECT_B2_CORRECT /
  BASELINE_WRONG_B2_CORRECT / BASELINE_CORRECT_B2_WRONG / BASELINE_WRONG_B2_WRONG; fields: speaker,
  token, phone, human label, baseline score, B2 score, encoder/acoustic/temporal evidence, decision
  transition; aggregates by phone, speaker, age, final consonant, /r/, TYPE-B, assessability).
  Diagnostic only - never used to tune the model.
