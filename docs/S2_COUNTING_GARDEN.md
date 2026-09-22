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
