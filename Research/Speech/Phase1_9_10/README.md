# Phase 1.9.10 — Deep-dive 12 child phone-model-error cases

**Decision:** `B. ROOT_CAUSE_PARTIALLY_IDENTIFIED` (methodology repaired in 1.9.11)  
**No scorer changes.** production_vad/router/unity/scorer_modified = false

Human second pass (raw Stage B): **TRUE_PRONUNCIATION_ERROR = 5, UNCERTAIN = 7**

Normalized states:

| state | n |
|---|---:|
| HUMAN_TRUE_ERROR (clean) | 4 |
| HUMAN_UNCERTAIN (not acceptable) | 7 |
| HUMAN_CONFLICTED (pme_01) | 1 |
| HUMAN_ACCEPTABLE (explicit) | 0 |

Measured mechanisms for the 7 uncertain cases are a **diagnostic hypothesis only**:
boundary 3, full-file CTC alignment 1, attractor 1, realization variation 2.

- revised 1.9.9 metric: 0.414 superseded → **0.0 (0/17 confirmed)**, 8 unresolved
- boundary rescue: 4/12; crop-beats-full inversion: 2/12
- F0 does **not** separate error vs OK groups (281.5 vs 280.7 Hz)
- word-edge mismatches: initial 12/12, final 10/12

Review server: `Scripts/serve_review.py` (direct submission, no file transfer).
