# PHASE 1.9.18 — EXECUTIVE SUMMARY

**Date:** 2026-10-04 · **Machine:** ASUS · **Branch:** `phase1-9-16-recovery`
**Base:** `d45d3ed` (Phase 1.9.17, SO762 phone tier verified)
**Type:** RESEARCH-ONLY child-phone adaptation (B1 head-only). No production change.

## Decision

### E = B1_REGRESSES_HUMAN_CORRECT_CHILD_SPEECH

## One-paragraph result

The frozen PhoneEvidenceV2@1.4.0 zero-shot baseline on SO762's 24 held-out child
test speakers scores **FRR = 0.227** (1,756 of 7,728 expert-correct child phone
tokens rejected) and **FAR = 0.271** (on only 48 expert-bad tokens — a small,
caveated denominator). B1 (frozen encoder + trainable small head, trained on the
80 child train speakers) **lowers FAR to 0.104 but RAISES FRR to 0.299**. Under
the FRR-first rule a FAR gain does not count when human-correct speech is
rejected more often, so B1 is a **regression**. B1 regresses FRR on **every**
age band (6→15) and on the majority of frequent phones, and it also lowers
confidence on adult-TTS control words — there is no evidenced domain-specific or
cross-domain gain. B2 is **not justified**.

## Key numbers

| metric | frozen baseline | B1 head-only | delta |
|---|---|---|---|
| FRR (expert-correct child phones) | 0.2272 | 0.2988 | **+0.0716 (worse)** |
| FAR (expert-bad child phones) | 0.2708 | 0.1042 | −0.1667 |
| n expert-correct tokens | 7,728 | 7,728 | — |
| n expert-bad tokens | 48 | 48 | — |
| valid AUC (B1 certificate) | — | 0.7999 | — |

## Scope limits (mandatory)

- SO762 children are **Mandarin-L1, ages 6–15**; LWE children are **Vietnamese-L1,
  ~age 4**. No LWE transfer number could be produced (raw child audio is
  gitignored) — see `09_LWE_TRANSFER.md`.
- SO762 is an **expert score tier**, not a dense observed-phone transcription;
  conventional PER / confusion matrices are **NOT_COMPUTABLE** and are not
  fabricated.
- SIAK transfer is **BLOCKED_PENDING_ND_REVIEW**.

## Artifacts

`b1_config.json`, `b1_results.json`, `b1_test_metrics.json`,
`zero_shot_baseline.json`, `phone_results.json`, `deletion_results.json`,
`historical_replay.json`, `lwe_transfer.json`, `siak_transfer.json`,
`generalization.json`, `age_l1_matrix.json`, `child_split.json`,
`provenance.json`, `decision.json`, `checkpoints/b1_head.pt`
(sha256 `44d86379…da3e6`), plus `evidence/` CSVs.
