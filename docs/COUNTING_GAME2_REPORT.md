# COUNTING GARDEN — GAMEPLAY #2 "BẬC THANG CON SỐ" — REPORT (S3-P2Z12)

Date: 2026-09-24. Machines: ASUS (batch: tests/build/log) + maynode (foreground:
real-click journey). Blueprint: `COUNTING_GAME2_BLUEPRINT.md`. Reference:
gameplay #1 (`COUNTING_GAME_REPORT.md`) — reused as quality/architecture
reference, never copied as a gameplay.

## 1. Deliverables

| item | where |
|---|---|
| Shared actor kit (extracted from CountingDemo) | `Assets/A_World/CountingGarden/LessonActors.cs` (LessonActor + LessonActors.Build + PacedVoice) |
| Arena scene (lazy) | `Assets/A_World/CountingGarden/StairPlayScene.unity` + `StairHillBuilder.cs` (+ `StairRun` step-identity component) |
| Activity | `Assets/A_World/CountingGarden/NumberStairs.cs` (state machine + acting + feedback + lifecycle adopt) |
| Garden sixth plot | `CountingGardenBuilder.cs` (zone 5 "Đồi Bậc Thang" + mini hill + `Digit3`) |
| Lazy wiring | `GameInstaller.BuildStairPlayScene` + Build Settings entry; `CountingGardenArea.SetPlay(entry, anchors, island, bounds, follow, objective)` generalization + `StairLifecycle` |
| Zone routing | `GardenZoneSpot.playSceneName/demoGate`; `CountingGardenArea.PlaySceneFor` |
| SFX | `AudioDirector` new procedural `step` clip |
| Tests | `Assets/Tests/EditMode/CT-P53_StairGame.cs` (8 tests) + deliberate re-pins (P46D/P47A/P50A) + P51G CRLF normalization |
| Docs | this report + blueprint |

Firewall kept: no bus/quest/save changes, no new manager/service/singleton, no
second loader, no new package, save untouched (in-memory activity).

## 2. Verification (batch, ASUS)

- EditMode suite (final clean tree, driver deleted): **616 total / 611 passed /
  0 failed / 5 skipped** (baseline 608/603 → +8 new CT-P53 tests, 0 regression).
- Production standalone build: **Succeeded errors=0 warnings=2**
  size=110,222,003 (6 scenes: Bootstrap/Market/Math/CountingGarden/
  CountingPlay/StairPlay). `LWE.World.dll` in this build is byte-identical to
  the journey build's (which passed the journey) — build verified by hash.
- Boot smoke (production, ASUS): `[Boot] screen=1280x800 hud='Choose a gate!'`,
  FACE_OK, **0 exceptions**.
- maynode boot smoke (production, 640x360): `[Boot] screen=640x360`, FACE_OK,
  0 exceptions.

## 3. Journey (real clicks, maynode, driver deleted after the round)

Driver: temp `TempP53Journey` (removed from the repo): real Input-System mouse
injection (queued press held 4 frames), full-HD first round / 640x360 later,
screenshots next to the build. No teleport, no state pokes; every transition
below is the game's own reaction to a real click.

`[P53J]` summary of the PASSING loop (round 3 + 4):

    language card -> English
    math gate (walk-in portal) -> MathScene built (5.6s)
    garden gate -> Counting Garden entered (4.4s)
    stair plot -> focus zone 5 -> panel -> Play -> StairPlayScene (lazy)
    intro -> demo (One./Two./Three. on steps 1-2-3) -> handoff -> child control
    climb: step 1 -> step 2 -> overshoot step 5 (guidance) -> back to step 3
    success settles (result=True) -> walk home -> garden (6 south clicks)
    re-entry -> adopt completed (adopt=True result=True) -> JOURNEY_END

Evidence shots (maynode `E:\LWW\P53JBuild\p53j-shots`, pulled to
`D:\Vscode\p53j-shots3`): 00_boot, 01_language, 02_math_arrival, 03_garden,
04_stair_plot, 05_arena_arrival, 06_intro_board, 07_intro_stairs,
08_demo_start, 09_demo_step1..3, 10_handoff, 11_player_step1, 12_player_step2,
13_overshoot, 14_success, 15_garden_back, 16_reentry_adopt (maps to brief
§33 A–O).

Self-audit of the shots: the "3" board reads correctly and cleanly (the earlier
sawtooth seam is gone); the stairs read as a staircase (bead row per tread);
the teacher/student staging and the handoff read; the child climbs with the
follow camera (treads visible); overshoot shows the child high on the hill and
the actors at the base; success shows the "3 ✓" result board; the garden return
and the re-entry adopt are visually distinct.

## 4. Real bugs found and fixed during the round

1. **Stripped tread colliders** (found in review before the journey): the
   shared `Box()` helper strips colliders, so clicks fell THROUGH the steps to
   the ground behind the hill — the child would walk past the stairs instead of
   up them. Fix: `BoxSolid` keeps the collider on the 6 treads + landing (the
   bake uses render meshes, so the NavMesh is unaffected). Pinned in P53C.
2. **Digit seam** (journey shot): at 9cm proud of the panel the digit bars cut
   the panel and the seam shaded as sawtooth teeth. Fix: digits ride 18cm out
   (clear of the panel mass) + `NoShadows` on boards/digits (no shadow acne on
   the one thing a child must read). Garden mini board offset matched.
3. **Student stopped mid-stairs** (journey shot): the handoff's walk-home had a
   time-based fallback, leaving the student standing on the treads where the
   child had to walk through him. Fix: two-leg return (base → beside teacher)
   and the climb phase waits until the stairs are actually clear (8s safety).
4. **Overshoot counted while descending** (journey log: overshoots=3 in round 1
   for one real overshoot): the counter/gauge fired for every band > target,
   including walking back down. Fix: only upward passes count/nag (P53E pins:
   step 4 = 1 overshoot; standing high never completes).
5. **Driver (not shipped)**: a manual `InputSystem.Update()` made the press
   live for a single input tick — the UI saw it but `ClickToMove` did not
   (player never moved); off-screen/behind-camera clicks routed nowhere; the
   "English" HUD chip shares its label with the language card. Fixes: queued
   press held 4 frames (system update delivers it), target-walk loop with
   edge-clamped clicks (gates), in-scope language-card click, south-walk loop
   for the exit door. Also: `InputSystem.settings.backgroundBehavior =
   IgnoreFocus` so an unfocused window still accepts the injected input.

## 5. Known limitations / visual review required

- **Ramp-vs-tread feet**: the NavMesh bakes stairs as a ramp; the child's root
  rides it, so feet can be ~9cm above/below a tread at tread centres. Recorded
  as the next visual lever: enable the Height Mesh on this scene's bake
  (`buildHeightMesh`, candidate in the blueprint §9) or per-step visual snap.
  TECHNICAL acceptable; needs the human eye to judge at gameplay distance.
- **Demo/success camera framing** was widened after round 1 (demo shows the
  whole staircase; success includes player+step3+teacher+board+result); a
  human should confirm the final framing reads.
- **Evidence resolution**: the last journey ran at 640x360 (user order: the
  smallest window on maynode), so shots 15/16 are 624x321 — the earlier shots
  are 1280x720. Re-capture at a larger size if print-quality evidence is needed.
- The brief's "không click-to-move" was read as "no scripted pathing / no
  click-the-number mechanic": control stays the project's free click-to-move
  (the child chooses where to walk; geometry+physics constrain it).
- Treads are the click path; clicking the PLAYER's own capsule can consume a
  click (driver round 3 step-2 timeout) — a child clicks again; not a blocker.

## 6. Round b — garden mini lesson + small window (user feedback)

User (with a screenshot): the stair plot in the garden shows nothing — "NPC dạy
trẻ chơi ở đâu? Sao không hiện?" Correct: zone 5 had only a static miniature,
unlike zone 2's living 2-NPC theatre. Order: redo it properly.

What changed:
- `StairLessonDemo` (+ `IGardenZoneDemo` + shared `LessonMotion` kit):
  the stair plot now runs the same two-NPC teaching miniature as the ball plot
  (ambient audience-gated pass + focused try-run; the panel opens after one
  full pass). `CountingGardenArea` binds demos per zone; `CountingDemo`
  implements the interface.
- `WindowPlacement` (`-window-bottom-right`, inert by default): the game parks
  itself at the display's bottom-right (verified: `[WindowPlacement] parked
  bottom-right x=1280 y=720 size=640x360 display=1920x1080`).
- Bugs caught: self-referential colour fields in the demo (`Gold = Gold` →
  every "gold" prop rendered black — pinned in P53J); the exit "walk blindly
  south" missed the 1.35m portal disc by 1.5m (aim at the door via WalkToWorld
  + strict unload/fresh-instance checks); Success-phase Follow() spam (now
  once); tread-centre clicks hitting the climber's own capsule (aim the tread's
  left edge).
- Suite: **619/614/0/5** (clean tree, +P53J/K/I). Production (driver-free,
  with the mini lesson + placement): **Succeeded errors=0**, size=110,223,317;
  maynode boot `[Boot] screen=640x360` FACE_OK 0 exception.
- Journey round 2 (full-HD, driver deleted after): the strict loop now proves
  the exit and the re-entry for real —
  `exit=True arena-unloaded=True` (through the door, not a flag flip) and
  `adopt=True result=True fresh-instance=True` (a genuinely new arena adopting
  the completed picture). Climb: step 1 → 2 → overshoot 5 (overshoots=2) →
  step 3 → success → 0 exceptions. Evidence `D:\Vscode\p53j-shots6` (22 files):
  the garden mini lesson reads (teacher + student on mini stairs + gold beads +
  gold "3" board + Watch! HUD), the child climbs with the follow camera, and
  the success frame holds player + step 3 + teacher + both boards.

## 7. Verdict

**TECHNICAL PASS** — suite green, production build booted, and a real-click
journey completed the whole loop (intro → demo → handoff → child climb →
overshoot guidance → success → garden return → re-entry adopt) with 0
exceptions; every deliberate re-pin documented.

**VISUAL REVIEW REQUIRED** — the human eye still owns: the feet-on-treads read,
final camera framings, and the child-scale feel of the hill (per brief §34).

Not committed (awaiting the user's order).

## 8. 9-step journeys (brief ��38�39): targets 1, 3, 5, 7, 9 live

Driver: temp `TempP53Journey` (deleted after; real Input-System injection,
target-aware, strict exit/unload + fresh-instance re-entry checks). Three runs
on maynode, full-HD, 0 exceptions throughout:

- Run A (`-stair-target 1`, plan [1,3]): t1 full loop (base nudge ? step 1 ?
  overshoot 2 ? back ? success ? real door exit ? fresh target-3 arena);
  t3 full loop (demo 1-2-3 ? climb ? undershoot nudge live on step 1 ?
  overshoot 4 ? back 3 ? success ? exit ? fresh target-5 arena = 3?5 live).
- Run B (`-stair-target 5`, plan [5,7]): t5 demo 1-5 ? climb (undershoot
  nudges live) ? overshoot 6 ? back 5 ? success ? exit ? fresh target-7 arena
  (5?7 live). Driver lesson: single tread clicks can graze the climber''s own
  capsule (destination = self, no move) � fixed with per-step retry taps at the
  tread''s left edge (what a real child does); also fixed a 1.5m portal-disc
  miss by aiming AT the exit door via WalkToWorld.
- Run T7 (`-stair-target 7`, plan [7,9]): t7 clean climb (overshoots=0,
  highest=7, success) ? exit ? fresh target-9 arena (7?9 live); t9 full loop
  (demo 1-9 ? climb ? overshoot landing band 10 ? back 9 ? success + 9-recap)
  ? exit ? fresh target-1 arena (9?1 wrap live).
- Ladder proven live end-to-end: 1?3?5?7?9?1, every transition a fresh
  instance (never a vacuous adopt), every exit through the real door with
  unload verified (`arena-unloaded=True`).
- Evidence: `D:\Vscode\p54j-runA` (37 files: t1+t3), `D:\Vscode\p54j-runB`
  (t5), `D:\Vscode\p54j-runC` (t7+t9, 40 files). Audited: t9 board "9" reads
  clean; t9 success frame holds player-on-9 + teacher + board + result;
  t1 success composition (player + step 1 + teacher + both "1" boards);
  t5 arena reads (board "5", unified 9-step hill, landing + goal arch).
- Suite (clean tree, driver deleted): **630/625/0/5** (+CT-P54 �11).
- Production (driver-free, final code): build succeeded errors=0; ASUS boot
  smoke FACE_OK 0 exception; deployed to maynode and running small
  bottom-right for the user''s eyes.

## 9. Verdict (9-step round)

**TECHNICAL PASS** � suite green (630), production booted, and real-click
journeys completed targets 1, 3, 5, 7, 9 live (each: intro ? demo ? handoff ?
climb ? overshoot/undershoot guidance ? success ? real-door exit ? fresh next
rung), 0 exceptions; deliberate re-pins documented; no new architecture, no
save change, no new packages.

**VISUAL REVIEW REQUIRED** � the human eye still owns: feet-on-treads at the
top of the hill, camera framings at 9, and the child-scale feel (per brief
��33�34). Evidence folders above.

Not committed (awaiting the user''s order).
