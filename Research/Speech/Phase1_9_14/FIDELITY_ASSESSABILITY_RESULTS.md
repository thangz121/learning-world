# P1 RESULTS — FIDELITY / ASSESSABILITY (RESEARCH ONLY)

**Experiment scripts:** `experiments/p1_fidelity_assessability.py`
**Data:** `artifacts/p1/p1_evidence_table.csv`, `p1_state_by_human.csv`, `p1_cases.csv`,
`p1_v1_vs_v2_summary.json`
**Inputs (frozen):** 1.9.8 pronunciation/VAD/inventory, 1.9.9 human review + ASR conflicts,
1.9.10 second pass, 1.9.11 scorer-miss + window, 1.9.12 final consonants.
**Provenance:** `manifests/p1_provenance.json` (SHA-256 of every input).

---

## 1. Question

Can a transparent rule layer separate, before any pronunciation verdict:

- “child probably said something we cannot safely assess”,
- “child produced an assessable attempt that appears incorrect”,
- “child did not make the requested attempt”?

…using only signals LWE already has, and never converting **no evidence** into
**wrong pronunciation**?

## 2. Signals and thresholds (documented, not tuned on labels)

| signal | source | use |
|---|---|---|
| duration | 1.9.8 recording_inventory | too-short audio |
| Silero segments / speech_ratio | 1.9.8 vad_results (silero mode) | speech presence |
| RMS mean, ZCR proxy | 1.9.8 vad_results | energy/quality floors |
| soft score, confidence | 1.9.8 pronunciation_results | target-evidence availability |
| ASR status/text | 1.9.9 asr_phone_conflicts (42 tokens) | supporting evidence only (v2) |
| human labels | 1.9.9–1.9.12 | evaluation only, never rule input |

Thresholds: `VAD_RATIO_FLOOR=0.05`, `MIN_DUR_S=0.15`, `RMS_NOISE_FLOOR=0.002`,
`RMS_ENERGY_PRESENT=0.01`, `CONF_VALID=0.05`, `CONF_POSSIBLE=0.005`.

## 3. Rule versions

**v1 — literal first-cut.** Treats ASR status as target-validity evidence:
`ASR_EMPTY` + no phone confidence → `UNINTELLIGIBLE`; `ASR_WRONG` + low confidence →
`FREE_SPEAK`; `ASR_CORRECT` → `ASSESSABLE`.

**v2 — child-safe.** ASR is supporting evidence only:
- `NO_SPEECH` requires converging evidence (too-short, or VAD-negative **and** energy floor);
- VAD-negative but energy present → `POSSIBLE_ATTEMPT (LOW, ENERGY_PRESENT_DESPITE_VAD)`;
- `ASR_EMPTY`/`ASR_WRONG` never produce `FREE_SPEAK`/`UNINTELLIGIBLE` without acoustic proof;
- weak target evidence (ASR empty/wrong **and** confidence < 0.005) → `POSSIBLE_ATTEMPT (LOW)`;
- some target evidence (ASR correct or confidence ≥ 0.005) → `VALID_ATTEMPT (MEDIUM)`;
- ASR correct **and** confidence ≥ 0.05 → `ASSESSABLE (HIGH)`.

## 4. Results on 49 human-labeled child tokens

State × human matrix (v2):

| fidelity state | HUMAN_CORRECT | HUMAN_INCORRECT | HUMAN_UNCERTAIN | total |
|---|---:|---:|---:|---:|
| NO_SPEECH | 0 | 0 | 0 | 0 |
| UNINTELLIGIBLE | 0 | 0 | 0 | 0 |
| INCOMPLETE | 0 | 0 | 0 | 0 |
| FREE_SPEAK | 0 | 0 | 0 | 0 |
| POSSIBLE_ATTEMPT (LOW) | 8 | 9 | 4 | 21 |
| VALID_ATTEMPT (MEDIUM) | 20 | 4 | 1 | 25 |
| ASSESSABLE (HIGH) | 2 | 0 | 1 | 3 |

v1 matrix (same set): UNINTELLIGIBLE 4 correct / 3 incorrect / 3 uncertain;
FREE_SPEAK 4 correct / 3 incorrect / 2 uncertain — i.e. the literal rules put **8
human-correct tokens into refusal/wrong-attempt states**.

### 4.1 False gates on human-correct speech (child-safety metric)

| rule set | false gates (NO_SPEECH/UNINTELLIGIBLE/FREE_SPEAK/INCOMPLETE) | IDs |
|---|---:|---|
| v1 | **8/30** | child_01_seven, child_02_eight, child_03_eight, child_04_three, child_06_six, child_06_two, child_07_one, child_09_six |
| v2 | **0/30** | — |

Each v1 failure is traceable to a single unsafe conversion: `ASR_EMPTY`→UNINTELLIGIBLE
(4 cases), `ASR_WRONG`→FREE_SPEAK (4 cases). Concrete evidence that ASR is not a validity
judge for 4-year-olds: the wrong transcripts include “Right”, “I”, “See?”, “Fitch.”,
“Snake.” on tokens human-rated CLEAR_CORRECT.

### 4.2 NO EVIDENCE ≠ BAD PRONUNCIATION (measured)

- 16/24 labeled ASR-empty tokens are human-correct. v2 routes them to
  `VALID_ATTEMPT` (12) or `POSSIBLE_ATTEMPT` (4); never to refusal, never to error.
- 9 human-correct tokens have frozen score < 50; v2 converts none of them into
  `FREE_SPEAK` and none into `UNINTELLIGIBLE`.
- Human-uncertain cases (6): v2 gives 4× `POSSIBLE_ATTEMPT (LOW)`, 1× `VALID_ATTEMPT`,
  1× `ASSESSABLE`; 0 refusals. Uncertainty stays visible instead of being collapsed to
  correct/wrong.

### 4.3 The deletion (SCORER_MISS) cases are a pronunciation-layer problem

| case | human | frozen score | v2 state | assessability |
|---|---|---:|---|---|
| child_06_four | CLEAR_INCORRECT | 100.0 | VALID_ATTEMPT | MEDIUM |
| child_07_four | CLEAR_INCORRECT | 100.0 | VALID_ATTEMPT | MEDIUM |
| child_09_four | CLEAR_INCORRECT | 66.8 | VALID_ATTEMPT | MEDIUM |
| child_03_six / child_03_two / child_04_eight | AMBIGUOUS | 50.0 | POSSIBLE_ATTEMPT | LOW |

v1 marked one as FREE_SPEAK and two as UNINTELLIGIBLE. The audio is a valid attempt; the
failure is that forced alignment cannot represent the deleted final phone (see P2). The
fidelity layer must not absorb that failure.

### 4.4 VAD-only gating would reject verified speech (IMG stress)

`IMG_0639`: Silero 0 segments / ratio 0.0, but hybrid found 28 segments and the human
Round-2 review (1.9.6) labeled **28/28 SPEECH**. v2 → `POSSIBLE_ATTEMPT (LOW)` with
`ENERGY_PRESENT_DESPITE_VAD`; v1 (with duration fixed) also does not refuse. A gate keyed
only on the default VAD would have called this NO_SPEECH. Any future fidelity layer must
use converging evidence, not VAD alone.

## 5. What is demonstrated vs not

**Demonstrated (human-reviewed LWE evidence):**
1. A literal fidelity layer built on ASR status false-gates 26.7% of human-correct child speech.
2. A conservative, signal-convergent layer reduces that to 0/30 while still exposing low
   assessability for weak-evidence tokens and uncertainty for human-uncertain tokens.
3. No-evidence states are not converted into wrong-pronunciation states.
4. VAD-negative does not imply no speech (IMG, human-verified).

**Not demonstrated (insufficient data):**
1. Correct `NO_SPEECH`/`UNINTELLIGIBLE` refusal — no labeled invalid recordings exist.
2. `FREE_SPEAK`/`INCOMPLETE` detection — no confirmed cases in the corpus.
3. Production thresholds — v2 thresholds are conservative by design, not human-validated.

## 6. For the architecture delta

- The layer is worth adding **research-first**, but its refusal states cannot be validated
  on LWE's current corpus; SIAK's rejected class is documented (1,489 items) but absent
  from the released data (see `SIAK_CHILD_CALIBRATION.md`).
- Recommended research interface (not production): pronunciation scoring may *compute*
  internally, but child-facing error feedback should be gated by `assessability ≥ MEDIUM`
  and `VALID_ATTEMPT/ASSESSABLE`; `POSSIBLE_ATTEMPT (LOW)` should surface “let's try once
  more” rather than an error verdict.
