# PHASE 1.9.16 — CHILD PHONE MODEL ADAPTATION RESEARCH

**Date:** 2026-10-03 · **Machine:** ASUS (CPU-only) · **Branch:** `ux/math-arenas-hotfix-20260930`
**Base:** Phase 1.9.15 `3f62ef4` · **Flags (unchanged):** `production_vad=false ·
router_locked=false · unity_integrated=false · scorer_modified=false ·
production_window_locked=false`

## Decision

### B = ADAPTATION_PROMISING_BUT_INSUFFICIENT

The smallest reasonable child-domain adaptation (B1: frozen encoder + trainable CTC head,
quality-filtered targets) **partially works and partially regresses**; it is not a safe
replacement for the frozen model under FRR-first. The frozen model remains the reference.
A separate child-phone-model phase with encoder-level adaptation and better data is justified,
with the conditions in §8.

---

## 1. What was asked

Whether the current phone evidence model is a major bottleneck, and whether a child-adapted
phone model is actually better. Training was allowed only if the data audit supported it;
LWE human-reviewed data had to remain external test; no production change.

## 2. Feasibility audit (details: `FEASIBILITY_AND_INVENTORY.md`)

| dataset | license | child data | phone labels | verdict |
|---|---|---|---|---|
| speechocean762 (SLR101, local) | CC BY 4.0 | 122 children, ages 6–15; train 58 spk/1,160 utt; test 64 spk/1,280 utt (speaker-disjoint) | **per-phone human scores 0/1/2 (5 experts)** + canonical ARPAbet; 94,445 judgments | **usable** for quality-filtered targets |
| SIAK (local) | CC-BY-ND-4.0 (model build/eval allowed; legal review open) | 172 spk, 16,308 utt, ages 4–12 | none (expert rating only) | auxiliary acoustic data (rating-weighted) |
| Zenodo 200495 (LWE corpus) | CC-BY-4.0 | 11 children | word transcripts + LWE human verdicts | **test only** (never trained) |
| MyST/CSLU/CMU Kids/OGI/NOCASA | — | not downloaded | some have phone labels | future augmentation |

- **No dataset provides human phone transcriptions of what the child actually said.**
  Supervised phone-recognition training is therefore not possible with local data.
- **Quality-filtered head adaptation (B1) is supported**: train on canonical targets, weight
  by the human phone scores (so762) / expert rating (SIAK), and mask unreliable items.
- CPU-only: full fine-tuning of a 317M-parameter encoder is not feasible locally; the
  frozen-encoder + head is the smallest testable adaptation (spec §9 B1).

## 3. Baseline (frozen model) — zero-shot benchmark

Details/artifacts: `ZERO_SHOT_BASELINE.md`, `artifacts/zeroshot/`.

| corpus | phones | exact | exact+soft | miss | notes |
|---|---:|---:|---:|---:|---|
| so762 test children (64 spk, 1,280 utt) | 21,617 | 75.4% | 81.9% | 18.1% | human phone scores available |
| SIAK test speakers (482 utt) | 1,758 | 69.1% | 76.0% | 24.0% | canonical agreement only |

- so762 evidence vs human phone score: score2 (correct) exact 77.7% / miss 16.3%;
  score1 (accented) 41.9% / 43.4%; score0 (incorrect/missed) 23.9% / 61.1%;
  AUC(posterior, score2 vs score0) = **0.825**.
- Word-final consonants (so762): score2 exact 87.3%; score0 exact 33.3% (miss 50%).
- Runtime: so762 ~0.75 s/utt (3.3 s median audio), SIAK 0.32 s/utt, CPU-only.

## 4. B1 adaptation (what was trained)

- Encoder **frozen** (`wav2vec2-xlsr-53-espeak-cv-ft`, snapshot `2c733782…`); head =
  2×Conv1d (1024→512→392, ~3.6M params, 13.8 MB) trained with CTC.
- Targets: canonical phones (so762 `ref-phones`; SIAK CMUdict). Weights: so762 mean human
  phone score / 2; SIAK expert rating / 100 (score ≥ 60 only). Fit: 1,160 so762 train-child
  utt + ~1,300 SIAK train utt; validation: 10 held-out so762 train-child speakers;
  best epoch 2 (val CTC 16.37), later epochs overfit.
- **No LWE audio or label used in training or model selection.**

## 5. A/B results (same audio → same soft-v2 pipeline)

Full table/artifacts: `AB_ADAPTATION_RESULTS.md`, `artifacts/ab/ab_metrics.json`.

| evaluation set | metric | baseline | adapted | direction |
|---|---|---:|---:|---|
| LWE human labels (n=48) | FRR (correct → <50) | 31.0% | 20.7% | better |
| | FAR (incorrect → ≥50) | 38.5% | 53.9% | worse |
| | AUC correct vs incorrect | 0.659 | **0.625** | **worse** |
| LWE final consonants (n=28) | human-present recall | 93.8% | **75.0%** | **worse (FRR 6.3%→25%)** |
| | human-absent detection | 58.3% | 33.3% | worse |
| SIAK test speakers (482) | Pearson / Spearman | 0.307 / 0.272 | **0.355 / 0.333** | better |
| | FRR-proxy (≥80 → <50) | 12.2% | **6.6%** | better |
| SIAK ages 4–6 (99, 5 spk) | Pearson / FRR-proxy | 0.184 / 42.9% | **0.373 / 10.7%** | better (limited) |
| so762 test children | all-phone exact (score2) | 77.7% | 78.5% | ~flat |
| | all-phone exact (score1/0) | 41.9% / 23.9% | 51.6% / 32.2% | better |
| | word-final consonant exact (score2) | 87.3% | **78.6%** | **worse** |
| | AUC phone score2 vs score0 | 0.825 | 0.808 | slightly worse |

Category forensics (so762, ≥30 samples): improved **ə +49.8 pts**, ð +15.1, θ +13.7, ʊ +13.1,
aɪ +12.0, ɪ +11.5, uː +9.9, ɹ +6.0; degraded **ɑ −26.0, w −25.9, p −23.3, h −15.9, z −13.8,
k −12.8, t −12.6, iː −11.3**.

Historical replay (12 PHONE_MODEL_ERROR + 6 SCORER_MISS, details `REPLAY_AND_GENERALIZATION.md`):
PME 5 FIXED / 2 PARTIALLY_FIXED / 3 UNCHANGED / 1 PARTIALLY_REGRESSED / 1 REGRESSED;
SCORER_MISS 5 UNCHANGED / 1 REGRESSED — the deleted-final /r/ "four" class is still not fixed.

## 6. FRR-first verdict (mandatory rule)

- LWE human-correct overall FRR improved (31.0%→20.7%), **but** the final-consonant class —
  the reason the phase exists — got worse (present recall 93.8%→75%). Under the program's
  rule, a candidate that increases false rejection of a human-correct class is not an
  improvement.
- External discrimination did not improve (AUC 0.659→0.625); FAR rose to 53.9%.
- The adapted head also does not fix the scoring misses that motivated the phase and does not
  remove the CTC-alignment deletion failure (unchanged by design).
- The adapted model is not proposed for any use. It is evidence about what a frozen-encoder
  head can and cannot do on this data.

## 7. Assessability interaction + robustness

- Assessability (H): on the 115 independent review clips with canonical targets, "confident
  evidence on human-not-assessable" is **2 vs 2** baseline/adapted — the head does not
  increase confident hallucination on that small set. Assessability, phone evidence and
  pronunciation stay separate layers.
- Robustness (J, synthetic stress only): both models are gain-invariant at −12 dB; +6 dB
  clips (clipping) and telephone-band/noise conditions degrade both; the adapted head keeps
  ~9 points higher mean score but its final-consonant recall remains below baseline in every
  condition (clean 66.7% vs 75%).

## 8. What would be required for a genuine child phone model (next phase conditions)

1. **Phone-level ground truth or broader quality-filtered corpora** — so762 alone is
   Mandarin-L1, 58 child training speakers; MyST/CSLU Kids/OGI (download + license review)
   or a Vietnamese-L1 child corpus would address the LWE population gap.
2. **Encoder-level adaptation** (top layers or LoRA) — requires GPU; head-only adaptation on
   frozen features trades one phone class for another.
3. **Category-balanced objective** — the observed trade (vowels/schwa up, stops/fricatives/
   finals down) is a training-objective problem, not only data volume.
4. **Deletion-aware alignment stays in the scorer path** — a better phone model does not fix
   the forced-alignment deletion blind spot by itself.
5. **Evaluation standard unchanged**: speaker-disjoint, FRR-first on human-correct child
   speech, LWE external held out.

## 9. Artifacts

```
Research/Speech/Phase1_9_16/
  PHASE_1_9_16_REPORT.md          this file
  FEASIBILITY_AND_INVENTORY.md    datasets, licenses, phone inventory, data quality, leakage
  ZERO_SHOT_BASELINE.md           Experiment A/C baseline + confusion forensics
  AB_ADAPTATION_RESULTS.md        Experiment B/D/F/G A/B pipeline + comparison table
  REPLAY_AND_GENERALIZATION.md    Experiment E + I
  ASSESSABILITY_AND_ROBUSTNESS.md Experiment H + J
  RUNTIME_AND_LICENSE.md          size/latency/RAM + license audit
  experiments/                    so762_manifest, feasibility_audit, inventory_audit,
                                  quality_audit, zeroshot_phone_benchmark, train_head,
                                  eval_ab, ab_summary, failure_replay,
                                  assessability_stress, runtime_license, build_manifests
  artifacts/audit|inventory|quality|zeroshot|ab|replay|generalization|stress|runtime
  artifacts/metrics.json          consolidated machine-readable metrics
  manifests/provenance.json       input/output hashes + revision
```
No production code, scorer, VAD, router, window or Unity behavior was modified. The adapted
head exists only as a research artifact (`artifacts/ab/head.pt`); the frozen model remains the
default. Human review remains authoritative; no LWE label was invented or used for fitting.
