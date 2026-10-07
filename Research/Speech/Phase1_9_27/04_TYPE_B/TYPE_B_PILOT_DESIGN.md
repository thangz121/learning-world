# TYPE-B PILOT DESIGN — WP-1.9.27 Part 24

> **Tóm tắt (VI):** Cả 4 ca TYPE-B bắt buộc nằm trong pack đánh giá; pilot tương lai phải phân biệt
> LABEL-LIMITED vs ENCODER-LIMITED vs MIXED. Hiện trạng: 0/4 xác nhận, 2 LABEL_LIMITED + 2 MIXED,
> 3/4 representation-dependent; 4 nhãn nghe là bước quyết định.

## Cases (frozen list, full evidence in `TYPE_B_CASES.csv`)

| case | word / phone | human status now | prod max_A | alt max_A | current class |
|---|---|---|---|---|---|
| child_07_seven | seven / n | PROBABLY_ABSENT HIGH (listening) | 0.629 | 0.940 | MIXED (isolated spike) |
| 014180143_15 | june / n | score-0 only | 0.682 | 0.248 | LABEL_LIMITED |
| 014190172_7 | name / m | score-0 only | 0.820 | 0.409 | MIXED (isolated spike) |
| 014350146_16 | education / n | score-0 only | 0.956 | 0.910 | LABEL_LIMITED |

All 4 are in Pack R pool F (`F_typeb_strong_false`); P0 priority for reviewers.

## What the future experiment must test

For each case, after >=2 independent listening labels:

1. If both reviewers hear ABSENT with HIGH confidence AND strong evidence persists across encoders
   in the correct position: `ENCODER_FALSE_EVIDENCE_SUPPORTED`.
2. If reviewers disagree or are UNCERTAIN: `LABEL_LIMITED`.
3. If evidence appears/disappears with encoder/window: `REPRESENTATION_DEPENDENT`.
4. Mix of the above: `MIXED`; otherwise `INCONCLUSIVE`.

No classification may be forced; "low score" or "high score" alone is never evidence.

## Evidence card fields (per case, filled by `TYPE_B_ANALYSIS_SCHEMA.csv`)

human label + confidence; production evidence (max_A, mean_A, peak frame, peak width, blank at peak,
margin); alternative encoder evidence (max_A, delta); acoustic/temporal evidence (isolated peak,
neighbours, position); assessability; final interpretation with the allowed classes above.

## Current verdict (WP-1.9.26/1.9.27, unchanged by this WP)

TYPE-B remains unresolved by labels: 0/4 confirmed encoder false evidence. The encoder hypothesis on
the false-evidence side is neither confirmed nor refuted. The only confident label
(`child_07_seven`) is a one-frame spike whose evidence strengthens under the second encoder.
Next step: the P0 review labels. No production change; no training.
