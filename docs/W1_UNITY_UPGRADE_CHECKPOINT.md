# Checkpoint W1-Unity6000.6-Upgrade — Editor baseline 6000.6.0f1 (v6.5)

> Editor/package upgrade, KHÔNG architecture rewrite. 4-agent ownership, Constrained 3D, GameInstaller, typed IDs, EventBus, audio architecture, Worker contract, pre-gen pipeline, Learning/Quest: giữ nguyên. CI license-free giữ nguyên (không UNITY_LICENSE/EMAIL/PASSWORD/game-ci).

## Đã làm (không cần Unity)
* `ProjectSettings/ProjectVersion.txt` → `6000.6.0f1`.
* Mọi ref `6000.0.60f1` trong docs/workflows đã chuyển (W0_T0_LOG giữ 2 dòng lịch sử).
* `Packages/manifest.json` giữ nguyên scaffold — KHÔNG đoán version; UPM resolve ở bước dưới.

## User chạy local trên máy dev (Hub Supported+Recommended)
1. [ ] Mở project bằng Unity 6000.6.0f1, để UPM resolve dependencies.
2. [ ] Verify compatibility: URP, AI Navigation, Input System, Test Framework, Addressables (thêm nếu thiếu, version từ registry).
3. [ ] Cập nhật `manifest.json` + commit `packages-lock.json` theo thực tế (ghi version thật vào log này).
4. [ ] Compile → sửa compile errors tối thiểu (không refactor unrelated).
5. [ ] Run EditMode: 16 CT (kỳ vọng 4 xanh cũ + W0-T1 implement dần).
6. [ ] Ghi package/API nào đổi ở đây.

## Package/API thay đổi (điền sau khi mở)
* Compiler baseline VERIFIED 2026-09-11 (Unity 6000.6.0f1, project `little-world-english`):
  - LangVersion **9.0** cho mọi assembly (`-langversion:9.0` trong toàn bộ 54 `Library/Bee/artifacts/1900b0aE.dag/LWE.*.rsp`; target `netstandard2.1`).
  - KHÔNG có `Assets/**/csc.rsp` override; asmdef không có trường LangVersion; `ProjectSettings.asset` → `additionalCompilerArguments: {}` (trống). → Editor version ≠ language version; policy chuẩn là C# 9 only.
  - Hậu quả: `record struct`/`record class` (C# 10) fail CS8773 → đã chuyển 10 types (`Events.cs` x8, `Services.cs` x2) sang `readonly struct` + `IEquatable<T>` + `==/!=` + `GetHashCode` + `ToString` + `Deconstruct`, fields public readonly. Xem `contracts/Events.md`, `contracts/Services.md`, `ARCHITECTURE.md` §1/§4/§6.
* InputSystem: `Packages/manifest.json` pin `1.13.0` nhưng UPM/Library resolve hash khác per-machine (`Library/Bee` ref `7a4e1a2a8194`, log thấy `ea5ab5b33a26`) + CS0619 TreeView-obsolete noise từ package Editor code (pre-existing, ngoài Assets, không chặn sửa project code nhưng làm bẩn log). Chưa lock `packages-lock.json` — cần commit theo thực tế ở bước 3.
* Gate status 2026-09-11 (C#9 root fix): CS8773 = 0, cascade record = 0. Còn 13 lỗi project unique (tất cả `Assets/_SharedKernel/GameInstaller.cs`: CS0246 x12 + CS0103 x1 — composition root nằm trong assembly zero-ref `LWE.SharedKernel` nhưng `new` types của `LWE.Brain`/`LWE.Audio`; `LocalSave` chưa tồn tại; `CloudflareGoogleTtsProvider` sai tên, file thật là `CloudflareTranslateTtsProvider.cs`). W1 BLOCKED cho tới khi Lead fix wiring này.
* Gate status 2026-09-11 08:42+07 (Lead Bootstrap decision implemented):
  - `Assets/_Bootstrap/` (LWE.Bootstrap.asmdef refs SharedKernel+World+Brain+Content+Audio) + `GameInstaller.cs` MOVED từ `_SharedKernel` (giữ GUID `e0c18bcf…9075368`, không scene nào ref nên không fixup). SharedKernel còn 0 ref tới Brain/Audio.
  - `LocalSave` (option A): implement mới `Assets/_SharedKernel/LocalSave.cs` theo frozen contract (ARCHITECTURE §3 skeleton + §9 "LocalSave JSON" + Services.md ISaveService + Part E npcVoices/worldSeed). JSON tại `persistentDataPath/lwe_save.json` (tên file là W0 choice, chưa freeze trong contract); DTO thay Dictionary (JsonUtility); Load fail-soft → fresh progress.
  - Provider canonical: `CloudflareTranslateTtsProvider` (xóa alias `CloudflareGoogleTtsProvider` trong D_Audio; sửa GameInstaller, ARCHITECTURE, AGENTS, ADR-007, WorkerTtsContract, architecture-lint — lint cũng đổi exempt path sang `_Bootstrap` + thêm `LocalSave` vào `new`-list).
  - CS1069 `UnityEngine.AudioModule` (lộ ra sau khi SharedKernel pass): root cause là `Packages/manifest.json` thiếu engine modules (chỉ 4 deps, không `com.unity.modules.*` → Bee không ref AudioModule). Fix: thêm `com.unity.modules.audio@1.0.0` + `com.unity.modules.unitywebrequestaudio@1.0.0` (version copy từ project `My project` cùng Editor 6000.6.0f1). UPM đã resolve, `packages-lock.json` có cả 2 (không commit — repo chưa có git).
  - Latent test bug lộ ra: `CT-A03:69` `await Assert.DoesNotThrowAsync(...)` → CS4008 (NUnit fork chỉ có overload `:void`). Fix test-only: try/await + `Assert.IsNull`, giữ nguyên intent + dedupe assert.
  - PROJECT-SOURCE ERRORS = 0 (Editor.log fresh section; 7/7 assemblies build OK: SharedKernel/Brain/World/Content/Audio/Bootstrap 08:42, Tests.EditMode 08:47). Còn 276 CS0619 trong `PackageCache` InputSystem Editor (pre-existing noise, không chạm PackageCache).
  - Package bumps (batch `-runTests` bị abort bởi CS0619 package `[Obsolete(error:true)]`): `com.unity.inputsystem 1.13.0→1.20.0`, `com.unity.ai.navigation 2.0.5→2.0.14` (versions copy từ `My project` cùng Editor 6000.6.0f1, log 0 CS0619; project code không dùng API 2 package này → zero-risk; URP/test-framework giữ pin). Resolve OK, `packages-lock.json` có modules + bumps (không commit — repo chưa có git).
  - CLI note: `-quit` (+`-nographics`) khiến batch Unity quit trước khi TestRunner chạy (2 runs, 0 tests). Doc-pattern (không `-quit`) chạy được, exit code 2.
  - EditMode batch 2026-09-11: 23 tests — 21 PASS, 2 FAIL đều là RED placeholder có chủ đích (`CT-003` world-state A/B, `CT-008` content bundle C/D — body chỉ `Assert.Fail("RED W0-T1...")`, ngoài scope gate này). Mọi test chạm code đã sửa (bus/events/services/audio/selector/quest/hints/learning + CT-A03 đã fix CS4008) đều XANH.
  - UNITY LOCAL GATE (project-source): GREEN. W1 vẫn BLOCKED cho tới khi CT-003/CT-008 được implement (scope Agent A/B/C/D, không phải gate này).
* Gate status 2026-09-11 09:30+07 (W0-T1 RED placeholders implemented, Lead review):
  - CT-003 (A+B): production `B_Brain/QuestRewardService` (subscribe QuestCompletedEvent, ledger friendship mia + world-change set, apply-once) + `QuestData.rewardFriendshipMia/rewardWorldChange` + GameInstaller wire `Rewards`. Test chạy full quest flow → flower_pot + mia 10 + không double-apply. A bind visual ở W1 (không code A_World ở gate này).
  - CT-008 (C+D): Unity-side mirror của validate_content.py authoring rules qua ContentDatabase readers (counts 15/35/50, id==filename, cấm Google voice names, active tags/forms/audio metadata voice+lang freeze, quest schema/targets/hint freeze/simplify/one-at-a-time, dialogue pack 30-40 + voices + word caps + audio mapping). --ship file gates ở lại phía python/pregen (W1). Production readers bổ sung: `QuestEntry.oneObjectiveAtATime`, `DialoguePack` + `ParseDialoguePack`. Test asmdef thêm ref `LWE.Content` (đúng chiều, không cycle).
  - EditMode batch: 23/23 PASS, 0 compile errors. Git checkpoint W0-T1-UNITY-GREEN (Assets, Packages/manifest+lock, ProjectSettings, docs). W1 được phép bắt đầu từ checkpoint này.
* Warning cleanup 2026-09-11 ~09:45+07 (post-tag working tree, chưa commit):
  - UAC0009: `GameInstaller` `#if UNITY_EDITOR || DEVELOPMENT_BUILD` → `#if UNITY_EDITOR || DEBUG` (supported managed symbol; giữ intent tests-only, không suppress).
  - Input deprecation: audit toàn Assets chỉ còn đúng 1 legacy dep đã verify (`ClickToMove` click picking) → migrate 2 dòng sang `UnityEngine.InputSystem.Mouse` (+null-guard batch) + ref `Unity.InputSystem` trong LWE.World + `activeInputHandler: 1` (New-only). Unity từng tự set Both(2) khi cài inputsystem 1.20.0.
  - Batch verify: 0 errors, 0 project-code warnings, 23/23 PASS.
* W1 Stage-1 2026-09-11 10:40+07 (4 agents parallel + Lead integration, headless verified):
  - A: MarketScene (1-GO shell) + MarketBuilder (code-built market: stall/tree/fences/crate+apple/player/NavMesh runtime bake/camera/EventSystem) + ClickRouter (New-Input single raycast, arrival interactions, vocab audio via Director) + SmartCamera modes + MarketHUD (banner + replay) + Apple/FlowerPot presenters; Interactable OnMouseDown removed.
  - B: QuestManager w1_mia_apple mirror + AdvanceOnSeen Find-only fix (double-click exploit) + QuestRewardService w1 mirror + Milo lines (Greet/InstructFind/InstructBring/PraiseFound/Repeat/SetTarget) + MiloPresenter/MiaPresenter (procedural figures, proximity greet, idle Tick, carrying/Bring/Give-correction).
  - C: Content/quests/w1_mia_apple.json + milo.json +3 lines + Content/audio_manifest.json (8 entries, key-exact params); validator authoring PASS.
  - D: tools/pregen_w1.py fetched 8/8 real mp3s (94KB) to Assets/StreamingAssets/audio/ + PregenSeeder (C#-computed keys, never throws) + CT-A05 resolver test.
  - Lead: SharedKernel IClickTarget (replaced A reflection hacks with typed calls + HUD replay Action), _Bootstrap MarketBootstrap + BootstrapScene (Build Settings idx0), GameInstaller seeder + additive MarketScene load (event-driven, exactly-once) + seen/spoken production routing, ugui 2.6.0 package (verified blocker: HUD needs UnityEngine.UI), BuildSettings both scenes.
  - Headless playmode smoke 20/20 (boot, quest find→bring→complete, friendship 10, flower visible, HUD done, NavMesh valid+path complete; physical displacement needs real frames — manual acceptance).
  - Batch gate: 0 errors, 0 project warnings, EditMode 24/24 PASS (23 + CT-A05). Manual in-Unity acceptance (look/feel/audio/ear/offline) still required before W1 complete.
* W1 NPC visual+animation upgrade (quaternius CC0, Game-view verified headless):
  - Milo = Worker_Male, Mia = Worker_Female (Ultimate Animated Character Pack, CC0; Casual/Chef rejected: weaker market-crew fit; Universal Base+AnimLib unreachable headless via itch tokens).
  - Prefabs + Idle/Victory/PickUp Animator controllers (Editor-API built); URP/Lit already on import, no conversion needed.
  - Presenters split GameplayRoot (collider/identity/events, unchanged APIs) vs VisualRoot (model+Animator+face kit); procedural wave layer (LateUpdate), Victory/PickUp triggers, look-at kept; sin-bob/hop removed (Animator owns motion).
  - Readability adaptations (instance materials only): orange/coral vests, tan faces, warm-brown skin, geometric doll eyes+smile on Head bone (100x bone-scale counter-scaling fix for the giant-blob artifact).
  - Screenshots verified: real characters, faces, hat/hair, stall/crate/world coherent, no magenta/white; wave + celebration triggers fired headless (visual timing by ear/eye = manual).

## Gate
Chỉ khi: mở được + compile sạch + EditMode PASS + lock ổn định → mới sang Vertical Slice implementation.
