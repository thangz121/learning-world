# NEXT GATE - WP-1.9.28

**Next gate:** `HUMAN_REVIEW_ROUND_AND_PILOT_EVALUATION` (unchanged; now with the import validator
and combined pack ready)

Ordered steps:

1. Human review (Pack R + Pack P) per `14_DECISION/HUMAN_REVIEW_PENDING.md`.
2. Import + agree + gate:
   - `experiments/import_review_exports.py` -> cleaned CSVs + `PACK_COMBINED.csv`;
   - `Phase1_9_27/experiments/label_analysis.py --pack .../PACK_COMBINED.csv --csv ...` -> consensus,
     kappa, sufficiency.
3. If pilot labels sufficient: frozen evaluation once per config:
   baseline -> B2-D (D0/D1/D3/D4/D5) -> B2-E (BASE/E_ENC/E_ACO/E_TMP/E_ENC_ACO) -> /r/ -> final
   consonants -> per-speaker -> assessability -> TYPE-B replay -> failure replay -> kill switch ->
   GO/NO-GO (A/B/C/D of WP-1.9.28 Part 27).
4. Parallel no-cost: TalkBank registration, PERCEPT-R request, MyST quote, counsel questions, JIBO
   author confirmation.

**B2 TRAINING remains NO** until a future gate explicitly authorizes it.
**Production remains frozen** (`production_vad=false · router_locked=false · unity_integrated=false ·
scorer_modified=false · production_window_locked=false`).
