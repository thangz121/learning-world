# Microsoft PA — Reuse Analysis

| Item | Type | Status | Reason |
|---|---|---|---|
| Result schema (scores + ErrorType + NBestPhonemes + offsets) | ARCHITECTURE_PATTERN | DIRECT reference for LWE evidence layer | Public, stable, proven |
| Omission/Insertion/Mispronunciation taxonomy | ALGORITHM concept | ADAPT (research) | Directly addresses final-consonant deletion |
| NBestPhonemes ranked alternatives | ALGORITHM concept | ADAPT (research) | Replaces our single best_obs+sim with ranked candidates |
| Pronunciation-specific STT model | DIRECT_MODEL | BLOCKED | Cloud-only; child data + cost + privacy |
| SDK | DIRECT_CODE | AVAILABLE (MIT samples; SDK binaries) | But service requires key + cloud |
| Offline container | DIRECT_MODEL | UNKNOWN | Azure Speech containers exist; not evaluated |

REUSE_COST: LOW for schema/design; HIGH for service integration (cloud dependency, child privacy)
EXPECTED_VALUE: HIGH as reference design; MEDIUM as service (adult-oriented, cloud)
