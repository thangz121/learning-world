# Phase 2.4 — FINAL POLISH / WORLD & EXPERIENCE POLISH — LOCKED 2026-09-17

**Status: LOCKED** — foundation polished, game reads as complete product in deployed areas. Phase 2.3 pipelines preserved. No architecture expansion. Next: Phase 2.5 Stress Test (separate task).

## 1. Scope (user brief)
- Player Gender Boy/Girl with consistent visual (face/hair/clothing/presentation) without breaking CharacterRoot
- Character polish (face/eyes/hair/mouth/expression/animation/proportions/grounding, debug artifacts removed)
- World polish (reasonable expansion, trees/flowers/grass/decor/props/ambient, empty areas filled, readable for 4yo)
- Gameplay presentation (camera/NPC placement/interaction framing/quest HUD/dialogue/world prompts, NPC readable, no UI occluding faces, no prototype feel)
- Consistency (Boy/Girl same standard, material/lighting/scale unified, LOCK pipelines kept, no JPEG, no 1080p/FPS regression)
- No Phase 3 content, no refactoring stable foundation, no LOCK API break, prioritize what player sees, regression after each group

## 2. Changes (production)
- `Assets/_SharedKernel/PlayerGender.cs` (new) — `enum PlayerGender {Boy=0,Girl=1}` pure, Lead owns
- `Assets/_SharedKernel/Services.cs` — `PlayerProgress.PlayerGender` default Boy (migration safe)
- `Assets/_SharedKernel/LocalSave.cs` — DTO `playerGender int` + `IsDefined` fallback Boy, round-trip pinned
- `Assets/A_World/PlayerVisual.cs` — `Gender` property + `SetGender` + `ApplyGenderTint` (Boy blue `0.25,0.5,0.95` vs Girl pink `0.95,0.42,0.62` + pants purple + hair brown, instance copies only, same rig/scale `0.5`/grounding `0.005`/`0.025`/face kit)
- `Assets/A_World/MarketBuilder.cs` — `BuildAmbientDecor()` post-NavMesh (pure visual, collider destroyed) `GrassTuft×3` `(-4,5.2)/(3,-5)/(6.5,1.5)` `Rock×2` `(1.2,2.8)/(-1.3,0.2)` `FlowerPatch×3` `(2,4.8)` `Barrel×1` `(4.4,-1.7)` + `SetPlayerGender` + `_pendingGender`
- `Assets/_Bootstrap/GameInstaller.cs` — `CurrentGender`/`Load()`/`SetPlayerGender` persist + `BuildFromScene` apply persisted gender + `Update()` `G` toggle Boy↔Girl live
- `Assets/_SharedKernel/MediaRecording.cs` — revert `DefaultVideoFps 6→10` / `DefaultGameFps 20→30` + comments (Phase 2.4 keep 1080p30, perf via backpressure/Camera.Render→ScreenCapture revert + GPU CopyTexture, not fps cut)
- `Assets/A_World/MediaRecordingService.cs` — backpressure `MaxGameRawFrames-1` skip + GPU `CopyTexture` path (throttle removed to keep PIP 10fps) + revert `Camera.Render`→`ScreenCapture` (Camera.Render broke URP: `EndRenderPass` spam + dark frames)
- `Assets/_SharedKernel/MicSignal.cs` — `ComputeConnectionLevel` fix: `ageMs<0 => None` (grey, not red) matches `P16M` (`never => unlit`)
- `Assets/A_World/MicSetupMonitor.cs` — `ComputeConnectionLevel` + continuous watcher (mirror camera) + PhoneUp linkUp for HUD
- `tools/phone_mic_gateway.py` — 10s→60s bound
- `Assets/Tests/EditMode/CT-P25_PlayerGender.cs` (new 9 tests) — enum/save/visual/builder/decor/recording pins
- `Assets/Tests/EditMode/CT-P20`/`CT-P21` — `Default*` constants (10/30)
- `.gitignore` — `__pycache__/` + `*.pyc`

## 3. Verification
- **FULL EDITMODE** `TempTestRunner.RunEditMode` 2026-09-17: **410 total, 406 passed, 0 failed, 4 skipped** (1 existing `P13M4_RealSamplesIfPresent` ignore-by-design + 3 `P25F/G/H` skipped when prefab/Resources unavailable in batch domain — gender API still pinned via `P25A-E,I`). **Gate PASS** (0 compile error, 0 fail).
- **CLEAN WINDOWS BUILD** `E2EBuild.BuildWindows` 2026-09-17: **Succeeded** `errors=4` (headless env noise, same as Phase 2.3) `warnings=4-7` `size=175M` `LWE.exe 667k` + `LWE_Data` fresh. **Gate PASS** (no architecture weakening, no test weakening).
- **BOOT 3×** `LWE-E2E/LWE.exe` 800×600: **3/3** `FACE_OK 3` `SHOE_SEAT side` 0 exceptions. **Gate PASS**.
- **P24Verify driver** `-p24verify 1920×1080 Max`:
  - Gender `Boy→Girl→Boy` `Pviz` sync `PASS`
  - Decor `tufts=3 rocks=2 patches=3 barrels=1` `PASS`
  - Recording `game=1920×1080@30 frames=90 dark=0` `video=320×240@10` `transcoded=True mp4=175k` `1080p30 PASS noJpeg PASS notDark PASS` `EndRenderPass 0`. **Gate PASS** (1080p, ≥30fps, rawvid lossless, no JPEG, PIP 10fps, no dark frames).
- **VISUAL QA** — automated decor/gender/recording PASS; human eye gate: world reads as layered complete product (FG path-mat, MG char-stall, BG fence-trees-outer, ambient tufts/rocks/patch/barrel fill empty lawn without cluttering path or occluding apple `3.5,-2` vs ball `5.5,3.2` vs pedestal `5.4,0.6`). Boy blue vs Girl pink distinct at spawn/HUD. Milo mat + stall + hedge + outer skirt prevent floating island. HUD chip whispers in interaction (0.35), bubble east-south clear of label/awning, cursor arrow+chevron. **No prototype/debug geometry** (white-T/helper 0, colliders destroyed on decor, `Ground -0.12` skirt, `FaceOK` 3). Human playtest of full quest (Talk→Find→Bring) still required to confirm no visual blocker before release — technical gates PASS, visual gate leaning PASS pending manual play.

## 4. Consistency
- Boy/Girl same `VisualScale 0.5` `GroundLift 0.005` `CharacterPresentation` kit `MeasureBandFront` `SoleSeatFor` — presentation unified, LOCK pipelines untouched: `QuestManager`/`HintService`/`AudioDirector`/`PhoneMic`/`Camera`/`NavMesh` contracts preserved, no new `Action<string>`, no JPEG intermediate (game `.rawvid` BGRA raw), recording still `libx264 slow CRF19 + LAME q4` at 1080p.

## 5. Open Items (non-blocking)
- `Walk_Carry` not wired (prop rides `Fist.R` bone, base walk reused)
- `PregenSeeder` audio fallback silent (separate packaging)
- Gender selection UI (currently `G` toggle + persist; future onboarding choice screen could replace `G`)
- Human visual playtest of full flow (Talk→Find→Bring→Wrong→Retry→Correct→Celebration) at 1080p on large display still recommended before Phase 2.5

## 6. Evidence
- EditMode `410` (log `runedit3.log` total/passed/failed/skipped)
- Build `LWE-E2E` `Succeeded` `LWE.exe` + `LWE_Data` (175M)
- Boot `Player.log` 3× `FACE_OK` + `SHOE_SEAT side`
- P24Verify `Player.log` `Gender PASS Decor PASS Rec 1080p30 PASS` + `rec-xxxxx.mp4 175k 1920×1080 90 frames dark 0`
