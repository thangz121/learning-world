# LABEL QUALITY REPORT — WP-1.9.26 Part 2

> **Tóm tắt (VI):** Pipeline nhãn đã hoàn chỉnh và kiểm thử end-to-end (blind payload, audio, submit,
> resume, export, cô lập reviewer; submission TEST đã xoá). Pack 276 ứng viên / 49 speakers /
> 15 pool (diagnostic + balanced + random control + SIAK age 4–6). **0 nhãn mới** vì không có
> reviewer; không tạo nhãn giả. Sau khi có reviewer: kappa hai người, chấm dứt SINGLE_REVIEWER.

## 1. Review pipeline (built and validated)

- `experiments/serve_review_1926.py`: blind HTTP server.
  - `GET /api/pack?reviewer=ID` → per-reviewer randomized order; payload = blind_id, word,
    target_phone only (verified: no machine fields).
  - `GET /audio/<blind_id>` → serves the local recording (WAV/FLAC).
  - `POST /api/submit` → appends one JSONL record per label to `artifacts/reviews/<reviewer>.jsonl`
    (reviewer id, blind_id, label, confidence, note, timestamp).
  - `GET /api/progress` → resume; `GET /api/export` → JSON/CSV.
  - Reviewer isolation verified (reviewer B cannot see reviewer A's file).
- Validation: `experiments/test_review_pipeline.py` — PASS (pack 276, blind payload, audio bytes,
  submit/progress/export, isolation); the TEST-TOOL submission was deleted; no labels fabricated.

## 2. Review corpus (Parts 3/26)

`REVIEW_CANDIDATES.csv` — 276 candidates, 49 speakers, deterministic seed 1926:

| pool | n | purpose |
|---|---:|---|
| A_r_present | 15 | diagnostic /r/ present |
| B_r_absent | 6 | diagnostic /r/ absent |
| C_r_uncertain | 3 | /r/ unlabelled |
| D_weak_present | 25 | weak present (max_A<0.15) |
| E_weak_absent | 8 | weak absent |
| F_typeb_strong_false | 5 | TYPE-B + related strong false evidence |
| G_isolated_peak | 17 | isolated one-frame peaks |
| H_encoder_disagreement | 16 | primary vs alt |Δ|≥0.30 |
| I_final_consonant_failure | 9 | true present production miss |
| J_high_score_false_accept | 5 | production-accepted absent |
| K_balanced_review | 60 | balanced phone-class × truth |
| L_low_score_true_present | 10 | max_A<0.02 true present |
| M_random_control | 60 | random control (representativeness) |
| N_consistency_control | 7 | historical listening labels re-inserted blind |
| O_siak_age46_broad | 30 | SIAK age 4–6 broad pool (no machine evidence; only ~5 speakers) |

The random control is deliberately larger than any diagnostic pool so that diagnostic cases can be
compared against a representative baseline. Every candidate has a `selection_reason`.

## 3. Machine vs human comparison matrix (Part 6)

`REVIEW_CANDIDATES.csv` already carries, per candidate: machine rank, max_A, mean_A, peak width,
margin, blank, production decision, alternative-encoder max_A (609-token run), historical label
(only for consistency controls). Human columns are added after review. Energy/spectral features for
the /r/ subset are in `03_R/R_CASES.csv`.

## 4. Label status

- **NEW_LABELS_COLLECTED = 0** (no reviewer available in-session; no fabrication).
- `HUMAN_LABEL_RESULTS.csv` = empty schema.
- `REVIEWER_AGREEMENT.csv` documents the no-reviewer and single-reviewer limitations.
- The pipeline turns the label blocker into a human-availability task: with two reviewers the pack
  can produce up to 276 labels; the minimum scientific subset is 60 PRESENT + 60 ABSENT across
  ≥20 speakers (see `08_B2_DATA/B2_DATA_REQUIREMENTS.csv`).

## 5. Quality controls

- Blind: machine fields never exposed by the server; coordinator CSV separated.
- Consistency controls: 7 historical labels are hidden in the pack to measure intra-reviewer
  consistency with the historical single reviewer.
- No majority voting; disagreements preserved; adjudication only after raw labels are frozen.
- Score-derived labels (SO762/SIAK) are never promoted to listening labels.
