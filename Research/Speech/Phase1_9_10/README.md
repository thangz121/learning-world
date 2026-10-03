# Phase 1.9.10 — Deep-dive 12 child phone-model-error cases

**Decision:** `B. ROOT_CAUSE_PARTIALLY_IDENTIFIED`  
**No scorer changes.** production_vad/router/unity/scorer_modified = false

| primary cause | n |
|---|---:|
| BOUNDARY_ERROR | 4 |
| CTC_ALIGNMENT_ERROR | 3 |
| PHONE_MODEL_GENERALIZATION_FAILURE | 3 |
| PHONEME_REALIZATION_VARIATION | 2 |

- human-correct rate of the 12 cases: **1.0**
- boundary rescue: **4/12**; crop-beats-full inversion: 2/12
- F0 does **not** separate error vs OK groups (281.5 vs 280.7 Hz)
- word-edge mismatches: initial 12/12, final 10/12

Human second-pass pack: `HumanReview/review_blind.html` (Stage A) → `review_reveal.html` (Stage B).
