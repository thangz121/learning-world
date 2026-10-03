# OpenPronounce — Reuse Analysis

| Item | Type | Status | Reason |
|---|---|---|---|
| DTW alignment-free comparison (embeddings vs reference) | ALGORITHM | ADAPT (research) | Avoids CTC span anchoring that hurt us in 1.9.12 |
| Prosody extraction (F0/energy contours) | DIRECT_CODE | DIRECT_REUSE (MIT) | Small, clean utilities |
| Phone leniency pairs (tense/lax, ɔ/ɑ, ɾ/t) | ALGORITHM | ADAPT | Child-realization tolerance pattern |
| Phone recognition (wav2vec2-lv-60-espeak-cv-ft) | DIRECT_MODEL | ADAPT if model license OK | Alternative to our phone model |
| Score weights 0.3/0.4/0.3 | — | REFERENCE_ONLY | LWE keeps own scorer |
| Full pipeline as library | DIRECT_CODE | ADAPT | MIT code, but GPL deps (phonemizer/Levenshtein) need review |
| gTTS reference | — | NOT_USEFUL | Cloud dependency; replace locally |

REUSE_COST: LOW-MEDIUM (code MIT; GPL deps caution)
EXPECTED_VALUE: MEDIUM-HIGH (independent cross-check + DTW + leniency patterns)
