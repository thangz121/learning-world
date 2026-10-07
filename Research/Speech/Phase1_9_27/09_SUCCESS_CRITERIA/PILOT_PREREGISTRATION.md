# PILOT PRE-REGISTRATION — WP-1.9.27 Part 28

> **Tóm tắt (VI):** Đăng ký trước toàn bộ: giả thuyết, split, features, models, metrics, tiêu chí
> thành công/thất bại, ablations, stopping rules — khóa trước khi thấy bất kỳ kết quả nào. Mọi
> thay đổi sau khi thấy kết quả đều vô hiệu hóa kết luận.

- Date frozen: 2026-10-07 (WP-1.9.27) — before any Pack P label exists.
- Registration scope: local pilot B2-D (alternative encoder) and B2-E (hybrid evidence).

## 1. Hypothesis

Encoder adaptation / hybrid acoustic-phonetic modeling directionally improves the FRR-first
trade-off and reduces missing-evidence for weak child final consonants on speaker-disjoint local
data; a representation difference (B2-D) or added phonetic evidence (B2-E) that fails this test is
not worth pursuing to full B2 without new evidence.

## 2. Data and split (frozen)

- 10 SO762 children not used in any WP-1.9.x set; 200 utterances; 546 final-consonant tokens;
  0.196 h; QC PASS 200/200; ages 6–7.
- Speaker-disjoint train/dev/test = 6/2/2 (`pilot_split.json`, seed 1927).
- Labels: HUMAN-LISTENING from Pack P (546 candidates, `PILOT_REVIEW_CANDIDATES.csv`), minimum
  60 PRESENT + 60 ABSENT, >=2 independent reviewers (or explicit single-reviewer limitation).

## 3. Features (frozen)

As listed in `07_B2_D/B2_D_FEATURE_SCHEMA.csv` and `08_B2_E/B2_E_FEATURE_SCHEMA.csv`, extracted and
already stored in `artifacts/pilot_features.csv`. No feature may be added after seeing test labels
except as a clearly marked post-hoc analysis.

## 4. Models (frozen)

- B2-D: no model; encoder comparison at the same measurement code and aggregation.
- B2-E: low-capacity only (regularized logistic regression / shallow GBM) + random-feature control;
  no deep nets, no fine-tuning, no stacking.
- Baseline: frozen production decision path (unmodified).

## 5. Metrics and outcome definitions

FRR-first; FAR; missing-evidence; AUC (n>=30/side); per-phone-class recall; /r/ separately;
per-speaker reporting; dev→test generalization drop <=5 points; assessability gate; calibration
+/-0.1; failure replay. Outcome labels and numeric thresholds: `SUCCESS_CRITERIA.md`,
`FAILURE_CRITERIA.md`, `KILL_SWITCH.md`.

## 6. Ablations (frozen)

`07_B2_D/B2_D_ABLATIONS.md` and `08_B2_E/B2_E_ABLATIONS.md`; primary set fixed at 5 variants per
experiment; the random-feature control is mandatory for any fitted variant.

## 7. Stopping rules

`KILL_SWITCH.md`. The pilot cannot authorize training; a positive result authorizes only a
data/licence decision for full B2.

## 8. Irreversibility

Once test metrics are read out, thresholds/metrics/features may not change. A rerun with different
choices is a new experiment with a new preregistration file.
