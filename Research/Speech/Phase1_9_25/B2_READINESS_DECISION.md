# B2 READINESS DECISION — WP-1.9.25 Phase 10

> **Tóm tắt (VI):** Cả hai điều kiện B2 đều KHÔNG đạt. (A) Nhãn người: 0 nhãn mới; 4/4 TYPE-B chưa
> giải quyết (2 LABEL_LIMITED, 2 MIXED); /r/ 1 PRESENT LOW. (B) Dữ liệu: không dataset nào đồng thời
> đạt quy mô 10–30 h + 30–50+ người nói + nhãn phone-level + tuổi 4–6 + license thương mại; route
> thương mại duy nhất ở quy mô lớn (MyST) không có nhãn phone và tuổi 8–11; /r/ tốt nhất (PERCEPT-R)
> là non-commercial. **B2 TRAINING = NO.**

## Condition A — human labels sufficient?

**NO.** 0 new labels collected (no reviewer in session; no fabrication). The 4 TYPE-B cases remain
unresolved (2 LABEL_LIMITED, 2 MIXED); /r/ has 1 PRESENT (LOW). The historical LWE labels are
single-reviewer; SO762 labels are expert scores, not listening labels. `REVIEWER_AGREEMENT.csv`
documents the no-reviewer and single-reviewer limitations.

## Condition B — legally usable dataset exists or a concrete legal acquisition route?

**NO as specified.** No audited dataset combines the B2 minimum (10–30 h child speech, ≥30–50
speakers, speaker-disjoint, phone-level labels, ages 4–6 preferred, commercial use):

- speechocean762: CLEAR and phone-labelled, but only 2.41 h child, Mandarin-L1, ages 6–15.
- MyST: large and commercially licensable (Boulder Learning; LDC2021S05), but word-level only and
  ages 8–11; phone labels would need generation/annotation.
- SIAK: local and age-4–6 capable, but CC-BY-ND needs legal review.
- PERCEPT-R / PERCEPT-GFTA: the best /r/ labels (rhotic/derhotic) but non-commercial and ages 6–17.
- CSLU Kids / CMU Kids: research-only; commercial route (OHSU) unverified.
- Vietnamese-L1 child speech: no publicly accessible corpus identified.

## Final gate (exactly one)

```
LABELS_INSUFFICIENT_DATA_LICENSE_BLOCKED
```

## B2 status (Part 23 format)

- **B2 TRAINING:** NO
- **B2 DESIGN:** NOT READY
- **B2 TARGET:** missing/weak acoustic evidence for weak child final consonants (liquids /r/, /l/
  first; one-frame finals; blank-dominated endings); strong false-evidence side unresolved.
- **B2 REQUIRED DATA:** 10–30 h child speech, ≥30–50 speakers, speaker-disjoint, ages 4–6 preferred;
  a commercial license or a partnership route (MyST paid license is the only large-scale candidate,
  and it lacks phone labels).
- **B2 REQUIRED LABELS:** minimum 60 PRESENT + 60 ABSENT listening labels across ≥20 speakers
  (15+ /r/ each side), speaker-disjoint; 4 TYPE-B listening labels; see `B2_LABEL_REQUIREMENTS.csv`.
- **B2 LICENSE:** BLOCKED (child corpora) / CLEAR (models, apache-2.0).
- **B2 EXPECTED BENEFIT:** recover evidence for weak final consonants (especially liquids) and reduce
  the 16.5% no-evidence present failures.
- **B2 KNOWN LIMITATION:** cannot guarantee fixing strong false peaks, label-ambiguous cases, or the
  acceptance-layer overlap; no Vietnamese-L1 domain coverage from public data.

## Next gate recommendation

```
LABEL_COLLECTION_AND_DATA_ACQUISITION
```

1. Run the human review round on L01–L23 (P0 TYPE-B first; /r/ P1; weak present; rank-2..5).
2. Decide the MyST commercial license question (cost/benefit) and/or the SIAK CC-BY-ND legal review;
   both are prerequisites for any scale B2.
3. Keep production untouched and B2 training disabled until a future explicitly approved gate.
