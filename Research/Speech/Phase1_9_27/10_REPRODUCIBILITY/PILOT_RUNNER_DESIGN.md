# PILOT RUNNER DESIGN — WP-1.9.27 Part 29

> **Tóm tắt (VI):** Runner nghiên cứu 5 stage: validate, features_prod, features_alt, features
> (merge), eval, report. Training bị chặn cứng (`--allow-training` in STOP). Features đã chạy xong
> (546 token prod 200s + alt 169s). eval/report chờ nhãn Pack P.

## Stages (as built)

| stage | what it does | status |
|---|---|---|
| validate | frozen split + manifest + labels-ready | run: PASS (labels false) |
| features_prod | production encoder evidence + parselmouth features per pilot token | run: 546 tokens, 200 s |
| features_alt | alternative encoder (lv-60-espeak) evidence | run: 546 tokens, 169 s |
| features | merge prod+alt into `pilot_features.csv` (30 fields) | run: 546 rows |
| eval | baseline / B2-D / B2-E evaluation | requires Pack P labels (currently STOPs) |
| report | per-speaker, /r/, assessability, failure replay summaries | requires eval output |

## Design constraints

- Training hard-disabled: `--allow-training` prints a STOP by design; no fine-tuning code exists in
  the runner.
- Models are loaded one at a time (running both encoders in one process previously caused a
  0xC0000005 memory fault); stages are split accordingly.
- Token identity: `token_id = <utt_id>_<final>` matches Pack P and the review pipeline.
- Labels are read from `03_LABEL_ANALYSIS/HUMAN_LABEL_RESULTS.csv` (consensus joined by token_id);
  the eval stage must refuse to run if labels do not cover both classes.
- All randomness is seeded (split seed 1927); review shuffle is per-reviewer deterministic.

## Runtime measured on ASUS (CPU-only)

- prod features: 200 s / 546 tokens (~0.37 s/token, includes parselmouth formants).
- alt features: 169 s / 546 tokens (~0.31 s/token).
- Combined in one process: memory fault (0xC0000005) — do not reintroduce.

## Remaining work (next gate)

1. Import Pack P labels (two reviewers).
2. Implement `eval` aggregation using the frozen config (D0–D5, BASE/E_*).
3. Run `report` with per-speaker, /r/, assessability and TYPE-B replay outputs.
4. Freeze results; apply `09_SUCCESS_CRITERIA/` mechanically.
