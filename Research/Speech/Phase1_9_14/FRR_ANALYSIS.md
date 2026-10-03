# FRR-FIRST ANALYSIS

**Data:** `artifacts/p2/p2_frr_analysis.json`, `p2_threshold_sweep.csv`,
`p2_baseline_vs_candidates.csv`, `artifacts/siak/siak_summary_ages4-5-6-7-8-9-10.json`
**Human reference:** 1.9.12 blind final-consonant review (28 tokens, single reviewer
`human_mobile`, 2026-10-03); SIAK single-annotator 0–100 scores.

---

## 1. Rule applied

Human-present (correct) child tokens are the highest-risk failure mode. A method that
increases rejection of human-present tokens cannot be called an improvement, regardless of
correlation/AUC/mean score. FRR is reported at matched FAR and as a frontier, not averaged
away.

## 2. Final-consonant FRR frontier (28 labels: 16 present / 12 absent)

| FRR budget | baseline soft-v2 FAR | deletion margin FAR | GOP-ratio FAR |
|---|---:|---:|---:|
| FRR = 0/16 | 12/12 (must accept all) | **6/12** (th = −0.0671) | 12/12 |
| FRR ≤ 1/16 | **5/12** (as shipped, FRR 1) | 6/12 (FRR 0) | 11/12 |
| FRR ≤ 2/16 | 5/12 | 5/12 (FRR 2, th = −0.0528) | 11/12 |
| FRR ≤ 3/16 | 5/12 | 5/12 | 8/12 (FRR 3, th = −3.424) |
| FRR ≤ 4/16 | 5/12 | 2/12 (FRR 4, th = −0.0167) | 4/12 (FRR 4) |

Reading: the margin buys 1 accepted-absent at the cost of 1 rejected-present (FRR 0 vs 1);
at equal FRR (≤2) the baseline and margin both accept 5/12 absent. **No candidate dominates
the frozen baseline under FRR-first.**

Tokens behind the numbers:

- Baseline present-rejected: `fc_child_07_one` (/n/, CLEARLY_PRESENT; the known tok_58
  window inversion case).
- Margin FRR=0 threshold rejects none; absent-accepted: `four` ×3 (`child_01/02/03`),
  `child_05_four`, `child_03_six`, `child_07_seven`.
- Margin FRR=2 additionally rejects `child_01_ten` (/n/, CLEARLY_PRESENT) and keeps the
  same 5 accepted-absent — a worse trade than baseline.
- GOP-ratio at FRR≤1 rejects `child_07_one` and still accepts 11/12 absent — unusable.

## 3. Why deletion-aware scoring increases FRR

The child corpus is dominated by short, coarticulated finals and variable child acoustics.
Under an unpenalized skip transition, weak-but-real finals (nasals in `one/seven/ten`,
fricatives in `six`) are deleted by the DP because their posterior mass is lower than the
blank alternative. Human listening recovers these tokens; the acoustic model does not.
This is the child-speech gap quantified by SIAK (below), now visible at the token level.

## 4. SIAK FRR-proxy (human-good tokens the frozen scorer rejects)

SIAK has no binary error labels; a strict FRR proxy is used: among utterances the human
annex scored **≥80**, the fraction where frozen soft-v2 gives **<50** (or <20).

| group | n | soft<50 on SIAK≥80 | soft<20 on SIAK≥80 | Pearson | Spearman |
|---|---:|---:|---:|---:|---:|
| all scored | 1,074 | 24.6% | 8.7% | 0.226 | 0.222 |
| age 4 | 44 | **40.0%** | 0.0% | 0.140 | 0.151 |
| age 5 | 420 | **33.3%** | 11.1% | 0.145 | 0.141 |
| age 6 | 130 | **34.0%** | 12.0% | 0.295 | 0.298 |
| age 7 | 120 | 21.4% | 14.3% | 0.193 | 0.182 |
| age 8 | 120 | 19.5% | 7.3% | 0.340 | 0.323 |
| age 9 | 120 | 11.3% | 3.8% | 0.178 | 0.188 |
| age 10 | 120 | 15.2% | 2.2% | 0.210 | 0.207 |
| test split (speaker-disjoint) | 207 | 26.7% | 6.7% | 0.263 | 0.236 |

The age gradient is monotone in the FRR-proxy: the scorer rejects ~1/3 of human-good
4–6-year-old tokens and ~1/7 of 9–10-year-old tokens. Any child-facing scorer decision must
account for this before numbers/verdicts are surfaced.

## 5. What FRR-first changes in this phase

1. The zero-penalty deletion-aware alignment — the most “faithful” representation — is
   rejected outright (43.8% FRR).
2. GOP-ratio is rejected at every operating point (FAR 91.7–100% at usable FRR).
3. The deletion margin survives as a *ranking/uncertainty* signal only; it must not be
   turned into an error verdict without a new, independent human-labeled set.
4. The frozen baseline itself is not declared “good” — 5/12 absent finals are still accepted
   and /r/ remains unsolved; it is simply not beaten by the candidates under this rule.

## 6. Caveats

- 28 tokens, one reviewer; class-level n = 5–9.
- SIAK FRR-proxy compares two different score semantics (word pronunciation rating vs
  phone-similarity score); it is directional evidence, not calibrated error.
- The SIAK age-4 group has 44 tokens from one speaker (`train070_othr`), so the 40% figure
  is a single-speaker effect as much as an age effect.
