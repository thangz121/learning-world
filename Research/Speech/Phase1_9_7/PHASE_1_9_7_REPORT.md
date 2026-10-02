# PHASE 1.9.7 REPORT — Multi-recording Hybrid Rescue + Boundary/Merge

**Base commit:** `a720e0c` (Phase 1.9.6 decision A)  
**Date:** 2026-10-02  
**production_vad:** false | **router_locked:** false | **unity_integrated:** false

---

## 1. Executive Summary

| Axis | Finding |
|---|---|
| **NORMAL case (NEW)** | Silero thr=0.5 works well (38 segs, speech_ratio≈0.598). Hybrid tracks Silero (IoU≈0.93). **Rescue not needed** as default. |
| **STRESS case (IMG)** | Silero=0; hybrid=28; human 1.9.6 **28/28 SPEECH** (min2s). Rescue justified on this stress file. |
| **Boundary** | Raw hybrid on IMG is highly fragmented (mean≈0.25s). pad+merge (e.g. 500/500) → ~12 longer regions (mean≈2.65s). |
| **Router** | Desired pattern (NEW→Silero, IMG→hybrid) on 20/25 grid cells — but **only 2 files** → `ROUTER_NOT_CALIBRATED`. |
| **Scorer safety** | blue/big show **BOUNDARY_SENSITIVITY** under crops; prefer full or padded. Higher crop score ≠ better. |
| **Decision** | **A. HYBRID_RESCUE_SUPPORTED_FOR_RESEARCH** |

**Roles (do not invert):**
- **NEW** = PRIMARY NORMAL-CASE REFERENCE  
- **IMG_0639** = HIGH-COMPLEXITY STRESS (hosting-program) — **not** normal calibration  

Dataset limitation: **MULTI-RECORDING DATASET CURRENTLY LIMITED TO 2 RECORDINGS**.

---

## 2. Evidence Inventory

| ID | File | Dur | Role | Silero n | Hybrid n |
|---|---|---:|---|---:|---:|
| NEW_1790 | `1790932799243_…mp3` SHA `17E3DA86…` | 94.0s | **NORMAL_REFERENCE** | **38** | 38 |
| IMG_0639 | `IMG_0639.mp3` SHA `E4CDF24D…` | 219.3s | **STRESS_CASE** | **0** | **28** |

Prior features (Phase 1.9.1):

| ID | cont_energy | energy_contrast |
|---|---:|---:|
| NEW | 0.234 | 0.877 |
| IMG | 0.973 | 0.308 |

CSV: `Results/recording_inventory.csv`

---

## 3. Recording Roles

| Role | Recording | Meaning |
|---|---|---|
| NORMAL_REFERENCE | NEW_1790 | Expected operating condition / primary calibration |
| STRESS_CASE | IMG_0639 | Difficult hosting-program audio; robustness only |

Do **not** average the two into one “real-world score”.

---

## 4. Normal-Case Results (NEW)

| Mode | n_seg | speech_ratio | median_dur | frag_index |
|---|---:|---:|---:|---:|
| silero 0.5 | 38 | 0.598 | ~1.05s | 0.68 |
| hybrid_score | 38 | 0.639 | ~1.15s | 0.63 |
| energy | 79 | 0.371 | ~0.41s | 2.27 |

Silero vs hybrid overlap (10ms grid, research metric): **IoU≈0.934**

**Primary answer:** Normal audio already works with **Silero default**. Hybrid is not required as the default path on NEW.

---

## 5. Stress-Case Results (IMG)

| Mode | n_seg | speech_ratio | median_dur | frag_index |
|---|---:|---:|---:|---:|
| silero 0.5 | **0** | 0 | — | 0 |
| hybrid_score | **28** | 0.032 | ~0.22s | **3.97** |
| energy | 75 | 0.093 | 0.25s | 3.67 |

Human (Phase 1.9.6, preserved): Round2 min2s **28/28 SPEECH**; Round1 short all UNCERTAIN.

Silero vs hybrid IoU = **0** (Silero empty).

**Primary answer:** On this stress file, hybrid recovers human-verified speech candidates Silero misses — with severe fragmentation at raw boundaries.

---

## 6. Multi-Recording Comparison

```
                    NORMAL (NEW)          STRESS (IMG)
Silero              works (38)            fails (0)
Hybrid              tracks Silero         recovers 28 SPEECH
Human validation    optional this phase   28/28 SPEECH (1.9.6)
Fragmentation       moderate              severe (raw)
Need for rescue     NO (default)          YES (selective)
Need for merge/pad  optional polish       YES for usable regions
```

Architectural hypothesis remains research-only:

```
NORMAL → Silero default
DIFFICULT → selective hybrid rescue → boundary repair → safe region → scorer
```

---

## 7. Human Review

| Source | Status |
|---|---|
| IMG hybrid candidates | **Preserved** from 1.9.6 — 28/28 SPEECH @ ≥2s |
| NEW silero samples | Optional pack `HumanReview/clips_NEW_silero_min2s/` (labels not required to conclude Silero works via counts) |
| IMG post pad/merge regions | Pack `clips_IMG_post_min2s/` for optional listen of repaired regions |

No short 0.15–0.47s-only review windows introduced.

Metadata: `HumanReview/review_metadata.json`

---

## 8–9. Boundary / Merge Analysis

Compact matrix on hybrid_score base (see `boundary_merge_results.csv`).

**IMG stress (selected):**

| strategy | pad | gap | min | n | mean_dur | speech_ratio |
|---|---:|---:|---:|---:|---:|---:|
| raw | 0 | 0 | 0 | 28 | ~0.25s | 0.032 |
| pad_merge | 500 | 500 | 0 | **12** | **~2.65s** | 0.145 |
| full_chain | 500 | 500 | 500 | 12 | ~2.65s | 0.145 |
| pad_merge | 300 | 300 | 0 | 18 | ~1.28s | 0.105 |
| full_chain | 300 | 300 | 500 | 18 | ~1.28s | 0.105 |

**NEW normal:** raw hybrid already median >1s; aggressive merge reduces n (e.g. full_chain 300/300/500 → 12 long regions) — may over-merge pauses; treat carefully.

**Research recommendation (not locked):**  
For stress hybrid candidates, try **pad≥300ms + merge_gap≥300ms** before human/scorer use.  
Raw tight crops remain **unsafe**.

---

## 10. Pronunciation Crop Safety

Soft-v2 vs full utterance (Phase 1.6-style controls):

| word | full | silero rawΔ | silero pad250Δ | hybrid rawΔ | mid40Δ | flag |
|---|---:|---:|---:|---:|---:|---|
| red | 100 | 0 | 0 | 0 | -33.3 | mid crop sensitive |
| cat | 100 | 0 | 0 | 0 | -33.3 | mid crop sensitive |
| apple | 75.2 | -0.1 | 0 | -0.1 | -0.1 | stable |
| blue | 5.8 | **+60.9** | +35.9 | +0.1 | -5.8 | **BOUNDARY_SENSITIVITY** |
| big | 33.3 | **+33.4** | +33.4 | +33.5 | 0 | **BOUNDARY_SENSITIVITY** |
| silence | low | — | — | — | — | control |

Interpretation: large positive deltas on blue/big are **crop artifacts**, not pronunciation gains.  
**Prefer full utterance or padded regions; never claim crop-improved pronunciation.**

---

## 11. Router Research

Hypothesis (unlocked): `cont > C AND contrast < X → hybrid else silero`

Grid C∈{0.7…0.9}, X∈{0.3…0.5} (25 cells):

- Desired (NEW→silero, IMG→hybrid): **20/25** cells  
- Status: **`ROUTER_NOT_CALIBRATED`** (n_recordings=2, overfit risk)  
- **Do not lock** C=0.8, X=0.4

---

## 12. What Is Proven

1. On **normal** NEW, Silero 0.5 is a viable default; hybrid ≈ Silero.  
2. On **stress** IMG, Silero fails; hybrid yields candidates human-verified as SPEECH (1.9.6).  
3. Raw hybrid boundaries on IMG are too short/fragmented for comfortable listen/word ID.  
4. Pad+merge research post-process can form longer regions (research only).  
5. Pronunciation scores move under tight crops (boundary sensitivity).

---

## 13. What Is NOT Proven

1. Production VAD / router / Unity path.  
2. Generalization beyond **2** real long files.  
3. Full-file recall on IMG (still candidate-level).  
4. Optimal pad/gap/min constants.  
5. Child-mic population performance (IMG ≠ normal child mic).  
6. That hybrid should replace Silero on normal audio.

---

## 14. Decision

### **A. HYBRID_RESCUE_SUPPORTED_FOR_RESEARCH**

```
production_vad = false
router_locked = false
unity_integrated = false
```

Architecture under research:

```
NORMAL AUDIO  → Silero (default)
DIFFICULT     → selective hybrid rescue → boundary repair → safe region → scorer
```

---

## 15. Exact Next Step

**Phase 1.9.8 (suggested):** acquire **≥3 additional real normal-case** recordings (not hosting-program stress) + optional second stress file; re-test router sensitivity; human-validate pad/merge regions on stress; keep scorer on full/padded only.

Until then: **do not** lower Silero thr globally, **do not** lock router, **do not** Unity-integrate.

---

## Artifacts

| Path | Content |
|---|---|
| `Results/recording_inventory.csv` | inventory |
| `Results/normal_case_results.csv` | NEW detectors |
| `Results/stress_case_results.csv` | IMG detectors |
| `Results/boundary_merge_results.csv` | pad/merge matrix |
| `Results/router_research_results.csv` | C/X grid |
| `Results/pronunciation_crop_safety.csv` | soft-v2 crop deltas |
| `Results/human_review_results.csv` | 1.9.6 preserved + optional |
| `Results/phase_1_9_7_master.json` | machine summary |
| `Results/decision.json` | decision A |
| `HumanReview/review_metadata.json` | review packs |
