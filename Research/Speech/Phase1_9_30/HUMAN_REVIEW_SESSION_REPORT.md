# HUMAN REVIEW SESSION REPORT — WP-1.9.30 (session 1, REAL)

> **Tóm tắt (VI):** Reviewer người thật REV-A đã hoàn thành TOÀN BỘ Pack R (276/276, 0 trùng lặp)
> qua server mù. Nhãn: 243 PRESENT / 17 ABSENT / 16 UNCERTAIN. Đã import (276 valid, 0 rejected),
> lưu hash raw. Kappa: không tính được (một reviewer — SINGLE_REVIEWER). Không đánh giá model.

## Session

| field | value |
|---|---|
| DATE | 2026-10-08 |
| MACHINE | ASUS |
| REVIEWER ID | `REV-A` (genuine, do người thật tự nhập; không phải QA/synthetic) |
| PACK | Pack R — 276 candidates |
| START | 2026-10-08T03:48:55Z (submission đầu) |
| END | 2026-10-08T05:33:51Z (submission cuối) |
| SERVER | `serve_review_1927.py`, port 8791, blind mode |

## Counts

| metric | value |
|---|---:|
| candidates presented | 276 (toàn bộ pack được phục vụ) |
| candidates completed | **276** |
| candidates remaining (Pack R) | 0 |
| remaining other pack | Pack P 546/546 (chưa review) |
| PRESENT | 243 |
| ABSENT | 17 |
| UNCERTAIN | 16 |
| ASSESSABLE | 273 |
| NOT_ASSESSABLE | 3 |
| duplicates | 0 |
| invalid / failed submissions | 0 / 0 |

## Integrity audit

| check | result |
|---|---|
| BLINDNESS | PASS — payload chỉ `blind_id, word, target_phone`; export không chứa trường máy |
| AUDIO MAPPING | PASS — audio phục vụ đúng blind_id (verified pre-session + spot-check) |
| SCHEMA | PASS — `blind_id,label,confidence,assessable,note,ts`; 276/276 valid, 0 rejected |
| RAW PRESERVED | REV-A.jsonl sha256 `4D702308...E5BB11`; export snapshot `7E150547...962474`; bản root `7DDA7784...46F4` (`artifacts/review_raw/RAW_PROVENANCE.json`) |
| DUPLICATE PROTECTION | PASS — 276 unique, 0 duplicate |

## Session 2 — focused quality pass, frozen test split (`REV-A-S2`, 2026-10-08)

| field | value |
|---|---|
| ID | `REV-A-S2` (SAME human as REV-A — NOT an independent reviewer) |
| pack | `PILOT_TEST_SPLIT_99.csv` (99; sha256 `747DBFFF...54DE2`), speakers 1075/1076 |
| start/end | 2026-10-08T07:49:03Z → 08:05:41Z (~16 min) |
| completed | **99/99**, 0 duplicates, 0 invalid |
| labels | 70 PRESENT / 27 ABSENT / 2 UNCERTAIN |
| confidence | 81 HIGH / 14 MEDIUM / 4 LOW |
| assessable | 99 ASSESSABLE / 0 NOT_ASSESSABLE |
| raw | `artifacts/reviews_s2/REV-A-S2.jsonl`; snapshot `.../exports/REV-A-S2_20261008T080544Z.csv` |
| intra-rater check (18 overlapping test cases) | 16 unchanged; 1 ABSENT→PRESENT; 1 PRESENT→ABSENT | 
| combined Pack P so far (S2 test supersedes old test; old dev/train kept) | 164/546: **126 PRESENT / 30 ABSENT / 8 UNCERTAIN** |

Caveat: criterion revision by the same human; S2 is a revised single-reviewer set, not a second
opinion. Old labels are archived unchanged; downstream analysis must never count REV-A and REV-A-S2
as two independent reviewers.

## Sub-gate snapshot (single reviewer — chưa tính là consensus)

| gate | current | required | status |
|---|---|---|---|
| overall PRESENT | 243 | >=60 | met |
| overall ABSENT | 17 | >=60 | **NOT met (Pack R)** |
| /r/ PRESENT | 22 (18 speakers) | >=15 | met |
| /r/ ABSENT | 1 | >=15 | **NOT met** |
| TYPE-B decisive | 4 labeled like REV-A (single) | 4/4 with >=2-reviewer consensus | pending REV-B |
| pilot test split (Pack P) | 0/99 | valid labels | **NOT met** |
| agreement | not estimable | >=2 reviewers | pending REV-B |

TYPE-B single-reviewer labels (HIGH confidence): `child_07_seven` PRESENT, `014180143_15` ABSENT,
`014190172_7` PRESENT, `014350146_16` ABSENT. Gate still 0/4 until an independent REV-B label exists.

## Reviewer self-observation (recorded 2026-10-08, before session 3)

The reviewer (REV-A, same human) reported possible leniency on confusable final phones
(/n/, /z/, /b/) and asked to condense the remaining scope for quality. Handling:
- All existing labels are preserved unchanged; nothing relabeled or repaired.
- A short, quality-focused pass over the **frozen test split (99 candidates, speakers 1075/1076)**
  is run under a NEW session ID (`REV-A-S2`, separate reviews dir) — deterministic pre-registered
  subset (split == test), not score/difficulty-selected.
- This is the SAME human in a new session, NOT an independent second reviewer; analysis must keep
  `SINGLE_REVIEWER_LIMITATION`, and the criterion-revision fact is recorded as a label-quality caveat.
- New labels are recorded as-is, whatever their distribution; no quota is targeted.

## Next review batch

1. **REV-B** opens the same URL, enters own ID, reviews Pack R independently (same 276; different
   shuffle; no access to REV-A labels).
2. Then **REV-A and REV-B both review Pack P** (546; test split 99 first) using
   `serve_review_1927.py 8791 --pack Research/Speech/Phase1_9_27/06_PILOT_DATA/PILOT_REVIEW_CANDIDATES.csv`.
3. Note for planning (not a rule change): Pack R yielded only 17 ABSENT; the frozen 60-ABSENT
   minimum will therefore depend on Pack P's natural distribution. The gate is not weakened.

No model prediction, score, or decision appears in this report or in reviewer-facing material.
