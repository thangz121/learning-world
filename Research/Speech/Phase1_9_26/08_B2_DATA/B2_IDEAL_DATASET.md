# B2 IDEAL DATASET — WP-1.9.26 Part 17

> **Tóm tắt (VI):** B2-IDEAL = corpus Việt-L1 trẻ 4–6 (30–50 speakers, 10–30h, nhãn phone-level
> human, consent đầy đủ) + license thương mại rõ ràng. Không tồn tại công khai; chỉ có thể đạt
> bằng thu thập địa phương hoặc hợp tác đại học. Đây là đích dài hạn.

| property | B2-IDEAL |
|---|---|
| hours | 10–30 h |
| speakers | 30–50, speaker-disjoint |
| ages | 4;0–6;11, Vietnamese-L1 |
| phone counts | target-balanced; final consonants and /r/ oversampled (see collection protocol) |
| /r/ count | ≥50 present + ≥40 absent labelled |
| labels | 200 PRESENT + 150 ABSENT human phone-level labels; double-reviewed |
| annotation confidence | two independent reviewers + adjudication; kappa reported |
| license | own corpus with consent for commercial model training (or a commercial license like MyST extended with phone labels) |
| GPU | required for adaptation; budget for cloud GPU |
| expected use | production-grade B2 with L1/age domain match |

**Gap:** no public corpus meets this; the route is the prospective collection protocol
(`06_VIETNAMESE/VIETNAMESE_CHILD_SPEECH_COLLECTION_PROTOCOL.md`) or a partnership with an existing
research group (VietSpeech, Hue University). Until then this remains a target, not a plan.
