# NEXT GATE DECISION — WP-1.9.23

**Final status:** **ACCEPTANCE_RULE_AND_ENCODER_BOTH_LIMITING**

**Next gate:** **LABEL_EXPANSION_AND_ENCODER_DESIGN_REQUIRED** (research-only; no B2 training,
no production change)

## 1. Required answers

**Can a new acceptance rule preserve identity recall while rejecting the weak/blank-dominated/
competitor-conflict false accepts?**
No. 0/68 interpretable rules are FRR-first safe on SO762 dev. The best available trade-off
(`D2_identity_blank_occ1.0`) removes 3/10 TYPE A false accepts (all similarity-path) and costs 4.6
dev recall points; rules that remove 8–9/10 TYPE A cost 21.8–51.6 points. The weak true-present and
weak false-accept distributions overlap (both blank-dominated, low-support, often negative-margin),
and 4/14 false accepts are strong encoder-side (TYPE B) and persist in every rule.

**Is the acceptance layer or the encoder the limiter?**
Both. The decision layer cannot separate the two weak classes with the current frozen evidence, and
the encoder produces strong false evidence for 4 absent phones (0.63–0.96) plus no evidence for the
weakest true presents (`child_06_six` 0.0007, `child_07_one` 0.013). Neither side alone is
sufficient.

**Is B2 now justified?**
**NO — B2 NOT READY** (`B2_READINESS.md`: acceptance defect unresolved, weak false accepts not
materially reduced, /r/ and weak-present labels insufficient). No training.

**What is the binding external-validation blocker?**
Human labels. 0 new labels were obtainable in-session; a 23-item READY_FOR_HUMAN_REVIEW pack is
prepared (`HUMAN_LABEL_REVIEW_PACK.csv` + `HUMAN_LABEL_REVIEW_INSTRUCTIONS.md`), /r/ first.

## 2. Recommended next steps (in order)
1. **Human label round** on the prepared pack (10–20 confident PRESENT finals, /r/ priority,
   weak-support present, rank-2..5 identity, strong false-accept candidates). No labels may be
   fabricated; UNCERTAIN stays UNCERTAIN.
2. **Design-only encoder-evidence study** (no training, no production change): why do weak finals
   lose all evidence and strong false peaks appear (blank domination, window crop, phone-class
   confusability)? Reuse the frame cache and the new labels.
3. **Only after 1–2** decide whether an encoder-level (B2) design is justified; no training.

## 3. Status
```
Speech Research: NOT COMPLETE
Last gate:       LABEL_EXPANSION_AND_ENCODER_DESIGN_REQUIRED (WP-1.9.23)
Final status:    ACCEPTANCE_RULE_AND_ENCODER_BOTH_LIMITING
B2:              NOT READY (no training)
Production:      untouched
Labels:          NEW_LABELS_COLLECTED = 0 (pack ready, 23 candidates)
```
