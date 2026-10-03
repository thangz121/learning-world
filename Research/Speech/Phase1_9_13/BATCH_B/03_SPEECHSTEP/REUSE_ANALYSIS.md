# SpeechStep — Reuse Analysis

| Item | Type | Status | Reason |
|---|---|---|---|
| Refusal-to-score rule ("declined rather than guessed") | ARCHITECTURE_PATTERN | ADAPT (research) | Matches our fidelity/assessability gap; strong child-safety rule |
| Age-band feedback policy (silent → zero numbers → mastery) | UX_PATTERN | ADAPT | Direct template for LWE child-facing output |
| Lisp type via substitution diagnosis | ALGORITHM concept | REFERENCE_ONLY | Phonological-feature-like; relevant to our error taxonomy |
| Word-position practice targeting | UX_PATTERN / TARGET_SELECTION | ADAPT | Useful for LWE curriculum design |
| Consent-gated recording + months-age + random IDs | PRIVACY_PATTERN | ADAPT | Strong COPPA model for LWE |
| Demonstration vs measurement scorer split | ARCHITECTURE_PATTERN | REFERENCE_ONLY | Honest fallback when scoring unavailable |
| wav2vec2+GOP claim | ALGORITHM | CONFIRMS our direction | No code released |
| Code/model/API | — | NO_REUSE | Proprietary, no API |

REUSE_COST: LOW (patterns)
EXPECTED_VALUE: HIGH for child UX + refusal + privacy patterns
