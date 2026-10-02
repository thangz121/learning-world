# Baseline Reproduction — Phase 1.9.1 Gate 1

Date: 2026-10-02  
Git HEAD at run: see commit after this phase  
Phase 1.9 code: `fd0c5f4` / HybridVAD module unchanged for this repro  

## Inputs
| File | Path | SHA-256 (16k mono derived) |
|---|---|---|
| IMG_0639 | `Research/Speech/Phase1_8/AudioDerived/IMG_0639_16k_mono.wav` | `9A662E8639D7C9694CFD997A221C541900BE39BEA26C3B961CFC2DF3162E5BCB` |
| NEW | `Research/Speech/Phase1_8/AudioDerived/NEW_16k_mono.wav` | `7281C131C777F5B590BC3583B771DED300434E6D09306292D8DDEF8587CCF4EE` |

## Config
- detector: `Research.Speech.Phase1_9.HybridVAD.hybrid_vad.HybridVAD`
- silero_thr: **0.5**
- energy_margin_db: 6.0
- reference_type for counts: **NO_REFERENCE** (repro of counts only)

## Results (reproduced)

| File | Mode | n_segments | speech_ratio | Expected (P1.9) | Match |
|---|---|---:|---:|---:|---|
| IMG | silero | 0 | 0.0 | 0 | OK |
| IMG | energy | 75 | 0.093 | 75 | OK |
| IMG | hybrid_score | 28 | 0.032 | 28 | OK |
| IMG | adaptive_then_silero | 0 | 0.0 | 0 | OK |
| NEW | silero | 38 | 0.598 | 38 | OK |
| NEW | adaptive_then_silero | 34 | 0.602 | 34 | OK |

**ALL_MATCH = True** → Gate 1 PASS. Proceed to Gate 2.

Artifact: `Results/baseline_results.json`
