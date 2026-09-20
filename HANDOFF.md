# HANDOFF — PHASE 3.0 WORLD FOUNDATION (single source of truth từ đây)

Ngày bắt đầu: 2026-09-19. Project: `E:\LWW\learning-world` (Unity 6000.6.0f1, URP).

> QUY ƯỚC (lệnh user 2026-09-19): handover các phase trước đã xóa. Chỉ ghi từ
> Phase 3.0 trở đi. Xong bất kỳ step todo nào đều ghi vào file này ngay.
> File lock chi tiết các phase cũ vẫn nằm ở `docs/HANDOFF/` (không đụng).

## 0. Locked contracts mang sang (tóm tắt — chi tiết ở docs/HANDOFF + ARCHITECTURE.md)

- Composition root duy nhất: `GameInstaller` (DontDestroy, BootstrapScene) + additive
  `MarketScene` (1 GO `MarketBuilder`, world build bằng code). Chỉ Installer được `new` service.
- Player: capsule + NavMeshAgent (speed 2.2, baseOffset 0) + `ClickToMove` (Bind bus,
  `MoveTo` plain/typed, arrival publish `NavigationCompleted`). `ClickRouter` bounds
  X[-8,8] Z[-6,6], arrival XZ-only, arrivalRange 0.75m, apple/ball interactionDistance 1.3m.
- Camera `SmartCamera`: Follow (offset 0,3.2,4.6) / Interaction / Cinematic,
  `FocusOnFor`/`FramePointFor` tự về Follow. HUD chip yield khi beat.
- NavMesh bake runtime `CollectObjects.All` TRƯỚC khi player tồn tại + carve tĩnh
  (cây/stall/crate/pedestal/hedge). Decor sau bake: collider-free, không rebake.
- EventBus typed readonly struct (C# 9, cấm `record`), subscriber tự dispose.
  Quest complete CHỈ qua `QuestCompletedEvent`. Quest hiện tại: w1_mia_apple, w1_mia_ball.
- Save `LocalSave` JSON — Phase 3.0 KHÔNG đổi save format (world nav state in-memory).
- C# LangVersion 9.0. Không `ServiceLocator`/`FindObjectOfType<Service>`/`new` service
  ngoài Installer. Không TTS/Worker từ World/Brain/Content.
- Baseline cây: main branch, HEAD 1f0cbe9 + worktree bẩn ~29 files (P24/P25/Round2,
  chưa commit). EditMode gần nhất 443/439/0/4. Không commit khi chưa được lệnh.

## 1. PHASE 3.0 — WORLD FOUNDATION (đang làm)

Mục tiêu: Main World thành trung tâm Learning World + 4 Subject (Math, Thinking,
English, Vietnamese), mỗi môn có road → gate (landmark) → playground → return.
KHÔNG Question/Topic/Lesson/Curriculum (các phase 3.1+).

Quyết định kiến trúc (từ audit, EXTEND không REWRITE):

- Single-scene spatial (KHÔNG scene .unity mới, KHÔNG SceneManager thứ hai):
  mở rộng MarketScene hiện tại, 4 district đặt 4 hướng quanh Main World, nối bằng
  road qua khe hedge. Player ĐI BỘ liên tục (WALK→DISCOVER→GATE→PLAYGROUND).
- "Transition" = WorldNavState change + gate trigger + camera framing + HUD,
  KHÔNG load/unload scene ⇒ không duplicate player/camera/audio/UI/bus theo cấu trúc.
- `SubjectDefinition` (data thuần: Id/DisplayName/positions/palette/landmark —
  KHÔNG lesson/question/topic). `IWorldNavService` in-memory (CurrentSubject,
  Enter/Return idempotent, publish `WorldChangedEvent`). Gate câm (chỉ gọi nav).
  Bootstrap subscribe bus → HUD/camera. Không đụng quest/audio/save/camera contracts.

## 2. STEP LOG (ghi ngay khi xong mỗi step)

- [x] Step 1 — Audit repo + architecture (2026-09-19): đọc HANDOFF cũ, ARCHITECTURE.md,
      GameInstaller, MarketBootstrap, MarketBuilder (1342 dòng), ClickRouter/ClickToMove/
      SmartCamera/WorldNameLabel/MarketHUD/QuestGuideLine, Services/Events/Ids,
      2 scenes, 47 test files, asmdefs, Unity 6000.6.0f1. Không test nào pin router
      bounds hay gọi BuildServices/Bootstrap.Build trực tiếp ⇒ mở rộng bounds +
      thêm file mới an toàn. Reset HANDOFF này về Phase 3.0 theo lệnh user.
- [x] Step 2 — Implementation plan (2026-09-19): single-scene spatial (không scene
  mới, không SceneManager thứ hai); 4 district Đông/Tây/Bắc/Nam quanh Main World,
  road qua khe hedge, gate landmark riêng shape language, playground + return arch
  mỗi môn; transition = WorldNavState + trigger + camera/HUD (không load scene).
  Files mới: WorldFoundation/SubjectDefinition/WorldNavService/SubjectGate/
  SubjectWorldBuilder/CT-P31. Sửa extend-only: MarketBuilder, ClickRouter bounds,
  GameInstaller, MarketBootstrap. Không đụng quest/audio/save/camera, không save
  format change, không Question/Lesson/Topic.
- [x] Step 3 — WorldFoundation + NavService + SubjectDefinition (2026-09-19):
  `WorldFoundation.cs` (SubjectId/WorldChangedEvent/IWorldNavService, C#9),
  `SubjectDefinition.cs` (catalog 4 môn: Math Đông / Thinking Tây / English Bắc /
  Vietnamese Nam + gate/entry/return/palette/landmark, KHÔNG learning fields),
  `WorldNavService.cs` (in-memory, idempotent), `SubjectGate.cs` (poll XZ,
  entry one-way-in / return one-way-out, không Rigidbody/trigger).
- [x] Step 4 — SubjectWorldBuilder (2026-09-19): roads (warm tan đồng nhất),
  4 gate landmark khác shape language (Blocks/Gears/Books/Scrolls + label
  WorldNameLabel tái dùng), playground (medallion + core + return arch vàng
  "Main" + boundary ring + tree + decor collider-free deterministic),
  signposts, outer hedge. Shell pre-bake / carves runtime / decor post-bake.
- [x] Step 5 — Wire extend-only (2026-09-19): MarketBuilder (ground 38x32,
  bounds 16/14, hedge khe 4 đường, carve biên chia đoạn, clear-zone roads,
  +AddCarve/SetWorldNav), ClickRouter bounds 16/14, ClickToMove.WarpTo
  (NavMeshAgent.Warp), GameInstaller new WorldNavService, MarketBootstrap
  subscribe WorldChangedEvent (HUD cache/restore objective, camera framing
  entry + warp Follow return). Entry gate fire khi != target (đi tắt liên môn
  không kẹt state). Không đụng quest/audio/save/camera contracts.
- [x] Step 6 — CT-P31 + EditMode (2026-09-19): 19 tests mới (identity/geometry/
  roadmap-guard/idempotent/full-loop/stress/gate one-way). Suite **462 total /
  458 pass / 0 fail / 4 skip** (4 skip cũ giữ nguyên) — không regression.
  Fix giữa chừng: `Color` không có `sqrMagnitude` (so kênh tay).
- [x] Step 7a — Clean build (2026-09-19): **Succeeded errors=0 warnings=4**
  size=108MB, payload DLL tươi. Boot player thật: **3×FACE_OK 0 exception**.
- [x] Step 7b — Survey navigation vòng 1 (2026-09-19): enter gate + HUD PASS cả
  4 môn, nhưng walk-playground/return FAIL cả 4 (state kẹt trong môn).
  Root cause (bug thật do survey khui): U-carve playground đặt NGƯỢC hướng —
  side walls chặn ngang đường vào. Fix: sides song song road, back chặn ngang.
  Driver cũng siết: arrival chỉ tính bằng feet (state fire sớm khi approach).
  Bài học 1 (Phase 3): carve hình chữ U phải vẽ theo TRỤC road — review bằng
  số (size axis vs road axis), không đọc bằng mắt.
- [x] Step 7c — Telemetry khui root cause thứ hai (2026-09-19): agent đứng yên
  ở cổng, `pathStatus=PathPartial remain=0.14` trong khi dest onMesh. Xà ngang
  cổng (~1.7–1.95m, giữ collider) bị bake NavMesh (physics colliders +
  agentHeight 2m) coi là thiếu headroom → cắt đường thành đảo cô lập ở CẢ 4
  cổng. Fix: strip collider mọi beam ngang trên road (visual-only) + nới pillar
  ±1.4→±1.6 (hành lang bake ≥1.4m) + gọn gear wheel.
  Bài học 2 (Phase 3): BẤT KỲ beam nào ngang road đều phải strip collider —
  bake đo headroom theo agentHeight, không theo mắt người. Telemetry
  (pathStatus/remaining/velocity/samplePosition) rẻ hơn mọi tranh cãi.
- [x] Step 7d — Root cause THẬT (2026-09-19): bake dùng RENDER MESHES
  (default), không phải colliders — strip collider vô tác dụng, xà vẫn cắt
  headroom ở cả 4 cổng + 4 return arch. Fix đúng: `NavMeshModifier.
  ignoreFromBuild` cho 10 beams + nới pillar ±1.6 + gọn gear.   Bài học 3:
  verify giả định engine bằng telemetry + build, không bằng trí nhớ.
- [x] Step 7e — Visual QA vòng 1 (2026-09-19): spawn đẹp (player+Milo+path+stall,
  VN gate đã dời x=3.5 nên không chắn frame-one); Thinking play đẹp (gear +
  return arch vàng + label Main crisp); Math entry/play chui vào xà (beat
  on-axis + return arch nằm trên sightline follow). Fix: beat chéo 3/4, return
  Math/Thinking sang phía bắc, shot giữa beat. Mic offer tự hiện lại giữa run
  (behavior Phase 2.x cũ — ngoài scope, driver dismiss trước mỗi shot).
- [x] Step 7f — Lockdown + VERDICT (2026-09-19): label cổng dời khỏi trục road
  (camera từng nhìn xuyên label), cây cũ né sightline Math, lintel English slim,
  chaos leg PASS hết (re-enter/cross-switch Math→English/spam/bump), HUD cache
  fix (chỉ cache khi từ Main), quest waypoints 5/5 + đi main không nổ transition.
  Survey cuối: **74 PASS / 0 FAIL, COMPLETE fails=0**, events == 12,
  census 1/1/1/1/1/8. Temp xóa sạch (0 file, asmdef không đụng).
  FINAL clean build (production-only): **Succeeded errors=0 warnings=1**.
  Boot final: **3×FACE_OK 0 exception**. FINAL EditMode trên cây khóa:
  **462/458/0/4**. Không commit (chờ user).

## 3. PHASE 3.0 — VERDICT: PASS (code + build + runtime + visual + stress)

- Main World nguyên vẹn + 4 district (Math Đông / Thinking Tây / English Bắc /
  Vietnamese Nam x=3.5). Road warm-tan + signpost + khe hedge + medallion.
- 4 gate khác shape language (Blocks/Gears/Books/Scrolls) + label riêng +
  playground (core + return arch vàng "Main" + boundary + tree) + decor
  deterministic collider-free.
- Transition = WorldNavState + gate poll + camera beat + HUD (không load scene
  ⇒ không duplicate theo cấu trúc). Return = arch + WarpTo + restore HUD.
- Evidence: `C:/Users/PC/AppData/Local/Temp/opencode/p3-*.png` + `p31-*.xml` +
  `p31-build*.log` + `Player.log` (74/0, 12 events, census sạch).
- 3 bug thật do survey khui đều fix + verify (U-carve ngược, xà cắt headroom,
  HUD cache cross-switch). Bài học 1-3 ở Step 7b-7d.
- KHÔNG đụng: quest/audio/save/camera contracts, save format, recording,
  speech, phone camera. Không Question/Lesson/Topic (đúng roadmap).
- CHƯA tự verify được (cần user): nhìn 4 khu bằng mắt, click quest thật trong
  world mới, cursor live bằng chuột thật, mic>60s + phone/PIP (giữ từ Phase 2.5).

## 4. ROUND POLISH (theo 5 yêu cầu user 2026-09-19)

- Backup: build Phase 3.0 PASS copy tại
  `C:/Users/PC/AppData/Local/Temp/opencode/P3Build-backup-20260919/` +
  code = HEAD 1f0cbe9 + 73 worktree entries (chưa commit, giữ nguyên).
- Q5: cổng "Main" THỰC RA là return arch (đường về, arch vàng nhỏ) — sẽ rename
  thành "Về" cho hết nhầm với subject gate.
- Việc làm: (1) dọn cỏ/decor thưa + thoáng, (2) dời return khỏi trục che lấp +
  stagger label, (3) backup (xong), (4) redesign cổng đậm chất từng môn.

## 5. ROUND POLISH — VERDICT: PASS (2026-09-19)

1. Dọn cỏ: tufts 14→8, flower clusters 8→5 (ít blooms), path-edge 6→4/side,
   playground picks 6→4, road-edge thưa + thoáng (lateral 1.45). Spawn shot
   thoáng, readability giữ nguyên.
2. Hết che lấp: return Math (13.6,-0.9) / Thinking (-13.6,-0.9) / English
   (3.0,-9.2) dời khỏi trục entry + follow; label cổng +2.2 lateral, return
   label stagger 1.9. Gatebeat 4 môn tách bạch.
3. Backup giữ tại `P3Build-backup-20260919/` (+ code HEAD 1f0cbe9 + worktree).
4. Cổng đậm chất: Math (shape trio cube/sphere/cylinder + hàng hạt abacus),
   Thinking (bánh răng 0.9 + puzzle tab), English (open-book crown + bút chì),
   Vietnamese (nón 2 tầng + tassels + trống). Beams mới ignoreFromBuild,
   pillars giữ carve, bits roadside strip collider.
5. "Main" → return arch nay là label **"Về"** (không còn nhầm subject gate).
- Verify round: EditMode **462/458/0/4** + build Succeeded errors=0 + survey
  **COMPLETE fails=0** (full loop + chaos + waypoints) + visual 4 gatebeat/play
  + spawn. Lockdown: temp 0 file, asmdef sạch. FINAL clean build Succeeded
  errors=0 warnings=1, boot 3×FACE_OK 0 exc, FINAL EditMode 462/458/0/4.
  Không commit (chờ user).
