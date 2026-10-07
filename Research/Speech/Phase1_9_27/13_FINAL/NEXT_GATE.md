# NEXT GATE — WP-1.9.27

**Next gate:** `HUMAN_REVIEW_ROUND_AND_PILOT_EVALUATION`

Ordered plan (changed from WP-1.9.26 only in that the pilot machinery now exists):

1. **Human review round** (no licence, no GPU):
   - Pack R (276) with 2 independent reviewers: minimum 60 PRESENT + 60 ABSENT (>=20 speakers),
     15+15 /r/ consensus (>=10 speakers), all 4 TYPE-B.
   - Pack P (546 pilot tokens) with the same reviewers: minimum 60 PRESENT + 60 ABSENT on the
     speaker-disjoint pilot set.
   - Import: `label_analysis.py` (per pack); compute kappa; preserve disagreements; adjudicate after
     freeze.
2. **Pilot evaluation readout** (CPU): implement/run the frozen B2-D and B2-E configs on the pilot
   test split; apply `09_SUCCESS_CRITERIA/` mechanically; record SUCCESS / PARTIAL / FAILURE /
   INCONCLUSIVE.
3. **Decision point**: a positive/pilot-partial result authorizes a data-route decision (MyST quote
   or OCSC/JIBO research) and, only later and separately, a GPU B2-C work package; a negative result
   stops B2 and keeps the acceptance/UX layer as the primary path.
4. **Parallel, no-cost**: TalkBank registration (OCSC/CAPIL), PERCEPT-R research request, MyST
   commercial quote, counsel questions, JIBO author license confirmation, AusKidTalk terms.

**B2 TRAINING remains NO** until labels + pilot readout + data route are in place.
**Production remains frozen** (`production_vad=false · router_locked=false · unity_integrated=false ·
scorer_modified=false · production_window_locked=false`).
