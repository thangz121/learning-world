# PHASE 1.9.14 PROPOSAL (evidence-ranked, research-only)

Ranked by external evidence strength × LWE relevance. Do not implement in 1.9.13.

## P1. Fidelity / assessability layer (highest evidence consensus)
- Evidence: Speechace taxonomy (E1), Chivox quality gate (E1), SpeechStep refusal (E0/E1),
  SIAK/NOCASA rejected class (E2), SayBananas audio-quality attrition (E2), SpeechLP attempt
  detection (E1), PER-MDD FRR-first (E2).
- Experiment: define states (NO_SPEECH / UNINTELLIGIBLE / INCOMPLETE / FREE_SPEAK / CORRECT)
  using existing signals (energy, VAD, ASR text vs expected text); validate against our
  human-labeled sets (1.9.9–1.9.12).
- Success: separates "cannot assess" from "pronunciation error" on our corpus.

## P2. Deletion-aware evidence (blank-interleaved Viterbi + GOP-ratio)
- Evidence: Microsoft `Omission` (E1), slip `present=false` + GOP-ratio (E3), GOP-AF (E2),
  speak-better deletion sim=0 (E3).
- Experiment: implement on the 65 final-consonant tokens; test against the 28 human labels
  (1.9.12) and the "four" class.
- Success: catches /r/ deletions without lowering human-correct tokens (FRR-first).

## P3. Child calibration benchmark (SIAK age 4–6 + speechocean762)
- Evidence: SIAK dataset acquired (E4), speechocean762 free commercial (E2), NOCASA methodology
  (UAR + zero-rating removal) (E2).
- Experiment: score SIAK 4–6y subset + speechocean762 child subset with our pipeline and the
  open wav2vec2 phone model; report UAR/FRR, speaker-disjoint.
- Success: first honest child-calibration number for LWE.

## P4. No-training retrieval evidence (PER-MDD) + GOP-AF reimplementation
- Evidence: PER-MDD (E2, FRR 4.43%, no training), GOP-AF (E2, CMU Kids).
- Experiment: build a per-word retrieval pool for LWE vocabulary; compare with GOP-ratio.
- Success: phoneme evidence without any scorer training.

## P5. Phonological-feature diagnosis
- Evidence: three papers agree on attribute-level diagnosis; one includes Vietnamese-L1 (E2).
- Experiment: 35-attribute (or compact 6-category) head on wav2vec2; evaluate diagnosis on our
  error classes (deletion/voicing).
- Success: "how the error was made" feedback beyond phoneme substitution.

## P6. Target-selection adoption (curriculum, not scoring)
- Evidence: SpeechLP algorithm (E1) + SpeechStep word positions (E1) + SIAK content (E4).
- Experiment: implement Crowe & McLeod norms + latest-consonant rule + FCD process filter for
  LWE word ordering.
- Success: developmentally ordered LWE vocabulary.

## P7. Child-facing decision layer (UX)
- Evidence: SpeakStar tri-state + stars (E1), SpeechStep age bands (E1), Speech Blubs attempt
  reward (E1), SayBananas KR/KP (E2).
- Experiment: GREAT / ALMOST / TRY AGAIN / CANNOT ASSESS output, stars, no numbers on kids;
  parent mode with diagnostics.
- Success: human spot-check of child feedback comprehension.

Not selected: anything requiring cloud child audio (privacy), commercial API integration
(cost/child calibration unverified), or production changes before human validation.
