# LABEL COLLECTION REPORT — WP-1.9.25 Phase 1/2

> **Tóm tắt (VI):** Không có reviewer trong session → **0 nhãn mới**, không tạo nhãn giả. Pack 23 ứng
> viên (L01–L23) giữ nguyên trạng thái READY_FOR_HUMAN_REVIEW. Audit chất lượng nhãn: 28 nhãn LWE là
> **listening labels** (single reviewer, HIGH 23 / MEDIUM 3 / LOW 2); so762 là **expert phone scores**
> (không phải listening labels). Không thể tính agreement (không có reviewer thứ hai). 3/4 ca TYPE-B
> chỉ có score-0. Đây là blocker ngoại vi chính.

**Starting gate:** LABELS_INSUFFICIENT_ENCODER_HYPOTHESIS_SUPPORTED
**No fabrication, no score-derived labels promoted to listening labels, no MAYNODE evidence.**

## 1. Evidence map (Phase 0)

| category | what exists | source |
|---|---|---|
| Human listening labels | 28 LWE blind final-consonant labels: 16 PRESENT / 12 ABSENT; confidence HIGH 23 / MEDIUM 3 / LOW 2; single reviewer `human_mobile` | WP-1.9.12 `final_consonant_human_review.csv` |
| Dataset labels | SO762 per-phone expert scores 0/1/2 (not listening labels); SIAK word scores 0–100; LWE has no phone labels | local corpora |
| Machine labels | production decisions for 609 primary-window tokens; 68 acceptance rules; two espeak encoders | WP-1.9.21–1.9.24 |
| Ambiguous cases | 4 unresolved TYPE-B; 6 LWE word-level AMBIGUOUS tokens | WP-1.9.22–1.9.24 |
| /r/ evidence | 24 cases (9 LWE: 1 PRESENT LOW / 5 ABSENT / 3 unlabelled; 15 so762) | WP-1.9.25 `R_CASES.csv` |
| Alternative encoder | lv-60-espeak counterfactual, 47 rows | WP-1.9.24 `alt_encoder_counterfactual.csv` |
| Dataset/license | 18 candidate datasets audited | WP-1.9.25 `DATASET_LICENSE_AUDIT.csv` |

## 2. Review status

- The WP-1.9.24 blinded pack (L01–L23) was loaded unchanged; no candidate was regenerated.
- `serve_review.py` is hard-coded to the WP-1.9.15 `fidelity_v2` pack (`ALLOWED_PACKS`); a minimal
  adaptation or the WP-1.9.12 blind HTML would be needed to serve this pack. No reviewer was
  available in-session to use either path.
- **NEW_LABELS_COLLECTED = 0.** `HUMAN_LABEL_RESULTS.csv` is created with the full schema and zero
  rows. No label was derived from machine output; no score-0 was converted to a listening label.

## 3. Priority mapping (for the human review round)

| priority | blind IDs | content |
|---|---|---|
| P0 | L11, L12, L14, L15 | the 4 unresolved TYPE-B cases (strong false-evidence candidates) |
| P1 | L01–L10, L20–L23 | /r/ present candidates (L01–L03, L20–L23), weak PRESENT (L04–L05), max_A<0.02 with independent evidence (L07 max_A 0.00002), rank-2..5 identity (L06–L10) |
| P2 | L13, L16–L19 | additional strong false-evidence candidate (L13, production already rejects it), /r/ ABSENT controls (L16–L19) |

## 4. Label quality audit

| label set | type | reviewer count | confidence | relevance to TYPE-B |
|---|---|---|---|---|
| LWE 28 blind labels | listening | 1 (single reviewer) | HIGH 23 / MEDIUM 3 / LOW 2 | `child_07_seven` PROBABLY_ABSENT HIGH is the only confident TYPE-B label |
| SO762 phone scores | expert scores, 5 experts per corpus paper | score-derived | n/a | 3 TYPE-B cases have score-0 only |
| SIAK word scores | single expert 0–100 | score-derived | n/a | not used for TYPE-B |
| `child_07_seven` | listening | 1 | HIGH | the only TYPE-B case with a confident listening label |

**Reviewer-confidence matrix (TYPE-B relevance):** child_07_seven = listening/HIGH (label-strong but
temporally isolated); 014180143_15 / 014350146_16 / 014190172_7 = score-0 only (no listening
confidence). `SINGLE_REVIEWER_LIMITATION = TRUE` for the historical labels; no agreement statistic is
computable (`REVIEWER_AGREEMENT.csv` documents this explicitly).

## 5. What the labels would resolve

1. TYPE-B: are the 4 strong-evidence cases true encoder false evidence or label errors?
2. Weak/blank-dominated false accepts: true weak productions vs acceptance-layer false positives.
3. /r/: is the frozen encoder's near-zero /r/ evidence an encoder limit or a label/data limit?
4. rank-2..5 identity accepts: how often the similarity pick is right.

## 6. Files

```
HUMAN_LABEL_RESULTS.csv   empty schema (0 rows)
REVIEWER_AGREEMENT.csv    no-reviewer + single-reviewer documentation
HUMAN_LABEL_REVIEW_PACK_FINAL.csv (WP-1.9.24, L01-L23, unchanged)
```
