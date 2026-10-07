# TEMPORAL SUPPORT AGGREGATION REPORT — WP-1.9.21

> **Tóm tắt (VI):** WP-1.9.21 kiểm định giả thuyết "aggregation/support" của 1.9.20: liệu một luật
> support có tính thời gian, có mặt nạ vị trí, có giữ được bằng chứng final-consonant mà span-mean
> pha loãng, đồng thời loại được đỉnh giả / occurrence sai / nhiễu cửa sổ hay không. Đã dựng
> **frame cache tái sử dụng** (2.436 token-window, 117.128 frame; tái lập pipeline 1.9.19 với drift
> số học 2,4–5,9%), định nghĩa vùng ứng viên A/B/C/D + position masking, chạy 8 họ biến thể
> (MAX/TOP-K/LOCAL_CLUSTER/PEAK_PROMINENCE/TEMPORAL_SUPPORT/CONSERVATIVE...). Kết quả: **không luật
> support nào giữ được recall production** (dev 0,9088; tốt nhất 0,9018); tại recall khớp trên LWE
> (0,9375) thì FAR tăng 0,4167 → 0,75; tại điểm plateau (FAR 0,0833) recall rơi còn 0,5625. Các ca
> present yếu không có bằng chứng trong vùng (encoder no-evidence), ca absent mạnh (child_07_seven
> peak 0,63) không phân biệt được bằng posterior, ca 0,919 của child_03_six là occurrence sai. Gate:
> **SUPPORT_AGGREGATION_FAIL**; B2 NOT YET; production không đổi.

**Machine:** ASUS · **Branch tip:** `ac08f25` (= main, docs-only handoff) · **Scope:** research-only
**Flags:** `production_vad=false · router_locked=false · unity_integrated=false · scorer_modified=false · production_window_locked=false`

## 1. Mission

From WP-1.9.20: the previously claimed alignment failures were reclassified (8/9 NOT_ALIGNMENT) and the
surviving mechanism was **scoring aggregation (span mean diluting spiky evidence)**, plus
window/occurrence/encoder limits. This WP asks:

> Can a temporally plausible support/aggregation rule preserve genuinely present final-consonant
> evidence that the span mean destroys, while rejecting isolated peaks, wrong occurrences,
> neighboring-phone confusion, and false acoustic evidence?

Not "replace mean with max". This is a research-only package: no production scorer/VAD/router/Unity
change, no training, no B2.

## 2. Prior evidence reused (do not relitigate)

| WP | Result | Gate |
|---|---|---|
| 1.9.17 | alignment/deletion diagnostic | `REQUIRES_ALIGNMENT_WORK` |
| 1.9.18 | deletion-aware PRESENT/ABSENT/UNCERTAIN; FRR-first-safe point absent | `ALIGNMENT_DELETION_GATE_FAIL` |
| 1.9.19 | 11-window causality: ALIGNMENT 9 / WINDOW 3 / ENCODER 3 / MIXED 1 | `MIXED_WINDOW_AND_ACOUSTIC` |
| 1.9.20 | counterfactual realignment recovers 0/9; 8/9 NOT_ALIGNMENT; negative control FAIL (11 vs 8) | `ALIGNMENT_REPRESENTATION_FAIL` |

Frame-level logits were never cached in 1.9.17–1.9.20; 1.9.20 also re-ran the encoder once per
final-consonant token instead of once per utterance. This WP fixes both (see §4).

## 3. Current aggregation failure (production behaviour, measured)

Production `soft_match` decides on the production span with a **mean** posterior plus top-1 identity;
`production_vad=false` means the production construction scores the **full recording** (primary window
= `full`; `raw/pad100/pad250` are the 1.9.19 stability windows).

Measured on the same frozen cache (primary window):

| dataset | recall | FAR | absent-det | unsupported PRESENT | false support |
|---|---:|---:|---:|---:|---:|
| LWE 28 blind labels (16P/12A) | 0.9375 | 0.4167 | 0.5833 | 9 | 3 |
| SO762 dev (285P/17A) | 0.9088 | 0.5294 | 0.4706 | 68 | 53 |
| SO762 test (227P, speaker-disjoint) | 0.8943 | – | – | 40 | 28 |

Two distinct mechanisms are visible in the frame evidence:

1. **Mean dilution is real for the strong presents** (9/16): in-span max 0.49–0.96 while span mean is
   0.003–0.096. But production already accepts all of them via top-1 identity, so mean dilution does
   **not** cause their acceptance.
2. **Pure mean-posterior aggregation is unusable**: `mean_A` thresholded at 0.05 keeps 1/16 presents
   (recall 0.0625) and at ≥0.10 keeps none (control curve in `SUPPORT_VARIANT_RESULTS.json`).

The only human-PRESENT token production misses is `child_07_one` (span mean 0.00107; baseline `miss`).
Production false positives are identity-driven (`child_02_four` 0.154, `child_03_four` 0.056,
`child_01_four` 0.036, `child_06_four` 0.041 — all accepted `exact/soft`) plus one **strong false peak**
`child_07_seven` (max 0.629, TS 0.315, accepted `exact`, human PROBABLY_ABSENT).

## 4. Reusable frame evidence cache

`artifacts/frame_cache/` (committed; manifest with hashes/provenance):

| corpus | utterances | encoder runs | token-windows | frame rows | drift vs 1.9.19 stored rows |
|---|---:|---:|---:|---:|---:|
| lwe | 80 | 320 | 320 | 11,344 | 19/320 (5.9%) |
| so762_dev | 94 | 376 | 936 | 42,202 | 22/936 (2.4%) |
| so762_test | 93 | 372 | 908 | 47,090 | 30/908 (3.3%) |
| so762_absent_dev | 22 | 88 | 272 | 16,492 | 7/272 (2.6%) |
| **total** | 289 | 1,156 | 2,436 | 117,128 | 78/2,436 (3.2%) |

- Files: `frames_<corpus>.csv` (per-frame target posterior, top-1, best competitor, blank, margin,
  region membership A/B/C/D, earlier-same-class mask, prev/next phone posteriors) and
  `token_evidence_<corpus>.csv` (per token-window aggregation features + baseline + deletion-aware
  evidence + human labels).
- Determinism: re-running the WP-1.9.19 code path now reproduces this cache exactly (verified twice
  in-process and in a second process, e.g. `child_02_five pad100` = 0.8652 in both). The 3.2% drift
  against the stored 1.9.19 rows is **cross-session CPU encoder numeric drift** (small posterior deltas
  and a few near-tie span/deletion-path flips, concentrated in padded windows); baseline decisions in
  the checked rows are unchanged. All WP-1.9.21 comparisons are internal to this cache.
- No raw child audio is stored; `FRAME_CACHE_MANIFEST.json` records model snapshot
  (`2c733782…`), preprocessor/vocab hashes, input artifact hashes, region parameters and file hashes.

## 5. Final-phone candidate region (position-masked)

Derived from the frozen `ctc_align` span structure for focus phone `j` (word-final). `M` = union of
production spans of **earlier phones with the same canonical class** (mandatory position mask).

| region | definition |
|---|---|
| A | production span `[A0, A1]` |
| B | `[A0−5, A1+5]` frames (100 ms context) minus `M` |
| C | final tail after the **preceding-phone acoustic boundary** (last frame inside the preceding span with preceding-class posterior ≥ 50% of its span max), extending ≤5 frames into the following phone's span, minus `M` |
| D | conservative union `B ∪ C` (primary support region) |

No whole-word/whole-utterance search: the store range is bounded by the preceding span start, the
following span end (+100 ms) and any earlier same-class span. Earlier same-class evidence is reported
separately as `max_masked_earlier` (WRONG_OCCURRENCE candidates) and never used as support.

## 6. Support variants and pre-declared protocol

All features are computed on the masked region D unless stated. Constants are documented in the
manifest (`TS_MARGIN_SCALE=0.25`, `TS_COHERENCE_WIDTH=2`, `TS_HALF_FRAC=0.5`, `TS_FLOOR=0.05`).

| variant | score | decision family |
|---|---|---|
| A `CURRENT_MEAN` | production span mean + top-1 identity (`soft_match`, replicated) | production baseline |
| B `MAX` | `max_D` | `≥ τ`, τ ∈ {0.01…0.60} (control, not presented as a solution) |
| C `TOP-K` | mean of top-1/2/3/5 target frames in D | `≥ τ` |
| D `LOCAL_CLUSTER` | half-height cluster around the peak: `peak`, `cluster_width`, `neighbor_support` | `peak ≥ tp` ∧ `width ≥ w` ∧ `neighbor ≥ tn`, w ∈ {1,2}, tn ∈ {0, 0.05} |
| E `PEAK_PROMINENCE` | `peak − competitor_at_peak` | `peak ≥ tp` ∧ `prom ≥ tm` |
| F `TEMPORAL_SUPPORT` | `peak × clip(prom_comp/0.25,0,1) × clip(width_half/2,0,1)` | `≥ τ` |
| G `CONSERVATIVE_SUPPORT` | `TEMPORAL_SUPPORT` with PRESENT/ABSENT/UNCERTAIN band | `≥ thi` / `≤ tlo` |

Protocol: dev = SO762 train children (dev12 + absent-enriched, 302 tokens, speaker-disjoint from
test); FRR-first selection (dev recall must not fall below production dev recall 0.9088; then maximise
absent detection, then coverage); freeze; evaluate externally on the 28 LWE blind labels; never tune
on LWE.

**Selection outcome: no posterior-support setting reaches the dev recall floor.** Best is `MAX τ=0.01`
at dev recall 0.9018 (production 0.9088), so the fallback (max recall) selects `MAX τ=0.01`. This
already answers the FRR-first question on development data: posterior aggregation cannot even
reproduce production recall, because production accepts weak/no-evidence presents by top-1 identity.

## 7. LWE external evaluation (28 blind labels, primary window)

| variant (frozen setting) | present recall | FAR | absent-det | unsupported PRESENT | false support | UNCERTAIN |
|---|---:|---:|---:|---:|---:|---:|
| A `CURRENT_MEAN` (production) | **0.9375** | **0.4167** | 0.5833 | 9 | 3 | 0 |
| B `MAX τ=0.01` (selected) | 0.9375 | 0.7500 | 0.2500 | 13 | 6 | 0 |
| C `TOP2 τ=0.01` | 0.8750 | 0.6667 | 0.3333 | 11 | 5 | 0 |
| C `TOP3 τ=0.01` | 0.8125 | 0.5833 | 0.4167 | 9 | 3 | 0 |
| C `TOP5 τ=0.01` | 0.8125 | 0.5000 | 0.5000 | 8 | 2 | 0 |
| D `LOCAL_CLUSTER (tp0.2,w1,tn0)` | 0.5625 | 0.0833 | 0.9167 | 0 | 0 | 0 |
| E `PEAK_PROMINENCE (tp0.15,tm0)` | 0.5625 | 0.1667 | 0.8333 | 0 | 0 | 0 |
| F `TEMPORAL_SUPPORT τ=0.01` | 0.5625 | 0.1667 | 0.8333 | 0 | 0 | 0 |
| G `CONSERVATIVE (0.15/0.05)` | 0.5625 | 0.0909 | 0.9091 | 0 | 0 | 0.0357 |

**Matched-recall control (MAX):** to keep production recall 0.9375, τ must be ≤0.01, where FAR =
0.75–0.9167 and 13–15 of the 16 PRESENT decisions are unsupported. **No support threshold dominates
the production operating point.** The MAX frontier: τ=0.05 → (0.75, 0.25); τ=0.2–0.4 → (0.5625,
0.0833). TOP3 τ=0.3–0.5 → FAR 0 with recall 0.375→0. TEMPORAL_SUPPORT τ=0.4 → (0.3125, 0).
AUC (LWE): MAX 0.7813, TOP3 0.8021, TEMPORAL_SUPPORT 0.7813; dev AUC ≈ 0.82–0.84.

**SO762 held-out test (227 present tokens, speaker-disjoint):** production recall 0.8943; selected
`MAX τ=0.01` 0.8855 (34 unsupported); plateau variants 0.70–0.75; `LOCAL_CLUSTER` 0.7533. The same
recall/coherence trade-off generalizes; the test set contains no absent tokens (ground-truth
limitation, reported).

## 8. Known-case replay (13 decisive cases; `SUPPORT_CASE_ANALYSIS.csv`, `SUPPORT_FRAME_TRACE.csv`)

Frozen selected rule (`MAX τ=0.01`) and plateau (`MAX τ=0.3`, noted per row):

| case | human | peak (D) | class | current → research (τ=0.01) | plateau τ=0.3 | mechanism |
|---|---|---:|---|---|---|---|
| child_01_eight | PRESENT | 0.592 | STRONG_SPARSE | PRESENT → PRESENT | PRESENT | evidence in span (mean diluted; identity saves it) |
| child_01_nine | PROBABLY_PRESENT | 0.0155 | NO_SUPPORT | PRESENT → PRESENT (unsupported) | ABSENT | 0.979 is the **earlier /n/** (masked → WRONG_OCCURRENCE); final /n/ has no independent evidence |
| child_07_one | PRESENT | 0.013 | NO_SUPPORT | **ABSENT → PRESENT** (unsupported) | ABSENT | the only production miss; "recovered" only below any safe threshold (prom < 0) |
| child_01_seven | PROBABLY_PRESENT | 0.059 | WEAK_SPARSE | PRESENT → PRESENT | ABSENT | weak; pad250 shows 0.504 (window-sensitive) |
| child_02_ten | PRESENT | 0.066 | COMPETITOR_DOMINATED | PRESENT → PRESENT | ABSENT | peak frame better explained by another class (prom −0.544) |
| child_04_four | PROBABLY_PRESENT | 0.090 | WEAK_SPARSE | PRESENT → PRESENT | ABSENT | raw 0.304 / pad250 0.479 but full 0.090 → window artifact |
| child_01_ten | PRESENT | 0.029 | NO_SUPPORT | PRESENT → PRESENT | ABSENT | identity-driven production acceptance |
| child_06_six | PROBABLY_PRESENT | 0.0007 | NO_SUPPORT | **PRESENT → ABSENT (regressed)** | ABSENT | encoder no-evidence in every window |
| child_01_four / child_03_four / child_06_four | ABSENT | 0.036 / 0.056 / 0.041 | NO_SUPPORT / WEAK_SPARSE | PRESENT → PRESENT (false) | ABSENT | production identity false positives; plateau rejects them |
| child_02_four | ABSENT | 0.154 | WEAK_COHERENT | PRESENT → PRESENT (false) | ABSENT | strongest of the weak false positives |
| child_07_seven | PROBABLY_ABSENT | **0.629** | STRONG_SPARSE | PRESENT → PRESENT (false) | PRESENT | **strong false peak**; indistinguishable from present by any posterior support rule |
| child_03_six | ABSENT | 0.00002 | NO_SUPPORT | ABSENT → ABSENT | ABSENT | 0.919 belongs to the earlier /s/ (only in shifted windows) → WRONG_OCCURRENCE, correctly excluded |

Totals: fixed 1 (`child_07_one`, unsupported), regressed 1 (`child_06_six`).
Reclassification (`SUPPORT_FAILURE_RECLASSIFICATION.csv`): FALSE_PEAK_COMPETITOR 5, WEAK_INCONCLUSIVE 4,
ENCODER_NO_EVIDENCE 2, WRONG_OCCURRENCE_MASKED 1, TRUE_SPARSE_SUPPORT 1.

## 9. Negative controls

- **LWE known ABSENT (12):** production FAR 0.4167. At the selected `MAX τ=0.01` FAR 0.75 with 7
  unsupported PRESENT; at the plateau (τ=0.2–0.4) FAR 0.0833. `LOCAL_CLUSTER` reaches FAR 0.0833,
  `TOP3 τ=0.3–0.5` reaches FAR 0.
- **SO762 absent-enriched dev (17 absent):** selected variants reject 11–12/17
  (absent_det 0.647–0.706, false_support 0).
- **Position-masked negative control (mandatory):** unmasked global MAX (control, not a candidate) at
  τ=0.2–0.5 wrongly accepts `child_01_nine` from the earlier /n/ (0.979) — `wrong_occurrence_cases =
  [child_01_nine]`. The masked rule removes that false recovery. `child_03_six`'s 0.919 exists only in
  left/shift windows and is attributed to the earlier /s/, not to the final phone.
- **False-support rate** (PRESENT whose peak frame is competitor-dominated, `prom_comp ≤ 0`): production
  3/28, MAX τ=0.01 6/28, plateau variants 0/28.

A method that finds more PRESENT but places support in wrong regions is not a success; here the
opposite failure appears: safe thresholds lose true presents, unsafe thresholds are dominated by
unsupported/false decisions.

## 10. Window interaction (full / raw / pad100 / pad250)

Decision flips for the selected `MAX τ=0.01` (all windows): LWE 5/28 (17.9%), SO762 test 24/227
(10.6%), dev 28/302 (9.3%). The weak presents are the flippers: `child_04_four` (full 0.090 / raw
0.304 / pad250 0.479), `child_01_seven` (0.059 → 0.504), `child_01_nine` (0.0011 → 0.005; its 0.977
in raw is the masked earlier occurrence). Support median std across windows 0.0023. A strong support
mechanism should not depend on one lucky crop; these gains do, and they flip the decision.

## 11. Sparse peak analysis (spec §7)

| set | n | width 1 | width 2 | width ≥3 | median peak | median TS |
|---|---:|---:|---:|---:|---:|---:|
| SO762 dev present | 285 | 238 | 46 | 1 | 0.829 | 0.448 |
| SO762 dev absent | 17 | 16 | 1 | 0 | 0.005 | 0.000 |
| LWE present | 16 | **16** | 0 | 0 | 0.542 | 0.271 |
| LWE absent | 12 | 11 | 1 | 0 | 0.028 | 0.000 |

Valid child final consonants are frequently **single-frame (20 ms) events** (all 16 LWE presents; 84%
of dev presents). Requiring coherence collapses recall: `LOCAL_CLUSTER` with width ≥2 or
neighbor ≥0.05 drops dev recall from 0.76 to 0.14 while absent detection rises to ~1.0. AUC of
width1/2/3/5 scores is 0.78–0.82 — width adds little beyond amplitude. So a single-frame spike must
not automatically be PRESENT, but longer support is not automatically better either; the model gives
no separable "briefness" signature.

## 12. Human-uncertain set (kept separate)

LWE word-level AMBIGUOUS final-consonant tokens: **2** (`child_03_two`, `child_06_five`; the other
AMBIGUOUS words are vowel-final and out of scope). Decisions at the frozen settings vary by family
(`child_06_five` PRESENT by MAX/CURRENT_MEAN, ABSENT by TOP-K/LOCAL_CLUSTER/TS; `child_03_two`
ABSENT by all except CONSERVATIVE UNCERTAIN). They are never used as ground truth and the system is
not credited for confident decisions here. The remaining 50 unlabeled/no-verdict final-consonant LWE
tokens are reported separately (frozen rule: 25 PRESENT / 25 ABSENT) — also not ground truth.

## 13. New-label audit and /r/

- Existing human final-consonant labels in the repo: **28** (16 PRESENT / 12 ABSENT, LWE blind).
  `p1_evidence_table.csv` carries the same 28 labels; other human reviews (1.9.12 window clarity,
  1.9.14 word verdicts, 1.9.15 assessability, root `Human_Review_StageA_Filled.csv`) are **not**
  final-consonant presence labels and were not converted.
- New confidently-labeled PRESENT finals collected in-session: **0** (no human listener available;
  labels were not manufactured). `NEW_HUMAN_LABELS.csv` is created empty with the required schema;
  `LABEL_CANDIDATES.csv` lists 20 review candidates (prioritised /r/, then other finals, local
  recordings only, model support values included). Review tooling:
  `Research/Speech/Phase1_9_15/experiments/serve_review.py`. No audio was copied into the repo.
- /r/: present n=1 (`child_04_four`, PROBABLY_PRESENT, LOW), absent n=5. **/r/ remains
  data-limited**; no /r/-specific tuning was performed and no "/r/ solved" claim is made.

## 14. Root-cause update

1. Alignment is not the binding constraint (1.9.20 stands).
2. **Aggregation is also not the binding constraint for the end decision.** Temporal support does
   recover the strong spiky presents that the mean dilutes, but production already accepts them by
   top-1 identity; the remaining present failures are **encoder no-evidence** (7/13 decisive cases
   have peak < 0.05 in every reasonable window; `child_06_six` 0.0007, `child_07_one` 0.013) or
   **window artifacts** (`child_04_four`, `child_01_seven`), and the false-positive floor is set by
   identity-driven acceptance plus a **strong false peak** (`child_07_seven` 0.63).
3. Position masking is validated: the earlier same-class occurrence trap (`child_01_nine` 0.979,
   `child_03_six` 0.919-in-shifted-windows) is correctly excluded; an unmasked method would have
   claimed WRONG_OCCURRENCE recovery.
4. The decision layer (acceptance rule: top-1 identity vs posterior support, and how to use
   UNCERTAIN) is now the highest-value target — but only after more trusted labels exist.

## 15. B2 decision

**NOT YET.** The aggregation/support problem has now been evaluated: no rule is FRR-safe, the
remaining genuinely unsupported PRESENT cases persist across windows, and there are not enough
confidently-labeled present finals (esp. /r/) to separate "no evidence" from "label wrong". B2 can
only be reconsidered after the acceptance/identity logic is re-measured FRR-first and the label gap
is addressed.

## 16. Limitations

- SO762 ground truth is a phone-score threshold (score 0 = incorrect OR missed); the held-out test
  set has no absent tokens, so held-out evidence is recall/stability only.
- Dev absent class is small (17 tokens); threshold selection is consequently weak — this is why the
  full LWE frontier is reported alongside the pre-registered point.
- 3.2% cross-session encoder drift vs stored 1.9.19 rows (documented; internal comparisons unaffected).
- Single reviewer history for the 28 labels; /r/ n=1 LOW.
- The selected fallback point (`MAX τ=0.01`) is deliberately the recall-maximal point when no rule
  meets the FRR floor; the report therefore also gives the plateau operating points and the full
  curves so the conclusion does not depend on the fallback choice.

## 17. Files

```
Research/Speech/Phase1_9_21/
  TEMPORAL_SUPPORT_AGGREGATION_REPORT.md   this file
  SUPPORT_CASE_ANALYSIS.csv                13 decisive cases, all required columns
  SUPPORT_FRAME_TRACE.csv                  561 frame rows (primary window) for the 13 cases
  SUPPORT_VARIANT_RESULTS.json             all variants, dev grids, curves, controls, stability
  SUPPORT_FAILURE_RECLASSIFICATION.csv     previous -> new root cause per decisive case
  NEW_HUMAN_LABELS.csv                     empty schema (0 new labels; no reviewer in-session)
  LABEL_CANDIDATES.csv                     20 candidates for a future human review round
  NEXT_GATE_DECISION.md
  artifacts/frame_cache/                   frames_*.csv, token_evidence_*.csv,
                                           FRAME_CACHE_MANIFEST.json (hashes + provenance)
  experiments/                             p21_lib.py, build_frame_cache.py, analyze_support.py,
                                           check_determinism.py, diff_1919.py, dump_lwe_full.py,
                                           inspect_cache.py, inspect_results.py, final_summary.py
```

No production file was modified; no model was trained; no Unity code touched.

## 18. Final gate

```
SUPPORT_AGGREGATION_FAIL
```
