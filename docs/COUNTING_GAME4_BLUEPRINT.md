# COUNTING GARDEN — GAMEPLAY #4: "XÂY THÁP THEO SỐ" (BUILD THE TOWER) — BLUEPRINT

Phase: S3-P2Z14 (2026-09-25). Full vertical slice: Math Hub gate → transition →
independent Micro-World → arena → demo → player gameplay → completion → reward →
exit → return to Math Hub.
Reference: gameplay #1/#2/#3 (`COUNTING_GAME*_BLUEPRINT.md` / `..._REPORT.md`) —
reused as QUALITY/ARCHITECTURE/PRESENTATION reference, never copied as a gameplay.

## 1. Experience (what the child lives)

NHÌN SỐ → NGHE → XEM BẠN LẤY KHỐI & XẾP THÁP → ĐI LẤY KHỐI → MANG VỀ →
ĐẶT LÊN THÁP → THÁP CAO LÊN TỪNG KHỐI → ĐỦ THÌ ĐƯỢC XÁC NHẬN.

Pattern: **PICK → CARRY → PLACE (STACK)** — vs #1 PICK→CARRY→PLACE (container),
vs #2 COUNT/MOVE/STOP, vs #3 PICK/CARRY/FEED. Identity: the child builds a real
tower whose height IS the count.

## 2. Gate (Math Hub) — finished, not redesigned

- Gate `build_yard` already exists in `MicroWorldCatalog` ("Sân Xây Dựng",
  "BUILD / CONSTRUCT", Gold) with crane + jib + load block + material blocks +
  barrel at (6.3, 0, -12.6). Human-reviewed hub language untouched.
- ADDED for this game: a walk-in portal at the gate (same contract as
  `CountingGardenPortal`: whole-arch coverage 0.7m hub-side, fireRadius 1.8,
  cold-start latch → walking past never triggers) → `BuildTowerArea.EnterFromHub`.
- ADDED landmark: `BuildYardLandmark` — a mini block tower beside the gate whose
  height reflects the last completed target (progress reflection, §25). Shows a
  3-block preview before any completion.

## 3. Micro-World: BUILD YARD (independent scene)

- `BuildTowerScene` (lazy, `WorldTransition.EnterMicroAsync` only, one micro
  slot) at island +360x — separate from Main/Math/garden/play/stair/rabbit.
- `BuildTowerArea` (scene-local module in MathScene, same proven contract as
  `CountingGardenArea`) owns: travel beats (tunnel/HUD cache, warp, router
  bounds, camera follow/frame), the `ActivityLifecycle("build_tower")` and the
  target ladder, so re-entry adopts the finished picture.
- Entry spawn CLEAR of the exit portal radius (J4 lesson).

## 4. Spatial layout (child scale; entry z=-3, board faces the child)

    ENTRY (0,-3)
      -> ORIENTATION: board "N" (0,7.6) + teacher (-1.4,6.6) + student (0.7,5.7)
      -> BLOCK YARD west (-2.2,3.0): 10 blocks (N + spare) as site material stacks
      -> BUILD PAD east (2.3,3.1): wooden platform + corner pegs + ghost slot
      -> RESULT board (3.4,-0.6, off sightline) + EXIT (0,-11.5)

- Loop entry→yard ≈ 4.6m, yard→pad ≈ 4.5m: a 20-60s round per block, exactly
  the #1/#3 rhythm. No overhead geometry crosses the walk lines; all dressing
  off-corridor (P42 discipline).

## 5. Camera-first shots

| shot | purpose | framing |
|---|---|---|
| teaching | teacher explains the board | board + teacher + student + yard/pad glimpse |
| demo | student builds | wide side view: yard + student + pad + growing tower |
| success | payoff | player + tower + teacher + board + result — look height FOLLOWS the real tower height (mid-tower), per §18 (no target-9 hack: the formula is `base + placed*blockH*0.5`) |
| follow | the child works | north of the child looking south; yard left, pad right always visible |

## 6. Block design + state

- Block: 0.5×0.44×0.5 warm cube (kid-scaled, readable silhouette), palette
  cycles 3 construction colours (tan / gold / coral) so the tower reads as
  stacked pieces without rainbow noise.
- ONE component decides state: `TowerBlock` — Available → Picked → Carried →
  Placed. Placement index = `placed.Count` at place time (never click count).
  Placed blocks can never be re-picked/-counted; duplicates impossible.
- Ghost slot: while carrying, a subtle pulsing outline cube marks the NEXT
  stack slot (preview only — placement still requires the confirm interaction).

## 7. Building logic (target 1-9, one arena)

- Target N only decides: how many blocks to place, tower height, completion,
  demo length. Ladder 3 → 5 → 7 → 9 → 1 (brief test targets 1,3,5,7,9);
  `-build-target N` CLI for diagnostics.
- Tower slot i (0-based): pad centre + (0, blockTop + i*0.44, 0). 9 blocks ≈
  3.96m — inside camera far and below the hedge silhouette; verified by pin.

## 8. Demo = real actions (never an animation)

Student: watches → walks to the yard (no teleport) → picks block (arc to his
fist) → carries → walks to the pad → places (arc + snap + wooden clack + tower
grows) × N → teacher counts each placement ("One block." … "N blocks.") →
"Đúng rồi!" → handoff ("Bây giờ đến lượt con nhé!") → walks back beside the
teacher → observes. Demo and player use the SAME components and calls.

## 9. Player loop

click block (walk → bend → carry) → click the pad OR stop on it (reach →
block flies to the next slot → snappy place) — the tower's height is the
count. Undershoot: settled below target → gentle "N more blocks!" (8s cooldown,
near the work only). Overshoot (N+1): teacher recaps the real stack 1..N,
"The board says N." + "N is enough.", the spare walks home; success returns.
Never a fail screen, never a punishment.

## 10. Completion / reward / exit / return

- Success: confirm + recap 1..N (pacer, one line in flight) + result board
  (digit + tick) + both NPCs celebrate + the child's own victory + lifecycle
  Completed + hub landmark grows to the completed height.
- The exit portal is ALWAYS live ("cổng về" landmark, same as #1-3, human
  reviewed pattern) — completion never auto-returns (brief §23).
- Exit: warp to the hub return spot, restore Math bounds/HUD, unload the micro
  scene (WorldTransition), no state left hanging. Re-entry adopts.

## 11. GitHub-first research (ADAPT / REFERENCE / REJECT)

| source | verdict | what was taken |
|---|---|---|
| Rex1121/Unity-RTS-Learning BuildingPlacementSystem | ADAPT (idea) | ghost-preview + snap + explicit confirm; implemented with the existing ClickRouter/IClickTarget + polled pad, no placement-mode framework |
| adammyhre gist SimpleBuildPlacer (socket snapping) | REFERENCE ONLY | confirms ghost/socket UX; socket graph rejected (deterministic stack index is the counting contract) |
| DanFilby/Unity_BuildingSystem (place on top, snapping) | REFERENCE ONLY | stacking-on-top exists but free-form; rejected as implementation (no runtime mesh/save system) |
| AngelofDeath19/Tower-Stacker-Unity + tower-stacker tutorials | REJECT | precision timing + physics collapse = fail states, unfit for 4yo; our tower never collapses |
| Unity-Technologies/Unity-Robotics-Hub pick_and_place | REFERENCE ONLY | fixed placement poses; ours are data slots |
| psmth35 FeedTheAnimals / #3 learnings | REUSE | lesson/actor/audio kits already shared |

Tools: Animator (PickUp/Celebrate reused), NavMesh runtime bake (reused),
SmartCamera (`FramePointFor` dynamic look for tower height), LessonActors +
PacedVoice + DemoJuice (shared kits), ActivityLifecycle, MicroWorldPortal
(one new optional target field — no new framework), AudioDirector (+1
procedural `block` clip, same pattern as `step`/`munch`). No new package, no
new manager/service/singleton; EventBus/save/quest untouched.

## 12. Test matrix → CT-P56

| brief item | test |
|---|---|
| A gate portal + landmark (hub) | P56A |
| B arena structure + child scale + stack geometry @9 | P56B |
| C targets 1..9 + block count | P56C |
| D full flow @3 (intro/demo/handoff/player/success) | P56D |
| E undershoot nudge | P56E |
| F overshoot correction (extra block never counts) | P56F |
| G spam safety (double pick / pick while carrying / place empty) | P56G |
| H block lifecycle + stack index determinism | P56H |
| I re-entry adopt (tower stays built, no replay) | P56I |
| J lazy slot + Build Settings + scene shell | P56J |
| K ladder + CLI | P56K |
| L speech safety @9 (recorded real run) | P56L |
| M carry robustness (walk away/back, far trek) | P56M |
| N camera height formula grows with the tower | P56N |

## 13. Definition of Done mapping

Gate identity + interaction + debounce; transition = real micro-scene load;
independent world identity (entry → orientation → yard → pad → result →
celebration → exit); teacher/board; real demo; handoff; real player
pick/carry/place; block state; real tower; 1-9; under/over handled; audio
matches action; camera serves action; completion/reward/exit/return/re-entry;
EditMode + Windows build + standalone journey + evidence; human visual review
pending (TECHNICAL PASS / VISUAL REVIEW REQUIRED split in the report).

## 14. maynode build round (2026-09-25): independent research + live-flow fixes

Independent GitHub-first research (this round) — ADOPT / ADAPT / REFERENCE / REJECT:

| source | verdict | what was taken |
|---|---|---|
| Arsenic-23/FortniteStyle-Building-System (grid snap + ghost valid/invalid + explicit build) | REFERENCE | confirms ghost + explicit confirm + occupancy; no networking/grid imported (the stack index IS the counting contract) |
| adammyhre gist SimpleBuildPlacer (socket snapping, `skipSocketIfOccupied`) | REFERENCE | socket occupancy ≈ our one-block-per-slot rule; the free socket graph is rejected |
| SST-Systems/Interaction-Objects (hand joint carry + outline hover) | ADAPT (idea) | the carried block rides the REAL fist bone (bug fixed below); proximity pulse is our highlight, no outline package |
| Unity-Technologies/BossRoom `PickUpAction` (parent to hand socket + PositionConstraint) | REFERENCE | same hand-socket pattern via the existing `CharacterPresentation` hand bone |
| tmorgner/UnityTutorialSystem `NextEventSelector` ("bouncing ball over the next step") | ADAPT | the silent next-block cue: one available block breathes while the child still owes blocks |
| thefuntastic/Unity3d-Finite-State-Machine + blaz-cerpnjak FSM | REJECT dependency | the Tick-driven phase enum + ActivityLifecycle already covers the sequence; no package |
| Cinemachine 3rd-person / austephner camera-collision | REJECT | open arena with authored shots; SmartCamera beats already suffice |

Fixes from the live-flow audit (real bugs in the ASUS slice, fixed at source):

1. **Carry followed the player ROOT** (the installer computed the hand but never
   passed it), so the block dragged at the child's feet. `BuildTowerGame.Build`
   now takes an optional hand and resolves `PlayerVisual.HandBone`; the
   installer passes it. Pinned by P56O (the block tracks the fist across the
   arena).
2. **The target ladder advanced the instant Success fired**, so (a) the
   overshoot correction could never trigger in live play (the spare counted
   toward the next rung while the board still showed the old target), and
   (b) a completed round leaked into the next rung mid-round. The advance now
   happens ONLY when the child LEAVES the yard (`ExitToHub`), mirroring the
   garden's IsInPlay gate for #2/#3: a completed round keeps its target, a
   spare block runs the gentle correction, and leaving advances exactly one
   rung with a fresh lifecycle for the next entry. Pinned by P56P.
3. **Gate interaction cue (brief §2):** `BuildYardGateHint` — a soft breathing
   gold glow on the threshold pad when the child enters the 5m approach radius.
   Presentation only; the portal keeps the walk-in trigger + cold-start
   debounce (passing by never fires it). Pinned by P56Q.
4. **Silent next-step cue (brief §30):** exactly one available block breathes
   while the child still owes blocks and no block is in hand. Pinned by P56R.
5. Flows at target 1 and 7 added (P56S) + entry/exit clearance (P56T).

Test count: CT-P56 is now 20 tests; full suite 664/659/0/5 (the merge with the
maynode stair round had moved the baseline to 660 before this round).
