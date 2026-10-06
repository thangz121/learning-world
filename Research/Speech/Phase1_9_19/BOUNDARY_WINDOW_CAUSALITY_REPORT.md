# BOUNDARY / WINDOW CAUSALITY AUDIT — WP-1.9.19

**Machine:** ASUS · **Branch tip used:** `c611724` (= main tip; branch not switched)
**Scope:** research-only. No production change, no model change, no training, no Unity.
**Flags:** `production_vad=false · router_locked=false · unity_integrated=false ·
scorer_modified=false · production_window_locked=false`

**Gate:** **MIXED_WINDOW_AND_ACOUSTIC** (see `NEXT_GATE_DECISION.md`)

---

## 1. Research question

Are the missing final-phone evidence cases caused by the frozen encoder itself, or by the
audio window / VAD boundary / context supplied to it? Causal separation of
WINDOW / ALIGNMENT / ENCODER / MIXED.

## 2. Existing window pipeline (reconstructed from source, not reports)

`Research/Speech/Phase1_9_11/Scripts/run_window_ab.py` (frozen scorer) +
`Research/Speech/Phase1_9/HybridVAD/hybrid_vad.py`:

```
source audio (44.1 kHz wav)
→ load_mono16: mono, linear resample to 16 kHz   [boundary-neutral]
→ Silero VAD thr 0.5 via HybridVAD(mode="silero") → segments
→ raw window   = [segs[0].start, segs[-1].end]
→ padded window= raw ± pad (100/200/250/300/500 ms in 1.9.11)
→ 16 kHz wav → frozen PhoneEvidenceV2.logits → probs [T,392]
→ ctc_align (monotone DP, every target phone gets a span)
→ soft_match decision (post ≥ .25/.35 or top-1 identity; best_sim ≥ .35)
→ final-phone evidence = class posterior over the assigned span
```

Boundary-sensitive points in that chain: VAD segment union (a), pad width (b), any crop
that moves phonetic/blank context (c), the monotone span allocation (d; evidence can be
assigned to the wrong frames), and the acceptance rule (e; top-1 identity can accept a
near-empty span). WP-1.9.19 varies only (a)–(c) by cropping the same audio; (d)–(e) are
measured through alignment-free and deletion-aware evidence.

## 3. Controlled window family (pre-declared)

At 20 ms CTC frames, 50/100/250/500 ms = 2.5 / 5 / 12.5 / 25 frames:
`full` (whole recording), `raw` (VAD union, 1.9.11 construction), `pad50/100/250/500`
(symmetric), `right100/right250` (right context only), `left100` (left only),
`shift_left100/shift_right100` (raw length translated ±100 ms, clamped).
LWE tokens: all 11; so762: 6 (`full, raw, pad100, pad250, pad500, right250`).
No window was chosen by score; selection used dev evidence only (below).

## 4. Data

- LWE: 80 tokens (9 children) × 11 windows = 880 rows; 28 blind final labels
  (16 present / 12 absent) from 1.9.12. Baseline reproduction unchanged.
- so762 dev: 24 train-child speakers, 96 utt × 6 windows (234 final phones; the sample
  happens to contain no score-0 finals → recall/stability only).
- so762 absent-dev (dev-only, speaker-disjoint): 22 utterances containing word-final
  score-<0.5 consonants, 68 final tokens (51 present / **17 absent**) × 6 windows.
- so762 test (held out): 24 test-child speakers, 96 utt × 6 windows (227 final phones).
- Speakers: dev ∩ test = ∅; LWE external never used for selection.

## 5. Per-window separation on the LWE 28 blind labels (E floor 0.30)

| window | AUC (present vs absent) | present recall | absent false-present | absent detection |
|---|---:|---:|---:|---:|
| full | 0.750 | 0.625 | 0.083 | 0.917 |
| raw | **0.880** | 0.563 | 0.083 | 0.917 |
| pad50 | 0.844 | 0.563 | **0.000** | **1.000** |
| pad100 | 0.818 | 0.688 | 0.167 | 0.833 |
| pad250 | 0.828 | **0.750** | 0.083 | 0.917 |
| pad500 | 0.755 | 0.625 | 0.083 | 0.917 |
| right100 | 0.862 | 0.563 | 0.083 | 0.917 |
| right250 | 0.867 | 0.563 | 0.083 | 0.917 |
| left100 | 0.807 | **0.750** | 0.167 | 0.833 |
| shift_left100 | 0.779 | 0.500 | 0.167 | 0.833 |
| shift_right100 | 0.828 | 0.500 | 0.083 | 0.917 |
| baseline production (soft-v2, 1.9.12) | — | **0.938** | **0.417** | 0.583 |

Window selection on the speaker-disjoint dev (absent-dev): best AUC = `pad100`
(0.7918); all dev windows are within 0.77–0.79. Applied to held-out LWE, `pad100` gives
recall 0.688 / absent-FP 0.167 — **no transfer win**; the best-separating window on LWE
(`raw`, AUC 0.880) still has recall 0.563. No window keeps production recall (0.938).

## 6. Failure replay (known cases) across all windows

| token | human | root cause | E full | E raw | max E | notes |
|---|---|---|---|---:|---:|---|
| child_07_one /n/ | PRESENT | **ENCODER** | 0.001 | 0.059 | 0.091 | raw-window *score* 72.5 did **not** recover /n/ evidence |
| child_01_ten /n/ | PRESENT | **ENCODER** | 0.029 | 0.029 | 0.045 | no evidence in any of 11 windows |
| child_06_six /s/ | PRESENT | **ENCODER** | 0.003 | 0.001 | 0.009 | ditto |
| child_01_seven /n/ | PRESENT | **WINDOW** | 0.059 | 0.090 | **0.504** | recovered at pad250 |
| child_02_ten /n/ | PRESENT | **WINDOW** | 0.066 | 0.295 | **0.431** | recovered at pad100/left100 |
| child_04_four /r/ | PRESENT (LOW) | **WINDOW** | 0.003 | **0.304** | **0.479** | context recovers /r/ |
| child_01_nine /n/ | PROB. PRESENT | **ALIGNMENT** | **0.979** | 0.978 | 0.984 | production span posterior 0.0011; evidence present |
| child_01_four /r/ | ABSENT | STABLE_ABSENT | 0.022 | 0.018 | 0.030 | deletion robust to boundaries |
| child_03_four /r/ | ABSENT | STABLE_ABSENT | 0.056 | 0.004 | 0.056 | ditto |
| child_06_four /r/ | ABSENT | STABLE_ABSENT | 0.041 | 0.007 | 0.063 | ditto |
| child_02_four /r/ | ABSENT | STABLE_ABSENT | 0.154 | 0.063 | 0.154 | weak but sub-floor everywhere |
| child_07_seven /n/ | ABSENT | ABSENT_CONFLICT | 0.629 | 0.504 | 0.715 | strong evidence vs human ABSENT in every window |

Present-token causal distribution (16): **ALIGNMENT 9, WINDOW 3, ENCODER 3, MIXED 1**.
Absent-token distribution (12): **STABLE_ABSENT 10, FALSE_GAIN 1 (child_03_six),
CONFLICT 1 (child_07_seven)**.

## 7. Boundary stability and the 21/80 score inversions

- Window-sensitive tokens (evidence range ≥0.30 or ≥2 decision flips): LWE labeled
  60.7%, all LWE 50%, so762 test 25.1%, so762 dev 18.4%, absent-dev 19.1%.
- Alignment-sensitive (target-frame position flips >100 ms while evidence ≥0.3): LWE
  labeled 7.1% by the strict position rule; the causal ALIGNMENT class (span misses
  full-context evidence) is 9/16 present tokens.
- **Inversions explained by final-phone evidence: 6/21** (`child_03_six`,
  `child_04_five`, `child_04_four`, `child_05_three`, `child_08_three`, `child_09_six`).
  The other 15 inversion tokens have stable final-phone evidence across windows; their
  score flips come from other phones in the word or from score aggregation, not from the
  final consonant.

## 8. Negative check (does context create evidence on absent phones?)

| corpus | present recovered by context | absent falsely gained by context | mean ΔE (present / absent) |
|---|---:|---:|---|
| LWE | 3/16 | 1/12 | 0.214 / 0.132 |
| so762 absent-dev | 1/51 | 0/17 | 0.048 / 0.022 |
| so762 test | 14/227 | 0/0 (no absent in sample) | 0.099 / — |

Context gains are larger for present tokens but not absent-free: one absent token
(`child_03_six`, /s/, human CLEARLY_ABSENT) rises from 0.001 (raw) to 0.919 (pad100) —
the model finds an /s/-like region when more left/right context is supplied. Any future
window policy must survive this false-gain check.

## 9. Final-consonant aggregation (LWE 28; other phones have insufficient n)

| phone | tokens | present | absent | recall full → best window | absent-det full → best | var | boundary-sensitive | encoder-gap |
|---|---:|---:|---:|---|---|---:|---:|---:|
| /t/ | 8 | 5 | 3 | 1.00 → 1.00 | 1.00 → 1.00 | 0.384 | 0% | 0% |
| /v/ | 3 | 1 | 2 | 1.00 → 1.00 | 1.00 → 1.00 | 0.196 | 0% | 0% |
| /n/ | 9 | 8 | 1 | 0.50 → 0.75 | 0.00 → 0.00 | 0.373 | 22% | 22% |
| /s/ | 2 | 1 | 1 | 0.00 → 0.00 | 1.00 → 0.00 | 0.464 | 0% | 50% |
| /r/ | 6 | 1 | 5 | 0.00 → **1.00** | 1.00 → 1.00 | 0.137 | 0% | 0% |
| /k/ /z/ /p/ | — | — | — | insufficient labeled tokens | | | | |

**/r/:** the single human-present /r/ (LOW reviewer confidence) is recovered by boundary
control (raw 0.304, right250 0.479), and all 5 absent /r/ stay below floor in every
window → the earlier "no /r/ evidence" claim was partly a full-window artifact. Per the
WP rule, **/r/ remains unresolved due to insufficient confidently labeled present
examples (n=1)**.

## 10. Causal classification and B2 assessment

Separated effects:
- **WINDOW**: 3/16 present finals (01_seven, 02_ten, 04_four) — full-window evidence
  <0.1, recovered by reasonable padding/raw; one absent token also gains (03_six).
- **ALIGNMENT**: 9/16 present finals — full-context evidence exists (E ≥0.3) but the
  production monotone span places the phone in a near-empty region (span posterior
  <0.05; e.g., child_01_nine 0.979 vs 0.0011). This is the dominant present-token issue.
- **ENCODER**: 3/16 present finals (01_ten, 06_six, 07_one) — no evidence in any of the
  11 windows; the raw-window score recovery of `child_07_one` did not recover the /n/.
- **MIXED/CONFLICT**: child_07_seven (absent with strong evidence), child_02_four
  (weak evidence), child_04_five / 05_three (score inversions from other phones).

**Answer to the B2 question:** after controlling for reasonable audio boundaries, the
frozen encoder still fails to produce evidence for a **minority but real subset of
human-present finals (3/16; 3 children)**, while a comparable subset is window/context
recovered (3/16) and the largest subset is an alignment failure (9/16). Both effects are
material → **MIXED**; B2 is **not yet** scientifically justified as the next step because
the alignment failure must be removed and more confidently-labeled present finals are
needed before an encoder change could be attributed to acoustics rather than pipeline.

## 11. Limitations

- LWE labeled finals n=28, single reviewer, 9 children; one present /r/ (LOW confidence).
- so762 dev/test samples: the 12-speaker sample contains no score-0 finals; absent-dev
  adds 17 absent tokens but is enriched by construction (still train-child speakers).
- Window family is small by design; no exhaustive search (anti-overfitting rule).
- The deletion-aware evidence E uses the frozen 1.9.14 prototype; the margin LLR remains
  unreliable near trailing silence (documented in 1.9.18) and is not used as a gate here.
- Score inversions driven by non-final phones are out of scope for this WP.

## 12. Files

```
Research/Speech/Phase1_9_19/
  BOUNDARY_WINDOW_CAUSALITY_REPORT.md   this file
  WINDOW_EXPERIMENT_RESULTS.csv         1188 rows (token x window, 3 corpora)
  WINDOW_STABILITY.csv                  per-token evidence/decision/alignment stability
  FINAL_CONSONANT_WINDOW_ANALYSIS.csv   per-phone aggregates
  EXPERIMENT_RESULTS.json               windows, separation, selection, negative check, causes
  NEXT_GATE_DECISION.md                 gate + B2 justification
  experiments/  window_causality.py, analyze_windows.py, final_metrics.py,
                run_absent_dev.py, _inspect_lwe.py
  artifacts/    window_rows_{lwe,so762_dev,so762_test,so762_absent_dev}.csv,
                inversion_explained.csv
```
No production file was modified; nothing was committed or pushed.
