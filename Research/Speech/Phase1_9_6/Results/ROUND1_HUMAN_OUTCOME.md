# Round 1 human outcome — Phase 1.9.6

**Reviewer:** human on maynode  
**Date:** 2026-10-02  
**UI:** short clips (±50ms), mean raw duration ≈0.25s, all &lt;0.5s

## Result

| Metric | Value |
|---|---:|
| total | 28 |
| SPEECH | 0 |
| NON_SPEECH | 0 |
| MIXED | 0 |
| UNCERTAIN | **28** |

**Human statement:** clips too short to determine whether voice is present; no clip reached a usable SPEECH/NON_SPEECH judgment.

## Speech reference standard

File (repo root):

`1790932799243_8856108714107255767_8856108714107255767.mp3`

- SHA-256: `17E3DA86267D1B7B864EA862A6C5C3B12AF4E49FD649F2FE140652A53DDF33FF`
- Role: **SPEECH_REFERENCE_STANDARD** (clear recording for calibration)
- Not a substitute label for IMG hybrid candidates

## Decision impact (Round 1 alone)

**C. HUMAN_REVIEW_INCONCLUSIVE**

Reason: 100% UNCERTAIN; short hybrid candidates are not human-decidable at raw length.

## Follow-up

Round 2: each candidate centered in a **≥2.0 s** listen window (`review_min2s.html`), after calibrating on NEW reference clips.  
Raw detector timestamps remain unchanged.
