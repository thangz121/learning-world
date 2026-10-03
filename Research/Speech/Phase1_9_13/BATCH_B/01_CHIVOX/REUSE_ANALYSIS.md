# CHIVOX — Reuse Analysis

| Item | Type | Status | Reason |
|---|---|---|---|
| Audio-quality gate before pronunciation feedback | ARCHITECTURE_PATTERN | ADAPT (research) | Directly addresses our IMG_0639/bad-audio confusion |
| "No valid voice → re-record" assessability rule | ARCHITECTURE_PATTERN | ADAPT | Matches our fidelity gap |
| Age/level calibration controls (thresholds, retry length) | CALIBRATION_METHOD | REFERENCE_ONLY | We cannot copy data, but the design is clear |
| Correction taxonomy (missed/misread/superfluous) | ARCHITECTURE_PATTERN | REFERENCE_ONLY | Maps to our deletion/substitution/insertion needs |
| `gop_adjust` as exposed parameter | API_PATTERN | REFERENCE_ONLY | Interesting: score adjustment as config, not magic |
| Child-pattern tolerance (pitch/pauses/repetitions/omissions) | ARCHITECTURE_PATTERN | REFERENCE_ONLY | Validates our child findings |
| Engine/API | API_AVAILABLE (pilot credits) | COMMERCIAL_ONLY | Keys required; child model not verifiable without pilot |
| Code/model | — | NO_REUSE | Proprietary |

REUSE_COST: MEDIUM (pilot integration) / LOW (patterns)
EXPECTED_VALUE: HIGH as child-architecture reference; MEDIUM as API (needs pilot validation)
