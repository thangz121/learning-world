# PHASE 2A — CONTENT / QUEST DATA FOUNDATION (HANDOFF)

Date: 2026-09-14. Status: FOUNDATION LANDED, quest-2 world staging OPEN.
Phase 1: LOCKED (`docs/HANDOFF/PHASE_1_LOCK.md`, tags
`phase-1-locked` / `v0.1-phase1-locked`). This file is the 2A state.

## WHAT 2A PROVED

Adding a quest is now configuration work: the existing apple quest is
fully representable as DATA, and one NEW quest + one NEW word ride the
same pipeline with zero gameplay-logic duplication and zero changes to
any frozen system (QuestManager, QuestRewardService, ContentDatabase,
presenters, camera, HUD — none modified).

## FILES (all additive except pinned count updates)

Content (source of truth, `Content/`):
- `vocab/ball.json` (NEW): active object quest target (toy/round/blue/
  play/market, learning_v1, en-US, normal+slow). Bundle now 51 / 16 / 35.
- `quests/w1_mia_ball.json` (NEW): find_ball → bring_ball, shopkeeper_mia,
  4-freeze hints, simplify, friendship 10, flower_pot. PATTERN B.
- `dialogues/manifest.json`: +2 lines (`inst_07` "Ball please!",
  `ok_05` "Great! Ball!", mia_v1, ≤6 words) → count 36 → 38.

Code (production, additive):
- `Assets/C_Content/QuestPattern.cs` (NEW): QuestPattern A–E enum +
  total classifier (never throws). Layer-correct: no UnityEngine, no IO.
- `Assets/C_Content/QuestContentCatalog.cs` (NEW): QuestContentEntry
  (questId/chapterId/npc/pattern/objectives/targetObject/distractors/
  vocabulary/dialogueIds/wrong+correct/completionEffect/reward/next/audio)
  + pure BuildEntry + Validate + documented conventions (w1_ → chapter w1,
  targetObject = first Find/Bring target, completionEffect mirrors
  rewardWorldChange, nextQuest "" = terminal).
- `Assets/_Bootstrap/CatalogQuestProvider.cs` (NEW): runtime adapter over
  the FROZEN IQuestContentProvider boundary. Lives in Bootstrap because
  only the composition root sees both LWE.Brain and LWE.Content —
  placing it in C_Content failed to compile (CS0246), which PROVES the
  layering instead of weakening it. Not yet constructed (2F wires it).

Pins updated WITH content (validators must grow with the bundle):
- `Assets/Tests/EditMode/CT-008_*`: 50→51 files, 15→16 active.
- `tools/validate_content.py`: 15→16 active, 50→51 total.
- `tools/pregen_audio.py`: docstring only (collection is dynamic —
  ball + 2 lines flow into pregen with no code change).
- `seed_content.py`: INTENTIONALLY untouched — already drifted from
  Content/ (missing w1_mia_apple + 6th quest); Content JSON is source of
  truth. Rerunning the seeder would DESTROY content. (2D should either
  regenerate it from Content/ or delete it.)

Tests: `CT-P06_Phase2AFoundation` (6 tests) + test-asmdef gains
`LWE.Bootstrap` ref (needed to reach the provider):
- P06A apple-as-data (pattern B, chapter w1, audio map, next=w1_mia_ball).
- P06B ball-as-data (pattern B, terminal, validates clean).
- P06C classifier matrix over ALL 7 shipped quest JSONs + edges.
- P06D provider runs BOTH quests through the REAL QuestManager +
  QuestRewardService (find→bring→completed ×2, friendship 20, flower_pot).
- P06E pregen maps ball_normal/ball_slow + inst_07/ok_05.
- P06F grown-bundle invariants (51/16/38, targets resolve).

Evidence: EditMode **75/75 PASS** (69 frozen + 6 new, 0 failed);
`validate_content.py` → PASS (authoring, 16/35/51, 38 lines).

## VOCABULARY PROGRESSION (first rows; full table is 2C)

| WORD | CATEGORY | ACTIVE | INTRO QUEST | REPETITION | NPC+OBJECT CTX | AUDIO | CHILD ACTION | REVIEW |
|------|----------|--------|-------------|------------|----------------|-------|--------------|--------|
| apple | food | YES | w1_mia_apple (P1) | ball quest distracts; market quests reuse | Mia + crate | normal+slow, learning_v1 | find, carry, bring, (speak in market_help_mia) | market_help_mia speak_apple |
| ball | object | YES | w1_mia_ball (2F stages world) | apple quest pickup path (R9) pre-exposes | Mia + pedestal | normal+slow, learning_v1 (mapped, binaries in 2D pack) | find, carry, bring | future market quest |

Ball sequencing note: R9 already gives every player a neutral ball pickup
(WordSeen ball, 0 wrongs) inside the APPLE quest — so the ball word is
passively exposed in Phase 1 play BEFORE its active quest. The learning
loop stays play-embedded (no flashcards added anywhere).

## AUDIO MAPPING (2D owns binaries; mappings proven here)

- ball_normal / ball_slow: learning_v1, en-US, 0.85 / 0.70 (P06E).
- inst_07 / ok_05: mia_v1, en-US, 0.85, `audio/dialog/*.mp3` (P06E + pack).
- Reuse: retry_03 (wrong), plus generic greet/done lines (no new bytes).
- Binaries ship in the 2D pregen pack (`tools/pregen_audio.py` picks the
  new items up dynamically — verified by reading, not yet executed).

## DIALOGUE / HINT / REWARD MAPPING

- Apple entry: inst_02 + inst_04 (instruction), ok_01 (correct), retry_03
  (wrong). Ball entry: inst_07, ok_05, retry_03. All validated present.
- Visual hint: ball quest reuses the bubble + `AnchorFor` contract; the
  ICON stays apple until 2E stages a ball icon child (bubble supports
  icon-child swaps by design — no system change needed).
- Reward: flower_pot for both (existing visual binding); per-quest effects
  are a 2F decision, recorded here so it isn't accidental.

## HONEST VALIDATION STATUS (not claimed)

- The ball quest has NOT run standalone: it has no world staging yet
  (today the ball prop is R9's distractor with neutral-pickup rules;
  promoting it to quest TARGET needs presenter/world work = 2E/2F scope,
  explicitly after the pipeline per the mission order).
- What DID run: the full pipeline below the world layer — real JSON →
  real parsers → real QuestManager/QuestRewardService → completed ×2.
- No bulk content added (one quest, one word — per the scope rule).
- Phase 1 untouched: 69/69 frozen tests still green inside the 75/75.

## NEXT (in mission order, do not skip)

- 2B: quest-pattern reuse audit (which of A–E the world can stage today).
- 2C: full vocabulary progression table (this file holds rows 1–2).
- 2D: audio content pipeline (pregen binaries for ball + 2 lines).
- 2E: NPC template validation (Golden Character Standard on NPC #3).
- 2F: stage + standalone-validate w1_mia_ball per PHASE_2_QA_CHECKLIST.
- 2G/2H: real-player validation, then scale.
