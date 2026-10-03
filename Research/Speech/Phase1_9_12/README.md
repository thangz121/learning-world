# Phase 1.9.12 — Final-consonant acoustic support + expanded window review

**Decisions (pre-human-submission):**
- `ACOUSTIC_SUPPORT: C. PROMISING_BUT_INSUFFICIENT` — CTC-span-anchored features are not independent
- `WINDOW: D. EVIDENCE_INSUFFICIENT` — pending expanded human review

production_vad/router/unity/scorer_modified/production_window_locked = **false**

## Packs (LAN 8768)
- `HumanReview/final_consonant_blind.html` → `_reveal.html` (28 items)
- `HumanReview/window_blind.html` → `window_reveal.html` (20 tokens × 5 anonymous clips)

## Key facts
- 65 child tokens with final consonants: NASAL 31, FRICATIVE 16, STOP 9, LIQUID 9
- 14/65 "restored by padding" (raw<50 → pad≥50), incl. 5 confirmed deleted /r/
- 325 feature rows (65 × FINAL_RAW/PAD50/100/150/200)
- Preliminary phone-vs-acoustic (13 inferred labels): BOTH_RIGHT 7, BOTH_WRONG 4,
  PHONE_WRONG_ACOUSTIC_RIGHT 1, PHONE_RIGHT_ACOUSTIC_WRONG 1

Server: `Scripts/serve_review.py` (packs: final_consonant / window_expanded).
