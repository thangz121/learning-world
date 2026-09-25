# COUNTING GARDEN — GAMEPLAY #4 "XÂY THÁP THEO SỐ" (BUILD THE TOWER) — REPORT (S3-P2Z14)

Date: 2026-09-25. Machine: ASUS (batch: tests + production build + boot smoke).
Journey/evidence: **pending on maynode** (user order: "Không chạy journey trên
asus"). Blueprint: `COUNTING_GAME4_BLUEPRINT.md`.

## 1. Deliverables

| item | where |
|---|---|
| Gate portal + progress landmark (hub) | `MathWorldBuilder.cs` (`BuildTowerPortal`, `BuildTowerLandmark`, `BuildYardHubReturnLocal`) + `BuildYard/BuildYardLandmark.cs` |
| Micro-world travel module | `BuildYard/BuildTowerArea.cs` (MathScene-side, same contract as CountingGardenArea) |
| Arena scene (lazy) | `BuildYard/BuildTowerScene.unity` + `BuildYard/BuildTowerBuilder.cs` |
| Activity (blocks + pad + demo + player) | `BuildYard/BuildTowerGame.cs` (`TowerBlock`, `BuildPadZone`, `BuildTowerGame`) |
| Lazy wiring | `GameInstaller.BuildBuildTowerScene` + area bind + hub-portal guard |
| Portal contract | `MicroWorldPortal.BuildArea` (one new optional target; no new framework) |
| SFX | `AudioDirector` new procedural `block` clip |
| Tests | `CT-P56_BuildTower.cs` (14 tests) |
| Docs | this report + `COUNTING_GAME4_BLUEPRINT.md` |
| Journey tooling (for maynode, NOT committed) | `asus-<ts>-journey-driver.cs.txt` + `asus-<ts>-build-driver.cs.txt` sent with the bundle |

Firewall kept: no bus/quest/save changes, no new manager/service/singleton, no
second loader, no new package, save untouched (in-memory activity). The
human-reviewed Math Hub visual language is untouched; only the build_yard gate
gained its portal + landmark.

## 2. Verification (ASUS, batch)

- EditMode suite (clean tree, drivers removed): **657 total / 652 passed / 0
  failed / 5 skipped** (baseline 643/638 → +14 CT-P56, 0 regression).
  Artifact: `D:\Vscode\p56-editmode-results.xml`.
- Production standalone build (driver-free): **Succeeded errors=0 warnings=10**
  (8 scenes incl. BuildTowerScene). Artifact: `D:\Vscode\P56Build\LWE.exe`.
- Boot smoke (production, 1280x720 windowed, log-only):
  `[Boot] screen=1280x720 hud='Choose a gate!'`, FACE_OK, **0 exceptions**.
  Artifact: `D:\Vscode\p56-boot.log`.
- **Standalone journey: NOT run on ASUS** (user order). Pending on maynode with
  the shipped driver.

## 3. Journey tooling for maynode (exact steps)

1. Copy `asus-<ts>-journey-driver.cs.txt` →
   `Assets/A_World/BuildYard/TempP56Journey.cs` and
   `asus-<ts>-build-driver.cs.txt` → `Assets/Editor/TempBuildP56.cs`.
2. Build the journey player: Unity batchmethod `TempBuildP56.BuildJourney`
   (output `D:/Vscode/P56JBuild`, or edit the path constant).
3. Run: `LWE.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0
   -logFile <log> -journey` (windowed, foreground).
4. Expected `[P56J]` trace: language EN → math gate walk-in → Math hub →
   build gate walk-in (portal auto-fires) → BuildTowerScene → target 3 (ladder)
   → intro → demo stacks 3 → handoff → child picks/carries/places 3 (shots
   10/11) → success → overshoot spare → corrected → exit door → hub → re-enter
   gate → fresh instance adopts (shot 17) → `JOURNEY_END ... errors=0`.
5. Shots land in `D:/Vscode/p56j-shots` (00..17). Map to brief §29: A=03,
   B=04 (portal fires), C=04/05 (transition/entry), D=05, E=06, F=07/08,
   G=10, H=10, I=11, J=11_tower_3, K=12 (5 optional via `-build-target 5`),
   L=11 at 9 (`-build-target 9`), M=12, N=15, O=16, P=16, Q=17.
6. Do NOT commit the drivers (delete after the run) — production build must
   stay driver-free.

## 4. Design decisions worth knowing

- Gate: the existing `build_yard` gate (crane/jib/load/material blocks,
  human-reviewed) gained a whole-arch walk-in portal (fireRadius 1.8, 0.7m
  hub-side, cold-start latch so walking past never triggers) and a mini-tower
  landmark whose height follows the last completed target (brief §25).
- The micro-world is a REAL scene (`BuildTowerScene`, island +360x) loaded
  through the shared micro slot; the child spawns at the world entry, never at
  the tower (brief §3/§4).
- Blocks: 10 (target + spare) in the yard, bake-ignored + collider-stripped in
  the builder; the game re-adds pick colliders. Placement slot = stack index at
  place time (`Count`), so tower height can never disagree with the real stack;
  a placed block refuses re-picks (no duplicate counting).
- Demo and player use the SAME components/calls; the demo tower tidies back to
  the yard during handoff (arcs, staggered) so the child builds it themselves.
- Success camera: one proportional formula over the real tower height
  (`cam += (0, 0.15*top, -0.20*top)`, look at mid-tower) — brief §18, no
  target-9 special case (pinned P56N).
- Overshoot: the extra block visibly lands on top, the teacher recounts 1..N,
  points at the board, "N is enough.", the block hops home → success returns.
  Never a fail screen.

## 5. Known limitations / visual review required

- **Journey + evidence not run on ASUS** (user order). The first ASUS journey
  attempt exposed a DRIVER bug only (it kept clicking the old-world gate after
  Math travel, so the player wandered into the Counting Garden portal); the
  driver was fixed to condition-based walking and shipped for maynode. The game
  code itself was not implicated.
- Block/tower silhouette at 9 and the success framing read are human-eye items
  (maynode shots 11_tower_9 + 12).
- The demo's tidy-up (blocks arc home) is a deliberate readability choice
  (child starts from an empty pad); the human should confirm it reads as
  "dọn dẹp", not as a glitch.
- Hub landmark grows only after a completion (in-memory); a full game restart
  resets it to the 3-block preview (same in-memory policy as all activities).

## 6. Conflict-prone files for the maynode merge

Code: `Assets/A_World/MathWorld/MathWorldBuilder.cs`,
`Assets/A_World/MathWorld/MicroWorldPortal.cs`, `Assets/D_Audio/AudioDirector.cs`,
`Assets/_Bootstrap/GameInstaller.cs`, `ProjectSettings/EditorBuildSettings.asset`.
New assets (+ all .meta shipped): `Assets/A_World/BuildYard/` (5 files + folder
meta), `Assets/Tests/EditMode/CT-P56_BuildTower.cs`(+meta),
`docs/COUNTING_GAME4_BLUEPRINT.md`, `docs/COUNTING_GAME4_REPORT.md`.
`HANDOFF.md` deliberately NOT touched on ASUS (origin/main has the newer
maynode handoff; writing here would conflict).

## 7. Definition of Done self-audit (code+tests+build done on ASUS)

[x] gate identity/portal [x] transition = real micro-scene [x] independent
world + entry [x] orientation (teacher+board) [x] real demo [x] handoff
[x] real player pick/carry/place [x] block state + stack index [x] real tower
[x] target 1-9 + ladder/CLI [x] undershoot [x] overshoot [x] spam protection
[x] audio matches action (block/pickup/success + target-composed lines)
[x] camera shots + height formula [x] completion/reward/exit/return paths
[x] re-entry adopt [x] EditMode 657/652/0/5 [x] production build + boot smoke.
[ ] standalone journey + evidence — **pending maynode** (user order).
[ ] human visual review — pending.
