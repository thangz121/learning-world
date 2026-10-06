# SESSION REPORT — WP-1.9.19 (BOUNDARY / WINDOW CAUSALITY AUDIT)

> **Tóm tắt (VI):** Kiểm tra nhân quả window/boundary trên cùng audio, 11 cửa sổ pre-declared
> (full/raw/pad50-500/right/left/shift100) × 80 token LWE + so762 speaker-disjoint. Kết quả:
> present tokens (16) → **ALIGNMENT 9, WINDOW 3, ENCODER 3, MIXED 1**; absent (12) → STABLE_ABSENT 10,
> FALSE_GAIN 1, CONFLICT 1. Boundary control cứu **3/16** present; **3/16 vẫn không evidence trong
> mọi window** (child_01_ten, child_06_six, child_07_one); absent tăng giả 1/12; 6/21 inversion giải
> thích bằng evidence phụ âm cuối. **NEXT GATE = MIXED_WINDOW_AND_ACOUSTIC; B2 = NOT YET**.
> Không commit/push.

- **Date:** 2026-10-03
- **Machine:** ASUS
- **Base:** WP-1.9.18 (`c611724` = main tip)
- **Branch:** `ux/math-arenas-hotfix-20260930` (tip = main; không chuyển nhánh)
- **Scope:** research-only; no production/model/training/Unity change; **không commit**
- **Flags:** all five false.
- **HF token:** set via env `HF_TOKEN` (user-level) only — not stored in the repo.

## 1. Pipeline reconstruction (from source)
`Phase1_9_11/Scripts/run_window_ab.py` + `Phase1_9/HybridVAD/hybrid_vad.py` (`9872608B…`):
```
source wav → load_mono16 (16 kHz) → Silero VAD thr 0.5 → segments
→ raw = [segs[0].start, segs[-1].end] → padded = raw ± pad
→ 16 kHz wav → frozen logits → ctc_align (span forced per phone) → soft_match decision
```
Boundary-sensitive points: VAD union, pad width, crop context, monotone span allocation, top-1
acceptance. The experiment varies only the supplied context (same model/target).

## 2. Experimental design
- Window family (pre-declared; 20 ms frames): `full`, `raw`, `pad50/100/250/500`, `right100/right250`,
  `left100`, `shift_left100/shift_right100` (raw length translated ±100 ms).
- Scripts: `experiments/window_causality.py` (evidence per window), `analyze_windows.py` (stability,
  causal classes, separation, negative check, aggregates), `final_metrics.py` (inversions, rates),
  `run_absent_dev.py` (absent-enriched dev), `_inspect_lwe.py`.
- Per window recorded: audio start/end/duration, VAD bounds, left/right context, target frame +
  absolute time, span, span posterior, frame_max, top-1 phone/posterior, margin, deletion-aware
  evidence `E`, baseline + research decisions.
- Corpora: LWE 80 tokens × 11 windows (880 rows); so762 dev/test 24+24 speakers × 6 windows
  (234/227 finals); so762 absent-dev (22 utt prone to score<0.5 finals, 68 tokens: 51 present /
  17 absent) × 6 windows; dev×test speaker overlap = 0; LWE external.

## 3. Per-window separation (LWE 28, E floor 0.30)
| window | AUC | present recall | absent-FP | absent-det |
|---|---:|---:|---:|---:|
| full | 0.750 | 0.625 | 0.083 | 0.917 |
| raw | **0.880** | 0.563 | 0.083 | 0.917 |
| pad50 | 0.844 | 0.563 | **0.000** | **1.000** |
| pad100 | 0.818 | 0.688 | 0.167 | 0.833 |
| pad250 | 0.828 | **0.750** | 0.083 | 0.917 |
| pad500 | 0.755 | 0.625 | 0.083 | 0.917 |
| right100/250 | 0.862/0.867 | 0.563 | 0.083 | 0.917 |
| left100 | 0.807 | **0.750** | 0.167 | 0.833 |
| shift_left/right100 | 0.779/0.828 | 0.500 | 0.167/0.083 | 0.833/0.917 |
| production baseline | — | **0.938** | **0.417** | 0.583 |

- Dev-selected window (absent-enriched dev, best AUC): `pad100` (dev AUC 0.7918) — **no transfer win**
  on LWE (0.688/0.167 vs full 0.625/0.083); no window keeps production recall.

## 4. Causal classification (LWE labeled)
- Present (16): **ALIGNMENT 9, WINDOW 3, ENCODER 3, MIXED 1**.
  - WINDOW: child_01_seven (0.059→0.504 pad250), child_02_ten (0.066→0.431), child_04_four
    (0.003→0.304 raw / 0.479 right250).
  - ENCODER (no evidence in 11 windows): child_01_ten (max 0.045), child_06_six (max 0.009),
    child_07_one (max 0.091 — the raw-window score 72.5 did **not** recover /n/ evidence).
  - ALIGNMENT: full-context E ≥0.3 but production span posterior <0.05 (child_01_nine: 0.979 vs
    0.0011).
- Absent (12): **STABLE_ABSENT 10**, ABSENT_FALSE_GAIN 1 (child_03_six: raw 0.001 → pad100 0.919),
  ABSENT_CONFLICT 1 (child_07_seven: 0.63 at full vs human ABSENT).

## 5. Stability + inversions + negative check
- Window-sensitive (evidence range ≥0.30 or ≥2 decision flips): **LWE labeled 60.7%**, all LWE 50.0%,
  so762 test 25.1%, dev 18.4%, absent-dev 19.1%. Alignment-sensitive (strict frame position) 7.1%.
- 1.9.17 inversions (21/80 score flips): **6/21 explained by final-phone evidence** (03_six, 04_five,
  04_four, 05_three, 08_three, 09_six); the other 15 have stable final-phone evidence (score flips
  from other phones/aggregation).
- Negative check: present recovered by context **3/16** vs absent falsely gained **1/12**; mean ΔE
  0.214 (present) vs 0.132 (absent) → context gains are larger for present but not absent-free.

## 6. Final-consonant aggregation (LWE 28)
| phone | tokens | recall full→best | absent-det full→best | var | encoder-gap |
|---|---:|---|---|---:|---:|
| /t/ | 8 | 1.00→1.00 | 1.00→1.00 | 0.384 | 0% |
| /v/ | 3 | 1.00→1.00 | 1.00→1.00 | 0.196 | 0% |
| /n/ | 9 | 0.50→0.75 | 0.00→0.00 | 0.373 | 22% |
| /s/ | 2 | 0.00→0.00 | 1.00→0.00 | 0.464 | 50% |
| /r/ | 6 | 0.00→**1.00** | 1.00→1.00 | 0.137 | 0% |

- **/r/**: the single present /r/ (LOW confidence) is recovered by context (raw 0.304, right250 0.479);
  all 5 absent /r/ stay sub-floor in every window → /r/ remains **unresolved due to insufficient
  confidently-labeled present examples (n=1)** per spec.

## 7. Gate + B2 assessment
**NEXT GATE = MIXED_WINDOW_AND_ACOUSTIC**
- Window/context real: 3/16 present recovered; 6/21 inversions explained; 1 absent false-gain.
- Encoder limitation survives for a minority: 3/16 present with no evidence under any of 11 windows.
- Alignment dominates the present-token failures: 9/16.
**B2 scientifically justified: NOT YET** — fix span allocation (alignment) and collect more
confidently-labeled present finals under a fixed boundary protocol first; no B2 training here.

## 8. Artifacts (uncommitted)
```
Research/Speech/Phase1_9_19/
  BOUNDARY_WINDOW_CAUSALITY_REPORT.md, WINDOW_EXPERIMENT_RESULTS.csv (1,188 rows),
  WINDOW_STABILITY.csv, FINAL_CONSONANT_WINDOW_ANALYSIS.csv, EXPERIMENT_RESULTS.json,
  NEXT_GATE_DECISION.md
  experiments/ (window_causality.py, analyze_windows.py, final_metrics.py, run_absent_dev.py,
  _inspect_lwe.py), artifacts/ (window_rows_lwe/so762_dev/so762_test/so762_absent_dev,
  inversion_explained.csv)
```

## 9. Git
- **No commit, no push** (work package rule). Production hashes unchanged
  (`phone_evidence_v2.py 87254B7B…`, `inventory.py 97D07C12…`, `hybrid_vad.py 9872608B…`).
- Speech Research status: **NOT COMPLETE**.
