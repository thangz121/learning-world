# PHASE 1.9.8 REPORT — Real ~4yo Child Speech Corpus + VAD/Pronunciation Validation

**Base:** Phase 1.9.7 `a81b3ff`  
**Date:** 2026-10-03  
**production_vad:** false · **router_locked:** false · **unity_integrated:** false  
**asr_is_not_pronunciation_judge:** true

---

## 1. Executive Summary

| Axis | Result |
|---|---|
| Primary dataset | Zenodo **200495** Children speech recording (English) |
| License | **CC-BY-4.0** (open; attribution required) |
| Children | **11** (dataset mean age **M=4.9y**; **individual ages UNKNOWN**) |
| Inventory | **671** WAV; **417** targeted; **254** spontaneous; **~88 min** |
| Silero on child | Works on most files: targeted zero-seg rate **~3.3%** (5/150 studio-eval) |
| Hybrid vs Silero | Similar zero rates; hybrid does **not** dominate as rescue on this set |
| Router C=0.8/X=0.4 | Routes **0%** of evaluated child files to hybrid |
| Pronunciation | 80 number tokens scored; mean soft **~63.9** vs CMUdict; **27.5%** &lt;50 |
| Decision | **A. REAL_CHILD_VALIDATION_SUPPORTS_CURRENT_PIPELINE** (VAD default path) |

**Critical role reminder:** NEW_1790 = normal adult/general reference; IMG_0639 = high-complexity stress; **child speech = new population, not automatically stress**.

Child realization is **evidence of realistic acoustics**, **not** canonical gold pronunciation (CMUdict remains target).

---

## 2. Research Question

Does the frozen Silero/hybrid VAD + Phase 1.6 pronunciation pipeline behave acceptably on real speech from children around age ~4–5, vs only adult/test audio?

---

## 3. Dataset Sources

| ID | Source | Action |
|---|---|---|
| **zenodo_200495** | https://doi.org/10.5281/zenodo.200495 | **Downloaded + run** |
| CHILDES-Aligned | talkbank.org | Audited only — access terms; not downloaded |
| CHILDES Hall | talkbank.org | Audited only |
| OCSC | public versions unclear | Not downloaded |

Zenodo content (from official metadata + local files):
- 11 children; mean age M=4.9; 5F/6M (sex in folder names only)
- Free speech (Frog story retell) + 5 predefined sentences + numbers 1–10
- Mics: studio / portable / NAO robot
- Manual free-speech sentence cuts
- English; native + non-native speakers

---

## 4. License / Usage Audit

| Field | zenodo_200495 |
|---|---|
| license | **CC-BY-4.0** |
| research_use | yes |
| commercial_use | yes **with attribution** |
| redistribution | yes under CC-BY-4.0 |
| download | open |
| product embed | **legal review still required** before shipping audio or derivatives |
| usage_class | POTENTIALLY_PRODUCT_COMPATIBLE_WITH_ATTRIBUTION / prefer RESEARCH_ONLY until counsel OK |
| Git | **raw child audio NOT committed** (`Research/Speech/ExternalData/`) |

---

## 5. Child Age Distribution

| Bucket | n_speakers | note |
|---|---:|---|
| 4.x years (dataset mean) | 11 | **No per-child ages in package** |
| 3.x / 5.x / 6.x | 0 labeled | — |
| UNKNOWN individual | 11 | age_source = dataset_mean_M=4.9y_only |

**Never inferred age from voice.**

---

## 6. Speaker Distribution

Anonymous IDs `child_01`…`child_11` from folder names `NN_{M\|F}_{native\|nonNative}`.

Per-child recording counts and durations: `Results/speaker_inventory.csv`.  
No single-child dominance forced into a fake “accuracy%”.

---

## 7. Recording Inventory

| Category | n |
|---:|
| TARGETED_CHILD_SPEECH | 417 |
| TRANSCRIBED_SPONTANEOUS | 254 |
| Total inventoried | 671 |
| Total duration | ~87.9 min |

CSV: `recording_inventory.csv`

---

## 8. Normal Child Speech Characteristics

- Short targeted numbers/sentences + spontaneous retell  
- Multiple mics (studio preferred for eval)  
- Native and non-native English children  
- Treated as **CHILD_NORMAL** population, not IMG-like stress  

---

## 9. Silero VAD Results (thr=0.5 frozen)

| Population | pct zero segs | mean n_seg (eval set) |
|---|---:|---:|
| Child overall (eval) | **2.6%** | ~3.6 |
| Child targeted studio subset | **3.3%** (5/150) | — |
| NEW_1790 baseline | 0% (38 segs) | 38 |
| IMG_0639 stress | **100%** zero (0 segs) | 0 |

**Answer:** On this ~4.9y mean child set, Silero **rarely misses entire files**. Failure mode of IMG (total collapse) is **not** the typical child pattern here.

---

## 10. Hybrid VAD Results

| Population | pct zero | mean n |
|---|---:|---:|
| Child overall | **2.6%** | ~3.2 |
| Child vs Silero | similar zeros; hybrid often fewer segs on long free speech |

**Answer:** Hybrid does **not** show large systematic rescue benefit on this child set the way it did on IMG. Sometimes slightly more merge-like behavior (fewer segs).

---

## 11. Human Review

- Disagreement pack built when Silero empty & hybrid positive or large n-diff  
- This run produced **1** auto disagreement clip (≥2s window) — Silero/hybrid largely agree  
- Labels left empty for optional human fill: `HumanReview/review_min2s.html`  
- Phase 1.9.6 IMG 28/28 SPEECH evidence **preserved**, not overwritten  

---

## 12. Child VAD Failure Modes

| Question | Evidence on this dataset |
|---|---|
| A Silero miss child speech? | Rare whole-file miss (~3% targeted) |
| B Fragmentation? | Present on free speech; not IMG-level |
| C Excess FP? | Not human-quantified this phase |
| D Pitch instability? | No dedicated F0 model; not proven |
| E Quiet miss? | Not isolated |
| F Rapid boundary issues? | Possible; crop tests show boundary sensitivity on some tokens |
| G Many short segs spontaneous? | Yes on free retell (expected) |
| H Hybrid rescue help? | **Not primarily needed** vs Silero here |

---

## 13. Boundary Analysis

- Number tokens: some Silero crops empty (`soft_silero_raw=None`) while full-file scores exist → **use full or pad**, not raw tight crop  
- Large |Δ| full vs crop on several items → **BOUNDARY_SENSITIVITY** (same lesson as Phase 1.9.7 adult LWE)  

---

## 14. Pronunciation Scoring Results

Pipeline: frozen soft-v2 + CMUdict; **80** studio number utterances (cap).

| Metric | Value |
|---|---:|
| mean soft_full | **63.9** |
| frac soft &lt; 50 | **27.5%** |
| conf tracked separately | yes |

Per-child mean soft (approx): child_05 ~75; child_07 ~53; child_08 ~54 (higher low-score rate).

**Interpretation rules applied:**
- Low score ≠ proven “wrong phoneme” without human phone review  
- Child audio ≠ gold pronunciation  
- SCORE ≠ CONFIDENCE  

---

## 15. Child Speaker-Invariance Analysis

**Not proven** that F0/formants alone caused low scores (no controlled pitch-normalized A/B).  
**Observed:** wide score spread on same targets across children; some tokens (e.g. “two”, “eight”) often low — may be articulation, mic, or model mismatch — **needs phone-level human audit**, not a forced “add pitch norm” claim.

---

## 16. ASR vs Phone Evidence

ASR not used as judge this phase (`asr_is_not_pronunciation_judge=true`). No automatic boost/fail from ASR.

---

## 17. Child Acoustic Analysis

Proxies logged: continuous_energy_ratio, energy_contrast, rms_mean, zcr_proxy.  
Router features on child files rarely match IMG-like (high cont + low contrast) pattern.

CSV: `child_acoustic_results.csv`

---

## 18. Router Research

Hypothesis C=0.8, X=0.4 on evaluated child rows: **route hybrid frac ≈ 0**.  
Status: **`CHILD_ROUTER_NOT_CALIBRATED`** / fits “leave child on Silero” more than “always hybrid”.  
Do **not** lock.

---

## 19. Comparison

| Group | VAD Silero | Hybrid role | Pronunciation |
|---|---|---|---|
| NEW_1790 | Strong (38 segs) | Tracks Silero | N/A adult long |
| IMG_0639 | **Fails (0)** | Rescue 28 SPEECH (1.9.6) | N/A |
| REAL_CHILD zenodo | **Usually works** | Marginal vs Silero | Variable vs CMUdict |

---

## 20. What Is Proven

1. Usable open child English set (~4.9y mean) under CC-BY-4.0 can be run through frozen pipeline.  
2. Silero 0.5 detects speech on most child targeted/spontaneous files here.  
3. IMG total Silero failure is **not** representative of this child set.  
4. Hybrid is not required as default for this child population.  
5. Pronunciation scores vs CMUdict vary widely; padding/full safer than empty/raw crops.  

---

## 21. What Is NOT Proven

1. Per-child exact ages (unknown).  
2. Production readiness / product license clearance.  
3. That scorer is fair to child vocal tracts (no controlled invariance test).  
4. Phoneme-level error taxonomy with human listeners.  
5. CHILDES/OCSC results (not downloaded).  
6. Router constants for children.  

---

## 22. Limitations

- Mean age only; bucket is approximate  
- Numbers/sentences ≠ LWE game vocabulary fully  
- Single soft-v2 scorer path  
- Human review of child disagreement mostly optional (few auto-disagreements)  
- Speaker-disjoint train/test N/A (no training this phase)  
- External audio gitignored  

---

## 23. Decision

### **A. REAL_CHILD_VALIDATION_SUPPORTS_CURRENT_PIPELINE**

Meaning: **VAD default Silero remains reasonable** for this child set; hybrid stays research rescue for genuine stress (e.g. IMG), not child-default.

```
production_vad = false
router_locked = false
unity_integrated = false
```

**Caveat:** Pronunciation layer still needs child-aware **evaluation and possible future research** (score spread); that does **not** by itself flip VAD architecture this phase. If product requires child-norm guarantees, follow with targeted phone human study (next step).

---

## 24. Exact Next Step

**Phase 1.9.9 (suggested):** Human phone-level audit on low-scoring child number/sentence tokens + LWE vocabulary recorded by 4yo if available; keep Silero default; keep hybrid stress-only; legal review before any product use of Zenodo audio.

---

## Artifacts

All under `Research/Speech/Phase1_9_8/Results/` and `HumanReview/`.  
Raw audio: `Research/Speech/ExternalData/zenodo_200495/` (gitignored).
