# ACCEPTANCE / IDENTITY DECISION AUDIT — WP-1.9.22

> **Tóm tắt (VI):** Audit cơ chế ACCEPT của production PhoneEvidenceV2 trên frame cache 1.9.21.
> Tái lập quyết định production **chính xác 609/609 token** (đối chiếu bản lưu 1.9.12: 65/65).
> Phát hiện: production accept **527/609** token, trong đó **501 qua identity credit** (best_obs ==
> target; 397 rank-1, **104 rank 2–5 = 20,8%**), **26 qua similarity**, và **0 qua posterior path**
> (post ≥ 0,25 không bao giờ kích hoạt — variant E ≡ A). Toàn bộ 14 ca false accept đều là
> identity/similarity: 7 IDENTITY_WEAK, 4 IDENTITY_STRONG (encoder), 3 SOFT_SIMILARITY; 10/14 có
> competitor mạnh hơn target ở span-mean, 12/14 bị blank chi phối ≥0,9. Nhưng gỡ identity credit
> (variant B) **loại 446 true present** (dev recall 0,9088 → 0,0632; LWE 0,9375 → 0,0) để chỉ bỏ 11
> false accept → FRR-first không chấp nhận được. Kết luận: **ACCEPTANCE_LOGIC_AND_ENCODER_BOTH_REQUIRED**;
> B2 NOT READY; gate kế tiếp: redesign acceptance rule (research-only) + mở rộng nhãn.

**Machine:** ASUS · **Branch:** `main` (working `ux/math-arenas-hotfix-20260930`, tip `60a55ab`)
**Scope:** research-only · **Flags:** `production_vad=false · router_locked=false · unity_integrated=false · scorer_modified=false · production_window_locked=false`

## 1. Mission

Answer one question: why can production `PhoneEvidenceV2` ACCEPT a target phone when its acoustic
posterior/support is very low, and is the TOP-1 identity mechanism responsible for most false
acceptance? Neither H1 (identity is primary) nor H0 (identity is not) may be assumed; both must be
settled from frame-level evidence already available in WP-1.9.21.

## 2. Sources and method

- WP-1.9.21 frame cache: `Research/Speech/Phase1_9_21/artifacts/frame_cache/`
  (2,436 token-windows / 117,128 frames; manifest hash recorded in `EXPERIMENT_RESULTS.json`).
- Experiment 1 target recompute (primary window `full` = production construction, `production_vad=false`):
  `artifacts/spanmean_decision_<corpus>.csv` — the span-mean top-5, rank, competitor, blank,
  identity margin, per token. 289 encoder runs; no production code changed.
- All counterfactual decisions are research-only functions over these artifacts.

## 3. Experiment 1 — exact reconstruction and reproduction

**Step-by-step path reconstructed per token:** production span (`ctc_align`), span-mean target
posterior, top-1/top-2 phones and posteriors, target rank in the span-mean top-5, identity credit
(`best_obs == target`), best similarity, exact/soft/miss classification, final decision, competitor,
blank, identity margin.

Reproduction results:

| check | result |
|---|---|
| recomputed vs WP-1.9.12 archived LWE decisions (match + posterior, 65 child tokens) | **65/65 exact, 0 mismatch** |
| recomputed vs WP-1.9.21 cache (609 primary-window tokens) | **609/609, 0 mismatch** |

No discrepancy to diagnose. The frozen production result is fully reproduced.

**Acceptance census (609 primary-window tokens, 527 accepted):**

| acceptance path | n | share of accepts |
|---|---:|---:|
| identity credit (`best_obs == target`) | 501 | 95.1% |
| — of which rank-1 (span-mean argmax == target) | 397 | 75.3% |
| — of which rank 2–5 (target in top-5 but not top-1) | **104** | **20.8%** |
| similarity path (`best_obs != target`, raw sim ≥ 0.35) | 26 | 4.9% |
| posterior path (`span-mean target post ≥ 0.25`) | **0** | 0% |

Variant E (`identity OR similarity`, no posterior gate) is **identical to production on every
dataset** (0 decisions changed) — the posterior path never fires. Production acceptance is
*entirely* identity + similarity, and one fifth of identity accepts are not top-1 at all.

**LWE 28 blind labels:** 20 accepted (19 identity: 10 rank-0, 5 rank-1, 1 rank-2, 3 rank-3; 1
similarity: `child_01_four`, /l/→/ɹ/ sim 0.35). Span-mean blank posterior is 0.80–0.999 on almost
every token: identity is decided in frames where blank owns most of the probability mass.

## 4. Experiment 2 — identity counterfactuals (FRR-first)

Variants: A production; B production minus identity clause (posterior + similarity paths only);
C support-only (`max target posterior in span ≥ τ`); C2 peak-support-only (D region); D identity AND
support; E identity OR similarity (no posterior); F support OR similarity (no identity); G posterior
mean only. Full table: `ACCEPTANCE_VARIANT_RESULTS.csv`.

| variant | dataset | recall | FAR | changed | true presents rejected | false accepts removed |
|---|---|---:|---:|---:|---:|---:|
| A production | LWE | 0.9375 | 0.4167 | 0 | – | – |
| A production | dev | 0.9088 | 0.5294 | 0 | – | – |
| A production | test | 0.8943 | – | 0 | – | – |
| B no identity | LWE | **0.0000** | 0.0833 | 19 | **15/16** | 4/5 (similarity accept remains) |
| B no identity | dev | **0.0632** | 0.1176 | 248 | **241/285** | 9/17 |
| B no identity | test | **0.0573** | – | 190 | **190/227** | – |
| E identity OR sim | all | ≡ A | ≡ A | 0 | 0 | 0 |
| D identity AND support τ=0.05 | LWE | 0.7500 | 0.2500 | 5 | 3 | 2 |
| D identity AND support τ=0.10 | LWE | 0.5625 | 0.1667 | 9 | 6 | 3 |
| D identity AND support τ=0.20 | LWE | 0.5625 | 0.0833 | 10 | 6 | 4 |
| C support-only τ=0.05 | LWE | 0.7500 | 0.2500 | 5 | 3 | 2 |
| C support-only τ=0.20 | LWE | 0.5625 | 0.0833 | 10 | 6 | 4 |
| F support OR sim τ=0.05 | LWE | 0.7500 | 0.3333 | 4 | 3 | 1 |
| G posterior mean τ=0.02 | LWE | 0.5000 | 0.0000 | 12 | 7 | 5 |
| G posterior mean τ≥0.05 | LWE | ≤0.0625 | 0.0 | ≥19 | ≥14 | 5 |

FRR-first verdict: **removing identity credit is not viable** (B rejects 446 true presents across
LWE+dev+test to remove 11 false accepts, and leaves the 3 similarity-path false accepts). Support
gating (C/D) trades weak true presents for weak false accepts at the same frontier WP-1.9.21 already
mapped, and cannot remove the strong false peaks. No variant improves the FRR-first trade-off.

## 5. Experiment 3 — false-acceptance decomposition (14 cases)

`FALSE_ACCEPTANCE_DECOMPOSITION.csv` (5 LWE + 9 SO762 absent-enriched, every production-accepted
absent token):

| mechanism | n | examples |
|---|---:|---|
| IDENTITY_WEAK (identity, span support < 0.30) | 7 | `child_02_four` (rank0, max 0.154), `child_03_four` (rank0, 0.056), `child_06_four` (rank1, 0.041), `014350103_6` (rank3, 0.0014), `014350158_8` (rank3, 0.0065), `021790169_3` (rank2, 0.0007), `001450109_7` (rank3, 0.283) |
| IDENTITY_STRONG (identity, span support ≥ 0.30) | 4 | `child_07_seven` (rank1, 0.629), `014180143_15` (0.682), `014190172_7` (0.820), `014350146_16` (0.956) |
| SOFT_SIMILARITY (non-identity similarity ≥ 0.35) | 3 | `child_01_four` (/l/→/ɹ/), `014350017_18` (max 0.005), `014470149_5` (max 0.040) |

Flags: **competitor_conflict 10/14** (the strongest non-target class beats the target in the
span-mean), **blank_dominated 12/14** (blank ≥ 0.90 with target post < 0.05), **window_dependent 1/14**
(`child_01_four`). No vague "OTHER" rows. The weak-support false accepts are identity/similarity
decisions made where blank dominates and the competitor often wins; the four strong-support false
accepts are encoder/acoustic disagreements (model confident, human absent) — they would remain after
any acceptance-logic fix.

## 6. Experiment 4 — present-side cost

Across all labeled true presents (528 in LWE+dev+test): removing identity credit (B)
**rejects 446 true presents**, changes nothing for 34, and correctly rejects 11 false presents;
1 token (`child_07_one`) is not accepted by production at all. Focus cases
(`EXPERIMENT_RESULTS.json` → `experiment4_present_cost`):

| case | truth | prod | identity rank | span post | span max | B (no identity) | D τ=0.1 | D τ=0.3 |
|---|---|---|---|---|---:|---|---|---|---|
| child_01_eight | P | exact | rank0 | 0.021 | 0.592 | rejects true | keeps | keeps |
| child_01_nine | P | exact | rank1 | 0.0011 | 0.0155 | rejects true | rejects | rejects |
| child_01_seven | P | exact | rank3 | 0.0055 | 0.059 | rejects true | rejects | rejects |
| child_01_ten | P | exact | rank0 | 0.0043 | 0.029 | rejects true | rejects | rejects |
| child_02_ten | P | exact | rank1 | 0.0037 | 0.066 | rejects true | rejects | rejects |
| child_04_four | P | exact | rank3 | 0.0030 | 0.090 | rejects true | rejects | rejects |
| child_06_six | P | exact | rank3 | 0.0004 | 0.0007 | rejects true | rejects | rejects |
| child_07_one | P | **miss** | none | 0.0011 | 0.013 | rejects (already) | rejects | rejects |
| child_01_four | A | soft | none (similarity) | 0.0040 | 0.036 | **keeps false** | rejects | rejects |
| child_02_four | A | exact | rank0 | 0.0093 | 0.154 | rejects false | keeps | rejects |
| child_03_four | A | exact | rank0 | 0.0048 | 0.056 | rejects false | rejects | rejects |
| child_07_seven | A | exact | rank1 | 0.0154 | 0.629 | rejects false | **keeps false** | **keeps false** |

The identity clause is the recall mechanism for weak/no-evidence true presents (nine of the listed
cases have span max < 0.10) and the false-accept mechanism for weak false presents — the same
evidence-free region. `child_07_seven` (strong false peak) survives every support gate.

## 7. Experiment 5 — identity margin

`IDENTITY_MARGIN_ANALYSIS.csv` (span-mean margin = target span-mean posterior − strongest non-target
class span-mean posterior; peak margin = target − competitor at the in-span peak):

| group | n | margin median | margin < 0 | post < 0.01 | blank ≥ 0.90 | identity accepted | identity rank > 0 | peak blank ≥ 0.5 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| true PRESENT | 528 | +0.0225 | 169 (32.0%) | 136 | 314 | 456 | 97 | 128 |
| true ABSENT | 29 | −0.0059 | 25 (86.2%) | 25 | 25 | 11 | 7 | 21 |
| human UNCERTAIN (word-level, all) | 6 | −0.00004 | 4 | 4 | 6 | 3 | 1 | 3 |

Discrimination AUC (identity margin / span post / span max): LWE 0.682 / 0.760 / 0.781; SO762 dev
0.770 / 0.813 / 0.792. **Identity margin is a weak discriminator**; span-max is no better (1.9.21
conclusion holds). 32% of true presents are accepted with a *negative* margin; 20.8% of all identity
accepts are not top-1; identity is decided in blank-dominated, low-posterior frames. Identity credit
is therefore **not meaningful acoustic evidence** — it is a similarity-based winner among
low-confidence candidates — but it is the only evidence weak true presents have.

## 8. Experiment 6 — window identity causality

`WINDOW_IDENTITY_ANALYSIS.csv` (557 labeled tokens × 4 windows):

| classification | n | share |
|---|---:|---:|
| IDENTITY_STABLE_PRESENT | 447 | 80.3% |
| IDENTITY_STABLE_ABSENT | 46 | 8.3% |
| IDENTITY_WINDOW_SENSITIVE | 42 | 7.5% |
| IDENTITY_UNSTABLE | 14 | 2.5% |
| IDENTITY_APPEARS_ONLY_OUTSIDE_CORE_SPAN | 7 | 1.3% |
| IDENTITY_FROM_NEIGHBOR_PHONE | 1 | 0.2% |

11.5% of tokens have window-dependent identity. Examples: `000010095_12` (truth present, production
miss) has identity only in `raw`; `000060031_2` (production miss) has identity in raw/pad100/pad250
but not full; `000490088_2` (accepted) has identity in full only. Some production decisions are
therefore made on one lucky crop.

## 9. Experiment 7 — human label sufficiency

| label set | present | absent | uncertain | /r/ |
|---|---:|---:|---:|---:|
| LWE blind final consonants (WP-1.9.12) | 16 | 12 | 2 (AMBIGUOUS final-consonant; 6 word-level) | 1 P / 5 A |
| SO762 dev (score ≥0.5 = present) | 285 | 17 | – | few |
| SO762 test (speaker-disjoint) | 227 | 0 | – | few |

Additional labels needed (exact request, no fabrication): (1) **10–20 confidently-present weak final
consonants, /r/ first** — to separate "identity-weak true present" from "identity-weak false accept";
(2) **rank-2–5 identity tokens** (104/501 accepts are not top-1) labelled to determine how often the
similarity pick is right; (3) more **/r/ absent** examples (currently 5). The 20-item candidate pack
from WP-1.9.21 (`LABEL_CANDIDATES.csv`) is ready; no reviewer was available in-session, so 0 new
labels were collected.

## 10. Experiment 8 — B2 readiness

See `B2_READINESS.md`. Result: **B2 NOT READY** (identity removal destroys FRR-first; acceptance
defect unresolved; /r/ and weak-present labels insufficient). This is not permission to train.

## 11. Root-cause update and final status

1. **Identity contribution quantified:** production acceptance is identity (95.1%) + similarity
   (4.9%); the posterior path never fires; 20.8% of identity accepts are rank 2–5.
2. **False-acceptance mechanism quantified:** all 14 false accepts are identity/similarity
   decisions; 10/14 weak-support, 12/14 blank-dominated, 10/14 competitor-conflict, 4/14 strong
   encoder-side false evidence.
3. **Identity is load-bearing:** removing it rejects 446 true presents (FRR 0.09 → 0.94 on dev;
   0.06 → 1.0 on LWE) to remove 11 false accepts, and does not remove similarity-path false accepts.
4. **Therefore both halves are required:** a redesigned acceptance rule (rank/margin/blank-aware,
   with a legitimate UNCERTAIN outcome, FRR-first) *and* better encoder evidence for weak finals.
   Neither alone satisfies FRR-first.

**FINAL STATUS: `ACCEPTANCE_LOGIC_AND_ENCODER_BOTH_REQUIRED`**

## 12. Limitations

- SO762 ground truth is a phone-score threshold; the held-out test set has no absent tokens; dev has
  only 17 absent phones (plus 12 LWE absent), so FAR estimates are coarse.
- The span-mean top-5 list is reconstructed in this session; cross-session encoder numeric drift
  (3.2% of WP-1.9.21 token-windows) means the frozen *stored* summaries can differ by small amounts,
  but the 609/609 and 65/65 reproduction checks were exact within this session.
- No human reviewer was available; label sufficiency remains the binding external-validation limit.
- Margin/AUC analysis uses the primary (full) window; window causality is reported separately.

## 13. Files

```
Research/Speech/Phase1_9_22/
  ACCEPTANCE_IDENTITY_AUDIT.md          this report
  ACCEPTANCE_VARIANT_RESULTS.csv        Experiment 2 (all variants × datasets)
  FALSE_ACCEPTANCE_DECOMPOSITION.csv    Experiment 3 (14 false accepts)
  IDENTITY_MARGIN_ANALYSIS.csv          Experiment 5 (per-token margins/groups)
  WINDOW_IDENTITY_ANALYSIS.csv          Experiment 6 (cross-window identity)
  B2_READINESS.md                       Experiment 8 checklist
  EXPERIMENT_RESULTS.json               machine-readable summary
  NEXT_GATE_DECISION.md
  artifacts/experiment1_reproduction.json
  artifacts/spanmean_decision_{lwe,so762_dev,so762_test,so762_absent_dev}.csv
  experiments/ recon_reproduce.py, rebuild_spanmean_decision.py, analyze_acceptance.py,
               inspect_spanmean.py, inspect_acceptance.py
```

No production file modified; no model trained; no Unity code touched; no raw audio stored.
