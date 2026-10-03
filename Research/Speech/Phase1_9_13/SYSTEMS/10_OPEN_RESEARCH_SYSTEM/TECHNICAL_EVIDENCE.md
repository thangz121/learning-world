# slip — Technical Evidence (E3)

## Forced alignment (lib/align.ts)
- Standard **blank-interleaved (2L+1) Viterbi** over CTC log-posteriors; skips a blank only
  between two different tokens; backtrace yields spans per expected token.
- `Span.present = false` when a token had to be synthesized because the audio is too short —
  i.e., the implementation can **represent a missing phone explicitly** (relevant to our
  deleted final consonants).

## GOP (lib/gop.ts)
- For each span: `GOP-Avg = mean_t log P(canonical | frame)`; closer to 0 better.
- `GOP-ratio = mean_t ( logP_canonical − max_k≠blank logP_k )` (likelihood-ratio normalization).
- `heardId = argmax_k≠blank Σ_t logP_k` (the "heard" phone over the span).
- Severity buckets (good/s1/s2/s3) with a user sensitivity shift; **thresholds explicitly
  uncalibrated**; `lowConfidence` if span < 3 frames or not present.
- Author's honest note: raw GOP-CTC ≈ 0.44–0.46 phone-level correlation vs human raters.

## Stack
- transformers.js + ONNX Runtime Web (int8 wav2vec2-lv-60-espeak-cv-ft), espeak-ng WASM,
  CMUdict; optional Python inference seam (`services/infer/app.py`).

## Relevance to LWE
- This is the closest open design to what our Phase 1.9.12 recommended: landmark/alignment
  handling that can represent deletions + likelihood-ratio GOP instead of raw similarity.
- Code cannot be reused (no license) but the **algorithm is standard math** (torchaudio
  forced_align + GOP) that LWE can implement independently.
