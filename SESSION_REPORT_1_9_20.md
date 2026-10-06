# SESSION REPORT — WP-1.9.20 (ALIGNMENT REPRESENTATION AUDIT & COUNTERFACTUAL REALIGNMENT)

> **Tóm tắt (VI):** Audit representation/alignment trên frozen logits (không train). 5 họ alignment
> (A current, B blank-aware, C skip, D evidence-weighted, E conservative) chạy trên evaluation sets
> của 1.9.19 (80 LWE + 214 utt so762; không có logits cache nên recompute tối thiểu ~12 phút).
> Kết quả: counterfactual cứu **0/9** ca "ALIGNMENT"; **8/9 là NOT_ALIGNMENT** (evidence đã nằm
> trong span; nhãn cũ do so sánh max-vs-mean), 1 INCONCLUSIVE (child_01_nine — 0.979 là occurrence
> khác, trước span). Negative control **FAIL** (false recovery 11 > 8 trên 29 absent). Gate
> **ALIGNMENT_REPRESENTATION_FAIL**; B2 NOT YET.

- **Date:** 2026-10-03
- **Machine:** ASUS · **Branch tip:** `6fc7158` (= main) · **Scope:** research-only
- **Flags:** all five false. **Git:** tự động commit/push theo lệnh thường trực của user.

## 1. Mission
Xác định counterfactual realignment (giữ frozen model/audio/evidence) có sửa được các
alignment failure speaker-disjoint, FRR-first mà không tạo false PRESENT không.

## 2. Implementation
- `experiments/alignment_counterfactual.py`: A `ctc_align` production (read-only); B blank-aware
  2L+1 (không skip); C skip với penalty δ ∈ {0, .5, 1, 2, 4} (normalized emissions);
  D C + λ·GOP (λ ∈ {.5, 1}); E C + conservative rule (0.5 present / 0.2–0.5 uncertain).
  Giữ monotonicity/order/blank structure; chỉ thêm quyền skip có cost.
- `experiments/analyze_alignment.py`: chọn variant trên dev (recovery subject to false-recovery),
  freeze, đánh giá held-out test + LWE, reclassify 9 case, negative control, xuất outputs.
- Recompute: logits không được cache ở 1.9.17–19 (chỉ summary rows) → chạy encoder đúng phạm vi
  evaluation sets cũ: 80 LWE + 96 dev + 96 test + 22 absent-dev utt.

## 3. Results
- Dev selection: `C_skip_d1.0` (recovery 35/55) nhưng false-recovery 58.8% (metric "evidence
  outside span" bị nhiễu bởi occurrence khác của cùng phone class).
- Reclassification 9 ca ALIGNMENT cũ: **FIXED 0, PARTIAL 0, NOT_ALIGNMENT 8, INCONCLUSIVE 1**.
  8 ca có old span max 0.49–0.96, evidence outside ≈0 → lỗi thật là **scoring/aggregation
  (mean pha loãng evidence spiky)**, không phải alignment. `child_01_nine`: frame 0.979 ở frame
  12 (< span 16–42) = occurrence trước đó, counterfactual xoá phone đúng thay vì teleport.
- LWE 28: old production recall 0.9375 / absent-FP 0.4167; new A ≡ C = 0.5625 / 0.0833
  (khác biệt do decision rule floor 0.30, không do alignment; E_cons UNCERTAIN 0).
- Negative control: 29 absent → old false 8, **new false 11 (FAIL)**.
- Speaker-disjoint PASS; temporal plausibility PASS (monotone by construction).

## 4. Gate
**ALIGNMENT_REPRESENTATION_FAIL** — alignment không phải binding constraint cho các ca đã điều tra.
Primary conclusion MIXED (scoring/aggregation + occurrence/window/encoder). B2 **NOT YET**.
Next: research-only aggregation/support rule (span max/support thay mean, FRR-first) + nhãn
present finals tin cậy (đặc biệt /r/, n=1 LOW) + word-position labels. /r/ vẫn data-limited.

## 5. Artifacts (committed)
```
Research/Speech/Phase1_9_20/
  ALIGNMENT_COUNTERFACTUAL_REPORT.md, ALIGNMENT_CASE_ANALYSIS.csv (80),
  ALIGNMENT_TRACE.csv (347), ALIGNMENT_VARIANT_RESULTS.json,
  ALIGNMENT_FAILURE_RECLASSIFICATION.csv (9), NEXT_GATE_DECISION.md,
  experiments/ (2), artifacts/alignment_rows.csv (609)
```

## 6. Standing instruction
Từ WP-1.9.20: mỗi work package hoàn tất sẽ **tự động commit + push** lên nhánh làm việc và
`main` (fast-forward), kèm `SESSION_REPORT_<ID>.md` ở thư mục gốc và cập nhật index — không hỏi lại.
