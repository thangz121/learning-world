# PHASE 1.9.9 REPORT — Child Pronunciation Phone-Level Audit

**Base:** Phase 1.9.8 `3ca915b`  
**Date:** 2026-10-03  
**production_vad / router_locked / unity:** **false**  
**asr_is_not_pronunciation_judge:** true  

**Decision this phase:** **B. SCORER_NEEDS_CHILD_SPECIFIC_RESEARCH**  
Reason: Stage A human blind labels complete (n=42).  
**human_correct_low_score_rate = 0.414** (12/29 human-OK tokens scored &lt;50).  
Also **human_incorrect_high_score_rate = 0.545** (6/11 human-bad scored ≥50).

---

## 1. Executive Summary

| Item | Value |
|---|---|
| Baseline 80-token reproduction | **YES** — n=80, mean=63.9038, frac&lt;50=0.2750 |
| Balanced review sample | **42** (8 VERY_LOW, 10 LOW, 12 MID, 12 HIGH + boundary extras) |
| Clips exported | FULL / RAW / PAD250 / HYB / LISTEN per item |
| Blind UI | `HumanReview/review_blind.html` (scores hidden) |
| Reveal UI | `HumanReview/review_reveal.html` |
| Human labels filled | **42/42** |
| human_correct_low_score_rate | **0.414** (12/29) |
| human_incorrect_high_score_rate | **0.545** (6/11) |
| LWE vocab in Zenodo | **LIMITED** (numbers + sentence tokens; not full LWE list) |

VAD architecture **unchanged**: Silero default; hybrid research rescue only.  
Scorer: **needs child-specific research** (not production change yet).

---

## 2. Frozen Baseline Reproduction

| Metric | Phase 1.9.8 | This phase |
|---|---:|---:|
| n | 80 | 80 |
| mean soft_full | 63.90375 | 63.9038 |
| frac &lt; 50 | 0.275 | 0.2750 |

Source CSV: `Phase1_9_8/Results/pronunciation_results.csv`  
`baseline_commit=3ca915b` · subset=studio numbers cap80  
Artifact: `Results/baseline_reproduction.csv`

---

## 3. Research Question

When a ~4yo child utterance scores low under frozen soft-v2+CMUdict, is that:
true phoneme error, child variation, phone-model weakness, alignment/boundary, audio quality, or ambiguous?

**Score is a hypothesis to test, not the judge.**

---

## 4. Dataset

Zenodo 200495 under `Research/Speech/ExternalData/zenodo_200495/` (gitignored raw).  
Same 80 studio number tokens as 1.9.8.  
Age: dataset mean M=4.9y; individual UNKNOWN.

---

## 5. Low-Score Token Inventory

All 80 ranked in `Results/low_score_inventory.csv` with bands:

| Band | Definition | n (of 80) |
|---|---|---:|
| HIGH | ≥80 | (from inventory) |
| MID | 50–80 | |
| LOW | 20–50 | |
| VERY_LOW | &lt;20 | |

Counts: see `phase_1_9_9_master.json` → `inventory_counts`.

---

## 6. Human Blind Review

Pack: **42** items. Stage A completed by human (file `Human_Review_StageA_Filled.csv` → Results/).

| Human label | n |
|---|---:|
| CLEAR_CORRECT + PROBABLY_CORRECT | **29** |
| CLEAR_INCORRECT + PROBABLY_INCORRECT | **11** |
| AMBIGUOUS | **2** |

Most boundary labels: BOUNDARY_OK (few BOTH_CUT / END_CUT / UNCLEAR).

---

## 7. Human vs Score

| Metric | Value |
|---|---:|
| human_correct_low_score_rate | **0.414** (12/29) |
| human_incorrect_high_score_rate | **0.545** (6/11) |
| human_ambiguous_rate | **0.048** (2/42) |

| Human \ Score | Low (&lt;50) | High (≥50) |
|---|---:|---:|
| CLEAR/PROBABLY_CORRECT | **12** | 17 |
| CLEAR/PROBABLY_INCORRECT | 5 | **6** |
| AMBIGUOUS | 1 | 1 |

**12 human-OK but score&lt;50** (examples): eight/two/one/three/seven/six/five with soft 0–39 and phone n_miss≥1 → classified **PHONE_MODEL_ERROR** (heuristic; human authority on correctness).

**6 human-BAD but score≥50** (SCORER_MISS): e.g. four@100, six@50, two@50, eight@50.

---

## 8. Phone-Level Diagnostics

For each sample token: canonical ARPABET, soft hits (expected/best_obs/match_type/sim/posterior/spans), n_miss, conf_reasons.

Artifact: `phone_diagnostics.csv` (updated with human + diagnostic).

| diagnostic | n |
|---|---:|
| AGREEMENT_OK | 17 |
| PHONE_MODEL_ERROR | **12** |
| SCORER_MISS | **6** |
| TRUE_PRONUNCIATION_ERROR | 5 |
| HUMAN_UNCERTAIN | 2 |

Human-OK + low score + phone misses → primary research signal: **phone model / child acoustic mismatch**, not “child always wrong”.

---

## 9. ASR vs Phone Evidence

Moonshine (tiny) run on the 42 reviewed LISTEN/FULL clips (supporting evidence only).

| ASR status | n |
|---|---:|
| ASR_EMPTY | **24** |
| ASR_WRONG | **13** |
| ASR_CORRECT | **5** |

| Conflict vs human | n |
|---|---:|
| ASR_EMPTY_HUMAN_RIGHT | **18** |
| ASR_WRONG_HUMAN_RIGHT | **7** |
| ASR_EMPTY_HUMAN_WRONG | 5 |
| NO_CONFLICT | 12 |
| ASR_RIGHT_HUMAN_WRONG | 0 |

**Key finding:** ASR is unreliable on isolated child number words (only 5/42 correct);  
**ASR_EMPTY ≠ NON_SPEECH / incorrect** — 18 cases ASR-empty while human heard acceptable speech.  
Policy unchanged: `asr_is_not_pronunciation_judge = true`.  
Artifacts: `Results/asr_phone_conflicts.csv`, `Results/asr_conflict_summary.json`.

---

## 10–11. Boundary / Padding Analysis

Pad sweep 0/100/200/250/300/500 ms on Silero crop: `padding_sweep.csv`, `boundary_diagnostics.csv`.

Patterns flagged when applicable:
- `FULL_HIGH_RAW_LOW_PAD_HIGH` → boundary sensitivity candidate  
- `ALL_LOW` → deeper issue candidate  
- `ALL_HIGHISH` → agreement candidate  

**Rule:** pad raising score ≠ child “pronounced better”; it may restore context.

---

## 12. Word-Level Analysis

`word_level_results.csv`: n, mean, median, std, min, max, frac&lt;50, frac&lt;20, mean conf, mean |Δ| boundary.  
Human rates blank until review.

Words of interest from 1.9.8 (two, eight, three, …) are included in the sample extremes.

---

## 13. Child-Level Analysis

`child_level_results.csv` per anonymous `child_XX`.  
**No inference** of ability/age/identity. Model-behavior only.

---

## 14–16. Acoustic / Pitch / Formant

`acoustic_analysis.csv`: duration, rms, centroid, flatness, F0 proxy (autocorr).  
**PITCH_EFFECT_NOT_IDENTIFIED** as causal claim (proxy + small paired bands only).  
**FORMANT_EFFECT_NOT_IDENTIFIED** (no reliable F1/F2 tracker this phase).

---

## 17. LWE Vocabulary Coverage

From repo `Phase1_1/audio/sapi_*.wav` vs Zenodo number/sentence tokens:

| | |
|---|---|
| ZENODO_TARGET_COVERAGE | **LIMITED** |
| REAL_CHILD_LWE_VOCABULARY_DATA | **PARTIAL_NUMBERS_ONLY** |
| Overlap | see `lwe_child_vocab_coverage.csv` |

Do **not** map unrelated child words to LWE targets.

---

## 18. Failure Mode Classification

See §7–8. Dominant research-relevant modes on this sample:
- **PHONE_MODEL_ERROR** (12): human acceptably correct, score low, phone misses  
- **SCORER_MISS** (6): human incorrect, score still mid/high  
- **TRUE_PRONUNCIATION_ERROR** (5): human incorrect + score low (useful detection)  
- Boundary-cut rarely primary (most BOUNDARY_OK)

---

## 19. What Is Proven

1. Baseline 80-token scores reproducible.  
2. On 42 blind-reviewed tokens, **~41% of human-correct speech scores &lt;50**.  
3. **~55% of human-incorrect speech still scores ≥50**.  
4. Low scores are **not** equivalent to “child mispronounced”.  
5. Phone-model mismatch is a leading candidate mechanism (not proven causal F0).  
6. LWE full vocab still not covered by Zenodo numbers.

---

## 20. What Is NOT Proven

1. Exact fix (calibration vs phone model vs child norm).  
2. Pitch/formant causality.  
3. Production-ready child scorer.  
4. Generalization beyond numbers / this 42-sample.  
5. ASR as a useful correct/incorrect signal (it is not — 5/42 correct).

---

## 21. Limitations

- Single reviewer Stage A  
- n=42 balanced sample of 80  
- Numbers only; not full LWE vocab  
- Diagnostic labels are heuristic on top of human pronunciation authority  
- ASR (Moonshine tiny) weak on isolated child words; supporting evidence only  

---

## 22. Decision

### **B. SCORER_NEEDS_CHILD_SPECIFIC_RESEARCH**

```
production_vad = false
router_locked = false
unity_integrated = false
```

Do **not** integrate scorer changes into Unity yet.  
Do **not** blindly calibrate scores before isolating phone-model vs true error.

---

## 23. Exact Next Step

**Phase 1.9.10 (suggested):** isolated research variants on the 12 PHONE_MODEL_ERROR + 6 SCORER_MISS cases only:
1. phone-posterior / soft-match diagnostics deep dive  
2. optional child-aware research head **without** overwriting frozen soft-v2  
3. expand human review to more LWE vocab if real child audio becomes available  
Keep VAD architecture frozen (Silero default).

---

## Artifacts

Evidence clips under `HumanReview/clips/` (tracked for remote review).  
Raw Zenodo audio remains outside Git.
