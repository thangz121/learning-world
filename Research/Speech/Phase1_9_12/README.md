# Phase 1.9.12 — Final-consonant acoustic support + expanded window review

**Decisions:**
- `ACOUSTIC_SUPPORT: B. REDUNDANT` — CTC-span-anchored features inherit forced-alignment errors
  (phone right 22/28 vs acoustic 17/28)
- `WINDOW: D. EVIDENCE_INSUFFICIENT` — 35/100 clips rated; finish via
  `HumanReview/window_blind_remaining.html`

production_vad/router/unity/scorer_modified/production_window_locked = **false**

## Human final-consonant review (28, completed)
present 16 / absent 12: /r/ 5/6 absent, /t/ 3/8, fricatives 3/5, /n/ 1/9.

## Key facts
- 65 child tokens with final consonants: NASAL 31, FRICATIVE 16, STOP 9, LIQUID 9
- 14/65 "restored by padding" (raw<50 → pad≥50), incl. 5 confirmed deleted /r/
- tok_04 nine: RAW score 33.8 but rated CLEAR → numeric drops ≠ perception

Server: `Scripts/serve_review.py` (packs: final_consonant / window_expanded / window_expanded_remaining).
