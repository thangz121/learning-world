# ARCHITECTURE DELTA

Research-only comparison. No production change is authorized by this document.
Flags remain `production_vad=false · router_locked=false · unity_integrated=false ·
scorer_modified=false · production_window_locked=false`.

---

## 1. Current LWE (frozen)

```
CHILD AUDIO
  ↓
VAD (Silero 0.5 default / hybrid research rescue)
  ↓
ASR / PHONE (wav2vec2-xlsr-53-espeak-cv-ft CTC, forced alignment)
  ↓
SCORE (soft-v2: 100 × mean phone similarity)
  ↓
CONFIDENCE (posterior × exact-ratio × miss penalty)
  ↓
DECISION (single score; score ≠ confidence)
```

Known failure modes measured before this phase: window sensitivity 51.3% (1.9.11), deleted
final phones scored 66.8–100 (1.9.11/1.9.12), CTC-span acoustic features contaminated
(1.9.12), low scores on human-correct child speech (1.9.9: 0.414 first-pass, revised 0.0/17
with 8 unresolved).

## 2. Proposed research architecture (this phase's evidence status)

```
CHILD AUDIO
  ↓
AUDIO QUALITY ...................... defined; not separately tested (no clipping/SNR metric)
  ↓
VAD / EOS .......................... frozen; VAD-negative ≠ no speech (IMG, 28/28 human speech)
  ↓
FIDELITY / ASSESSABILITY ........... SUPPORTED (P1 v2); refusal states NOT validated
  ↓
TARGET EVIDENCE .................... ASR supporting-only (P1); ASR wrong/empty on correct speech
  ↓
MULTI-EVIDENCE PRONUNCIATION ....... unchanged (soft-v2); deletion margin available as evidence
  ↓
DELETION-AWARE ALIGNMENT ........... REPRESENTATION OK; FRR-first benefit NOT demonstrated (P2)
  ↓
DIAGNOSTICS ........................ deletion/omission state now representable; not production
  ↓
SCORE .............................. frozen; SIAK calibration 0.22 (P3)
  ↓
CONFIDENCE ......................... frozen; low confidence ≠ bad pronunciation (P1)
  ↓
CHILD DECISION ..................... research only: GREAT/ALMOST/TRY AGAIN/CANNOT ASSESS
  ↓
PARENT DIAGNOSTICS ................. research only (numbers/trends in parent mode)
```

## 3. Layer-by-layer delta

### 3.1 FIDELITY / ASSESSABILITY (new layer proposed)

| field | value |
|---|---|
| Evidence | External: Speechace `fidelity_class`, Chivox quality gate, SpeechStep refusal, SIAK rejected class, SayBananas <50% unanalyzable (E1/E2, from 1.9.13). LWE: P1 (human-reviewed 49 tokens + IMG stress) |
| Observed LWE failure it addresses | 8/30 human-correct tokens falsely gated by literal rules; 16/24 ASR-empty tokens human-correct; 9 human-correct tokens scored <50; IMG human-speech with VAD=0 |
| Experiment | P1 v1 vs v2 transparent rules (no ML), FRR-style false-gate metric on human-correct child speech |
| Result | v1 false-gate 8/30 (26.7%); v2 false-gate 0/30; human-uncertain routed to POSSIBLE_ATTEMPT; deletion errors stay in the pronunciation layer; no-evidence never becomes wrong-pronunciation |
| Remaining uncertainty | Refusal states (`NO_SPEECH`, `UNINTELLIGIBLE`, `FREE_SPEAK`, `INCOMPLETE`) have **no labeled cases** to validate; v2 thresholds are conservative by design, not human-validated; single reviewer |
| Status | **Architecture requirement for any future scoring work (research); not implementable as production gate yet** |

### 3.2 ASR AS SUPPORTING EVIDENCE ONLY (semantics change, no code change)

| field | value |
|---|---|
| Evidence | LWE P1: ASR_EMPTY 16/24 human-correct; ASR_WRONG transcripts "I"/"Right"/"See?"/"Fitch." on CLEAR_CORRECT tokens; 1.9.9 established `asr_is_not_pronunciation_judge` |
| Observed failure | Any validity decision keyed on ASR rejects correct children (v1 measured) |
| Experiment | P1 v1 (ASR-driven) vs v2 (ASR-supporting) |
| Result | Removing ASR from the validity path removes all 8 false gates while keeping low-assessability routing |
| Remaining uncertainty | Does not prove ASR-positive implies validity either; high ASR confidence ≠ correct pronunciation |
| Status | **Supported as a rule for future layers** |

### 3.3 DELETION-AWARE ALIGNMENT (representation)

| field | value |
|---|---|
| Evidence | Microsoft `ErrorType=Omission`, SpeechSuper `/x/ omitted`, slip `present=false`, GOP-AF insertion/deletion (E1/E2/E3, 1.9.13); 1.9.12 forced alignment cannot represent deleted /r/ |
| Observed failure | 3 confirmed "four" deletions scored 66.8–100; 4/5 human-absent /r/ not caught by baseline |
| Experiment | P2 blank-interleaved (2L+1) Viterbi + skip transitions + margin vs frozen soft-v2 on 65 tokens / 28 human labels |
| Result | Deletion now representable; zero-penalty variant FRR 7/16 (unsafe); margin FRR=0 FAR 6/12 vs baseline FRR=1 FAR 5/12 (no dominance); best AUC 0.839 but no improving operating point |
| Remaining uncertainty | Whether the margin as an uncertainty flag (not verdict) helps; whether a child-adapted phone model would separate weak-but-present /r/ from absent /r/ |
| Status | **Hypothesis, not justified as a scorer change** |

### 3.4 GOP-STYLE EVIDENCE (replacement for CTC-span features)

| field | value |
|---|---|
| Evidence | GOP-Avg/GOP-ratio (E3), GOP-AF (E2), VoxTutor variance normalization (E4) from 1.9.13 |
| Observed failure | 1.9.12 CTC-span acoustic features inherited alignment errors and added no value |
| Experiment | P2 GOP-ratio over forced-present span (same model, same audio) |
| Result | AUC 0.740 vs baseline 0.760; at FRR≤1, FAR 91.7% vs baseline 41.7% |
| Remaining uncertainty | GOP may still help on longer words/adults; no evidence for it on child final consonants |
| Status | **Hypothesis rejected for this task on this dataset** |

### 3.5 FRR-FIRST EVALUATION (evaluation change)

| field | value |
|---|---|
| Evidence | PER-MDD “FRR is the most critical metric” (E2, 1.9.13); LWE FRR-first rule |
| Observed failure | Previous window/acoustic analyses could “improve” while rejecting correct children |
| Experiment | P2 matched-FRR frontier; P3 SIAK FRR-proxy by age |
| Result | Candidates rejected on FRR grounds; SIAK shows 33–40% rejection of human-good 4–6yo tokens |
| Remaining uncertainty | None about the metric; it is a rule, not a hypothesis |
| Status | **Supported as mandatory evaluation practice** |

### 3.6 CHILD-SPECIFIC CALIBRATION (data layer)

| field | value |
|---|---|
| Evidence | SIAK release (E4 data) + SIAK paper (E2); NOCASA methodology (E2) |
| Observed failure | Scorer treats all child ages alike; score means rise with age (55→71) |
| Experiment | P3: 1,074 SIAK utterances scored with frozen pipeline, speaker-disjoint test split reported |
| Result | Pearson 0.226 overall / 0.263 speaker-disjoint; 4–6yo FRR-proxy 33–40%; rejected class absent from release |
| Remaining uncertainty | License legal review; only 5 speakers at 4–6; no Vietnamese-L1 data |
| Status | **Calibration target identified (research), no adoption** |

### 3.7 CHILD DECISION / PARENT MODE (UX layer)

| field | value |
|---|---|
| Evidence | SpeakStar tri-state + stars, SpeechStep “zero numbers on kids”, Speech Blubs attempt reward, SayBananas KR/KP (E1/E2, 1.9.13) |
| Observed failure | LWE currently surfaces 0–100 directly |
| Experiment | None this phase (explicitly research-only; see `CURRICULUM_AND_UX_RESEARCH.md`) |
| Result | Design comparison only: GREAT / ALMOST / TRY AGAIN / CANNOT ASSESS + stars; numbers in parent mode |
| Remaining uncertainty | Needs human spot-check with children/parents before any UI work |
| Status | **Research only** |

## 4. What the delta means

- The phase's core question is answered **YES**: LWE has been asking the scorer a question
  (“how good is this pronunciation?”) before establishing that the recording is an assessable
  attempt. The failure is measured, not hypothetical.
- The proposed *fix* for the scorer (deletion-aware/GOP evidence) is **not** supported by the
  FRR-first experiment. The two conclusions must not be conflated: fidelity is a real missing
  layer; the scorer research direction from 1.9.13 needs a different experiment.
- Nothing here authorizes production work. The next research step (if any) should be the
  margin-as-uncertainty flag on a new independent human-labeled set, not a scorer change.
