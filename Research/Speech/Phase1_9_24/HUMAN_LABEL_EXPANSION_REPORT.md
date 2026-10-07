# HUMAN LABEL EXPANSION REPORT — WP-1.9.24 Part A

> **Tóm tắt (VI):** Phân loại 23 ứng viên WP-1.9.23 thành 11 PRESENT-candidate, 9 ABSENT-candidate,
> 3 UNCERTAIN-candidate (theo thứ tự ưu tiên /r/ → weak present → rank-2..5 → strong false-accept →
> /r/ absent → strong /r/ present). Đóng gói lại thành pack mù (blind_id L01..L23, che mọi trường
> machine cho tới khi ghi nhãn). Kiểm tra tooling: `serve_review.py` hard-code cho pack fidelity_v2 →
> cần adaptation nhỏ hoặc dùng HTML blind của WP-1.9.12. **Không có reviewer trong session →
> NEW_LABELS_COLLECTED = 0; không tạo nhãn giả.** Nhãn hiện có vẫn là 28 LWE blind (/r/ 1P/5A,
> single-reviewer). Đây là blocker ngoại vi chính.

**Machine:** ASUS · **Scope:** research-only · **Flags:** all five false · **No B2, no production change**

## 1. Pack classification (23 candidates)

Source: `Research/Speech/Phase1_9_23/HUMAN_LABEL_REVIEW_PACK.csv` (loaded, not regenerated).
Final blinded pack: `HUMAN_LABEL_REVIEW_PACK_FINAL.csv` (`blind_id` L01–L23, `review_order`,
`candidate_class`, four priority tags, `hidden_until_label` list).

| class | n | notes |
|---|---:|---|
| PRESENT_CANDIDATE | 11 | weak-support present (2, word verdict positive) + rank-2..5 identity accepts (5, so762 score present) + strong /r/ present reference (4) |
| ABSENT_CANDIDATE | 9 | strong false-accept candidates (5, TYPE-B score-0) + additional /r/ absent (4) |
| UNCERTAIN_CANDIDATE | 3 | unlabelled LWE `four` /r/ tokens (word verdicts negative; the only /r/ present candidates available) |

Priority tags: `tag_r_priority` 7 (3 LWE four + 4 strong /r/ present), `tag_weak_present` 2,
`tag_rank25_identity` 5, `tag_strong_false_accept` 5.

## 2. Review tooling inspection

- `Research/Speech/Phase1_9_15/experiments/serve_review.py`: serves `HumanReview/` over LAN and
  accepts POST `/submit`; `ALLOWED_PACKS = {"fidelity_v2"}` — **hard-coded to the WP-1.9.15 pack**.
  It cannot serve the WP-1.9.24 pack without a small adaptation (new pack dir + allowed-pack entry).
- Alternative already in repo: the WP-1.9.12 standalone blind HTML
  (`Phase1_9_12/HumanReview/final_consonant_blind.html`) uses the same blind-clip pattern.
- Because no human reviewer is available in this session, neither path was executed.

## 3. Protocol (reviewer instructions)

`HUMAN_LABEL_REVIEW_INSTRUCTIONS.md` (from WP-1.9.23) applies, with the final pack fields:

1. Reviewer listens **without seeing machine fields** (`hidden_until_label`: rank, max_A, blank,
   margin, production/rule decisions, reference note). Only `blind_id`, `word`, `target_phone`,
   `audio_reference` are visible.
2. Labels: PRESENT / ABSENT / UNCERTAIN; confidence HIGH / MEDIUM / LOW; optional note.
3. Question: "Can I reasonably hear/identify the target final consonant?" — not "does the model say
   it is there?". A weak but plausible child production may be PRESENT; UNCERTAIN is legitimate.
4. No label may be derived from machine output. Historical labels are not modified.

## 4. Label quality and agreement

- "Confident PRESENT" = PRESENT with HIGH confidence, or explicitly defensible MEDIUM.
- **SINGLE_REVIEWER_LIMITATION = TRUE**: no second reviewer exists for the 28 historical LWE labels
  or for the new pack; no agreement statistic can be computed (0 new labels).
- **NEW_LABELS_COLLECTED = 0.** `HUMAN_LABEL_RESULTS.csv` is created with the required schema and
  zero rows; no fabricated labels.

## 5. Existing label audit (current authority set)

| set | present | absent | uncertain | /r/ |
|---|---:|---:|---:|---:|
| LWE blind final consonants (WP-1.9.12, single reviewer) | 16 | 12 | 2 AMBIGUOUS | 1 P (LOW) / 5 A |
| SO762 dev (phone score ≥ 0.5 = present) | 285 | 17 | – | few |
| SO762 test (speaker-disjoint) | 227 | 0 | – | few |

Additional labels needed (exact): 10–20 confident PRESENT finals (/r/ first), labels for the 3 TYPE-B
score-0 candidates, and labels for rank-2..5 identity accepts. Until then the TYPE-B false-evidence
question cannot be separated from label ambiguity.

## 6. Why this matters for Part B

The encoder audit (see `ENCODER_FAILURE_AUDIT.md`) shows the *missing-evidence* limitation is
supported by confident human labels (`child_06_six` PROBABLY_PRESENT HIGH, `child_07_one`
CLEARLY_PRESENT HIGH, both with max_A < 0.02), but the *strong false-evidence* cases cannot be
confirmed because 3/4 have no confident human listening label and 2/4 are isolated one-frame peaks.
Human label quality and encoder representation quality must stay separated.

## 7. Files

```
Research/Speech/Phase1_9_24/
  HUMAN_LABEL_EXPANSION_REPORT.md   this file
  HUMAN_LABEL_REVIEW_PACK_FINAL.csv 23 candidates, blinded, classified
  HUMAN_LABEL_RESULTS.csv           empty schema (NEW_LABELS_COLLECTED = 0)
  HUMAN_LABEL_REVIEW_INSTRUCTIONS.md (WP-1.9.23, applies)
```
