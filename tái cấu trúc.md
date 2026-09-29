# TÁI CẤU TRÚC LITTLE WORLD ENGLISH

MODE: FULL RESTRUCTURE / LEGACY PURGE / ARCHITECTURE MIGRATION.
Status: PLAN (audited from repository evidence at HEAD `149537a`, tree clean).
Rule: no destructive step runs before this file exists; phases are committed separately.

---

## 1. Current Repository State (evidence, not guesses)

- **HEAD / branch**: `149537a` on `main`, working tree CLEAN. Previous commit = Counting
  Garden cleanup (keep #2/#3, remove build/delivery/match gameplay).
- **Scenes in Build Settings (7)**: `BootstrapScene`, `MarketScene` (Main world),
  `MathScene`, `CountingGardenScene`, `StairPlayScene`, `RabbitPlayScene`,
  `DiscoveryScene`.
- **Current world tree**:
  - Main world = `MarketScene` (`MarketBuilder`): player spawn, 4-subject gate arc
    (`SubjectCatalog`: Math / Thinking / English / Vietnamese, human-reviewed hub-arc
    layout + brick walkways), Milo/Mia market quest content (dormant in production via
    `HubSelectionOnly`), return arch "Về", `PersistentCore` (Player/Camera/Router/HUD/
    Cursor/Marker/EventSystem).
  - `MathScene` = old Math hub (`MathWorldBuilder`): Tess + `math_counting` quest,
    10-gate ring (`MicroWorldCatalog`), ring/waypoints/carves, plus area modules
    `CountingGardenArea` (garden travel + lifecycles + zone picker) and `DiscoveryArea`.
  - `CountingGardenScene` = "Vườn Đếm" 2 plots (carrot → RabbitPlayScene,
    stair → StairPlayScene) + mini lessons + zone panel.
  - `DiscoveryScene` = #7 search gameplay (3 pockets, apple/flower/butterfly tasks).
  - Games: `RabbitPlayScene` (#3) + `StairPlayScene` (#2), each with its own
    `PlaySpot`/Wait→question→play→success→exit flow, lifecycle adopt.
- **Subjects today**: 4 gates (Math/Thinking/English/Vietnamese) + Meta `main`.
  Math is the only subject with an additive scene; the other three lead to in-world
  skeleton districts (`SubjectWorldBuilder`) + return triggers.
- **Approved games**: Rabbit Feeding (#3) + Number Stairs (#2) — human accepted.
- **Legacy/unaccepted gameplay present**: Discovery Garden (#7), old Math hub/gates,
  Counting Garden plot layer, market English quests (dormant in production).
- **Tests**: 73 EditMode files (`CT-001..012`, `CT-A01..04`, `CT-P01..P62`); suite
  **667 / 662 passed / 0 failed / 5 skipped** (`pclean-final-results.xml`).
- **Catalogs**: `MicroWorldCatalog` (10 gates); `SubjectCatalog` (4 subjects);
  `NpcRoster` (milo/mia/tess); `Content/quests` (8 JSON: 7 market + `math_counting`);
  vocab ~51; dialogues (milo, shopkeeper_mia); `audio_manifest.json`.
- **Content model today**: quests carry `npc`/aliases; no Subject/Skill/Game taxonomy.
- **Build**: production batchmode Succeeded errors=0, 7 scenes (`P62Build`).
- **Major shared systems**: `GameInstaller` (composition root), `MarketBootstrap`,
  `WorldTransition` + `ISceneOps`/`UnitySceneOps` + one micro slot, typed
  `GameEventBus`, `LocalSave`, `AudioDirector` + TTS/STT stack, `SmartCamera`,
  `ClickRouter`/`ClickToMove`, `ActivityLifecycle`, `ActivityFeedback`,
  `ActivityGuide`, `GameJuice`, `LessonActors`/`PacedVoice`, `MicroWorldPortal`,
  `MicroGateHint`, `IMicroWorldArea`, media/recording stack.

## 2. Product Owner Acceptance Baseline

**HUMAN_ACCEPTED (exactly two, permanently recorded):**
- TOÁN HỌC → ĐẾM → **Cho thỏ ăn** (Rabbit Feeding) — HUMAN_ACCEPTED
- TOÁN HỌC → ĐẾM → **Bậc thang con số** (Number Stairs) — HUMAN_ACCEPTED

**NOT ACCEPTED — everything else.** Discovery Garden, Build Yard, Delivery Village,
Match Meadow, Fruit Orchard, Sorting Park, Puzzle Workshop, Number Bridge, Memory
Grove, market English quests, old Math hub, old Counting Garden plot layer, all other
experimental gameplay and Micro-World implementations.

Engineering status never implies acceptance: build PASS, tests PASS, scene exists,
audit PASS are **not** human acceptance. Status vocabulary is fixed:
`UNPLANNED / SKELETON / IMPLEMENTED / ENGINEERING_VERIFIED / HUMAN_ACCEPTED / REJECTED`.
"LIVE" must never be used to imply acceptance.

## 3. OLD → NEW Architecture Mapping

| OLD | NEW |
|---|---|
| Main world hub yard + 4 subject gates | **(A) Subject Selection Yard** (5 gates) |
| Meta `main` / no subject concept in content | `subjectId` taxonomy (math/thinking/vietnamese/english/exploration) |
| Old Math hub (`MathScene` + `MathWorldBuilder`) | **DELETE** (replaced by generic B/C yards) |
| Old 10-gate `MicroWorldCatalog` ring | **DELETE** (gates are not product taxonomy) |
| Counting Garden plot picker (`CountingGardenScene`) | **(B)** Math → Skill Yard → **(C)** Đếm Game Yard |
| Rabbit Feeding door (garden zone 0) | Math → Đếm → **Cho thỏ ăn** (game gate → `RabbitPlayScene`) |
| Number Stairs door (garden zone 1) | Math → Đếm → **Bậc thang con số** (game gate → `StairPlayScene`) |
| Garden mini lessons (`RabbitLessonDemo`, `StairLessonDemo`) | **DELETE** with the garden layer (gameplay arenas unchanged) |
| Discovery Garden (#7) scene/game/gate/area/driver/tests | **DELETE** (future discovery must be redesigned under A→B→C) |
| Build Yard / Delivery Village / Match Meadow | already removed (29/09 cleanup) — only landmark gates remain; those gates go too |
| Other subject skeleton gates (fruit_orchard, sorting_park, puzzle_workshop, number_bridge, memory_grove) | **DELETE** (their concepts are not in the new skill trees) |
| In-world subject districts (`SubjectWorldBuilder` playgrounds) | **DELETE**; subject gates now lead to the subject's Skill Yard |
| Market English quests + Milo/Mia quest props | legacy content (dormant via `HubSelectionOnly`) → unregistered from product; engine/quest stack retained |
| Old `areaId` micro-world routing | generic `SelectionNav` (subject/skill/game context) + `WorldTransition` |
| Old tests CT-P45/46/47/48/49/50/51/53/54/55/56/57/59/60/61/62 (game/garden/hub-specific) | deleted; new `CT-S1x` selection-architecture tests + approved-game tests preserved |

## 4. NEW WORLD TREE (final, complete)

```
LITTLE WORLD ENGLISH
└── (A) SÂN CHỌN MÔN HỌC  [MainScene = subject yard, 5 gates]
    ├── TOÁN HỌC ──────────── (B) SÂN CHỌN KỸ NĂNG: ĐẾM | HÌNH HỌC | SO SÁNH | PHÂN LOẠI | THỨ TỰ
    │        └── ĐẾM ──────── (C) SÂN CHỌN TRÒ CHƠI: Cho thỏ ăn ✅ | Bậc thang con số ✅
    │        └── HÌNH HỌC ─── (C) [CHƯA CÓ TRÒ CHƠI]
    │        └── SO SÁNH ──── (C) [CHƯA CÓ TRÒ CHƠI]
    │        └── PHÂN LOẠI ── (C) [CHƯA CÓ TRÒ CHƠI]
    │        └── THỨ TỰ ───── (C) [CHƯA CÓ TRÒ CHƠI]
    ├── TƯ DUY ────────────── (B) QUY LUẬT | LOGIC | GIẢI QUYẾT VẤN ĐỀ | TRÍ NHỚ   (all C = empty)
    ├── TIẾNG VIỆT ────────── (B) NGHE | NÓI | LÀM QUEN CHỮ | TIỀN ĐỌC - TIỀN VIẾT  (all C = empty)
    ├── TIẾNG ANH ─────────── (B) LISTENING | SPEAKING | VOCABULARY | SENTENCES      (all C = empty)
    └── KHÁM PHÁ ──────────── (B) TỰ NHIÊN | ĐỘNG VẬT | THẾ GIỚI XUNG QUANH | ĐỜI SỐNG (all C = empty)
```
Five subjects; MATH and TƯ DUY stay separate subjects. Every skill owns a Game
Selection Yard (empty skills show `[CHƯA CÓ TRÒ CHƠI]`, no fake games).

## 5. Scene Architecture

| scene | role | loading | owner |
|---|---|---|---|
| `BootstrapScene` | boot + all services (composition root) | build index 0 | `GameInstaller` |
| `MarketScene` → **MainScene role = (A) Subject Yard** | subject selection (5 gates) | additive at boot | `MarketBootstrap`/`MarketBuilder` (hub-selection mode) |
| `SelectionYardScene` (**NEW, one generic scene**) | (B) Skill Yard **and** (C) Game Yard | lazy, shared micro slot via `WorldTransition.EnterMicroAsync` | `SelectionYardArea` |
| `RabbitPlayScene` | game: Cho thỏ ăn | lazy micro slot | arena `RabbitFeed` (unchanged) |
| `StairPlayScene` | game: Bậc thang con số | lazy micro slot | arena `NumberStairs` (unchanged) |

- ONE generic yard scene serves both B and C (data-driven by subject/skill context);
  no 21 scene copies. A yard is rebuilt on each entry (level + context set before load).
- Return flow: Game → Exit portal → Game Yard (C) → back portal → Skill Yard (B) →
  back portal → Subject Yard (A). Each yard owns a "back" portal; the arena exit
  returns to its game yard (not the old garden).
- Deleted scenes: `MathScene`, `CountingGardenScene`, `DiscoveryScene`.
- Generic layers: yard building, gates/portals, camera framing, HUD tunnel/objective,
  lifecycle ownership live in `SelectionYardBuilder` + `SelectionYardArea`; game
  scenes stay game-owned.

## 6. Folder / Code Architecture

```
_SharedKernel/            KEEP + ADD   (engine; add LearningMap.cs = pure taxonomy)
_Bootstrap/               KEEP          (GameInstaller, MarketBootstrap, UnitySceneOps)
A_World/
  LearningMap/            NEW           (yard data: subject/skill/game placement + status)
  SelectionYard/          NEW           (SelectionYardBuilder, SelectionYardArea,
                                         SelectionGate/portal wiring, placeholder board)
  Supermarket/            KEEP→RENAME-BY-ROLE (MainScene content = subject yard; file
                                         kept to preserve scene GUID)
  CountingGarden/         SPLIT: KEEP RabbitPlayBuilder/RabbitFeed/RabbitBowlDrag/
                                  StairHillBuilder+NumberStairs+StairRun/LessonActors/
                                  PacedVoice/DemoJuice/PlayerVisual deps
                                  DELETE CountingGardenBuilder, GardenZone*,
                                  RabbitLessonDemo, StairLessonDemo, IGardenZoneDemo,
                                  TempP55Journey
  DiscoveryGarden/        DELETE ENTIRELY
  MathWorld/              DELETE MathWorldBuilder/MathLearningEntries/MicroWorldGate
                                  (+Catalog)/MathBeacon/MathBloomDisplay/MathTokenCarry/
                                  CountingGardenArea/DiscoveryArea/TempP62Journey
                                  KEEP MicroWorldPortal, MicroGateHint, IMicroWorldArea
                                  (generic, moved under SelectionYard ownership)
  FullJourney/            RETARGET (driver walks the NEW A→B→C→game chain)
B_Brain/                  KEEP (quest/hint/learning engine; market quest wiring dormant)
C_Content/                KEEP (engine). Market quest JSONs = legacy content: archive
                          (see §8/§15) — engine fixtures (vocab/dialogue/audio pipeline)
                          stay so content-pipeline tests keep validating the ENGINE.
D_Audio/                  KEEP
Tests/EditMode/           MIGRATE (delete hub/garden/discovery tests; keep engine +
                          approved-game outcome tests; new CT-S1x architecture tests)
Editor/                   KEEP drivers (retarget)
```

## 7. Naming Migration

| old | new | reason | ids/GUIDs |
|---|---|---|---|
| `MarketScene`/`MarketBuilder` | role = Subject Yard (A) | product hierarchy | scene GUID + file kept |
| `MicroWorldCatalog` gate ids | `SubjectIds` + `SkillIds` + `GameIds` | taxonomy split, no micro-world ids as product taxonomy | old ids die with files |
| `math_counting` quest | not part of navigation (legacy) | content reset | JSON archived |
| garden zone indices | skill/game selection entries | hierarchy | replaced by data entries |
| `gameId` strings | `rabbit_feeding`, `number_stairs` | explicit accepted games | new stable ids in `LearningMap` |

No blind global renames; each rename is per-file with compile/test proof.

## 8. Content / Catalog Migration (new data model)

- **Taxonomy (`LearningMap`, pure C#9, _SharedKernel)**: `Subject { Id, DisplayName,
  Skills[] }`, `Skill { Id, SubjectId, DisplayName }`, `GameEntry { Id, SkillId,
  DisplayName, SceneName?, Status }`. Status enum as §2.
- Registrations: exactly 5 subjects, 21 skills (5+4+4+4+4), 2 games:
  `rabbit_feeding` + `number_stairs` (both HUMAN_ACCEPTED, scene names preserved).
- Navigation reads ONLY the LearningMap: gate labels/accents/status from data.
- Save/progress: `LocalSave` format untouched; activity state stays in-memory keyed by
  game id (lifecycles moved from `CountingGardenArea` to `SelectionYardArea`).
- Old catalogs removed: `MicroWorldCatalog`, garden zone naming, market quest
  registration from the product path (dormant/none in hub-only production).

## 9. Legacy Purge Plan

| category | action | notes |
|---|---|---|
| `DiscoveryScene` + `DiscoveryArea/Builder/Game` + gate + driver + CT-P62 | DELETE | §6 special rule |
| `MathScene` + `MathWorldBuilder` + `MathLearningEntries` + hub quest wiring + 10-gate catalog/gates + ring/carves/signposts + Tess hub presenter wiring | DELETE | replaced by B/C yards |
| `CountingGardenScene` + garden builder/zone picker/panel/vignette/mini-lessons | DELETE | replaced by Game Yard; arenas untouched |
| `SubjectWorldBuilder` in-world districts + return triggers | DELETE | subject gates now go to Skill Yards |
| 5 skeleton gate concepts (orchard/sorting/puzzle/bridge/memory) + landmark trio (match/delivery/build) | DELETE gates | not in the new skill trees |
| `TempP55Journey`, `TempP62Journey`, `TempFullJourney` (old legs) | RETARGET/DELETE | new journey follows A→B→C→game |
| Hub/garden/game tests CT-P45/46/47/48/49/50/51/53/54/55/56/57/59/60/61/62 | DELETE (approved-game outcome pinned fresh in new tests) | engine tests stay |
| Market quest content registration + quest props | dormant → unregister; JSONs archived, not referenced | engine + fixtures kept |
| Docs claiming old architecture | archive/annotate | HANDOFF stays historical |

Safety: delete in phase order with per-phase suite; anything shared (portal, hint,
transition, lifecycle, feedback, NPC kit, camera, audio) is verified by reference
scan before removal.

## 10. Shared Skeleton Extraction

| piece | now | target | legacy coupling to remove |
|---|---|---|---|
| `MicroWorldPortal` | MathWorld | SelectionYard (kept path) | old area fields (done), gains selection routing |
| `MicroGateHint` | MathWorld | SelectionYard | none (generic) |
| `IMicroWorldArea` | MathWorld | SelectionYard | old area impls die |
| travel beats (tunnel/warp/bounds/HUD) | `CountingGardenArea` | `SelectionYardArea` | zone picker/panel/garden specifics |
| game lifecycles | `CountingGardenArea` | `SelectionYardArea` (per game id) | garden coupling |
| `ActivityLifecycle`/feedback/juice/NPC kit/camera/audio | core | unchanged | none |
| persistent core (player/camera/HUD/…) | `MarketBuilder.BuildPersistentCore` | keep (Main stays loaded) | market quest content stays behind `HubSelectionOnly` |

## 11. Approved Game Protection Plan

- Files never touched during migration: `RabbitFeed.cs`, `RabbitPlayBuilder.cs`,
  `RabbitBowlDrag.cs`, `NumberStairs.cs`, `StairHillBuilder.cs`, `StairRun`,
  `LessonActors`, `PacedVoice`, `DemoJuice`, game scenes + their `.meta`/GUIDs, game
  SFX (`step`, `munch`, `pickup`, `basket`, `success`, `ding`).
- Only changes allowed: how they are REACHED (entry portal wiring) and where they
  RETURN (game yard). Their `Build` signatures may gain nothing; wiring moves.
- Regression pins: keep the arena-flow tests (renamed under new test ids) + a new
  end-to-end EditMode test that loads both arenas and completes the core loop.
- Verification after every phase: suite green + arena scenes build + boot smoke.

## 12. Navigation Flow (A→B→C→Game)

- **A (subject yard)**: walk into a subject gate → `SelectionYardArea.EnterSkill(subject)`.
- **B (skill yard)**: gates per skill; live skill (counting) → EnterGame(skill);
  empty skill → its gate exists but its Game Yard shows `[CHƯA CÓ TRÒ CHƠI]`.
- **C (game yard)**: gates per game; `rabbit_feeding` → `RabbitPlayScene`;
  `number_stairs` → `StairPlayScene`; empty → placeholder board only.
- **Game → exit** → back to the C yard → back portal → B yard → back portal → A yard.
- Today only MATH → ĐẾM has games; all other paths end at a labelled empty C yard.

## 13. Test Migration

- KEEP: engine/language/audio/save/camera/quest-engine contract tests (`CT-001..012`,
  `CT-A0x`, media/mic/telemetry series) after reference scan.
- MOVE/RENAME: approved-game core tests (former CT-P55/CT-P54/P53 cores) → new
  `CT-S2x` set asserting the SAME arena behaviour (gameplay frozen).
- DELETE: hub/garden/zone/discovery tests and everything pinned to deleted scenes.
- CREATE: `CT-S10` LearningMap (5 subjects, 21 skills, 2 games, statuses),
  `CT-S11` selection nav seams (enter/return/context/idempotence),
  `CT-S12` yard builder structure (gate per entry, empty placeholder, no fake games),
  `CT-S13` legacy-reference firewall, `CT-S14` approved-game end-to-end loop.

## 14. Build Settings Migration

- Current (7): Bootstrap, Market, Math, CountingGarden, StairPlay, RabbitPlay, Discovery.
- Remove: Math, CountingGarden, Discovery.
- Keep: Bootstrap, Market, StairPlay, RabbitPlay.
- Create: `SelectionYardScene` (generic B/C yard).
- Final (5): Bootstrap → Market → SelectionYard → StairPlay → RabbitPlay.

## 15. Documentation Migration

- Update: `docs/COUNTING_GARDEN_REFERENCE_REVIEW.md` (new architecture supersedes),
  `docs/JOURNEY_DRIVERS.md` (new journey chain), `docs/GAME_DESIGN.md` + `PRODUCT.md`
  only if they contradict the new hierarchy (annotate, no rewrite),
  add `docs/LEARNING_MAP.md` (subject/skill/game tree + status vocabulary).
- Archive/annotate: all `COUNTING_GAME*` docs as historical; `MATH_*`, `S2_*`,
  `P3.0.1_*` stay historical.
- Untouched: `HANDOFF.md`, `docs/HANDOFF/*`, W0/W1 logs (historical records).

## 16. Implementation Phases

**PHASE 0 — AUDIT + BASELINE** (this file). Verify: HEAD/tree/tests/build recorded.
Rollback: none (no code).

**PHASE 1 — NEW SHARED ARCHITECTURE SKELETON (additive)**
Goal: `LearningMap` + `SelectionYardBuilder` + `SelectionYardArea` + `SelectionYardScene`
shell + nav seams + CT-S10..S12 tests. Files: new only (+ optional scene). No old file
deleted, no behaviour changed. Verify: full suite green (≥667), build compiles.
Rollback: delete new files/scene.

**PHASE 2 — SUBJECT SELECTION (A)**
Goal: Main world = 5 subject gates (add KHÁM PHÁ), subject gates call
`SelectionYardArea.EnterSkill(subject)` (Math no longer travels to MathScene;
districts/return triggers stop being bound). Verify: suite + boot + journey stub
(A → skill yard). Rollback: revert MarketBuilder/Bootstrap edits (commit per step).

**PHASE 3 — SKILL SELECTION (B)**
Goal: generic skill yard renders the subject's skills (5/4/4/4/4) with live/skeleton
status; empty skills open their C yard with the placeholder. Verify: CT-S11/S12 +
boot + manual stroll. Rollback: phase commit.

**PHASE 4 — GAME SELECTION (C) + MIGRATE COUNTING**
Goal: counting's C yard shows the two games; game gates load the EXISTING arenas;
arena exits return to the C yard; lifecycles move to `SelectionYardArea`. Verify:
arena tests green + suite. Rollback: phase commit (arenas untouched for easy revert).

**PHASE 5 — LEGACY PURGE**
Goal: delete MathScene/CountingGarden/Discovery + builders/areas/catalog/districts/
drivers/tests per §9. Verify: reference scan zero, suite, build, Build Settings = 5.
Rollback: phase commit.

**PHASE 6 — CONTENT / CATALOG RESET**
Goal: unregister market quests from the product path; archive their JSONs; taxonomy is
the only catalog; `docs/LEARNING_MAP.md`; docs migration per §15. Verify: content
validator + engine content tests green.

**PHASE 7 — TEST / BUILD RESET**
Goal: new CT-S1x suite + retargeted FullJourney (A→B→C→game→return) + final production
build + journey on maynode (when online). Verify: suite + build + boot + journey.

**PHASE 8 — FINAL VERIFICATION**
Goal: acceptance checklist §18 + final report §18.

Adjustment rule: if a phase's step threatens the approved games, stop, document here,
choose the smallest safe correction, continue.

## 17. Risk Register

| risk | detection | mitigation | verification |
|---|---|---|---|
| serialized scene refs to deleted scripts (MarketScene/arena scenes) | build warnings "missing script"; boot log | delete scenes together with their scripts; never leave scene refs dangling | build errors=0 + boot smoke |
| GUID loss on moved shared files (portal/hint) | move with `.meta` intact | git mv semantics (done once already for MicroGateHint) | reference scan + suite |
| arena regression while rewiring entry/exit | suite + dedicated E2E loop test | arenas untouched; wire only portals; phase commit rollback | CT-S14 + suite |
| lifecycle ownership lost (adopt on re-entry) | re-entry adopt tests | move lifecycles to persistent `SelectionYardArea` before deleting garden area | CT-S14 + suite |
| Build Settings/loading breakage | boot smoke | keep 5-scene order; one micro slot contract unchanged | boot + journey |
| save/progress break | tests | save format untouched; in-memory keyed by game id | save tests untouched |
| main-world quest content accidentally active | boot log | `HubSelectionOnly=true` in production remains; quest registration removed from product path | boot log scan |
| shared core deleted by mistake | reference scan per file | only files listed in §9/§10 are removed; engine list in §7 protected | scan + suite + build |

## 18. Final Acceptance Checklist (post-execution)

- [ ] 5 subjects exist (Toán, Tư duy, Tiếng Việt, Tiếng Anh, Khám Phá)
- [ ] Math and Thinking are separate subjects
- [ ] Every subject has a Skill Selection Yard
- [ ] Every skill has a Game Selection Yard (placeholder when empty)
- [ ] Counting has exactly two games
- [ ] Rabbit Feeding = HUMAN_ACCEPTED (navigation changed, gameplay frozen)
- [ ] Number Stairs = HUMAN_ACCEPTED (navigation changed, gameplay frozen)
- [ ] No other game = HUMAN_ACCEPTED
- [ ] Old direct-to-game navigation removed
- [ ] Legacy gameplay deleted (Discovery/MathHub/CountingGarden plots/skeleton gates)
- [ ] Shared engine preserved
- [ ] Build works (5 scenes, errors=0)
- [ ] Tests work (suite green, no weakened assertions)
- [ ] No legacy playable paths remain
- [ ] Final tree matches the product architecture of §4

---

### Session execution log (updated as phases land)

- 2026-09-29: PHASE 0 complete — this plan (audit at 149537a; suite 667:662/0/5;
  build Succeeded errors=0; 7 scenes).
