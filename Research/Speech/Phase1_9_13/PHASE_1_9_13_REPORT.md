# PHASE 1.9.13 REPORT — Pronunciation System Forensics (Technology Harvest)

**Date:** 2026-10-03  
**Flags:** production_vad=false · router_locked=false · unity_integrated=false ·
scorer_modified=false · production_window_locked=false  
**Decision:** **A = CLEAR_ARCHITECTURE_INSIGHTS**

---

## 1. Executive Summary

Ten external systems were investigated with primary sources, one local run (OpenPronounce, 16
cases), one benchmark reproduction (VoxTutor), one dataset acquisition (SIAK, 16,308 child
utterances), and source audits of two browser systems (speak-better-than-ai, slip).

Three findings materially change how we should think about the LWE speaking system:

1. **Attempt/fidelity detection is a first-class concept in mature systems** (Speechace:
   `fidelity_class` CORRECT/NO_SPEECH/INCOMPLETE/FREE_SPEAK; SIAK: rejected class; Microsoft:
   Completeness + miscue). LWE has no such layer — a major source of our 1.9.9–1.9.12 confusion.
2. **Our CTC-span-anchored acoustic features are a known failure mode with a published fix**:
   GOP over full-sequence posteriors (GOP-AF, arXiv 2507.16838, evaluated on CMU Kids) and
   blank-interleaved Viterbi alignment with an explicit `present=false` / deletion state
   (slip, speak-better-than-ai, Microsoft `Omission`).
3. **No external system has solved ~4yo pronunciation scoring.** OpenPronounce (MIT, local)
   scored a human-confirmed-correct child token at 2.46; SIAK's child model reaches only
   0.59–0.61 correlation with a single annotator. Child scoring remains our own problem —
   but SIAK gives us 16,308 labeled child utterances (age 4–6: 594) for calibration.

No production changes were made. Everything below is evidence-tagged (E0–E6).

---

## 2. Systems Actually Investigated

01 Speech Blubs · 02 AI Speak/M-Speak · 03 ELSA · 04 Microsoft PA · 05 Speechace ·
06 OpenPronounce · 07 speak-better-than-ai · 08 Alignment-free line (GOP-AF + VoxTutor) ·
09 SIAK · 10 slip.

---

## 3. Systems Successfully Played/Tested

- **None of the consumer products could be played** from this environment (mobile apps,
  accounts, subscriptions, microphones, no browser automation).
- Speech Blubs / M-Speak / ELSA / Microsoft PA / Speechace: BLOCKED at login/subscription/
  API-key/payment boundaries — documented per system, not bypassed.

---

## 4. Systems Successfully Run Locally

| System | Evidence | What ran |
|---|---|---|
| OpenPronounce (MIT) | **E4/E6** | 16 corpus cases through the full pipeline (isolated venv); raw JSON archived |
| VoxTutor (MIT) | **E4** | `python -m evals.harness` reproduced the 2×2 dissociation exactly |
| SIAK dataset | **E4 (data)** | 16,308 flac downloaded + inventoried (ages, languages, scores) |

OpenPronounce key results (E6 on our audio): adult red/cat/red apple = 100; blue 23, book 25;
wrong-word 6.8; silence 0.0; child missing-final-/r/ "four" 12–25 (detected); **human-correct
child token child_07_one = 2.46 (false low)**.

---

## 5. Systems Blocked

| System | BLOCKED_REASON |
|---|---|
| Speech Blubs | NO_FREE_ACCESS (mobile app + subscription; no web/API) |
| AI Speak / M-Speak | NO_FREE_ACCESS (app + account; no API) |
| ELSA | API_KEY_REQUIRED (partner token); app needs mic/account |
| Microsoft PA | PAYMENT_REQUIRED (Azure account payment method) |
| Speechace | API_KEY_REQUIRED (trial key by request/approval) |
| speak-better-than-ai | HARDWARE_LIMITATION (browser + mic; no automation) |
| slip | HARDWARE_LIMITATION (browser + mic) |
| SIAK scoring | MODEL_UNAVAILABLE (no released code; store apps stripped) |

Each block was documented with what the human must do; no bypass attempted.

---

## 6. Common Architecture Patterns

```
MIC → EOS/VAD → (fidelity gate) → acoustic model → phoneme evidence
    → alignment OR alignment-free comparison → scoring model → calibration → feedback
```

- Commercial (ELSA, Speechace, Microsoft): cloud, phoneme-level, scoring + prosody/fluency.
- Open local (OpenPronounce, speak-better-than-ai, slip): wav2vec2-lv-60-espeak-cv-ft +
  espeak-ng G2P + DTW/Viterbi + simple scoring; browser/CPU friendly.
- Research (GOP-AF, SIAK, discrete-token surprisal, WavLM-DTW): no forced aligner or
  alignment-free; native-only training; lightweight regression.

---

## 7. Child-Specific Architecture Patterns

- Only **SIAK** is child-specific: child data, short single words, star scoring, rejected class.
- Commercial child apps (Speech Blubs, M-Speak) reward **attempts**, not phoneme accuracy.
- No inspected system exposes a child acoustic model or child calibration for reuse.
- Conclusion: child-specific handling is a genuine gap, not a solved problem.

---

## 8. Fidelity / Attempt Detection Findings

| System | Mechanism | Evidence |
|---|---|---|
| Speechace | `fidelity_class` = CORRECT / NO_SPEECH / INCOMPLETE / FREE_SPEAK; unfaithful → reduced scores + score_issue_list | E1 |
| SIAK | rejected class: silence, interrupted, wrong word, spoken noise, lack of effort (training labels); 2017 planned restricted-grammar effort check | E2 |
| Microsoft | CompletenessScore (word ratio) + miscue Omission/Insertion; silence → no text | E1 |
| speak-better / OpenPronounce | none — silence just scores low | E3/E4 |

**This is the single most reusable concept for LWE.**

---

## 9. VAD / EOS Findings

- speak-better-than-ai (E3): RMS > 0.025 for 3 frames starts speech; stop after **1300 ms**
  silence; 16 kHz mono.
- SIAK (E2): server VAD detects when the child stops talking; score <1 s.
- ELSA (E2): server-side endpointing after a beep prompt; streaming.
- Our window/padding question is secondary to having an explicit attempt gate.

---

## 10. Phoneme Recognition Findings

- `facebook/wav2vec2-lv-60-espeak-cv-ft` is the de-facto open phone model: used by
  OpenPronounce (E3/E4), speak-better-than-ai (E3), slip (E3). ONNX/int8 (~318 MB) runs on CPU
  and in-browser.
- Commercial systems use proprietary models (ELSA end-to-end ASR; Speechace acoustic model).
- Microsoft exposes `NBestPhonemes` (top-5 ranked candidates with confidence) — the richest
  public per-phoneme evidence schema.

---

## 11. Alignment Findings

- slip (E3): blank-interleaved (2L+1) Viterbi CTC forced alignment (torchaudio-equivalent);
  **`present=false` when a phone had to be synthesized** — deletion representation.
- speak-better-than-ai (E3): custom DP with deletion sim=0; score = Σsim/len(expected).
- VoxTutor (E4): forced alignment survives speaking-rate warp; raw distance does not.
- SIAK/ELSA: GMM-HMM forced alignment in production (older generation).

---

## 12. Alignment-Free Findings

- GOP-AF (E2, arXiv 2507.16838): log posterior of the target phoneme over the **full**
  observation sequence with context — no committed segmentation; includes insertion/deletion;
  best MDD on **CMU Kids** and SpeechOcean762.
- OpenPronounce: FastDTW over wav2vec2 embeddings vs TTS reference (runs, but failed on child).
- WavLM-DTW templates (E2): ~5 native templates reach 95% of full-template performance.
- VoxTutor (E4): GOP normalization (likelihood-ratio) fixes channel noise; alignment fixes rate.

---

## 13. Acoustic Feature Findings

- GOP-Avg = mean log P(canonical | frame); GOP-ratio = mean(logP_canon − best non-blank)
  (slip, E3).
- OpenPronounce combines acoustic DTW (0.3) + phoneme error (0.4) + word error (0.3) (E3).
- VoxTutor shows raw distance = true error + within-phone variance; GOP subtracts variance (E4).
- Our 1.9.12 span-anchored features measured the wrong thing; this line explains why.

---

## 14. Final-Consonant Findings

- Microsoft `ErrorType=Omission` + low phoneme AccuracyScore is the mature representation (E1).
- slip `present=false` + GOP-ratio + severity (E3).
- speak-better-than-ai deletion sim=0 lowers score proportionally (E3).
- GOP-AF represents deletions without committing to a span (E2).
- Our 1.9.12 conclusion ("landmark anchoring needed") matches the external consensus.

---

## 15. Scoring Findings

- Range: 0–5 stars (SIAK), 0–100 (Speechace/OpenPronounce/speak-better), rubric mapping
  (IELTS/PTE/TOEIC/CEFR — Speechace/ELSA/Microsoft).
- Components: accuracy/fluency/completeness/prosody (Microsoft); pronunciation/intonation/
  fluency (ELSA/M-Speak); weighted sum (OpenPronounce 0.3/0.4/0.3).
- Child products emphasize **reward for attempt** over precision (Speech Blubs, SIAK 1–5 points
  "for any proper attempts").

---

## 16. Confidence Findings

- Microsoft: per-phoneme NBestPhonemes confidence scores (E1).
- slip: `lowConfidence` when span < 3 frames or phone absent; severity thresholds explicitly
  uncalibrated (E3).
- OpenPronounce/speak-better: rough token confidences (1/Σexp) — not calibrated (E3).
- No system exposes a well-calibrated confidence without its own score.

---

## 17. Human Calibration Findings

- SIAK (E2): single expert, 0–100 → 0–5 stars; system corr 0.59–0.61; speaker-disjoint splits;
  1,489 rejected items. Child L2, Finnish L1 + UK native.
- GOP-AF (E2): CMU Kids + SpeechOcean762.
- No child Vietnamese-L1 calibration data exists publicly.
- Our own human evidence (1.9.9–1.9.12) remains the only source for our population.

---

## 18. Window / Boundary Findings

- No inspected system exposes window/padding policy; they solve the problem with EOS/attempt
  gating instead (Speechace fidelity, SIAK VAD, ELSA endpointing).
- Our 1.9.11/1.9.12 window conclusion (no human-perceptible advantage; padding can rescue true
  errors) stands; external evidence suggests the window question is the wrong lever.

---

## 19. Privacy Findings

- Speech Blubs (E1): voice detection on-device; "WE WILL NOT SAVE NOR COLLECT ANY VOICE
  RECORDS"; TrueDepth on-device.
- OpenPronounce/speak-better/slip: fully local inference after model download.
- ELSA/Speechace/Microsoft: cloud audio; retention per vendor policy; child use requires
  DPA/privacy review.
- LWE architecture should prefer the on-device/no-retention pattern.

---

## 20. Licensing Findings

- Reusable code: OpenPronounce (MIT), speak-better-than-ai (MIT), VoxTutor (MIT) — all with
  dependency caveats (espeak-ng GPL-3.0, phonemizer GPL-3.0, Levenshtein GPL-2.0).
- SIAK dataset: CC-BY-ND-4.0 with explicit note that commercial model building/evaluation is
  **not prohibited**; no derivatives for unrelated works → legal review required.
- slip: **no license** → no code reuse.
- Models: wav2vec2-lv-60-espeak-cv-ft card to be verified before any commercial use.

---

## 21. Reusable Open-Source Components

OpenPronounce (MIT): DTW comparison, prosody extraction, phone comparison with leniency pairs.
speak-better-than-ai (MIT): EOS logic, deletion-aware DP scoring, local TTS/G2P stack.
VoxTutor (MIT): alignment/GOP regression harness.

---

## 22. Reusable Models

- wav2vec2-lv-60-espeak-cv-ft (phone CTC; verify card) — cross-check model.
- wav2vec2-large-960h (embeddings/word) — reference only.
- No child model available; SIAK dataset enables future training/calibration.

---

## 23. Reusable Algorithms

- Blank-interleaved Viterbi forced alignment with `present` flag (deletion representation).
- GOP-Avg + GOP-ratio (likelihood-ratio normalization).
- Alignment-free GOP (GOP-AF) for CTC models.
- DTW template comparison (OpenPronounce/WavLM).
- EOS energy-hangover constants.

---

## 24. Reusable Architecture Patterns

- Fidelity/attempt gate before scoring (Speechace).
- Error taxonomy: Omission/Insertion/Mispronunciation (+ prosody breaks) (Microsoft).
- NBestPhonemes ranked candidates (Microsoft).
- Streaming + server endpointing (ELSA) — cloud reference only.

---

## 25. Reusable UX Patterns

- Reward attempts, not precision (Speech Blubs, SIAK).
- Stars instead of 0–100 (M-Speak, SIAK).
- "Tap to scrub to the slipped sound" (slip).
- Retry-on-unfaithful instead of misleading score (Speechace).

---

## 26. Commercial APIs Worth Considering

| API | Why | Caveat |
|---|---|---|
| Microsoft PA | richest schema (phoneme + NBestPhonemes + Omission), pay-per-use | cloud, child privacy review |
| Speechace | explicit fidelity taxonomy | key by request, adult models |
| ELSA | pronunciation/intonation/fluency dimensions | partner token, adult models |

No adoption decision; evaluation only.

---

## 27. Things We Should STOP Doing

1. Treating a low score as evidence of pronunciation error without an attempt/fidelity state.
2. Anchoring acoustic features on CTC spans (1.9.12 failure).
3. Chasing window/padding policy as the primary lever.
4. Building confidence/scores without a deletion-aware alignment.

---

## 28. Things We Should NOT Reinvent

See `DO_NOT_REINVENT.md` (attempt detection, omission representation, alignment-free GOP,
EOS baselines, phoneme model, child UX).

---

## 29. Things We Still Need to Research

See `REMAINING_GAPS.md` (child calibration, child fidelity, Vietnamese-L1 data, local
child-safe stack, deletion-aware evidence in our pipeline).

---

## 30. Recommended Next Research Experiments

1. **Fidelity layer design** (research-only): define states (NO_SPEECH / INCOMPLETE /
   FREE_SPEAK / CORRECT) using our existing signals (energy, VAD, ASR text, expected text) and
   validate against our 1.9.9–1.9.12 human labels.
2. **Deletion-aware evidence**: implement blank-interleaved Viterbi + GOP-ratio on the 65
   final-consonant tokens (1.9.12) and test against the 28 human final-consonant labels.
3. **Cross-check model**: run wav2vec2-lv-60-espeak-cv-ft (via OpenPronounce, already installed)
   on the 28 final-consonant human labels.
4. **SIAK calibration**: after legal review, evaluate our scorer against SIAK child scores
   (age 4–6 subset first).

---

## 31. Production Changes Proposed

**Proposal only — nothing implemented.**

| proposal | why | risk | validation |
|---|---|---|---|
| Add an attempt/fidelity state to the research pipeline | fixes score confusion | state errors | our human labels |
| Replace span-anchored acoustic features with GOP-ratio | measured contamination | reimplementation effort | 28 final labels + SIAK |
| Represent deletion explicitly in alignment | final-consonant class | false deletions | child token set |

```
production_vad = false
router_locked = false
unity_integrated = false
scorer_modified = false
production_window_locked = false
```

---

## Decision Gate

### **A = CLEAR_ARCHITECTURE_INSIGHTS**

Evidence: three concrete, externally-validated architecture insights (fidelity layer,
alignment-free/GOP evidence, deletion representation) plus an acquired child dataset and three
MIT-licensed local reference implementations. No production change is authorized by this
decision.
