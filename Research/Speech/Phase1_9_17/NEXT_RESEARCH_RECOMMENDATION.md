# NEXT RESEARCH RECOMMENDATION — after Phase 1.9.17

**Gate:** GATE A — ALIGNMENT / DELETION IS PRIMARY BOTTLENECK
**Status:** SPEECH_RESEARCH_REQUIRES_ALIGNMENT_WORK
**Flags:** production untouched (`production_vad=false · router_locked=false ·
unity_integrated=false · scorer_modified=false · production_window_locked=false`)

---

## 1. Recommended next work package

**Name:** `WP-1.9.18 — Deletion-aware evidence & decision layer (research-only)`

Goal: design and evaluate an explicit alignment/evidence layer that can express
PRESENT / ABSENT / UNCERTAIN for word-final phones, under FRR-first, **without** touching the
production scorer and **without** training any model.

### Why this and not B2

- 4/6 human-labeled decision failures are alignment/deletion mechanisms; 0/6 are pure acoustic.
- 21/80 tokens flip pass/fail between the full and raw windows — the alignment/boundary stage
  is unstable independent of the acoustic model.
- 14/80 acceptances carry no acoustic support (posterior as low as 0.0007) — the forced
  alignment must place every target phone and the rule accepts on top-1 identity.
- B1 already showed acoustic-only adaptation cannot fix these (SCORER_MISS `four` unchanged),
  and it regressed final consonants. Training B2 before fixing the representation would make
  results uninterpretable.

### Work items

1. **Evidence layer (research-only, reuse 1.9.14 prototype):**
   blank-interleaved (2L+1) Viterbi with skip transitions; per-phone presence LLR
   (`margin`) and forced-present span posterior; no training.
2. **Deletion state:** emit `ABSENT` when `margin < 0` and span posterior < floor; emit
   `UNCERTAIN` in the overlap region; never convert absence into a pronunciation error
   (assessability/pronunciation separation preserved).
3. **FRR-first calibration of the decision:** choose the floor on speaker-disjoint data
   (SIAK/so762) so that human-present finals are preserved; report the full FRR/FAR frontier,
   including the 28 labeled finals and the `OK_UNSUPPORTED` tokens (present but unsupported).
4. **Window/boundary coupling:** decide the final-phone evidence on the deletion-aware span
   rather than the monotone-DP span; test the 21 window-flip tokens explicitly.
5. **Leave-one-out on /r/:** with only one human-present /r/ (LOW confidence), the
   present-vs-absent separability of /r/ is unresolved. Collect 10–20 confidently labeled
   child /r/ tokens (present and absent) before making any /r/ rule.
6. **Do not retrain the encoder in this work package.** After the alignment/deletion layer is
   fixed and re-measured, a separate decision about B2 can be made with honest baselines.

### Success criteria (pre-registered)

- No increase in false rejection of human-present finals vs the frozen baseline at matched
  absent-detection (the baseline is FRR 6.3% / absent detection 58.3%).
- A documented, monotone FRR/FAR frontier with explicit operating points.
- The absent-/r/ class detectable by evidence state (currently 0.20 absent detection).
- All results speaker-disjoint; LWE human labels remain external validation only.

### Stop conditions

- If no operating point beats the baseline absent detection without harming present recall on
  the 28 + new /r/ tokens, report the evidence floor as falsified and move to encoder/data
  discussion — but only with the alignment/deletion architecture excluded from doubt.

## 2. Data still needed (secondary, not blocking)

| need | why | priority |
|---|---|---|
| confidently labeled child /r/ (present + absent) | present-/r/ separability unknown (n=1 LOW) | high |
| more final-consonant tokens for young children (age 4) | LWE target population; current labeled n=28 | medium |
| Vietnamese-L1 child corpus | population calibration (still absent) | medium (later) |

## 3. What NOT to do next

- Do not train B2 / fine-tune the encoder now.
- Do not replace the production scorer/threshold or integrate into Unity.
- Do not adopt the 1.9.16 adapted head (it regressed final consonants; kept only as a
  research artifact).
- Do not treat the deletion-aware free-presence rule as a candidate scorer (FRR 7/16).

## 4. Provenance

- Diagnostic scripts: `Research/Speech/Phase1_9_17/experiments/`
- Evidence: `FAILURE_CASE_ANALYSIS.csv`, `EXPERIMENT_RESULTS.json`, `artifacts/case_evidence.csv`
- Prior supporting evidence: 1.9.12 final-consonant review, 1.9.14 P2 deletion-aware prototype,
  1.9.16 B1 A/B (final-consonant regression).
