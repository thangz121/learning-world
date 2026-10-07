# NEXT GATE DECISION — WP-1.9.21

**Final gate:** **SUPPORT_AGGREGATION_FAIL**

A temporally plausible, position-masked support/aggregation rule does **not** improve the FRR-first
tradeoff over the production span-mean + top-1-identity decision. The aggregation hypothesis from
WP-1.9.20 is now evaluated and closed as the primary path.

## 1. Required answers

**Can support aggregation preserve mean-destroyed present evidence?**
Partially, and only for evidence that already exists: the strong spiky presents (in-span max
0.49–0.96, span mean 0.003–0.096) are recovered by max/temporal support — but production already
accepts them through top-1 identity, so recall does not change. The genuinely missed/weak presents
(`child_06_six` 0.0007, `child_07_one` 0.013, `child_01_nine` final /n/ 0.0155) have no temporally
plausible in-region support in any window.

**Does any research rule beat the production operating point safely?**
No. No posterior-support setting reaches the production dev recall (0.9088; best 0.9018 with
`MAX τ=0.01`). On LWE, matching production recall (0.9375) requires τ ≤ 0.01, where FAR rises
0.4167 → 0.75 and 13/16 PRESENT decisions are unsupported. At the safe plateau (FAR 0.0833) recall
falls to 0.5625. The frontier is dominated by the production point in FRR-first terms.

**Is position masking validated?**
Yes. Earlier same-class occurrences are excluded: `child_01_nine` (0.979 earlier /n/) and
`child_03_six` (0.919 earlier /s/, shifted windows only) are correctly classified WRONG_OCCURRENCE;
an unmasked global-max control would have claimed `child_01_nine` as a false recovery. No candidate
variant used the earlier occurrence.

**Is a production scorer change justified?**
No. No production file/threshold was modified.

**Is B2 now justified?**
**NOT YET.** The remaining present failures are encoder no-evidence / window artifacts and the
false-positive floor is identity-driven acceptance plus one strong false peak (`child_07_seven`
0.63). More confidently-labeled present finals (esp. /r/, currently n=1 LOW) are required before any
encoder-level work, and the acceptance/identity logic should be re-measured FRR-first.

**Is more labeled data required?**
Yes. 0 new confident labels were obtainable in-session (no reviewer); a 20-item candidate pack
(`LABEL_CANDIDATES.csv`) is prepared for a human review round using the existing tooling.

## 2. Recommended next steps (in order)
1. **Acceptance/identity audit (research-only)**: why does top-1 identity accept weak/no-evidence
   finals (and weak false positives) while posterior support cannot? Re-measure FRR-first on the
   existing cache; keep the position mask and PRESENT/ABSENT/UNCERTAIN separation.
2. **Labels**: run the prepared review pack (10–20 clips, /r/ priority) and add trusted present
   finals; only then re-assess.
3. **Only after 1–2**: encoder-level (B2) design, with alignment and aggregation defects removed.

## 3. Status
```
Speech Research: NOT COMPLETE
Last gate:       SUPPORT_AGGREGATION_FAIL (WP-1.9.21)
B2:              NOT YET
Production:      untouched
Frame cache:     CREATED (reusable; artifacts/frame_cache, 2,436 token-windows / 117,128 frames)
```
