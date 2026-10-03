# PHASE 1.9.12 REPORT — Final-Consonant Acoustic Support (P2) + Expanded Window Validation

**Base:** Phase 1.9.11 `a80dc43`  
**Date:** 2026-10-03  
**production_vad:** false · **router_locked:** false · **unity_integrated:** false  
**scorer_modified:** false · **production_window_locked:** false  
**asr_is_not_pronunciation_judge:** true

---

## 1. Executive Summary

| Objective | Result |
|---|---|
| A — Final-consonant acoustic support | 65 child tokens; **28 human-reviewed (blind)**; 12/28 absent; CTC-anchored acoustic layer is **redundant/contaminated** (phone right 22/28, acoustic right 17/28) → decision **B** |
| B — Expanded window human validation | 20 stratified tokens × 5 anonymous windows = 100 clips; **35 directly rated + 65 imputed by reviewer rule** (same-token clips perceptually identical); no window shows a human-perceptible advantage → decision **C** |

Key measured facts:

- Child final-consonant classes available: NASAL 31 (/n/), FRICATIVE 16 (/s/ 7, /v/ 9),
  STOP 9 (/t/), LIQUID 9 (/r/). Other classes absent in child data.
- **Human final-consonant review (28):** present 16, absent 12 — deletion is broader than the
  "four" class: STOP 3/8 absent (/t/ in eight), LIQUID 5/6 absent (/r/ in four),
  FRICATIVE 3/5 absent (/s/, /v/), NASAL 1/9 absent.
- **Methodology trap confirmed:** features anchored on CTC spans inherit forced-alignment
  errors — span voicing 0.95–1.0 ("PRESENT") on human-confirmed ABSENT /r/ cases.
- Real-label phone-vs-acoustic: BOTH_RIGHT 16, PHONE_RIGHT_ACOUSTIC_WRONG 6, BOTH_WRONG 5,
  PHONE_WRONG_ACOUSTIC_RIGHT 1.
- 14/65 final-consonant tokens had raw<50 → pad≥50 ("restored by padding"), including 5 /r/.
- Window final (35 direct + 65 imputed): good-clarity **FULL 0.65, PAD100/250/500 0.60,
  RAW 0.55** — no human-perceptible advantage for any window; the earlier PAD250 trend
  disappeared once same-token clips were treated as identical (reviewer rule).
- **tok_04 nine rated CLEAR at RAW despite score 33.8 vs 100** — numeric window drops did not
  match perception.

**No score is created; no policy is locked.**

---

## 2. Frozen Baseline

CMUdict target, Silero 0.5, soft-v2, CTC alignment, SCORE≠CONFIDENCE, ASR supporting-only —
all unchanged. Phase 1.9.11 window A/B results reused for sampling and interaction analysis.

---

## 3. Phase 1.9.11 Context

- Human-confirmed ending-sound class: 3 "four" tokens with deleted final /r/ scored 100/100/66.8
  at FULL (TRUE_SCORER_MISS).
- Human window spot check (13): FULL catches 0/3 errors, rejects 3/9 correct; RAW catches 2/3,
  rejects 5/9. No safe single window.
- Next: P2 acoustic support + expanded human window review.

---

## 4. Objective A: Final-Consonant Acoustic Support

Goal: determine whether raw-audio features focused on word-final consonants add **independent**
evidence when phone evidence is weak/ambiguous. Evidence-only; no score.

---

## 5. Final-Consonant Dataset

`Results/final_consonant_inventory.csv` — child words with final consonants:

| word | canonical | final | class | n tokens | n speakers |
|---|---|---|---:|---:|---:|
| one | W AH1 N | N | NASAL | 5 | 5 |
| four | F AO1 R | R | LIQUID | 9 | 9 |
| five | F AY1 V | V | FRICATIVE | 9 | 9 |
| six | S IH1 K S | S | FRICATIVE | 7 | 7 |
| seven | S EH1 V AH0 N | N | NASAL | 9 | 9 |
| eight | EY1 T | T | STOP | 9 | 9 |
| nine | N AY1 N | N | NASAL | 8 | 8 |
| ten | T EH1 N | N | NASAL | 9 | 9 |

Excluded (vowel-final): two (T UW1), three (TH R IY1).  
Total final-consonant tokens: **65**. Classes: NASAL 31, FRICATIVE 16, STOP 9, LIQUID 9.

LWE vocabulary (12 sapi items) all have **0 child examples** — ZENODO_TARGET_COVERAGE remains
LIMITED; LWE final classes (/g/, /k/, /d/, /z/, /l/, /t/, /n/) are `not_present` in child audio.

Availability by phoneme: /n/ /s/ /v/ /t/ /r/ = **available**; /p/ /b/ /d/ /k/ /g/ /f/ /z/ /sh/
/ch/ /m/ /ng/ /l/ = **not_present** in this child dataset.

---

## 6. Acoustic Feature Extraction

`Results/final_consonant_features.csv` — 65 tokens × 5 regions = **325 rows**:
FINAL_RAW (CTC span), FINAL_PAD_50/100/150/200 (span ± pad).

Per region: duration, RMS, RMS before/after, relative energy drop, ZCR, voicing ratio
(autocorrelation), centroid, flatness, high-frequency ratio (>2 kHz), voiced-class flag.

**Critical caveat (measured):** regions are anchored on the phone model's CTC span, so features
are **not independent** of phone evidence. Example: all 5 human-ABSENT "four" tokens show
voicing 0.95–1.0 (the span absorbs voiced vowel tail / trailing audio).

---

## 7. Human Final-Consonant Review (completed)

Blind pack submitted (28 items). Result: **present 16, absent 12, ambiguous 0** (all HIGH/MEDIUM
confidence except two LOW).

Class breakdown (present/absent/ambiguous):

| class | present | absent | ambiguous |
|---|---:|---:|---:|
| STOP (/t/) | 5 | **3** | 0 |
| FRICATIVE (/s/, /v/) | 2 | **3** | 0 |
| LIQUID (/r/) | 1 | **5** | 0 |
| NASAL (/n/) | 8 | **1** | 0 |

Final-consonant deletion is a **broad class** in this sample, not limited to "four": three
"eight" /t/ deletions (child_03, 07, 08), one "six" /s/ (child_03), two "five" /v/
(child_09, child_01), five "four" /r/.

---

## 8. Phone vs Acoustic Evidence (real labels)

Phone presence = match_type exact|soft; acoustic = exploratory class rules on FINAL_RAW:

| combination | n |
|---|---:|
| BOTH_RIGHT | 16 |
| PHONE_RIGHT_ACOUSTIC_WRONG | 6 |
| BOTH_WRONG | 5 |
| PHONE_WRONG_ACOUSTIC_RIGHT | 1 |

Phone evidence was correct on **22/28**; acoustic on **17/28**. When the acoustic layer differed
from phone evidence it was wrong **6×** vs right **1×**.

`Results/acoustic_support_value.csv`, `final_consonant_human_vs_model.csv`.

---

## 9. Independent Acoustic Value

**Not established — as implemented the layer is redundant/contaminated.** Features were
anchored on the phone model's CTC spans, so they inherit forced-alignment boundary errors
(voicing 0.95–1.0 on human-confirmed absent /r/). It adds no incremental value and mostly
adds error.

The concept is not fully tested: an independent layer must anchor on **acoustic landmarks**
(energy/voicing offset at word end, spectral-class detection), not the CTC span. This remains
the P2 next step.

---

## 10. Objective B: Expanded Window Human Validation

20 stratified tokens × 5 representative windows = 100 clips:
FULL, RAW, PAD100, PAD250, PAD500 (PAD200/300 covered numerically in 1.9.11).
Anonymous order A–E per token (stable seed recorded in metadata; mapping hidden until reveal).

---

## 11. Human Review Design

- Blind first: Clip A–E audio only; 4 questions per clip (clarity, boundary quality, target
  recognizability, reviewer confidence)
- Question framing: "which clip gives enough context to judge reliably?" — not "which score is best"
- Reveal page shows A–E mapping + scores after Stage A

---

## 12. Reviewer Population

**1 reviewer** (single). No inter-rater statistics computed; `window_reviewer_agreement.csv`
records `N/A — single reviewer`. No claim of independent validation.

---

## 13. Inter-Rater Agreement

Not computable (n=1 reviewer). Template left explicit.

---

## 14. Window Results (35 direct + 65 imputed by reviewer rule)

Reviewer rule (documented in `apply_window_imputation.py`): unrated clips in a token are
perceptually identical to the rated clips of the same token → carry nearest rated values
(flagged `imputed_by_reviewer_rule`).

| window | good clarity | boundary clean | recog clear | excess context |
|---|---:|---:|---:|---:|
| FULL | **0.65** (13/20) | 13 | 15 | 6 |
| RAW | 0.55 (11/20) | 13 | 14 | 5 |
| PAD100 | 0.60 (12/20) | 13 | 16 | 6 |
| PAD250 | 0.60 (12/20) | 15 | 17 | 4 |
| PAD500 | 0.60 (12/20) | 14 | 17 | 6 |

- **No window shows a human-perceptible advantage** (spread 0.10, FULL nominally highest)
- PAD250 has the fewest EXCESS_CONTEXT flags (4) but also no clarity advantage
- The partial-data PAD250 advantage did **not** survive the reviewer's same-token rule —
  it was an artifact of which clips were chosen to rate

---

## 15. Human Clarity vs Score

Full-coverage (with imputation): mean score by clarity — CLEAR 67.5 (n=50), POOR 54.6 (n=17),
MOSTLY_CLEAR 43.3 (n=10), AMBIGUOUS 32.4 (n=23).

- **4 high-score/poor-clarity** mismatches ("four" full/pad 100 rated POOR)
- **14 low-score/good-clarity** mismatches — incl. **tok_04 nine**: RAW score 33.8 but rated
  CLEAR/CLEAN/CLEAR. Numeric window drops did not correspond to perception.
- Artifacts: `window_clarity_vs_score.csv`, `window_mismatch_cases.csv`

These mismatches support the 1.9.11 conclusion that window score changes are not automatically
perceptual improvements.

---

## 16. Final-Consonant × Window Interaction

`Results/final_consonant_window_interaction.csv`:
- **14/65** final-consonant tokens had raw&lt;50 and best-pad≥50 ("restored by padding"):
  5 /r/, 5 /n/, 2 /v/, 2 /s/
- The 5 /r/ "four" cases are the known human-confirmed ending-sound deletions — padding raises
  their scores, i.e., padding can **rescue true errors** for final consonants
- Padding for /n/ tokens mostly reflects alignment recovery, not consonant restoration

---

## 17. Speaker-Level Analysis

Final-consonant tokens span 9 children (max 3 per speaker in the window pack).
No single child dominates either pack. Per-speaker window behavior to be computed after submission.

---

## 18. What Is Proven

1. 65 real child tokens with final consonants exist across 4 phoneme classes (n/s/v/t/r).
2. **Final-consonant deletion is common in this child sample:** 12/28 human-reviewed tokens
   absent — /r/ 5/6, /t/ 3/8, fricatives 3/5, /n/ 1/9.
3. CTC-span-anchored acoustic features **inherit forced-alignment errors** → redundant
   (phone right 22/28 vs acoustic 17/28; acoustic wrong 6× vs right 1× when differing).
4. 14/65 final-consonant tokens are "restored by padding" (raw&lt;50→pad≥50), including all 5
   confirmed /r/ deletions → padding is unsafe for this class.
5. Window ratings (partial) show **score changes do not always match perception** (tok_04:
   RAW 33.8 rated CLEAR; "four" full/pad 100 rated POOR).

---

## 19. What Is NOT Proven

1. That independent acoustic support adds value (current layer contaminated; 1 candidate case).
2. That padding improves human-perceived evidence (review pending).
3. Any window preference or policy.
4. That /n/ or /v/ finals behave like /r/ (class-specific conclusions need labels).
5. That the preliminary phone-vs-acoustic table generalizes (13 inferred labels).

---

## 20. Risks and Limitations

- Single reviewer; no inter-rater validation
- Inferred (not blind) labels used for the preliminary P2 table — clearly marked
- CTC spans are not ground-truth final-consonant boundaries
- 5-window human design deviates from the 7-window numeric design (documented, burden control)
- 20 tokens < 80-token target (single reviewer feasibility; documented)
- No class-balanced sample for /t/ and /s/ in the human pack (few tokens)

---

## 21. Research-Only Policy Recommendation

- **P2 next iteration (research):** extract acoustic features from **landmark-anchored** regions
  (energy/voicing offset from word end), then re-test incremental value on human-labeled finals.
- **Window:** do not adopt any policy; use the expanded review to test whether human judgments
  track the numeric differences before any policy candidate is named.
- `research_window_policy_candidate`: none yet.

---

## 22. Decision

**DECISION_ACOUSTIC: B. ACOUSTIC_SUPPORT_IS_REDUNDANT**  
(as implemented: CTC-anchored features add no value; concept remains untested with
landmark anchoring)

**DECISION_WINDOW: C. HUMAN_VALIDATION_DOES_NOT_SUPPORT_WINDOW_CHANGE**  
(no human-perceptible advantage for any window; score changes often not perceptual; single
reviewer with 65/100 imputed by the reviewer's same-token rule)

```
production_vad = false
router_locked = false
unity_integrated = false
scorer_modified = false
production_window_locked = false
```

---

## 23. Exact Next Step

1. (Research only) implement **landmark-anchored** final-consonant features (energy/voicing
   offset at word end) and re-test incremental value on the 28 human-labeled finals.
2. Keep the window question closed for now: human validation does not support a window change;
   any future window work must show perceptual benefit, not just numeric movement.
3. Keep Silero default, hybrid research-only, scorer and window policy frozen.

---

## Artifacts

`Results/` (inventory, features, human review, human-vs-model, acoustic value, window expanded,
agreement, consensus, FC×window interaction, master), `HumanReview/` (4 HTML packs, metadata,
28+100 clips), `Scripts/` (run_final_consonant, build_window_expanded, serve_review).
Raw child audio remains outside Git.
