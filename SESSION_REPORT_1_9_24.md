# SESSION REPORT — WP-1.9.24 (HUMAN LABEL EXPANSION + ENCODER EVIDENCE DESIGN AUDIT)

> **Tóm tắt (VI):** Hoàn thành cả hai nhánh. (A) Phân loại + làm mù 23 ứng viên (11 PRESENT / 9 ABSENT
> / 3 UNCERTAIN), kiểm tra tooling (serve_review.py hard-code fidelity_v2), không có reviewer →
> **NEW_LABELS_COLLECTED = 0**, không tạo nhãn giả. (B) Áp tiêu chí encoder-side failure + 7 test
> falsification cho 4 ca TYPE-B: **0/4 confirmed, 4/4 UNRESOLVED** (3 thiếu nhãn listening đáng tin,
> 2 đỉnh 1-frame cô lập). Counterfactual encoder thứ hai (lv-60-espeak, local): TYPE-B giữ ≥0,1 cả 4
> nhưng 2/4 giảm mạnh; no-evidence present 3/10 so762 + 3/28 LWE gain evidence mạnh → representation-
> dependent. Confusion profile: s↔z, m↔n, v↔f; liquid yếu nhất (/r/ median max 0.135, /l/ 0.005);
> 87/528 (16,5%) labeled present có max_A<0,02. Kết luận: **LABELS_INSUFFICIENT_ENCODER_HYPOTHESIS_
> SUPPORTED** (limitation no-evidence được ủng hộ; false-evidence chưa xác nhận vì thiếu nhãn).
> B2 TRAINING = NO; B2 DESIGN = NOT READY; license child corpora BLOCKED.

- **Date:** 2026-10-07
- **Machine:** ASUS · **Branch:** `ux/math-arenas-hotfix-20260930` (= `main`) · **Base commit:** `6770de9`
- **Scope:** research-only; no production/model/Unity change. **Flags:** all five false; no B2 training.
- **Git:** automatic commit/push per the standing instruction (branch + `main`, fast-forward).

## 1. Mission
Determine whether the remaining error mass is in the acoustic representation/encoder, and what a
justified B2 would have to change; and resolve the label blocker (encoder false evidence vs human
label ambiguity). No training, no production change.

## 2. Work performed
- Part A: `experiments/build_review_pack_final.py` — classified/blinded 23 candidates, built
  `HUMAN_LABEL_REVIEW_PACK_FINAL.csv`, empty `HUMAN_LABEL_RESULTS.csv`; inspected the review tooling.
- Part B: `experiments/run_encoder_audit.py` — TYPE-B replay (4 cases, full trace + falsification
  A–G), phone confusion profile, child-speech representation stats.
- Part B: `experiments/alt_encoder_counterfactual.py` — small local alternative-encoder comparison
  (`wav2vec2-lv-60-espeak-cv-ft`, local files only, no download); summary from CSV without rerun.
- `experiments/assemble_results.py` — `ENCODER_DESIGN_EXPERIMENT_RESULTS.json`.
- Reports: `ENCODER_FAILURE_AUDIT.md`, `CHILD_SPEECH_REPRESENTATION_AUDIT.md`, `B2_DESIGN_OPTIONS.md`,
  `B2_DATA_REQUIREMENTS.csv`, `LICENSE_AND_DATA_BLOCKERS.md`, `FAILURE_LABEL_DEPENDENCE_MATRIX.csv`,
  `HUMAN_LABEL_EXPANSION_REPORT.md`, `NEXT_GATE_DECISION.md`.

## 3. Exact metrics / measured results
- **Labels:** pack 23 (11 PRESENT / 9 ABSENT / 3 UNCERTAIN candidates); NEW_LABELS_COLLECTED = 0;
  SINGLE_REVIEWER_LIMITATION = TRUE; existing 28 LWE blind (16P/12A, /r/ 1P LOW / 5A).
- **TYPE-B (4):** all survive every acceptance rule; falsification verdicts 0/4 confirmed,
  4/4 UNRESOLVED (3 label-limited, 2 isolated one-frame peaks, 1 both).
  - child_07_seven (n, PROBABLY_ABSENT HIGH): max 0.629, neighbors 0.024/0.005 → fail C.
  - 014180143_15 (n): max 0.682, neighbor 0.272 → fail G. 014190172_7 (m): 0.820, 0.029/0.001 → fail C+G.
  - 014350146_16 (n): max 0.956, neighbors 0.235/0.249 → fail G.
- **Alt encoder:** TYPE_B persists 4/4 at ≥0.1 (median 0.751→0.675; child_07_seven 0.63→0.94,
  014350146_16 0.96→0.91, but 014180143_15 0.68→0.25, 014190172_7 0.82→0.41). No-evidence present
  3/10 gain strong evidence (0.76/0.82/0.92); LWE gained 3/28 (child_01_seven 0.059→0.47,
  child_04_four 0.09→0.41), lost 2/28; strong present controls persist 5/5.
- **No-evidence present:** 87/528 (16.5%) labeled presents with max_A < 0.02; 138 weak presents
  (max_A<0.15) with production recall 0.630.
- **Confusions:** s↔z (62/93, 41/58), m↔n (17/20), v↔f/b; liquids weakest (median max 0.109;
  /r/ FAR 0.833, /l/ median 0.005).
- **Child-speech stats:** stop median max 0.92 (one-frame 80%), fricative 0.75 (64%), nasal 0.68
  (43%), liquid 0.109 (57%), affricate 0.001.
- **B2:** TRAINING NO; DESIGN NOT READY; target = missing evidence for weak finals (liquids first);
  license child corpora BLOCKED (SIAK review open, MyST unverified, OGI/CMU restricted,
  Vietnamese-L1 corpus nonexistent); models apache-2.0 CLEAR; no GPU on ASUS.

## 4. Gate / decisions
- Final gate: **LABELS_INSUFFICIENT_ENCODER_HYPOTHESIS_SUPPORTED**.
- Next gate: **LABEL_COLLECTION_AND_DATA_LICENSE_GATE** (research-only).
- B2: NO training; design NOT READY. Production: untouched.

## 5. Artifacts
```
Research/Speech/Phase1_9_24/
  HUMAN_LABEL_EXPANSION_REPORT.md, HUMAN_LABEL_RESULTS.csv,
  HUMAN_LABEL_REVIEW_PACK_FINAL.csv, ENCODER_FAILURE_AUDIT.md, ENCODER_TYPE_B_CASES.csv,
  PHONE_CONFUSION_PROFILE.csv, CHILD_SPEECH_REPRESENTATION_AUDIT.md, B2_DESIGN_OPTIONS.md,
  B2_DATA_REQUIREMENTS.csv, LICENSE_AND_DATA_BLOCKERS.md,
  FAILURE_LABEL_DEPENDENCE_MATRIX.csv, ENCODER_DESIGN_EXPERIMENT_RESULTS.json,
  NEXT_GATE_DECISION.md,
  artifacts/ (child_speech_stats.json, alt_encoder_counterfactual.csv,
             alt_encoder_summary.json),
  experiments/ (build_review_pack_final.py, run_encoder_audit.py,
                alt_encoder_counterfactual.py, alt_summary_from_csv.py, assemble_results.py)
```

## 6. Open items / next steps
1. Collect the human labels (pack L01–L23; /r/ first; TYPE-B ABSENT; rank-2..5 identity).
2. Resolve data licensing (SIAK legal review; MyST audit; keep OGI/CMU out of commercial paths).
3. Research-only design study (B2-E hybrid evidence, B2-D broader local encoder comparison);
   B2-C needs licensed child data and a GPU.

## 7. Git
Commit + push this package to the working branch and `main` (fast-forward), including this report and
the index update, per the standing instruction; verify `main == origin/main` and a clean tree.
