# PHONE LABEL STRATEGIES — WP-1.9.26 Part 16

> **Tóm tắt (VI):** So sánh 3 đường annotation: (A) human phone labels; (B) expert forced alignment
> + human verification; (C) automatic labels + human audit. Không đường nào tự động là "ground
> truth": AUTOMATIC / EXPERT / HUMAN-LISTENING phải được lưu tách biệt. Cho mục tiêu của dự án
> (final consonant presence), chiến lược khuyến nghị là B+C với lớp HUMAN-LISTENING cho validation.

| criterion | A. human phone labels | B. expert forced alignment + verification | C. automatic + human audit |
|---|---|---|---|
| cost per hour | very high (full phone segmentation) | high (alignment + spot checks) | low (automatic + sampling) |
| reliability | highest for phones a human can hear; weak for coarticulated/weak finals | high for boundaries; verification needed for weak finals | variable; errors concentrated on child speech |
| scalability | low | medium | high |
| expected error | reviewer-dependent; low with trained annotators | alignment errors on deleted/weak phones | high on weak finals and /r/ |
| speaker leakage risk | none if speaker-disjoint | none if splits fixed before alignment | none if splits fixed |
| /r/ reliability | medium (rhoticity is perceptual) | low (alignment does not measure rhoticity) | low |
| final-consonant reliability | high for presence/absence | medium (boundary placement) | medium-low |
| best use | validation subset (the minimum label set) | training targets + timing | large-scale pre-annotation |

## Evidence types (must stay separate)

- **HUMAN-LISTENING** — labels from the blind review pipeline (PRESENT/ABSENT/UNCERTAIN + confidence).
- **EXPERT** — speech-ocean762/SIAK expert scores (score-derived, not listening labels).
- **AUTOMATIC** — forced alignment / CTC posteriors / generated phone labels.

Rules: never promote AUTOMATIC to HUMAN-LISTENING; never promote EXPERT scores to listening labels;
never treat alignment as ground truth for weak/deleted finals.

## Recommended strategy

1. Generate automatic alignments for any large corpus (MyST/OCSC/JIBO) with MFA + the corpus lexicon.
2. Human-verify a stratified subset (weak finals, /r/, isolated peaks) using the review pipeline.
3. Use HUMAN-LISTENING labels as the FRR-first evaluation target; AUTOMATIC/EXPERT as auxiliary
   evidence and training signals only.
4. For the local pilot, use existing recordings + new listening labels (no alignment needed).
