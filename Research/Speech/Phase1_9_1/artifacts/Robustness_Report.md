# Robustness Report — Gate 4

Reference on NEW clean Silero segs: **WEAK_REFERENCE_FROM_CLEAN_BASELINE** (not independent GT).

## White noise on NEW (thr=0.5)

| Condition | silero F1 | hybrid_score F1 | adaptive_then_silero F1 | energy F1 |
|---|---:|---:|---:|---:|
| clean | 1.000 | 0.966 | 0.997 | 0.684 |
| SNR20 | 0.899 | 0.902 | 0.907 | 0.647 |
| SNR10 | 0.832 | 0.834 | 0.794 | 0.458 |
| SNR5 | 0.801 | 0.805 | 0.623 | 0.226 |
| SNR0 | 0.726 | 0.728 | 0.321 | 0.090 |
| SNR-5 | 0.404 | 0.406 | 0.075 | 0.014 |
| IMG as-is | n=0 | n=28 | n=0 | n=75 |

## Findings
1. hybrid_score tracks Silero under white noise (similar F1).
2. adaptive_then_silero degrades faster under noise than Silero alone.
3. IMG collapse is **not** reproduced by moderate white SNR on NEW.
4. No human GT on noisy variants → metrics are weak-reference only.
