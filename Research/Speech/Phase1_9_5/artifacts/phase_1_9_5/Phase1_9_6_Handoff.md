# Phase 1.9.6 Handoff — exact inputs

Do not re-search the filesystem. Start from this list.

## 1. IMG exact SHA (original mp3)

`E4CDF24D2B5140B91EF637676CD5FA1B9A7F6B6A629F62AE9AC2657CB171A6F7`

Path: `Research/Speech/Phase1_9_5/incoming-audio/IMG_0639.mp3`  
Also: repo root `IMG_0639.mp3`, `D:\speech-lab\data\real-audio\IMG_0639.mp3`

## 2. IMG duration

219.284 s (derived 16 kHz mono length)

## 3. Derived 16k used by detector

Path: `Research/Speech/Phase1_8/AudioDerived/IMG_0639_16k_mono.wav`  
SHA: `9A662E8639D7C9694CFD997A221C541900BE39BEA26C3B961CFC2DF3162E5BCB`

## 4. 28 hybrid segments

- Reproduced n=28 with frozen `hybrid_score` thr=0.5 margin=6  
- Package: `artifacts/phase_1_9_5/IMG_Human_Review/`  
- manifest: `manifest.json` / `manifest.csv`  
- clips: `clips/`, `clips_padded_250ms/`, `clips_padded_500ms/`

## 5. IMG human review file path

`artifacts/phase_1_9_5/IMG_Human_Review/Human_Review_Template.csv`  
**Labels empty until human fills.**

## 6. Selected long recording IDs

| ID | Duration | Original SHA-256 |
|---|---:|---|
| `IMG_0639` | 219.284 s | `E4CDF24D2B5140B91EF637676CD5FA1B9A7F6B6A629F62AE9AC2657CB171A6F7` |
| `NEW_1790` | 94.016 s | `17E3DA86267D1B7B864EA862A6C5C3B12AF4E49FD649F2FE140652A53DDF33FF` |

NEW incoming path: `incoming-audio/NEW_1790932799243.mp3`  
NEW derived: `Phase1_8/AudioDerived/NEW_16k_mono.wav` SHA `7281C131C777F5B590BC3583B771DED300434E6D09306292D8DDEF8587CCF4EE`

## 7. Long human annotation paths

- `artifacts/phase_1_9_5/Human_Annotations/recording_IMG_0639/review_template.csv`  
- `artifacts/phase_1_9_5/Human_Annotations/recording_NEW_1790/review_template.csv`  
- `artifacts/phase_1_9_5/Human_Annotations/combined_review_template.csv`  
**Labels empty until human fills.**

## 8. Frozen detector versions

- Module: `Research/Speech/Phase1_9/HybridVAD/hybrid_vad.py`  
- Commit introduced: `fd0c5f4`  
- Modes: silero / hybrid_score / energy / spectral (unchanged)  
- Defaults for rebuild: silero_thr=**0.5**, energy_margin_db=**6.0**, mode=`hybrid_score` for IMG 28

## 9. Frozen normalization / router

- No separate normalized-feature module present on this checkout (1.9.4 absent)  
- Router: **not locked**; prior 1.9.1 grid research-only  
- Do **not** retune thresholds in 1.9.6 without new human evidence

## 10. Downstream scorer status

- Do not modify scorer  
- May be blocked if `transformers` missing in env  
- Handoff policy remains: prefer **full or padded** crop; CHEATING-BY-CROP concerns frozen from prior work if/when that doc exists

## 11. Exact Gates for Phase 1.9.6 (after labels exist)

1. Load filled human labels (IMG 28 + long regions)  
2. Score hybrid/silero/energy vs **HUMAN_REFERENCE** (single-reviewer) — never call GT  
3. Router stability only if labels support multi-file generalization  
4. Downstream padded handoff check if scorer env available  
5. Decision A/B/C with human evidence  

## 12. MUST NOT change

Unity, scorer formula, CMUdict, phoneme inventory, hybrid_score equations, Silero default thr, synthetic audio as real evidence, auto human labels.

## Registry

`artifacts/phase_1_9_5/source_registry.json`
