# NEXT GATE DECISION — WP-1.9.18

**Gate status:** **ALIGNMENT_DELETION_GATE_FAIL**

The deletion-aware evidence/decision layer was implemented and measured on speaker-disjoint
data under FRR-first. It does **not** satisfy the pre-registered success criteria.

---

## 1. Success criteria check (spec §14)

| # | criterion | measured | verdict |
|---|---|---|---|
| 1 | unsupported PRESENT materially decreases | LWE unsupported rate 0.3214 → 0.0 (support floor); baseline accepted 5 labeled present tokens with E < 0.1 | PASS |
| 2 | FC absent detection improves or becomes safer | LWE absent decided 0.5833 → 0.9167 (floor 0.30) / 0.8333 (free) | PASS |
| 3 | present recall does not collapse | LWE 0.9375 → 0.6250 (floor) / 0.5625 (free); 6 present tokens rejected (5 lacked any evidence) | **FAIL** |
| 4 | FRR-first performance improves | LWE FRR 0.0625 → 0.375–0.4375 | **FAIL** |
| 5 | improvement survives outside development cases | the same trade appears on held-out so762 test and LWE; no dominance anywhere | FAIL (no improvement to survive) |
| 6 | not merely converting difficult cases into UNCERTAIN | correct: the D rule produced real ABSENT decisions (6 present rejected, 9 absent decided); UNCERTAIN only 2/28 — but that is exactly why criteria 3–4 fail | PASS (mechanism honest) |
| 7 | mechanism explainable from traces | yes: baseline spans stretched into trailing silence (`child_01_nine`); absent /r/ accepted with no support; present finals with zero evidence (`child_06_six` E=0.003) | PASS |

Criteria 3 and 4 are non-negotiable → the gate cannot PASS.

## 2. Why it failed (evidence, not opinion)

1. The pre-registered threshold selection on dev had **no valid operating point**: no rule kept
   present recall ≥ 0.95 (5% of human-present finals have deletion-aware evidence E = 0.0) and
   no rule achieved absent-false-present = 0 (human-absent evidence reaches 0.956 on dev).
2. On the presence-specific LWE labels the trade is direct: every 0.1–0.15 of reduced FAR
   costs ~0.3 of present recall (0.9375 → 0.625).
3. The five LWE present tokens that a support floor rejects have E ≤ 0.066 — the baseline had
   accepted them on top-1 identity with span posteriors as low as 0.0007. The failure is in
   the **evidence**, not only in the decision rule.
4. The alignment mechanism is confirmed and bounded: deletion-aware placement fixes the
   stretched-span cases (`child_01_nine`: 0.0011 → 0.9793) and resolves all zero-evidence /r/
   deletions, but it cannot create evidence for phones the encoder does not represent.

## 3. Falsification statement

1.9.17 concluded **GATE A (alignment/deletion primary)** from the mechanism distribution of
the labeled failures. WP-1.9.18 attempted to falsify that by removing the span-forcing
assumption:

- The alignment/deletion mechanism is **confirmed** (spans into silence; deletion not
  representable; unsupported acceptances).
- The claim that fixing it would yield a net, FRR-first-safe improvement is **contradicted**.
  With current evidence, deletion-aware decisions either reject human-present finals or hide
  them as UNCERTAIN; neither passes the gate.
- The primary **actionable** bottleneck has therefore moved to the **evidence itself**
  (acoustic sensitivity/context — e.g., `child_07_one` full-audio E = 0.0005 vs raw-window
  score 72.5; `child_07_seven` strong evidence vs human ABSENT), together with label limits.

## 4. Follow-up options (spec §13)

**Recommended immediate next step: B (one more bounded boundary/window experiment) +
C (collect more labeled data), NOT B2.**

- **B — bounded boundary/window experiment (no training):** the full-vs-raw window inversions
  (21/80) and `child_07_one` (E 0.0005 full vs raw-window pass) may be a context/crop artifact
  of the encoder or of the pipeline's window. Test whether the same audio under controlled
  boundary conditions yields different final-phone evidence. This is cheap, has no training,
  and is required before concluding "acoustic limitation".
- **C — labeled data:** only one human-present /r/ exists (LOW confidence); present final
  consonants with confident labels are the scarce class. 10–20 present-/r/ and additional
  present final-consonant tokens (Vietnamese-L1 children if possible) are needed to separate
  "encoder cannot hear it" from "labels are wrong".
- **A — acoustic/model research (B2, encoder adaptation):** becomes justified **only after**
  B and C confirm that the evidence gap persists. The current data already suggests it may be
  the limiter, but proceeding now would repeat B1's mistake: changing acoustics without a
  trustworthy decision/evaluation layer.

Do **not** proceed to B2 now. Do **not** modify production. Do **not** integrate Unity.

## 5. Status

```
Speech Research: NOT COMPLETE
Next gate:       ALIGNMENT_DELETION_GATE_FAIL
Recommendation:  B (bounded boundary/window experiment) + C (labeled data) first
Production:      untouched
```
