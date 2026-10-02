# Human Review Guide — IMG_0639 hybrid VAD segments

## Purpose
Validate whether `hybrid_score` segments on **IMG_0639** contain audible speech.
This is **HUMAN_SINGLE_REVIEWER_REFERENCE**, **not** perfect ground truth.

## Files
- Clips: `HumanReview/clips_hybrid/*.wav` (28 clips, 50ms pad)
- Energy sample: `HumanReview/clips_energy/*.wav` (first 40 of 75)
- Manifest: `Results/human_review_manifest.json`
- CSV: `artifacts/Human_Review_Template.csv`
- HTML: `HumanReview/review.html` (open in browser)

## How to label
For each hybrid clip, set `human_label` to exactly one of:
- `SPEECH` — clear human speech (any language)
- `NON_SPEECH` — noise, hum, music-only, silence, artifacts
- `MIXED_UNCERTAIN` — speech+noise unclear, or unsure

Optional `boundary_ok`:
- `YES` / `CUT_START` / `CUT_END` / `CUT_BOTH` / `N/A`

## Rules
- Do **not** use AI scores to decide the label.
- Listen with headphones if possible.
- One reviewer only → report as single-reviewer reference.

## After review
Save filled CSV as `artifacts/Human_Review_Filled.csv` and re-run summary script if available.
