# ASSESSABILITY INTERACTION — Phase 1.9.16

Layers stay separate: AUDIO QUALITY / VAD / ASSESSABILITY / PHONE EVIDENCE /
PRONUNCIATION. Nothing in this phase modifies any layer (all production locks
false; no code in `Phase1_4` or earlier touched — verified by clean `git status`
outside `Phase1_9_16/`).

---

- 1.9.15 independent result stands (RECOVERED_FROM_GIT): v2 false-gate 0/103 on
  human-valid attempts; strict refusal detection insufficient (5/8 dangerous).
- Baseline phone evidence on the 115-clip fidelity pack is unchanged (frozen
  model bit-reproduced). No new assessability experiment was needed: with no
  child model, there is no new phone-evidence behavior to interact.
- Guard recorded for the future: when a child model exists, it must be tested
  on the 8 human-NOT_ASSESSABLE clips for hallucinated confident phones before
  any FRR claim is accepted. The clips and the v2 rule code are committed and
  referenced, not duplicated here.
