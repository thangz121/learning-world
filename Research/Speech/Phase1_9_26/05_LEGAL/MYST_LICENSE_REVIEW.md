# MyST LICENSE REVIEW — WP-1.9.26 Part 11

> **Tóm tắt (VI):** MyST (LDC2021S05): 470h, 1.371 học sinh lớp 3–5 (≈8–11 tuổi), 227.567 utterance,
> ~102K có transcript word-level, **có pronunciation dictionary** và chia train/dev/test. Research:
> CC BY-NC-SA + data use agreement; **thương mại: license trả phí qua Boulder Learning**
> (LDC page + research agreement PDF). Không có phone-level labels — phải sinh alignment + hiệu
> chuẩn người. Đây là route thương mại quy mô duy nhất đã xác minh.

## Facts (verified)

- LDC catalog LDC2021S05: "approximately 470 hours of English speech from 1371 students in grades
  3-5… along with transcripts and a pronunciation dictionary"; train/dev/test partitions; FLAC
  16 kHz; ~45% transcribed (102,433 utterances); commercial licensing contact: John Ramo,
  Boulder Learning.
- Boulder Learning research agreement (PDF): non-commercial education and research only; no
  disclosure/copy/redistribution outside the research group.
- Corpus paper: CC BY-NC-SA 4.0 for non-commercial; commercial licensing available; Wikipedia
  corpus list records a flat USD 10K commercial fee (secondary source — confirm with Boulder
  Learning before budgeting).

## Annotation gap

- Word-level transcripts + a pronunciation lexicon + train/dev/test splits make **forced alignment**
  feasible (e.g., MFA/Kaldi) → generated phone boundaries and phone posteriors.
- Generated phone labels are NOT human ground truth; they can serve as training targets only with a
  human-verification layer (see `07_ANNOTATION/PHONE_LABEL_STRATEGIES.md`).
- Estimated verification burden for a 10–30 h subset: word-level checks plus 500–2,000 phone
  spot-labels for validation; the corpus has no child-age 4–6 coverage (grades 3–5).

## Commercial route

- Request a commercial license quote from Boulder Learning (contact on the LDC page).
- Confirm: (a) fee, (b) permitted uses (training, derived weights, product embedding), (c) whether
  derived weights may be distributed, (d) whether phone-level derived annotations may be created,
  (e) term/renewal.
- Until signed, MyST remains CLEAR_WITH_CONDITIONS and cannot be used commercially.
