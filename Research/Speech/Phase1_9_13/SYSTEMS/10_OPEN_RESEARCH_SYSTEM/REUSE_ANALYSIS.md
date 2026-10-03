# slip — Reuse Analysis

| Item | Type | Status | Reason |
|---|---|---|---|
| Blank-interleaved Viterbi forced align (with present flag) | ALGORITHM | REFERENCE_ONLY | Standard torchaudio algorithm; implement independently |
| GOP-Avg + GOP-ratio scoring | ALGORITHM | ADAPT (research) | Likelihood-ratio normalization is what our 1.9.12 lacked |
| lowConfidence flag (span < 3 frames / not present) | ALGORITHM | ADAPT | Honest evidence gating |
| Severity buckets + sensitivity shift | UX_PATTERN | REFERENCE_ONLY | Child-friendly severity |
| "tap to scrub to the slipped sound" | UX_PATTERN | REFERENCE_ONLY | Strong child/parent UX idea |
| Code | — | **BLOCKED_LICENSE** | No LICENSE file → cannot reuse |
| Model (wav2vec2-lv-60-espeak-cv-ft) | DIRECT_MODEL | ADAPT if model card permits | Same model as OpenPronounce |

REUSE_COST: BLOCKED for code; LOW for algorithm reimplementation
EXPECTED_VALUE: HIGH (design directly targets our measured failure modes)
