# SESSION REPORT — WP-1.9.18 (DELETION-AWARE EVIDENCE & DECISION LAYER)

> **Tóm tắt (VI):** Xây lớp quyết định research-only PRESENT/ABSENT/UNCERTAIN cho phone cuối, không
> ép span, dùng E (deletion-aware evidence) + margin; variants A/B/C/D; dev/test so762 speaker-disjoint
> + LWE external. Kết quả trên 28 nhãn LWE: baseline recall 0.9375/absent-FP 0.4167; deletion-aware
> free 0.5625/0.1667; floor 0.30 → 0.625/0.0833; **không tồn tại operating point recall≥0.95 hay
> absent-FP=0**; 3 absent /r/ được sửa nhưng 5 present không bằng chứng bị reject. **GATE
> ALIGNMENT_DELETION_GATE_FAIL**; next: boundary/window + nhãn. Commit `c611724` (đã push main).

- **Date:** 2026-10-03
- **Machine:** ASUS
- **Base:** WP-1.9.17 (same session line)
- **Branch:** `ux/math-arenas-hotfix-20260930` (tip = main)
- **Scope:** research-only; no production change; no training/B2
- **Flags:** all five false.

## 1. Implemented (research-only)
- `experiments/deletion_aware_decision.py`: imports `PhoneEvidenceV2` read-only; replaces only the
  decision layer for the final phone.
  - Baseline decision replicated from `soft_match` (**0/80 mismatches vs 1.9.12 archived**).
  - Deletion-aware: blank-interleaved (2L+1) Viterbi with skips; per final phone:
    `E = da_span_max` (max class posterior at the assigned final-phone frame(s)),
    `margin` LLR/frame (global path comparison — documented unreliable near trailing silence),
    `free` presence, frame/greedy stats, GOP-ratio.
- Decision variants: **A** baseline; **B** deletion-aware free (`free` ⇒ PRESENT); **C** recall-first
  (E ≥ τp / E < τa / else UNCERTAIN); **D** safety-first (PRESENT E ≥ τp; ABSENT E < τa ∧ free-deleted
  ∧ margin ≤ 0; else UNCERTAIN). Thresholds to be selected on dev; diagnostic points labelled.

## 2. Data (speaker-disjoint)
| set | corpus | speakers | utt | final phones | present | absent |
|---|---|---|---:|---:|---:|---:|
| dev | so762 train children | 24 | 480 | 1,339 | 1,322 | 17 |
| test (held out) | so762 test children | 24 | 480 | 1,305 | 1,262 | 43 |
| external | LWE blind finals (1.9.12) | 9 | 80 | 28 | 16 | 12 |

- Overlap dev×test = 0; LWE never used for selection; 19 human-uncertain LWE tokens kept separate.
- Label caveat: so762 score 0 = "incorrect **or missed**" (deletion + substitution mixed).

## 3. Measurements (LWE 28; recall = P(PRESENT|present), absent-FP = P(PRESENT|absent))
| variant | present recall | absent-FP | absent-det | unsupported |
|---|---:|---:|---:|---:|
| A baseline | **0.9375** | 0.4167 | 0.5833 | 0.3214 |
| B deletion-aware free | 0.5625 | 0.1667 | 0.8333 | 0.0 |
| C floor 0.30 (diagnostic) | 0.6250 | 0.0833 | 0.9167 | 0.0 |
| D floor 0.30/0.10 (diagnostic) | 0.6250 | 0.0833 | 0.7500 | 0.0 |

- Dev/test follow the same trade (dev baseline 0.8896/0.7059; test 0.8693/0.4651; C floor 0.30
  test 0.649/0.116).
- **Threshold selection found no valid operating point:** recall ≥0.95 unattainable (present E p5 = 0.0),
  absent-FP = 0 unattainable (dev absent E max 0.956).
- E distributions show the cause: present E p25 = 0.029 (LWE) / 0.095 (dev); absent E p90 = 0.154
  (LWE) / 0.799 (dev); 6/16 LWE present tokens have E < 0.09 (baseline accepted them on top-1
  identity, posterior down to 0.0007).

## 4. Failure replay under diagnostic D
- **Fixed 3:** child_01_four, child_03_four, child_06_four (absent /r/ → ABSENT_DELETION).
- **Still wrong 2:** child_07_one (present /n/, E 0.0005; window inversion full 0 vs raw 72.5),
  child_07_seven (absent with E 0.63; acoustic/annotation conflict).
- **Regressed 5 (FRR cost):** present tokens without evidence (01_seven, 01_ten, 02_ten, 04_four,
  06_six) rejected by the floor; 2 absent → UNCERTAIN.
- Human-uncertain handling: baseline PRESENT 18/19; D reduces to 12 PRESENT / 5 ABSENT / 2 UNCERTAIN.

## 5. Root cause (falsification update)
1.9.17 prior DELETION 3 / MIXED 2 / ALIGNMENT 1 → confirmed as mechanisms, but **fixing deletion
does not yield an FRR-first-safe improvement**: every absent-detection gain costs present recall
(0.9375 → 0.625). Primary actionable bottleneck shifted to the **evidence itself** (acoustic
sensitivity/context) plus label limits; alignment remains a real but bounded mechanism.

## 6. Gate
**ALIGNMENT_DELETION_GATE_FAIL** (criteria 3 & 4 fail; no valid operating point).
Recommended next: **B** bounded boundary/window experiment (child_07_one; 21/80 inversions) +
**C** more confidently-labeled present finals (esp. /r/, n=1 LOW); then re-assess **A** (encoder work).
No B2, no production change, no Unity.

## 7. Artifacts (committed in `c611724`)
```
Research/Speech/Phase1_9_18/
  DELETION_AWARE_EVIDENCE_RESULTS.md, FAILURE_CASE_ANALYSIS.csv (80), EXPERIMENT_RESULTS.json,
  DECISION_TRACE_EXAMPLES.md, NEXT_GATE_DECISION.md,
  experiments/ (deletion_aware_decision.py, variants_v2.py, analysis_and_export.py),
  artifacts/ (lwe_phone_evidence.csv, so762_dev_finals.csv, so762_test_finals.csv)
```

## 8. Git
- Committed with 1.9.17 as `c611724` and pushed to `origin/ux/math-arenas-hotfix-20260930` and
  `origin/main` (fast-forward).
