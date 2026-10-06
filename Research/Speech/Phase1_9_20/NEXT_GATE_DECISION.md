# NEXT GATE DECISION — WP-1.9.20

**Final gate:** **ALIGNMENT_REPRESENTATION_FAIL**

The counterfactual realignment family (blank-aware / skip-deletion / evidence-weighted /
conservative) does **not** recover the previously classified alignment failures and fails the
negative control. The audit’s real outcome is a **correction of the failure diagnosis**.

## 1. Required answers

**Is alignment now sufficiently understood?**
Yes for the audited mechanism: the only new failure class claimed by 1.9.19 (ALIGNMENT 9/16) is
**not an alignment-placement problem**. 8/9 have their evidence already inside the production
span (spiky evidence diluted by the mean-based aggregation); 1/9 (`child_01_nine`) has its
0.979-posterior frame *before* the span (an earlier occurrence of the same phone class),
unusable by a monotone final-phone alignment. The production forced alignment is monotone,
forced-span and blank-free, but it is not the binding constraint for these cases.

**Is a production alignment change scientifically justified?**
No. Variants B/C/D/E match the current alignment on LWE decisions and slightly increase false
evidence on absent tokens (false recovery 11 vs 8 on 29 absent tokens). No production patch.

**Is B2 now justified?**
**NOT YET.** Alignment is cleared as the main suspect; the surviving mechanisms are
scoring/aggregation (span mean vs spiky evidence), window (3/16), encoder evidence gap (3/16)
and label limits. A research-only aggregation/support rule and more confidently-labeled present
finals should come first.

**Is more labeled data required?**
Yes — specifically confidently-labeled human-present final consonants (esp. /r/, n=1 LOW) to
separate “no evidence” from “label wrong”, and word-position labels to disambiguate repeated
phone classes (needed for honest outside-evidence metrics).

## 2. Recommended next steps (in order)
1. **Research-only aggregation/support rule**: replace span-mean acceptance with span
   max/support + explicit deletion/UNCERTAIN (the 1.9.18 layer already reports E; evaluate it as
   the decision rule with FRR-first and the negative control fixed by word-position masking).
2. **Labels**: 10–20 confidently-labeled present final consonants (esp. /r/) with word-position
   annotations.
3. **Only then** an encoder-level (B2) experiment design — with alignment and aggregation
   defects removed.

## 3. Status
```
Speech Research: NOT COMPLETE
Next gate:       ALIGNMENT_REPRESENTATION_FAIL (alignment is not the binding constraint)
B2:              NOT YET
Production:      untouched
```
