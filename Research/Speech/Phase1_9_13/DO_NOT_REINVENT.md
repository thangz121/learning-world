# DO_NOT_REINVENT — problems where mature solutions exist

### 1. Attempt / fidelity detection ("did the child actually try?")
- CURRENT_LWE_APPROACH: none — a low score is treated as possible pronunciation error
  (root cause of the 1.9.9–1.9.12 confusion).
- EXTERNAL_SYSTEM: Speechace (fidelity model), SIAK (rejected class: silence/interrupted/
  wrong word/noise/no effort), Microsoft (Completeness + miscue), SIAK 2017 (planned
  restricted-grammar effort check).
- THEIR_APPROACH: separate classifier/class-set that gates scoring and reduces scores or asks
  for retry.
- EVIDENCE: E1 (Speechace docs `fidelity_class`), E2 (SIAK papers), E1 (Microsoft docs).
- CAN_REUSE: design/taxonomy (no code).
- WHAT_WE_SHOULD_DO: design an explicit attempt state in the research pipeline before scoring.
- CONFIDENCE: HIGH.

### 2. Final-consonant deletion / omission representation
- CURRENT_LWE_APPROACH: soft-v2 forced per-expected-phone output; deletion not representable
  (1.9.12: acoustic over CTC spans inherited the error).
- EXTERNAL_SYSTEM: Microsoft PA (`ErrorType=Omission`; phoneme accuracy), slip (`present=false`
  span), speak-better-than-ai (deletion sim=0), GOP-AF (insertion/deletion in GOP).
- THEIR_APPROACH: explicit deletion/omission states + likelihood-ratio/GOP evidence.
- EVIDENCE: E1 (MS schema), E3 (slip/speak-better), E2 (GOP-AF).
- CAN_REUSE: algorithms are standard; reimplement.
- WHAT_WE_SHOULD_DO: implement deletion-aware alignment (blank-interleaved Viterbi) + GOP.
- CONFIDENCE: HIGH.

### 3. CTC-span-anchored acoustic features
- CURRENT_LWE_APPROACH: 1.9.12 features anchored on CTC spans → inherited alignment errors
  (voicing 0.95–1.0 on absent /r/).
- EXTERNAL_SYSTEM: GOP-AF (arXiv 2507.16838); slip GOP-ratio; OpenPronounce DTW.
- THEIR_APPROACH: don't commit to a span; use full-sequence posteriors / likelihood ratios /
  DTW distances.
- EVIDENCE: E2/E3.
- CAN_REUSE: reimplement GOP-AF or GOP-ratio.
- WHAT_WE_SHOULD_DO: replace span-anchored acoustic layer with GOP-style evidence.
- CONFIDENCE: HIGH.

### 4. EOS / end-of-speech for short child utterances
- CURRENT_LWE_APPROACH: Silero + window experiments (1.9.11/12 unresolved).
- EXTERNAL_SYSTEM: speak-better-than-ai (RMS 0.025 ×3 frames; 1300 ms hangover);
  SIAK server VAD; ELSA server endpointing.
- THEIR_APPROACH: simple energy hangover or server VAD; product-grade, not exotic.
- EVIDENCE: E3/E2.
- CAN_REUSE: constants as reference.
- WHAT_WE_SHOULD_DO: keep our VAD research, but benchmark against these simple baselines.
- CONFIDENCE: MEDIUM.

### 5. Phoneme recognition + G2P
- CURRENT_LWE_APPROACH: own phone model + CMUdict.
- EXTERNAL_SYSTEM: wav2vec2-lv-60-espeak-cv-ft + espeak-ng WASM (OpenPronounce,
  speak-better-than-ai, slip).
- THEIR_APPROACH: same model across systems; local, CPU/browser friendly.
- EVIDENCE: E3/E4.
- CAN_REUSE: model (pending card check) + MIT code (OpenPronounce/speak-better).
- WHAT_WE_SHOULD_DO: cross-check our phone evidence against this model on the same corpus.
- CONFIDENCE: MEDIUM-HIGH.

### 6. Child speech
- CURRENT_LWE_APPROACH: research only (1.9.8–1.9.12).
- EXTERNAL_SYSTEM: **nobody solved it fully.** SIAK is the only child-specific pipeline
  (corr 0.59–0.61, single annotator); OpenPronounce failed on our human-correct child token
  (2.46). Commercial systems are adult-oriented.
- THEIR_APPROACH: child data + lightweight models + generous scoring (stars for attempts).
- EVIDENCE: E2/E4.
- CAN_REUSE: SIAK dataset (license review) for calibration; star/attempt UX.
- WHAT_WE_SHOULD_DO: keep child-specific validation as our differentiator.
- CONFIDENCE: HIGH that this remains open.
