# SIAK LEGAL REVIEW — WP-1.9.26 Part 10

> **Tóm tắt (VI):** SIAK là CC-BY-ND-4.0. BY-ND cho phép chia sẻ nguyên trạng (kể cả thương mại) nhưng
> **không cho phép phân phối bản phái sinh**. Huấn luyện model tạo ra trọng số phái sinh — chưa rõ có
> bị coi là "adapted material" không. Kết luận: **REQUIRES_LEGAL_REVIEW**; không tự nâng lên CLEAR.

## Facts (verified)

- Local copy: Kaggle mirror of SIAK; local metadata records license `CC-BY-ND-4.0` with a note
  "commercial building/evaluation of speech technology models not prohibited" (an interpretation,
  not the license text).
- Radboud University CC-license page (fetched 2026-07-…): CC BY-ND "allows for redistribution,
  commercial and non-commercial use, provided it is passed along unchanged and in whole".
- SIAK content: 16,308 utterances (single words/short phrases), 172 speakers, ages 4–12 (594
  utterances age 4–6), single expert word score 0–100, no phone labels.

## The open legal question

Does training a model on SIAK and distributing the resulting weights (or using the model inside a
commercial product) constitute distributing an **adapted/derivative work** of the corpus?

- Argument that it does not: the model weights are not a reproduction of the recordings; they are
  statistics learned from them (position comparable to "text and data mining").
- Argument that it does: CC BY-ND's "adapted material" can include outputs that incorporate the
  licensed material; model weights trained on the corpus may be treated as derivative in some
  jurisdictions; no case law in this domain is cited here.
- Either way, the ND clause prevents distributing *audio* derivatives (clips, modified recordings).

## Separated uses

| use | assessment |
|---|---|
| research use of the audio | allowed under BY (attribution) |
| modification of the audio/data | not allowed to distribute modified data |
| derived features for internal research | low risk (not distributed) |
| model training | **unresolved** |
| distributing derived model weights | **unresolved** |
| commercial product use of such a model | **unresolved** |

## Required action

Send `LEGAL_QUESTIONS_FOR_COUNSEL.md` to a qualified reviewer. Until answered, SIAK remains
REQUIRES_LEGAL_REVIEW and must not be used for any distributed model or commercial path. Research
use (local diagnostics, not distributed) remains possible under the attribution requirement.
