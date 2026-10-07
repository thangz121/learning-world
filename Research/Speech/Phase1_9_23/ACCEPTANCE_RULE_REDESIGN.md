# ACCEPTANCE RULE REDESIGN — WP-1.9.23

> **Tóm tắt (VI):** Thiết kế lại research-only lớp ACCEPT với output PRESENT/ABSENT/UNCERTAIN trên
> 68 luật thuộc 8 họ (strict identity, rank-aware, margin, blank, identity+support+margin,
> identity+support+margin+blank, temporal, phone-adaptive). Chọn luật trên SO762 dev, LWE là
> external, SO762 test held-out. Kết quả: **0/68 luật FRR-first safe** (không luật nào giữ nguyên
> recall production mà giảm FAR). Luật tốt nhất (`D2_identity_blank_occ1.0`, bỏ nhánh similarity)
> chỉ bỏ được 3/10 false accept TYPE A, phải trả 4,6 điểm recall dev (0,9088→0,8632), và giữ nguyên
> 4/4 TYPE B (encoder). Các luật rank/margin/blank/temporal muốn bỏ TYPE A phải trả 15–50 điểm
> recall. Phân bố weak-true-present và weak-false-accept chồng lấn gần hoàn toàn. Final:
> **ACCEPTANCE_RULE_AND_ENCODER_BOTH_LIMITING**; B2 NOT READY; gate kế:
> LABEL_EXPANSION_AND_ENCODER_DESIGN_REQUIRED.

**Machine:** ASUS · **Branch:** `main` (working `ux/math-arenas-hotfix-20260930`, tip `d7ef0ff`)
**Scope:** research-only · **Flags:** `production_vad=false · router_locked=false · unity_integrated=false · scorer_modified=false · production_window_locked=false`

## 1. Mission

Determine whether a research-only acceptance rule can preserve the recall the identity mechanism
provides for weak true-present final consonants while rejecting the weak / blank-dominated /
competitor-conflict false acceptances exposed by WP-1.9.22. Output is always
PRESENT / ABSENT / UNCERTAIN. No production change, no B2, no learned model.

## 2. Method (no leakage)

- Evidence: existing frame cache only (WP-1.9.21) + WP-1.9.22 reconstruction
  (`spanmean_decision_*`, `WINDOW_IDENTITY_ANALYSIS`). No encoder rerun.
- Feature matrix (`artifacts/feature_matrix.csv`, 609 tokens): identity (rank, top-1/top-5 flags,
  target mean/max/peak/p50/p90, competitor mean/max/peak, margins, ratio), blank (mean/max/at-peak,
  occupancy ≥0.5/0.8/0.9), temporal support (count, longest run, runs, concentration, peak position,
  distances, before/after peak), competitor conflict (wins at peak, dominates fraction), window
  stability, phone class.
- Splits: SO762 dev (train children + absent-enriched, 285P/17A) for rule selection; LWE 28 blind
  labels external; SO762 test (227P, speaker-disjoint) held out. No threshold tuned on LWE.
- 68 interpretable rules in 8 families; pre-declared grids derived from dev distributions
  (support τ ∈ {0.02…0.50}, margin ∈ {−0.01…0.02}, blank ∈ {0.80…0.99}, occupancy ∈ {0.90…1.0},
  run ≥ 1/2, rank-aware τ per rank). No black-box model.

## 3. Production baseline (primary window `full`)

| dataset | recall | FRR | FAR | absent-det | unsupported PRESENT | strong encoder FP |
|---|---:|---:|---:|---:|---:|---:|
| SO762 dev | 0.9088 | 0.0912 | 0.5294 | 0.4706 | 51 | 3 |
| LWE 28 | 0.9375 | 0.0625 | 0.4167 | 0.5833 | 9 | 1 |
| SO762 test | 0.8943 | 0.1057 | – | – | 35 | 0 |

## 4. Rule families — representative results (dev / LWE external / test)

| family (representative) | dev recall | dev FAR | LWE recall | LWE FAR | test recall | UNCERTAIN |
|---|---:|---:|---:|---:|---:|---:|
| A1 rank-1 only | 0.6912 | 0.1176 | 0.5000 | 0.1667 | 0.6784 | 0 |
| B rank-aware (0.05/0.20/0.50) | 0.7404 | 0.1765 | 0.5625 | 0.2500 | 0.7313 | 0 |
| C1 identity + margin ≥ 0 | 0.6912 | 0.1176 | 0.4375 | 0.1667 | 0.6828 | 0 |
| D1 identity + blank mean ≤ 0.90 | 0.3930 | 0.0588 | – | – | – | 0 |
| D3 identity + blank at peak ≤ 0.80 | 0.8035 | 0.2353 | 0.6875 | 0.0833 | 0.7885 | 0 |
| **D2 identity + blank occupancy ≤ 1.0 (best)** | **0.8632** | **0.4118** | **0.9375** | **0.3333** | **0.8590** | 0 |
| E identity + support 0.05 + margin ≥ 0 | 0.6772 | 0.1176 | 0.3750 | 0.1667 | 0.6652 | 0 |
| F main (support 0.10 + margin 0.01 + blank 0.95) | 0.6244 | 0.0714 | 0.4444 | 0.0000 | 0.6182 | 22.2–35.7% |
| G1 identity + run ≥ 2 | 0.4140 | 0.1765 | 0.1875 | 0.0833 | 0.4141 | 0 |
| H1 phone-class adaptive (exploratory, overfit-prone) | 0.7579 | 0.1176 | 0.5625 | 0.1667 | 0.7093 | 0 |

**FRR-first safe rules: 0 / 68.** No rule preserves production dev recall (0.9088) while reducing
dev FAR (0.5294). The Pareto frontier (`ACCEPTANCE_PARETO_FRONTIER.csv`, 8 optimal points) is steep:
−4.6 recall points buys −11.8 FAR points (D2); −8.8 buys −23.5 (D1 0.99); −15 buys −41 (H1);
−28.4 buys −45.8 (F, with 22% UNCERTAIN); the margin-only rule needs −30.5 points for FAR 0.0588.
The trade-off becomes unreasonable immediately: the *first* incremental FAR reduction already costs
more recall than the 0.95 FRR-first standard allows.

## 5. False-accept accounting (mandatory TYPE A / TYPE B split)

14 production false accepts: **10 TYPE A (acceptance-layer)** + **4 TYPE B (encoder-side strong
evidence)**. `FALSE_ACCEPT_RECLASSIFICATION.csv`:

| rule | TYPE A removed | TYPE B remaining | recall cost (dev) |
|---|---:|---:|---:|
| D2 (best) | **3/10** (all 3 similarity-path accepts) | 4/4 | 4.6 pts |
| A1 rank-1 only | 8/10 (all rank>0) | 4/4 | 21.8 pts |
| C1 margin ≥ 0 | 8/10 | 4/4 | 21.8 pts |
| D1 blank mean ≤ 0.90 | 9/10 | 4/4 | 51.6 pts |
| F main | 8/10 | 4/4 | 28.4 pts |

The three rank-0/rank-1 weak identity false accepts (`child_02_four`, `child_03_four`,
`child_06_four`) and the four strong TYPE B false accepts are **not removed by any rule that keeps
recall**. Strong encoder false accepts remain in every candidate rule — they cannot be counted as
solved by acceptance redesign.

## 6. Subgroup analysis (`PHONE_SUBGROUP_ANALYSIS.csv`, dev+LWE labeled)

| subgroup | n | production recall | production FAR | best-rule recall | best-rule FAR |
|---|---:|---:|---:|---:|---:|
| weak present (max < 0.15) | 79 | 0.6582 | – | **0.4937** | – |
| strong present (max ≥ 0.30) | 207 | 1.0000 | – | 1.0000 | – |
| weak absent | 22 | – | 0.3636 | – | 0.2273 |
| strong absent | 5 | – | 0.8000 | – | 0.8000 |
| rank-1 identity | 209 | 1.0000 | 1.0000 | 1.0000 | 1.0000 |
| rank-2..5 identity | 63 | 1.0000 | 1.0000 | 1.0000 | 1.0000 |
| not top-5 | 58 | 0.3250 | 0.1667 | 0.0000 | 0.0000 |
| blank mean ≥ 0.90 | 204 | 0.8659 | 0.4800 | 0.8156 | 0.3600 |
| one-frame support | 203 | 0.8539 | 0.4000 | 0.7865 | 0.2800 |
| multi-frame support | 127 | 0.9919 | 1.0000 | 0.9837 | 1.0000 |
| /r/ | 14 | 1.0000 | 0.8333 | 1.0000 | 0.5000 |
| /t/ | 50 | 0.9773 | 0.3333 | 0.9545 | 0.3333 |
| /n/ | 81 | 0.9351 | 0.7500 | 0.8831 | 0.7500 |

Aggregates hide the damage: the best rule loses **37% of weak true presents** (0.6582 → 0.4937) to
remove 3 false accepts; every phone class with enough data shows the same pattern; /r/ stays
recall-perfect but FAR 0.5 with n=14 (1 LWE present, LOW). The rank-2..5 identity subgroup has FAR
1.0 in this pool (7/7 accepted absents), so rank restriction removes true positives before it fixes
those.

## 7. Known decisive cases (best rule)

| case | truth | prod | final | rank | max_A | blank | margin | failure |
|---|---|---|---|---|---|---|---|---|
| child_01_eight | P | PRESENT | PRESENT | 0 | 0.592 | 0.808 | −0.003 | – |
| child_01_nine | P | PRESENT | PRESENT | 1 | 0.016 | 0.991 | −0.002 | – |
| child_01_seven | P | PRESENT | PRESENT | 3 | 0.059 | 0.903 | −0.015 | – |
| child_01_ten | P | PRESENT | PRESENT | 0 | 0.029 | 0.986 | +0.001 | – |
| child_02_ten | P | PRESENT | PRESENT | 1 | 0.066 | 0.968 | −0.017 | – |
| child_04_four | P | PRESENT | PRESENT | 3 | 0.090 | 0.946 | −0.010 | – |
| child_06_six | P | PRESENT | PRESENT | 3 | 0.0007 | 0.974 | −0.020 | – |
| child_07_one | P | **miss** | ABSENT | −1 | 0.013 | 0.942 | −0.021 | not accepted by production |
| child_01_four | A | PRESENT | **ABSENT** | −1 | 0.036 | 0.926 | −0.013 | TYPE A (similarity) fixed |
| child_02_four | A | PRESENT | PRESENT | 0 | 0.154 | 0.984 | +0.008 | TYPE A unfixed |
| child_03_four | A | PRESENT | PRESENT | 0 | 0.056 | 0.990 | +0.004 | TYPE A unfixed |
| child_07_seven | A | PRESENT | PRESENT | 1 | 0.629 | 0.883 | −0.009 | TYPE B encoder |

## 8. Falsification summary (details in `FALSIFICATION_RESULTS.md`)

| test | result | verdict |
|---|---|---|
| 1 blank-dominated still accepted | 279 → 262 | FAIL |
| 2 rank-2..5 near-zero support accepted | 34 → 34 | FAIL |
| 3 competitor stronger accepted | 128 → 104 | FAIL |
| 4 one-frame spike accepted | 271 → 248 | still accepted (true one-frame finals exist) |
| 5 strong encoder false peak accepted | 4 → 4 | FAIL (irreducible) |
| 6 true weak present destroyed | 87 accepted → 72 rejected | FAIL |
| 7 LWE vs SO762 asymmetry | LWE 0.9375 / dev 0.8632 recall | transfer differs |
| 8 /r/ collapse | n=21, recall 1.0 → 1.0 | PASS (data-limited) |
| 9 blank-high conservatism | n=339, recall 0.8057, uncertain 0 | recall loss |
| 10 excessive UNCERTAIN | D2 0%; F family 22–36% | F over-conservative |

## 9. Human label expansion (Part 7/8)

`HUMAN_LABEL_REVIEW_PACK.csv`: 23 reviewer-ready candidates — /r/ present first (P1: 3 LWE four
tokens, all with negative word verdicts — no more likely-present unlabelled /r/ exists in LWE),
weak-support present (P2: 2), rank-2..5 identity accepts (P3: 5 so762), strong false-accept
candidates (P4: 5), additional /r/ absent (P5: 4), strong /r/ present reference (P6: 4). Audio
references are local paths only; no audio copied into the repo. Instructions:
`HUMAN_LABEL_REVIEW_INSTRUCTIONS.md`. **NEW_LABELS_COLLECTED = 0** (no reviewer available in-session;
labels were not fabricated).

Human vs machine agreement on the existing 28 blind labels: production accepts 15/16 human-PRESENT
and 5/12 human-ABSENT; best rule (D2) accepts 15/16 and 4/12; the conservative F rule accepts 7/16
and 0/12. Labels remain the binding external-validation limit, but they do not change the feature
overlap finding.

## 10. Answers to Part 11 questions (exact numbers)

**A. Can acceptance redesign eliminate most WEAK identity false accepts?** No. Best rule removes
3/10 TYPE A (all similarity-path); the 7 identity-weak cases survive every recall-preserving rule.
Rules that remove 8–9/10 TYPE A cost 21.8–51.6 dev recall points.

**B. Without destroying weak true-present recall?** No. Removing TYPE A requires rejecting the same
feature region as weak true presents: best rule loses 72/87 production-accepted weak presents;
rank/margin/blank rules lose 15–50% of all present recall. 0/68 rules are FRR-first safe.

**C. Can it handle rank-2..5 identity?** The rank-2..5 subgroup (n=63) has production FAR 1.0 and
best-rule FAR 1.0; rank-1 restriction removes them but also removes 97 rank>0 true presents
(WP-1.9.22), so no.

**D. Can blank awareness reduce false acceptance safely?** No. Blank mean ≤0.90 removes 9/10 TYPE A
but dev recall falls to 0.393; the mildest blank condition (D2) removes only the similarity subset.

**E. Can temporal support improve discrimination without rejecting real one-frame finals?** No.
Requiring run ≥ 2 drops LWE recall to 0.1875; one-frame support is the norm (203/330 labeled finals)
and is shared by true and false cases alike.

**F. What fraction of remaining false accepts are clearly encoder-side?** 4/14 (28.6%) are TYPE B
strong-support encoder disagreements (max 0.63–0.96); they persist in every rule. Of the 10 TYPE A,
7 remain after the best rule.

**G. After acceptance redesign, is B2 actually justified?** No. The redesign failed to find a safe
operating point (0/68 FRR-first safe); per the WP rule, B2 stays blocked by decision-layer
uncertainty. The evidence now points to encoder evidence for weak finals as the limiting factor, but
B2 remains NOT READY (no training) until labels validate the gray zone and a design-only study is
approved.

## 11. Final status

```
ACCEPTANCE_RULE_AND_ENCODER_BOTH_LIMITING
```

- No safe operating point exists with the current frozen evidence (0/68 FRR-first safe).
- Weak true-present and weak false-accept features overlap almost completely (both blank-dominated,
  low-support, often negative-margin); the best rule trades 4.6 recall points for 3/10 TYPE A.
- 4/14 false accepts are strong encoder-side and irreducible by any acceptance rule.
- Labels (10–20 confident present finals, /r/ first) remain unavailable and are the external
  validation blocker.

## 12. Limitations

- Dev absent class is 17 tokens; LWE 28 labels are small; /r/ present n=1 LOW.
- Rule grids are interpretable and dev-derived; the exploratory H family is flagged overfit-prone
  and was not selected.
- The best rule (D2) effectively drops the similarity-path acceptance; this is a real but narrow fix
  (3 false accepts) and not FRR-first safe.
- All conclusions are for the primary window; window-dependent identity (11.5%, WP-1.9.22) is not
  separately gated by these rules.
- No human reviewer was available; 0 new labels collected.

## 13. Files

```
Research/Speech/Phase1_9_23/
  ACCEPTANCE_RULE_REDESIGN.md, ACCEPTANCE_RULE_VARIANTS.csv, ACCEPTANCE_RULE_PER_TOKEN.csv,
  ACCEPTANCE_PARETO_FRONTIER.csv, FALSE_ACCEPT_RECLASSIFICATION.csv,
  HUMAN_LABEL_REVIEW_PACK.csv, HUMAN_LABEL_REVIEW_INSTRUCTIONS.md,
  PHONE_SUBGROUP_ANALYSIS.csv, FALSIFICATION_RESULTS.md, EXPERIMENT_RESULTS.json,
  B2_READINESS.md, NEXT_GATE_DECISION.md,
  artifacts/ (feature_matrix.csv),
  experiments/ (build_feature_matrix.py, run_rule_search.py, build_label_pack.py,
                inspect_rules.py)
```

No production file modified; no model trained; no Unity change; no raw audio stored.
