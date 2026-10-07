# B2 DESIGN OPTIONS — WP-1.9.24

NO TRAINING. This document only defines and ranks candidate B2 designs by the evidence from
WP-1.9.16–1.9.24. No fine-tuning, head training, LoRA, adaptation or model replacement is performed.

## Evidence base used for ranking

| WP | finding |
|---|---|
| 1.9.16 | frozen encoder + supervised child head (B1): final-consonant recall 93.8% → 75% ⇒ not adopted |
| 1.9.17–1.9.20 | alignment is not the binding constraint; 8/9 claimed alignment failures were aggregation |
| 1.9.21 | no FRR-first-safe support-aggregation rule; one-frame finals are real |
| 1.9.22 | identity acceptance is load-bearing (removal rejects 446 true presents); 20.8% of identity accepts are rank 2–5 |
| 1.9.23 | 0/68 acceptance rules FRR-first safe; TYPE A (10) / TYPE B (4) false accepts split |
| 1.9.24 | 87/528 labeled presents have max_A < 0.02 (missing evidence); cross-encoder evidence is representation-dependent; liquids (/r/, /l/) weakest; TYPE-B false evidence unconfirmed (label-limited) |

## Option comparison

| option | what it changes | target failure | required data / labels | compute | license | risk | cannot solve |
|---|---|---|---|---|---|---|---|
| **B2-A** same encoder + child-specific head | decision head on frozen features | phone-class calibration, rank>1 identity | 300+ child finals with phone labels; speaker-disjoint | CPU-feasible head only | local datasets only | **high** — direct negative evidence (1.9.16 recall 93.8→75) | missing evidence in frozen features; strong false peaks |
| **B2-B** phone-discrimination calibration | temperature/threshold per phone class on frozen evidence | s↔z, m↔n, v↔f confusions; weak/strong separation | 200+ labeled finals per affected class; no new audio | CPU | local | medium — calibration cannot create evidence | 16.5% no-evidence tokens; /r/ |
| **B2-C** encoder adaptation with child speech | w2v2-large fine-tune on child speech | missing evidence for weak finals; domain gap (adult multilingual → child) | 10–30 h child speech with phone-level labels; ≥30–50 speakers; speaker-disjoint; ages 4–6 preferred | **GPU required** (none on ASUS); 16 GB RAM insufficient for w2v2-large training | child corpora blocked/restricted (see `LICENSE_AND_DATA_BLOCKERS.md`) | high — data/licence blocked; risk of catastrophic forgetting | strong false peaks may persist; label quality |
| **B2-D** alternative speech encoder | replace encoder (e.g. lv-60-espeak, allophant, CUPE) | representation-dependent evidence | small labelled diagnostic set for selection | CPU for inference; GPU for any adaptation | lv-60 apache-2.0 (local); CUPE packaging non-standard; allophant incomplete | medium — 1.9.24 shows cross-encoder disagreement both ways; no drop-in winner | does not fix label ambiguity or acceptance logic |
| **B2-E** hybrid acoustic + phonetic evidence | add landmark/phonetic features (energy/voicing/frication) beside CTC posteriors | liquids, weak frication, one-frame finals, blank-dominated endings | 100–300 labeled finals for feature validation; acoustic features already computed in WP-1.9.12 | CPU | local | medium-low — no training required for feature engineering; must not become another black box | s↔z-type confusions if acoustically identical; label ambiguity |

## Ranking (by evidence, not intuition)

1. **B2-E (hybrid acoustic + phonetic evidence)** — directly targets the observed failures (liquids,
   weak frication, one-frame finals), needs no training, uses already-computed acoustic features and
   local data, and can be validated FRR-first on the existing cache + new labels. Lowest risk.
2. **B2-D (alternative encoder)** — the counterfactual already demonstrates representation dependence
   (3/10 no-evidence presents recovered by lv-60); a small, licensed, local comparison is feasible
   without training. But it loses some primary-strong evidence, so it must be evaluated, not adopted.
3. **B2-B (phone-discrimination calibration)** — cheap and safe, but addresses confusions/thresholds,
   not missing evidence; useful as a companion to E, not a solution alone.
4. **B2-C (encoder adaptation)** — highest theoretical ceiling, but blocked by data/licence and by the
   absence of a GPU; 1.9.16 also warns that child adaptation did not improve final-consonant recall.
5. **B2-A (child head on frozen encoder)** — deprioritised by direct negative evidence (1.9.16).

## B2 design readiness

- **B2 TRAINING: NO.**
- **B2 DESIGN: NOT READY** — the target mechanism is specified (missing evidence for weak finals,
   especially liquids; representation instability), and B2-E/B2-D are defensible research directions,
  but a defensible *executable* design requires (a) the 10–20 new confident labels for validation,
  (b) licensed child-speech data, and (c) a GPU for any adaptation. None are available now.
- **B2 TARGET:** missing/weak acoustic evidence for final consonants in child speech (liquids /r/, /l/
  first; one-frame finals; blank-dominated endings); not the unconfirmed strong false-evidence claim.
- **B2 KNOWN LIMITATION:** even a better encoder cannot guarantee fixing strong false peaks or
  label-ambiguous cases, and cannot repair acceptance-logic overlap.
