# Phase 1.9.10 — Deep-dive 12 child phone-model-error cases

**Decision:** `A. ROOT_CAUSE_SUFFICIENTLY_IDENTIFIED` (after human second pass)  
**No scorer changes.** production_vad/router/unity/scorer_modified = false

Human second pass (Stage A blind + Stage B evidence, submitted over LAN):

| verdict | n |
|---|---:|
| acceptable pronunciation, low score = system-side | **7** |
| true pronunciation error (low score justified) | **5** |

System-side mechanisms: BOUNDARY 3, full-file CTC alignment 1, realization variation 2,
attractor collapse 1 → **4/7 window-related**.

- revised 1.9.9 `human_correct_low_score_rate`: 0.414 → **0.292**
- boundary rescue: 4/12; crop-beats-full inversion: 2/12
- F0 does **not** separate error vs OK groups (281.5 vs 280.7 Hz)
- word-edge mismatches: initial 12/12, final 10/12

Review server: `Scripts/serve_review.py` (direct submission, no file transfer).
