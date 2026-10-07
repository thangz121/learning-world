# /r/ LABEL REQUIREMENTS — WP-1.9.27 Part 23

> **Tóm tắt (VI):** Yêu cầu nhãn /r/: tối thiểu 15 PRESENT + 15 ABSENT, >=15 speaker,
> speaker-disjoint, ưu tiên weak + isolated peak; hiện có 1 PRESENT LOW / 5 ABSENT HIGH / 3 chưa
> nhãn trong 9 ca LWE + 15 ca SO762 (score, không phải nhãn nghe). Không kết luận /r/ trước khi đủ.

## R_MINIMUM

| requirement | value | rationale |
|---|---|---|
| PRESENT labels | >=15 | +/-0.20 Wilson CI at p=0.8 |
| ABSENT labels | >=15 | symmetric control |
| speakers | >=15 | prevent single-speaker dominance |
| speaker-disjoint | yes | train/dev/test must not share speakers |
| ages | 4–8 preferred | local LWE children are 4–6; SO762 6–15 |
| difficulty mix | weak + isolated peaks included | the actual failure population |
| contexts | multiple (ɔ/ɝ/ɑ) + word/utterance-final | avoid one allophone |

## Current inventory (1.9.26/1.9.27)

- 24 /r/ cases: 9 LWE + 15 SO762, 19 speakers, 20/24 isolated peaks.
- LWE: 1 PRESENT LOW, 5 ABSENT HIGH, 3 unlabelled; median max_A 0.041.
- SO762: 11 score-2.0, 1 score-0.0, 3 intermediate (expert scores, NOT listening labels).
- Pack R pools A/B/C carry 24 /r/ candidates for the review round; Pack P adds 8 pilot /r/ tokens.

## Instructions to reviewers for /r/

Judge whether there is a rhotic constriction/lowering/colouring; a short or weakly rhotic production
still counts as PRESENT if judged perceptible. Do not require a full adult rhotic. If the token is
vowel-only or the final region is silent, ABSENT. If you cannot decide, UNCERTAIN.

## What would count as "R_LABELS_SUFFICIENT"

See `03_LABEL_ANALYSIS/LABEL_SUFFICIENCY_GATE.md`: >=15 PRESENT + >=15 ABSENT consensus labels
across >=10 speakers. Until then: no /r/ detector, no /r/ claim, and /r/ remains reported as
under-labelled in every B2 document.
