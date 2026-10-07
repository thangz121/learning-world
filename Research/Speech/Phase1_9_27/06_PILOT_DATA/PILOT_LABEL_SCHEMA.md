# PILOT LABEL SCHEMA — WP-1.9.27 Part 12

> **Tóm tắt (VI):** Nhãn pilot tách bạch 3 loại bằng chứng: HUMAN-LISTENING (thẩm quyền đánh giá),
> EXPERT (score có sẵn), AUTOMATIC (alignment/posterior). Schema nghe: PRESENT/ABSENT/UNCERTAIN/
> NOT_ASSESSABLE + HIGH/MEDIUM/LOW + note. Không nâng cấp tự động EXPERT/AUTOMATIC thành nhãn nghe.

## Label record (HUMAN-LISTENING)

| field | required | values |
|---|---|---|
| reviewer_id | yes | REV-A / REV-B / REV-C |
| blind_id | yes | P27-### (Pack P) or R### (Pack R) |
| token_id | yes (join key) | e.g. `014690004_2` — matches `pilot_features.csv` |
| word | yes | target word |
| target_phone | yes | e.g. t, n, ɹ |
| label | yes | PRESENT / ABSENT / UNCERTAIN / NOT_ASSESSABLE |
| confidence | yes | HIGH / MEDIUM / LOW |
| assessable | yes | ASSESSABLE / NOT_ASSESSABLE |
| note | no | free text |
| ts | yes | ISO-8601 |

Definitions are in `02_EXTERNAL_REVIEW/REVIEW_PROTOCOL.md`; the same definitions apply to Pack P.
`NOT_ASSESSABLE` means the recording cannot support a decision; it is not ABSENT and it must be
reported separately in every metric.

## Evidence classes (never mixed)

| class | source | allowed use |
|---|---|---|
| HUMAN-LISTENING | blind review (this pipeline) | evaluation authority (FRR-first, /r/, TYPE-B) |
| EXPERT | SO762 phone scores, SIAK word scores | auxiliary evidence only |
| AUTOMATIC | forced alignment, CTC posteriors, generated phone labels | training signals / diagnostics only |

Rules: never promote AUTOMATIC or EXPERT to HUMAN-LISTENING; never treat generated alignment as
ground truth for weak/deleted finals; never let machine evidence reach the reviewer before a label
is recorded.

## Confidence semantics

- HIGH: clear decision, no hesitation.
- MEDIUM: likely correct with some ambiguity.
- LOW: a guess; the note must name the alternatives.

## Sufficiency

Pilot evaluation requires the minimum set of 60 PRESENT + 60 ABSENT (of the 546 Pack P tokens) with
>=2 reviewers, or an explicit `SINGLE_REVIEWER_LIMITATION`; anything smaller is INCONCLUSIVE per the
frozen success criteria. /r/ pilot evaluation additionally requires the 8+8 target described in
`05_R/R_LABEL_REQUIREMENTS.md`.
