# STOP_REINVENTING (Batch A + Batch B, evidence-backed)

## 1. Attempt / fidelity detection
- WHAT WE WERE DOING: treating a low score as possible pronunciation error; no attempt state.
- WHAT EXTERNAL SYSTEMS DO: Speechace `fidelity_class` (CORRECT/NO_SPEECH/INCOMPLETE/FREE_SPEAK);
  Chivox "post proc failed → re-record" + audio-quality gate before feedback; Microsoft
  Completeness + miscue; SpeechStep "declined rather than guessed at"; SayBananas/SIAK reject
  unanalyzable/zero-rated samples; NOCASA excludes rating-0; SpeechLP "voice detection separates
  child speech from game audio so every score reflects a real attempt".
- EVIDENCE: E1 (Speechace/Chivox/Microsoft), E2 (SIAK/NOCASA/SayBananas), E0/E1 (SpeechStep/SpeechLP).
- CAN WE USE IT? Yes as design (no code).
- IF NOT, WHY? Proprietary implementations.
- WHAT SHOULD LWE DO: add an explicit assessability/fidelity state before scoring.

## 2. Final-consonant deletion / omission representation
- WHAT WE WERE DOING: forced per-expected-phone output; deletion not representable (1.9.12).
- WHAT EXTERNAL SYSTEMS DO: Microsoft `ErrorType=Omission`; SpeechSuper `/x/ omitted` + inserted
  before/after; slip `present=false`; speak-better deletion sim=0; GOP-AF insertion/deletion.
- EVIDENCE: E1 (MS/SpeechSuper), E3 (slip/speak-better), E2 (GOP-AF).
- CAN WE USE IT? Reimplement (standard math).
- WHAT SHOULD LWE DO: blank-interleaved Viterbi + GOP-ratio + explicit deletion state.

## 3. CTC-span-anchored acoustic features
- WHAT WE WERE DOING: extracting features over CTC spans → inherited alignment errors.
- WHAT EXTERNAL SYSTEMS DO: GOP-AF (no committed segmentation); slip GOP-ratio; OpenPronounce DTW.
- EVIDENCE: E2/E3.
- WHAT SHOULD LWE DO: replace span anchoring with GOP-style evidence.

## 4. Child-specific scoring
- WHAT WE WERE DOING: trying to make adult-trained pipelines work on ~4yo speech.
- WHAT EXTERNAL SYSTEMS DO: **nobody solved it fully** — SIAK (child model, corr 0.59–0.61,
  single annotator); NOCASA baseline ceiling UAR 36.37%; OpenPronounce failed on a
  human-correct child token (2.46); commercial child apps reward attempts rather than phoneme
  accuracy.
- EVIDENCE: E2/E4.
- CAN WE USE IT? SIAK + speechocean762 datasets (licenses permit model building/eval).
- WHAT SHOULD LWE DO: keep child scoring as our differentiator; calibrate against SIAK
  (age 4–6: 594 utt.) and speechocean762.

## 5. Audio-quality gate
- WHAT WE WERE DOING: VAD/window tuning as the main lever (1.9.11/12 unresolved).
- WHAT EXTERNAL SYSTEMS DO: quality checks before scoring (Chivox); ~50% of SayBananas
  recordings unanalyzable due to audio quality; NOCASA removes unintelligible/noisy/silent;
  Speechace reduces scores on unfaithful attempts.
- EVIDENCE: E1/E2.
- WHAT SHOULD LWE DO: gate on audio quality/assessability before pronunciation evidence.

## 6. Target word selection for children
- WHAT WE WERE DOING: LWE word lists without developmental ordering.
- WHAT EXTERNAL SYSTEMS DO: SpeechLP (Crowe & McLeod 2020 norms + latest-consonant rule +
  position + phonological process filter incl. FCD + age-relative difficulty); SpeechStep
  (age bands + word-position practice); SIAK (age-appropriate single words).
- EVIDENCE: E1/E2.
- WHAT SHOULD LWE DO: adopt the published acquisition norms + process filter for curriculum.

## 7. Child-facing output
- WHAT WE WERE DOING: 0–100 score.
- WHAT EXTERNAL SYSTEMS DO: stars (SIAK/M-Speak), heard/nearly heard/not heard (SpeakStar),
  "zero numbers on kids" (SpeechStep Garden), KR/KP feedback (SayBananas), reward attempts
  (Speech Blubs), "slipped → sounded like → tip" (SpeechTherapyMagic).
- EVIDENCE: E0/E1/E2.
- WHAT SHOULD LWE DO: tri-state word feedback + stars for children; numbers in parent mode.

## 8. Score adjustment as configuration
- WHAT WE WERE DOING: hidden calibration debates.
- WHAT EXTERNAL SYSTEMS DO: Chivox `gop_adjust` [−1,1]; SpeechSuper strict↔lenient slider
  (slack [−1,+1], step 0.1) exposed to integrators; age-group settings (3~6/6~12/>12).
- EVIDENCE: E1.
- WHAT SHOULD LWE DO: expose tolerance/age as explicit settings in the research pipeline,
  not as hidden constants.

## 9. FRR-first evaluation
- WHAT WE WERE DOING: F1/IoU against pseudo-references.
- WHAT EXTERNAL SYSTEMS DO: PER-MDD paper: "FRR is regarded as the most critical metric" —
  false rejection of correct phones frustrates learners.
- EVIDENCE: E2.
- WHAT SHOULD LWE DO: track FRR on human-confirmed-correct child tokens as a first-class metric.
