# 09 — LWE TRANSFER TEST (STEP 11) — the critical gate

**Script:** `experiments/run_transfer_and_replay.py` · **Artifact:** `lwe_transfer.json`

---

## Status: LWE_TRANSFER_BLOCKED_RAW_AUDIO_GITIGNORED

Real-child LWE waveforms are privacy-sensitive and deliberately not committed
(`.gitignore`: `Research/Speech/**/real-child/`, `real-audio/`). Only the frozen
per-token soft scores + human verdicts are in git
(`Phase1_9_15/artifacts/external/external_lwe_features.csv`, 80 tokens, 9
speakers). **B1 cannot be evaluated on this audio in this environment**, so the
cross-domain number is **NOT_COMPUTABLE** — not invented.

## Pre-registered frozen-baseline reference (the number transfer must beat)

| metric | value |
|---|---|
| n human-correct LWE tokens | 29 |
| frozen baseline human-correct **low-score rate** (soft < 50) | **0.3103** |
| n human-incorrect LWE tokens | 13 |
| frozen baseline human-incorrect high-score rate (soft ≥ 50) | 0.3846 |

## Why this gate matters

B1 was trained on **Mandarin-L1 children ages 6–15**. LWE is **Vietnamese-L1
children ≈age 4**. Per the phase rules, transfer must be tested, never assumed.
Because the primary success metric is FRR on human-correct child speech, the LWE
transfer test is the decisive cross-domain check — and it **could not be run
here**. Therefore this phase cannot make any LWE improvement claim, regardless
of the SO762 result.

## To unblock

Re-run B1 inference against the privacy-sensitive LWE audio on a machine that
holds it (e.g. MAYNODE local `ExternalData/zenodo_200495` + the 1.9.9–1.9.12
clips), using the same frozen encoder + alignment + B1 head and the SAME
per-token protocol. The harness is ready (`run_b1_eval.py` scoring path).
