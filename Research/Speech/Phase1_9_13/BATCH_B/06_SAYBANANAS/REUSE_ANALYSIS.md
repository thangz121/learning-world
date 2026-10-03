# SayBananas — Reuse Analysis

| Item | Type | Status | Reason |
|---|---|---|---|
| Template matching (correct/incorrect templates per word) | ALGORITHM | ADAPT (research) | Low-resource, personalised alternative to phone scoring |
| Audio-quality gate (reject unanalyzable) | ARCHITECTURE_PATTERN | ADAPT | External evidence: ~50% attrition without it |
| High-dose trial model (~100/session, dose-response) | UX/PRACTICE_PATTERN | ADAPT | Practice design for LWE |
| Max one retry per word | UX_PATTERN | ADAPT | Avoid practicing errors |
| KR/KP feedback framing; caregiver options | UX_PATTERN | REFERENCE_ONLY | Therapy-informed feedback |
| Game design: exercises during gameplay, mandatory star triggers | UX_PATTERN | REFERENCE_ONLY | Engagement pattern |
| Code/model/API | — | NO_REUSE | Proprietary |

REUSE_COST: LOW (patterns)
EXPECTED_VALUE: HIGH (template matching + audio-quality evidence + practice model)
