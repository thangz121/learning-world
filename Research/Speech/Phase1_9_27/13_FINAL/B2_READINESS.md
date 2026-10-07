# B2 READINESS — WP-1.9.27

> **Tóm tắt (VI):** B2 TRAINING = NO. B2 DESIGN = sẵn sàng cho pilot (B2-D/B2-E + tiêu chí + kill
> switch đã khóa), nhưng chưa đủ điều kiện để huấn luyện: thiếu nhãn người Pack P, thiếu route dữ
> liệu pháp lý cho scope thương mại, và B2-C cần GPU. Không có thay đổi production.

## Readiness checklist (from WP-1.9.24/25, updated)

| # | condition | status |
|---|---|---|
| 1 | production decision reconstructed exactly | YES (609/609) |
| 2 | identity contribution quantified | YES (WP-1.9.22/23) |
| 3 | false-acceptance mechanism quantified | YES (7 IDENTITY_WEAK / 4 IDENTITY_STRONG / 3 SOFT_SIMILARITY) |
| 4 | removing identity preserves FRR-first | NO (identity is load-bearing: recall 0.94 -> 0.0) |
| 5 | remaining errors plausibly encoder/acoustic | SUPPORTED, not proven; TYPE-B unresolved by labels |
| 6 | trusted labels for relevant phone classes | NO (0 new; 28 historical single-reviewer) |
| 7 | /r/ no longer data-limited | NO (1 PRESENT LOW; 15+15 required) |
| 8 | no unresolved scoring/acceptance defect | NO (acceptance+encoder both limiting; 0/68 safe rules) |
| 9 | pilot executed with frozen design | NOT YET (labels pending) |
| 10 | data/licence route for intended scope | NO (MyST quote pending; research routes only) |
| 11 | GPU available for adaptation | NO (ASUS CPU-only) |

## Verdict

**B2 TRAINING: NO. B2 DESIGN: pilot-ready but blocked on human labels.** The pilot is the next
scientific step; only a positive pilot plus a signed/cleared data route may open a future B2 work
package. No production change; no Unity; no fine-tuning.

## What B2 must change if it ever proceeds

- B2-D: representation choice for the missing-evidence failure mode (two candidates, no winner yet).
- B2-E: add phonetic/temporal evidence to recover weak finals and /r/ rhoticity without buying
  recall through false accepts (assessability gate mandatory).
- B2-C (future GPU): child-speech adaptation only after licensed data and a positive pilot.
