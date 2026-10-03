# ELSA — Reuse Analysis

| Item | Type | Status | Reason |
|---|---|---|---|
| Streaming + server-side endpointing | ARCHITECTURE_PATTERN | REFERENCE_ONLY | Mature pattern; LWE can adopt offline equivalent |
| Phoneme-deviation scoring (vs native) | ALGORITHM | REFERENCE_ONLY | Published concept, no code |
| L1-specific acoustic models | ARCHITECTURE_PATTERN | NOT_USEFUL | LWE audience is single-L1 (VN) but targets EN; different problem |
| Speaker-ID filtering (meetings) | ALGORITHM | REFERENCE_ONLY | Out of LWE scope |
| Multidimensional scoring (pron/intonation/fluency) | ARCHITECTURE_PATTERN | REFERENCE_ONLY | LWE currently single-dimension; possible future split |
| API | API_SERVICE | BLOCKED | Partner token required; adult models |
| Code/model | — | NOT_REUSABLE | Proprietary |

REUSE_COST: BLOCKED for assets; LOW for pattern reference
EXPECTED_VALUE: MEDIUM (architecture + scoring concept references)
