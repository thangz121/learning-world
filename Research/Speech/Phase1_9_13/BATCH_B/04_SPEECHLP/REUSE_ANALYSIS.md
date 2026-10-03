# SpeechLP — Reuse Analysis

| Item | Type | Status | Reason |
|---|---|---|---|
| Target selection algorithm (Crowe & McLeod 2020 + rules) | ALGORITHM / TARGET_SELECTION | **DIRECT_REUSE (knowledge)** | Published norms; implementable by LWE |
| Latest-consonant rule + multisyllabic +1y | ALGORITHM | DIRECT_REUSE | Simple, defensible curriculum logic |
| Phonological process filter (incl. FCD) | ARCHITECTURE_PATTERN | ADAPT | Aligns with our final-consonant findings |
| Age-relative difficulty model | ALGORITHM | ADAPT | 4-dimension difficulty for word selection |
| Smart voice detection (child speech vs game audio) | ARCHITECTURE_PATTERN | REFERENCE_ONLY | Attempt detection in child product |
| Sound-by-sound progress + screener report format | UX_PATTERN | ADAPT | Parent/SLP reporting template |
| Code/model/API | — | NO_REUSE | Proprietary |

REUSE_COST: LOW (implement norms + rules)
EXPECTED_VALUE: **HIGH for LWE curriculum/target design**
