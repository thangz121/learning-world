# ALIGNMENT COUNTERFACTUAL REPORT — WP-1.9.20

**Machine:** ASUS · **Branch tip:** `6fc7158` (= main) · **Scope:** research-only; no production,
model, training or Unity change. **Flags:** all five false.
**Gate:** **ALIGNMENT_REPRESENTATION_FAIL** (see `NEXT_GATE_DECISION.md`)

## 1. Current alignment (reconstructed from source)
`PhoneEvidenceV2.ctc_align` (`87254B7B…`): monotone DP `dp[t][j]`, stay/advance only, **no blank
state, no skips**; every target phone forced to own ≥1 frame; emission = log summed class
posterior; spans merged/split to exactly n; decision uses the **mean** posterior over the span
(+ top-1 identity).

## 2. Counterfactual family (same frozen probs, same targets)
- **A CURRENT** production `ctc_align`.
- **B BLANK**: expanded 2L+1 Viterbi (blank states, no skips).
- **C SKIP**: B + epsilon deletion transitions, penalty δ ∈ {0, 0.5, 1, 2, 4}, normalized
  emissions (target vs all classes + blank).
- **D EVID**: C + λ·GOP(target vs best competitor), λ ∈ {0.5, 1}.
- **E CONS**: C (δ=1) + conservative support rule (PRESENT ≥0.5, UNCERTAIN 0.2–0.5).

Temporal order, monotonicity, neighboring-phone context and blank structure are preserved by
construction (no teleporting); the only new freedom is skipping a phone at a cost.

## 3. Efficiency
No frame-level logits were cached in 1.9.17–19 (verified: only summary rows). The frozen encoder
was re-run **only** for the exact 1.9.19 evaluation sets: 80 LWE tokens + 214 so762 utterances
(dev 96 / test 96 / absent-dev 22). Total ~12 min CPU. No window sweep was recomputed.

## 4. Failure replay — the 9 “ALIGNMENT” cases from 1.9.19
Selection on dev (so762 train-child speakers) picked `C_skip_d1.0` (recovery 35/55) but with
**false-recovery 58.8%** on absent dev tokens — the “outside evidence” metric is contaminated by
other occurrences of the same phone class.

Reclassification (`ALIGNMENT_FAILURE_RECLASSIFICATION.csv`):

| new class | n | meaning |
|---|---:|---|
| FIXED_ALIGNMENT | **0/9** | no case recovered by any counterfactual alignment |
| PARTIALLY_FIXED | 0/9 | — |
| NOT_ALIGNMENT | **8/9** | evidence was already inside the production span (old span max 0.49–0.96, outside ≈0); 1.9.19’s ALIGNMENT label came from comparing a deletion-aware **max** against the production span **mean** — a spiky span diluted by mean-based scoring, not a placement error |
| INCONCLUSIVE | 1/9 | `child_01_nine`: its only high-posterior frame (0.979) is **frame 12, before the production span 16–42** — i.e., an earlier occurrence of /n/ (initial /n/ region), not recoverable evidence for the final phone under monotonicity; the counterfactual correctly deletes the final phone instead of teleporting |

**The 1.9.19 ALIGNMENT classification was wrong for 8/9 cases.** The mechanism behind the original
label is mean-vs-max: production soft_match aggregates the span with a mean (dilution of spiky
evidence); the counterfactual alignment cannot “fix” that because the evidence is already in the
span.

## 5. LWE external (28 blind labels; new decisions use the same rule for all variants: span max ≥0.30)
| | present recall | absent-FP |
|---|---:|---:|
| old production (soft_match) | 0.9375 | 0.4167 |
| new A_current (floor rule) | 0.5625 | 0.0833 |
| new C_skip_d1.0 (selected) | 0.5625 | 0.0833 |

The old-vs-new gap is the **decision rule** (soft_match acceptance vs evidence floor), not the
alignment: variants A and C produce identical LWE metrics. No alignment variant improves recall
or separation. E_cons: UNCERTAIN 0 on LWE (no conservative gain).

Negative control (29 absent tokens: LWE 12 + so762 absent-dev 17): old false-evidence 8, **new
false-evidence 11** — the counterfactual alignment slightly **increases** target evidence on
absent tokens (false alignment recovery), failing the negative control.

## 6. SO762 speaker-disjoint
Dev selection → freeze → held-out test (speaker overlap 0): the selected variant equals
A/C at the decision level; present recall and stability are unchanged, span displacement median
~±100 ms with no systematic recovery of missing evidence. so762 score-0 remains “incorrect or
missed”, used only for alignment/generalization evidence.

## 7. Negative control verdict
**FAIL**: false alignment recovery 11 > old 8 on absent tokens; the dev “alignable” set could not
be separated from other-occurrence evidence, which is exactly the trap the spec warns about.

## 8. Mechanism conclusions
1. The previously named ALIGNMENT failures are **not** alignment failures: the evidence is inside
   the production span; the failure is **scoring aggregation (mean dilution)**, plus
   **other-occurrence/non-monotonic evidence** (child_01_nine) and genuine no-evidence cases.
2. The current forced alignment is **not the binding constraint** for these cases; a
   blank/skip/weighted realignment neither recovers them nor stays clean on absent tokens.
3. Root-cause update: ALIGNMENT 9 → **NOT_ALIGNMENT 8 + INCONCLUSIVE 1**, with the real
   mechanism = scoring/aggregation (spiky evidence), evidence occurrence, window and encoder
   (from 1.9.19: WINDOW 3, ENCODER 3, MIXED 1 unchanged).
4. /r/ remains data-limited (1 present, LOW) and was not tuned.

## 9. Limitations
- `outside-other-occurrence` metric cannot distinguish initial/final occurrences of the same
  phone class without word-position labels (documented; affected the dev selection).
- `blank_fraction` and `new_margin` columns left blank (not computed) in
  `ALIGNMENT_CASE_ANALYSIS.csv`; the mechanism does not depend on them.
- Decision-rule change (floor 0.30) is reported separately from alignment effects.
- Small n (28 LWE labels; 9 prior alignment cases).

## 10. B2 implication
**B2 NOT YET.** The alignment audit removed the largest claimed present-token failure class by
correction, not by a new alignment. The next highest-value work is a **research-only
aggregation/support rule** (use span max/support instead of mean, with FRR-first validation) and
more confidently-labeled present finals — before any encoder work.

## 11. Files
```
Research/Speech/Phase1_9_20/
  ALIGNMENT_COUNTERFACTUAL_REPORT.md   this file
  ALIGNMENT_CASE_ANALYSIS.csv          80 LWE tokens, old/new spans/evidence/decisions + flags
  ALIGNMENT_TRACE.csv                  347 frame-level rows for 6 representative cases
  ALIGNMENT_VARIANT_RESULTS.json       variants, dev selection, LWE/so762 results, negative control
  ALIGNMENT_FAILURE_RECLASSIFICATION.csv  9 prior ALIGNMENT cases reclassified
  NEXT_GATE_DECISION.md
  experiments/ alignment_counterfactual.py, analyze_alignment.py
  artifacts/   alignment_rows.csv (609 rows: 80 LWE + 529 so762)
```
No production file modified; nothing committed yet.
