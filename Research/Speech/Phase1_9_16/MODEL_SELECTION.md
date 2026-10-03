# MODEL SELECTION — Phase 1.9.16 (no candidate selected)

**Principle (inherited from 1.9.15):** select the smallest intervention the
evidence supports. No candidate may be selected without speaker-disjoint
validation on unseen children.

---

## Candidates considered

| candidate | description | verdict |
|---|---|---|
| frozen baseline (identity) | `wav2vec2-xlsr-53-espeak-cv-ft@2c73378` + `PhoneEvidenceV2@1.4.0` | RETAINED (bit-reproduced 482/482; the reference) |
| B1: frozen encoder + trainable phone head | needs phone-level targets | **REJECTED — no targets exist** (SIAK ratings-only, LWE verdicts-only, no accessible phone-labeled child corpus) |
| B1-variant: pseudo-phone targets from frozen forced alignment | MODEL-DERIVED labels (circular) + SIAK ND training block | **REJECTED for this phase** — circularity unmeasured, license review open; recorded as future design option, not run |
| B2: partial encoder fine-tuning | needs B1 justification first | NOT REACHED (B1 unsupportable) |
| B3: full fine-tuning | needs B1/B2 justification | NOT REACHED; also not credible on CPU-only hardware |
| External phone-labeled child corpus (MyST/PF-STAR/CMU Kids) | assessed, not acquired | BLOCKED_ACCESS / WORDS_ONLY / license-unverified respectively |

## Decision

**No child model is selected because no child model was trainable on current
materials.** This is not a negative result about adaptation as a hypothesis —
it is a finding about the materials. `child_model_metrics.json` records
NOT_AVAILABLE (not zero, not failure).
