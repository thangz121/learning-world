# Phase 2E Golden NPC Template

## Status

PASS — NPC presentation now has reusable contracts CONSUMED by both Milo
and Mia (not paper abstractions): one shared material helper, one NPC
identity roster, one bubble icon API. Golden Character Standard intact,
zero visual regression, zero behavior change. No QuestStager (2F owns it).

## Scope

Presentation layer around NPCs only (§25 held: no Stager, no ball staging,
no third NPC, no camera/character/audio redesign, no speech work).

## Baseline

- Phase 1 LOCKED (Golden Character Standard v1 locked 2026-09-12).
- 2A 75/75 → 2C 86/86 → 2D 92/92 at 2E start. Git CLEAN (110976a).
- 2B P1 items owned by 2E: bubble icon api (P1-5), QuestEntry.npc
  consumption, label literals. Staging couplings stay 2F.

## Golden Character Standard

UNCHANGED and re-proven: CharacterRoot gameplay authority, VisualRoot
presentation-only, +Z forward, 0.5 visual scale, frame-2 face build, eye
pane p95, expressions/blink/breath/glance, Idle/Victory/PickUp triggers,
no root motion, instance-only tints, isReadable FBX. 2E raises reuse TO
this bar (shared helper moved INTO CharacterPresentation) — nothing was
simplified to make templating easier. Grounding + face remain hard visual
acceptance criteria (§Grounding Evidence, §Visual Acceptance).

## Milo/Mia Responsibility Matrix (traced, not assumed)

| Capability | Milo | Mia | Shared? | Templated? |
|---|---|---|---|---|
| CharacterPresentation kit (face/blink/breath/glance/shoes) | ✓ | ✓ | YES (same API) | pre-existing — no work |
| Set/PulseExpression, LookAt, BlinkNow | ✓ | ✓ | YES | pre-existing — pinned P09H |
| TintSharedMaterials (~40 lines verbatim) | copy | copy | YES verbatim | 2E: moved to CP, both call it |
| Wave LateUpdate (~15 lines + bone state) | ✓ | ✓ | YES, stateful | KEPT duplicated (§6: abstraction would tangle ordering) |
| BuildVisual skeleton | Worker_Male, lift 0.01 | Worker_Female, lift 0.02 | pattern, values differ | DEFERRED (one example per rig; helper shape recorded §Deferred) |
| QuestStarted re-key | generic | generic + carry reset | YES | pre-existing — no work |
| QuestCompleted | Celebrate + Happy baseline | Celebrate + Happy baseline + hygiene | YES | pre-existing — no work |
| Greet zone + facing cone | ✓ | — (shopkeeper by design) | CHARACTER-SPECIFIC | kept in Milo |
| Carry + bring/wrong/echo paths | — | ✓ (quest-specific) | QUEST-SPECIFIC | kept in Mia → 2F stages |
| Click role | talk-gate + instruction replay | wave + bring/wrong | DIFFERENT ROLES (greeter vs receiver) | kept separate — no false base class |
| Voice | Milo static | none (Milo voices reactions) | CHARACTER-SPECIFIC | kept |
| Labels | Bootstrap literal | Bootstrap literal | hardcoded | 2E: roster-sourced |
| Bubble icon | apple (world-level) | apple (world-level) | hardcoded | 2E: SetIcon API |

## Shared vs Character-Specific Behavior

SHARED (now single-sourced): face kit + expressions + LookAt + blink +
shoes + material tinting + capsule pattern + quest-event reaction pattern
+ label component + bubble placement + icon builder.
CHARACTER-SPECIFIC (kept, with reason): greet behavior (Milo hosts
first-contact), carry/bring state (Mia receives — quest role, not
character essence, but inseparable until 2F stages the second receiver),
voice ownership, vest/hat identity tints (values, not mechanism),
spawn/anchor positions, label timing (Milo frame-one, Mia post-intro).

## NPC Identity Contract

Stable id ≠ display ≠ quest alias ≠ voice. `NpcDefinition`
(id/displayName/voice/questNpc/labelHeight) + static `NpcRoster`
(C_Content, pure data, content-owned per AGENTS.md). Cast today: milo +
mia ONLY (P09L pins count 2 — no third NPC invented). Role/behavior
dispatch deliberately EXCLUDED (2F designs behavior when the second
staged receiver exists).

## NPC Roster

Minimal by design: lookup by id + by quest alias, nothing else (no spawn
framework, no behavior tree, no schedule, no relationships — §8 held).
Bootstrap reads display names + label heights from it (literals gone;
fallbacks preserve behavior if roster ever misses).

## QuestEntry.npc Consumption (2B false-reusability #4 CLOSED)

`NpcRoster.ResolveQuestNpc` resolves the CONTENT alias without branching:
all 7 shipped quest JSONs (`shopkeeper_mia`) resolve to Mia (P09C proves
over real files). Presenters still don't guess NPCs from quests (correct:
no dispatcher built); the roster is the presentation-layer contract 2F's
stager will consume for receiver/narration wiring.

## Bubble Icon Contract (2B P1-5 CLOSED)

`SetIcon(WordId)`: rebuilds ONLY the icon child from a word→spec table
(apple: red fruit + stem + leaf, byte-identical to R9; ball: blue sphere
in distractor language; unknown: neutral dot — never apple by accident).
Container (AskIcon) + pulse + shell + tail untouched. Production consumes
it (MarketBuilder stages apple explicitly through the API). "Target
changes → icon changes" proven live (log apple→ball→apple) + deterministi-
cally (P09E/F/G/K: regression, ball, fallback, swap-hygiene).

## Dialogue Presentation Contract

Unchanged paths: Milo static voice (2D key contract intact — 2E touches
no text/params), HUD chip, bubble show/hide by quest phase. New: roster
voices are the single source 2F narration reads (P09D pins milo_v1/mia_v1
+ alias consistency). No new dialogue system (none needed).

## Expression Contract

CharacterPresentation Set/PulseExpression + LookAt(yaw,hold) + BlinkNow —
already shared, both NPCs + player consume. 2E pins with one fixture
(P09H: Sad frown ↔ Happy smile + LookAt + Blink on shared kit).
Semantic vocabulary (Correct/Wrong/Complete/Greeting → expressions) stays
in presenters (Mia Sad on WrongChoice, both Happy on CorrectChoice,
durable Happy baseline post-quest) — gameplay sends moments, presentation
decides faces (§13 held, no change needed).

## LookAt Contract

Reusable as-is: CP.LookAt API + camera-facing convention (follow cam
behind player ⇒ NPCs face camera, §2 of the Standard). No Milo/Mia/quest
hardcode (verified by read: presenters compute camera direction live).
No camera architecture change.

## Animation Contract

Shared pattern, documented (not rebuilt): Idle loop default; `Celebrate`
and `PickUp` triggers with auto-return (BOTH controllers have them —
verified in code: Milo/Mia presenters fire identical trigger names);
player adds Walk on `Moving`; generic rigs, no avatar, no root motion.
CHARACTER-SPECIFIC note: greet/wave procedural layer duplicated
deliberately (§Shared table). No new states added ("template should have
X" states rejected per §15).

## Anchor Contract

AnchorFor (bubble placement) + label-follow + fist-bone hand anchor +
interaction capsules: all pre-existing, all reused unchanged. P09J pins
AnchorFor math. No hardcoded world coordinates added; none removed.

## Grounding Evidence (runtime, visual)

p2e-milo: Milo planted on blue mat, contact shadow under both shoes, no
daylight gap. p2e-mia: Mia planted at stall, feet on grass with shadow,
counter occlusion normal (staging, not float). Faces read at closeup
(pupils + glints + brows + mouths on both). No sinking, no animator pop.
Criterion (§16) met on VISUAL truth, not transform values.

## Visual Acceptance (§28 questions, answered from inspected shots)

- Feet touch ground: YES ×2, shadows attached.
- Faces clean/readable: YES ×2 (no white ellipse, no helper geometry).
- Hair/hats read: yellow Milo / pink Mia, distinct at a glance.
- Hands/feet coherent doll shapes: YES.
- Expressions visible: neutral-readable at rest (quest moments
  covered by Phase 1 evidence; 2E changed no expression paths).
- UI overlap: labels clear of faces ("Milo"/"Mia" crisp); bubble shell
  crowds the Mia closeup from the authored south pose (known
  hint-closeup family, non-blocking — same class as Phase 1 item #4).
- Same game: YES — shared finish language preserved (the tint move is
  byte-identical logic; shots match Phase 1 look).
- No debug/skeleton artifacts: YES. No regression vs Phase 1 look.

## Runtime Validation (§27: 17/17)

Fresh proof build Succeeded → foregrounded run, quest started through
Milo's REAL click entry, all framings inside focus windows (first smoke
run shot AFTER window expiry — caught, fixed, re-run; lesson 45):
spawn/milo/mia/apple-icon/ball-icon/restored shots (6/6 written) +
icon transitions logged + zero exceptions. Clean final build Succeeded +
boot 3×FACE_OK + 0 exceptions. Cross-trigger: single talk → single quest
start, HUD "Find the apple" ✓. per-shot mapping caveat: under background
present-stall, screenshot requests queue and flush late — two icon files
captured neighbor states (lesson 46); the SET proves both icons render +
round-trip, log proves sequencing. No claim rests on a single frame.

## Test Coverage (§26: A–N)

P09 (11 tests): A ids · B display/heights · C all-quest npc resolve ·
D voices · E apple regression · F ball same-api · G fallback · H
expression+LookAt+blink · J AnchorFor · K swap hygiene · L no branching.
M/N (Milo/Mia regression): full suite green (below) + runtime smoke +
visual acceptance above. Prefab validation: CT-S01G (real-mesh eye band)
still green. No visual-quality claim from unit tests alone (§35 held).

## Performance / Complexity (§29)

Net code ADDED is small (roster ~70 lines, icon spec ~60, helper move
~25); net duplication REMOVED (~80 lines across two presenters).
No new Update loops (SetIcon builds on call only; roster pure static;
labels unchanged cadence). No new subscriptions, no coroutines, no global
state, no prefab cycles. No micro-optimization attempted.

## Files Changed

Shared/template (additive + dedup):
- Assets/_SharedKernel/CharacterPresentation.cs (+TintSharedMaterials)
- ADD Assets/C_Content/NpcRoster.cs (+.meta)
- Assets/A_World/WorldQuestionBubble.cs (SetIcon + spec table + builder)
Consumers (same behavior, new source):
- Assets/B_Brain/MiloPresenter.cs (−40-line copy → shared call)
- Assets/B_Brain/MiaPresenter.cs (−40-line copy → shared call)
- Assets/_Bootstrap/MarketBootstrap.cs (labels from roster)
- Assets/A_World/MarketBuilder.cs (bubble icon via SetIcon(apple))
Tests: ADD CT-P09_NpcTemplate.cs (+.meta). Docs: this file.
Frozen systems untouched: QuestManager, LearningService, HintService,
audio pipeline, camera core, quest content (no JSON changed).

## Known Limitations

- Wave gesture + BuildVisual skeleton still duplicated (deliberate —
  §Shared table; helper shape for BuildVisual recorded below for 2F).
- Roster covers 2 NPCs; third NPC exercises extensibility (claim:
  "additive entry + presenter", NOT proven until one exists).
- Bubble specs cover apple/ball/unknown; future targets add one table
  row (documented, unproven until used).
- Screenshot/file-state mapping under present-stall needs foreground
  discipline (lessons 44/45/46 — all observed, all worked around).
- Narration still apple-worded (Milo/HUD) — 2F entry-driven work (P1-2).

## Deferred to Phase 2F

- QuestStager (同意: NOT built in 2E — O below is NO).
- Ball staging (prop/carry/receiver/icon-call/narration/HUD/reward/chain).
- Provider runtime switch; quest chaining runtime; simplify consumer.
- BuildVisual helper (candidate params: prefab path, lift, scale, tint
  set, animator triggers — needs NPC #3 to validate, not two).
- Entry-driven narration (roster voices + manifest lines are ready).

## Deferred to Phase 4

- Retention/persistence/effectiveness (standing); Played-vs-Audible
  consumer semantics; bulk NPCs; adaptive behavior.

## Git Evidence

- Baseline CLEAN (110976a). Commit `phase2e: golden NPC template`
  (contracts + consumers + tests + doc). No tag (no phase lock claimed).
  Post-commit CLEAN.

## Final Verdict

A. Milo + Mia share: face kit, expressions, LookAt, blink, shoes,
   material tinting (now single-sourced), capsule pattern, quest-event
   reaction pattern, label component, bubble placement + icon builder.
B. NOT abstracted (deliberately): wave gesture, BuildVisual skeleton,
   greet behavior, carry/bring state, voice ownership, identity tint
   values, anchors/positions, label timing, any behavior dispatcher.
C. Identity canonical source: NpcRoster (id → display/voice/aliases).
D. QuestEntry.npc consumed by: NpcRoster.ResolveQuestNpc (presentation
   layer); all 7 shipped quests resolve. Runtime stager use = 2F.
E. Roster enough for future NPC? Structurally (additive entries) YES;
   proven with a third NPC NO — claimed as designed, not as proven.
F. Bubble icon data-driven? Word-driven table + fallback YES; Content-
   JSON-driven NO (one table row per future word — proportionate).
G. Ball via same API without code change? YES — proven live (log +
   photographed blue ball, no stem/leaf) and in tests.
H. Expression/LookAt/anchor reusable? YES — pre-existing shared kit,
   both NPCs consume, pinned P09H/J.
I. Milo regression? NO — suite green + Milo closeup matches Phase 1.
J. Mia regression? NO — suite green + Mia closeup matches Phase 1.
K. Golden Standard regression? NO — standard untouched; helper moved
   into it verbatim; visuals match.
L. Grounding visual proof? YES ×2 (contact + shadow, no gap).
M. Remaining couplings: narration apple-wording, receiver apple-keying,
   lifecycle apple-keying, reward const, chain absence (all itemized 2F).
N. Deferred to 2F: P1-1/2/3/4/6/7 + Stager + ball staging + provider
   switch (unchanged from 2B, now with presentation contracts ready).
O. QuestStager in 2E? NO — not designed, not built, not needed yet.
P. 2F ready to stage Ball? YES — roster + icon API + voices + audio
   binaries + catalog entries + QA checklist all staged as inputs;
   2F's job is now pure staging, no foundation missing.
