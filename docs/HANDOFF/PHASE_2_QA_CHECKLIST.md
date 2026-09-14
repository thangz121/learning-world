# PHASE 2 — QA CHECKLIST (inherits Phase 1 acceptance)

No quest, NPC, or environment ships in Phase 2 without passing EVERY
applicable section below. Code-only acceptance is NOT allowed: every item
needs either a unit proof (ENGINEERING) or a standalone proof (all rest).

Provenance: distilled from the Phase 1 chain (R4→R9 + lock regression),
`docs/GOLDEN_CHARACTER_STANDARD.md`, and `docs/HANDOFF/PHASE_1_LOCK.md`.
The Phase 1 lock's reopen gate applies unchanged.

## ENGINEERING (EditMode, batch `-runTests -testPlatform EditMode`)

- [ ] EditMode FULL suite green (no new failures, no skipped pins).
- [ ] New quest JSON parses via `ContentDatabase.ParseQuest`; id ==
      filename; hint_levels exact 4-freeze; simplify present;
      one_objective_at_a_time true; every target resolves to a vocab file.
- [ ] New vocab JSON parses via `ContentDatabase.ParseVocab`; id ==
      filename; active items carry tags + expectedForms + normal/slow +
      learning_v1 + en-US; no Google voice names anywhere in the blob.
- [ ] `tools/validate_content.py Content/vocab Content/quests
      Content/dialogues` → PASS (authoring).
- [ ] New quest classifies to the intended `QuestPattern` (CT-P06C matrix).
- [ ] `QuestContentCatalog.Validate` clean for the new entry (dialogue +
      audio + vocab cross-links resolve).
- [ ] `CatalogQuestProvider` runs the quest through the REAL QuestManager
      + QuestRewardService: find→bring→completed, friendship + world-change.
- [ ] `BuildPreGenList` maps every new word (normal 0.85 / slow 0.70) and
      every new dialogue line (0.85, en-US).
- [ ] No new `error CS`; no frozen-assembly change except additive files.

## VISUAL (standalone screenshots, individually inspected)

- [ ] Grounding: contact + shadow on every rig in the quest (front + side;
      numbers are reference only — BakeMesh is BANNED from probes).
- [ ] Feet/shoes: no daylight gap, no 0.1m+ float, no dangle.
- [ ] Faces: readable at 1.5m+, no white ellipse, no skeleton/debug geometry.
- [ ] New NPC (if any): Golden Character Standard §11 (grounding, feet,
      face, materials, hair, silhouette, idle, walk, interaction framing).
- [ ] Target vs distractor separation (color + form + placement, ≥3m or
      crate-vs-pedestal language — never color alone).
- [ ] Quest hint: bubble shell/icon readable at gameplay distance, placed
      via `AnchorFor`, never over label/face/awning.
- [ ] No missing materials, no debug objects, no stale quest visuals
      (crate/carried/bubble/glow/flower lifecycle per state).
- [ ] First-impression frame: layered world, NPC identity readable,
      HUD compact.

## LEARNING (content review, stays play-embedded)

- [ ] Vocabulary row filled: WORD / CATEGORY / ACTIVE-or-PASSIVE /
      INTRODUCTION QUEST / REPETITION / NPC+OBJECT CONTEXT / AUDIO /
      EXPECTED CHILD ACTION / REVIEW OPPORTUNITY.
- [ ] Active vs passive distinction preserved (no flashcard flows).
- [ ] Loop preserved: SEE → HEAR → UNDERSTAND → ACT → SPEAK/RESPOND →
      WORLD REACTS → REWARD → RECALL. No classroom quiz unless required.
- [ ] NPC lines obey word caps (Milo ≤8, others ≤6); speech forgiving
      (soft speech, mispronunciation, VI+EN mix, partial words all pass).
- [ ] Wrong path exists and retries (never a lock, never punishment).

## REAL PLAYER (fresh build → standalone → real clicks → shots → analysis)

- [ ] Build `Succeeded`, 0 compile errors, managed DLLs fresh
      (exe-stub mtime is NOT freshness evidence).
- [ ] Boot: faces OK, 0 exceptions.
- [ ] Full route live: SPAWN → NPC → TALK → QUEST → TARGET → WRONG →
      RETRY → CORRECT → CELEBRATION → COMPLETION (plus any quest-specific
      path: pickup / swap / echo-silent where applicable).
- [ ] Player.log: 0 exceptions, wrongs counted once per bring.
- [ ] HUD reads correct at every beat (`Great job!` at completion).
- [ ] Beats photographed; at least spawn + wrong + complete inspected.
- [ ] "Works in editor" is explicitly REJECTED as validation.
