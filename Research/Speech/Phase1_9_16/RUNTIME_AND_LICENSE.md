# RUNTIME AND LICENSE — Phase 1.9.16

## 1. Runtime (measured on MAYNODE, RERUN)

| item | value |
|---|---|
| hardware | Xeon E5-2678 v3 CPU, 16 GB RAM, no CUDA (AMD GPU unusable for torch) |
| model size on disk | ~1.27 GB (`pytorch_model.bin` 1,263,535,127 B) |
| cold start (first load) | ~4 s CPU (`smoke_model.py`) |
| warm inference | 0.405 s/utterance mean over 482 SIAK clips (0.26–3.86 s audio) |
| RAM during inference | fits comfortably in 16 GB (single-utterance batching) |
| training capacity | frozen-encoder feature caching + small head (B1) is CPU-feasible; full fine-tuning on CPU is not credible without a hardware plan |

A CPU-only B1 was therefore never blocked on hardware — it is blocked on
labels and license (below). An accurate-but-impractical model question does
not arise: no model was trained.

## 2. License (verified, not assumed)

| item | license | verdict |
|---|---|---|
| `facebook/wav2vec2-xlsr-53-espeak-cv-ft` | Apache-2.0 (cardData, verified 2026-10-02) | CLEAR for research + adaptation |
| `transformers` / `torch` CPU | Apache-2.0 / mixed Apache-BSD-MIT | CLEAR |
| CMUdict | BSD-like (Phase 1.1 registry) | CLEAR for target derivation |
| SIAK (`rkarhila/SIAK`) | **CC-BY-ND-4.0** | **BLOCKED_PENDING_LICENSE_REVIEW for training** — building/evaluating speech-tech models not prohibited per README note, but fine-tuning on ND material may constitute a derivative; open since 1.9.14, still open |
| SIAK human labels in committed CSVs | project-internal derivatives (scores only, no audio) | CLEAR for validation/reporting |
| LWE child audio / review labels | project-internal, privacy-sensitive; raw audio never committed | validation-only under existing authorization; nothing new collected |
| CMU Kids mirrors | mirror claims unverified (MIT assert, 1 like) | NOT relied upon |

No production recommendation is made under ambiguous licensing. Decision F
was considered and rejected as the *primary* decision: runtime is adequate and
the model license is clear — the phase is blocked first by missing phone
labels (D), with the SIAK training-license review as the second gate.
