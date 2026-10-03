# PHASE 1.9.10 REPORT — Deep-Dive 12 Phone-Model-Error Cases

**Base:** Phase 1.9.9 `e2449c5`  
**Date:** 2026-10-03  
**production_vad:** false · **router_locked:** false · **unity_integrated:** false · **scorer_modified:** false  
**asr_is_not_pronunciation_judge:** true

---

## 1. Executive Summary

The 12 PHONE_MODEL_ERROR cases from Phase 1.9.9 were re-run through the frozen pipeline with
full/raw/padded variants (0–500 ms), CTC span inspection, parselmouth acoustics (F0/F1/F2),
phone confusion analysis, and an adult diagnostic control.

| Finding | Value |
|---|---|
| Cases | **12/12 human-CORRECT** (by construction of the PME set) |
| Boundary rescue (raw&lt;50 → pad≥50, or +≥20) | **4/12 (33%)** |
| Rescue to usable score (≥50) | **2/12 (17%)** |
| Crop-beats-full inversion (raw≥50 & full&lt;50) | **2/12** |
| Mismatches at word-initial position | **12/12 initial phones (100%)** |
| Mismatches at word-final position | **10/12 final phones (83%)** |
| Mismatched CTC spans &lt;50 ms | **15/29 (52%)**, median 21 ms |
| Adult control exact-match rate (6 words) | **0.56** (blue/dog/big also fail for adult) |
| F0 median PME vs OK group | **281.5 vs 280.7 Hz — no separation** |
| Formant F1/F2 PME vs OK | 948/2452 vs 901/2298 Hz (+5–7%, descriptive only) |
| Decision | **B. ROOT_CAUSE_PARTIALLY_IDENTIFIED** |

**Headline:** low scores on these 12 human-correct child tokens are **not** explained by pitch.
They split into measurable mechanisms: boundary/crop loss, full-file CTC alignment failure,
attractor collapse of the phone posterior (repeated `l`/`n`), and documented child-realization
substitutions (θ→t, ɹ→w, f→v) that the canonical target treats as errors.

**No scorer change is justified yet.**

---

## 2. Frozen Baseline

- CMUdict canonical target, Silero 0.5, soft-v2, CTC alignment — unchanged
- Phase 1.9.9 artifacts used as authoritative case source (`human_review_results.csv`)
- No model replacement, no retraining, no threshold change, no calibration
- Adult control: `Research/Speech/Phase1_1/audio/sapi_*.wav` (existing adult pipeline data)

---

## 3. Phase 1.9.9 Findings Being Investigated

- `human_correct_low_score_rate = 0.414` (12/29 human-OK tokens &lt;50)
- 12 cases classified PHONE_MODEL_ERROR (human correct + low score + phone misses)
- ASR weak (5/42 correct); `ASR_EMPTY != pronunciation_error`

This phase asks: **why did the phone evidence fail on these 12?**

---

## 4. The 12 PHONE_MODEL_ERROR Cases

| case | speaker | target | human | full | raw | best pad | primary cause | conf |
|---|---|---|---|---|---:|---:|---|---|---|
| pme_01 | child_07 | eight | PROBABLY_CORRECT | 0.0 | 50.0 | 0.0 | CTC_ALIGNMENT_ERROR | MED |
| pme_02 | child_06 | two | CLEAR_CORRECT | 0.0 | 0.0 | 50.0 | BOUNDARY_ERROR | HIGH |
| pme_03 | child_07 | eight | PROBABLY_CORRECT | 0.0 | 0.1 | 0.0 | CTC_ALIGNMENT_ERROR | LOW |
| pme_04 | child_07 | one | CLEAR_CORRECT | 0.0 | 72.5 | 72.5 | CTC_ALIGNMENT_ERROR | MED |
| pme_05 | child_08 | eight | PROBABLY_CORRECT | 0.0 | 0.0 | 0.0 | PHONE_MODEL_GENERALIZATION_FAILURE | MED |
| pme_06 | child_04 | three | CLEAR_CORRECT | 11.7 | 6.7 | 39.2 | BOUNDARY_ERROR | MED |
| pme_07 | child_01 | seven | CLEAR_CORRECT | 20.0 | 40.1 | 44.1 | PHONE_MODEL_GENERALIZATION_FAILURE | MED |
| pme_08 | child_06 | six | CLEAR_CORRECT | 25.0 | 0.0 | 25.1 | BOUNDARY_ERROR | MED |
| pme_09 | child_09 | five | CLEAR_CORRECT | 33.4 | 33.4 | 33.4 | PHONE_MODEL_GENERALIZATION_FAILURE | LOW |
| pme_10 | child_01 | five | PROBABLY_CORRECT | 33.5 | 39.2 | 66.7 | BOUNDARY_ERROR | HIGH |
| pme_11 | child_03 | three | CLEAR_CORRECT | 39.2 | 39.2 | 39.2 | PHONEME_REALIZATION_VARIATION | LOW |
| pme_12 | child_08 | five | CLEAR_CORRECT | 39.2 | 39.2 | 39.2 | PHONEME_REALIZATION_VARIATION | LOW |

Artifacts: `phone_model_error_cases.csv`, `phone_model_error_root_causes.csv`

---

## 5. Case-by-Case Forensics (condensed)

**pme_01 eight (child_07)** — canonical `EY1 T`, observed full `d æ`; raw crop 50 &gt; full 0.
The VAD window aligns better than the full file → full-file alignment problem.

**pme_02 two (child_06)** — canonical `T UW1`; observed `ts.` + `ŋ`; raw 0 → pad_100 = 50.
Boundary rescue: crop loses onset/offset that padding restores.

**pme_03 eight (child_07)** — observed `l p`; one mismatched span &lt;50 ms; no variant recovers.
Weak evidence → LOW confidence.

**pme_04 one (child_07)** — canonical `W AH1 N`, observed `k k l`; raw window **72.5** while full
file **0.0**. Strongest crop-beats-full inversion; full-file context derails alignment.

**pme_05 eight (child_08)** — observed `n n` (attractor collapse); no variant recovers.

**pme_06 three (child_04)** — observed `t w l`; θ→t and ɹ→w are documented developmental
substitutions (secondary PHONEME_REALIZATION_VARIATION); padding lifts 6.7→39.2.

**pme_07 seven (child_01)** — canonical `S EH1 V AH0 N`, observed `l l l j n`; `l` repeated 3×
(attractor collapse), no variant recovers.

**pme_08 six (child_06)** — observed `l l ɹ`; padding lifts 0→25.1 (boundary component).

**pme_09 five (child_09)** — observed `l aɪ n`; no boundary rescue; unrelated substitutions.

**pme_10 five (child_01)** — observed `p aɪ ɑ`; raw 39.2 → pad_100 **66.7**; clearest usable rescue.

**pme_11 three (child_03)** — observed `t ɹ t`; θ→t documented substitution; iː→t unexplained.

**pme_12 five (child_08)** — observed `v aɪ t`; f→v documented voicing substitution.

---

## 6. Canonical vs Observed Phones

Per-case canonical/observed strings and mismatch lists are in
`phone_model_error_root_causes.csv` (`mismatch_type` column, e.g. `θ->t@initial`).
Aggregated in §7.

---

## 7. Phone Confusion Analysis

Top observed substitutions (`phone_confusion_matrix.csv`):

| expected | observed | count | note |
|---|---|---|---|
| θ | t | 2 | th-stopping (three) — documented child realization |
| s | l | 2 | attractor |
| eɪ | l | 2 | attractor |
| ɹ | w | 1 | gliding (three) — documented |
| f | v | 1 | voicing (five) — documented |
| f | p | 1 | stopping |
| t | n | 1 | attractor |
| v | n | 1 | attractor |
| ɛ/ɪ/iː/v/f | l | 1 each | `l` attractor total ≈ 8 occurrences |

**Pattern:** the phone posterior landscape on these short child tokens collapses toward
`l` (and to a lesser degree `n`) when the acoustics do not match the model's expectation.
Frequency alone is not proof of causality — it is a pattern for targeted investigation.

---

## 8. Word Position Analysis

| Position | Total phones | Mismatched | Rate |
|---|---:|---:|---:|
| word-initial | 12 | 12 | **100%** |
| word-medial | 11 | 7 | 64% |
| word-final | 12 | 10 | **83%** |

**22/29 mismatches sit at word edges.** Isolated child words have unusual onset/offset
structure; edge consonants are the most failure-prone (consistent with §9–10).

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
- Some mismatches absorb very long spans (max 2.23 s) → degenerate alignment (one phone
  stretched over the utterance) in collapse cases
- Combined with §8: model squeezes edge consonants into near-zero spans, or collapses
  the whole token to a single attractor phone
- Artifact: `ctc_span_analysis.csv`

---

## 11. Duration Analysis

- PME utterance duration mean **0.92 s**; OK group **1.06 s** (descriptive)
- Speaking rate: mean **3.59 phones/s** (range 1.32–4.87) — inside normal child range;
  no rate regime clearly isolated with n=12
- Phone span medians (§10) show the failure is span allocation, not overall tempo
- Artifact: `duration_analysis.csv`

---

## 12. Child Acoustic Analysis

Same parselmouth method for both groups (`acoustic_group_comparison.csv`):

| feature | PME (n=12) | OK (n=17) |
|---|---:|---:|
| F0 median (Hz) | 281.5 | 280.7 |
| F0 range (Hz) | 279.7 | 281.3 |
| F1 median (Hz) | 947.9 | 900.5 |
| F2 median (Hz) | 2452.1 | 2298.1 |
| duration (s) | 0.92 | 1.06 |

---

## 13. F0 Investigation

**F0 does not separate the error group from the agreement group** (281.5 vs 280.7 Hz).
Pitch is *not* a discriminating variable in this sample.

**PITCH_CAUSALITY_NOT_ESTABLISHED** (descriptive evidence against pitch as the driver).

---

## 14. F1/F2 Investigation

F1 and F2 are 5–7% higher in the PME group, but the distributions overlap and n=12/17
is too small for a statistical claim. No normalization applied, none justified yet.

**FORMANT_CAUSALITY_NOT_ESTABLISHED.**

---

## 15. Speaking Rate Investigation

No clustering of failures in a specific rate regime at n=12. The failure signature is
**span allocation at word edges** (§8–10), not speech tempo.

---

## 16. Word-Specific Analysis

| word | PME n | speakers | mean full |
|---|---:|---|---:|
| eight | 3 | child_03, 07, 08 | 0.0 |
| five | 3 | child_01, 08, 09 | 35.4 |
| three | 2 | child_03, 04 | 25.5 |
| one/two/six/seven | 1 each | — | 0–25 |

`eight` fails for 3 different children with 0.0 scores and collapse patterns (`l p`, `n n`,
`d æ`) — worth targeted follow-up. Sample too small to call any word "bad".

---

## 17. Speaker-Specific Analysis

| speaker | PME n | rescue | causes |
|---|---:|---:|---|
| child_06 | 2 | 2 | both BOUNDARY_ERROR |
| child_01 | 2 | 1 | boundary + generalization |
| child_03 | 2 | 0 | CTC + realization |
| child_07 | 2 | 0 | both CTC (crop&gt;full) |
| child_08 | 2 | 0 | realization + generalization |
| child_04 / child_09 | 1 each | 1 / 0 | boundary / generalization |

No child dominates; 7 children contribute 1–2 cases each. No developmental or medical
interpretation is made or implied.

---

## 18. Adult vs Child Diagnostic Comparison

Frozen pipeline on 6 adult `sapi_*` words (`adult_vs_child_phone_model.csv`):

| word | score | exact rate |
|---|---:|---:|
| red | 100 | 1.00 |
| cat | 100 | 1.00 |
| blue | 5.8 | 0.00 |
| big | 33.3 | 0.33 |
| book | 66.7 | 0.67 |
| dog | 33.3 | 0.33 |

- Adult mean exact rate **0.56** vs child PME mean **0.15**
- **blue/dog/big also fail for adult speech** → the phone/alignment pipeline is fragile
  on some words regardless of speaker age
- Caveat: different words, diagnostic only. **CHILD_SPECIFIC_PHONE_FAILURE = PARTIAL**
  (child tokens fail more, but the mechanism is not exclusively child-triggered)

---

## 19. Human Second-Pass Review

Pack created for the 12 cases (mobile-friendly):

- `HumanReview/review_blind.html` — Stage A: target + audio only, pronunciation judgment
- `HumanReview/review_reveal.html` — Stage B: score/phones/alignment/acoustics/ASR + "most responsible factor"
- 24 clips (`clips/pme_XX_LISTEN.wav`, `_FULL.wav`)
- Template: `Results/human_second_pass.csv` (labels empty)

**Status: not yet filled.** `single_reviewer = true` for any future fill; one reviewer is a
limitation, not independent ground truth.

---

## 20. Root-Cause Classification

Priority rules (documented in `run_phase_1_9_10.py`): measured boundary rescue → crop-beats-full
inversion → documented realization pairs → attractor collapse → short-span → unresolved.

| primary cause | n | cases |
|---|---:|---|
| BOUNDARY_ERROR | **4** | pme_02, 06, 08, 10 |
| CTC_ALIGNMENT_ERROR | **3** | pme_01, 03, 04 |
| PHONE_MODEL_GENERALIZATION_FAILURE | **3** | pme_05, 07, 09 |
| PHONEME_REALIZATION_VARIATION | **2** | pme_11, 12 |
| secondary causes | 4 | CTC×2 (05,07), realization×2 (06,10) |

Counting method: each case contributes exactly 1 primary cause; secondaries are listed
separately and never double-counted in the distribution.

---

## 21. What Is Proven

1. **12/12 PME cases are human-correct** — low score ≠ mispronunciation here.
2. **Word-edge failure concentration:** 22/29 mismatches at initial/final positions.
3. **Boundary/crop path explains 4/12** (rescue ≥20 pts; 2 reach ≥50).
4. **Full-file alignment can be worse than the VAD crop** (2/12, e.g. raw 72.5 vs full 0.0).
5. **Phone-posterior attractor collapse** (`l`/`n` repeats) is a real measurable mode (2–3/12).
6. **Documented child-realization substitutions** (θ→t, ɹ→w, f→v) occur in human-accepted speech.
7. **F0 does not discriminate** PME vs OK groups.
8. Adult pipeline also fails on some words (blue 0.0) → fragility is not purely child-specific.

---

## 22. What Is NOT Proven

1. That formant differences cause the failures (descriptive only, n small).
2. That any specific child or word is systematically "bad".
3. That a scorer change would improve real scoring.
4. That these 12 generalize to all child speech or to LWE vocabulary.
5. That ASR helps (it does not: 5/42 correct).
6. That the attractor `l`/`n` pattern is child-specific (adult controls also show failures).

---

## 23. Does the Scorer Need Modification?

### **PARTIALLY**

Mechanisms are now measurable but not yet sufficient to design a safe fix:

- boundary loss could justify **crop/pad policy** research (not score changes)
- full-file alignment inversion could justify **scoring the VAD window instead of the file**
  (a pipeline policy question, not a model change)
- attractor collapse and realization substitutions need the **human second pass** and a larger
  sample before any model-side conclusion

**Do not modify the scorer in this phase.**

---

## 24. Proposed Future Changes, If Any (documented only, not implemented)

| proposal | why | evidence | expected effect | risk | validation required |
|---|---|---|---|---|---|
| Score the padded VAD window, not the raw full file | 2/12 crop&gt;full inversions; 4/12 boundary rescue | `boundary_analysis.csv` | fewer spurious low scores | may hide real context effects | A/B on 80-token set + human check |
| Use multi-variant evidence (full+pad) with agreement rule | collapse cases fail all variants | this report §9–10 | more stable evidence | complexity | research-only first |
| Defer any phone-model change | attractor collapse unclear if child-specific | §18 adult control | — | — | larger child + adult matched-word set |

None of these are approved or implemented.

---

## 25. Limitations

- Human second-pass review **not yet filled** (Stage A/B pack ready)
- n=12; one recording session per child; individual ages unknown
- Adult comparison uses different words (diagnostic only)
- Child-realization pair table is documented phonology, not validated per-speaker
- F0/F1/F2 single-pass parselmouth measurements; no normalization
- `pme_03` classified with LOW confidence (weak short-span evidence)
- No statistical tests claimed anywhere

---

## 26. Decision

### **B. ROOT_CAUSE_PARTIALLY_IDENTIFIED**

```
production_vad = false
router_locked = false
unity_integrated = false
scorer_modified = false
```

---

## 27. Exact Next Step

1. Human fills `HumanReview/review_blind.html` → Stage A, then `review_reveal.html` → Stage B
   for the 12 cases (single reviewer; documented limitation).
2. Run the same forensic pack on the **6 SCORER_MISS** cases (human-incorrect, score≥50)
   to test whether the same mechanisms act in reverse.
3. Only after 1–2: design a research-only A/B on **scoring window policy** (full vs padded VAD
   window) against the frozen Phase 1.9.8 baseline — no model changes.

---

## Artifacts

`Results/` (18 files incl. `phase_1_9_10_master.json`), `HumanReview/` (blind/reveal HTML,
metadata, 24 clips), `Scripts/run_phase_1_9_10.py`, `Scripts/compare_acoustics.py`.
Raw child audio remains outside Git.
