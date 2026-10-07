# ENCODER FAILURE AUDIT — WP-1.9.24 Part B

> **Tóm tắt (VI):** Áp tiêu chí encoder-side failure (5 điều kiện) + falsification A–G cho 4 ca TYPE-B.
> Kết quả: **0/4 confirmed**; cả 4 UNRESOLVED — 3 ca thiếu nhãn human đáng tin (so762 score-0),
> 2 ca là đỉnh 1-frame cô lập (không có support lân cận). Ca mạnh nhất `child_07_seven` (PROBABLY_ABSENT
> HIGH) có 0,63 nhưng frame lân cận 0,024/0,005 → fail test C. Counterfactual encoder thứ hai
> (lv-60-espeak, local): 4/4 giữ evidence ≥0,1 nhưng 2/4 giảm mạnh (0,68→0,25; 0,82→0,41), 2/4 giữ
> (0,63→0,94; 0,96→0,91) → false-evidence không phải hằng số acoustic. Ngược lại, no-evidence
> present: 3/10 so762 + 3/28 LWE gain evidence mạnh dưới encoder thứ hai → limitation có thật và
> phụ thuộc representation. Kết luận: giả thuyết false-evidence CHƯA xác nhận (label-limited);
> limitation no-evidence ĐƯỢC ủng hộ.

**Scope:** research-only · no training, no production change · reads WP-1.9.21/1.9.22/1.9.23 artifacts.

## 1. Encoder-side failure criteria (Part 8)

A case is a candidate encoder-side failure only if: (1) human confidently ABSENT; (2) production
acceptance survives all acceptance-rule variants; (3) target evidence strong; (4) correct word/phone
position; (5) not explained by wrong occurrence, blank, competitor, window artifact, alignment or
acceptance logic.

## 2. TYPE-B replay (`ENCODER_TYPE_B_CASES.csv`)

| case | phone | human label (source) | max_A | strong windows | cluster w | neighbors (prev/next) | peak margin | blank@peak | A | B | C | D | E | F | G | verdict |
|---|---|---|---:|---:|---:|---|---:|---:|---|---|---|---|---|---|---|---|---|
| child_07_seven | n | PROBABLY_ABSENT (HIGH, WP-1.9.12 blind) | 0.629 | 4/4 | 1 | 0.024 / 0.005 | +0.387 | 0.092 | ✓ | ✓ | **✗** | ✓ | ✓ | ✓ | ✓ | UNRESOLVED |
| 014180143_15 | n | score 0 (so762, not a listening label) | 0.682 | 4/4 | 1 | 0.272 / 0.000 | +0.180 | 0.258 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | **✗** | UNRESOLVED |
| 014190172_7 | m | score 0 (so762) | 0.820 | 4/4 | 1 | 0.029 / 0.001 | +0.175 | 0.013 | ✓ | ✓ | **✗** | ✓ | ✓ | ✓ | **✗** | UNRESOLVED |
| 014350146_16 | n | score 0 (so762) | 0.956 | 4/4 | 1 | 0.235 / 0.249 | +0.864 | 0.031 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | **✗** | UNRESOLVED |

All four survive every WP-1.9.23 acceptance rule (rules A–H and the final D2 all keep them), so
acceptance redesign cannot remove them safely. But under the mandatory falsification none qualifies
as confirmed encoder false evidence:

- **3/4 lack a confident human listening label** (so762 score 0 means "incorrect or missed", not a
  per-consonant human listening judgment) — test G fails.
- **2/4 are isolated one-frame spikes** (child_07_seven, 014190172_7) with weak adjacent frames —
  test C fails. `child_07_seven`, the only case with a confident ABSENT label, is a single-frame
  event: the 0.63 peak has neighbors 0.024 and 0.005.
- Full trace data (top-5, positions, window stability, all rule decisions) is in
  `ENCODER_TYPE_B_CASES.csv`.

## 3. Alternative-encoder counterfactual (Part 18, local model only)

Second espeak-phoneme encoder: `facebook/wav2vec2-lv-60-espeak-cv-ft` (already cached locally,
apache-2.0, no download), same production-span measurement on a small diagnostic subset
(`artifacts/alt_encoder_counterfactual.csv`, `artifacts/alt_encoder_summary.json`).

| group | n | primary finds evidence | alt finds evidence | persists | lost | gained |
|---|---:|---:|---:|---:|---:|---:|
| TYPE_B | 4 | 4/4 | 4/4 | 4/4 (≥0.1) | 0 | 0 |
| LWE labeled | 28 | 39.3% | 42.9% | 32.1% | 7.1% | 10.7% |
| so762 no-evidence present | 10 | 0% | 30% | 0% | 0% | 30% |
| so762 strong present (control) | 5 | 100% | 100% | 100% | 0% | 0% |
| so762 absent | 5 | 60% | 40% | 40% | 20% | 0% |

Key per-token observations:

- TYPE_B magnitudes are encoder-dependent: `child_07_seven` 0.63 → **0.94** (persists), `014350146_16`
  0.96 → 0.91 (persists), but `014180143_15` 0.68 → **0.25** and `014190172_7` 0.82 → **0.41**.
- No-evidence present cases can gain strong evidence under the second encoder: `000010075_5`
  0.000 → **0.76**, `000010095_12` 0.000 → **0.82**, `000260050_5` 0.002 → **0.92**; LWE
  `child_01_seven` 0.059 → **0.47**, `child_04_four` (/r/, human PROBABLY_PRESENT) 0.090 → **0.41**.
- But the second encoder also **loses** evidence the primary found (`child_05_eight` 0.758 → 0.088,
  `child_01_eight` 0.592 → 0.136) and strong present controls persist 5/5.

Interpretation: the frozen encoder's evidence is **representation-dependent**, not an acoustic ground
truth. The false-evidence pattern is not stable (2/4 collapse), and the missing-evidence pattern is
partly recoverable by another encoder (3/10 + 2 LWE), but neither encoder dominates.

## 4. Phone confusion profile (`PHONE_CONFUSION_PROFILE.csv`, labeled tokens)

| target | n | top competitor(s) | median max_A | prod recall | prod FAR |
|---|---:|---|---:|---:|---:|
| n | 151 | (nasal/liquid neighbour) | 0.681 | 0.918 | 0.75 |
| s | 93 | **z (62)**, t (13) | 0.770 | 0.868 | 0.00 |
| t | 84 | d (27), vowel | 0.918 | 0.974 | 0.33 |
| z | 58 | **s (41)**, t (6) | 0.801 | 0.912 | 0.00 |
| v | 44 | f (17), b (9) | 0.732 | 0.756 | 0.00 |
| ɹ | 21 | liquid/l (4) | **0.135** | 1.000 | 0.833 |
| m | 20 | **n (17)** | 0.965 | 1.000 | 1.00 |
| k | 20 | (velar/vowel) | 0.977 | 0.895 | 0.00 |
| d | 14 | t (8) | 0.880 | 1.000 | 1.00 |
| l | 7 | – | **0.005** | 0.667 | 1.00 |

Systematic, expected articulatory confusions appear (s↔z, m↔n, v↔f, t↔d); liquids are the weakest
class by far (median max_A 0.005–0.135). This is a systematic representation pattern, not isolated
noise — but it is not proof of "encoder failure" for any single token.

## 5. Verdict

| question | answer |
|---|---|
| strong encoder false-evidence confirmed? | **NO — 0/4** (3 label-limited, 2 isolated one-frame peaks, 1 both) |
| encoder limitation (missing evidence) supported? | **YES** — 87/528 labeled present tokens have max_A < 0.02 (incl. `child_06_six` PROBABLY_PRESENT HIGH, `child_07_one` CLEARLY_PRESENT HIGH); 3/10 no-evidence so762 cases gain strong evidence under a second local encoder |
| acceptance logic able to fix the TYPE-B cases? | No (WP-1.9.23: 0/68 FRR-first safe; all four survive every rule) |
| is this an encoder failure or label ambiguity? | **False-evidence side: cannot separate (labels insufficient). Missing-evidence side: encoder limitation supported.** |

No production change; no training; no encoder fine-tuning; no model replacement.
