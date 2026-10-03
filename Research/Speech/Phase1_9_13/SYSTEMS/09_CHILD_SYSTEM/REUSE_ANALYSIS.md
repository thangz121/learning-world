# SIAK — Reuse Analysis

| Item | Type | Status | Reason |
|---|---|---|---|
| Child speech dataset (16,308 flac, ages 4–12, scores) | DIRECT_DATASET | ADAPT (license review) | CC-BY-ND: no unrelated derivatives; commercial model use not prohibited |
| Rejected-class taxonomy (silence/interrupted/wrong word/noise/no effort) | ARCHITECTURE_PATTERN | REFERENCE_ONLY | Same gap as our fidelity finding |
| PWLD (phonetically weighted Levenshtein) | ALGORITHM | REFERENCE_ONLY | Alignment-free-ish scoring |
| Broad speech-event classes (vowel/stop/fricative/nasal) | ALGORITHM | REFERENCE_ONLY | Lightweight validation gate |
| Scoring models | — | NOT_REUSABLE | Not released |
| Game code | — | NOT_AVAILABLE | Stripped / privacy repo only |

REUSE_COST: LOW (dataset download) / HIGH (license legal review)
EXPECTED_VALUE: HIGH for child calibration data; MEDIUM for algorithm patterns
