# S2 PIONEER MICRO-WORLD — COUNTING GARDEN (v2, technical record)

Date: 2026-09-22. Status: TECHNICAL PASS — HUMAN VISUAL REVIEW PENDING.
Suite: 567 total / 562 pass / 0 fail / 5 skip. Build: Succeeded errors=0
warnings=6 size=109,948,697. Boot: FACE_OK, 0 exceptions.

## 1. What this is (v2, user-corrected architecture)

The Counting Garden is its **OWN scene** — exactly like MathScene is to the
subject-selection hall. The child walks into the counting-garden gate in the
Math Hub -> the scene is **LAZY-loaded** (never at boot) -> warp lands in the
NEW YARD -> the yard holds **FIVE fenced garden zones in an arc** (enclosures
only for now, per user order: "chỉ cần quây khu lại, chưa cần làm gì thêm") ->
walking back to the exit marker unloads the scene and returns to the Math Hub.

## 2. Flow

Math Hub (MathScene) -> counting-garden gate mouth (walk-in portal) ->
WorldTransition.EnterMicroAsync(CountingGardenScene) [LAZY] -> tunnel + warp
to the garden EntryPoint -> arrival camera beat over the yard -> explore the
5 zones -> exit marker -> warp to the hub landing (outside the portal radius)
-> WorldTransition.ExitMicroAsync -> scene unloaded, camera follows, HUD
restored. The Math subject scene stays loaded underneath the whole time.

## 3. Reusable contracts (extensions proven by this pioneer)

- `WorldTransition` micro slot (Lead-owned, same machine): `EnterMicroAsync` /
  `ExitMicroAsync` / `MicroScene` / `MicroBusy`. LAZY by construction (the
  scene is requested only on the gate walk). `ReturnAsync` (subject unload) is
  refused while the micro scene is loaded. Honest failure semantics: failed
  load keeps the subject world + empty micro slot (retry allowed).
- `CountingGardenBuilder` (A_World/CountingGarden): scene contract
  (`SceneName`, `WorldOffset = (120,0,0)`, `EntryLocal`, `ZoneCount = 5`,
  `ZoneCenters`, `Anchors`, `EntryPoint`, `ExitPortal`) + code-built content.
- `CountingGardenArea` (scene-local module in MathScene): travel beats
  (tunnel, lazy load, warp in/out, camera frame/follow, HUD cache/restore) +
  pure state seams (`IsInside`, `CanEnter/Exit`, `TryEnter/ExitForTests`).
- `MicroWorldPortal` (reusable for the 9 remaining micro-worlds): walk-in
  XZ-poll trigger, Enter/Exit modes, one-shot + re-arm margin.
- `GameInstaller`: lazy scene arrival handler (`sceneLoaded` ->
  `BuildCountingGardenScene`), pushes the garden entry + anchors into the area
  module, wires the garden exit portal; the MAIN anchor lookup is explicit by
  name (the garden registry must never hijack the Math arrival beat).

## 4. The yard (scene content, code-built)

Island ground + rim, hedge ring, entry blossom arch + entry pad, exit disc +
exit portal, central courtyard pad, **5 equal fenced plots on a 50°–130° arc
(r=11)** each with: sand pad, Kenney fence ring with a mouth facing the
courtyard, a low blossom arch at the mouth and a `CGZoneNAnchor` marker — no
activities inside yet. Paths: entry walk, 5 spokes to the zone mouths and an
arc walk joining the mouths. Dressing: 6 blossom trees + petal carpets, 7
flower drifts, 18 falling petals, 2 butterflies, pastel rainbow behind the
yard (S6/S7 beauty kit). Scene-authored `ActivityAnchors` (8 slots).

## 5. Tests (CT-P46, 6/6)

- P46A hub portal rides the counting-gate mouth (enter mode, area id).
- P46B **lazy contract**: micro scene NOT loaded at subject entry; loads on
  demand; subject stays loaded; double enter spam-safe; subject return refused
  while inside; exit unloads; subject return works again.
- P46C failed load: false, empty micro slot, subject stays active, retry OK.
- P46D garden content: 5 zones on the arc + fences + anchors + entry/exit.
- P46E area state machine (no double enter/exit).
- P46F hub landing outside the portal re-arm radius.
- P37D deliberately re-pinned: the machine now owns 2 load/unload call sites
  (subject slot + micro slot), still the only place touching ISceneOps.

## 6. Known limitations (v2)

- The 5 zones are empty enclosures (user order) — counting activities are the
  next increment; the old math_counting pilot loop stays in the Math World
  garden (unchanged).
- No NPC staged in the new yard yet.
- No scripted real-input journey in this pass (EditMode contracts + build +
  boot verified); the lazy load is proven by the P46B machine test.

## 7. Human visual review checklist

1. Walk the counting-garden gate in the Math Hub: does the warp land in a
   NEW yard (not inside Math)?
2. Does the yard read as "counting gardens" (5 fenced plots in an arc)?
3. Is the entry arch/exit marker readable? Does the return trip work?
4. Do the zone mouths face the courtyard? Is walking between them clear?
5. Camera: arrival framing, then follow — any empty-ground framing?
6. Dressing: pink/blossom identity consistent with the Math World art?
7. Any stuck transition (gate spam, exit then re-enter, return to Main)?
8. Re-enter: scene loads fresh, no duplicates, no leftover state?

## 8. S3-P2X zone picker + play arena (user order §47B, 2026-09-23)

Flow: Math Hub -> counting-garden gate -> CountingGardenScene (5 plots) ->
click/walk up to a plot -> pink `GardenZonePanel` -> "Vào chơi" (staged plots
only) -> `CountingPlayScene` (own lazy micro slot swap: garden unload -> arena
load) -> the two-NPC Number-2 lesson plays there -> arena exit disc ->
garden reload + warp back to the plot.

- Zone doors: `GardenZoneSpot` (click pad with a collider for ClickRouter +
  NavMeshModifier so it never bakes; per-zone camera pair; `playEnabled` only
  for the demo plot, index 2).
- Focus beat: click OR proximity <= 2m -> camera frames the plot + HUD names
  it; double-click outside the panel cancels; the spot re-arms only after the
  child walks clear.
- Play swap reuses `WorldTransition`'s single micro slot
  (`ExitMicroAsync` -> `EnterMicroAsync`). Any failure reloads the garden and
  warps back to its entry: an empty world is never left under the child.
- `CountingGardenBuilder.BuildDemoStageInto(parent, origin)` is the ONE
  authored lesson layout (the garden theatre renders it as plot-2 scenery; the
  arena hosts the live `CountingDemo` via the `CountingPlayBuilder` overload +
  the island offset — no hardcoded map coordinates in the controller).
- Tests: CT-P50 (7). Suite 589/584/0/5. Build Succeeded errors=0. Headless
  wiring smoke: full loop, 0 exceptions. Human visual review of the new flow
  is PENDING (foreground build on maynode).

## 9. S3-P2Y feedback round (2026-09-23)

User asked for: clear plot boundaries everywhere, the demo running even before
any choice, attention shared with the 4 skeleton beds, the choose-panel only
after a full try-run, the play zone shrunk before selection (focus/expand on
play), and maximum demo animation.

- Boundaries: every bed gets a contrasting ground border ring (plus its fence
  ring); the demo plot gets the same border + a back/side fence ring placed
  outside the crescent-walk band (north stays open as the theatre mouth).
- Attention: `GardenZoneVignette` gives each bed an always-on counting
  performance (beads pop 1..N, crops breathe).
- Ambient demo: the garden hosts a pivot-compensated MINIATURE of the lesson
  (`DemoMiniScale`), camera beats off, actors/FX inside the scaled root — it
  loops for everyone; the arena keeps the full-size live version.
- Panel gate: focusing a staged plot restarts the lesson and shows the panel
  only when the loop completes (HUD "Watch!" meanwhile); skeleton plots keep
  the immediate Back-only panel.
- Animation: `DemoJuice` (confetti, sparkles, pulsing spotlight), squash &
  stretch, basket pop, ball sparkles, and a third camera shot for the
  "2 + tick" payoff.
- Tests: CT-P50H (8th zone test) + CT-P48B thresholds re-pinned to the
  miniature. Suite 590/585/0/5. Build Succeeded errors=0 warnings=4.

## 10. S3-P2Z real-click journey (maynode) — 7 fixes (2026-09-23)

The whole flow was driven on the home machine with REAL mouse injection
(Input System state events at screen coordinates; no teleport/direct state),
which exposed and fixed seven real bugs:

1. Zone-focus camera anchors were children of the scaled click pad → the
   camera landed at ground level (user report "camera đang fail"). Anchors now
   live on the unscaled garden root.
2. The zone panel's Play button was dead: `BindPanel` only stored the panel on
   the area and never handed the area to the panel. Two-way bind + self-heal.
3. The language chooser never opened: `SystemDialogBusy` tested dialog
   COMPONENT presence (all dialogs exist hidden from boot) instead of
   `IsShowing`.
4. The lesson card re-issued forever while the child stood in its radius,
   hiding the way out behind the camera. Budgeted re-issues + explicit
   hand-back to Follow (pinned by CT-P48E).
5. The hub HUD pill (bottom-centre) swallowed every world click near the
   child's feet: panel + text are display-only now (raycastTarget=false).
6. Returning from the arena landed on the plot and instantly re-focused it
   (camera pinned). Proximity re-arms only after walking clear.
7. The stage spotlight shipped without a material → magenta under URP; it now
   gets the shared Lit tint.

Journey result: language → Math gate → garden gate → plot focus → try-run →
panel Play → arena lesson → card release → walk-out → garden → walk-out → Math
hub, with **0 exceptions**. Evidence: `Temp/opencode/p2yj-shots/*.png`.

## 11. S3-P2Z3 — arena cleared for the child's game (2026-09-23)

User order: the play arena must contain ONLY the game (design incoming); the
Number-2 NPC demo was removed from it. The arena scene now stages
infrastructure + anchors only (ground/paths/entry/exit door/fences/dressing;
GameplayFocus at the empty field centre) and no `CountingDemo`, no `CGDemo*`
props, no spotlight. The garden keeps its ambient miniature + the "panel only
after the try-run" flow. CT-P50F re-pinned to the empty arena. Suite
591/586/0/5, build Succeeded errors=0.
