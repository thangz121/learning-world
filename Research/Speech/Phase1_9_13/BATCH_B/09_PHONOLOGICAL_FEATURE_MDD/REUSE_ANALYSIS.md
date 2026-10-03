# Phonological-Feature MDD — Reuse Analysis

| Item | Type | Status | Reason |
|---|---|---|---|
| 35-attribute phonological representation | ALGORITHM | ADAPT (research) | Better diagnosis (how error made) + unique phoneme code |
| Multi-label CTC (SCTC-SB) | ALGORITHM | REFERENCE_ONLY | Train on native-only data |
| AF categories (vowel 3 / consonant 3) | ALGORITHM | ADAPT | Compact attribute set for LWE |
| Diagnosis by max-deviation attribute + direction | ALGORITHM | ADAPT | Direct formative feedback generator |
| Active learning sampling | ALGORITHM | REFERENCE_ONLY | Annotation-cost control |
| Vietnamese-L1 evaluation evidence | RESEARCH_REFERENCE | — | LWE audience L1 |
| Code/models | — | NOT_AVAILABLE | No public package found |

REUSE_COST: MEDIUM (reimplementation; needs wav2vec2 + attribute labels)
EXPECTED_VALUE: HIGH for error diagnosis; UNKNOWN for child latency
