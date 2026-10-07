# /r/ PILOT DESIGN — WP-1.9.27 Part 23

> **Tóm tắt (VI):** Thiết kế đánh giá /r/ chuyên biệt: tối thiểu 15 PRESENT + 15 ABSENT, nhiều
> speaker/ngữ cảnh/điều kiện thu; đo max_A/mean_A/temporal support/F1-F2-F3/F3-F2/RMS/voiced/
> transition/alt encoder. Không xây "/r/ detector" — đây là chẩn đoán.

## Question

> "Is the /r/ weakness encoder-limited (representation) or data-limited (labels/examples)?"

No general /r/ conclusion is allowed from the current 1 PRESENT LOW label.

## Requirements

- >=15 PRESENT + >=15 ABSENT /r/ listening labels, >=15 speakers, speaker-disjoint reporting.
- Multiple vowel contexts (ɔ/ɝ/ɑ) and recording conditions; include weak and isolated-peak cases.
- /r/ candidates live in Pack R pools A/B/C (24 cases) + Pack P pilot tokens (8 /r/) + new candidates
  if the review finds too few.
- PERCEPT-R (research-only, non-commercial) is the external rhotic benchmark if local labels remain
  insufficient; do not use it for commercial modeling.

## Features to measure (per case)

`R_FEATURE_SCHEMA.csv` lists the exact columns: model evidence (max_A, mean_A, temporal support,
isolated peak), acoustics (span, RMS ratio, voiced fraction, F1/F2/F3, F3-F2, F3/F2), transition
evidence, and alternative-encoder evidence (alt max_A, alt delta). All are already extracted for the
24 existing cases in `R_CASES.csv`.

## Current observations (1.9.26 audit, n=24 — hypotheses only)

- 20/24 isolated one-frame peaks; LWE median max_A 0.041; production span median 487 ms.
- RMS in span / utterance median 1.13; F3 median 2812 Hz, F3/F2 median 1.84 (weak rhoticity
  hypothesis; n small, child formants noisy).
- Alternative encoder recovers `child_04_four` (0.09 -> 0.41) and `child_01_seven` (0.06 -> 0.47) but
  loses evidence elsewhere: representation dependence, not a fix.

## Decision rule after labels

- If labelled PRESENT /r/ tokens show low evidence (max_A) and low F3-F2: representation-limited
  -> include /r/ in B2-E hybrid features.
- If labels show /r/ mostly ABSENT: the production misses are label-correct; /r/ may be a data/age
  issue, not a scorer defect.
- Either way: no /r/ detector until the pilot reads out.
