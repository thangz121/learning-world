# PHASE 1.9.13 — PRONUNCIATION SYSTEM FORENSICS — CONSOLIDATED EXTERNAL TECHNOLOGY HARVEST

**Date:** 2026-10-03 · **Batches:** A (10 systems) + B (10 systems)  
**Flags:** production_vad=false · router_locked=false · unity_integrated=false ·
scorer_modified=false · production_window_locked=false  
**Decision:** **A = CLEAR_ARCHITECTURE_INSIGHTS**

---

## 1. Mission
Find what the world has already solved in pronunciation assessment (especially for children),
verify it with primary evidence and real runs where possible, and determine what LWE can legally
and technically reuse — instead of reinventing it.

## 2. Research Scope
20 system slots investigated (10 Batch A + 10 Batch B), plus datasets and research lines.
Evidence hierarchy E0–E6 enforced. No production changes.

## 3. Batch A Systems
Speech Blubs · AI Speak/M-Speak · ELSA · Microsoft PA · Speechace · OpenPronounce ·
speak-better-than-ai · Alignment-free line (GOP-AF + VoxTutor) · SIAK · slip.

## 4. Batch B Systems
Chivox · SpeechSuper · SpeechStep · SpeechLP · SpeakStar · SayBananas ·
SpeechTherapyMagic · NOCASA + speechocean762 (+ CMU Kids/CSLU Kids/MyST noted) ·
Phonological-feature MDD · Training-free retrieval MDD (PER-MDD).

## 5. Systems Actually Played
None — every consumer product required a mobile app, account, subscription, or mic-only browser
session. No authentication/paywall/CAPTCHA was bypassed; each block is documented.

## 6. Systems Actually Run
| System | Level | What ran |
|---|---|---|
| OpenPronounce (MIT) | **E4/E6** | 16 corpus cases, full pipeline, raw JSON archived |
| VoxTutor (MIT) | **E4** | 2×2 alignment/GOP harness reproduced exactly |
| SIAK dataset | **E4 (data)** | 16,308 child utterances downloaded + inventoried |
| wav2vec2-lv-60-espeak-cv-ft | E4 (via OpenPronounce) | phone evidence on our corpus |

## 7. Systems Blocked
Speech Blubs (mobile-only) · M-Speak (app) · ELSA (partner token) · Microsoft PA (Azure key) ·
Speechace (key by request) · Chivox (appKey+secret) · SpeechSuper (mic-only demo) ·
SpeechStep (mic) · SpeechLP (app/account) · SpeakStar (unreleased) · SayBananas (mobile) ·
SpeechTherapyMagic (account+mic) · NOCASA (EULA) · slip/speak-better (browser-only).

## 8. Evidence Quality
E4/E6: OpenPronounce, VoxTutor, SIAK data. E3: speak-better-than-ai, slip (+ Batch A repos).
E2: ELSA, SIAK papers, GOP-AF, NOCASA, speechocean762, phonological-feature MDD, PER-MDD,
SayBananas studies. E1: Speechace, Microsoft, Chivox, SpeechSuper, SpeechStep, SpeechLP,
Speech Blubs, SpeakStar, SpeechTherapyMagic, M-Speak (E0/E1). E0: marketing claims, marked.

## 9. Common Architecture Patterns
See `EXTERNAL_ARCHITECTURE_PATTERNS.md`: audio-quality gate → VAD/EOS → fidelity/attempt →
target → phone/acoustic evidence → alignment or alignment-free → diagnostics → scoring →
confidence → age/tolerance → child-facing decision → parent/teacher mode.

## 10. Child-Specific Architecture
Only SIAK is genuinely child-specific (models + data + rejected class). Consumer child apps
differentiate by **UX and policy** (attempt reward, no numbers, age bands, word-position
practice), not by disclosed child acoustics. Commercial APIs are adult-oriented; age controls
exist (SpeechSuper 3~6/6~12/>12; Chivox age/level) but mechanisms are unknown.

## 11. Audio Quality / VAD / EOS
- Chivox: quality checks (clipping/background/missing/incomplete) BEFORE feedback; `post proc
  failed` → re-record (E1).
- speak-better-than-ai: RMS>0.025 ×3 frames start; 1300 ms silence stop (E3).
- SIAK: server VAD; ELSA: server endpointing (E2).
- SayBananas study: **<50% of recordings unanalyzable** due to audio quality (E2) — direct
  external confirmation of our assessability problem.

## 12. Fidelity / Attempt Detection
Speechace `fidelity_class` (CORRECT/NO_SPEECH/INCOMPLETE/FREE_SPEAK) + score reduction;
Chivox valid-voice check; SpeechStep "declined rather than guessed at"; SpeechLP voice
separation ("every score reflects a real attempt"); SIAK rejected class; NOCASA zero-rating
removal. LWE has no such layer — the single largest architectural gap.

## 13. Child Speech Modeling
No adoptable child model exists publicly. SIAK corr 0.59–0.61 (single annotator); NOCASA
baseline UAR 36.37%; OpenPronounce scored a human-correct child token 2.46. Child scoring
remains genuinely unsolved externally.

## 14. Phoneme Recognition
`facebook/wav2vec2-lv-60-espeak-cv-ft` is the de-facto open phone model (3 independent OSS
systems); HuBERT used by PER-MDD; commercial models proprietary. Microsoft exposes ranked
phoneme candidates (NBestPhonemes).

## 15. Alignment
Blank-interleaved (2L+1) Viterbi with `present=false` (slip, E3); deletion sim=0 DP
(speak-better, E3); GMM-HMM forced alignment in older commercial systems (ELSA/SIAK, E2);
VoxTutor shows forced alignment is what survives speaking-rate variation (E4).

## 16. Alignment-Free Approaches
GOP-AF over full-sequence posteriors with insertion/deletion support, evaluated on CMU Kids
(E2); DTW vs TTS reference (OpenPronounce, E4); retrieval over ASR embeddings (PER-MDD, E2);
discrete-token surprisal; WavLM-DTW templates (~5 templates ≈95% performance, E2).

## 17. Acoustic Evidence
GOP-Avg and GOP-ratio (likelihood-ratio) (E3); DTW distances (E4); VoxTutor proves raw
distance = error + within-phone variance, GOP normalization removes the variance (E4).

## 18. Phonological Feature Evidence
35 attributes + multi-label CTC trained on native-only speech: FAR<30%/DER<10% vs phoneme
57%/31%; /th/→/s/ FAR 72%→37% via dental attribute (E2). SLATE 2025: ART framework cuts DER
25.88% relative; includes 3 Vietnamese-L1 speakers (E2). Diagnosis = max-deviation attribute +
direction → formative instruction.

## 19. Final-Consonant Handling
Microsoft `ErrorType=Omission`; SpeechSuper `/x/ omitted` + inserted before/after; slip
`present=false` + GOP severity; speak-better deletion penalty; GOP-AF deletion support.
Mature systems represent deletion explicitly — LWE's 1.9.12 finding matches the external
consensus.

## 20. Scoring
Ranges: stars (SIAK, M-Speak), 0–100 (OSS/commercial), rubric mapping (IELTS/PTE/TOEIC/CEFR).
Components: accuracy/fluency/completeness/prosody (Microsoft), pronunciation/intonation/
fluency (ELSA), weighted 0.3/0.4/0.3 (OpenPronounce). Child products hide numbers.

## 21. Confidence
NBestPhonemes scores (Microsoft, E1); `lowConfidence` span gating (slip, E3); uncalibrated
token confidences in OSS (E3). No commercial system exposes calibrated confidence.

## 22. Assessability / Refusal To Guess
See `ASSESSABILITY_MATRIX.md`. Mature commercial/child systems gate or refuse; open-source
systems return low scores instead. SpeechStep's "declined rather than guessed at" is the
cleanest child-safety statement found (E0/E1).

## 23. Age Conditioning
SpeechSuper demo exposes 3~6 / 6~12 / >12 (E1); Chivox exposes age/level calibration +
`gop_adjust` (E1); SpeechStep changes feedback policy per age band (no numbers on toddlers →
honest scores for teens) (E1). Mechanism of score change is unknown everywhere. Controlled
experiments NOT RUN (blocked) — `BATCH_B/age_conditioning_results.csv`.

## 24. Strict / Lenient Modes
SpeechSuper `slack` slider [−1,+1] step 0.1 (E1); Chivox `gop_adjust` [−1,1] (E1). Both expose
tolerance as explicit configuration rather than hidden constants.

## 25. Human Calibration
SIAK: single expert 0–100 → 0–5 stars; system corr 0.59–0.61; 1,489 rejected items (E2).
speechocean762: 5 experts, sentence/word/phoneme annotations, free commercial (E2). NOCASA:
expert stars, UAR methodology, baseline 36.37% (E2). No Vietnamese-L1 child calibration data
exists publicly.

## 26. Child-Facing Feedback
Stars + attempt reward (Speech Blubs/M-Speak/SIAK); heard/nearly heard/not heard + 0–3 stars
(SpeakStar); "zero numbers on kids" (SpeechStep Garden); KR/KP "Good job!/Not quite"
(SayBananas); "slipped → sounded like → tip" (SpeechTherapyMagic).

## 27. Parent / Teacher Feedback
Progress by sound/skill, trends, session logs, SLP dashboards, PIN-shared home practice
(SpeechLP, SpeechTherapyMagic, SpeechStep, Say66; E1/E2).

## 28. Reusable Open-Source Components
OpenPronounce (MIT; GPL deps caution) · speak-better-than-ai (MIT; espeak-ng GPL) ·
VoxTutor (MIT) · nocasa-baselines (check license) · speechocean762 Kaldi recipe.

## 29. Reusable Models
wav2vec2-lv-60-espeak-cv-ft (phone CTC; verify card) · hubert-large-ls960-ft (PER-MDD base;
verify card) · wav2vec2-large-960h (embeddings). No child model.

## 30. Reusable Datasets
speechocean762 (English, half children, phoneme labels, commercial-free — highest priority) ·
SIAK (16,308 child utterances; CC-BY-ND with commercial model use allowed; 594 at ages 4–6) ·
NOCASA/TeflonNorL2 (EULA; methodology).

## 31. Reusable Algorithms
Deletion-aware Viterbi with `present` flag · GOP-Avg/GOP-ratio · GOP-AF · retrieval MDD
(PER-MDD) · phonological attributes · template matching · DTW templates · target-selection
norms (Crowe & McLeod 2020 + latest-consonant rule + FCD process filter) · EOS constants.

## 32. Reusable Architecture Patterns
Fidelity/assessability gate · audio-quality gate · error taxonomy (omission/insertion/
substitution) · dual-engine cross-check · adult-managed accounts · offline/no-collection ·
demonstration-vs-measurement fallback.

## 33. Reusable UX Patterns
Tri-state word colouring + stars · attempt reward · age-band feedback policy · max one retry ·
high-dose trial model (~100/session) · "tap to scrub to the slip" · parent/SLP reports ·
word-position practice.

## 34. Commercial APIs Worth Evaluating
Microsoft PA (richest schema: NBestPhonemes + Omission) · Speechace (fidelity taxonomy) ·
Chivox (child engine + quality gate; pilot by contact) · SpeechSuper (age + leniency controls) ·
ELSA (multidimensional). All cloud; child behavior unverified; privacy review required.

## 35. License Constraints
MIT OSS reusable with dependency review (espeak-ng GPL-3.0; phonemizer GPL-3.0; Levenshtein
GPL-2.0). SIAK CC-BY-ND (commercial model building/eval not prohibited; no unrelated
derivatives). speechocean762 free commercial + non-commercial. slip has **no license**.
wav2vec2/HuBERT model cards to verify. Commercial APIs via contracts only.

## 36. Privacy Constraints
Speech Blubs: on-device voice detection, no audio retention (E1). SpeechStep: consent-gated
recording, no child name, age in months, separate training consent, cookieless analytics (E1).
SpeakStar: no account/analytics/network (E1). OSS local: audio never leaves the machine
(E3/E4). Cloud APIs: audio uploaded; child use needs DPA review. LWE should prefer local.

## 37. Runtime / Hardware Constraints
OpenPronounce: 1.3–1.8 s warm CPU per short utterance; cold ~36 s model load. wav2vec2 int8
~318 MB (browser-capable). VoxTutor: numpy-only instant. GOP-AF/attribute/per-MDD: no CPU
latency published → must be measured before any offline Windows target decision.

## 38. What LWE Has Already Solved
Real child corpus acquisition + human-label workflow (1.9.8–1.9.12); blind review + LAN
submission tooling; honest decision gates; baseline VAD/pronunciation pipeline; failure
taxonomy (boundary/window, deletion, fidelity gap); reproducibility discipline.

## 39. What LWE Is Currently Solving Inefficiently
Window/padding tuning as the primary lever (external systems solve it with EOS + fidelity
gates); acoustic features over CTC spans (GOP/alignment-free is the known fix); 0–100 for
children (mature child systems hide numbers); hidden calibration (age/leniency are exposed
elsewhere); no audio-quality gate (mature systems gate first).

## 40. What LWE Should STOP Reinventing
See `STOP_REINVENTING.md` (9 evidence-backed items: fidelity, omission representation,
span anchoring, child scoring, audio-quality gate, target selection, child output, score
adjustment config, FRR-first evaluation).

## 41. What LWE Still Must Research
See `REMAINING_GAPS.md` + `IMPORTANT_UNKNOWN.md` (child calibration, child fidelity,
Vietnamese-L1 data, local child-safe stack, deletion-aware evidence in our pipeline, CPU
latency of research methods).

## 42. Candidate Future Architecture
See `EXTERNAL_ARCHITECTURE_PATTERNS.md` + 1.9.14 proposal:
CHILD AUDIO → AUDIO QUALITY → VAD/EOS → ASSESSABILITY/FIDELITY → MULTI-EVIDENCE
(phoneme/GOP/attributes/retrieval) → DIAGNOSTIC FUSION → CONFIDENCE → CHILD DECISION
(GREAT/ALMOST/TRY AGAIN/CANNOT ASSESS) → PARENT MODE (diagnostics + progress).
Research candidate only.

## 43. Proposed Experiments
See `PHASE_1_9_14_PROPOSAL.md` P1–P7 (fidelity layer, deletion-aware GOP, SIAK+speechocean762
calibration, PER-MDD/GOP-AF, phonological attributes, target-selection adoption, child
decision layer).

## 44. Risks
Overfitting to our 65-token child set; license ambiguity (SIAK ND, model cards); cloud child
privacy; CPU latency of wav2vec2-scale models for offline Windows; temptation to adopt
commercial APIs without child validation; imputation/annotation quality limits in datasets
(single annotator for SIAK).

## 45. Decision Gate
### **A = CLEAR_ARCHITECTURE_INSIGHTS**

Concrete evidence-backed changes now justified (none implemented):
1. Add an explicit **fidelity/assessability state** before pronunciation feedback (7+ systems).
2. Replace CTC-span-anchored acoustic features with **GOP-ratio / GOP-AF** evidence.
3. Represent **deletion/omission explicitly** in alignment (final-consonant class).
4. Add an **audio-quality gate** before scoring (external attrition evidence).
5. Adopt **FRR-first evaluation** on human-correct child tokens.
6. Adopt **child-facing tri-state/stars** output with numbers in parent mode.
7. Adopt **published target-selection norms** (Crowe & McLeod 2020 + FCD filter) for curriculum.
8. Calibrate against **speechocean762 + SIAK (4–6y)** with speaker-disjoint UAR methodology.

No production change is authorized by this decision.
