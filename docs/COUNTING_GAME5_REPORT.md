# COUNTING GARDEN — GAMEPLAY #5 "GIAO HÀNG ĐÚNG SỐ" (DELIVER THE APPLES) — REPORT (S3-P2Z15)

Date: 2026-09-25. Machine: ASUS (batch: tests + production build + boot smoke).
Journey/evidence: **pending on maynode** (standing order: journey drivers are
committed with the bundle, journeys run on maynode). Blueprint:
`COUNTING_GAME5_BLUEPRINT.md`.

## 1. Deliverables

| item | where |
|---|---|
| Gate portal (hub) | `MathWorldBuilder.DeliveryPortal` + `DeliveryHubReturnLocal` (gate `delivery_village`, cottage identity already human-reviewed) |
| Shared seam | `Assets/A_World/MathWorld/IMicroWorldArea.cs` + `MicroWorldPortal` dispatch (3 areas, no breaking change) |
| Micro-world travel module | `Assets/A_World/DeliveryVillage/DeliveryArea.cs` |
| Arena (lazy) | `DeliveryVillage/DeliveryScene.unity` + `DeliveryBuilder.cs` |
| Activity | `DeliveryVillage/DeliveryGame.cs` (`DeliveryItem`, `DeliveryZone`, `DeliveryGame`) |
| Lazy wiring | `GameInstaller.BuildDeliveryScene` + area bind + hub-portal guard |
| SFX | `AudioDirector` new procedural `give` clip |
| Tests | `CT-P57_DeliveryGame.cs` (14 tests) |
| Docs | this report + `COUNTING_GAME5_BLUEPRINT.md` |
| Journey drivers (committed per standing order) | `DeliveryVillage/TempP57Journey.cs` + `Editor/TempBuildP57.cs` |

Firewall kept: no bus/quest/save changes, no new manager/service/singleton, no
second loader, no new package, save untouched (in-memory), no inventory system.

## 2. Verification (ASUS, batch)

- EditMode suite (frozen tree, drivers present): **671 total / 666 passed / 0
  failed / 5 skipped** (baseline 657 → +14 CT-P57, 0 regression).
  Artifact: `D:\Vscode\p57-editmode-results.xml`.
- Production standalone build (driver present, inert): see build log
  `D:\Vscode\p57-build.log`; artifact `D:\Vscode\P57Build\LWE.exe`.
- Boot smoke (no `-journey`): expect FACE_OK, 0 exceptions and NO `[P57J]` line
  (driver gated). Artifact: `D:\Vscode\p57-boot.log`.
- **Standalone journey: NOT run on ASUS** (standing order). Run on maynode with
  the committed driver (below).

## 3. Journey (maynode)

1. Build journey player: batchmethod `TempBuildP57.BuildJourney` → `D:/Vscode/P57JBuild`.
2. Run windowed foreground:
   `LWE.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile <log> -journey`
3. Expect `[P57J] ... JOURNEY_END shots=19 delivered=4 errors=0`; shots in
   `D:/Vscode/p57j-shots` (00..18).
4. Shot map to brief §28: A=03 (gate), B=04 (portal fires/transition), C=05
   (entry), D=06 (teacher + order board), E=07 (demo pickup), F=07/08, G=08
   (demo handover), H=09 (demo done), I=12_crate_1 (receiver reaction), J=09,
   K=10 (handoff), L=11 (player carry), M=12 (correct delivery), N/E undershoot
   (spare leg logs), O=14 (overshoot correction), P=13 (completion), Q=16
   (reward: result + exit cue), R=17 (return hub), S=17, T=18 (re-entry adopt).
   Target 9 evidence: rerun with `-deliver-target 9`.

## 4. Design decisions worth knowing

- The gate `delivery_village` already had the human-reviewed cottage + mailbox +
  parcel identity; this round only added the walk-in portal (fireRadius 1.8,
  0.7m hub-side, cold-start latch) — no hub redesign.
- **Seam**: `IMicroWorldArea` (Player/EnterFromHub/ExitToHub) — the portal now
  dispatches through one interface for all three areas; concrete fields kept so
  no old wiring/test breaks (P57A pins types + dispatch).
- Items: 10 apples (order + spare), primitives in the ApplePresenter language
  (known size, no PropKit pivot risk), bake-ignored + collider-stripped in the
  builder; the game re-adds pick colliders.
- Handover is a TWO-LEG flight: apple rides the player's fist → flies to Mia's
  HANDS (the real moment: receive gesture + thanks + teacher count + sparkle) →
  arcs into the crate slot (visible count = the crate, never a UI tracker).
- Wrong-item guard: the receiver accepts `ItemKind.Apple` only; a refusal
  cooldown keeps live proximity polls from spamming (pinned by P57G with a
  synthetic kind; pioneer content is apples only).
- Overshoot: the extra apple reaches her hands first, then the teacher recounts
  the crate 1..N + "Đủ N quả rồi." and it hops home → success returns.
- Exit cue (brief §22): a gold beacon above the exit arch, hidden until the
  order completes (also lit on adopt) — "the way home is open", no auto-return.
- Camera: teaching (order board) / demo (stall + lane + Mia) / success (player +
  Mia + crate + board + result) anchors; follow keeps stall left, receiver
  right.

## 5. Known limitations / visual review required

- **Journey + evidence not run on ASUS** (standing order) — maynode runs it.
- Apple look: primitives (red sphere + stem + leaf) match ApplePresenter's mini
  apple; the human eye should confirm they read at gameplay distance (the
  billboard lane arrows and the crate fill order are the other eye items).
- The demo's crate tidy-up (apples arc home) is deliberate (the child starts
  from an empty crate) — confirm it reads as "dọn hàng", not a glitch.
- In-memory progression (same policy as #1-#4); the hub gate carries no
  landmark this round (brief does not ask for one).

## 6. Conflict-prone files for the maynode merge

Code: `Assets/A_World/MathWorld/MathWorldBuilder.cs`,
`MicroWorldPortal.cs`, `CountingGardenArea.cs` (+interface),
`Assets/A_World/BuildYard/BuildTowerArea.cs` (+interface),
`Assets/D_Audio/AudioDirector.cs`, `Assets/_Bootstrap/GameInstaller.cs`,
`ProjectSettings/EditorBuildSettings.asset` (+DeliveryScene).
New assets (+ .meta shipped): `Assets/A_World/MathWorld/IMicroWorldArea.cs`,
`Assets/A_World/DeliveryVillage/` (6 files + folder meta),
`Assets/Tests/EditMode/CT-P57_DeliveryGame.cs`(+meta),
`docs/COUNTING_GAME5_BLUEPRINT.md`, `docs/COUNTING_GAME5_REPORT.md`,
drivers `DeliveryVillage/TempP57Journey.cs` + `Editor/TempBuildP57.cs`.
`HANDOFF.md` deliberately NOT touched on ASUS.

## 7. Definition of Done self-audit (code+tests+build done on ASUS)

[x] gate identity/portal [x] transition = real micro-scene [x] independent
world + entry [x] orientation + order board [x] teacher [x] real demo
[x] handoff [x] real pickup/carry/delivery [x] receiver reacts (notice/ receive/
thanks/hop) [x] target 1-9 + ladder/CLI [x] undershoot [x] overshoot
[x] wrong item [x] spam protection [x] audio matches action [x] camera shots
[x] completion/reward [x] exit cue [x] return path [x] re-entry adopt
[x] EditMode 671/666/0/5 [x] build + boot smoke [x] drivers committed.
[ ] standalone journey + evidence — **maynode** (standing order).
[ ] human visual review — pending.
