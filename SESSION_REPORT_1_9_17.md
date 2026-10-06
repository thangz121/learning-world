# SESSION REPORT — WP-1.9.17 (DELETION / ALIGNMENT DIAGNOSTIC)

> **Tóm tắt (VI):** Chẩn đoán nguyên nhân lỗi phụ âm cuối trên 80 token LWE (12 PME + 6 SCORER_MISS
> + 28 nhãn mù). Phân rã ACOUSTIC/ALIGNMENT/DELETION/SCORING/THRESHOLD: 6 lỗi quyết định →
> **DELETION 3/6, MIXED 2/6, ALIGNMENT 1/6**; 21/80 token đảo pass/fail giữa full và raw window;
> 14/80 accept không có bằng chứng; forced alignment kéo span vào im lặng (child_01_nine 0.0011 vs
> 0.9793). **GATE A = ALIGNMENT/DELETION là bottleneck chính**; status
> `SPEECH_RESEARCH_REQUIRES_ALIGNMENT_WORK`. Commit `c611724` (đã push main).

- **Date:** 2026-10-03
- **Machine:** ASUS
- **Base:** Phase 1.9.16 `e8d96da`
- **Branch:** `ux/math-arenas-hotfix-20260930` (tip = main; không chuyển nhánh)
- **Scope:** research-only; **không commit** (work package rule); production untouched
- **Flags:** all five false.

## 1. Phase A — pipeline reconstruction (from source)
Source: `Phase1_4/SoftMatching/phone_evidence_v2.py` (`87254B7B…`, `PhoneEvidenceV2@1.4.0`,
`ctc_forced_v1`), `Phase1_4/PhoneInventory/inventory.py` (`97D07C12…`).
- logits: soundfile → 16 kHz → frozen wav2vec2-espeak CTC → softmax `[T,392]` (~50 fps).
- alignment: `ctc_align` monotone DP (stay/advance only, no blank, no skips) — **every target phone
  is forced to own ≥1 frame; deletion is not representable**.
- decision: `soft_match` — `exact` if top-1 identity or post ≥0.35; `soft` if post ≥0.25 or
  best_sim ≥0.35; else `miss`; score = 100 × mean(sim); confidence = mean_post × … .
- Where a final consonant can be lost: encoder (no evidence); span (assigned to a low-evidence
  region); match rule (top-1 identity acceptance with posterior 0.0007); mean over phones;
  the 50 threshold; window/boundary.

## 2. Phase B — failure replay (frame-level)
- Script: `experiments/del_alignment_diagnostic.py` → `artifacts/case_evidence.csv` (80 tokens ×
  span/DA/frame/greedy/window evidence), `FAILURE_CASE_ANALYSIS.csv` (80 rows).
- Primary labeled decision failures (28 labels): 6 disagreements.
  - `child_01_four`, `child_03_four`, `child_06_four` (/r/ absent): baseline soft/exact with E ≤0.06
    and no support → **DELETION**.
  - `child_02_four` (/r/ absent, E 0.154) and `child_07_seven` (/n/ absent, E 0.63, strong) →
    **MIXED** (weak evidence / acoustic-annotation conflict).
  - `child_07_one` (/n/ present): full 0.0 vs raw window 72.5 → **ALIGNMENT** (window/boundary).
- Correctly decided: 22/28 (baseline FRR 1/16, FAR 5/12).

## 3. Phase C — deletion-aware counterfactual
- Blank-interleaved (2L+1) Viterbi + skip transitions + margin LLR (research prototype from 1.9.14),
  same frozen logits/targets; no architecture change needed.
- 28 labels: baseline recall 0.9375 / absent-det 0.5833; DA free presence 0.5625 / 0.8333;
  margin ≥ −0.02 → 0.75 / 0.8333; **no threshold dominates baseline**; /r/ absent free-present
  rate 0.0 (but the single present /r/ also 0.0 → not separable, n=1).

## 4. Phase D — counterfactual scoring
- Evidence-floor sweeps (`span_max_post`): at baseline recall, absent detection only 0.25; improving
  absent detection to 0.75–0.92 costs recall (0.56–0.75) → **no scoring-only fix**.
- Exposed pathway: acceptance on top-1 identity with posterior 0.0007 (`child_06_six`); 14/80
  unsupported acceptances (5 labeled present → `OK_UNSUPPORTED`).

## 5. Phases E/F — aggregation + root cause
- By phone over 28 labels: /t/ perfect; **/r/ absent detection 0.20**; /n/ 0/1; word table included.
- Root-cause distribution (6 labeled decision failures): **DELETION 3/6 (50%), MIXED 2/6 (33.3%),
  ALIGNMENT 1/6 (16.7%)**; ACOUSTIC 0, SCORING 0, THRESHOLD 0.
- Inferred PME/SM consonant finals: DELETION 2, ALIGNMENT 1, ACOUSTIC 1.
- Process-level: 21/80 window inversions; 14/80 unsupported acceptances; 25/80 baseline-vs-DA
  disagreements; span collapse child_01_nine (0.0011 → 0.9793).

## 6. Phases H/I — assessability + reproducibility
- 0 assessability failures in this corpus (no refusal states; 19 human-uncertain kept separate);
  no phone score used as an assessability proxy.
- Model revision `2c733782…`; deterministic; audio gitignored; `.gitignore` untouched; no tracked
  file modified.

## 7. Gate
**GATE A — ALIGNMENT / DELETION IS PRIMARY BOTTLENECK** (with mixed caveats: 2/6; acoustic
insensitivity remains a secondary limit). Status **SPEECH_RESEARCH_REQUIRES_ALIGNMENT_WORK**.
Next recommendation: deletion-aware evidence/decision layer research, FRR-first, no B2.

## 8. Artifacts (committed later in `c611724`)
```
Research/Speech/Phase1_9_17/
  DELETION_ALIGNMENT_DIAGNOSTIC.md, FAILURE_CASE_ANALYSIS.csv (80), EXPERIMENT_RESULTS.json,
  NEXT_RESEARCH_RECOMMENDATION.md, experiments/ (2 scripts + utilities),
  artifacts/case_evidence.csv
```

## 9. Git
- Work package rule: no commit at execution time. Later committed with 1.9.18 as `c611724`
  ("phase1.9.17-1.9.18: deletion/alignment diagnostic + deletion-aware decision layer (gate FAIL)")
  and pushed to branch + `main`.
