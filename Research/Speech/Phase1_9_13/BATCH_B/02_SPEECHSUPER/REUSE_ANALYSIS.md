# SpeechSuper — Reuse Analysis

| Item | Type | Status | Reason |
|---|---|---|---|
| Age-group control (3~6/6~12/>12) | CALIBRATION_METHOD | REFERENCE_ONLY | Confirms age conditioning is a product control; mechanism unknown |
| Strict/lenient slider (slack −1..+1) | CALIBRATION_METHOD | REFERENCE_ONLY | Same idea as Chivox `gop_adjust`; exposes tolerance as config |
| Phoneme omission/insertion diagnostics | ARCHITECTURE_PATTERN | REFERENCE_ONLY | Matches our final-consonant/deletion gap |
| `/x/ sounds like /y/` feedback | UX_PATTERN | ADAPT | Child-friendly substitution wording |
| Rhythm/speed/tone metrics | ARCHITECTURE_PATTERN | REFERENCE_ONLY | Beyond our current scope |
| API | API_AVAILABLE (trial by contact) | COMMERCIAL_ONLY | Cloud; adult/child model unverified |
| Code/model | — | NO_REUSE | Proprietary |

REUSE_COST: LOW (patterns) / MEDIUM (trial API)
EXPECTED_VALUE: HIGH for age/leniency control evidence; MEDIUM as API
