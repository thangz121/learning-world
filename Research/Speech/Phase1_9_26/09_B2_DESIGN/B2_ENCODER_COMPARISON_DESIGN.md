# B2 ENCODER COMPARISON DESIGN + RESULTS — WP-1.9.26 Part 20

> **Tóm tắt (VI):** So sánh có kiểm soát 2 encoder espeak có sẵn local trên toàn bộ 609 token:
> production xlsr-53 vs lv-60. Kết quả: missing-evidence 16,5% vs 18,8%; false-evidence 17,2% vs
> 13,8%; median present 0,79 cả hai; agreement 82,8% (50 gain / 55 loss). Không encoder nào thắng
> tổng thể → "alternative encoder" là công cụ chẩn đoán, không phải giải pháp.

## Design

- Encoders: `facebook/wav2vec2-xlsr-53-espeak-cv-ft` (production, apache-2.0) vs
  `facebook/wav2vec2-lv-60-espeak-cv-ft` (local, apache-2.0). No downloads; no training.
- Data: all 609 primary-window evaluation tokens (LWE 80 + SO762 529), 45 speakers.
- Measurement: max target-class posterior in the production span (same DP/alignment code for both),
  missing-evidence (present tokens < 0.02), false-evidence (absent tokens ≥ 0.30), /r/, final
  consonants, agreement at 0.10, isolated peaks, speaker spread.
- Artifacts: `artifacts/alt_encoder_all.csv` (609 rows), `artifacts/encoder_comparison.json`,
  `09_B2_DESIGN/ENCODER_COMPARISON.csv`.

## Results (measured)

| metric | production xlsr-53 | alt lv-60 |
|---|---:|---:|
| missing-evidence rate (present, max_A<0.02) | 16.48% | 18.75% |
| false-evidence rate (absent, max_A≥0.30) | 17.24% | 13.79% |
| present median max_A | 0.7919 | 0.7923 |
| agreement at 0.10 | 82.76% | – |
| tokens where alt gains evidence | – | 50 |
| tokens where alt loses evidence | – | 55 |
| speaker median spread | 0.981 | 0.974 |
| isolated-peak rate (present) | 82.6% | – |

## Interpretation

- Aggregate behaviour is nearly identical (median 0.79) but individual decisions differ for ~17% of
  tokens in both directions — representation dependence is real and symmetric.
- The alternative reduces false-evidence slightly but increases missing-evidence: it is a diagnostic
  cross-check, not a replacement.
- The /r/ subset remains the weakest for both (median max_A ~0.04 in LWE; see `03_R/R_DEEP_AUDIT.md`).
- Next: include the comparison in the pilot evaluation as a control; a future third encoder should
  be admitted only if locally available, apache-compatible, and tested on the same 609 tokens.
