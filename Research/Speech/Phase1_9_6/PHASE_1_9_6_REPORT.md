# Phase 1.9.6 Report — Hybrid Rescue Verification

Date: 2026-10-02  
Git base: `ad50950` (1.9.5) → this phase commit  
Branch: `ux/math-arenas-hotfix-20260930`

## 0. Reality check

| Item | Value |
|---|---|
| Agent can human-listen? | **No** |
| Labels fabricated? | **No** |
| Status | **HUMAN_REVIEW_PENDING** |
| Decision | **C. HUMAN_REVIEW_INCONCLUSIVE** |

Review pack is complete and reproducible. Human evidence is the blocker — not missing clips.

---

## 1. Baseline locked (unchanged)

| Condition | Detector | n_seg | notes |
|---|---|---:|---|
| IMG_0639 | Silero 0.5 | **0** | baseline failure |
| IMG_0639 | hybrid_score | **28** | candidates under test |
| IMG_0639 | hybrid speech_ratio | ≈0.032 | not speech proof |
| NEW | Silero 0.5 | 38 | not review target |

- Mean raw duration ≈ **0.252 s**; **all 28 < 0.5 s**; 9 in [0.10, 0.20)  
- Algorithm **not** modified  
- Silero default **not** lowered  
- Unity / scorer / router constants **not** touched  

Source derived 16k SHA: `9A662E8639D7C9694CFD997A221C541900BE39BEA26C3B961CFC2DF3162E5BCB`  
Original mp3 SHA: `E4CDF24D2B5140B91EF637676CD5FA1B9A7F6B6A629F62AE9AC2657CB171A6F7`

---

## 2. Clip audit (Gate)

| Check | Result |
|---|---|
| Reproduced hybrid n | 28 |
| Match Phase 1.9.1 timestamps | OK |
| Match Phase 1.9.5 timestamps | OK |
| Time-ordered | OK |
| No duplicates | OK |
| Within source duration (219.284 s) | OK |
| Non-empty WAV peak/rms | OK |
| Status | **CLIPS_AUDITED_OK** (0 issues) |

Fresh 1.9.6 exports: `HumanReview/clips_hybrid/clip_XX.wav` with **±50 ms preview pad**; raw timestamps preserved in metadata.

Artifact: `Results/clip_audit.json`

---

## 3. Human review workflow

| Asset | Path |
|---|---|
| MODE A UI (primary) | `HumanReview/review.html` |
| MODE B UI | `HumanReview/review_mode_b.html` |
| Builder | `HumanReview/build_html.py` |
| Empty labels CSV | `Results/Human_Review_Labels.csv` |
| Protocol | `Protocol/HUMAN_REVIEW_PROTOCOL.md` |
| Analyzer | `scripts/analyze_human_labels.py` |

MODE A shows only: clip id, source `IMG_0639`, time, duration, player, SPEECH/NON_SPEECH/MIXED/UNCERTAIN.  
No “hybrid detected speech”, no scores, no ASR, no energy/spectral.

---

## 4. Evidence roles (preserved)

- **HUMAN_LABEL** = authority for “is there speech in this candidate?”  
- Silero = baseline  
- hybrid_score = research candidate  
- energy = WEAK_PSEUDO_REF  
- ASR = auxiliary; **ASR_EMPTY ≠ NON_SPEECH** (prior 0/28 nonempty must not auto-label NON_SPEECH)

---

## 5. Answers to the 8 required questions

| # | Question | Answer |
|---|---|---|
| 1 | Hybrid candidate count? | **28** |
| 2 | Human SPEECH count? | **28** (Round 2 min2s); Round 1 short = undecidable |
| 3 | NON_SPEECH count? | **0** (Round 2) |
| 4 | MIXED/UNCERTAIN? | Round2 **0**; Round1 short **28 UNCERTAIN** |
| 5 | SPEECH by duration? | Raw all &lt;0.5s; human-decidable only with **≥2s** context |
| 6 | Hybrid recovers speech when Silero=0? | **YES on these 28 candidates** (IMG only) |
| 7 | Production VAD? | **NO** |
| 8 | Next? | **Phase 1.9.7** multi-recording + boundary/merge study |

---

## 6. Limitations (do not drop)

1. Candidate review ≠ full-file GT; **no full-file recall/FN** from 28 clips.  
2. Single file IMG_0639 only.  
3. Single-reviewer reference if/when filled — not multi-rater GT.  
4. Do not state “hybrid accuracy = X%” without explicit metric + denominator.  
5. Do not send tight crops to pronunciation scorer.  
6. Prior F1 vs energy/Silero/ASR **not** used as pass criteria here.

---

## 7. Decision (after Round 1 + Round 2 human listen)

### **A. HUMAN_VERIFIED_PROMISING**

| Round | Window | Result |
|---|---|---|
| 1 | raw / ±50ms (mean ~0.25s) | **28/28 UNCERTAIN** — too short to decide |
| 2 | centered **≥2.0s** | **28/28 SPEECH** |

**Speech reference standard:**  
`1790932799243_8856108714107255767_8856108714107255767.mp3`  
SHA `17E3DA86267D1B7B864EA862A6C5C3B12AF4E49FD649F2FE140652A53DDF33FF`  
(clearer calibration; cuts still sometimes mid-word)

**Reviewer qualitative notes:**
- All hybrid candidates contain speech when given ≥2s context.
- Word identity hard; ~**70%** words guessable.
- Boundaries often feel truncated / not end-of-word clean.
- IMG source = hosting-program audio (difficult).

**Still true:**
- Candidate-level only — **not** full-file VAD GT / recall.
- **Not** production VAD; **no** router lock; **no** Unity.
- Do not feed tight raw crops to pronunciation scorer.

Artifacts: `Results/ROUND1_HUMAN_OUTCOME.md`, `Results/ROUND2_HUMAN_OUTCOME.md`,  
`Results/Human_Review_Labels_min2s_Filled.csv`, `Results/human_review_results.json`

---

## 8. Exact next step

**Phase 1.9.7 — Multi-recording Hybrid Rescue Validation**  
+ boundary/merge research (incomplete word ends).  
Do not change Silero default or lock production constants yet.
