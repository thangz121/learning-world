# SESSION REPORT — WP-1.9.21 (TEMPORALLY PLAUSIBLE SUPPORT AGGREGATION + LABEL SUFFICIENCY AUDIT)

> **Tóm tắt (VI):** Kiểm định giả thuyết aggregation/support của 1.9.20 trên frozen model (không
> train, không đổi production). Dựng frame cache tái sử dụng (289 utt / 1.156 encoder run / 2.436
> token-window / 117.128 frame; drift số học 3,2% so với bản lưu 1.9.19, baseline decision không đổi),
> định nghĩa vùng ứng viên A/B/C/D + position masking (loại occurrence cùng lớp trước đó), chạy 8 họ
> biến thể support. **Không luật nào giữ được recall production** (dev floor 0,9088; tốt nhất 0,9018);
> tại recall khớp LWE 0,9375 FAR tăng 0,4167 → 0,75 (13/16 PRESENT không được support); tại plateau
> FAR 0,0833 recall còn 0,5625. Position masking đúng (child_01_nine 0,979 và child_03_six 0,919 là
> occurrence sai). Gate **SUPPORT_AGGREGATION_FAIL**; B2 NOT YET; nhãn mới thu được 0 (không có
> reviewer trong phiên), pack 20 ứng viên đã chuẩn bị; /r/ vẫn data-limited (1 present LOW).

- **Date:** 2026-10-07
- **Machine:** ASUS · **Branch:** `ux/math-arenas-hotfix-20260930` (= `main`) · **Base commit:** `ac08f25`
- **Scope:** research-only; no production/model/Unity change. **Flags:** all five false.
- **Git:** automatic commit/push per the standing instruction (branch + `main`, fast-forward).

## 1. Mission
Determine whether a temporally plausible, position-masked support/aggregation rule can preserve
genuinely present final-consonant evidence destroyed by the span mean while rejecting isolated peaks,
wrong occurrences, neighboring-phone confusion and false acoustic evidence; evaluate FRR-first; audit
label sufficiency; do not unlock B2 unless justified.

## 2. Work performed
- Reused 1.9.18/1.9.19/1.9.20 artifacts; confirmed no frame-level cache existed (only summary rows).
- `experiments/p21_lib.py` — shared region/variant/evidence library (read-only `PhoneEvidenceV2`).
- `experiments/build_frame_cache.py` — recomputed the frozen encoder exactly for the 1.9.19 evaluation
  sets (grouped per utterance, one encoder run per utterance × window; four windows:
  full/raw/pad100/pad250) and wrote `artifacts/frame_cache/frames_*.csv`,
  `token_evidence_*.csv`, `FRAME_CACHE_MANIFEST.json` (hashes/provenance; no raw audio).
- Candidate regions A (production span), B (±5-frame context), C (final tail after the
  preceding-phone acoustic boundary), D (conservative union), all minus earlier same-class spans
  (mandatory position mask); WRONG_OCCURRENCE reported separately.
- Support variants: CURRENT_MEAN (production), MAX, TOP-2/3/5, LOCAL_CLUSTER, PEAK_PROMINENCE,
  TEMPORAL_SUPPORT, CONSERVATIVE (PRESENT/ABSENT/UNCERTAIN), plus unmasked-global and mean-A controls.
- `experiments/analyze_support.py` — dev selection (SO762 train children, FRR-first), external LWE
  evaluation, negative controls, window stability, sparse-peak analysis, human-uncertain separation,
  13-case replay, failure reclassification; all required outputs written.
- Determinism checks: `check_determinism.py`, `diff_1919.py` (cross-process reproduction verified).

## 3. Exact metrics / measured results
- **Frame cache:** lwe 320 token-windows/11,344 frames; so762_dev 936/42,202; so762_test 908/47,090;
  so762_absent_dev 272/16,492; total 2,436/117,128; 1,156 encoder runs, 819 s.
- **Drift vs stored 1.9.19 rows:** 78/2,436 (3.2%) — small numeric deltas / near-tie span flips from
  cross-session CPU numerics; baseline decisions unchanged in checked rows; re-run of the 1.9.19 code
  path reproduces the cache exactly (e.g. `child_02_five pad100` 0.8652 in two processes).
- **Production (full window):** LWE recall 0.9375 / FAR 0.4167 (9 unsupported, 3 false support);
  dev 0.9088 / 0.5294; test 0.8943.
- **Selection:** no support setting reaches the dev floor 0.9088 (best `MAX τ=0.01` = 0.9018) → frozen
  fallback = `MAX τ=0.01`; LWE recall 0.9375 with FAR 0.75 (13 unsupported, 6 false support).
- **Plateau (`MAX τ=0.2–0.4`):** LWE recall 0.5625 / FAR 0.0833; TOP3 τ=0.3–0.5 → FAR 0; conservative
  0.5625/0.0909 with 3.6% UNCERTAIN. Matched-recall MAX (τ≤0.01): FAR 0.75–0.9167.
- **Mean-A control:** recall 0.5 at τ=0.02, 0.0625 at 0.05, 0 at ≥0.10 → mean aggregation unusable.
- **Known cases:** fixed 1 (`child_07_one`, unsupported), regressed 1 (`child_06_six`); root causes:
  FALSE_PEAK_COMPETITOR 5, WEAK_INCONCLUSIVE 4, ENCODER_NO_EVIDENCE 2, WRONG_OCCURRENCE_MASKED 1,
  TRUE_SPARSE_SUPPORT 1.
- **Sparse peaks:** all 16 LWE present peaks are width-1 (20 ms); dev present 238/285 width-1; width
  AUC 0.78–0.82; coherence requirement (width ≥2) drops dev recall 0.76 → 0.14.
- **Stability (selected MAX):** LWE flips 5/28 (17.9%), test 24/227 (10.6%), dev 28/302 (9.3%).
- **Negative controls:** unmasked global MAX wrongly accepts `child_01_nine` (earlier /n/ 0.979);
  masked rule excludes it; SO762 absent-enriched absent-det 0.647–0.706 for selected variants.
- **Human-uncertain:** 2 AMBIGUOUS final-consonant tokens kept separate (never ground truth).
- **Label audit:** 0 new confident PRESENT labels (no reviewer in-session; not manufactured);
  /r/ present n=1 LOW; 20-item candidate pack written; no audio added to the repo.

## 4. Gate / decisions
- Gate: **SUPPORT_AGGREGATION_FAIL** (`NEXT_GATE_DECISION.md`).
- Primary finding: **MIXED** — mean dilution is real for strong presents (but production's top-1
  identity already accepts them); weak present misses are encoder no-evidence / window artifacts;
  false positives are identity-driven plus one strong false peak (`child_07_seven` 0.63).
- B2: **NOT YET**. Production scorer change justified: **NO**. More labeled data required: **YES**.

## 5. Artifacts
```
Research/Speech/Phase1_9_21/
  TEMPORAL_SUPPORT_AGGREGATION_REPORT.md, NEXT_GATE_DECISION.md,
  SUPPORT_CASE_ANALYSIS.csv, SUPPORT_FRAME_TRACE.csv, SUPPORT_VARIANT_RESULTS.json,
  SUPPORT_FAILURE_RECLASSIFICATION.csv, NEW_HUMAN_LABELS.csv, LABEL_CANDIDATES.csv,
  artifacts/frame_cache/ (frames_*.csv, token_evidence_*.csv, FRAME_CACHE_MANIFEST.json),
  experiments/ (p21_lib.py, build_frame_cache.py, analyze_support.py, check_determinism.py,
                diff_1919.py, dump_lwe_full.py, inspect_cache.py, inspect_results.py,
                final_summary.py)
```

## 6. Open items / next steps
1. Acceptance/identity audit (research-only) on the existing cache: why top-1 identity accepts
   weak/no-evidence finals and weak false positives; FRR-first.
2. Human review round on `LABEL_CANDIDATES.csv` (10–20 clips, /r/ priority) with the existing tooling.
3. Only then consider an encoder-level (B2) design.

## 7. Git
Commit + push this package to the working branch and `main` (fast-forward), including this report and
the index update, per the standing instruction; verify `main == origin/main` and a clean tree.
