# Candidate report template (copy to candidates/<ID>/candidate_report.md)

# Candidate: <ID>

## Source
Repository / project URL.

## Version
Exact commit/tag/version + date tested.

## License
Repository license (+ differing SPDX headers found in source files).

## Model License
Exact model/checkpoint/tokenizer/dictionary license. NOT_AVAILABLE if none.

## Dependency License
Important dependency + runtime licenses (incl. transitive ones actually installed).

## Intended Role
STT / VAD / Alignment / Phoneme / Pronunciation / Toolkit-primitive / Tool-offline.

## Installation
Actual steps used (env, commands, versions). Smallest reproducible env.

## Runtime
CPU/GPU/OS/RAM measured on our machine.

## Tests
Exact tests run: smoke + common corpus groups + LWE mic-derived tests + failure cases.

## Results
Measured values only. Fields the candidate cannot provide: NOT_AVAILABLE.

## Baseline Comparison
Same audio, BASELINE vs CANDIDATE. Never different recordings.

## Improvements
Only measured improvements (one is enough to retain).

## Problems
Only observed problems.

## Unique Capability
What this candidate provides that others may not.

## Retention Decision
RETAIN / HOLD / BLOCKED (+ license status CLEAR/REVIEW_REQUIRED/RESTRICTED/RESEARCH_ONLY/BLOCKED).

## Future Combination
Possible stack role, e.g. VAD-only front-end for candidate 02.
