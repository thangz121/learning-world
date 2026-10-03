# PHASE 1.9.14 — FIDELITY / ASSESSABILITY + DELETION-AWARE PRONUNCIATION RESEARCH

**Date:** 2026-10-03 · **Machine:** ASUS · **Branch:** `ux/math-arenas-hotfix-20260930`
**Base:** Phase 1.9.13 `3ab6c01` (consolidation), HEAD `b4a140e`
**Flags (unchanged):** `production_vad=false · router_locked=false · unity_integrated=false ·
scorer_modified=false · production_window_locked=false`
**Decision:** **B = RESEARCH_SUPPORTS_PARTIAL_CHANGE**

---

## 1. Mission

Determine whether LWE has been asking the pronunciation scorer a question it should not have
been asked yet — i.e. whether pronouncing a verdict on an attempt requires a prior
fidelity/assessability decision — and whether deletion-aware evidence fixes the known
final-consonant false-rejection/invisibility problem. Research only; no production Unity,
VAD, router, window or scorer change.

## 2. What was run (all reproducible, scripts committed)

| Exp | What | Data | Model | Runtime |
|---|---|---|---|---|
| P1 | Fidelity/assessability rule layer v1 (literal) vs v2 (child-safe) | 49 human-labeled child tokens (1.9.9/1.9.10/1.9.11/1.9.12) + IMG stress | none (rule layer) | <5 s |
| P2 | Blank-interleaved (2L+1) deletion-aware Viterbi + deletion margin + GOP-ratio vs frozen soft-v2 | 65 child final-consonant tokens, 28 blind human-labeled | frozen `wav2vec2-xlsr-53-espeak-cv-ft` via `PhoneEvidenceV2@1.4.0` | 39.5 s CPU |
| P3 | SIAK calibration + metadata/rejected-class audit | SIAK 16,308 utt (1,074 scored: all age 4–6 + 120/age 7–10) | same frozen soft-v2 | 335 s CPU |

Baseline reproduction: `PhoneEvidenceV2.soft_match` re-run on all 65 final-consonant tokens
reproduced the archived 1.9.12 `match_type` with **0 mismatches** — the baseline comparison is
same-audio, same-code, same-model.

## 3. P1 — Fidelity / assessability: the naive question is unsafe, the conservative one is testable

Full evidence: `FIDELITY_ASSESSABILITY_RESULTS.md`, `artifacts/p1/`.

Selected human-labeled set (49 cases: 30 human-correct, 13 human-incorrect, 6 human-uncertain):

| rule set | false gate on human-correct child speech | low-score-correct → FREE_SPEAK | ASR-empty human-correct | deletion SCORER_MISS cases |
|---|---:|---:|---|---|
| v1 literal (ASR drives validity) | **8/30 (26.7%)** | **2** | 8 POSSIBLE / 4 VALID / **4 UNINTELLIGIBLE** | 1 FREE_SPEAK, 2 UNINTELLIGIBLE, 2 VALID, 1 POSSIBLE |
| v2 child-safe (ASR supporting only) | **0/30** | **0** | 12 VALID / 4 POSSIBLE, never refused | 3 VALID, 3 POSSIBLE |

Key measured facts:

- On child speech, ASR is not merely weak — it is misleading: `ASR_WRONG` transcripts include
  "I", "Right", "See?", "Fitch.", "Snake.", "Phone" on tokens human-rated CLEAR_CORRECT
  (1.9.9 evidence). Any rule that converts ASR emptiness/wrongness into validity evidence
  rejects correct children.
- **NO EVIDENCE ≠ BAD PRONUNCIATION is now measured, not just asserted:** 16/24 ASR-empty
  labeled tokens are human-correct; v1 called 4 of them UNINTELLIGIBLE and 4 more FREE_SPEAK.
- 9 human-correct tokens scored < 50 by the frozen scorer; v2 never converts them into a
  fidelity refusal, so pronunciation weakness stays in the pronunciation layer.
- IMG stress: Silero=0 segments but human Round-2 28/28 SPEECH; v2 yields
  `POSSIBLE_ATTEMPT (LOW)` with `ENERGY_PRESENT_DESPITE_VAD`, **not** `NO_SPEECH`.
  A naive VAD-only gate would have rejected every one of those verified-speech recordings.
- Human-uncertain cases (6): v2 routes 4 to POSSIBLE_ATTEMPT (LOW) and 2 to
  VALID_ASSESSABLE; it refuses none (0). Human uncertainty is visible to the layer, not erased.
- States `INCOMPLETE` and `FREE_SPEAK` remain defined but unused in v2: the current signal set
  has no confirmed free-speech/partial recordings to validate them against. Emitting them
  would be fabrication.

What P1 does **not** establish: the refusal states themselves (`NO_SPEECH`, `UNINTELLIGIBLE`)
were never validated because the corpus contains no human-labeled invalid recordings
(the Zenodo set is clean, prompted, single-word). The layer is validated for *not
false-gating*, not for *correctly gating*.

## 4. P2 — Deletion-aware evidence: representation fixed, FRR-first outcome negative

Full evidence: `DELETION_AWARE_RESULTS.md`, `FRR_ANALYSIS.md`, `artifacts/p2/`.

65 tokens / 28 blind human labels (16 present, 12 absent). Primary metric FRR on
human-present finals; secondary FAR on human-absent finals.

| method | human-present rejected (FRR) | human-absent accepted (FAR) | AUC (present ranking) |
|---|---:|---:|---:|
| A. frozen soft-v2 (as shipped) | **1/16 (6.3%)** | 5/12 (41.7%) | 0.760 |
| B. free deletion-aware Viterbi (zero penalty) | **7/16 (43.8%)** | 2/12 (16.7%) | 0.698 |
| B. deletion margin (per-frame LLR), best FRR=0 operating point | **0/16** | 6/12 (50%) | **0.839** |
| B. deletion margin, FRR=2/16 operating point | 2/16 | 5/12 (41.7%) | 0.839 |
| C. GOP-ratio (likelihood-ratio) | 1/16 at best FRR-matched point | 11/12 (91.7%) | 0.740 |
| C. mean expected posterior | 3/16 | 6/12 | 0.750 |

- The deletion-aware representation **does** represent deletion now (baseline forced alignment
  cannot): at zero penalty it rejects 10/12 absent finals; but it also rejects 7/16
  human-present finals → FRR-unsafe, a direct violation of the FRR-first rule.
- The deletion margin (score difference between "final phone present" and "final phone
  deleted" hypotheses) ranks present vs absent better than the baseline (AUC 0.84 vs 0.76)
  but **no threshold improves the FRR/FAR trade-off**: at matched FAR (5/12), margin FRR is
  2/16 vs baseline 1/16. At FRR=0 it catches 6/12 absent vs baseline's 7/12 at FRR=1/16.
- GOP-ratio does **not** outperform the CTC-span approach at any defensible operating point.
- The specific target class is still unsolved: baseline catches 1/5 human-absent /r/; the
  margin catches 2/5 at FRR=0 and 4/5 only by also rejecting present nasals. The one
  human-`PROBABLY_PRESENT` /r/ (child_04_four, LOW review confidence) has the *worst*
  GOP-ratio of all /r/ tokens — the phone model does not hear it, and no method recovers it.
- Baseline score behavior is explained but not fixed: 3 human-absent /r/ still score 66.7–100
  because forced alignment assigns the deleted phone a match. That is a pronunciation-evidence
  failure, not a fidelity failure — exactly the layer separation this phase is about.

## 5. P3 — SIAK: child calibration and the rejected-class question

Full evidence: `SIAK_CHILD_CALIBRATION.md`, `artifacts/siak/`.

Release audit (16,308 utt / 172 speakers / ages 4–12, single annotator 0–100,
license CC-BY-ND-4.0, commercial model building/eval not prohibited):

- Age 4–6 subset: **594 utt / 5 speakers** (222 unique targets) — exactly the handoff figure;
  the ages are real years from filenames, not assumed.
- Frozen soft-v2 scored on 1,074 utterances (all 4–6 + 120/age 7–10):
  Pearson **0.226** / Spearman **0.222** overall; test-split (speaker-disjoint) 0.263/0.236.
  For comparison, SIAK's own trained models reach 0.59–0.61 (E2, single annotator).
- Age gradient: 4–6 correlations 0.14–0.30; means rise with age (55→71 soft); FRR-proxy
  (SIAK ≥80 but soft <50) is **33–40% at ages 4–6** vs 11–21% at ages 7–10.
  The adult-trained pipeline rejects roughly one third of human-good 4-year-old tokens.
- Rejected/zero-rating class: **not observable in the downloaded release** (min score 1,
  zero rating-0 rows, no rejection reason). The paper documents 1,489 rejected items
  (E2); the low-score items (198 at score 1) are rated pronunciation, not rejections.
  The rejected concept therefore gives *documented* but not *empirically verifiable* support
  for an assessability layer; conflating score-1 with rejection would be retrofitting.
- Speaker leakage: official train/test splits are speaker-disjoint by ID; within-split each
  speaker repeats targets many times (train001: 420 items), so any future fine-tune must hold
  out speakers, not utterances.

## 6. Success-criteria answers

1. **Does LWE need an explicit assessability/fidelity layer?**
   *Supported* (partial): 26.7% false-gate rate of the literal rule set, 16 ASR-empty
   human-correct tokens, 9 low-score human-correct tokens, IMG VAD-negative verified speech,
   plus external consensus (E1/E2). The need is demonstrated; the gate states are not yet
   validated because our corpus lacks labeled invalid recordings.
2. **Can the layer separate the states?**
   *Demonstrated for the separations testable here* (v2: no speech/no-evidence never becomes
   wrong pronunciation; human-uncertain routed to low assessability; deletion errors stay
   pronunciation-layer problems). *Not testable* for FREE_SPEAK/INCOMPLETE/true-unintelligible
   with current data; they remain defined-but-unused.
3. **Does deletion-aware evidence reduce final-consonant false rejection?**
   *Contradicted as implemented.* Free deletion increases FRR 1/16→7/16; the margin does not
   beat the baseline at matched FRR. The representation is more faithful; the child-safe
   benefit is not shown.
4. **Does GOP-style evidence outperform the CTC-span approach without FRR increase?**
   *Not demonstrated.* GOP-ratio is worse at every FRR-matched point; the deletion margin
   ranks better (AUC 0.84) but has no improving operating point.
5. **What does SIAK tell us about child-specific assessment?**
   It confirms the gap quantitatively: zero-shot adult phone scoring correlates 0.22–0.26
   with child human scores (speaker-disjoint), fails ~1/3 of human-good 4–6yo tokens, and
   its rejected class is documented but absent from the release.
6. **Findings strong enough to become architecture requirements** (research, not production):
   - Fidelity/assessability must be an explicit layer *before* pronunciation feedback;
     ASR output must never by itself determine attempt validity.
   - `NO EVIDENCE` must remain a distinct state and must never be rendered as a pronunciation
     error.
   - FRR-first evaluation on human-confirmed child speech is mandatory for any scorer change.
   - The current phone pipeline cannot be considered a child assessor without calibration
     (SIAK numbers above).
7. **Findings that remain hypotheses:**
   - deletion-aware/blank-interleaved alignment as a *scoring* improvement;
   - GOP-ratio for final-consonant evidence on child speech;
   - thresholded deletion margin as an uncertainty signal (promising AUC, no validated
     operating point);
   - complete state taxonomy for FREE_SPEAK/INCOMPLETE;
   - SIAK-based calibration (license review pending, single annotator, 5 speakers at 4–6).

## 7. Decision

### B = RESEARCH_SUPPORTS_PARTIAL_CHANGE

Supported: separate fidelity/assessability from pronunciation; never convert no-evidence
into wrong-pronunciation; FRR-first evaluation; treat ASR as supporting evidence only.
Not supported: changing the scorer to deletion-aware/GOP evidence as a fix (no FRR-first
improvement demonstrated); locking any VAD/router/window behavior.

```
production_vad = false
router_locked = false
unity_integrated = false
scorer_modified = false
production_window_locked = false
```

## 8. Evidence levels used in this report

- **human-reviewed LWE**: 1.9.9/1.9.10/1.9.11/1.9.12 labels (single reviewer mobile; noted).
- **LWE experimental**: P1/P2/P3 runs above, reproducible from committed scripts.
- **external documented (E1/E2)**: Speechace/Chivox/Microsoft/SIAK paper, cited from 1.9.13.
- **agent inference**: marked in the docs where present (e.g., v1→v2 rationale).

## 9. Artifacts

```
Research/Speech/Phase1_9_14/
  PHASE_1_9_14_REPORT.md            this file
  FIDELITY_ASSESSABILITY_RESULTS.md
  DELETION_AWARE_RESULTS.md
  SIAK_CHILD_CALIBRATION.md
  FRR_ANALYSIS.md
  ARCHITECTURE_DELTA.md
  LICENSE_AND_DATA_NOTES.md
  CURRICULUM_AND_UX_RESEARCH.md     (target selection + child-facing output research)
  experiments/                      p1_fidelity_assessability.py, p2_deletion_aware.py,
                                    p2_compare.py, siak_calibration.py, siak_metadata.py
  artifacts/p1/                     evidence table, state matrix, v1-vs-v2 summary, cases
  artifacts/p2/                     tokens (65), summary, threshold sweep, mismatches,
                                    baseline-vs-candidates, class confusion, FRR analysis
  artifacts/siak/                   scored CSV (1,074), summary, metadata, speakers
  manifests/                        p1/p2 provenance + phase manifest (hashes)
```

Raw child audio never committed (`Research/Speech/ExternalData/` is gitignored).

## 10. Honest limitations

- Single human reviewer for all 1.9.9–1.9.12 labels (documented in those phases).
- 28-token final-consonant human set; per-class n is 5–9 — class-level conclusions are weak.
- Rule thresholds (v2) were chosen conservatively *after* seeing v1 failures; they were not
  tuned on the labels, but they are still agent-designed, not human-validated.
- No human-labeled NO_SPEECH/UNINTELLIGIBLE/FREE_SPEAK cases exist to validate refusals.
- SIAK ages 4–6 contain only 5 speakers; speaker-disjoint numbers are directional.
- Speechocean762 was not downloaded (open blocker from 1.9.13); no complementary benchmark.
