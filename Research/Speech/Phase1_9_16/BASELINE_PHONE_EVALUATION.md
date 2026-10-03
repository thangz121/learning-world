# BASELINE PHONE EVALUATION — Phase 1.9.16 (frozen, no training)

**Script:** `experiments/run_baseline.py`
**Artifacts:** `baseline_reproduction.csv`, `baseline_metrics.json`
Baseline: `facebook/wav2vec2-xlsr-53-espeak-cv-ft@2c73378` + `PhoneEvidenceV2@1.4.0`,
exact 1.9.15 call path. All numbers RERUN ON MAYNODE.

---

## 1. Reproduction (SIAK speaker-disjoint test, n=482)

| check | result |
|---|---|
| utterances scored / errors | 482 / 0 |
| soft-score exact agreement with 1.9.15 | **482/482** |
| wall time (CPU) | 197.9 s (0.405 s/utt) |
| FRR-proxy (soft<50 on rating≥80, n=196) | 12.24% (1.9.15: 12.2% — match) |
| mean confidence | 0.0436 (low absolute confidences are the known evidence weakness) |

The frozen baseline is bit-reproducible on MAYNODE. This is the fixed reference
every future child model must beat on unseen speakers.

## 2. What could NOT be measured (explicit, not hidden)

- **PER / phone accuracy / sub-del-ins / confusion matrix: NOT_COMPUTABLE.**
  SIAK provides expert ratings, not phone transcripts; LWE provides word verdicts,
  not phone transcripts. Any "PER" reported on these corpora would be computed
  against model-derived pseudo-labels (circular). Refused on methodology grounds.
- LWE-side recomputation: BLOCKED on gitignored raw audio
  (EXTERNAL_LWE_AUDIO_UNAVAILABLE). Instead verified committed determinism:
  archived vs 1.9.15-recomputed soft scores agree **80/80** exactly
  (`external_lwe_features.csv`). The LWE baseline numbers stand as committed.

## 3. Baseline characterization for the A/B harness

- Score scale is compressed and low-confidence on child speech (mean conf 0.044);
  the 12.2% FRR-proxy and 31.0% LWE FRR (1.9.15) are the numbers to beat.
- Same audio → same alignment (`ctc_forced_v1`) → same soft-v2 is implemented and
  validated; only the phone-evidence source will vary when a child model exists.
