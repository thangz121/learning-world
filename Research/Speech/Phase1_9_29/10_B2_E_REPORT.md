# 10 - B2-E REPORT (WP-1.9.29 Part 10)

**NOT EXECUTED**

- **REASON:** Baseline and B2-D are NOT EXECUTED (no labels); B2-E runs after both.
- **REQUIRED GATE:** baseline + B2-D readouts frozen; sufficient genuine labels; fitting only if
  >=60 PRESENT + 60 ABSENT in train+dev.
- **CURRENT STATUS:** Design frozen and unchanged. Permitted variants only: BASE, E_ENC, E_ACO,
  E_TMP, E_ENC_ACO (primary set); secondary variants (E_ENC_TMP, E_ENC_PHO, FULL) only if the
  primary test labels reach >=30/side. No new features, no post-hoc feature selection, no threshold
  search, no rule redesign. Random-feature control mandatory for any fitted variant. Assessability
  gate mandatory (no credit for confidence on NOT_ASSESSABLE audio).

Evaluation items prepared: FRR-first, FAR, present recall, absent detection, /r/, final consonants,
TYPE-B, assessability, speaker generalization, gain/loss, failure categories.

Training safety: no fine-tuning, no LoRA, no head training, no full B2 training. Nothing was
trained; `pilot_runner.py --allow-training` remains a hard stop.
