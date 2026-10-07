# TYPE-B CASE REASSESSMENT — WP-1.9.25 Phase 3

> **Tóm tắt (VI):** Không ca nào đủ điều kiện **ENCODER_FALSE_EVIDENCE_SUPPORTED**. Kết quả:
> **LABEL_LIMITED 2** (014180143_15, 014350146_16 — chỉ có score-0), **MIXED 2**
> (child_07_seven: nhãn PROBABLY_ABSENT HIGH nhưng đỉnh 1-frame cô lập, encoder thứ hai còn mạnh hơn
> 0,63→0,94; 014190172_7: score-0 + đỉnh cô lập + encoder thứ hai giảm 0,82→0,41).
> Không dùng score thấp/cao để kết luận; dùng bằng chứng thời gian + phonetic.

Allowed classifications: ENCODER_FALSE_EVIDENCE_SUPPORTED · ENCODER_FALSE_EVIDENCE_NOT_SUPPORTED ·
LABEL_LIMITED · REPRESENTATION_DEPENDENT · MIXED · INCONCLUSIVE.

## 1. Case cards (full data in `TYPE_B_CASES.csv`)

### child_07_seven — /n/, "seven" (LWE, child_07)
- Human: FINAL_CONSONANT_PROBABLY_ABSENT, HIGH, WP-1.9.12 blind listening review.
- Production: accepted (exact); max_A 0.629; mean_A 0.015; peak frame 40/49 at 805 ms, span 4–48;
  competitor 0.243 at peak; peak margin +0.387; blank at peak 0.092; cluster width 1;
  neighbors 0.024 / 0.005 (isolated one-frame spike).
- Alternative encoder: **0.940** (stronger than production 0.629).
- Label-limited: no. Representation-limited: no (evidence strengthens). Isolated peak: **yes**.
- **Classification: MIXED** — confident ABSENT label vs a temporally isolated spike that another
  encoder strengthens; this is not a robust "encoder false evidence" case, but it cannot be dismissed
  either.

### 014180143_15 — /n/, "JUNE" (so762 absent-enriched, 014180143)
- Human: score-0 only (expert per-phone score; NOT a listening label).
- Production: accepted (exact); max_A 0.682; mean_A 0.021; peak margin +0.180; blank at peak 0.258;
  neighbor support 0.272 (not isolated).
- Alternative encoder: 0.248 (drops by 64%).
- **Classification: LABEL_LIMITED** — the case cannot be called encoder false evidence without a
  confident human listening label; the representation shift is also evidence of encoder dependence.

### 014190172_7 — /m/, "NAME" (so762 absent-enriched)
- Human: score-0 only.
- Production: accepted (exact); max_A 0.820; mean_A 0.213; peak margin +0.175; blank at peak 0.013;
  cluster width 1; neighbors 0.029 / 0.001 (isolated).
- Alternative encoder: 0.409 (drops by 50%).
- **Classification: MIXED** — label-limited and temporally isolated and representation-dependent.

### 014350146_16 — /n/, "EDUCATION" (so762 absent-enriched)
- Human: score-0 only.
- Production: accepted (exact); max_A 0.956; mean_A 0.020; peak margin +0.864; blank at peak 0.031;
  neighbors 0.235 / 0.249 (temporal support present, longest run 3).
- Alternative encoder: 0.910 (persists).
- **Classification: LABEL_LIMITED** — the strongest and most temporally supported case, but it has no
  confident human listening label; cannot be promoted to encoder false evidence.

## 2. Related strong false-evidence candidate (not TYPE-B)

`014470150_3` (L13, so762 absent-enriched, /t/): max_A 0.687, rank −1, production **rejects** it
(miss) because it has no identity credit. It is strong false evidence that the acceptance layer
already rejects — useful for the encoder hypothesis but not a false accept.

## 3. Summary

| classification | n | cases |
|---|---:|---|
| ENCODER_FALSE_EVIDENCE_SUPPORTED | 0 | – |
| ENCODER_FALSE_EVIDENCE_NOT_SUPPORTED | 0 | – |
| LABEL_LIMITED | 2 | 014180143_15, 014350146_16 |
| MIXED | 2 | child_07_seven, 014190172_7 |
| REPRESENTATION_DEPENDENT | 0 (as sole class) | – |
| INCONCLUSIVE | 0 | – |

- Isolated one-frame peaks: 2/4 (child_07_seven, 014190172_7).
- No confident listening label: 3/4.
- Alternative encoder preserves ≥0.1 in 4/4 but drops magnitude by ≥50% in 2/4.
- No case meets all encoder-side failure criteria (Part 8 of WP-1.9.24) or all falsification tests.

**Consequence:** the encoder false-evidence hypothesis remains unresolved by labels; the
no-evidence limitation (WP-1.9.24) is unaffected and still supported.
