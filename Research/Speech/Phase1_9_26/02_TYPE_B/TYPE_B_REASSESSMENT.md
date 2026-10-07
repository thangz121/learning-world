# TYPE-B REASSESSMENT — WP-1.9.26 Part 2

> **Tóm tắt (VI):** Với bằng chứng encoder mở rộng (609 token), 4 ca TYPE-B vẫn **0/4 xác nhận**:
> 2 LABEL_LIMITED, 2 MIXED; 3/4 label-limited, 2/4 isolated peak, 3/4 representation-dependent
> (|Δ alt| ≥ 0,30). Bảng đầy đủ: `TYPE_B_CASES.csv`, `TYPE_B_EVIDENCE_MATRIX.csv`.

| case | human | prod max_A | alt max_A | Δalt | isolated | label-limited | classification |
|---|---|---:|---:|---:|---|---|---|
| child_07_seven | PROBABLY_ABSENT HIGH (listening) | 0.629 | 0.940 | +0.311 | yes | no | MIXED |
| 014180143_15 | score-0 only | 0.682 | 0.248 | −0.434 | no | yes | LABEL_LIMITED |
| 014190172_7 | score-0 only | 0.820 | 0.409 | −0.411 | yes | yes | MIXED |
| 014350146_16 | score-0 only | 0.956 | 0.910 | −0.046 | no | yes | LABEL_LIMITED |

## Findings

1. **No case meets the encoder-side failure criteria** (confident ABSENT + strong evidence + correct
   position + no alternative explanation). The only confident label (`child_07_seven`) is a
   one-frame spike whose evidence strengthens under the second encoder — not a robust encoder
   false positive.
2. **Representation dependence is the norm, not the exception**: 3/4 cases shift by ≥0.30 under a
   second espeak encoder; across the full 609-token pool the encoders agree at the 0.10 level only
   82.8% of the time (50 gains vs 55 losses).
3. **The aggregate comparison does not crown a winner**: production missing-evidence 16.5% vs alt
   18.8%; production false-evidence 17.2% vs alt 13.8%; present median max_A 0.79 for both.
4. The related case `014470150_3` (L13, /t/, max_A 0.687) is strong false evidence that the
   **acceptance layer already rejects** (rank −1, production miss) — evidence that the acceptance
   layer and the encoder failure are separable.

## Verdict

TYPE-B remains **unresolved by labels**; the encoder hypothesis on the false-evidence side is
neither confirmed nor refuted. The four listening labels in the review pack (F pool) are the
decisive next step. No production change; no training.
