# Phase 1.1 — Speech Technology Research Lab

RESEARCH LAB, not library review. No subjective ranking. No premature architecture.

- Registry: `SpeechResearchRegistry.md` (one row per candidate, status vocabulary only)
- Per-candidate report: `candidates/<ID>/candidate_report.md` (see `candidate_template.md`)
- Harness spec: `benchmark_harness.md`
- Corpus spec: `test_corpus.md`
- Licenses: `candidates/<ID>/licenses.md` (chain audit per candidate)

Rules in force:
- No production integration yet — research adapters only (`CandidateAdapter`), never
  production API contracts. No gameplay/world/NPC/lesson changes.
- No giant model binaries in git — download/setup scripts or external paths only.
- Child speech: labeled evidence only; surrogate sets marked SURROGATE.
- `Transcript == pronounced-correct` is NEVER assumed; ASR_RECOGNITION and
  PRONUNCIATION_ASSESSMENT are separate fields.
