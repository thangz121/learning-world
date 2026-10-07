# B2 MINIMUM DATASET — WP-1.9.26 Part 17

> **Tóm tắt (VI):** B2-MINIMUM = pilot nhỏ, dữ liệu local + nhãn người mới: 8–10 speakers, 2–4h,
> oversample phụ âm cuối và /r/, 60 PRESENT + 60 ABSENT listening labels, speaker-disjoint
> 6/1/1. Đủ để trả lời "encoder adaptation có đáng theo đuổi không?" — KHÔNG đủ để production.

| property | B2-MINIMUM |
|---|---|
| hours | 2–4 h (local: LWE 11 children + SO762 child subset + SIAK 4–6 words) |
| speakers | 8–10 (speaker-disjoint train/dev/test = 6/1/1 or 6/2) |
| ages | 4–11 (LWE mean 4.9; so762 6–15; SIAK 4–6 words) |
| phone counts | ≥30 final-consonant tokens per phone class in the eval set (t, k, s, z, n, m, r, l, v, d) |
| /r/ count | ≥15 labelled /r/ tokens (15 PRESENT + 15 ABSENT required; minimum 8+8) |
| final-consonant count | ≥120 labelled final-consonant tokens |
| labels | 60 PRESENT + 60 ABSENT listening labels (MINIMUM set in `B2_DATA_REQUIREMENTS.csv`) |
| annotation confidence | HUMAN-LISTENING HIGH/MEDIUM; UNCERTAIN kept separate |
| license | local recordings (LWE CC BY 4.0 test-only; so762 CC BY 4.0; SIAK legal review) — no new licensing for the pilot |
| GPU | none for the pilot evaluation (B2-E/B2-D inference only); a small head (B2-A) is CPU-feasible |
| expected use | decide whether full B2 is worth pursuing; measure FRR-first trade-off on the local cache |

**Verdict on the 10–30 h range:** for a *pilot* answering "is encoder adaptation worth pursuing?",
10–30 h is **excessive** — 2–4 h with speaker-disjoint labels and phone-balanced targets is enough
to detect a directionally useful effect. For a *production* model, 10–30 h is **plausible but
unproven**: the published child-ASR literature uses 100+ h for robust adaptation (MyST 400+ h), and
the project's own 1.9.16 negative result (frozen encoder + head) argues that scale alone is not the
issue. The pilot is the scientific gate.
