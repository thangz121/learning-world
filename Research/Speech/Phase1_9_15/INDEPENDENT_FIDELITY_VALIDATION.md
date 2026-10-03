# INDEPENDENT FIDELITY / ASSESSABILITY VALIDATION — P1

**Scripts:** `experiments/build_fidelity_review.py`, `serve_review.py`, `merge_fidelity_review.py`,
`behavior_audit.py`
**Artifacts:** `artifacts/fidelity/fidelity_independent.csv` (115), `independent_metrics.json`,
`system_behavior_audit.csv/json`, `selection_table.csv`, `candidate_pool.csv`
**Review:** blind pack served on LAN/Tailscale; reviewer `human_maynode`; submission
`Results/fidelity_v2_StageA_Filled.csv` (115 rows, 2026-10-03).

---

## 1. This is a genuinely new blind set

- 115 recordings selected from the real-child corpus (Zenodo 200495) **excluding every
  previously reviewed (speaker, target) pair** used in 1.9.9–1.9.12.
- Stratified selection (~12 strata) using duration / VAD / RMS / ZCR / ASR / soft-v2 signals
  computed at selection time; **no model score was visible in the review UI**.
- The reviewer could not see v1/v2 outputs, strata, or scores.
- Single reviewer, 0 `UNCERTAIN` labels (the reviewer was decisive on all 115) —
  **no inter-rater agreement is computable**; this is documented, not hidden. All conclusions
  are single-reviewer evidence.
- Independent labels were **not used to tune the rules**; v1/v2 are frozen from 1.9.14.

Strata selected (n): NO_SPEECH 2, VERY_SHORT 8, NEAR_SILENCE 4, SOFT 8, LOUD 8, NOISY 12,
ASR_EMPTY 14, ASR_WRONG 10, PHONE_LOW 8, ORDINARY_HIGH 15, ORDINARY_MID 6, SENTENCE 10,
FREE_SPEECH 10.

## 2. Reviewer labels (human authority)

| question | distribution |
|---|---|
| content | SPEECH_PRESENT 114, NO_SPEECH 1, UNCERTAIN 0 |
| attempt | VALID_ATTEMPT 99, UNINTELLIGIBLE 12, POSSIBLE_ATTEMPT 3, INCOMPLETE 1 |
| assessability | ASSESSABLE 107, NOT_ASSESSABLE 8 |
| confidence | 0 LOW / no UNCERTAIN (decisive) |

The reviewer used `UNINTELLIGIBLE` as the "unclear speech" label; no `FREE_SPEAK_WRONG_TARGET`
items were chosen, so the system's `FREE_SPEAK` state has no human counterpart label in this
set (noted in metrics).

## 3. Required metrics (frozen rules on the new set)

Human-valid attempt = content SPEECH_PRESENT and attempt ∈ {VALID_ATTEMPT, POSSIBLE_ATTEMPT,
INCOMPLETE} → n = 103 (strict VALID_ATTEMPT only → n = 99).

| metric | v1 literal | v2 child-safe |
|---|---:|---:|
| **false gate on valid attempts** | **17/103 = 16.5%** | **0/103 = 0.0%** |
| false gate (strict) | 16/99 = 16.2% | 0/99 = 0.0% |
| human NOT_ASSESSABLE n | 8 | 8 |
| dangerous accept (strict: system VALID/ASSESSABLE) | 2/8 = 25.0% | 5/8 = 62.5% |
| dangerous accept (loose: also POSSIBLE_ATTEMPT) | 6/8 = 75.0% | 7/8 = 87.5% |

v2 confusion (assessability × system state):

| human assessability | POSSIBLE_ATTEMPT | VALID_ATTEMPT | ASSESSABLE | NO_SPEECH |
|---|---:|---:|---:|---:|
| ASSESSABLE (107) | 31 | 69 | 7 | 0 |
| NOT_ASSESSABLE (8) | 2 | **5** | 0 | 1 |

## 4. What survived, what failed

**Survived independent validation:** the 1.9.14 result that the child-safe rules do **not**
false-gate human-valid child speech: v1 gates 16.5% (including 9 FREE_SPEAK and 9
UNINTELLIGIBLE on human-valid attempts), v2 **0%**. The core "NO EVIDENCE ≠ BAD
PRONUNCIATION" separation holds on unseen recordings.

**Failed / insufficient:** v2's *refusal* states are under-sensitive. Of 8 human
`NOT_ASSESSABLE`/`UNINTELLIGIBLE` recordings, v2 refuses only 1 (`NO_SPEECH`); it passes
5 as `VALID_ATTEMPT` (MEDIUM assessability) — the dangerous false acceptance the spec asks to
track. Root causes (each traceable):

1. Four VERY_SHORT tokens (0.15–0.5 s, `three`/`nine`, soft 39–67, ASR empty/wrong):
   v2's `MIN_DUR_S=0.15` and confidence floor (`≥0.005`) keep them VALID/POSSIBLE even though
   the human could not understand them.
2. One ORDINARY_HIGH outlier `fi_076` (`ten`): frozen soft score **100.0** but the human
   rated it UNINTELLIGIBLE (ASR empty, conf 0.197). No signal available to v2 contradicts the
   high score — an evidence-layer failure, not a rule-order failure.
3. Two free-speech segments get `POSSIBLE_ATTEMPT (LOW)` (correctly low but not refusal) and
   one is NO_SPEECH (correct).

The trade is explicit: **v1 catches more unusable recordings by using ASR as validity
evidence, but rejects 16.5% of valid child speech; v2 never rejects valid speech but misses
most human-unintelligible recordings.** Under the program's FRR-first rule, v2's direction is
correct (protect human-valid child speech), but it must not be described as a validated
assessability gate — refusal detection needs new evidence (e.g., intelligibility modelling or
an acoustic-quality detector), not threshold tweaks tuned on this set.

## 5. Other P1 findings

- 1/1 human `NO_SPEECH` label was detected (`NO_SPEECH`, precision 1.0) — but only 1 case
  exists; no statistical claim.
- 4 human `UNINTELLIGIBLE` tokens were still rated `ASSESSABLE` by the reviewer — confirming
  intelligibility and assessability are different judgments; do not collapse them.
- `VALID_ATTEMPT` system precision 0.89 (66/74) vs human attempt labels; `POSSIBLE_ATTEMPT`
  precision 0.06 — v2 uses POSSIBLE as the default low-evidence bucket, as designed.
- Unlabeled behavior audit before review (`system_behavior_audit.json`): v1 would have gated
  20/115, v2 only 1; 2 VAD-negative high-energy items → POSSIBLE_ATTEMPT; 35 ASR-empty items
  → VALID/ASSESSABLE; 0 free-speech items scored MEDIUM/HIGH. This predicted the observed
  direction (v2 safe, under-refusing) before labels existed.

## 6. Status

`assessability_separation_independently_validated = TRUE (false-gate 0/103)`
`assessability_refusal_detection_validated = FALSE (5/8 dangerous strict, 62.5%)`
`single_reviewer = TRUE (documented)`
`rules_tuned_on_this_set = FALSE`
