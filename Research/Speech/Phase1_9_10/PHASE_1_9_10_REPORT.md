# PHASE 1.9.10 REPORT — Deep-Dive 12 Phone-Model-Error Cases

**Base:** Phase 1.9.9 `e2449c5`  
**Date:** 2026-10-03 — **methodology repaired in Phase 1.9.11** (three human states)  
**production_vad:** false · **router_locked:** false · **unity_integrated:** false · **scorer_modified:** false  
**asr_is_not_pronunciation_judge:** true

> **Repair notice (1.9.11):** an earlier revision treated Stage B `UNCERTAIN` as
> "acceptable" and carried stale 8/12–4/12 text. This report now uses three distinct
> human states and keeps the pme_01 conflict visible. All counts derive from the
> submitted records.

---

## 1. Executive Summary

The 12 PHONE_MODEL_ERROR cases from Phase 1.9.9 were re-run through the frozen pipeline with
full/raw/padded variants (0–500 ms), CTC span inspection, parselmouth acoustics (F0/F1/F2),
phone confusion analysis, an adult diagnostic control — then reviewed by the human second pass
(Stage A blind + Stage B evidence).

**Human state model:**

| state | n | cases |
|---|---:|---|
| HUMAN_TRUE_ERROR (clean) | **4** | pme_03, 05, 09, 10 |
| HUMAN_UNCERTAIN | **7** | pme_02, 04, 06, 07, 08, 11, 12 |
| HUMAN_CONFLICTED (Stage A vs B) | **1** | pme_01 |
| HUMAN_ACCEPTABLE (explicit) | **0** | — |

Raw Stage B distribution (source of truth): **TRUE_PRONUNCIATION_ERROR = 5, UNCERTAIN = 7**.  
`HUMAN_UNCERTAIN` is **not** counted as acceptable and is **not** human ground truth.

| Finding | Value |
|---|---|
| Boundary rescue (raw&lt;50 → pad≥50, or +≥20) | **4/12 (33%)** |
| Crop-beats-full inversion (raw≥50 & full&lt;50) | **2/12** |
| Word-initial mismatches | **12/12 initial phones (100%)** |
| Mismatched CTC spans &lt;50 ms | **15/29 (52%)**, median 21 ms |
| Adult control exact-match rate (6 words) | **0.56** (blue/dog/big also fail for adult) |
| F0 median error-cases vs OK group | **281.5 vs 280.7 Hz — no separation** |
| Revised 1.9.9 metric | 0.414 superseded → **0.0 (0/17 confirmed)**, 8 unresolved |
| Decision | **B. ROOT_CAUSE_PARTIALLY_IDENTIFIED** |

**Headline:** 5/12 were explicitly judged true pronunciation errors; 7/12 remained uncertain
at Stage B and cannot be treated as human ground truth. Measured mechanisms for the 7 uncertain
cases are a **diagnostic hypothesis**: boundary 3, full-file CTC alignment 1, phone-model
attractor 1, phoneme realization variation 2.

**No scorer change is implemented or justified yet.**

---

## 2. Frozen Baseline

- CMUdict canonical target, Silero 0.5, soft-v2, CTC alignment — unchanged
- Phase 1.9.9 artifacts used as authoritative case source (`human_review_results.csv`)
- No model replacement, no retraining, no threshold change, no calibration
- Adult control: `Research/Speech/Phase1_1/audio/sapi_*.wav` (existing adult pipeline data)

---

## 3. Phase 1.9.9 Findings Being Investigated

- `human_correct_low_score_rate = 0.414` (12/29 first-pass-correct tokens &lt;50)
- 12 cases classified PHONE_MODEL_ERROR (first-pass correct + low score + phone misses)
- ASR weak (5/42 correct); `ASR_EMPTY != pronunciation_error`

This phase asks: **why did the phone evidence fail on these 12?**

---

## 4. The 12 PHONE_MODEL_ERROR Cases

| case | speaker | target | Stage A | Stage B | full | raw | best pad | measured mechanism | state |
|---|---|---|---|---|---:|---:|---|---|---|
| pme_01 | child_03 | eight | CLEAR_CORRECT | TRUE_ERROR | 0.0 | 50.0 | 0.0 | CTC_ALIGNMENT (crop&gt;full) | **CONFLICTED** |
| pme_02 | child_06 | two | PROBABLY_CORRECT | UNCERTAIN | 0.0 | 0.0 | 50.0 | BOUNDARY_ERROR | UNCERTAIN |
| pme_03 | child_07 | eight | PROBABLY_INCORRECT | TRUE_ERROR | 0.0 | 0.1 | 0.0 | CTC_ALIGNMENT (short span) | TRUE_ERROR |
| pme_04 | child_07 | one | CLEAR_CORRECT | UNCERTAIN | 0.0 | 72.5 | 72.5 | CTC_ALIGNMENT (crop&gt;full) | UNCERTAIN |
| pme_05 | child_08 | eight | PROBABLY_INCORRECT | TRUE_ERROR | 0.0 | 0.0 | 0.0 | GENERALIZATION (attractor n,n) | TRUE_ERROR |
| pme_06 | child_04 | three | CLEAR_CORRECT | UNCERTAIN | 11.7 | 6.7 | 39.2 | BOUNDARY_ERROR | UNCERTAIN |
| pme_07 | child_01 | seven | CLEAR_CORRECT | UNCERTAIN | 20.0 | 40.1 | 44.1 | GENERALIZATION (attractor l,l,l) | UNCERTAIN |
| pme_08 | child_06 | six | CLEAR_CORRECT | UNCERTAIN | 25.0 | 0.0 | 25.1 | BOUNDARY_ERROR | UNCERTAIN |
| pme_09 | child_09 | five | PROBABLY_INCORRECT | TRUE_ERROR | 33.4 | 33.4 | 33.4 | GENERALIZATION | TRUE_ERROR |
| pme_10 | child_01 | five | PROBABLY_INCORRECT | TRUE_ERROR | 33.5 | 39.2 | 66.7 | BOUNDARY_ERROR | TRUE_ERROR |
| pme_11 | child_03 | three | CLEAR_CORRECT | UNCERTAIN | 39.2 | 39.2 | 39.2 | REALIZATION (θ→t) | UNCERTAIN |
| pme_12 | child_08 | five | CLEAR_CORRECT | UNCERTAIN | 39.2 | 39.2 | 39.2 | REALIZATION (f→v) | UNCERTAIN |

Artifacts: `phone_model_error_cases.csv`, `phone_model_error_root_causes.csv`,
`human_second_pass.csv`

---

## 5. Case-by-Case Forensics (condensed)

**pme_01 eight (child_03)** — canonical `EY1 T`, observed full `d æ`; raw crop 50 &gt; full 0.
Stage A said CLEAR_CORRECT but Stage B said TRUE_ERROR → **CONFLICTED**, not clean truth.

**pme_02 two (child_06)** — canonical `T UW1`; observed `ts.` + `ŋ`; raw 0 → pad_100 = 50.

**pme_03 eight (child_07)** — observed `l p`; one mismatched span &lt;50 ms; no variant recovers.

**pme_04 one (child_07)** — canonical `W AH1 N`, observed `k k l`; raw window **72.5** while full
file **0.0**. Strongest crop-beats-full inversion.

**pme_05 eight (child_08)** — observed `n n` (attractor collapse); no variant recovers.

**pme_06 three (child_04)** — observed `t w l`; θ→t and ɹ→w documented substitutions; padding
lifts 6.7→39.2.

**pme_07 seven (child_01)** — canonical `S EH1 V AH0 N`, observed `l l l j n`; `l` repeated 3×.

**pme_08 six (child_06)** — observed `l l ɹ`; padding lifts 0→25.1.

**pme_09 five (child_09)** — observed `l aɪ n`; no boundary rescue.

**pme_10 five (child_01)** — observed `p aɪ ɑ`; raw 39.2 → pad_100 **66.7**; human says true error.

**pme_11 three (child_03)** — observed `t ɹ t`; θ→t documented substitution.

**pme_12 five (child_08)** — observed `v aɪ t`; f→v documented voicing substitution.

---

## 6. Canonical vs Observed Phones

Per-case canonical/observed strings and mismatch lists are in
`phone_model_error_root_causes.csv` (`mismatch_type` column, e.g. `θ->t@initial`).

---

## 7. Phone Confusion Analysis

Top observed substitutions (`phone_confusion_matrix.csv`):

| expected | observed | count | note |
|---|---|---|---|
| θ | t | 2 | th-stopping (three) |
| s | l | 2 | attractor |
| eɪ | l | 2 | attractor |
| ɹ | w | 1 | gliding (three) |
| f | v | 1 | voicing (five) |
| f | p | 1 | stopping |
| ɛ/ɪ/iː/v/f | l | 1 each | `l` attractor ≈ 8 total |

**Pattern:** the posterior landscape collapses toward `l` (and `n`) on short child tokens when
acoustics don't match model expectation. Frequency alone is not causal proof.

---

## 8. Word Position Analysis

| Position | Total phones | Mismatched | Rate |
|---|---:|---:|---:|
| word-initial | 12 | 12 | **100%** |
| word-medial | 11 | 7 | 64% |
| word-final | 12 | 10 | **83%** |

**22/29 mismatches sit at word edges.**

---

## 9. Boundary Analysis

| case | raw | full | best pad | rescue | to usable | crop&gt;full |
|---|---:|---:|---|---|---|---|
| pme_01 | 50.0 | 0.0 | 0.0 | no | no | **yes** |
| pme_02 | 0.0 | 0.0 | 50.0 | yes | **yes** | no |
| pme_04 | 72.5 | 0.0 | 72.5 | no | no | **yes** |
| pme_06 | 6.7 | 11.7 | 39.2 | yes | no | no |
| pme_08 | 0.0 | 25.0 | 25.1 | yes | no | no |
| pme_10 | 39.2 | 33.5 | 66.7 | yes | **yes** | no |
| others | — | — | — | no | no | no |

- **Rescue definition:** `raw<50 AND best_pad>=50` OR `best_pad-raw>=20` (explicit)
- BOUNDARY_RESCUE_RATE = **4/12 (33%)**; to-usable = **2/12 (17%)**
- 2 additional cases are the inverse problem: crop works, full file fails.

---

## 10. CTC Alignment Analysis

- 29 mismatched phones; **15/29 (52%) have spans &lt;50 ms** (median 21 ms, p75 512 ms)
- Some mismatches absorb very long spans (max 2.23 s) → degenerate alignment
- Artifact: `ctc_span_analysis.csv`

---

## 11. Duration Analysis

- PME utterance duration mean **0.92 s**; OK group **1.06 s** (descriptive)
- Speaking rate: mean **3.59 phones/s** (range 1.32–4.87); no rate regime isolated at n=12
- Artifact: `duration_analysis.csv`

---

## 12. Child Acoustic Analysis

Same parselmouth method for both groups (`acoustic_group_comparison.csv`):

| feature | error cases (n=12) | OK group (n=17) |
|---|---:|---:|
| F0 median (Hz) | 281.5 | 280.7 |
| F0 range (Hz) | 279.7 | 281.3 |
| F1 median (Hz) | 947.9 | 900.5 |
| F2 median (Hz) | 2452.1 | 2298.1 |
| duration (s) | 0.92 | 1.06 |

---

## 13. F0 Investigation

**F0 does not separate the error group from the agreement group** (281.5 vs 280.7 Hz).

**PITCH_CAUSALITY_NOT_ESTABLISHED.**

---

## 14. F1/F2 Investigation

F1 and F2 are 5–7% higher in the error group; distributions overlap; n=12/17 too small for a
statistical claim. No normalization applied or justified.

**FORMANT_CAUSALITY_NOT_ESTABLISHED.**

---

## 15. Speaking Rate Investigation

No clustering of failures in a specific rate regime at n=12. The failure signature is span
allocation at word edges, not speech tempo.

---

## 16. Word-Specific Analysis

| word | error-case n | speakers | mean full |
|---|---:|---|---:|
| eight | 3 | child_03, 07, 08 | 0.0 |
| five | 3 | child_01, 08, 09 | 35.4 |
| three | 2 | child_03, 04 | 25.5 |
| one/two/six/seven | 1 each | — | 0–25 |

`eight` fails for 3 different children with 0.0 scores and collapse patterns. Sample too small
to call any word "bad".

---

## 17. Speaker-Specific Analysis

No child dominates; 7 children contribute 1–2 cases each. No developmental or medical
interpretation is made or implied.

---

## 18. Adult vs Child Diagnostic Comparison

| word | score | exact rate |
|---|---:|---:|
| red | 100 | 1.00 |
| cat | 100 | 1.00 |
| blue | 5.8 | 0.00 |
| big | 33.3 | 0.33 |
| book | 66.7 | 0.67 |
| dog | 33.3 | 0.33 |

- Adult mean exact rate **0.56** vs child error-case mean **0.15**
- **blue/dog/big also fail for adult speech** → fragility is not exclusively child-triggered
- Caveat: different words, diagnostic only. **CHILD_SPECIFIC_PHONE_FAILURE = PARTIAL**

---

## 19. Human Second-Pass Review

Pack: `HumanReview/review_blind.html` (Stage A) + `review_reveal.html` (Stage B), submitted
over LAN (`serve_review.py` → `Results/Human_SecondPass_StageA/B_Filled.csv`).

**State model (repaired):** Stage B TRUE → HUMAN_TRUE_ERROR; Stage B UNCERTAIN → HUMAN_UNCERTAIN
(not acceptable); Stage A correct + Stage B true error → HUMAN_CONFLICTED.

| case | Stage A | Stage B | state |
|---|---|---|---|
| pme_01 | CLEAR_CORRECT | TRUE_PRONUNCIATION_ERROR | **CONFLICTED** |
| pme_02 | PROBABLY_CORRECT | UNCERTAIN | UNCERTAIN |
| pme_03 | PROBABLY_INCORRECT | TRUE_PRONUNCIATION_ERROR | TRUE_ERROR |
| pme_04 | CLEAR_CORRECT | UNCERTAIN | UNCERTAIN |
| pme_05 | PROBABLY_INCORRECT | TRUE_PRONUNCIATION_ERROR | TRUE_ERROR |
| pme_06 | CLEAR_CORRECT | UNCERTAIN | UNCERTAIN |
| pme_07 | CLEAR_CORRECT | UNCERTAIN | UNCERTAIN |
| pme_08 | CLEAR_CORRECT | UNCERTAIN | UNCERTAIN |
| pme_09 | PROBABLY_INCORRECT | TRUE_PRONUNCIATION_ERROR | TRUE_ERROR |
| pme_10 | PROBABLY_INCORRECT | TRUE_PRONUNCIATION_ERROR | TRUE_ERROR |
| pme_11 | CLEAR_CORRECT | UNCERTAIN | UNCERTAIN |
| pme_12 | CLEAR_CORRECT | UNCERTAIN | UNCERTAIN |

- `single_reviewer = true` — one reviewer, not independent ground truth
- pme_01 conflict kept visible; not used as clean ground truth

Artifact: `Results/human_second_pass.csv`

---

## 20. Root-Cause Classification (repaired)

Measured mechanism rules (documented in `run_phase_1_9_10.py`). Human states are separate:

| group | measured mechanism | n | cases |
|---|---|---:|---|
| HUMAN_TRUE_ERROR (4) | — (human verdict) | 4 | pme_03, 05, 09, 10 |
| HUMAN_CONFLICTED (1) | CTC_ALIGNMENT (crop&gt;full) | 1 | pme_01 |
| HUMAN_UNCERTAIN (7) — diagnostic hypothesis only | BOUNDARY_ERROR | 3 | pme_02, 06, 08 |
| | CTC_ALIGNMENT (full-file) | 1 | pme_04 |
| | GENERALIZATION (attractor) | 1 | pme_07 |
| | REALIZATION variation | 2 | pme_11, 12 |

**The 7 uncertain mechanisms are NOT human-verified truth.** Among them, 4 are window-related
(boundary 3 + full-file alignment 1) — a hypothesis to be tested by the 1.9.11 window A/B.

---

## 21. What Is Proven

1. **5/12 were explicitly judged true pronunciation errors** (incl. pme_01 conflicted);
   **7/12 remained UNCERTAIN and cannot be treated as human ground truth**.
2. **Word-edge failure concentration:** 22/29 mismatches at initial/final positions.
3. **Window/boundary effects are measurable:** 4/12 boundary rescue; 2/12 crop-beats-full.
4. **Full-file alignment can be worse than the VAD crop** (raw 72.5 vs full 0.0 on pme_04).
5. **Phone-posterior attractor collapse** (`l`/`n` repeats) is a measurable mode (pme_05, 07).
6. **Documented child-realization substitutions** (θ→t, ɹ→w, f→v) occur (pme_11, 12).
7. **F0 does not discriminate** error vs OK groups.
8. Adult pipeline also fails on some words (blue 0.0) → not purely child-specific.
9. **Revised 1.9.9 metric:** 0.414 superseded; confirmed-acceptable low-score rate **0.0 (0/17)**
   with 8 unresolved cases excluded (explicit definition).

---

## 22. What Is NOT Proven

1. That the 7 UNCERTAIN cases are acceptable — they are unresolved.
2. That formant differences cause the failures (descriptive only).
3. That any specific child or word is systematically "bad".
4. That a scorer change would improve real scoring.
5. That these 12 generalize to all child speech or LWE vocabulary.
6. That ASR helps (it does not: 5/42 correct).
7. That the attractor `l`/`n` pattern is child-specific (adult controls also fail).

---

## 23. Does the Scorer Need Modification?

### **NO — not yet**

- The repaired human evidence does not confirm that the low scores were wrong:
  4 clean true errors, 7 uncertain, 1 conflicted, 0 explicitly acceptable.
- The window/boundary hypothesis is promising but must be tested by the 1.9.11 A/B before any
  policy change.
- No model-side change is supported by these 12 cases.

---

## 24. Proposed Future Changes, If Any (documented only, not implemented)

| proposal | why | evidence | expected effect | risk | validation required |
|---|---|---|---|---|---|
| Score the padded VAD window, not the raw full file | 2/12 crop&gt;full inversions; 4/12 boundary rescue | `boundary_analysis.csv` | fewer spurious low scores | may hide real context effects | 1.9.11 80-token A/B + human spot check |
| Multi-variant agreement | collapse cases fail all variants | this report §9–10 | more stable evidence | complexity | research-only first |
| Defer any phone-model change | attractor collapse unclear if child-specific | §18 adult control | — | — | larger child + adult matched-word set |

None of these are approved or implemented.

---

## 25. Limitations

- **Single reviewer** (`single_reviewer = true`) — not independent ground truth
- **7/12 UNCERTAIN + 1/12 CONFLICTED** — the majority of the set is not resolved truth
- pme_01 Stage A/B inconsistency recorded, not resolved
- n=12; one recording session per child; individual ages unknown
- Adult comparison uses different words (diagnostic only)
- Child-realization pair table is documented phonology, not validated per-speaker
- F0/F1/F2 single-pass parselmouth measurements; no normalization
- No statistical tests claimed anywhere

---

## 26. Decision

### **B. ROOT_CAUSE_PARTIALLY_IDENTIFIED**

Repaired after methodology fix: the dominant mechanisms are measurable for the uncertain cases
(diagnostic hypothesis), but human truth covers only 4 clean true errors + 1 conflict; the
remainder is unresolved.

```
production_vad = false
router_locked = false
unity_integrated = false
scorer_modified = false
```

---

## 27. Exact Next Step

1. Phase 1.9.11: forensic the **6 SCORER_MISS** cases (reverse failure).
2. Phase 1.9.11: 80-token window A/B (FULL/RAW/PAD×5 + multi-variant policies) to test the
   window hypothesis, with human spot check.
3. No scorer change until the window effect is quantified and human-checked.

---

## Artifacts

`Results/` (18 files incl. `phase_1_9_10_master.json`, `decision.json`,
`human_second_pass.csv`), `HumanReview/` (blind/reveal HTML, metadata, 24 clips),
`Scripts/run_phase_1_9_10.py`, `Scripts/compare_acoustics.py`, `Scripts/merge_second_pass.py`.
Raw child audio remains outside Git.
