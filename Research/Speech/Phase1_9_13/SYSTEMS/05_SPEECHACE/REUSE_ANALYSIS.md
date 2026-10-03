# Speechace — Reuse Analysis

| Item | Type | Status | Reason |
|---|---|---|---|
| Fidelity classifier concept (CORRECT/NO_SPEECH/INCOMPLETE/FREE_SPEAK) | ARCHITECTURE_PATTERN | ADAPT (research) | Directly fills LWE's missing attempt-detection layer |
| Score-reduction on unfaithful attempts | UX_PATTERN | REFERENCE_ONLY | Retry instead of misleading score |
| `sound_most_like` per expected phone | ALGORITHM concept | REFERENCE (already similar to our best_obs) | Mature analog |
| Phoneme/syllable/word score hierarchy + rubrics | ARCHITECTURE_PATTERN | REFERENCE_ONLY | LWE only needs child-friendly subset |
| API service | API_SERVICE | POSSIBLE (paid/trial, adult models, cloud privacy) | Not child-validated |
| Code/model | — | NOT_REUSABLE | Proprietary |

REUSE_COST: MEDIUM (API trial) / LOW (pattern)
EXPECTED_VALUE: HIGH for fidelity design; MEDIUM as service
