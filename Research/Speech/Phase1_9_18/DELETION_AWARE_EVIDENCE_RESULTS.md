# DELETION-AWARE EVIDENCE & DECISION LAYER — WP-1.9.18 RESULTS

**Machine:** ASUS · **Branch tip used:** `e8d96da` (= main tip; branch not switched)
**Scope:** research-only. No production change, no training, no B2, no Unity.
**Flags:** `production_vad=false · router_locked=false · unity_integrated=false ·
scorer_modified=false · production_window_locked=false`

**Gate:** **ALIGNMENT_DELETION_GATE_FAIL** (see `NEXT_GATE_DECISION.md`)

---

## 1. Research question

If target phones may be **PRESENT / ABSENT / UNCERTAIN** without forcing every phone to own a
span, can final-consonant decisions become more reliable under speaker-disjoint, FRR-first
evaluation?

**Measured answer: NO for the current evidence.** Deletion-aware decisions materially reduce
false PRESENT on absent finals (FAR 0.4167 → 0.0833–0.1667 on LWE) but collapse present recall
(0.9375 → 0.5625–0.625); the FRR-first criterion fails, and the pre-registered threshold
objectives are unsatisfiable because human-present finals frequently have **no evidence at
all** in the frozen model.

## 2. What was implemented (research-only)

`experiments/deletion_aware_decision.py` — imports `PhoneEvidenceV2` read-only and replaces
only the decision layer for target phones:

```
audio (16 kHz) → frozen wav2vec2 logits → probs
   ├── baseline: production ctc_align span + soft_match decision (replicated, 0/80 mismatch)
   └── deletion-aware: blank-interleaved (2L+1) Viterbi with skip transitions
        per final phone:
          E        = da_span_max   max class posterior at the assigned final-phone frame(s)
          margin   = (score forced-present − score forced-absent)/T   (global LLR)
          free     = final phone visited with zero skip penalty
          frame_max / sustained / best_span  (alignment-free peaks)
          gop_ratio vs competing classes
```

Decisions:
- **A baseline** production.
- **B deletion-aware free**: PRESENT iff the free path visits the phone.
- **C recall-first** (E ≥ τp PRESENT, E < τa ABSENT, else UNCERTAIN) — thresholds to be
  selected on dev with recall ≥ 0.95.
- **D safety-first** (E ≥ τp PRESENT; E < τa AND free-deleted AND margin ≤ 0 ABSENT; else
  UNCERTAIN) — thresholds selected on dev with absent-false-present = 0.

**No threshold dependencies on the evaluation sets**; diagnostic points (0.20/0.30) are
labelled as diagnostic only.

## 3. Datasets and speaker split

| set | corpus | speakers | utterances | final phones | present | absent |
|---|---|---|---:|---:|---:|---:|
| dev | so762 train children (first 24 spk) | 24 | 480 | 1,339 | 1,322 | 17 |
| test (held out) | so762 test children (first 24 spk) | 24 | 480 | 1,305 | 1,262 | 43 |
| external | LWE real-child blind finals (1.9.12) | 9 | 80 | 28 | 16 | 12 |

- Speaker overlap dev×test = 0; LWE never used for selection.
- **Label caveat:** so762 phone score 0 = "incorrect **or missed**" — it mixes deletion with
  substitution; so762 is used for threshold development and scale, while the LWE 28
  presence-specific labels are the deletion benchmark.
- 19 LWE tokens with human word-level UNCERTAIN verdicts remain separate (see §7).

## 4. Results (measured)

`present recall` = P(PRESENT | human present). `absent FP` = P(PRESENT | human absent).
`absent decided` = P(ABSENT | human absent). UNCERTAIN excluded from both.

| set | variant | present recall | absent FP | absent decided | uncertain P/A | coverage |
|---|---|---:|---:|---:|---:|---:|
| dev | A baseline | 0.8896 | 0.7059 | 0.2941 | 0 / 0 | 1.00 |
| dev | B deletion-aware | 0.7579 | 0.5294 | 0.4706 | 0 / 0 | 1.00 |
| dev | C floor 0.20 (diag) | 0.7133 | 0.2941 | 0.7059 | 0 / 0 | 1.00 |
| test | A baseline | 0.8693 | 0.4651 | 0.5349 | 0 / 0 | 1.00 |
| test | B deletion-aware | 0.7092 | 0.3488 | 0.6512 | 0 / 0 | 1.00 |
| test | C floor 0.30 (diag) | 0.6490 | 0.1163 | 0.8837 | 0 / 0 | 1.00 |
| **LWE** | **A baseline** | **0.9375** | **0.4167** | **0.5833** | 0 / 0 | 1.00 |
| **LWE** | **B deletion-aware** | **0.5625** | **0.1667** | **0.8333** | 0 / 0 | 1.00 |
| **LWE** | **C floor 0.30 (diag)** | **0.6250** | **0.0833** | **0.9167** | 0 / 0 | 1.00 |
| **LWE** | **D floor 0.30/0.10 (diag)** | **0.6250** | **0.0833** | **0.7500** | 0 / 0.1667 | 0.93 |

Threshold selection results: **no valid point exists** — recall ≥ 0.95 is unattainable on dev
(5% of human-present finals have E = 0.0), and absent-false-present = 0 is unattainable
(dev human-absent E reaches 0.956).

## 5. Why: evidence distributions (E = deletion-aware final-phone evidence)

| set | present E p5 / p25 / median | absent E p50 / p75 / p90 / max |
|---|---|---|
| dev | 0.0000 / 0.095 / 0.838 | 0.0049 / 0.682 / 0.799 / **0.956** |
| test | 0.0000 / 0.048 / 0.770 | 0.0002 / 0.014 / 0.262 / 0.982 |
| LWE | 0.0005 / 0.029 / 0.592 | 0.0243 / 0.056 / 0.154 / **0.629** |

- **Human-present finals with no evidence:** on LWE, 6/16 present tokens have E < 0.09
  (`child_01_seven` 0.059, `child_01_ten` 0.029, `child_02_ten` 0.066, `child_04_four` 0.003,
  `child_06_six` 0.003, `child_07_one` 0.0005); on dev, present E p5 = 0.0. Any support floor
  therefore rejects them.
- These same tokens are the baseline's **unsupported acceptances** — accepted on top-1
  identity with span posterior as low as 0.0007. The baseline "worked" on them by accepting
  without support; the deletion-aware layer refuses to do that, which is honest but raises FRR.
- Human-absent finals mostly have low E (LWE p90 = 0.154), except `child_07_seven` (0.629,
  strong nasal evidence against a human ABSENT label — mixed/annotation conflict).

## 6. Alignment mechanism (confirmed, but not sufficient)

- `child_01_nine` (/n/): baseline span 16–42 with span posterior **0.0011** vs deletion-aware
  assigned frame 12 with E **0.9793** — the production DP stretched the final phone into
  trailing silence; the word's phones actually end around frame 12.
- `child_07_one` (/n/, human CLEARLY_PRESENT): full-audio E = 0.0005 but the raw VAD window
  scores 72.5 (1.9.11) — boundary/context dependent.
- 21/80 tokens flip between full and raw windows (1.9.17); 25/80 baseline-vs-deletion-aware
  presence disagreements.
- The **margin LLR is not a reliable presence gate** for final phones: it is negative for
  several strongly-evidenced present tokens (global path comparison; trailing silence/blank
  wins). Documented and reflected in the decision rules (E is the support; margin only
  qualifies the ABSENT branch).

## 7. Known-failure replay (LWE 28) under the diagnostic D rule (E floor 0.30/0.10)

| case | human | baseline | D decision (reason) | outcome |
|---|---|---|---|---|
| child_01_four /r/ | ABSENT | soft (72.5) | ABSENT (ABSENT_DELETION) | **fixed** |
| child_03_four /r/ | ABSENT | exact (100) | ABSENT (ABSENT_DELETION) | **fixed** |
| child_06_four /r/ | ABSENT | exact (100) | ABSENT (ABSENT_DELETION) | **fixed** |
| child_02_four /r/ | ABSENT | exact (73.5) | UNCERTAIN (COMPETING_SPANS) | not fixed (honest) |
| child_07_seven /n/ | ABSENT | exact (20) | PRESENT (PRESENT_SUPPORTED, E=0.63) | still wrong (mixed) |
| child_07_one /n/ | PRESENT | miss (0) | ABSENT (E=0.0005) | still wrong (acoustic/boundary) |
| child_01_seven /n/ | PRESENT | exact (20) | ABSENT (no evidence, E=0.059) | **regressed (FRR)** |
| child_01_ten /n/ | PRESENT | exact (100) | ABSENT (E=0.029) | **regressed (FRR)** |
| child_02_ten /n/ | PRESENT | exact (100) | ABSENT (E=0.066) | **regressed (FRR)** |
| child_04_four /r/ | PRESENT (LOW) | exact (66.7) | ABSENT (E=0.003) | **regressed (FRR)** |
| child_06_six /s/ | PRESENT | exact (25) | ABSENT (E=0.003) | **regressed (FRR)** |

Fixed 3, regressed 5 (all regressions are human-present tokens the baseline accepted with no
acoustic support). Deletion awareness resolves the absent-/r/ class exactly; it cannot resolve
present finals for which the frozen encoder has no evidence.

## 8. Human-uncertain cases (kept separate)

19 LWE tokens with word-level UNCERTAIN verdicts (no final labels):
- baseline: PRESENT 18 / ABSENT 1 (decisive despite human uncertainty);
- D diagnostic: PRESENT 12 / ABSENT 5 / UNCERTAIN 2 (less overconfident than baseline);
- their E evidence: STRONG 11, MODERATE 2, WEAK 2, NONE 4.
No ground truth was assigned; uncertainty was not converted either way.

## 9. Assessability boundary

Nothing here infers assessability from phone evidence: the 1.9.15 independent refusal states
are untouched; the assessability gate, phone evidence and pronunciation remain separate
layers. No assessability decision was changed or derived.

## 10. Root cause after WP-1.9.18 (falsification update)

1.9.17 prior: DELETION 3 / MIXED 2 / ALIGNMENT 1. The new experiment **confirms the alignment
and deletion mechanisms** (baseline spans stretched into silence; absent /r/ accepted with no
support; both reproducible) but **contradicts the hypothesis that fixing deletion/decision
logic yields a net improvement**: every gain in absent detection is paid for by rejecting
human-present finals that have no evidence. Updated classification of the 6 labeled
disagreements under D:

```
fixed by deletion-aware decision : 3/6
regressed (present, no evidence) : 5  (new FRR cost)
remaining wrong                  : child_07_one (acoustic/boundary), child_07_seven (mixed),
                                   child_02_four (weak evidence -> UNCERTAIN)
```

The primary **actionable** bottleneck is therefore the **evidence itself** (acoustic
sensitivity/context), with alignment/deletion a real but now-measured-and-bounded mechanism.

## 11. Limitations

- LWE labeled finals n=28 (single reviewer, 9 children); one present /r/ (LOW confidence).
- so762 "absent" mixes deletion and substitution (score 0 = incorrect or missed), which
  inflates dev absent FP at baseline; LWE labels are presence-specific.
- The margin LLR is a global comparison and is systematically negative near trailing silence;
  it was not used as a hard gate.
- The DA forced-present span is often 1 frame; E is a peak evidence measure, not a duration
  measure. Stop bursts are single-frame, so `sustained` was rejected as a support statistic.
- Diagnostic thresholds (0.20/0.30) are reported for mechanism exposure, not tuning.

## 12. Files

```
Research/Speech/Phase1_9_18/
  DELETION_AWARE_EVIDENCE_RESULTS.md   this file
  FAILURE_CASE_ANALYSIS.csv            80 LWE tokens, decisions A/B/C/D + reasons + root cause
  EXPERIMENT_RESULTS.json              variants v2, frontiers, diagnostics, distributions
  DECISION_TRACE_EXAMPLES.md           representative traces
  NEXT_GATE_DECISION.md                gate + follow-up
  experiments/  deletion_aware_decision.py, variants_v2.py, analysis_and_export.py,
                _print_diag.py
  artifacts/    lwe_phone_evidence.csv, so762_dev_finals.csv, so762_test_finals.csv
```
No production file was modified; nothing was committed or pushed.
