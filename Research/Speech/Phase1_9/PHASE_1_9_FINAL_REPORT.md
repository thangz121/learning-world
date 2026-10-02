# Phase 1.9 FINAL REPORT — Hybrid VAD / real-audio front-end

Date: 2026-10-02. ASUS CPU-only.  
Frozen Phase 1.8: `9749b92`. Silero thr=**0.5** baseline reproduced.

---

## 1. Scope
Test adaptive energy/spectral front-ends **with** Silero—not replace Silero first, not lower thr globally, not damage phoneme path.

## 2. Frozen Phase 1.8 baseline (reproduced)

| File | thr | n_seg | speech_ratio |
|---|---:|---:|---:|
| IMG_0639 | 0.5 | **0** | 0.0 |
| NEW | 0.5 | **38** | **0.598** |

## 3. Dataset
- Real: IMG_0639, NEW (Phase 1.8 derived 16k mono)
- Controlled: white noise SNR 20…−5 on NEW (SYNTHETIC)
- Phoneme preservation: LWE short words (red/cat/apple/blue/big)

## 4. Human VAD reference
- **NEW:** Silero thr0.5 segments = reference (high internal consistency)
- **IMG:** no full human frame labels; **weak pseudo-ref** = energy gate margin 8 dB (labeled WEAK_PSEUDO_REF—not ground truth)

## 5–9. Strategies tested
`silero`, `energy`, `spectral`, `energy_and_silero`, `energy_or_silero`, `adaptive_then_silero`, `spectral_and_energy_then_silero`, `hybrid_score`; thr sweep 0.3–0.7 on silero-only.

### Threshold sweep on IMG (critical)
| thr | n_seg |
|---:|---:|
| 0.3–0.7 | **all 0** |

**Lowering Silero threshold does not fix IMG.**

## 10–11. Real-audio A/B (selected)

| Condition | Mode | n_seg | speech_ratio | F1 vs ref | P | R | Runtime |
|---|---|---:|---:|---:|---:|---:|---:|
| NEW | silero 0.5 | 38 | 0.598 | **1.000** (self) | 1.00 | 1.00 | ~3.8s |
| NEW | adaptive_then_silero | 34 | 0.602 | **0.997** | 0.994 | 1.00 | ~3.7s |
| NEW | hybrid_score | 38 | 0.639 | 0.966 | 0.935 | 0.999 | ~3.8s |
| NEW | energy_and_silero | 71 | 0.324 | 0.711 | 1.00 | 0.55 | ~3.7s |
| NEW | spectral alone | 8 | 0.979 | 0.746 | 0.60 | 0.98 | ~3.9s |
| IMG | silero 0.5 | **0** | 0 | 0 | 0 | 0 | ~8.6s |
| IMG | adaptive_then_silero | **0** | 0 | 0 | 0 | 0 | ~8.8s |
| IMG | energy | 75 | 0.093 | 0.65† | 0.48 | 1.00 | ~8.6s |
| IMG | energy_or_silero | 75 | 0.093 | 0.65† | 0.48 | 1.00 | ~9.0s |
| IMG | **hybrid_score** | **28** | **0.032** | **0.84†** | 0.999 | 0.73 | ~8.7s |
| IMG | spectral | 38 | 0.938 | 0.09† | 0.05 | 0.99 | ~8.8s |

† vs energy@8dB **weak pseudo-ref** (not human GT).

**Key mechanistic finding:**  
`adaptive_then_silero` / `*_then_silero` **cannot recover IMG** because they require Silero segments as the final speech set; Silero emits none.  
**Energy-relative / hybrid_score** can emit candidates on IMG without Silero positives.

## 12. Controlled noise curve (NEW, ref=clean Silero segs)

| SNR | silero F1 | adaptive_then_silero F1 | hybrid_score F1 |
|---:|---:|---:|---:|
| 20 | 0.91 | 0.91 | 0.91 |
| 10 | 0.84 | 0.78 | 0.85 |
| 5 | 0.83 | 0.66 | 0.83 |
| 0 | 0.74 | 0.32 | 0.73 |
| −5 | 0.37 | 0.16 | 0.37 |
| IMG as-is | **0 segs** | **0 segs** | **28 segs** |

Confirms Phase 1.8: white noise ≠ IMG continuous-energy collapse.  
`adaptive_then_silero` is **more fragile** under noise than silero-alone on NEW.

## 13. Pause robustness
Not fully separate suite; NEW multi-seg structure preserved by adaptive_then_silero (34 vs 38 segs, F1 0.997).

## 14. Soft/child-like
Not expanded beyond LWE short words (CPU). No child claim.

## 15. Phoneme preservation (soft-v2 on full vs crop)

| word | full | silero crop | hybrid crop | Δ sil | Δ hyb |
|---|---:|---:|---:|---:|---:|
| red | 100 | 100 | 100 | 0 | 0 |
| cat | 100 | 100 | 100 | 0 | 0 |
| apple | 75.2 | 75.0 | 75.0 | −0.2 | −0.2 |
| blue | 5.8 | 33.3 | 33.3 | +27.5 | +27.5 |
| big | 33.3 | 100 | 100 | +66.7 | +66.7 |

**Note:** Large deltas on blue/big are **crop/alignment sensitivity**, identical for silero and hybrid crops—not hybrid-specific damage. Full-file scoring path unchanged when VAD only selects time ranges for ASR; pronunciation path should prefer full utterance or padded crops.

## 16. Downstream
No score boost from ASR. VAD experiment isolated.

## 17. Runtime
NEW ~3.7–4.0 s/mode; IMG ~8.6–9.0 s/mode (file length). Hybrid overhead ≈ Silero (features cheap vs Silero cost).

## 18. Failure cases
- `adaptive_then_silero` useless when Silero empty  
- `spectral` alone → near-full speech_ratio on IMG (high FP vs structure)  
- `energy_and_silero` collapses on IMG (AND with empty Silero)  
- IMG lacks human frame GT → absolute recall on IMG is provisional  

## 19. Retained front-end configuration

### Decision: **C. SILERO_PLUS_HYBRID_FRONT_END** (refined)

| Context | Config |
|---|---|
| **Default / NEW-like contrastive speech** | **Silero thr=0.5 only** |
| **Difficult continuous-energy (IMG-like)** | **`hybrid_score`** (or energy-or-silero as research alternate) — does **not** require Silero hits |
| **Do not use as default** | Global thr < 0.5; spectral-alone; energy_and_silero; adaptive_then_silero for IMG-like |

Rationale measured:
1. Thr sweep cannot fix IMG.  
2. NEW stays excellent with silero 0.5 and adaptive_then_silero F1≈0.997.  
3. Only non-Silero-dependent gates produce IMG segments.  
4. hybrid_score on IMG: 28 segs, speech_ratio 0.032 (selective vs spectral 0.94).

## 20. Optional
- Human frame annotation on IMG subset  
- Stationary real noise library beyond white noise  
- Learned fusion weights  

## 21. Remaining gaps
- IMG human VAD labels  
- Validate hybrid_score segments by listen  
- Integrate dual-path front-end into protocol without breaking NEW  

## 22. Phase 2 recommendation
1. Protocol VAD router: if continuous_energy_ratio>0.8 and energy_contrast<0.4 → `hybrid_score` path else Silero 0.5.  
2. Human-listen validate IMG hybrid segments.  
3. Keep pronunciation scoring target-gated.  
4. Real-child pilot when consented.

---

## Acceptance checklist

| ID | Status |
|---|---|
| A Baselines frozen | **PASS** |
| B Silero reproducible | **PASS** |
| C Noise-floor strategy | **PASS** (percentile floor) |
| D Energy strategy | **PASS** |
| E Spectral strategy | **PASS** |
| F Hybrid strategies | **PASS** |
| G Threshold sweep | **PASS** (fails on IMG) |
| H Metrics beyond seg count | **PASS** (P/R/F1) |
| I Real A/B retained | **PASS** |
| J Noise curve | **PASS** |
| K Phoneme preservation checked | **PASS** (crop caveat) |
| L No silent phoneme damage as “win” | **PASS** |
| M No ASR score boost | **PASS** |
| N No child claim | **PASS** |
| O Baseline not auto-changed | **PASS** (default still silero 0.5) |

## Artifacts
- `Results/vad_baseline_v19.json`
- `Results/vad_hybrid_results.json`
- `Results/vad_noise_curve.json`
- `Results/phoneme_preservation.json`
- `Results/vad_decision.json`
