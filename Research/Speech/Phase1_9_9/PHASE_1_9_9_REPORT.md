# PHASE 1.9.9 REPORT — Child Pronunciation Phone-Level Audit

**Base:** Phase 1.9.8 `3ca915b`  
**Date:** 2026-10-03  
**production_vad / router_locked / unity:** **false**  
**asr_is_not_pronunciation_judge:** true  

**Decision this phase:** **C. EVIDENCE_INSUFFICIENT**  
Reason: automated forensics + blind-review evidence pack are complete; **human Stage A labels not yet filled**. Scorer A/B cannot be chosen without the human ear as authority.

---

## 1. Executive Summary

| Item | Value |
|---|---|
| Baseline 80-token reproduction | **YES** — n=80, mean=63.9038, frac&lt;50=0.2750 |
| Balanced review sample | **42** (8 VERY_LOW, 10 LOW, 12 MID, 12 HIGH + boundary extras) |
| Clips exported | FULL / RAW / PAD250 / HYB / LISTEN per item |
| Blind UI | `HumanReview/review_blind.html` (scores hidden) |
| Reveal UI | `HumanReview/review_reveal.html` |
| Human labels filled | **0** |
| LWE vocab in Zenodo | **LIMITED** (numbers + sentence tokens; not full LWE list) |

VAD architecture **unchanged**: Silero default; hybrid research rescue only.

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

Pack: **42** items, multi-child multi-word, not worst-only.

Stage A (`review_blind.html`):
- Shows target + audio (LISTEN/FULL/RAW/PAD250/HYB)
- Hides score, confidence, ASR, phone evidence
- Labels: CLEAR_CORRECT … CLEAR_INCORRECT, boundary, quality, confidence

Stage B (`review_reveal.html`): reveals model evidence after Stage A.

**Status:** labels empty — awaiting human on maynode/local.

Export path: `Human_Review_StageA_Filled.csv` → merge into `Results/human_review_results.csv`.

---

## 7. Human vs Score

| Metric | Value |
|---|---|
| human_correct_low_score_rate | **PENDING** |
| human_incorrect_high_score_rate | **PENDING** |
| human_ambiguous_rate | **PENDING** |

Matrix placeholder: `human_vs_score_matrix.csv`

---

## 8. Phone-Level Diagnostics

For each sample token: canonical ARPABET, soft hits (expected/best_obs/match_type/sim/posterior/spans), n_miss, conf_reasons.

Artifact: `phone_diagnostics.csv`  
`diagnostic=UNRESOLVED_PENDING_HUMAN` until Stage A.

**Example pattern (automated):** several VERY_LOW “two”/“eight” show high n_miss and weak posteriors — consistent with *either* true error *or* phone-model/child mismatch; **not classified without human**.

---

## 9. ASR vs Phone Evidence

Moonshine ASR **unavailable** in env this run (`ASR_UNAVAILABLE`).  
Column reserved in `asr_phone_conflicts.csv`.  
Policy unchanged: ASR is not the pronunciation judge.

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

Taxonomy ready; all sample diagnostics **UNRESOLVED_PENDING_HUMAN** until Stage A:
TRUE_PRONUNCIATION_ERROR / PHONE_MODEL_ERROR / BOUNDARY_ERROR / … / UNRESOLVED

---

## 19. What Is Proven

1. 1.9.8 baseline scores are stable and reproducible from stored results.  
2. A balanced 42-item blind-review pack with multi-crop audio can be served.  
3. Phone-hit forensics and pad sweeps are extractable without changing the scorer.  
4. LWE full vocabulary is **not** covered by Zenodo numbers alone.

---

## 20. What Is NOT Proven

1. Fraction of low scores that are human-acceptable child speech.  
2. Systematic child-acoustic bias of the scorer.  
3. Need for pitch normalization.  
4. Production scorer changes.  
5. ASR conflict rates (ASR unavailable).

---

## 21. Limitations

- No human labels yet → Decision C  
- ASR missing in environment  
- F0 is proxy only  
- Numbers ≠ LWE game vocab  
- Individual child ages unknown  

---

## 22. Decision

### **C. EVIDENCE_INSUFFICIENT**

```
production_vad = false
router_locked = false
unity_integrated = false
```

Do not choose A (scorer generalizes) or B (needs child-specific fix) without human Stage A rates.

---

## 23. Exact Next Step

1. Open `HumanReview/review_blind.html` (maynode or local).  
2. Complete Stage A for all 42 items → save `Human_Review_StageA_Filled.csv`.  
3. Run merge script / update `human_review_results.csv`.  
4. Recompute `human_correct_low_score_rate` and failure taxonomy → flip decision to **A or B**.  
5. Only then consider isolated research scorer variants — **do not calibrate first**.

---

## Artifacts

Evidence clips under `HumanReview/clips/` (tracked for remote review).  
Raw Zenodo audio remains outside Git.
