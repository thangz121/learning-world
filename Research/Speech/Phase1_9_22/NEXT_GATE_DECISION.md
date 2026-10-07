# NEXT GATE DECISION — WP-1.9.22

**Final status:** **ACCEPTANCE_LOGIC_AND_ENCODER_BOTH_REQUIRED**

**Next gate:** **ACCEPTANCE_RULE_REDESIGN_REQUIRED** (research-only; no B2, no production change)

## 1. Required answers

**Why can production accept a target with very low acoustic support?**
Because acceptance is not posterior-driven. Production accepts 527/609 primary-window tokens:
501 via identity credit (`best_obs == target`, 95.1%; 104 of them are rank 2–5, not top-1), 26 via
the similarity path, and **0 via the posterior path** (`span-mean post ≥ 0.25` never fires; variant
E ≡ production on every dataset). Identity is decided in frames where blank owns 0.80–0.999 of the
span-mean mass, and 20.8% of identity accepts are not even the top-1 phone.

**Is TOP-1 identity responsible for a substantial portion of false acceptance when support is weak?**
Yes for the weak-support subset, with a precision nuance: all 14 production false accepts are
identity/similarity decisions; 10/14 have weak support (<0.30), 12/14 are blank-dominated, 10/14
have a competitor above the target in the span mean. The remaining 4/14 have strong support
(0.63–0.96) — those are encoder/acoustic disagreements that no acceptance fix removes.

**Does removing identity credit fix it?**
No. Variant B (production minus identity) rejects **446 true presents** across LWE+dev+test to remove
11 false accepts (and leaves 3 similarity-path false accepts). LWE recall 0.9375 → 0.0; dev
0.9088 → 0.0632; test 0.8943 → 0.0573. FRR-first forbids it. Support gates (C/D) reproduce the
WP-1.9.21 frontier and cannot remove the strong false peaks.

**Is B2 now justified?**
**NO — B2 NOT READY** (see `B2_READINESS.md`: acceptance defect unresolved, identity removal unsafe,
labels insufficient, /r/ data-limited).

## 2. Recommended next steps (in order)
1. **Acceptance-rule redesign (research-only):** rank-aware identity (rank-1 vs rank 2–5), explicit
   margin/blank conditions, a legitimate UNCERTAIN band; FRR-first evaluation on the existing cache
   and the 28 blind labels, then SO762 speaker-disjoint dev/test. No production change.
2. **Label expansion:** run the WP-1.9.21 20-item review pack (weak present finals, /r/ first,
   rank-2–5 identity tokens) with the existing tooling.
3. **Only then:** decide whether the remaining error mass is encoder-level (B2 design), with the
   acceptance rule and labels settled.

## 3. Status
```
Speech Research: NOT COMPLETE
Last gate:       ACCEPTANCE_RULE_REDESIGN_REQUIRED (WP-1.9.22)
Final status:    ACCEPTANCE_LOGIC_AND_ENCODER_BOTH_REQUIRED
B2:              NOT READY
Production:      untouched
```
