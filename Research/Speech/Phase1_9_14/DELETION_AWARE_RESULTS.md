# P2 RESULTS — DELETION-AWARE FINAL-CONSONANT EVIDENCE (RESEARCH ONLY)

**Scripts:** `experiments/p2_deletion_aware.py`, `experiments/p2_compare.py`
**Artifacts:** `artifacts/p2/p2_tokens.csv` (65), `p2_summary.json`, `p2_threshold_sweep.csv`,
`p2_mismatches.csv`, `p2_baseline_vs_candidates.csv`, `p2_class_confusion.csv`, `p2_frr_analysis.json`
**Provenance:** `manifests/p2_provenance.json`
**Frozen baselines untouched:** 1.2 / 1.5 / 1.6 / 1.8 VAD / 1.9.12 human evidence / soft-v2.

---

## 1. Why the current representation fails

Phase 1.9.12 proved that `PhoneEvidenceV2.ctc_align` (a monotone DP that must assign every
target phone to ≥1 frame) cannot represent a **deleted** final phone. On the 3 confirmed
“four” cases the model emitted `f ɔ ɹ` and scores 100/100/66.8 although the reviewer heard no
ending sound. This phase asked whether an explicit deletion representation changes the
FRR-first outcome on the 65-token / 28-human-label set.

## 2. Methods (all on the same frozen model and audio)

Emissions are per-frame relative log-posteriors over **all phone classes + CTC blank**
(`log p(c) - logsumexp(...)`), so blank is a genuine competing hypothesis.

**A. soft-v2 (baseline).** Archived 1.9.12 result: final-phone `match_type ∈ {exact, soft}`
= present. Re-run reproduced archived `match_type` on all 65 tokens (0 mismatches).

**B. Deletion-aware blank-interleaved (2L+1) Viterbi.** State layout
`0=blank, 1=p1, 2=blank, 3=p2, …, 2n=blank`; stay/advance consume frames; deletion is an
epsilon transition `blank_{j-1} → blank_j` (slip-style `present=false`). Two readouts:
- `dalign_present_free`: final phone visited with zero skip penalty (most aggressive);
- `dalign_margin_per_frame` = `[score(forced present) − score(forced absent)] / T`, i.e. a
  per-frame log-likelihood ratio between the two hypotheses. Higher = more present evidence.

**C. GOP-ratio.** Over the forced-present final span (from B):
`log mean-p(expected final) − log mean-p(best competing phone class or blank)`;
`gop_expected` = mean expected posterior over the same span.

## 3. FRR-first results (28 blind human labels: 16 present, 12 absent)

| method | human-present rejected (FRR) | human-absent accepted (FAR) | AUC |
|---|---:|---:|---:|
| A. soft-v2 as shipped | **1/16 (6.3%)** | 5/12 (41.7%) | 0.760 |
| B. free deletion (zero penalty) | **7/16 (43.8%)** | 2/12 (16.7%) | 0.698 |
| B. margin, FRR=0 point (th = −0.0671) | **0/16** | 6/12 (50.0%) | **0.839** |
| B. margin, FRR≤2 point (th = −0.0528) | 2/16 | 5/12 (41.7%) | 0.839 |
| C. GOP-ratio, FRR≤1 point (th = −5.728) | 1/16 | 11/12 (91.7%) | 0.740 |
| C. gop_expected, FRR≤3 point | 3/16 | 6/12 (50.0%) | 0.750 |

Matched-FRR frontier (min FAR at FRR ≤ k/16): at every k the baseline is equal or better;
the margin only trades one accepted absent (FAR 5→6) for one rejected present (FRR 1→0),
i.e. it is **not** an improvement on this set. Full frontier in `p2_frr_analysis.json`.

## 4. What the representation change did achieve

- Deletion is now representable: the zero-penalty alignment rejects 10/12 human-absent
  finals (baseline rejects 7/12) — but at the cost of rejecting 7/16 human-present finals.
  This is the classic FRR trap and is flagged, not adopted.
- The margin is the best *ranking* signal (AUC 0.839 vs 0.760 baseline) — useful as an
  uncertainty/assessability feature, not as a verdict.
- Class behaviour (`p2_class_confusion.csv`):

| class | n | baseline correct | free-deletion correct | baseline misses |
|---|---:|---:|---:|---|
| STOP /t/ | 8 | 8/8 | 8/8 | — |
| FRICATIVE /s,v/ | 5 | 5/5 | 3/5 | — |
| LIQUID /r/ | 6 | **2/6** | 5/6 | fc_child_01/02/03/06_four |
| NASAL /n/ | 9 | 7/9 | **3/9** | fc_child_07_seven (+ weak child_04_nine) |

- The /r/ deletion class remains unsolved without breaking present tokens: the baseline
  catches 1/5 absent /r/ (child_05), the conservative margin catches 2/5 (child_03, child_06),
  and reaching 4/5 costs rejection of human-present nasals (`child_01_nine/ten`, `child_01_seven`).
- The single human-`PROBABLY_PRESENT` /r/ (child_04_four, reviewer confidence LOW) has the
  **worst** GOP-ratio among /r/ tokens (−5.19) and a negative margin (−0.017). The acoustic
  model simply does not encode a final /r/ there. With one LOW-confidence human label this
  must stay `UNCERTAIN`, not be counted as a system failure or a human error.
- Present-token margin overlap: `child_01_ten` (CLEARLY_PRESENT) margin −0.0671 is identical
  to the FRR=0 threshold; `child_01_seven` −0.053; `child_07_one` (CLEARLY_PRESENT, the
  known 1.9.11 tok_58 inversion) −0.066 and GOP −7.2 (worst of all tokens). Child-specific
  variation, not deletion, dominates these.

## 5. Required method assessment (Phase spec §10)

| property | A soft-v2 | B blank-interleaved Viterbi | C GOP-ratio |
|---|---|---|---|
| depends on existing CTC alignment? | **yes** (span anchoring) | no (alignment computed anew) | span from B, so indirectly |
| represents deletion? | **no** | **yes** (`present=false` + skip) | no (needs a present span; deletion decided by B) |
| distinguishes absence from bad realization? | no (both = low match) | partially (margin LLR; overlap at low evidence) | partially (low ratio also for weak-but-present /r/) |
| preserves human-correct child tokens? | mostly (FRR 1/16) | zero-penalty **no** (7/16); margin at FRR=0 yes | no safe point on this set |
| CPU runtime (warm, per token) | ~0.29 s (forward; archived) | DP 8.8 ms (3 Viterbi runs, T≈50–200, n≤4) + shared forward | ~1 ms + shared forward |
| model/license | wav2vec2-xlsr-53-espeak-cv-ft Apache-2.0, local; repo research code | same | same |

## 6. Verdict

**The deletion-aware representation is more faithful but, as implemented, does not reduce
final-consonant false rejection; GOP-ratio does not outperform the CTC-span approach at any
defensible FRR-first operating point.** The hypothesis from 1.9.13 (“replace span anchoring
with GOP-style evidence”) is **not supported** on this dataset in its minimal form.

Residual hypothesis worth one future experiment: the deletion margin as an *assessability /
uncertainty* feature (AUC 0.839) rather than a pronunciation verdict — e.g. flag
`|margin| < τ` as “cannot assess this final consonant” instead of scoring it. It must not be
adopted before an independent human-labeled set confirms the threshold.
