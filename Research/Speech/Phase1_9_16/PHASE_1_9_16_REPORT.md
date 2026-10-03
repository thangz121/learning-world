# PHASE 1.9.16 — CHILD PHONE MODEL ADAPTATION RESEARCH (MAYNODE)

**Date:** 2026-10-03 · **Machine:** MAYNODE · **Branch:** `phase1-9-16-recovery`
**Base:** `3f62ef4` (Phase 1.9.15, decision B) · **ASUS status:** unreachable;
all ASUS-local 1.9.16 work treated as LOST_FROM_ASUS, never reconstructed.
**Flags (unchanged):** `production_vad=false · router_locked=false ·
unity_integrated=false · scorer_modified=false · production_window_locked=false`

## Decision

### D = DATA_INSUFFICIENT_FOR_CHILD_ADAPTATION

---

## 1. Mission

Determine whether a child-adapted phone model produces better phoneme evidence
for LWE children than the frozen baseline, while preserving human-correct
child speech — answerable YES / NO / INSUFFICIENT with evidence.

## 2. What was run (all non-training work completed)

| step | result | provenance |
|---|---|---|
| Material recovery + rebuild | model+SIAK+CMUdict re-acquired; SIAK byte-identical (3/3 hashes) | REDOWNLOADED, verified |
| Data audit (3,168 utt, 47 min audio) | quality OK; **phone labels: NONE anywhere** | RERUN |
| Phone inventory audit | 389/392 mapped; all control words covered; v1.4.0 frozen | RERUN |
| Speaker-disjoint splits | 2094/493/482/99, overlap [], seed 1515 inherited | REPRODUCED + re-verified |
| Zero-shot frozen baseline (482 test) | **482/482 exact reproduction**; FRR-proxy 12.24% | RERUN |
| LWE determinism check | archived vs recomputed soft 80/80 exact | RECOVERED_FROM_GIT |
| Failure replay harness (23 cases) | baseline side frozen; child side NOT_AVAILABLE → 23 INCONCLUSIVE | baseline RERUN |
| FRR/FAR, speaker (27), age, assessability, runtime, license | baseline halves complete; child halves pending | mixed, labeled per file |
| Adaptation training (B1/B2/B3) | **NOT RUN — blocked (see §3)** | — |

## 3. Why D (evidence, not assertion)

1. **No phone labels:** SIAK = expert ratings, LWE = word verdicts, CMU Kids =
   words-only, MyST/PF-STAR = restricted access. Supervised B1 has no targets;
   pseudo-labels would be circular (unmeasured). PER/confusion are
   NOT_COMPUTABLE, declared — not faked.
2. **License:** SIAK CC-BY-ND-4.0 training use still BLOCKED_PENDING_LICENSE_REVIEW
   (open since 1.9.14); even pseudo-label training on SIAK audio needs the review.
3. The hypothesis (phone evidence → CTC → soft-v2 bottleneck) is **neither
   confirmed nor refuted** — the experiment it requires cannot be built from
   current materials. D, not C: the bottleneck evidence from 1.9.15 stands
   uncontradicted, but insufficient data prevents testing the remedy.
4. Not E (no child model → no regression measurable) and not F-primary
   (runtime adequate, model license clear; license is the *second* gate).

## 4. What would unblock a rerun (precise shopping list)

1. A child corpus with a genuine phone tier (gold or independently validated),
   speaker IDs, and clear training rights — OR completed SIAK ND legal review
   plus an approved pseudo-label protocol with measured circularity.
2. Then: B1 head-only → speaker-disjoint validation → the frozen A/B harness in
   this directory executes unchanged (baseline half already frozen).

## 5. Artifacts

Reports (11): this file, DATA_AUDIT, PHONE_INVENTORY_AUDIT,
BASELINE_PHONE_EVALUATION, CHILD_ADAPTATION_RESULTS, LWE_FAILURE_REPLAY,
FRR_FAR_ANALYSIS, SPEAKER_GENERALIZATION, ASSESSABILITY_INTERACTION,
RUNTIME_AND_LICENSE, MODEL_SELECTION — plus RECOVERY_STATE,
ASUS_RECOVERY_STATUS, MAYNODE_MATERIAL_MANIFEST/AUDIT.
Machine-readable: dataset_manifest, split_manifest, baseline_metrics,
child_model_metrics (NOT_AVAILABLE), lwe_failure_replay, frr_far,
speaker_metrics, age_metrics, baseline_reproduction, provenance
(`confusion_matrix.csv` declared absent: needs phone labels on both axes).
No training was launched; no checkpoint exists; nothing was overwritten.

## 6. Honest limitations

- Single-reviewer human labels inherited from 1.9.9–1.9.15; no new human review.
- Ages 4–6: 5 speakers — no age-4 claim possible.
- LWE recomputation blocked on gitignored raw audio (used committed features).
- CPU-only: B1 feasible when unblocked; full fine-tuning needs a hardware plan.
