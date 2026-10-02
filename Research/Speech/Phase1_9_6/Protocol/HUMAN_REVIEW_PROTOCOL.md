# Human Review Protocol — Phase 1.9.6 Hybrid Rescue Verification

## Question under test

When Silero thr=0.5 yields **0** segments on IMG_0639, do the **28** `hybrid_score` candidates contain **human-audible SPEECH**?

## Authority ranking (do not invert)

| Rank | Source | Role |
|---:|---|---|
| 1 | **HUMAN_LABEL** (listen) | Authority for candidate speech presence |
| 2 | Silero 0.5 | Baseline (failed on IMG) |
| 3 | hybrid_score | Research candidate under test |
| 4 | energy | WEAK_PSEUDO_REF only — **not GT** |
| 5 | ASR (e.g. Moonshine) | Auxiliary only |

**ASR_EMPTY ≠ NON_SPEECH.**

## Scope limitation (mandatory)

This review judges **only the 28 hybrid candidate clips**.

It does **NOT** provide:

- full-file speech timeline for 219s  
- full-file recall / false-negative rate  
- production VAD accuracy  

`HUMAN REVIEW CANDIDATES ≠ FULL-FILE VAD GROUND TRUTH`

## Labels

| Label | Meaning |
|---|---|
| SPEECH | Clear human voice in the clip (any language) |
| NON_SPEECH | No human voice (noise, music, click, room, device, silence, …) |
| MIXED | Speech + non-speech, or speech only partial |
| UNCERTAIN | Cannot decide reliably |

Do **not** force SPEECH/NON_SPEECH when unclear.

## Bias control

**Primary review = MODE A** (`HumanReview/review.html`):

- Shows: clip id, source name `IMG_0639`, time range, duration, player, label choices  
- Hides: hybrid score, energy, spectral, Silero result, ASR, “continuous-energy”, pseudo-ref, any “expected speech” wording  

**MODE B** (`review_mode_b.html`): optional technical details after MODE A decisions.

## Procedure

1. Open `HumanReview/review.html` in a browser (local file OK; clips must sit beside HTML).  
2. Use headphones if possible.  
3. Listen to **all 28** clips — no sampling.  
4. Choose one label per clip; notes optional.  
5. Click **Export labels CSV** → save as `Results/Human_Review_Labels_Filled.csv`.  
6. Run:  
   `python Research/Speech/Phase1_9_6/scripts/analyze_human_labels.py`  
7. Reference type: **SINGLE_REVIEWER_REFERENCE** (not ground truth).

## Preview padding

Clips include **±50 ms preview pad** for listenability.

- `raw_start_s` / `raw_end_s` = detector timestamps (unchanged)  
- `preview_start_s` / `preview_end_s` = export window  

Do **not** feed tight raw crops into the pronunciation scorer (prefer full utterance or ≥250 ms pad).

## Decision gate (after complete labels)

| Code | When |
|---|---|
| **A. HUMAN_VERIFIED_PROMISING** | Substantial SPEECH among candidates; audio clear |
| **B. HUMAN_VERIFIED_WEAK_OR_MIXED** | Some speech but heavy fragmentation/noise/mixed |
| **C. HUMAN_REVIEW_INCONCLUSIVE** | Mostly MIXED/UNCERTAIN, unclear audio, **or review incomplete** |

Until all 28 labels exist → **C** / `HUMAN_REVIEW_PENDING`.
