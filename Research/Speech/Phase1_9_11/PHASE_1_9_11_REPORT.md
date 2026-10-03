# PHASE 1.9.11 REPORT — Methodology Repair + SCORER_MISS Forensics + 80-Token Window A/B

**Base:** Phase 1.9.9 `e2449c5`, Phase 1.9.10 `cec525e` → repair commit `10a1d25`  
**Date:** 2026-10-03  
**production_vad:** false · **router_locked:** false · **unity_integrated:** false · **scorer_modified:** false  
**asr_is_not_pronunciation_judge:** true

---

## 1. Executive Summary

Three sequential objectives completed:

| # | Objective | Result |
|---|---|---|
| 1 | Repair 1.9.10 human-label methodology | **PASS** — 15 stale items fixed; three human states; pme_01 conflict visible |
| 2 | Forensic 6 SCORER_MISS cases | 4/6 **window-dependent** (score range ≥20), 2/6 **soft-match too permissive** |
| 3 | 80-token window A/B (7 variants + 4 policies) | **window-sensitive rate 51.3%**; full≈padded in aggregate; raw VAD is the outlier |

**Key human-conditioned findings (80 tokens; 17 confirmed correct, 15 confirmed error, 10 uncertain, 38 not reviewed):**

- Confirmed **correct**: full & padded reject 0/17; **raw VAD rejects 7/17 (41%)** → raw hurts sensitivity
- Confirmed **error**: full passes 6/15; **padded passes 7/15 (false rescue)**; raw passes only 2/15
- `false_rescue` = 7/15 confirmed errors; `true_error_rescue` = 5/15
- crop-beats-full inversion: 4/80 (5%); full-beats-raw: 17/80

**Human review (both packs) completed** — key human-verified results:

- SCORER_MISS: **3/6 confirmed TRUE_SCORER_MISS** (all "four", reviewer rule: missing ending sound
  = fail); 2 uncertain; 1 conflicted. FULL scores these 100/100/66.8 → forced alignment cannot
  represent a deleted final phone.
- Window spot-check (13): FULL misses **3/3** confirmed ending-sound errors while rejecting **3/9**
  correct; RAW catches 2/3 errors but rejects **5/9** correct; PAD250 misses 2/3 errors.

**Decision:** **B. WINDOW_EFFECT_REAL_BUT_NOT_SAFE** — window effects are real and large per-token,
but no single window/policy is safe; padding rescues true errors, raw VAD rejects correct speech.

`SCORER_FORMULA_CHANGE: RESEARCH_NEEDED` (ending-sound/final-phone evidence; not implemented)

---

## 2. Phase 1.9.10 Methodology Repair

Audit (`Results/phase1_9_10_consistency_audit.csv`, 15 items, all FIXED):

- hard-coded `old_human_ok=29 / old_low_ok=12` → derived from records
- `UNCERTAIN → SYSTEM_SIDE_ACCEPTABLE_LOW_SCORE` removed
- stale decision text "8/12 acceptable / 4/12 true errors" removed from script/master/decision
- report speaker mapping fixed (pme_01 is child_03, not child_07)
- master/decision rebuilt; decision restored to **B. ROOT_CAUSE_PARTIALLY_IDENTIFIED**

Remaining textual mentions of "8/12" are repair descriptions only (not data).
`PHASE_1_9_10_CONSISTENCY: PASS`

---

## 3. Human Label Semantics

| state | meaning |
|---|---|
| HUMAN_TRUE_ERROR | Stage B explicitly TRUE_PRONUNCIATION_ERROR |
| HUMAN_UNCERTAIN | Stage B UNCERTAIN — **not** acceptable, **not** truth |
| HUMAN_CONFLICTED | Stage A correct + Stage B true error (pme_01) |
| HUMAN_ACCEPTABLE | explicit acceptable verdict (none in this set) |

Raw Stage B (source of truth): **TRUE_PRONUNCIATION_ERROR = 5, UNCERTAIN = 7**.  
Normalized: **4 TRUE_ERROR + 7 UNCERTAIN + 1 CONFLICTED + 0 ACCEPTABLE**.

---

## 4. Phase 1.9.10 Recalculated Metrics

Counts derived from `Phase1_9_9/Results/human_review_results.csv`:
reviewed 42, first-pass correct 29, incorrect 11, ambiguous 2, correct-low 12 → rate 0.414 (superseded).

Revised `human_correct_low_score_rate` (explicit definition): confirmed-acceptable =
first-pass correct cases **not** revised by second pass → denominator **17**, numerator **0**,
**rate 0.0**; 8 unresolved cases (7 uncertain + 1 conflicted) excluded.
`metric_status = RECALCULATED_WITH_EXPLICIT_DEFINITION`.

---

## 5. The 6 SCORER_MISS Cases

From Phase 1.9.9 `diagnostic == SCORER_MISS` (human incorrect, score ≥50):

| case | speaker | target | 1.9.9 score | canonical | observed (full) | n_miss |
|---|---|---|---:|---|---|---:|
| sm_01 | child_03 | six | 50.0 | S IH1 K S | s æ k k | 2 |
| sm_02 | child_03 | two | 50.0 | T UW1 | t j | 1 |
| sm_03 | child_04 | eight | 50.0 | EY1 T | n t | 1 |
| sm_04 | child_07 | four | 100.0 | F AO1 R | f ɔ ɹ | 0 |
| sm_05 | child_06 | four | 100.0 | F AO1 R | f ɔ ɹ | 0 |
| sm_06 | child_09 | four | 66.8 | F AO1 R | f ɔ æ | 1 |

Artifact: `Results/scorer_miss_cases.csv`

---

## 6. SCORER_MISS Forensics

Frozen scorer; variants FULL/RAW/PAD 100–500 (`scorer_miss_forensics.csv`):

| case | full | raw | p100 | p200 | p250 | p300 | p500 | range | mechanism |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| sm_01 | 50.0 | 25.1 | 50.1 | 50.0 | 50.0 | 50.0 | 50.0 | 25.0 | WINDOW_DEPENDENT |
| sm_02 | 50.0 | 0.0 | 50.0 | 50.0 | 50.0 | 50.0 | 50.0 | 50.0 | WINDOW_DEPENDENT |
| sm_03 | 50.0 | 58.8 | 50.0 | 50.0 | 50.0 | 50.0 | 50.0 | 8.8 | SOFT_MATCH_TOO_PERMISSIVE |
| sm_04 | 100.0 | 6.0 | 100.0 | 100.0 | 100.0 | 100.0 | 100.0 | **94.0** | WINDOW_DEPENDENT |
| sm_05 | 100.0 | 66.7 | 100.0 | 100.0 | 100.0 | 100.0 | 100.0 | 33.3 | SOFT_MATCH_TOO_PERMISSIVE |
| sm_06 | 66.8 | 6.1 | 33.7 | 66.8 | 66.8 | 66.8 | 66.8 | 60.7 | WINDOW_DEPENDENT |

- **4/6 window-dependent** (range ≥20): RAW VAD drops far below full/padded
- **2/6 soft-match too permissive**: all windows ≥50 despite phone mismatches
- sm_04 (four): raw 6.0 vs full/padded 100 — extreme window swing on a human-CLEAR_INCORRECT token
- **The reverse failure is connected to the window problem**: for 4/6, the raw window would not
  have missed the error (score <50), while full/padded produce the high score

Mechanism rules are documented in the script; confidence MEDIUM for both mechanisms;
human second review pack prepared (labels empty).

---

## 7. Human Review of SCORER_MISS (completed)

Submitted over LAN (`serve_review.py`, pack=`scorer_miss`). Reviewer rule recorded:
**missing ending sound → fail**.

| case | target | Stage A | Stage B | state |
|---|---|---|---|---|
| sm_01 | six | CLEAR_CORRECT | TRUE_SCORER_MISS | **CONFLICTED** |
| sm_02 | two | CLEAR_CORRECT | UNCERTAIN | UNCERTAIN |
| sm_03 | eight | PROBABLY_INCORRECT | HUMAN_UNCERTAINTY | UNCERTAIN |
| sm_04 | four | PROBABLY_INCORRECT | TRUE_SCORER_MISS | **TRUE_ERROR** |
| sm_05 | four | CLEAR_INCORRECT | TRUE_SCORER_MISS | **TRUE_ERROR** |
| sm_06 | four | CLEAR_INCORRECT | TRUE_SCORER_MISS | **TRUE_ERROR** |

- **3/6 confirmed TRUE_SCORER_MISS** — all "four" with missing final /r/
- FULL scored these **100.0 / 100.0 / 66.8** while the reviewer heard no ending sound
- Observed phone evidence still emitted `f ɔ ɹ` (sm_04/05, 0 misses) →
  **forced alignment assigns a match to a deleted final phone**; mechanism:
  `FINAL_CONSONANT_DELETION_NOT_REPRESENTED_BY_FORCED_ALIGNMENT`
- sm_01/sm_02/sm_03 remain uncertain/conflicted → not clean ground truth
- Artifact: `Results/scorer_miss_human_review.csv`

---

## 8. Frozen 80-Token Dataset

Phase 1.9.8 studio-number 80 tokens; full scores reused from the frozen 1.9.8 CSV.
Human status mapping (from 1.9.9 + repaired 1.9.10 states):

| status | n |
|---|---:|
| HUMAN_CONFIRMED_CORRECT | 17 |
| HUMAN_CONFIRMED_ERROR | 15 |
| HUMAN_UNCERTAIN | 10 (incl. 1 conflicted) |
| NOT_HUMAN_REVIEWED | 38 |

**Most of the 80 tokens remain NOT_HUMAN_REVIEWED** — no fabricated ground truth.

---

## 9. Window A/B Design

Conditions: FULL (frozen 1.9.8), RAW_VAD (Silero 0.5 union), PAD_100/200/250/300/500 (predefined,
no optimization against the 80 tokens). Scorer untouched.

Policies:
- **M1** oracle-best → `ORACLE_LIKE_UPPER_BOUND` (not a proposal)
- **M2** median across 6 windows
- **M3** smallest stable padded window (first pad within 5 of pad500; else median fallback)
- **M4** agreement pad200↔pad300 (threshold tested 5/10/15; main = 10, conservative min on disagreement)

---

## 10. FULL vs RAW_VAD

| variant | mean | median | frac ≥50 | frac <50 |
|---|---:|---:|---:|---:|
| FULL | 63.9 | 66.8 | 0.72 | 0.28 |
| RAW_VAD | 55.4 | 54.5 | 0.56 | 0.44 |

- raw-beats-full: **4/80 (5%)**; full-beats-raw: **17/80 (21%)**
- Human-conditioned: raw VAD fails **7/17 confirmed-correct** tokens (41%) → poor sensitivity
- Human-conditioned: raw passes only **2/15 confirmed-error** tokens → better specificity

---

## 11. Padding Results

| variant | mean | frac ≥50 |
|---|---:|---:|
| PAD_100 | 62.7 | 0.66 |
| PAD_200 | 64.2 | 0.74 |
| PAD_250 | 63.7 | 0.72 |
| PAD_300 | 64.5 | 0.74 |
| PAD_500 | 63.9 | 0.72 |

Padding ≥200 ms is aggregate-equivalent to FULL.  
**Danger:** among 15 confirmed errors, **7 pass (≥50) under padding**; 5 of those are raw&lt;50→pad≥50
rescues. `pad_rescue_confirmed_correct = 0` (no correct token needed padding rescue).

---

## 12. Multi-Variant Results

| policy | mean | frac ≥50 |
|---|---:|---:|
| M1 oracle (upper bound) | 71.1 | 0.81 |
| M2 median | 63.9 | 0.72 |
| M3 smallest stable | 63.9 | 0.72 |
| M4 agreement (th=10) | 63.0 | 0.72 |

M4 agreement rates: th5 91.3%, **th10 92.5%**, th15 92.5% (exploratory).
Policies M2–M4 ≈ FULL in aggregate; no policy demonstrably safer on confirmed errors.

---

## 13. Window Sensitivity

- **window-sensitive rate (range ≥20): 51.3%**
- median range **23.5**; p90 range **50.3**
- More than half of the tokens move ≥20 points depending only on the audio window.

---

## 14. FULL vs PADDED Inversions

Explicit thresholds (documented): FULL_UNDERSCORES = full < median_pad − 10;
FULL_OVERSCORES = full > median_pad + 10; else WINDOW_STABLE.

| group | n |
|---|---:|
| WINDOW_STABLE | 75 |
| FULL_UNDERSCORES | 3 |
| FULL_OVERSCORES | 2 |

- pad-beats-full (pad≥50 while full<50): **4/80**
- full-beats-pad (full≥50 while all pads<50): **0/80**
- So FULL vs padded rarely inverts; the sensitivity comes from RAW vs everything else.

---

## 15. SCORER_MISS × Window Interaction

- 4/6 SCORER_MISS cases are window-dependent; their high scores come from FULL/padded windows
- Under the raw VAD window, only 2/15 confirmed errors still pass (vs 6/15 full, 7/15 padded)
- **Human-confirmed ending-sound class:** the 3 "four" errors pass at FULL (100/100/66.8);
  RAW catches 2/3 (6.0 / 66.7 / 6.1) but fails sm_05; PAD250 catches 0/3 (100/100/100)
- **Window choice materially changes the specificity/sensitivity balance** — the apparent
  "scorer miss" is partly a window/evidence problem, partly forced-alignment/phone-model
  permissiveness on deleted final phones

---

## 16. Human Spot Check (completed)

Balanced pack: 13 unique items (FULL_UNDERSCORES 3, FULL_OVERSCORES 2, WINDOW_STABLE 5,
LARGEST_RANGE 3 after dedup). Human states: **9 correct, 3 error, 1 uncertain**.

Window-conditioned behavior on the human-checked subset (`window_human_spot_check.csv`):

| window | correct pass | correct rejected | error pass | error caught |
|---|---:|---:|---:|---:|
| FULL | 0.67 | **3/9** | **1.00** | **0/3** |
| RAW_VAD | **0.44** | **5/9** | 0.33 | **2/3** |
| PAD250 | 0.67 | 3/9 | 0.67 | 1/3 |

Notable human-checked cases:
- **tok_58 one (child_07)**: CLEAR_CORRECT — full=0.0, pad250=0.0, **raw=72.5** → crop beats full
  on a human-correct token (direct confirmation of the inversion)
- **tok_56 four (child_07)**: CLEAR_INCORRECT — full=100, raw=6.0 → full misses the ending-sound
  error; raw catches it
- **tok_79 six (child_09)**: CLEAR_CORRECT, Stage B = WINDOW_EFFECT — raw=0.0 vs full/pad=75
- **tok_30 four (child_04)**: PROBABLY_INCORRECT — full=66.7 passes, raw/pad=33 correctly fail

**No window is safe: FULL misses true errors, RAW rejects correct speech.**
Limitation: 3/2 under/over cases exist, so the "5 each" target was not reachable.

---

## 17. What Is Proven

1. Phase 1.9.10 methodology repaired: 5 TRUE + 7 UNCERTAIN raw; 4+7+1+0 normalized;
   pme_01 conflict visible; stale 8/12–4/12 text removed (audit PASS).
2. Revised 1.9.9 metric: 0.414 superseded → 0.0 (0/17 confirmed) with 8 unresolved.
3. **Human-confirmed ending-sound class:** 3 "four" tokens with deleted final /r/ score
   100/100/66.8 at FULL → forced alignment cannot represent deleted final phones.
4. Window effects are real and large per-token: 51.3% sensitive; median range 23.5.
5. FULL ≈ padded in aggregate; **RAW VAD is the outlier** (hurts correct speech).
6. **Human-checked trade-off (13 tokens):** FULL catches 0/3 errors and rejects 3/9 correct;
   RAW catches 2/3 errors and rejects 5/9 correct; PAD250 catches 1/3 and rejects 3/9.
7. Crop-beats-full confirmed by ear on tok_58 (correct token: full=0, raw=72.5).
8. No multi-variant policy beats FULL in aggregate safety on confirmed labels.

---

## 18. What Is NOT Proven

1. That any window policy improves real scoring safety.
2. That sm_01/sm_02/sm_03 behave like the confirmed ending-sound cases (uncertain/conflicted).
3. That soft-match permissiveness generalizes beyond these cases.
4. That the 10 uncertain/38 unreviewed 80-token cases behave like the labeled ones.
5. That a final-phone acoustic-support check would fix the ending-sound class without
   breaking correct speech (not implemented, not tested).

---

## 19. Risks

- Adopting padded windows without human checks could **hide true errors** (7/15 false rescues).
- Adopting raw VAD windows could **reject correct child speech** (7/17).
- M1 oracle policy is an upper bound and must never be presented as achievable.
- 38/80 tokens have no human labels; aggregate numbers are label-biased.

---

## 20. Proposed Research-Only Policy

None adopted. Two separate proposals, documented only:

**P1 — Window/evidence policy (not safe yet):** multi-variant evidence with explicit
disagreement flagging (M4-style) rather than a single window.
proposed_change = score with pad200+pad300 agreement rule (th=10 exploratory);
expected_effect = flag unstable tokens instead of silently rescoring;
risk = disagreement rate unvalidated; RAW/FULL trade-off now human-confirmed as unsafe;
validation_plan = larger human-checked sample per window.

**P2 — Final-phone evidence check (new, from human finding):**
proposed_change = for word-final consonants, require acoustic support (e.g., energy/phone
posterior above a floor in the final span) before accepting the forced-alignment match;
expected_effect = catch deleted final phones like the "four" class;
risk = may reject correct weak final consonants;
validation_plan = test on the 3 confirmed cases + matched correct tokens with human labels.
**Not implemented; scorer remains frozen.**

---

## 21. Decision

### **B. WINDOW_EFFECT_REAL_BUT_NOT_SAFE**

```
production_vad = false
router_locked = false
unity_integrated = false
scorer_modified = false
```

`SCORER_FORMULA_CHANGE: RESEARCH_NEEDED` — specifically for the human-confirmed
final-consonant-deletion class (P2 above); NOT implemented.

---

## 22. Exact Next Step

1. Design a research-only **final-phone acoustic-support check** (P2) targeting the confirmed
   "four" ending-sound class; test against matched correct tokens before any scorer change.
2. Expand human-checked window sample (the 13-token spot check is too small to pick a window
   policy); no policy adoption until false-rescue is human-verified.
3. Keep Silero default, hybrid research-only, scorer frozen.

---

## Artifacts

`Results/` (consistency audit, human normalization, scorer-miss cases/forensics/review,
window A/B 80 tokens, window summary, multi-variant, spot-check, master, decision),
`HumanReview/` (4 HTML packs, metadata, clips), `Scripts/` (forensics, window A/B, server).
Raw child audio remains outside Git.
