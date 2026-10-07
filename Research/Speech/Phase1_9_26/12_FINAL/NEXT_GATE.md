# NEXT GATE — WP-1.9.26

**Next gate:** `HUMAN_REVIEW_ROUND_AND_LOCAL_PILOT`

Ordered plan:

1. **Human review round** (no licensing, no GPU): 2 reviewers on the 276-candidate pack; minimum
   60 PRESENT + 60 ABSENT (≥20 speakers) + 15+15 /r/ + 4 TYPE-B labels. Compute kappa; keep
   disagreements.
2. **Local pilot** (design frozen in `09_B2_DESIGN/B2_PILOT_DESIGN.md`): B2-E hybrid features and
   B2-D encoder comparison on 8–10 speaker-disjoint local data with the new labels; FRR-first
   success criteria frozen in `09_B2_DESIGN/B2_SUCCESS_CRITERIA.md`.
3. **Parallel, no-cost**: register a TalkBank account and run the encoder diagnostics on OCSC/JIBO
   (research-only); request the MyST commercial quote; send `LEGAL_QUESTIONS_FOR_COUNSEL.md`.
4. **Decision point**: if the pilot passes FRR-first and improves missing-evidence, authorize the
   data path (MyST license or Vietnamese-L1 partnership) for a full B2; if it fails, stop B2 and
   focus on the acceptance/UX layer.

**B2 TRAINING remains NO** until the pilot results and the license/collection route are in place.
**Production remains frozen** (`production_vad=false · router_locked=false · unity_integrated=false ·
scorer_modified=false · production_window_locked=false`).
