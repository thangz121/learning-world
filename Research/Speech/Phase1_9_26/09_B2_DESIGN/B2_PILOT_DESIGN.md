# B2 PILOT DESIGN (DESIGN ONLY) — WP-1.9.26 Part 25

> **Tóm tắt (VI):** Pilot khả thi ngay không cần license mới: 8–10 speakers local, 2–4h, 60P+60A
> nhãn listening (từ pack 276), oversample /r/ và phụ âm cuối; speaker-disjoint 6/1/1; mục tiêu là
> trả lời "encoder adaptation có đáng theo đuổi?" — không phải production readiness.

## Question the pilot can answer

> "Does adapting/calibrating the acoustic representation directionally improve the FRR-first
> trade-off on weak child final consonants, on speaker-disjoint local data?"

## Design

| element | value |
|---|---|
| data | existing local recordings: LWE (11 children, 4–6), SO762 child subset (122 children, 6–15), SIAK age-4–6 words (if cleared) |
| speakers | 8–10 for the labelled pilot (speaker-disjoint 6/1/1 or 6/2) |
| hours | 2–4 |
| labels | 60 PRESENT + 60 ABSENT listening labels from the 276-candidate pack (pools A–E, K, M) + 8+8 /r/ |
| targets | ≥30 tokens per phone class; /r/ and liquids oversampled |
| experiments | B2-E (hybrid features, CPU) and B2-D (encoder comparison, CPU); B2-A (small head) optional |
| controls | frozen production encoder; production acceptance baseline; random-feature control |
| success | FRR-first pass on the pilot test split + directional improvement on missing-evidence tokens |
| failure | no improvement or FRR regression → do not pursue full B2 without new evidence |
| limitation | cannot establish production readiness; cannot cover Vietnamese-L1; too small for encoder fine-tuning |

## Why this is scientifically defensible

- It uses the project's own local data with the strongest existing labels (blind listening) and a
  speaker-disjoint split, so a positive result is not driven by one speaker.
- It directly targets the supported limitation (missing evidence for weak finals) and the
  representation dependence already measured across two encoders.
- It requires no license decision, no GPU, and no production change.
- A negative result saves the cost of a 10–30 h collection; a positive result justifies it.

## What the pilot must NOT claim

- Production readiness; Vietnamese-L1 coverage; robustness to recording conditions beyond the local
  mics; any improvement on the strong false-evidence side.
