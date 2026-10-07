# B2-D DESIGN (FROZEN — NO TRAINING) — WP-1.9.27 Part 14

> **Tóm tắt (VI):** B2-D = so sánh encoder thay thế (không huấn luyện). Khóa: đầu vào 16 kHz mono;
> inventory production; cùng code alignment/DP; cùng aggregation (mean/max trong span);
> acceptance chỉ để nghiên cứu; tập đánh giá = 546 token pilot + 609 token chẩn đoán; metrics
> missing/false//r//agreement/per-speaker. Giả thuyết: representation khác có giảm lỗi
> representation-dependent không. Câu trả lời hiện tại: hai encoder gần như tương đương tổng thể.

## Frozen design

| element | value |
|---|---|
| production encoder | facebook/wav2vec2-xlsr-53-espeak-cv-ft (Apache-2.0) |
| alternative encoder | facebook/wav2vec2-lv-60-espeak-cv-ft (Apache-2.0, local) |
| input | 16 kHz mono; no augmentation; same audio bytes for both |
| phone inventory | production inventory adapter (shared canonization/mapping) |
| alignment | same DP/CTC alignment code for both encoders |
| evidence extraction | target-class posterior max/mean inside the production span; rank; competitor; blank |
| aggregation | mean (baseline) + max (control); no new aggregation families inside B2-D |
| acceptance | research-only comparison; production acceptance logic NOT modified |
| evaluation set A | pilot 546 tokens, speaker-disjoint test split (labels required) |
| evaluation set B | the 609-token diagnostic set (already measured in WP-1.9.26) |
| metrics | missing-evidence rate, false-evidence rate, agreement@0.10, per-speaker spread, /r/, isolated peaks |

## Primary hypothesis

A different speech representation reduces representation-dependent failures for child final
consonants (missing evidence, weak/isolated evidence, /r/) without a material increase in false
evidence.

## Known evidence before the pilot (no winner)

| metric | production xlsr-53 | alt lv-60 |
|---|---:|---:|
| missing-evidence (present, max_A<0.02) | 16.48% | 18.75% |
| false-evidence (absent, max_A>=0.30) | 17.24% | 13.79% |
| present median max_A | 0.7919 | 0.7923 |
| agreement @0.10 | 82.76% | – |
| gains / losses | – | 50 / 55 |

Pilot features for both encoders are extracted (`pilot_features.csv`), so B2-D reads out as soon as
Pack P labels exist. A third encoder may only be admitted if locally available, warehouse-compatible
(Apache-style), and measured on the same frozen sets.

## Explicit non-goals

- No encoder fine-tuning, no LoRA, no head training, no model replacement.
- No production acceptance change; no Unity; no threshold tuning against test.
- B2-D alone cannot claim production readiness.
