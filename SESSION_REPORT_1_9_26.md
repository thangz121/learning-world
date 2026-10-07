# SESSION REPORT — WP-1.9.26 (RESEARCH BLOCKER BREAKOUT)

> **Tóm tắt (VI):** WP lớn nhằm loại bỏ blocker thay vì lặp lại "blocked". Đã: xây + kiểm thử
> pipeline nhãn mù (server + 276 ứng viên/49 speakers + protocol + README); mở rộng so sánh encoder
> ra toàn bộ 609 token (missing 16,5% vs 18,8%; false 17,2% vs 13,8%; agreement 82,8%); audit /r/
> 24 ca với F1/F2/F3/RMS; mở rộng audit dữ liệu 18→28 nguồn, phát hiện **OCSC 303 trẻ 4–9**,
> **JIBO 110 trẻ 4–7**, AusKidTalk 136h; xác minh route thương mại MyST (paid) và license
> speechocean762 (CC BY 4.0); thiết kế pilot 8–10 speakers + tiêu chí thành công khóa. **0 nhãn mới**
> (không reviewer; không tạo nhãn giả). Gate: **LABEL_COLLECTION_READY_DATA_BLOCKED**; B2 TRAINING
> = NO.

- **Date:** 2026-10-07 · **Machine:** ASUS · **Branch:** `ux/math-arenas-hotfix-20260930` (= `main`)
- **Base commit:** `8ba65d2` · **Scope:** research-only; no production/model/Unity change
- **Flags:** production_vad=false, router_locked=false, unity_integrated=false,
  scorer_modified=false, production_window_locked=false; B2 training = NO.

## 1. Objective
Actively attempt to remove the WP-1.9.25 blockers: (A) human labels, (B) child-speech data,
(C) license; determine whether B2 is justified; no training.

## 2. Work performed (Parts 1–31)
- **Research state** (`00_RESEARCH_STATE.md`, `artifacts/research_state.json`): known / unresolved /
  contradictory / label-dependent / data-dependent / no-label / external-data / legal evidence.
- **Label pipeline (Parts 2–6, 26–27)**: `serve_review_1926.py` (blind, per-reviewer randomized
  order, resume, JSON/CSV export, reviewer isolation) + `test_review_pipeline.py` (PASS; TEST
  submission deleted); 276-candidate pack (15 pools: diagnostic, balanced, random control, SIAK
  4–6); `LABELING_PROTOCOL.md`, `REVIEWER_README.md`, `HUMAN_LABEL_RESULTS.csv` (empty),
  `REVIEWER_AGREEMENT.csv`, `LABEL_QUALITY_REPORT.md`, `reviewer_agreement.py` (ready).
- **TYPE-B (Part 2 of the tree)**: `TYPE_B_CASES.csv`, `TYPE_B_EVIDENCE_MATRIX.csv`,
  `TYPE_B_REASSESSMENT.md` — 0/4 confirmed, 2 LABEL_LIMITED + 2 MIXED, 3/4 representation-dependent.
- **/r/ (Part 7)**: `r_deep_audit.py` → `03_R/R_CASES.csv` with F1/F2/F3 (parselmouth), RMS,
  voiced fraction, context; `R_DEEP_AUDIT.md`, `R_LABEL_REQUIREMENTS.csv`.
- **Encoder comparison (Part 20)**: `alt_encoder_all.py` (609 tokens, 205 s) +
  `encoder_comparison.py` → `ENCODER_COMPARISON.csv`, `encoder_comparison.json`.
- **Datasets/license (Parts 8–13, 21–23)**: 28 sources in `DATASET_MASTER_MATRIX.csv` +
  `DATASET_LICENSE_MATRIX.csv` + `DATASET_DOMAIN_MATRIX.csv`; `DATASET_DISCOVERY_REPORT.md`,
  `DATASET_ACQUISITION_ROADMAP.md`; legal reviews (SIAK, MyST, PERCEPT-R, restricted, counsel list).
- **Vietnamese-L1 (Parts 14–15, 28)**: `VIETNAMESE_L1_DATA_AUDIT.md`,
  `VIETNAMESE_CHILD_DATA_GAP.md`, `VIETNAMESE_CHILD_SPEECH_COLLECTION_PROTOCOL.md` (design only).
- **Annotation (Part 16)**: `PHONE_LABEL_STRATEGIES.md`, `ANNOTATION_COST_QUALITY_MATRIX.csv`.
- **B2 data/design (Parts 17–19, 25)**: MINIMUM/ROBUST/IDEAL datasets, `B2_DATA_REQUIREMENTS.csv`,
  `B2_EXPERIMENT_PLAN.md` (A–E), `B2_SUCCESS_CRITERIA.md` (frozen), `B2_PILOT_DESIGN.md`,
  `B2_ENCODER_COMPARISON_DESIGN.md`.
- **Blocker breakout (Parts 23–24)**: `BLOCKER_STATUS.md`, `BLOCKER_REMOVAL_SCORE.csv`,
  `ACQUISITION_ROUTE_RANKING.md`, `NO_DATA_DEAD_END_TEST.md`.

## 3. Exact metrics
- Label pack: 276 candidates / 49 speakers / 15 pools (diagnostic + balanced + random control +
  SIAK age 4–6); pipeline test PASS; **NEW_LABELS_COLLECTED = 0**.
- Encoder comparison (609 tokens): missing-evidence 16.48% vs 18.75%; false-evidence 17.24% vs
  13.79%; median present max_A 0.7919 vs 0.7923; agreement 82.76%; gains 50 / losses 55; isolated
  peaks 82.6%.
- /r/: 24 cases, 20 isolated peaks, median span 487 ms, RMS ratio 1.13, median F3 2812 Hz,
  F3/F2 1.84; LWE median max_A 0.041.
- Datasets: 28 audited; new age-4–6 corpora OCSC (303, 4–9), JIBO (110, 4–7), CAPIL (30, 5–6);
  MyST commercial route verified (LDC2021S05, 470 h, 1371 speakers, lexicon); speechocean762
  CC BY 4.0 (2.41 h child); SIAK CC-BY-ND unresolved.
- TYPE-B: 2 MIXED + 2 LABEL_LIMITED; 3/4 label-limited; 2/4 isolated peaks; 3/4
  representation-dependent.

## 4. Final gate / decisions
- Final gate: **LABEL_COLLECTION_READY_DATA_BLOCKED**.
- Next gate: `HUMAN_REVIEW_ROUND_AND_LOCAL_PILOT`.
- B2 TRAINING: NO; B2 DESIGN: pilot-ready (frozen criteria); production untouched.

## 5. Files
```
Research/Speech/Phase1_9_26/ (00_RESEARCH_STATE.md, 01_..12_ trees, experiments/, artifacts/)
SESSION_REPORT_1_9_26.md (this file, root; copy in 12_FINAL/)
```

## 6. Open items
1. Run the human review round (2 reviewers) on the 276-candidate pack.
2. Execute the local pilot after labels (B2-E + B2-D, FRR-first, speaker-disjoint).
3. Register TalkBank; request MyST quote; send the counsel questions; confirm JIBO terms.
4. Do not train B2; do not modify production.
