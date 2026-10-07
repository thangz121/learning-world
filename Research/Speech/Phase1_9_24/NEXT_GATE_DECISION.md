# NEXT GATE DECISION — WP-1.9.24

**Final gate:** **LABELS_INSUFFICIENT_ENCODER_HYPOTHESIS_SUPPORTED**

**Next gate:** **LABEL_COLLECTION_AND_DATA_LICENSE_GATE** (research-only; no B2 training,
no production change)

## 1. Required answers

**Does the evidence justify a future B2 experiment?**
Partially and asymmetrically. The **missing-evidence** encoder limitation is supported: 87/528
labeled PRESENT tokens (16.5%) have max_A < 0.02, including human listening labels with HIGH
confidence (`child_06_six` PROBABLY_PRESENT, `child_07_one` CLEARLY_PRESENT), and the alternative
local encoder recovers strong evidence for 3/10 no-evidence so762 presents and 2 LWE weak presents.
The **strong false-evidence** claim (TYPE-B) is NOT confirmed: 0/4 pass the mandatory falsification
(3/4 lack a confident human listening label; 2/4 are isolated one-frame peaks; the only confident
case, `child_07_seven`, fails the neighboring-support test).

**Can the current evidence separate encoder limitation from label ambiguity?**
No for the false-evidence side (human labels insufficient); yes for the missing-evidence side
(confident human PRESENT labels with no model evidence). This is why the gate is
LABELS_INSUFFICIENT_ENCODER_HYPOTHESIS_SUPPORTED, not INCONCLUSIVE.

**What did the human label expansion achieve?**
The 23-candidate pack was classified and blinded (11 PRESENT / 9 ABSENT / 3 UNCERTAIN candidates);
`HUMAN_LABEL_RESULTS.csv` is empty because no reviewer was available in-session.
**NEW_LABELS_COLLECTED = 0**; no labels were fabricated. Existing labels remain 28 LWE blind
(16P/12A; /r/ 1P LOW / 5A) plus so762 phone scores.

**Is B2 now justified?**
**NO training.** B2 DESIGN: NOT READY (target specified, but labels, licensed child data and a GPU
are missing). B2 LICENSE: BLOCKED for child corpora (SIAK legal review open, MyST unverified,
OGI/CMU restricted, Vietnamese-L1 corpus nonexistent); model licenses are apache-2.0 CLEAR.

## 2. Recommended next steps (in order)
1. **Label collection** on `HUMAN_LABEL_REVIEW_PACK_FINAL.csv` (10–20 confident PRESENT finals,
   /r/ first; TYPE-B ABSENT labels; rank-2..5 identity). No machine-derived labels.
2. **License resolution** before any child-data use: SIAK CC-BY-ND legal review; MyST audit;
   confirm speechocean762 CC BY 4.0 scope for derived use; keep OGI/CMU out of commercial paths.
3. **Research-only design study** (no training): B2-E hybrid acoustic/phonetic evidence and a
   broader B2-D local encoder comparison, validated FRR-first on the existing cache + new labels.
4. **Only after 1–3** decide whether B2-C (encoder adaptation) is executable; it additionally
   requires a GPU that ASUS does not have.

## 3. Status
```
Speech Research: NOT COMPLETE
Last gate:       LABEL_COLLECTION_AND_DATA_LICENSE_GATE (WP-1.9.24)
Final gate:      LABELS_INSUFFICIENT_ENCODER_HYPOTHESIS_SUPPORTED
B2 TRAINING:     NO
B2 DESIGN:       NOT READY
B2 TARGET:       missing/weak acoustic evidence for weak child final consonants
                 (liquids /r/, /l/ first; one-frame finals; blank-dominated endings)
B2 REQUIRED DATA: 10-30 h child speech with phone-level labels; >=30-50 speakers;
                 speaker-disjoint; ages 4-6 preferred (Vietnamese-L1 ideal, nonexistent)
B2 REQUIRED LABELS: 10-20 confident PRESENT finals (/r/ first) + TYPE-B ABSENT labels
B2 LICENSE:      BLOCKED (child corpora) / CLEAR (models)
Production:      untouched
```
