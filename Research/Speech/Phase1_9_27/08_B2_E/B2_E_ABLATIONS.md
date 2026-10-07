# B2-E ABLATIONS (FROZEN) — WP-1.9.27 Part 16

> **Tóm tắt (VI):** Ablation khóa trước kết quả: BASELINE (CTC-only/production), ENCODER_ONLY,
> ACOUSTIC_ONLY, ENCODER+ACOUSTIC, ENCODER+TEMPORAL, ENCODER+ACOUSTIC+TEMPORAL, ENCODER+PHONETIC,
> FULL_HYBRID. Với 546 token, chỉ chạy tối đa 5 biến thể chính; cắt bớt phải được biện luận trước.

| id | feature set | model | primary? |
|---|---|---|---|
| BASE | production decision (no fitting) | none | yes (reference) |
| E_ENC | encoder-only group | logistic / shallow GBM | yes |
| E_ACO | energy + voicing | same | yes |
| E_TMP | temporal group | same | yes |
| E_ENC_ACO | encoder + energy/voicing | same | yes |
| E_ENC_TMP | encoder + temporal | same | secondary |
| E_ENC_PHO | encoder + formants | same | secondary (small n for formants) |
| FULL | all groups + transition cue | same | secondary |

Selection rule (pre-registered): with the pilot's 546 tokens and a labelled subset of at least
60P+60A, run BASE + E_ENC + E_ACO + E_TMP + E_ENC_ACO (5 primary). Add E_ENC_TMP/E_ENC_PHO/FULL only
if every primary variant has >=30 labelled test tokens per side; otherwise report them as
not-evaluated. A random-feature control (same model, shuffled features) must accompany every fitted
variant. No per-phone model unless a global model fails and the failure is uniform; per-phone
thresholds are exploratory and reported with an overfitting penalty (leave-one-speaker-out).
