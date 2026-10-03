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
| A — Final-consonant acoustic support | 65 child tokens with final consonants; features extracted on 5 regions each (325 rows); **preliminary acoustic layer is NOT independent** (anchored on CTC spans) → decision **C** |
| B — Expanded window human validation | 20 stratified tokens × 5 anonymous windows = **100 clips**; blind pack ready; reviewer submission pending → decision **D** for now |

Key measured facts:

- Child final-consonant classes available: NASAL 31 (/n/), FRICATIVE 16 (/s/ 7, /v/ 9),
  STOP 9 (/t/), LIQUID 9 (/r/). Other classes absent in child data.
- **Methodology trap found:** extracting acoustic features over CTC-derived spans makes them
  inherit forced-alignment errors — for the 5 human-confirmed ABSENT final /r/ ("four") cases
  the span voicing ratio was **0.95–1.0 → "PRESENT"** (wrong).
- Preliminary phone-vs-acoustic-vs-human on 13 inferred labels: BOTH_RIGHT 7, BOTH_WRONG 4,
  PHONE_WRONG_ACOUSTIC_RIGHT 1, PHONE_RIGHT_ACOUSTIC_WRONG 1.
- 14/65 final-consonant tokens had raw<50 → pad≥50 ("restored by padding"), including 5 /r/.
- Window pack: 20 tokens (HIGH 3, MID 3, LOW 3, VERY_LOW 3, WINDOW_SENSITIVE 4,
  FINAL_CONSONANT_CASE 4), 5 windows (FULL, RAW, PAD100, PAD250, PAD500), randomized A–E.

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

## 7. Human Final-Consonant Review

New blind pack (`HumanReview/final_consonant_blind.html` → `_reveal.html`), **28 items**
(eight 8, four 6, seven 4, five 3, one/six/ten 2, nine 1), 5-point final-consonant schema
(clearly present → clearly absent; AMBIGUOUS kept distinct). **Submission pending** (LAN 8768).

Existing inferred labels from 1.9.11 packs (13 tokens) used for the preliminary analysis only:
5 "four" = CLEARLY_ABSENT (ending-sound rule); 8 correct tokens = inferred PRESENT.

---

## 8. Phone vs Acoustic Evidence

Preliminary (13 inferred labels; phone presence = exact|soft):

| combination | n |
|---|---:|
| BOTH_RIGHT | 7 |
| BOTH_WRONG | 4 |
| PHONE_WRONG_ACOUSTIC_RIGHT | 1 (child_07 one: phone miss, acoustic PRESENT, human PRESENT) |
| PHONE_RIGHT_ACOUSTIC_WRONG | 1 (child_09 four: phone miss→absent correct, acoustic PRESENT wrong) |

`Results/acoustic_support_value.csv`, `final_consonant_human_vs_model.csv`.

---

## 9. Independent Acoustic Value

**Not established.** The current acoustic layer is span-anchored → it mostly echoes phone
boundaries (BOTH_WRONG on 4/5 confirmed-absent "four"). One case (child_07 one) shows potential
acoustic value for /n/, but n=1.

The independent layer required by the phase must anchor on **acoustic landmarks**
(energy/voicing offset at word end, spectral-class detection) rather than the CTC span.
This is the main P2 methodological finding.

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

## 14. Window Results

Pending human submission. Numeric context (1.9.11): FULL≈padded in aggregate; RAW is the outlier
(rejects 5/9 human-correct in the 13-token spot check). This pack tests whether human clarity/
boundary/recognizability judgments agree with those numbers.

---

## 15. Human Clarity vs Score

Pending. After submission: cross-tab clarity × score band × window, plus
high-score/poor-clarity and low-score/high-clarity cases.

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
2. CTC-span-anchored acoustic features **inherit forced-alignment errors** (5/5 confirmed-absent
   /r/ showed "PRESENT" voicing) → not independent evidence.
3. 14/65 final-consonant tokens are "restored by padding" (raw&lt;50→pad≥50), including all 5
   confirmed ending-sound deletions → padding is unsafe for this class.
4. A 100-clip anonymous A–E window pack across 20 stratified tokens is ready for human review.
5. LWE vocabulary finals are absent from the child dataset (coverage still LIMITED).

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

**DECISION_ACOUSTIC: C. ACOUSTIC_SUPPORT_IS_PROMISING_BUT_INSUFFICIENT**  
(implementation not independent; needs landmark anchoring + human labels)

**DECISION_WINDOW: D. EVIDENCE_INSUFFICIENT**  
(pending expanded human submission; no policy supported)

```
production_vad = false
router_locked = false
unity_integrated = false
scorer_modified = false
production_window_locked = false
```

---

## 23. Exact Next Step

1. Human submits the two packs over LAN (port 8768):
   - `final_consonant_blind.html` → `_reveal.html` (28 items)
   - `window_blind.html` → `window_reveal.html` (20 tokens × 5 clips)
2. Recompute: final-consonant human-vs-model, acoustic value with real (not inferred) labels;
   window clarity/boundary/recognizability vs scores; update both decisions.
3. Then (research only): implement landmark-anchored final-consonant features and re-test.

---

## Artifacts

`Results/` (inventory, features, human review, human-vs-model, acoustic value, window expanded,
agreement, consensus, FC×window interaction, master), `HumanReview/` (4 HTML packs, metadata,
28+100 clips), `Scripts/` (run_final_consonant, build_window_expanded, serve_review).
Raw child audio remains outside Git.
