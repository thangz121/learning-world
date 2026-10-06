# MODEL RUNTIME + LICENSE AUDIT

**Script:** `experiments/runtime_license.py` · **Artifact:** `artifacts/runtime/runtime_license.json`

---

## 1. Runtime (ASUS, Windows, CPU-only)

| metric | value |
|---|---|
| model file size | `pytorch_model.bin` 1,263,535,127 B (~1.18 GiB); safetensors copy same |
| head size | 14,505,983 B (~13.8 MB), ~3.6M params |
| cold model load | 13.6 s |
| warm `soft_match` (short word) | 0.585 s |
| peak working set after one inference | 1,754 MB (~1.75 GB) |
| baseline throughput | 0.29–0.32 s/utt (short words), ~0.75 s/utt (so762 sentences ~3.3 s) |
| adapted overhead | +0.25–0.35 s/utt (head forward + same post-processing) |
| torch | 2.14.1+cpu; no GPU on ASUS |

Tradeoff statement: the adapted head is small and cheap, but it does not improve the target
objective (final consonants / external AUC), so its cost cannot be justified; the frozen model
stays the reference. Encoder-level adaptation (the likely path to real gains) would need GPU
and is out of scope for local CPU.

## 2. License audit (spec §21)

| asset | license | training | commercial | verdict |
|---|---|---|---|---|
| `facebook/wav2vec2-xlsr-53-espeak-cv-ft` | Apache-2.0 (verified 2026-10-02) | allowed | allowed | OK |
| speechocean762 (SLR101) | CC BY 4.0 (`openslr.org/101`, fetched 2026-10-03) | allowed | allowed (attribution) | OK |
| SIAK | CC-BY-ND-4.0; README allows commercial model building/evaluation, no unrelated derivatives | allowed per README | nuanced — **legal review still open** | used only as auxiliary acoustic data |
| Zenodo 200495 (LWE corpus) | CC-BY-4.0 | **not used for training** | allowed | test-only |
| Moonshine-tiny (ASR, supporting) | MIT (Phase 1.1 registry) | n/a | allowed | OK |

All models/datasets used for the B1 experiment are license-compatible for research and
commercial evaluation; the only unresolved nuance is SIAK's CC-BY-ND interpretation for
model weights, which was flagged in 1.9.14/1.9.15 and remains open. No decision in this phase
depends on shipping SIAK-trained weights.

## 3. Reproducibility

- Model revision: `facebook/wav2vec2-xlsr-53-espeak-cv-ft`, local snapshot
  `2c733782da5604684829819a5eb744c193fe9398`.
- Training seed 1516, fixed deterministic selection; scripts and configs committed.
- Feature caches (~520 MB) were deleted after training to keep the repository small;
  `train_head.py` regenerates them (~25 min CPU).
