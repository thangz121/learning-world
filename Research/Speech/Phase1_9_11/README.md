# Phase 1.9.11 — Repair + SCORER_MISS + 80-token window A/B

**Decision:** `B. WINDOW_EFFECT_REAL_BUT_NOT_SAFE`  
`SCORER_FORMULA_CHANGE: NOT_JUSTIFIED` · production_vad/router/unity/scorer_modified = false

1. **Repair (PASS):** 3 human states; 5 raw TRUE + 7 UNCERTAIN; pme_01 conflicted;
   revised metric 0.0 (0/17 confirmed), 8 unresolved.
2. **SCORER_MISS:** 4/6 window-dependent, 2/6 soft-match-too-permissive (measured).
3. **Window A/B (80 tokens):** sensitive rate **51.3%**; FULL≈padded; raw VAD rejects 7/17
   confirmed-correct; padding passes 7/15 confirmed-error (false rescue).

## Pending human review (LAN port 8767)
- `HumanReview/scorer_miss_blind.html` → `_reveal.html`
- `HumanReview/window_spotcheck_blind.html` → `_reveal.html`

Server: `Scripts/serve_review.py` (direct submission, packs: scorer_miss / window_spot).
