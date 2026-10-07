# PUBLIC CHILD DATA AUDIT (age 4–6 branch) — WP-1.9.27 Part 22

> **Tóm tắt (VI):** Audit sâu 4 nguồn tuổi nhỏ mới phát hiện: OCSC (303 spk 4–9, TalkBank
> research), JIBO Kids (110 spk 4–7, 21 h, LICENSE chưa rõ), AusKidTalk (620 spk 3–12, 136,6 h
> annotated, terms chưa xác minh), CAPIL (30 trẻ 5–6 + 30 người lớn, TalkBank research). Kết luận:
> dữ liệu thô tuổi 4–7 tồn tại cho research, nhưng KHÔNG nguồn nào có phone labels + quyền thương
> mại; mọi route đều cần đăng ký/đối chiếu pháp lý. Không tải nguồn license chưa rõ.

## OCSC — Ohio Child Speech Corpus (TalkBank/CHILDES)

- 303 speakers, ages 4–9, US English; seven-task protocol; orthographic CHILDES transcripts only
  (no phone labels, no explicit split).
- Access: free TalkBank account; TalkBank research terms (non-commercial).
- Value: best public age+scale match for research-only encoder diagnostics/age-domain checks.
- Action: register TalkBank account; do not use for commercial modeling.

## JIBO Kids Corpus

- 110 speakers, ages 4–7 (pre-K to grade 1), ~21 h, 383 wav files, word-level data.
- Public GitHub + Zenodo mirror; NO LICENSE file found; rights unverified.
- Value: excellent age match, but rights must be confirmed with the authors before any use.
- Action: author confirmation required; treat as LICENSE_UNVERIFIED until then.

## AusKidTalk

- ~620 speakers, ages 3–12, 136.6 h annotated (461 children transcribed), single-word task,
  Australian English.
- Access via project registration; terms unverified; commercial status unknown.
- Value: largest annotated child set; English variant differs from the product's English.
- Action: legal/terms inquiry; no download before terms are read.

## CAPIL (CHILDES)

- 30 children (5;1–6;10) + 30 adults, ~18 h, spontaneous speech transcribed.
- TalkBank research terms; free registration.
- Value: age 5–6 bonus set; spontaneous domain differs from the product's word tasks.

## Cross-source conclusion

| need | OCSC | JIBO | AusKidTalk | CAPIL |
|---|---|---|---|---|
| age 4–7 at scale | yes (4–9) | yes (4–7) | part (3–12) | part (5–6) |
| phone labels | no | no | no | no |
| explicit splits | no | no | not verified | no |
| commercial rights | no (research) | unknown | unknown | no (research) |
| action before use | registration | author confirmation | terms inquiry | registration |

No audited source provides phone-level labels at scale with commercial rights. The dominant blocker
remains phone-level annotation, not raw child speech. Do not download license-unclear corpora.
