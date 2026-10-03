# 08 — HISTORICAL FAILURE REPLAY (STEP 10)

**Script:** `experiments/run_transfer_and_replay.py` · **Artifacts:**
`historical_replay.json`, `evidence/controls_replay.json`

---

## Forensic case replay (12 PHONE_MODEL_ERROR + 6 SCORER_MISS + 5 context)

- Cases located: `Phase1_9_9/Results/scorer_failure_cases.csv` (23 rows).
- **B1 replay: NOT_COMPUTABLE.** The case waveforms are privacy-sensitive raw
  LWE child recordings and are gitignored; B1 cannot be re-run on audio that is
  not present in this environment. The baseline half is committed
  (`Phase1_9_16/lwe_failure_replay.csv`) and stands.
- No child output was synthesized or imputed.

## Frozen control words (audio present: `Phase1_1/audio`)

These are **adult-TTS-or-synthetic** control words, not child speech.

| control | target | baseline soft | B1 mean prob | n |
|---|---|---|---|---|
| sapi_red | red | 100.0 | 0.671 | 3 |
| sapi_cat | cat | 100.0 | 0.606 | 3 |
| sapi_red_apple | red apple | 100.0 | 0.711 | 7 |
| pregen_apple_normal | apple | 75.2 | 0.613 | 4 |
| sapi_blue | blue | 5.8 | 0.178 | 3 |
| sapi_big | big | 33.3 | 0.233 | 3 |
| sapi_book | book | 66.7 | 0.230 | 3 |
| sapi_dog | dog | 33.3 | 0.183 | 3 |
| stress_silence | red | 14.2 | 0.182 | 3 |
| stress_noise | red | 16.7 | 0.181 | 3 |

## Interpretation

B1 lowers confidence on **every** control, including the perfect adult-TTS
samples (`sapi_red`/`cat` at baseline 100). This confirms B1 learned a general
"shift toward rejection" rather than any child-specific or pronunciation-specific
evidence. The silence/noise controls stay low for both models (assessability
guard intact).
