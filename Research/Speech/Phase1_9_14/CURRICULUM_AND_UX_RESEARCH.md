# CURRICULUM TARGET-SELECTION + CHILD-FACING OUTPUT — RESEARCH NOTES

Research only. **No Unity content, no curriculum data, no UI and no scoring change** is made
or authorized by this document. This file records the Phase 1.9.14 answer to spec §13
(target-selection research) and §14 (child-facing output research).

---

## Part A — Target-selection research (curriculum, not scorer)

### A.1 The 1.9.13 claim

External evidence (SpeechLP E1/E2; SpeechStep E1; SIAK content E4) says child pronunciation
difficulty depends on: developmental acquisition norms, word position, phonological process,
latest-acquired consonant in the word, and final-consonant deletion (FCD). LWE currently
orders vocabulary without any developmental model.

### A.2 LWE's current targets (facts, not proposals)

- Runtime/curriculum speaking targets today (SAPI reference set): `big`, `blue`, `book`,
  `cat`, `close`, `dog`, `open`, `open the door`, `red`, `red apple`, `small`,
  `this is a cat` (+ the arena words `one`…`ten`, `four` etc. in the math games).
- Final classes in this set (/g/, /k/, /d/, /s/, /n/, /l/, /t/) have **0 examples** in the
  child corpus except /t/, /n/, /s/ — Phase 1.9.12 `ZENODO_TARGET_COVERAGE: LIMITED`.
- The child corpus only covers /n/ /s/ /v/ /t/ /r/ finals with labels; /r/ deletion is the
  best-documented failure (P2).

### A.3 Norms that would be needed (external evidence, not adopted)

| source | what it gives | evidence | caveat |
|---|---|---|---|
| Crowe & McLeod (2020) consonant acquisition norms | age by which English consonants are mastered (~90% criterion) | E2 (via SpeechLP, 1.9.13) | English (US/Australia); not Vietnamese-L1 |
| Latest-consonant rule | word difficulty ≈ hardest (latest-acquired) consonant | E1/E2 (SpeechLP) | heuristic; not validated on L1-Vietnamese children |
| FCD process filter | avoid/flag word-final targets prone to deletion for younger children | E1/E2 + LWE P2 evidence (FCD is real: 12/28 human-absent finals) | LWE child sample shows FCD across /r/, /t/, /v/, /s/, not only /r/ |
| age metadata | SIAK ages 4–12 (E4) | usable for ordering, not for scoring | SIAK targets are Finnish/UK-English children |

### A.4 Findings for LWE curriculum (research hypotheses only)

1. **Supported by LWE evidence:** final-consonant deletion is common in real 4–5yo speech
   (12/28 human-reviewed tokens; /r/ 5/6). Any curriculum that teaches final-consonant words
   without expecting deletion will over-diagnose errors (the 1.9.11/1.9.12 SCORER_MISS class).
2. **Supported by external evidence:** developmentally ordered targets are the established
   practice in mature child products; LWE has no such ordering.
3. **Hypothesis:** the current LWE list mixes late-acquired sounds (`this is a cat` clusters,
   `red apple`, `close`) with early words; a norms-based reordering could reduce frustration.
4. **Blocked:** no Vietnamese-L1 acquisition norms exist publicly (1.9.13 REMAINING_GAPS #4).
   Any ordering derived from English-L1 norms is an approximation for LWE learners.
5. **Not proposed here:** changing the curriculum, generating a new word list, or coupling
   target selection to the scorer. A separate, human-reviewed curriculum phase is required.

### A.5 If pursued later (prerequisites, not a plan)

- Obtain/verify Crowe & McLeod 2020 norm table license and reproduction rights.
- Human-review a candidate reordered list with a Vietnamese children's-English teacher.
- Keep target selection independent from scoring (score must not depend on which words are
  currently "in curriculum").

---

## Part B — Child-facing output research (UX architecture, not implemented)

### B.1 The question

The child is 4 years old. Should a technical 0–100 pronunciation score ever be the direct
child-facing feedback? External child products say no (1.9.13, E1/E2):

| system | child-facing output | numbers? |
|---|---|---|
| SpeechStep Garden | “zero numbers on kids”; honest scores for teens | no |
| Speech Blubs | attempt reward, no score | no |
| SpeakStar | heard / nearly heard / not heard + 0–3 stars | no |
| SayBananas | KR/KP “Good job!” / “Not quite” | no |
| SIAK (research app) | 1–5 stars | no |
| LWE today | 0–100 score internally; game behavior already voice-first | engine number |

### B.2 Proposed separation (research architecture)

```
ENGINE (soft-v2 + P1 fidelity + future evidence)
   ↓ technical evidence (score, confidence, assessability, phone errors, deletion margin)
PARENT / TEACHER DIAGNOSTICS (numbers allowed: score, trend, error class, retry count)
   ↓
CHILD DECISION (age 4: GREAT / ALMOST / TRY AGAIN / CANNOT ASSESS)
```

### B.3 Mapping rules under study (hypotheses)

- `ASSESSABLE` + high score/evidence → **GREAT** (celebration, attempt reward).
- `VALID_ATTEMPT` + mixed evidence → **ALMOST** (model the target, one retry max).
- `VALID_ATTEMPT` + clear error evidence → **TRY AGAIN** (never “wrong”; scaffold).
- `POSSIBLE_ATTEMPT (LOW)` / `UNINTELLIGIBLE` / `NO_SPEECH` → **CANNOT ASSESS**
  (“Let's try together”) — never an error verdict. This is the child-facing face of the P1
  layer; today's engine would instead show a low number or an error cue.
- Numbers, per-phoneme diagnostics and deletion findings live in parent mode only.

### B.4 FRR-first consequence for UX

SIAK calibration shows the frozen scorer rejects 33–40% of human-good 4–6yo tokens. If a
child-facing “TRY AGAIN” were driven by the raw score at today's thresholds, roughly one in
three good attempts by a 4-year-old would be marked as failure. Therefore:
**no child-facing negative verdict may be emitted from pronunciation evidence until
assessability ≥ MEDIUM and FRR on human-correct child speech is measured at the operating
point.** (Matches the P1/P2 conclusions; no implementation in this phase.)

### B.5 Open UX research questions

1. Does a 4-year-old understand GREAT/ALMOST/TRY AGAIN consistently (comprehension test)?
2. Should CANNOT_ASSESS be visually distinct from TRY AGAIN (avoid perceived failure)?
3. Stars vs friendly character reaction — which carries less evaluative pressure?
4. Retry policy: evidence supports “max one retry” (1.9.13); verify with child observations.
5. Parent mode content: score + error class + assessability + trend; needs a separate
   privacy/human review (no child audio leaves the device).

No UI, text, or game behavior was changed in this phase.
