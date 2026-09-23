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

## 6. HUB-BEAUTY ROUND — TIẾP TỤC (2026-09-20, máy ASUS)

- Môi trường: gỡ Unity 5.5.0f3 (`C:/Program Files/Unity/Editor` + MonoDevelop kèm theo
  + `D:/unitydownloadassistant-5-5-0f3.exe`). Chỉ giữ Unity **6000.6.0f1**
  (`C:/Program Files/Unity/Hub/Editor/6000.6.0f1`, `Unity.exe -version` → 6000.6.0f1).
  Registry sạch (chỉ còn 6000.6.0f1 + Hub 3.21.1).
- Việc làm: gate name boards chuyển sang `WorldNameLabel.SetupLocked` (trước đó code
  chết — boards vẫn dùng floating `Setup` + anchor). VN banner + 3 boards gỗ đều
  `SetupLocked(name, boardPos, HubCenter)` (yaw khóa = BillboardRotation 1 lần, đã chứng
  minh song song mặt board). Xóa anchor GO thừa. `Setup` nay reset `_locked=false`
  (tái dùng label follow-mode không bị đóng băng). Test mới `P33F_SetupLockedPaintsBoard`
  (position + yaw + unlock) trong `CT-P33_HubSelection.cs` (không file test mới ⇒ không
  đụng TestManifest mapping).
- Verify máy ASUS: EditMode **472 total / 467 pass / 0 fail / 5 skip** (baseline 462/
  458/0/4 + P32×4 + P33×6; P33E skip trong domain EditMode như thiết kế). Không regression.
  Lưu ý: `-quit` + `-runTests` cùng lúc khiến Unity thoát trước khi chạy test (bug đã biết)
  → chạy `-runTests` không kèm `-quit` (tự thoát code 0).
- Build P34 (`Temp/opencode/P34Build/LWE.exe`, hub-mode mặc định): UTP success:true,
  **103.4MB**, level0+level1 + Managed DLLs tươi. 1 warning benign (không xóa được
  BuildHistory cũ của máy khác: access denied).
- Boot smoke (build hub thật, windowed): **FACE_OK 1×, 0 exception**, services wired đủ
  (phone/local cam, recording, dep check). Game đang chạy (user tự nhìn hub bằng mắt —
  screenshot bị trình duyệt che nên chưa chụp được).
- Chưa làm (cần user): nhìn hub + 4 cổng bằng mắt trong game đang chạy, đóng game khi xong.
  Không commit (chờ lệnh, như mọi khi).

## 7. FIX THEO FEEDBACK MẮT USER (2026-09-20, máy ASUS)

- User báo từ game đang chạy: (1) cọc biển che chữ — cọc phải ở phía sau;
  (2) game hiện prompt thiếu chứng chỉ LAN (dev-intent thì bỏ qua).
- Root cause (1): cột cọc biển là cylinder 2m, scale y=1.1 → cao 2.2m, đỉnh 1.65m
  đâm VÀO pill chữ (mép dưới 1.36m) cùng XZ — đầu cọc cắt chữ từ mọi hướng hub.
  Fix: scale y 1.1→0.6 (cọc 1.2m, đỉnh 1.15m < 1.36m): cọc nằm sau/dưới chữ mọi góc,
  pill vẫn đọc như gắn trên cọc. Không đổi vị trí XZ ⇒ bake/nav giữ nguyên.
- Fix (2): prompt DepSetup (FFmpeg/Python/LAN-cert) là dev tooling Phase 2.x —
  đúng là cố tình trong bản dev. Nhưng build hub là hướng ship cho trẻ con nên skip
  hẳn khi `HubSelectionOnly` (1 dòng guard trong MarketBootstrap, cùng pattern Milo/
  Mia; `DependencySetup` không ai đọc ngoài chỗ gán ⇒ null-safe).
- Verify: EditMode **472/467/0/5** (P22 DepSetup xanh hết) → rebuild P34 UTP
  success:true (LWE.World.dll + LWE.Bootstrap.dll + level0/1 tươi 15:38).
  Boot build mới: **FACE_OK, 0 exception, 0 dòng DepSetup/prompt/LAN** (prompt hết).
- Log note (user yêu cầu log mọi việc): Unity batchmode chạy detached + flush log
  out-of-order (đuôi log kẹt ở dòng startup trong khi build đã success) + process
  nán lại sau build (thiếu -quit). Từ nay: check hoàn thành bằng marker UTP
  `success:true` + timestamp DLL/level, không tin đuôi log; kill Unity sau build.
- Game mới đang chạy — user nhìn lại cọc biển + xác nhận hết prompt rồi báo sang phần tiếp.
- (Update sau boot): user xác nhận hết prompt LAN. Cọc biển hết che chữ (fix rút cọc).
  Không commit (chờ lệnh).

## 8. BIỂN NHẦM CỬA — GẮN BIỂN SÁT CỘT (2026-09-20, máy ASUS)

- User báo + ảnh: biển "Tiếng Anh" nổi trên cửa Tư duy — cắm nhầm biển vào sai cửa.
- Xác minh tọa độ: KHÔNG nhầm dữ liệu — mỗi biển đúng tên cửa mình và đứng gần nhất
  cửa mình (vd biển Anh (-4.02,-1.35) cách cửa Anh 3.69m, cách cửa Tư duy 7m).
  Vấn đề là cảm nhận: biển đứng detached 3.7m ngoài sân, từ góc plaza nhìn thẳng
  hàng camera-biển-cửa-bên (vd đứng plaza-đông: biển Anh đè đúng lên cửa Tư duy).
- Fix: biển ôm sát cột cửa — sp = gate + face*1.1 + lat*2.3 (cách tâm cửa ~2.55m:
  ngoài pillar carve 2.0 + đĩa cửa 1.3, trong plaza veto 2.6, tránh road/walkway/
  entry đã đối chiếu 4 cửa). Biển đọc như đồ của cửa từ mọi góc hub.
- Verify: EditMode 472/467/0/5 exit 0 → build UTP success:true (World.dll + level0/1
  tươi 15:58, các DLL khác giữ nguyên đúng). Boot: FACE_OK, 0 exception, 0 DepSetup.
- Quy trình build (user lệnh: tối đa 180s, phải timeout): build incremental chỉ ~60-90s tới
  UTP success; process Unity nán lại sau build (thiếu -quit) + log flush lộn xộn
  từng gây hiểu lầm "treo". Từ nay watch bằng artifact (UTP success + DLL tươi),
  deadline 180s, kill Unity sau build để nhả lock.
- Game mới đang chạy — user nhìn lại: mỗi biển phải dính sát cửa của nó.
- (Update: user CHƯA đồng ý — ra quy ước mới, xem §9.)
  Không commit (chờ lệnh).

## 9. QUY ƯỚC BIỂN: TRÁI ĐƯỜNG VÀO, NGAY LỐI VÀO (2026-09-20, máy ASUS)

- User chốt quy ước: mỗi biển ở bên TRÁI đường vào cổng, ngay lối vào cổng.
- Code: xóa flip `lat.x * gate.x < 0` trong BuildSignpost (flip này từng đẩy biển
  Math/VN sang bên PHẢI). lat thô = (-face.z, 0, face.x) đã chứng minh luôn là
  bên trái hướng đi vào (left = up x fwd, fwd = -face) — giữ nguyên cho cả 4 cửa.
  sp = gate + face*0.9 + lat*2.2 (sát miệng cửa, ngoài pillar carve, đã đối chiếu
  road/walkway/entry 4 cửa). Mũi tên vẫn chỉ vào cửa (toGate tính lại).
- Verify: EditMode 472/467/0/5 exit 0 → build UTP success:true trong deadline
  (World.dll + level0/1 tươi 16:17). Boot: FACE_OK, 0 exception, 0 DepSetup.
- Quy trình (user nhắc — mọi bước build/test có timeout thật): watch artifact +
  deadline 180s, hết giờ tự kill Unity + kết luận (không poll vô hạn). Đã áp dụng
  từ bước này (BUILD-WATCH=SUCCESS).
- Game mới đang chạy — user kiểm tra quy ước trái-đường-vào.
- (Update: user vẫn chưa đồng ý + ảnh mới: cây to giữa sân che cổng → chặt, xem §10.)
  Không commit (chờ lệnh).

## 10. CHẶT CÂY GIỮA SÂN CHE CỔNG (2026-09-20, máy ASUS)

- User + ảnh: cây to giữa sân che cổng, cãi trái/phải vô nghĩa — chặt đi.
- Xác định: cây phía tây (-6.2, 3.8) scale 1.0 ngay lối từ spawn vào sân (gần camera,
  tán che cổng; không test nào giữ, không code nào tìm theo tên).
- Chặt: xóa BuildTree(-6.2, 3.8) + xóa TreeCarve cùng tọa độ (carve không cây =
  tường vô hình — chính comment cũ cũng dặn). Hoa/đá/gốc thấp quanh đó giữ nguyên.
- Verify: EditMode 472/467/0/5 exit 0 → build UTP success:true trong deadline
  (World.dll + level0/1 tươi 16:35). Boot: FACE_OK, 0 exception, 0 DepSetup.
- Game mới đang chạy — user nhìn: hết cây, cổng thoáng.
- (Update: user báo tiếp — cổng Tư duy bị 2 cây che cột + spawn camera thấp + HUD
  "Choose a gate!" đè biển, xem §11. Rồi lệnh push hết lên GitHub về máy nhà.)
  Không commit (chờ lệnh).

## 11. CỘT TƯ DUY + CAMERA CAO + HUD ĐÈ BIỂN (2026-09-20, máy ASUS)

- User + ảnh: (1) 2 cây che cột cổng Tư duy; (2) spawn camera để cao hơn nhìn trọn
  4 cổng; (3) pill "Choose a gate!" đè biển tên 1 cổng.
- Fix (1): dời cây tây (-14,-9,1.6x) ra góc SW sâu (-15,-11.5) + cây district Tư duy
  (-14.5,4.5) vào sâu (-15,6). Carve district đi theo tp (cùng code). Không test nào
  giữ tọa độ cây (đã grep). Hedge/bụi quanh cổng đã sạch sẵn (plaza veto 2.6m).
- Fix (2): camera hub cao hơn — MarketBuilder.HubFollowOffset (0,5,7) + helper
  FollowOffset() (hub?Hub:default). Đấu nối 3 chỗ: pose đầu + Follow đầu (MarketBuilder)
  + Follow return-to-Main (MarketBootstrap). defaultOffset KHÔNG đụng (CT-P32 đóng
  băng) + test mới P33G (hub cao/rộng hơn, full giữ nguyên).
- Fix (3): HUD objective ở hub chuyển từ chip trên-trái xuống bottom-center + chữ
  giữa (chỉ sân cỏ + bàn chân chiếu tới đó, không bao giờ có biển chữ). Full-world
  giữ nguyên. (Bắt được NRE suýt xảy ra: _objectiveText gán alignment trước khi
  tạo — đã tách ra sau.)
- Verify: EditMode 472/468/0/5 exit 0 → build UTP success:true (World.dll +
  level0/1 tươi 16:48). Boot: FACE_OK, 0 exception, 0 DepSetup.
- BÀI HỌC LỚN (user mắng đúng): build xong từ 16:53 nhưng watch treo tới 17:40 vì
  chờ dòng UTP success trong log (Unity flush log chậm tới ~47 phút!). Từ nay watch
  build CHỈ bằng timestamp DLL/level (filesystem truth, tức thì), coi UTP success
  là phụ. Build incremental thật chỉ ~60s.
- PUSH GitHub (user lệnh về máy nhà làm tiếp): commit + push origin/main.
  Library/ + Temp/ ignored — máy nhà mở project sẽ import lại từ đầu (lâu lần đầu).
  (Đã push xong 3ba1494 — user về máy nhà được.)
  Không commit (chờ lệnh — đã push, cây làm việc sạch).

## 12. XÓA CÂY TƯ DUY + FIX BẤM CỔNG KHÔNG VÀO (2026-09-20, máy ASUS)

- User + ảnh: (1) xóa HẾT cây khu vực cổng Tư duy; (2) bấm vào cổng không vào được.
- Fix (1): xóa cây backdrop (-15,-11.5) + early-return bỏ cây district Tư duy
  (kèm carve của nó — không tường vô hình). Medallion + core + return arch giữ.
- Root cause (2): bấm vào cột/vòm (có collider) → MoveTo trúng điểm trong vật cản
  → agent kẹt ngoài poll radius 1.2m → cổng không kích (survey cũ chỉ đi bằng
  waypoint tính sẵn, chưa từng bấm thật — đúng mục "cần user" còn nợ).
  Fix: ClickRouter.TrySnapToGateMouth — click scenery trong 2m tâm cổng (cổng vào
  + return arch) thì retarget về tâm hành lang XZ (đi bộ được) để đi xuyên trigger.
  Bán kính 2m KHÔNG chạm biển (2.38m): ngắm biển không bị ép vào. Không đụng
  Interactable/IClickTarget quest. Test mới P33H (cột/return snap, spawn + biển
  không snap).
- Verify: EditMode 472/469/0/5 exit 0 (P33H xanh) → build xong 22:24:43 (World.dll
  + Bootstrap.dll + level0/1 tươi). Boot: FACE_OK, 0 exception.
- TỰ KIỂM ĐIỂM (user mắng đúng, lần 2): build xong 22:24:43 nhưng tới 22:43 tôi
  mới kết luận — vì watch đòi CẢ dòng UTP success (flush chậm) CẢ DLL tươi, và
  16 phút đầu không chạy watch nào. Từ nay: (1) launch + watch LÀ MỘT lệnh duy
  nhất, không khoe "BUILD-LAUNCHED" riêng; (2) watch CHỈ nhìn timestamp DLL/level,
  deadline 180s, UTP chỉ để đọc thêm — đã viết ở §11 mà không làm theo.
- Game mới đang chạy — user bấm thử vào cổng Tư duy + nhìn quanh còn cây nào không.
- (Update: user bảo CHƯA xóa cây nào — rà số lại, xem §13.)
  Chưa push (chờ user gom lệnh).

## 13. BLOB 5.2M + QUY TRÌNH ATOMIC (2026-09-20, máy ASUS)

- User: chưa hề xóa cây nào ở cổng Tư duy. Đúng — tôi đã xóa nhầm 2 cây ở xa
  (SW corner + district), còn thủ phạm thật chưa đụng.
- Rà tọa độ bằng số: blob trang trí (-9.5,-8.5) scale 2.6 = khối tròn RỘNG 5.2m
  CAO 1.8m, cách cổng chỉ 4.6m — từ camera thấp nó đè cả 2 cột (đúng ảnh user).
  (Từng whack-a-mole: blob này trước nằm TRÊN cổng VN, bị dời sang đây.)
- Fix: chặt blob (-9.5,-8.5) + bụi tây (-6.8,-4.8) cách cổng 3.8m. Blob không
  collider/carve (thuần visual) nên bake chỉ nhẹ đi. Bụi đông/nam giữ.
- Verify: EditMode 472/469/0/5 exit 0 → build SUCCESS một nhịp 37s (level0/1 tươi
  22:49:23). Boot: FACE_OK, 0 exception.
- QUY TRÌNH MỚI (user mắng "suốt ngày treo" — đúng): launch + watch + kill GỘP
  CHUNG MỘT lệnh, deadline gắn trong lệnh, không khoe trạng thái giữa chừng.
  Watch build CHỈ bằng timestamp DLL/level. Đã áp dụng từ nhịp này (test + build
  đều một nhịp, không gap).
- Game mới đang chạy — user nhìn cổng Tư duy: nếu còn "thân cây" nào thì chụp
  lại giúp (quanh cổng giờ không còn cây code nào — muốn định danh chính xác).
- (Update: user báo bấm cổng thì đi vòng ra sau → snap ra miệng cổng; rồi mắng
  treo 2 lần vì tôi tách build/relaunch — xem §14.)
  Chưa push (chờ user gom lệnh).

## 14. SNAP RA MIỆNG CỔNG + ATOMIC THẬT SỰ (2026-09-20, máy ASUS)

- User: bấm cổng Tư duy thì đi vòng ra sau, không đi thẳng theo đường gạch.
  Giải thích: NavMesh đi đường chim bay (cỏ đi được hết), không bám gạch; snap cũ
  nhắm tâm cổng khiến đường cắt qua cột → pathfinder vòng tránh.
- Fix: TrySnapToGateMouth retarget về MIỆNG cổng = tâm + 0.6m về hub (đầu đường
  gạch — đi "lên gạch" + arrival 0.6m nằm trong trigger 1.2m). Return arch giữ
  tâm. P33H cập nhật (mouth cách tâm 0.6 + phía hub).
- Verify: EditMode 472/469/0/5 (P33H xanh) → build SUCCESS một nhịp 30s (22:56:15,
  World.dll tươi). Boot: FACE_OK, 0 exception.
- QUY TRÌNH (user mắng "lại treo" 2 lần — đúng cả 2): BUILD-WATCH=SUCCESS 22:56:15
  nhưng tôi vẫn tách relaunch riêng → user nhìn gap treo. Từ nay pipeline
  build-watch → kill → relaunch → boot-verify GỘP MỘT lệnh duy nhất. Nhịp này đã
  làm đúng vậy (kill 13876 + launch + 45s verify một lệnh).
- Game mới đang chạy — user bấm cổng Tư duy: phải đi lên đường gạch vào thẳng cửa.
- (Update: user báo bấm GIỮA cổng vẫn chui ra sau + "2 cây trụ cổng như 2 cây xanh"
  → hóa ra cột gear xanh đọc như thân cây + click xuyên cổng — xem §15.)
  Chưa push (chờ user gom lệnh).

## 15. CỘT GEAR HẾT GIỐNG CÂY + SNAP CLICK XUYÊN (2026-09-20, máy ASUS)

- User + ảnh: (1) bấm GIỮA cổng vẫn chui ra sau — "có lộn chiều vào không, đổi
  chiều thử"; (2) 2 trụ cổng Tư duy nhìn như 2 cây xanh.
- Xác minh (2): đúng là cột cổng — trụ gear là cột xanh đặc cao 3.2m + răng mỏng,
  nhìn từ sân ra y thân cây. Không phải cây (quanh cổng đã hết cây code).
  Fix: bánh xe đổi sang đá sáng (hết đọc lá cây) + răng xanh to hơn giữ identity +
  đế đá + hub cream giữ; cùng tọa độ/carve/collider (chi tiết mới visual-only) —
  bake/nav không đổi.
- Root cause (1): KHÔNG lộn chiều — bấm giữa cổng = tia xuyên qua vòm, trúng đất
  khu sau cổng 3-5m (ngoài bán kính snap 2m) → điểm đến rơi sau cổng. Fix:
  TrySnapGateOnRay — tia click xuyên trong 1.2m tâm cổng VÀ cổng nằm tại/trước
  điểm trúng (không bắt click đất xa mà cổng nằm sau) thì về miệng cổng.
  P33I chốt cả 2 chiều (xuyên → snap, bãi cỏ → không cướp).
- Verify: EditMode 472/470/0/5 (P33I fail 1 lần do test ngắm lệch 1.55m — sửa tia
  ngắm thẳng tâm, xanh; code đúng từ đầu). Build SUCCESS một nhịp 30s (World.dll
  + level0/1 tươi 23:33). Boot: FACE_OK, 0 exception. (Một nhịp check tưởng crash
  vì Get-Process hụt race lúc khởi động — check lại process vẫn chạy khỏe.)
- Game mới đang chạy — user bấm giữa cổng Tư duy (dừng ở cửa, không chui sau nữa)
  + nhìn cột đã ra chất cổng đá chưa.
- (Update: user paste spec PHASE 3.0.x Subject Playground — audit + thiết kế, xem §18.)
  Chưa push (chờ user gom lệnh).

## 18. PHASE 3.0.x KICKOFF — MATH PILOT, SCENE RIÊNG (2026-09-21, máy ASUS)

- User lệnh: scene riêng từng môn, Core dùng chung (không duplicate/rewrite),
  loading/unloading qua MỘT World Transition/Loader contract chung.
- REPORT §31 (trade-off trước khi khóa implement):
  single-scene = kế thừa survey PASS + không trùng Core, nhưng scene phình,
  không unload thật, NavMesh chung; multi-scene = đúng chữ spec + unload thật,
  nhưng phải giải quyết player/camera đi lại, 2 NavMesh chồng, bake/scene ops.
  User chốt multi-scene → firewall: (1) additive cùng tồn tại (Main ở lại, tắt
  hiển thị khi vào môn — chuyển nhanh, không reload, không mất state);
  (2) player/camera thành persistent (DDOL khi vào môn — đúng §14 CORE list);
  (3) MathScene offset +60x + camera far 100→300 (2 NavMesh không chạm nhau,
  không rebake bao giờ); (4) ISceneOps + WorldTransition machine (Lead contract,
  fake-test được); (5) SubjectCatalog.SceneName (null = đi bộ cũ — 3 môn còn lại
  giữ nguyên cho tới phase của chúng); (6) no-save-format-change (3.1 sở hữu).
- AUDIT (Steps 1-4): GameInstaller sở hữu hết services (bus/save/speech/policy/
  learning/hints/quests/rewards/worldnav/tts/audio/voices/mics), BootstrapScene
  DontDestroy + MarketScene additive, quest data-driven 7 quests + 51 vocab,
  NPC = presenter+model+Bind, suite baseline 472.
- S1 DONE (contract): _SharedKernel/WorldTransition.cs (Idle/Loading/InSubject/
  Unloading + Enter/Return idempotent + spam-safe + fail-truthful) +
  CT-P34 (6 tests fake ops). Suite 476/0/5 exit 0. Chưa đấu nối game (0 đổi
  hành vi) — công tắc ở S2.
- MATH LAYOUT (S3/S4): lobby tại origin MathScene (sân medallion + abacus +
  host NPC), Area A Counting Garden (tây), Area B Number Bridge (đông/bắc),
  entry IDs ổn định (math.lobby, math.counting_garden.entry...), return arch nam.
  NPC/quest theo pattern Milo/Mia + QuestManager, text C_Content sở hữu JSON.
  Không question engine (placeholder + seam cho 3.1).
- Tiếp: S1b adapter UnitySceneOps + MathScene shell + BuildSettings; S2 công tắc
  + player/camera travel; S3 lobby/NPC/quest; S4 areas/entry/return; S5 stress +
  recording + visual + PASS verdict.
- (Update: S1b PASS xong. S2 implement xong — travel wiring live, xem §20.
  Journey thật + verdict S2 làm riêng, CHƯA PASS.)

## 20. S2 IMPLEMENTED — TRAVEL WIRING LIVE, CHƯA JOURNEY (2026-09-21, máy ASUS)

- SubjectDefinition.SceneName (Math="MathScene", 3 môn null = đi bộ cũ giữ nguyên).
- ClickRouter.boundCenter (default 0 = legacy; Bootstrap recentre (60,0,0) khi vào).
- MarketBuilder.BuildPersistentCore (Player/Camera/Router/HUD/Cursor/Marker/
  EventSystem → scene-root PersistentCore; MarketScene không unload nên không cần
  DDOL) + camera far 100→300 (Math +60x nằm trong far, precision mm dư).
- MathWorldBuilder (đất/biên/arch vàng/entry-lobby-zone pads/mặt trời/nav bake/
  return gate bind Main) + MathLearningEntries (math.lobby/counting_garden/
  number_bridge) + root offset (60,0,0) do GameInstaller đặt lúc scene load.
- GameInstaller: sceneLoaded MathScene → offset + build + bake + bind return,
  expose MathWorldRoot/MathEntryPoint (null = fail, đường cleanup dùng).
- MarketBootstrap: Build nhận (loader, ops) + BuildPersistentCore; gate Math →
  TravelToSubjectAsync (lock input, HUD Entering, loader, null-check, deactivate
  Main root + Milo/Mia/labels/guide, bounds Math, WarpTo entry, Follow default,
  HUD Math World, unlock; fail → unload + ở Main + HUD lỗi); return arch →
  ReturnFromSubjectCoreAsync (warp cached pos/spawn, bounds 0, unload,
  reactivate, Follow hub, HUD restore, unlock). Milo/Mia/labels/guide null-guard
  (hub không có). Spatial 3 cổng + return cũ giữ nguyên từng dòng.
- Verify: EditMode 480/0/5 (P35A catalog, P35B bounds default; giữa chừng fail
  compile 2 dòng miloGo/miaGo sót sau đổi field — sửa, không nới gì). Build
  SUCCESS một nhịp 30s. Boot smoke: FACE_OK, 0 exception — game đang chạy.
- KHÔNG CLAIM PASS: journey thật Spawn→cổng→Math→lobby→areas→return→Main +
  adversarial + screenshots + stress + recording làm NHỊP RIÊNG.
- Game đang chạy build S2. Chưa push (chờ user gom lệnh).

## 19. S1b DONE — ADAPTER + MATH SHELL (2026-09-21, máy ASUS)

- Files tạo: Assets/A_World/MathWorld/MathScene.unity (+ .meta guid tự cấp,
  Unity chấp nhận) — MathWorld + 7 roots (Environment/Lobby/GameplayAreas/Npc/
  LearningEntry/ReturnPoint(0,0,5)/EntryPoint(0,0,-5)), NavMeshSettings đồng
  agent Main (r0.5/h2); Assets/_Bootstrap/UnitySceneOps.cs (ISceneOps thật qua
  SceneManager, null-op = exception trung thực).
- Files sửa: GameInstaller (sở hữu WorldTransitions + SceneOps, chưa ai gọi —
  0 đổi hành vi); EditorBuildSettings (+ MathScene); CT-P34 (+P34G/H).
- Ownership (§11): P34H đọc YAML thật — shell 0 script Core; machine + ops single
  instance trong GameInstaller. Failure (§12): machine-level đã cover P34;
  adapter null-op (scene thiếu khỏi Build Settings) → exception → machine false.
- Verify: EditMode 478/0/5 (P34G/H xanh; lỗi giữa chừng: thiếu using UnityEngine
  cho AsyncOperation — sửa 1 dòng). Build SUCCESS một nhịp 45s — log: MathScene
  import sạch + build vào player (1.4kb cạnh Bootstrap/Market).
- Tiếp: S2 (SubjectCatalog.SceneName + công tắc gate Math + player/camera travel
  + Main deactivate). 3 gate còn lại giữ đi bộ cũ.

## 16. TƯỜNG CARVE VÔ HÌNH CHẮN ĐƯỜNG GẠCH (2026-09-20, máy ASUS)

- User: có vật vô hình KHÁ LỚN chắn giữa lối từ sân vào cổng → phải đi vòng.
- Root cause: EdgeCarveW_N/E_N — 2 tường carve dài 6.7m chống hàng rào cũ x=±8,
  nhưng đường gạch + đường môn đi xuyên x=±8 ở z≈-2.9/-4 (cổng đã dời ra
  ngoài bounds cũ từ Phase 3.0 mà carve không mở cửa). Đường gạch Tư duy cắt
  tường đúng giữa (-8,-2.93). Chân đi vòng qua đầu tường ("ra đằng sau").
  (Survey cũ vẫn pass vì waypoint đi vòng đầu bắc mà vẫn tới nơi.)
- Fix: chẻ W_N/E_N làm đôi, mở cửa sổ z∈[-5,-1.9] ôm đường môn + đường gạch;
  bụi rào ở đó đã trống sẵn (veto/plaza). W_S/E_S không vướng, giữ nguyên.
  Carve runtime (không bake) nên hiệu lực ngay khi boot, không rủi ro bake.
- Verify: EditMode 472/470/0/5 exit 0 → build SUCCESS một nhịp 30s (level0/1 tươi
  23:42:55). Boot: FACE_OK, 0 exception.
- Game mới đang chạy — user bấm cổng Tư duy: giờ phải đi THẲNG theo gạch vào cửa
  (không vòng nữa). Bấm thử cả cổng Toán (đông cũng được mở).
- (Update: user báo ỔN → push + tag 3.0.0.1, xem §17.)
  Chưa push (chờ user gom lệnh).

## 17. PUSH + TAG 3.0.0.1 (2026-09-20/21, máy ASUS)

- User xác nhận ổn → push hết + đánh dấu bản 3.0.0.1.
- Nội dung từ sau push 3ba1494: snap miệng cổng + snap ray xuyên (P33H/I),
  chặt blob/bụi/cây Tư duy, cột gear đá, chẻ tường carve Đông/Tây, HANDOFF §12-16.
- Verify cuối: EditMode 472/470/0/5 exit 0, build SUCCESS, boot FACE_OK 0 exc.
- Tag: 3.0.0.1 (theo quy ước tag trần như hub-base). bundleVersion trong
  ProjectSettings giữ 1.0 (chưa đụng — user chưa lệnh).

## 21. S3-IMPLEMENTATION — MATH PLAYABLE SKELETON (2026-09-21, máy ASUS)

- Lệnh user: code S3 (lobby/host/quest/garden/bridge/return), CHƯA full journey.
  Change Impact: MEDIUM-HIGH (layout + bake MathScene; Main/transition Core
  không đụng) → Test Strategy: TARGETED (EditMode + build + boot) theo §16,
  journey 11 shots theo lệnh user DỜI sang nhịp duyệt riêng.
- Content: `Content/quests/math_counting.json` (find_one/bring_one → "one",
  hint freeze-4, simplify 2+demo, reward world_change math_bloom +
  friendship_mia 0); manifest +2 (math_01 "Find the one!", math_02 "Great!
  One!", tess/npc_female_01) 38→40 đúng cap; roster += Tess (display Tess,
  npc_female_01, alias math_host/tess, label 2.35).
- Brain (additive, không đổi logic): BuiltIn QuestManager += math_counting;
  BuiltInRewardContent += math_counting (0 + math_bloom, inert — friendship
  vẫn Mia-ledger-only tới Phase 4); mới `Tess.cs` (voice npc_female_01, literals
  do manifest cap frozen), `MathHostPresenter.cs` (pattern Milo/Mia: Bind,
  IClickTarget, OnFirstTalk/OnTalk, greet-once, hint tick, click/proximity
  bring 0.75m qua ReportAction, KHÔNG StoryMoment để khỏi frame anchor ẩn,
  visual primitive Blocks-theme + nod), `_Bootstrap/MathQuestDirector.cs`
  (Lead wiring: first-talk start/resume, WordSeen narrate, complete celebrate;
  advancement vẫn qua lambda global của Bootstrap — không double-advance).
- World (extend-only): MathWorldBuilder += lobby abacus (nam, corridor x=0
  thoáng), Counting Garden (beds + pedestal 1/2/3 + gold One Interactable),
  Number Bridge (stream + planks + rails 1.8m + blocks), 4 path warm-tan,
  HostAnchorLocal (2.2,0,0.8), CountingObjects expose; bake sau mọi geometry.
  Entry IDs giữ nguyên (Lobby/CountingGarden/NumberBridge).
- Installer: OnSubjectSceneLoaded += WireMathContent (bind counting bus,
  Tess.Bind, tạo Tess + label roster-driven + director dưới MathWorld root →
  unload cuốn theo; best-effort không strand). Bootstrap: cache Main objective
  khi travel scene + fail-branch restore (hub "Choose a gate!" hết mất).
- Test: CT-P36 ×9 (JSON/provider/loop/reward/manifest/roster/no-leak/
  catalog/pattern — KHÔNG di chuyển player); amend P09C + count 2→3 và P06F
  38→40 (phase-growth pins, old-content giữ nguyên).
- Verify: validator PASS (pack 40) → EditMode **494/489/0/5** (baseline
  485/480 + P36 ×9) → build SUCCESS ~35s (World/Brain/Bootstrap/Content DLL +
  level2 tươi 11:38) → boot **FACE_OK 0 exception**, spawn shot sạch (hub
  nguyên vẹn). Game đang chạy build S3 (PID 764).
- Nợ sang journey duyệt: Tess/carry-token visual, L2/L3 Tess voice riêng,
  math_bloom consumer, HUD "Well done!" hậu-quest. Không commit (chờ lệnh).

## 22. S3A — LOADER HONESTY + TRANSITION COVER (2026-09-21, máy ASUS)

- Lệnh user: mọi bước trừ journey; chuyển cảnh phải khác thấy rõ; GitHub
  phải được ÁP DỤNG (không chỉ đọc).
- GitHub đã inspect (README/source-flow/license/version/architecture):
  (1) UnityTechnologies/open-project-1 wiki-arch (Apache-2.0, archived 2021,
  Unity 2020.3): PersistentManagers + additive SceneLoader + event channels
  ≈ BootstrapScene/GameInstaller + typed bus SẴN CÓ → REFERENCE ONLY;
  (2) Addressables-Sample (1.5k stars): Bootstrap→Foundation additive
  load/unload khớp Travel/Return, nhưng Addressables package + pipeline quá
  nặng cho 4 scene local → REJECT dep, REFERENCE ONLY flow;
  (3) Persomatey/unity-scene-bridge (MIT, 0 star): singleton prefab + 5
  loading-screen class + canvases = hệ thống UI mới, URP không rõ →
  REJECT dep, ADAPT đúng 1 pattern: fade-in → load → enable → unload-old →
  fade-out (không fake progress);
  (4) EggyStudio SceneLoader (MPL-2.0, Unity 6000+): progress/events hay
  nhưng song song architecture + copyleft → REJECT dep, REFERENCE ONLY
  (progress bar còn flash với load <1s — chính SceneBridge cũng ghi nhận);
  (5) oculus AssetStreaming (Addressables + Mesh Baker + Oculus proprietary,
  open-world LOD): sai scale bài toán → REJECT.
- Kết luận architecture (§7): WorldTransition + UnitySceneOps ĐÃ là shared
  loader on-demand thật (không cần sửa): load duy nhất qua EnterAsync khi
  gate fire (P37D pin call-site), boot chỉ Bootstrap + Market (Player.log
  boot không một dòng MathScene; Tess/content chỉ xuất hiện sau entry —
  journey S3 đã chứng minh), warp chỉ sau IsLoaded + entry non-null (Main
  chỉ hide sau load verified). Thiếu đúng 2 thứ research chỉ ra: failure
  reason surfacing + transition visual → làm cả hai trong firewall.
- Code: WorldTransition.LastError (fail có lý do, no-op refusal im lặng);
  Bootstrap log dev truthful ("Entered … InSubject" / fail + LastError, warp
  fail, unload issue) + HUD states (Entering → World, fail restore);
  MarketHUD += TransitionCover (fullscreen black, last-sibling, raycast OFF,
  start disabled) + SetTransitionCover clamp; Bootstrap.FadeCoverAsync
  (0.35s/6 bước, resume main-thread cùng pattern WarpTo sẵn có) đấu vào
  travel (out → load → warp → in), return core (out → unload → in),
  fail branches lift + cleanup(false), catch + fail-safe không kẹt đen.
  Không progress bar giả, không system mới, không Math-loader riêng.
- Test: CT-P37 ×4 (LastError fail/unverified/silent-noop, single call-site +
  firewall no-LoadSceneAsync ngoài Core) + CT-P38 ×3 (cover structure/API/
  teardown-safe). Suite **501/496/0/5**. Build SUCCESS (World/Bootstrap
  tươi 13:40). Boot FACE_OK 0 exc + spawn sạch.
- MỞ (biết rõ, chờ S5/user): pill HUD + camera-box vắng mặt từ frame đầu
  trên MỌI build (pre-existing, không do S3/S3A — cover cùng canvas nên
  shots transition S5 sẽ diagnostic luôn); Tess eyes chưa đọc được từ xa;
  visual proof của fade + quest loop thuộc S5 journey (bị cấm ở S3A).
  Không commit (chờ lệnh).

## 23. S3A-BATCH — QUY ƯỚC MỚI + P39 NUMERICS (2026-09-21, máy ASUS)

- Lệnh user (standing rule): trừ bản kết thúc phase, MỌI verification chạy
  batch-số liệu, KHÔNG mở foreground. S3 còn dang dở: mã hoá bài học
  pillar-snap S3-journey thành pins headless.
- CT-P39 ×5 (production statics thật + catalog geometry, không player/scene/
  screenshot): mouth+stopping<fire cả 4 cổng; pillar outer-face 1.95m snap
  (regression miss S3); signpost 2.38m exempt cả 4; trigger fire đúng mouth/
  im ngoài 1.5m (instance thật + Bind dry-run); ray xuyên cổng snap.
  Giữa đường fail P39D (quên Bind → target mặc định Main no-op) → fix test,
  code đúng từ đầu. Suite **506/501/0/5**. Tests-only → KHÔNG rebuild
  (build SUCCESS 13:40 vẫn hiệu lực cho shipped code), không relaunch.
- Chẩn đoán hẹp vụ pill HUD (không foreground): CT-P03A (panel+text build
  đúng) PASS → construction nguyên vẹn; vắng mặt là render-time, cần mắt S5.
  Game đã kill, cây sạch. Không commit (chờ lệnh).

## 24. S3B — INTEGRATION CLOSURE (2026-09-21, máy ASUS, batch-rule)

- Standing rule mới (user): trừ bản kết thúc phase, verify batch-số liệu,
  KHÔNG foreground. S3B tuân thủ: EditMode + build + boot-LOG (không shot
  lái, không driving).
- §1 Pill/camera-box — ROOT CAUSE: TOOLING, game vô tội. Dev-truth log trên
  BUILD (`[MarketHUD] canvas=True panel=320x64 text font=LegacyRuntime
  alpha=1 cover=True` + `[Boot] screen=1920x1094 hud='Choose a gate!'` +
  `[PhoneCameraHud] showing=True`) chứng minh hierarchy active + font + text
  đầy đủ; client thật 1920×1094 trong khi capture tool chỉ lấy 1280×729 từ
  origin sai → pill (bottom-center) + camera-box (bottom-left (20,20)) nằm
  ngoài khung hình. Ảnh fullscreen cũ (j-fs1) từng hiện cả hai — khớp. Fix:
  tooling capture S5 (lấy full client), KHÔNG sửa game (construction đúng).
  Eyes regression: bằng chứng "mất UI" cũ vô hiệu → PASS.
- §2 Carry-token: `MathTokenCarry` (World→Carried→Consumed, bus-only;
  anchor PlayerHand travels; re-entry adopt live state) + wiring installer.
- §3 math_bloom consumer: `MathBloomDisplay` (3 blooms ẩn → hiện +
  `[MathBloom]` log) + wiring. Producer (ledger P36D) → consumer khép kín.
- §4 Post-quest HUD: Start/Active/Complete/Post texts qua CT-P40 trên
  components THẬT (P40A-D: first-talk/find/bring-complete/pre-talk-tap),
  subscription order mirror production. Director + HUD + global Great-job
  thứ tự deterministic ("Math World" cuối).
- §5 Functional review headless: BuildContent tách khỏi bake (extend-only) +
  CT-P41 ×5 (entries/typed target/hidden blooms/landmarks/layout-discipline).
  Giữa đường: Box 6-arg sót + Destroy edit-mode (×2 files) → fix theo pattern
  DestroyNow có sẵn. Suite **515/510/0/5**.
- §6 GitHub source-level: Eggy SceneGroupManager FULL source (phát hiện:
  OnSceneLoaded của họ fire lúc START load — machine mình honest hơn);
  SceneBridge flow; OP1 wiki-arch (line-source 404, ghi thật); Addressables
  + Oculus README; Unity allowSceneActivation docs (gating serialize mọi op
  — adapter mình không gate nên không deadlock). Phân loại giữ nguyên S3A:
  ADAPT duy nhất fade; còn lại REFERENCE ONLY/REJECT.
- Verify: EditMode 515/510/0/5 (cây cuối) → build SUCCESS → boot FACE_OK
  0 exc + facts-log. Không commit (chờ lệnh).

## 25. S4 — POLISH / HARDENING (2026-09-21, máy ASUS, batch-rule, P3.0.1)

- Impact: decor MEDIUM (meshes mới pre-bake, collider-free + P42 targeted);
  NPC/quest/bloom LOW (visual-only); camera/audio/UI/Core KHÔNG đụng.
  Không journey/11-shots/adversarial (S5).
- GitHub-first: celebration search 1 (PopcornFX $/Konfetti Android-only/
  Asset packs $/license → REJECT hết, giữ scale-pop nội bộ zero-dep);
  decor/NPC/beacon = REUSE patterns nội bộ (MarketBuilder/SubjectWorldBuilder
  tufts/blooms/pebbles, CursorPresenter DestroyNow/Lit-fallback, Mia
  proximity). Không dependency mới, không framework mới.
- Env: BuildDecor deterministic (pots/tufts/pebbles/garden-blooms/reeds/
  backdrop ×3/return-disc+flowers/chevrons) — TẤT CẢ collider-free, trong
  hedge, clear corridors. Return disc PathTan.
- NPC: Tess eyes lên domes (0.20/0.08 — inspection thấy 0.19 chìm trong đầu
  r0.22) + breathe dress 2% ~0.4Hz (procedural, không rig).
- Gameplay: MathBeacon (bob ±0.08 + spin 40°/s, motion-only, chết theo cube);
  bloom pop ×1.4 one-shot; chevrons dẫn lên cầu.
- Safety headless CT-P42 ×4 (decor-colliders/corridor-overlap/budget<220/
  beacon) + P41E skip backdrop. Suite **519/514/0/5**.
- Foundation: `Assets/Documentation/SUBJECT_WORLD_FOUNDATION.md` (skeleton +
  PROVEN/CANDIDATE/SPECIFIC/NOT-YET + matrix + variation + firewall).
- Perf: build 103.4MB giữ nguyên (S3A), level2 2.4KB (shell tí hon,
  content code-built). Không regression.
- Verify: EditMode 519/514/0/5 → build SUCCESS → boot FACE_OK 0 exc +
  HUD 'Choose a gate!'. Không commit (chờ lệnh).

## 26. PHASE 3.0.1.1 — SKELETON-COMPLETE (user feedback round, 2026-09-21, máy ASUS)

- User dừng journey, tự lái chuột, báo 5 việc (ảnh medallion vỡ hình đính kèm):
  (1) hầu hết cửa có vật cản khi đi chuột; (2) ô trung tâm Math vỡ ảnh
  (răng cưa — z-fighting); (3) cửa Về Math cũng bị chặn như bệnh Tư duy gate;
  (4) World Toán sơ sài, thiếu chất Toán; (5) phase này phải xong phần THÔ
  hoàn toàn (Tess hoàn chỉnh hình thể, không xếp khối; mỗi world = một
  gameplay). Lệnh: research GitHub tối đa cho world đẹp nhất, không tiếc token.
- GitHub (ADAPT mechanics, không copy code — 2D/scene-riêng, ta 3D/code-built):
  Dima34/Frog-Adventure (counting game: số + hành động) + ynsemre1/learn-math
  (Number Hunt: tìm số 1-10 → bóng bay + điểm; Fruit Basket: đếm + thêm;
  Star Race: so sánh ít/nhiều). Chắt ra: number-row 1..5 bằng pips +
  shape-trio Blocks + gold-rhythm discs. S3A/S4 verdicts (REJECT dep/ADAPT
  fade) giữ nguyên.
- Code (firewall giữ: manifest 40 frozen, không Question/Lesson/Topic,
  không đụng quest/audio/save/camera contracts):
  (a) Tess Golden body: `TessVisual.prefab` (clone MiaVisual + guid mới,
  chung Worker_Female rig + NPC controller) + `MathHostPresenter` rewrite
  visual (prefab + scale 0.5 + tints: vest Math-blue/hat gold/Face-Skin như
  Mia + CharacterPresentation face/shoes + wave + Happy pulse/PickUp/
  Celebrate; logic quest/click/proximity/hint Y NGUYÊN) + public
  `BuildBodyImmediate()` idempotent (MarketHUD precedent);
  (b) cửa Về Math: pillars +-0.9→+-1.25 (khe 2.2m), beam 2.8, fireRadius
  1.3→1.6 (trigger sớm, khỏi cần vào chính giữa);
  (c) medallion hết vỡ: pads top 0.015, paths top 0.047 (cách 32mm, không
  coplanar), return disc nổi trên path;
  (d) beat camera entry Math: warp xong frame Tess 2s (khỏi nhìn biên bắc
  trống) rồi Follow tự về — statics `EntryWorldPos/HostWorldPos` world-owned;
  (e) spatial returns có lối về: đĩa gold + nhãn "Về" (vẫn KHÔNG arch —
  giữ lệnh hub-declutter), trigger giữ nguyên;
  (f) chất Toán: entry board (posts + beam + beads), number row 1..5 pips,
  shape trio (cube/sphere/cylinder), bridge discs — tất cả deterministic,
  corridor-safe, collider-free trừ pads walkable;
  (g) SubjectWorldBuilder.StripCollider edit-safe (DestroyImmediate ngoài play).
- Test: CT-P43 ×6 (return gap/board corridor/pad-path heights/number-row/
  spatial Về/Tess Golden + idempotent) + amend P42A prefixes. Giữa đường:
  P43D clearance 0.797<0.8 (dời row), P43E Destroy-editmode (fix StripCollider),
  P43F lòi 2 sự thật batch: AddComponent KHÔNG gọi Awake (capsule proof) →
  BuildBodyImmediate; run -testFilter dính DLL cũ 1 lần → full suite là truth.
- Verify: EditMode **525/520/0/5** (P43 ×6 xanh) → build P35 SUCCESS
  (UTP success:true, DLL + level tươi 16:20; folder MỚI vì P34Build/LWE.exe
  đang bị game user lái lock). KHÔNG boot (user đang lái bản cũ — boot
  foreground sẽ giật chuột). Không commit (chờ lệnh).
- Nợ sang user/mắt (không foreground): swap sang P35Build khi rảnh → đi bộ
  chuột qua 4 cổng + cửa Về, nhìn Tess/lobby/garden/bridge, xong báo để sang
  phase polish. S5 journey bản cũ SUPERSEDED bởi round này (evidence S5-01..
  S5-05e + s4-*.png giữ trong Temp).

## 27. PHASE 3.0.2 — DISTRICT SCALE (verdict user: CHƯA PASS, 2026-09-21)

- User mở P35 cửa sổ nhỏ, lái mắt, verdict CHƯA PASS + ảnh bridge (kèm 5 lệnh):
  đất phải rộng hơn; vùng mint (xanh nước biển) thay đi cho đỡ skeleton;
  spawn đứng CHÍNH GIỮA world; world ≥ sân chính; decor đẹp hơn từ GitHub.
- GitHub decor THẬT (không chỉ đọc): Quaternius CC0 "LowPoly Nature Pack"
  (cùng tác giả rigs, OpenGameArt, 1.2MB) — tải về, import 13 FBX
  (Tree1-4/Bush1-3/Grass1-3/Rock1-3) vào `A_World/Resources/NatureKit/` +
  provenance README. `NatureKit.cs` loader: deterministic place, URP Lit
  convert (giữ màu flat), click-through (FBX không collider), strip node
  Camera/Lamp của Blender (P41E bắt 1 Camera lạc ở |x|=18.7), edit-safe.
- World 28m→38m (ground r19, hedge r18, ngang sân chính + districts):
  entry (0,-8)+board z-7, garden (-10.5,3), bridge (10.5,-3), return (0,8),
  paths dài 8.4/11m, shell EntryPoint→(0,0,0) = warp GIỮA lobby (Tess cách
  2.3m → greet chào ngay), ReturnPoint→(0,0,8). Router bounds 20/20 khi vào
  Math, restore 16/14 khi về (toán cũ kẹt click ngoài biên).
- Đất hết mint-slab: cỏ (0.36,0.62,0.34) + 2 meadow patches + suối NƯỚC xanh
  (0.30,0.56,0.86). Decor mới 19 placements Quaternius + number row tính từ
  trục (không magic numbers) + trio/discs theo districts.
- Test: P41E ≤18.5 + corridor (0,-8)→(0,0); P42B samples mới + exemptions;
  P42C budget <320 (>100); P43D segment mới; P43G nature (19 + no-collider +
  no-camera/lamp + clearance). Lỗi giữa chừng: FBX kèm Camera/Lamp (fix
  NatureKit strip + P43G pin). Batch lesson (mắc 2 lần): run `-runTests`
  phải launch detached + poll process (single-call return sớm, process chạy
  ngầm); stale UnityLockfile sau force-kill phải xóa; lingering Unity sau
  build/test phải kill tay (thiếu -quit).
- Verify: EditMode **526/521/0/5** (P43G xanh) → build P36 SUCCESS
  (UTP success:true, DLL tươi 17:08). Game cũ user đã tắt sạch (Player.log
  shutdown đẹp, không crash). Không boot (chờ user mở P36 tự check).
  Không commit (chờ lệnh).

## 28. P3.0.1.1 DEEP AUDIT + BATCH 1 COMPOSITION (2026-09-21, máy nhà)

- Lệnh user: chạy lại brief P3.0.1.1 (deep audit + redesign + hardening)
  TRÊN BẢN MỚI (sau khi user nhớ ra chưa pull — đã pull ff d8c4d06→15ad8bb
  + tag 3.0.0.1, 4 commit hub+P3.0.1+P3.0.2). Docs viết lại 100% theo cây
  thật (bản cũ viết trên P3.0.0 đã bỏ).
- STAGE A/B/C (docs, chưa commit):
  `docs/MATH_WORLD_DESIGN_AUDIT.md` (15 findings F1-F15: pad-as-zone, thiếu
  landmark, thiếu spatial grammar, garden không phải garden, bridge không
  phải bridge, host nook trống, reward off-screen, learning 1 từ, dead Math
  district trong MarketScene F9, Math-branch trong shared travel F10, vành
  hedge hở 4.9m F11, return MathScene thiếu nhãn "Về" F13, budget chưa đo
  F14, test chưa pin composition F15),
  `docs/MATH_WORLD_REDESIGN.md` (grammar v2 + layout cụ thể + batch plan +
  decision points),
  `docs/MATH_WORLD_REUSE_MATRIX.md` (promotion rules + hardening queue).
- STAGE D — BATCH 1 (world composition, gameplay không đụng):
  MathWorldBuilder: courtyard sand + rim + planters + 6 mouth markers; host
  nook (mat/planters/basket, backdrop bush dời sau Tess); Counting Frame
  5 rods×3 beads cao 2.0m; entry arch 5 beads; garden: fence 2 cửa + gate
  arch + 2 beds mới + crops đếm được 3/2/4/5 + counting tree 5 bead-fruit +
  5 stepping stones pips 1..5 + giant sunflower; brook: banks/reeds/lilies +
  stringers/posts + bead garland; clearing bờ bắc: 5 stones pips 1..5 + bead
  pile; loop path meadow 5 đoạn + garden inner path; vành rim thêm 8 bụi +
  4 backdrop tree; carves nhốt nước trừ corridor deck 1.8m; return "Về".
  Pin cập nhật có chủ đích: P43B 3→5 beads; P42C cap 320→400 (đo thật);
  thêm P43H/I/J (label, garden plot, hub/bridge composition). Lesson cũ giữ:
  beam ngang phải ignoreFromBuild (garden gate + bridge garland).
- Evidence (batch, không foreground): baseline 526/521/0/5 + census 176 →
  B1 suite **529/524/0/5** + census **373** (<400) → build release 3 scenes
  **Succeeded** 108,488,772 bytes → boot headless **FACE_OK 0 exception**.
  Temp tooling (TempMathCensus/TempBuild) đã xóa sạch (Assets/Editor/ gỡ).
  Artifacts: `Temp/opencode/p311b-*`; build review:
  `Temp/opencode/p311b-B1Build/LWE.exe`.
- STAGE E — CHỜ USER REVIEW (mắt): checklist trong
  `docs/MATH_WORLD_VISUAL_QA.md` §3 (arrival/hub/garden/bridge/loop/quest
  regression). Chưa làm Batch 2-5 (host staging + camera beats + arrival
  audio; counting activity + content sidecar 1-5; reward visibility;
  hardening F9/F10/F12; visual QA cuối).
- Không commit (chờ lệnh).

## 29. P3.0.1.1 B1R � USER ROUND: C�Y BAY / CLUTTER / SCALE / KENNEY ASSETS (2026-09-21)

- User m? build B1 (?nh): (1) c�y c?m tr�n kh�ng trung ngo�i m�p d?o; (2) qu�
  nhi?u kh?i g? ? trung t�m s�n; (3) s�n qu� b�; (4) t�m GitHub repo h?u �ch
  v� th�m v�o; (5) l�m d?p + g?n, du?c ph�p s?p x?p l?i/thay UI.
- GitHub-first (ADAPT/REUSE, kh�ng dep m?i): kenney mirrors (shorepine/kenney,
  ETdoFresh, dreamengine manifest) ch? c� GLB/thi?u food ? d�ng ZIP CH�NH TH?C
  kenney.nl: Food Kit + Nature Kit (CC0 1.0). Quaternius Crops qua Google Drive
  (gdown) b? Drive throttle/timeout ? b? (c� log, kh�ng treo ti?n tr�nh);
  partial d� x�a. 58 FBX copy v�o `Assets/A_World/Resources/PropKit/` +
  colormap.png; provenance `Assets/Documentation/ENVIRONMENT_ASSET_SOURCES.md`
  �L. Loader m?i `A_World/PropKit.cs`: harmonize m�u theo T�N material
  (leafsGreen/grass?green d? �n, wood/bark?brown, stone?grey; colormap gi?
  texture), strip collider, ignoreFromBuild cho bridge module, deterministic.
- MathWorldBuilder B1R (rewrite layout): d?o r19?r26 (52m) + skirt t?i; m?i
  object n?m trong r25 (h?t c�y/d� bay ngo�i void); rim = 20 hedge + 11 Kenney
  tree + backdrop blob TRONG d?o; hub d?n h?n (b? 4 ch?u, 6 mouth stone, 20
  number-row pad + pips; Counting Frame d?i (-5.2,-3.2)); Tess nook ch? mat +
  2 Kenney flower + basket; garden (-16,5) r6.5 = Kenney fence 1m + fence_gate
  + 4 beds crops th?t (3 carrot/2 pumpkin/4 corn/5 strawberry) + counting tree
  + 5 stepping stones (path_stoneCircle) + pips 1..5; bridge (15.5,-5) = 3
  Kenney bridge module (ignoreFromBuild) tr�n deck walkable + rail posts +
  banks/lilies + carves nu?c; clearing b? b?c (15.5,-8.5) 5 stones + bead pile;
  meadow loop 5 do?n + garden inner 4 do?n; entry z=-12, return z=+12 (nh�n
  V?); bounds 27x27. Quaternius Math trees (white-canopy) b? kh?i Math.
- Test pins c?p nh?t: P41D/E (r25.5, corridor z=-12), P42A prefixes m?i,
  P42B samples 15 di?m (spokes + loop + inner), P42C cap 400 (census th?t 311),
  P43D (number row ? garden stones), P43G (38 MathProp), P43I (Kenney fence/
  crops), P43J (bridge modules; b? mouth).
- Verify (batch, log d?y d?): EditMode **529/524/0/5**; census **311**
  transforms; build release **Succeeded** 109,358,564 bytes (22:49); boot
  headless **FACE_OK 0 exception**. Temp tooling (TempPropInspect/
  TempMathCensus/TempBuild) d� x�a s?ch. Build review:
  `Temp/opencode/p311b-B1RBuild/LWE.exe`.
- CH? USER REVIEW M?T (Stage E). Chua commit (ch? l?nh).

## 30. P3.0.1.1 B1R2 � ROOT CAUSE CLICK + FLOATING + MATH IDENTITY (2026-09-21)

- User round 2 (?nh): c�y v?n ? r�a/ngo�i d?o; click chu?t kh�ng di chuy?n;
  chua c� ch?t "s�n choi to�n".
- Ch?n do�n b?ng runtime self-test t? d?ng (temp, c� log, d� x�a): ph�t hi?n
  3 bug TH?T:
  (1) MathGround l� CYLINDER scale 26 = b�n k�nh 13m (primitive radius 0.5)
  + collider capsule scale m�o th�nh kh?i c?u kh?ng l? ? ray click tru?t
  ngo�i ~13m (player kh�ng di), NavMesh kh�ng t?i garden/bridge (r16-24),
  c�y/hedge d?ng ngo�i m�p c?. FIX: ground = Plane 5.2 (52x52m, MeshCollider
  ph?ng nhu Main); Pad strip collider (cylinder pad scale 11 = c?u b�n k�nh
  5.5); MathOuterField 300m strip + ignoreFromBuild. Self-test: far move t?i
  garden PathComplete; clickRay hit MathGround.
  (2) Dim c?a mic offer (Phase 2.1) nu?t M?I click world khi panel m?
  ([ClickSpy] overUi=True hits=Title|OfferPanel|Dim@50) ? tr? b? k?t kh�ng
  di du?c. FIX: MicSetupDialog + DependencySetupDialog Dim raycastTarget=false
  (panel + n�t v?n b?m du?c, world kh�ng c�n b? ch?n).
  (3) V�nh dai: hedge r24 + tree line r22 n?m TR�N c? th?t (kh�ng c�n float).
- Math identity B1R2: domino 1..6 hai b�n tr?c entry, Number Tower 5 kh?i
  3.5m, d?u + / - ngo�i s�n d�ng, pips d?m ? m?t tru?c t?ng bed, bridge rail
  d?i g?.
- Verify: EditMode **529/524/0/5**; build **Succeeded** (World.dll 23:32);
  boot headless **FACE_OK 0 exception**. Temp tooling (self-test/spy/build)
  x�a s?ch; asmdef Bootstrap revert (kh�ng th�m Unity.InputSystem).
  Build review: `Temp/opencode/p311b-B1R2Test/LWE.exe`.
- CH? USER REVIEW M?T. Chua commit (ch? l?nh).

## 31. P3.0.1.1 B1R3 � POLISH ROUND (tunnel/sky/red-pink/c?t/camera) (2026-09-21)

- User round 3 (5 vi?c): (1) transition hub?Math th�nh "du?ng h?m kh�ng gian
  d?m ch?t to�n"; (2) th�m d?/h?ng (dang qu� v�ng/xanh); (3a) ngo�i s�n th�nh
  b?u tr?i/m? t?m m?t; (3b) c?t ghi t�n world ? g�c s�n ? spawn focus c?t r?i
  v? nh�n v?t; (4) camera spawn cao hon.
- Code:
  (1) MarketHUD + math tunnel: canvas ri�ng order 80, 6 v�ng h?t
  (gold/blue/pink) ph�ng ra + 16 glyph (1-9, + - =, ?, ?) tr�i; PlayTunnel/
  StopTunnel; Bootstrap d�ng cho c? enter + return (thay fade den); CT-P38D pin.
  (2) �?/h?ng: bunting c? tam gi�c qua s�n, hoa h?ng/d? + n?m d?, Number Tower
  + Counting Frame + entry beads cycle gold/blue/pink.
  (3a) BuildGround: d?o bay � v�nh d?t du?i c? (MathSkyRim), bi?n m�y y=-14
  (MathSkyCloudSea), 9 m�y quanh v�nh + 3 m�y cao; t?t c? ignoreFromBuild +
  collider-free.
  (3b) MathWorldSignPost/Board + label "To�n" + 3 h?t t?i entry plaza
  (SignWorldPos); arrival beat frame c?t 2.2s r?i FramePointFor t? v? Follow
  nh�n v?t.
  (4) MathWorldBuilder.FollowOffset (0,4.6,6.4) cho spawn Math (Main/hub gi?
  nguy�n offset).
- Test pins: P41D/E (sky skip prefix + sign), P42A (bunting/sign beads),
  P42C cap 400?440 (do th?t 400), P43G 38?43 MathProp, +P38D tunnel structure.
- Verify: EditMode **530/525/0/5** ? build **Succeeded** (World.dll 23:53) ?
  boot headless **FACE_OK 0 exception**. Temp tooling x�a s?ch. Build review:
  `Temp/opencode/p311b-B1R3Build/LWE.exe`.
- Chua commit (ch? l?nh).

## 32. P3.0.1.1 B1R4 � SMOOTHNESS ROUND (disco/bunting/tunnel) (2026-09-22)

- User round 4 (?nh): (1) chuy?n c?nh ok nhung d�i; (2) chuy?n c?nh v? ?nh,
  kh�ng mu?t; (3) s�n Math nh?p nh�y nhu s�n disco khi player di chuy?n.
- Root cause + fix:
  (1)+(3) MathSkyRim (v�nh d?o) top d�ng y=0 � coplanar v?i m?t c? ? z-fight
  n�u/xanh nh?p nh�y khi camera di chuy?n. Fix: rim t?t 15cm du?i c?
  (y=-1.55, cao 2.8) ? m�p d?o s?ch, h?t fight.
  (2) D�y bunting: LookRotation align +Z theo span nhung length l?i ghi v�o X
  ? d�y th�nh thanh ngang l?c, c? nhu kim cuong bay r?i. Fix: length sang Z.
  Tunnel: sprite v�ng 256px anti-alias (h?t v? khi upscale), glyph ch? d�ng
  k� t? LegacyRuntime c� (1-9, + - =; b? ?/? v� hi?n � l?i tr�n v�i m�y),
  SmoothStep + xoay ch?m hon, max ring 2.9?2.2, fade 0.18/0.22s, hold tru?c
  load 320?120ms (ng?n l?i theo y�u c?u).
- Verify: EditMode **530/525/0/5**; build **Succeeded** (World.dll 06:27);
  boot **FACE_OK 0 exception**. Temp build script x�a (gi? Assets/Editor.meta
  tracked). Build review: `Temp/opencode/p311b-B1R4Build/LWE.exe`.
- Chua commit (ch? l?nh).

## 33. B1R5 � RIM BURIAL FIX (2026-09-22)

- User ?nh: s�n b? "ng?p" n�u � B1R4 d?i rim sang scaleY 2.8 nhung cylinder
  primitive cao 2*scaleY = 5.6m, d?nh +1.25m ph? k�n d?o. Fix: scaleY 1.4,
  center y=-1.5 ? d?nh -0.10m (m�p d?o du?i c?, kh�ng ph?, kh�ng z-fight).
- Verify: suite 530/525/0/5 ? build **Succeeded** (World.dll 06:36) ? m?
  `Temp/opencode/p311b-B1R5Build/LWE.exe`. Temp x�a. Chua commit.

## 34. B1R6 � SIGN FIX + DECLUTTER (2026-09-22)

- User ?nh: s�n l?n x?n + b?ng t�n world l?i (board den, kh�ng ch?).
- Fix: label d�ng SetupLocked d?t tru?c board 22cm, kh�a hu?ng v? s�n (d�ng
  pattern hub gate); b? bunting (kim cuong bay r?i); domino k�o s�t l? du?ng
  entry (x=�1.9, c�ch 1.5m, tile to hon) th�nh number walk g?n; rim stone
  6 nh? -> 4 to d?m ? 4 g�c ch�o.
- Pins: P41D/P42A/P43J c?p nh?t. Verify suite 530/525/0/5 ? build Succeeded
  (World.dll 06:43) ? m? `Temp/opencode/p311b-B1R6Build/LWE.exe`. Temp x�a.
  Chua commit.

## 35. B1R7 � OUTER = SKY (2026-09-22)

- User: ph?n outer dang tr?ng, c?n th�nh b?u tr?i. B? plane MathSkyCloudSea
  (y=-14) � ngo�i d?o gi? l� n?n tr?i xanh c?a camera; gi? 9 m�y quanh v�nh
  (h? th?p) + 4 m�y du?i ch�n d?o. P41D pin d?i sang MathSkyCloud0.
- Verify: suite 530/525/0/5 ? build Succeeded (World.dll 06:57) ? m?
  `Temp/opencode/p311b-B1R7Build/LWE.exe`. Temp x�a. Chua commit.

## 36. P3.0.1 REAL JOURNEY + HIDDEN BUG GATE � PASS (2026-09-22)

- M?c ti�u: ch?ng minh skeleton Math d? tin c?y tru?c l?p Gameplay Pattern
  (KH�NG redesign/polish trong pass n�y).
- C�ch ch?y: build standalone release + journey driver t?m (click th?t qua
  Input System t?i to? d? m�n h�nh; KH�NG teleport/Warp/SetPosition/direct
  state/fake interaction), log t?ng transition; driver xo� sau khi xong.
  Docs: `docs/P3.0.1_MATH_JOURNEY_AUDIT.md` + `docs/P3.0.1_HIDDEN_BUG_AUDIT.md`.
- Journey th?t khui 8 bug ?n (unit test kh�ng th?y):
  J1 P1 � card mic offer (640x460 + Body text) an raycast ? click n?a ph?i
  m�n h�nh b? nu?t, player k?t ? "Find the one". Fix: card/title/body/status
  raycastTarget=false (MicSetupDialog + DependencySetupDialog; n�t v?n b?m).
  J2 P1 � return gate MathScene bind SubjectIds.Main nhung SubjectGate return
  branch ch? fire khi Current==target (Math?Main) ? c?a V? KH�NG BAO GI? fire,
  tr? k?t trong Math. Fix: bind SubjectIds.Math (pin CT-P43K).
  J3 P1 � DeactivateMainPresentation t?t root ch?a Main NavMeshSurface ?
  NavMeshSurface.OnDisable?RemoveData() g? navmesh Main ? "Return warp failed;
  player stays" (player k?t ? to? d? Math sau unload). Fix: chuy?n NavMesh GO
  c?a Main v�o PersistentCore (d?ch v? core persistent).
  J4 P1/P2 � return warp v? d�ng v? tr� cu (mi?ng c?ng) + latch _wasInside c�n
  true ? c?ng kh�ng re-fire, kh�ng re-enter du?c. Fix: n?u v? tr� cache n?m
  trong 2.2m c?ng th� warp ra 3m ph�a s�n (log "Return warp -> (7.7,-2.8)").
  J5 P1 � host tuoi sau re-entry chua t?ng th?y QuestStartedEvent ? bring b?t
  kh? thi. Fix: IsFindDone/CompleteBring state-driven (pin CT-P40E).
  J6 P2 � bloom m?t khi re-entry sau completion (ch? nghe event). Fix: Build
  nh?n IQuestService, adopt quest d� Completed (pin CT-P40F).
  J7 P2 � cube "one" hi?n l?i sau completion khi re-entry (token InWorld).
  Fix: carry adopt Consumed (pin CT-P40G).
  J8 P2 � bake Math d�ng CollectObjects.All qu�t M?I scene (log bake li?t k�
  mesh Main) ? navmesh ch?ng l�n Main + bake player/NPC Main th�nh obstacle.
  Fix: surface d?t tr�n MathWorld root + CollectObjects.Children.
- Verify: 6 v�ng journey; v�ng cu?i PASS tr?n: spawn ? gate ? Math (scenes 3,
  hosts/directors/carries/blooms/interactables=1) ? talk ? find (WordSeen,
  Carried) ? bring (QuestCompleted, Consumed, bloomShown=True) ? bridge t?i
  clearing (76,-9.6) ? return (warp 7.7,-2.8; scenes 2; Math objects 0; HUD
  restore) ? re-entry (objects 1/1, quest gi? idx=2, bloom adopt=True) ? return
  2 s?ch. Kh�ng duplicate ? 12 dump.
- Suite cu?i: **534/529/0/5**; build release **Succeeded** (World.dll 08:01);
  boot **FACE_OK 0 exception**. Temp tooling xo� s?ch; asmdef Bootstrap revert.
- VERDICT: **READY FOR GAMEPLAY FOUNDATION** (kh�ng c�n P0/P1). Next theo brief:
  Gameplay Pattern Foundation (pattern ? question type), chua l�m quest content.
- Commit + push main theo l?nh user.

## 37. S2 - GATE-SHAPE PASS (user round: "cổng thừa / cổng cần dev quá mờ nhạt") (2026-09-22)

- User round (2 khu vực, đã chốt qua hỏi đáp): (1) quá nhiều cổng thừa — cổng về
  chỉ để về sảnh chính chọn môn; (2) cổng cần dev quá mờ nhạt, chưa ra hình hài
  cổng. Chốt xử lý: cổng về = MARKER (disc + chữ "Về"), không còn dáng cổng;
  10 cổng micro-world (Math hub) + 4 cổng môn học (sảnh chính) phải ra hình hài cổng thật.
- Math hub (`MathWorldBuilder`):
  (1) 10 gate: thêm khung cổng dùng chung `GateFrame` — 2 trụ 2.7m (local ±1.6)
  + xà 3.8m @2.78m tô accent của từng cổng; motif giữ đặc trưng (bead row, kính
  lúp, fruit row, cặp hình, color bins, peg board, cottage, giàn giáo, deck,
  moon) gắn trên/trước khung. Xà bake-ignored (headroom rule), toàn bộ collider-free.
  (2) `MathGateBody` offset `GateBodyZ=-2.0`: thân cổng đứng SAU waypoint của
  ring ⇒ ring 1.5m giữ nguyên bề rộng (cluster cũ bị pinch), cổng vẫn quay mặt hub.
  (3) Name pill neo theo body, cao 3.6m (trên xà).
  (4) Cổng về: bỏ 3 mảnh arch (MathReturnA/B/Beam) — chỉ còn disc vàng d2.6 +
  label "Về" + trigger ẩn (P43A pin "marker, not gate").
- Sảnh chính (`SubjectWorldBuilder`): 4 cổng môn nâng thành silhouette cổng thật —
  Toán: trụ cube 2.45m + xà cylinder @2.38 + finial; Tư duy: bánh răng 2.1m +
  puzzle beam @2.62; Tiếng Anh: sách 2.3m + xà @2.5 + crown; Tiếng Việt: tablet
  2.1m + banner @2.5 + nón lá ~3.1m. Board tên gắn TRÊN mặt xà (bỏ board bay).
  Mọi xà/crown ignoreFromBuild; vị trí trụ/carve/road giữ nguyên.
- Pins cập nhật có chủ đích: P41D (MathReturnDisc), P43A (return marker),
  + mới P43L (subject gates are real gates) + P45J (10 gate frames).
- Verify: EditMode **557/552/0/5**; build release **Succeeded** errors=0
  warnings=5 (pre-existing) size=109,391,897; boot **FACE_OK 0 exception**
  (`s2-boot.log`). Census: math=634 (cap 640), hubShell=289. Temp build script +
  TempCensus xóa sạch.
- Docs cập nhật: MATH_HUB_GATES/DESIGN/VISUAL_QA §S2.
- Chưa commit (chờ lệnh).
- F9 (audit): Spatial Hub thôi dựng district Toán chết (medallion/core/tree/road)
  + điểm "Về" thừa trong MarketScene; giữ cổng Toán + signpost; slot ReturnGates
  null-aligned theo catalog nên binding không lệch (P43E re-pin: 3 spatial returns).
- Verify lần cuối (sau F9): EditMode **557/552/0/5**; build **Succeeded** errors=0
  warnings=5 size=109,391,897; boot **FACE_OK 0 exception**.
## 38. S3 - CARTOON GATE PASS (user round: "các cổng giống nhau quá") (2026-09-22)

- User (kèm ảnh): 10 cổng nhìn giống hệt nhau; muốn cartoon hơn và đúng bản
  chất từng micro-world.
- Fix (`MathWorldBuilder`): bỏ khung post-and-lintel dùng chung; thêm cartoon
  kit (CartoonPost trụ tròn + chỏm bi, BlockPost khối số, StripedPost sọc công
  trường, CartoonArch vòm dày nửa ellipse nhiều màu, CartoonRing vòng kính lúp,
  MushroomPost nấm bi đỏ chấm trắng) và làm riêng từng cổng:
  01 Vườn Đếm: chân khối 1-2-3 + vòm xanh + dãy bead treo dưới vòm.
  02 Vườn Khám Phá: cổng LÀ kính lúp khổng lồ (vòng mint + mặt kính + cán).
  03 Vườn Trái Cây: chân là cây ăn quả, vòm cành lá, táo treo + giỏ.
  04 Đồng Ghép Cặp: hai nửa đối xứng (aqua|gold) gặp nhau ở chỏm hồng + cặp
  hình bên dưới.
  05 Công Viên Phân Loại: vòm cầu vồng + 3 topper hình dạng + 3 bin màu.
  06 Xưởng Xếp Hình: hai thanh ghép gỗ lệch tầng (jigsaw) + bàn thợ.
  07 Làng Giao Hàng: mái nhà + ống khói + bưu kiện + hòm thư.
  08 Sân Xây Dựng: chân sọc vàng/nâu + dầm + cần cẩu + móc + vật liệu.
  09 Cầu Số: vòm đá + mặt cầu gỗ + lan can + suối đá.
  10 Rừng Trí Nhớ: chân nấm đốm đỏ + vòm tím chạng vạng + mặt trăng + nấm nhỏ đôi.
- Nav/index giữ nguyên: mọi mảnh vòm bake-ignored (headroom), chân collider-free
  bake off-path, body vẫn lùi GateBodyZ=-2.0 khỏi ring.
- Pins: P45J pin chân ±1.6 + mảnh MathGateArch* ignored + map motif riêng từng
  cổng; P42C/P45E re-pin cap 640 -> 720 (đo 698, ghi ở MATH_HUB_VISUAL_QA).
- Verify: EditMode **557/552/0/5**; build release **Succeeded** errors=0
  warnings=5 size=109,395,481; boot **FACE_OK 0 exception** (`s3-boot.log`).
  TempCensus/build script xóa sạch. Docs: MATH_HUB_GATES/DESIGN/VISUAL_QA §S3.
- Chưa commit (chờ lệnh).
## 39. S4 - DECLUTTER + FIX CỔNG + CAMERA ORBIT (user round 3 ảnh) (2026-09-22)

- User: (1) một số thứ rối mắt; (2) cổng vẫn có cái bị lỗi; (3) cần giữ chuột /
  con lăn để điều chỉnh hướng nhìn map thay vì cố định 1 góc.
- Cổng (`MathWorldBuilder`):
  - Vòm cartoon: overlap mảnh 1.25 -> 1.06 (hết góc nhô như gãy).
  - Xưởng Xếp Hình: tab jigsaw khóa giữa 2 thanh + trả lại 2 chân bàn thợ.
  - Sân Xây Dựng: thay dầm chéo (trông như bập bênh) bằng cần cẩu tháp:
    dầm + cột + cần ngang + cáp + móc; vật liệu dời ra sau.
  - Làng Giao Hàng: mái ngắn về đúng nhịp trụ; Cầu Số: bỏ sỏi rill; Vườn Đếm:
    bỏ trio; Vườn Khám Phá: bỏ bụi + sỏi; Ghép Cặp/Trí Nhớ: bỏ cặp bi thừa.
- Declutter hub: bỏ 6 pebble entry + 4 tuft grass (MathGateEdge0-5, Tuft0-3) —
  domino walk + chevron đã đủ dẫn mắt. Census 698 -> 681 (cap 720 giữ nguyên).
- Camera orbit (`SmartCamera`, global): giữ con lăn HOẶC chuột phải + kéo để
  xoay follow view; yaw tự do, pitch kẹp -22..+42; zoom lăn giữ nguyên. Neutral
  (yaw0/pitch0/zoom1) tái tạo đúng framing khóa. Pure seams OrbitOffset /
  ApplyOrbitDrag; pin CT-P32E/F/G (+3 test). Ghi chú contract ở
  CONSTRAINED_3D.md §2.
- Pins: P45H bỏ MathGateEdge0/Tuft0, thêm Accent0Stem/Accent1Stem.
- Verify: EditMode **560/555/0/5**; build release **Succeeded** errors=0
  warnings=4 size=109,395,481; boot **FACE_OK 0 exception** (`s4-boot.log`).
  TempCensus/build script xóa sạch.
- Chưa commit (chờ lệnh).
## 40. S5 - FIX CỔNG CẦU SỐ + SÂN XÂY DỰNG (user ảnh: "Nó đang kiểu gì đây?") (2026-09-22)

- User ảnh cận cảnh 2 cổng: Cầu Số có "mặt cầu" chơ vơ trên vòm + 2 que nhỏ;
  Sân Xây Dựng có dầm chéo mảnh trông như giàn giáo gãy.
- Fix (`MathWorldBuilder`):
  - Cầu Số: deck gỗ đặt khít trên đỉnh vòm (3.9 x 1.0 @3.3) + 2 trụ lan can
    dày ở 2 đầu deck (thay 2 que mảnh giữa); giữ boardwalk sau cổng. Trụ đá
    hạ còn 1.9m để vòm spring từ chỏm bi.
  - Sân Xây Dựng: thay cần cẩu mảnh bằng cần cẩu giàn vàng chunky — cột giữa
    (0.3) + cần ngang đối xứng (3.4) + 2 cáp: bên trái treo kiện hàng, bên
    phải treo móc; đọc rõ BUILD từ lobby.
- Verify: EditMode **560/555/0/5**; build release **Succeeded** errors=0
  warnings=4 size=109,395,993; boot **FACE_OK 0 exception** (`s5-boot.log`).
  Temp build script xóa sạch.
- Chưa commit (chờ lệnh).
## 41. S6 - BEAUTY + GAM HỒNG (user: "world đẹp hơn" + "game cho con gái") (2026-09-22)

- User: làm world đẹp hơn (được sửa bất cứ gì) + thêm gam hồng vì game hướng
  tới bé gái là chính.
- Kit mới `A_World/WorldBeauty.cs` (primitive, shared material, collider-free):
  BlossomTree (thân + 3 tán hồng), PetalCarpet (thảm cánh hoa), FlowerDrift
  (cụm hoa pastel hồng), PastelRainbow (6 dải hồng→lilac + 2 mây chân), và
  ApplyMainAtmosphere / ApplyMathAtmosphere.
- Math hub: 6 cây hoa anh đào + thảm dưới gốc, 8 cụm hoa hồng ở lobby/entry,
  cầu vồng pastel đáp sau cổng Bắc tại (0,0,21) bán kính 11 (view arrival +
  lobby nhìn Bắc nên cầu vồng nằm giữa khung), mây đổi sang hồng nhạt.
- Sảnh chính: 4 cây hoa anh đào + thảm + 6 cụm hoa trên bãi cỏ (có guard
  clear-zone/walkway/gate-plaza nên không đè đường đi).
- Không khí mỗi world: MarketBootstrap đổi fog/ambient khi travel — Math dùng
  sương mù xa 32-170m (bầu trời/mây/cầu vồng hiện rõ; fog 18-45m của Main
  trước đây nuốt mất), quay về Main khôi phục 18-45m.
- Pins: P45K (beauty landmarks + rainbow bake-ignored), P42A thêm prefix
  MathRainbow/MathBlossom/MathPetal/MathFlowerDrift, P42C/P45E re-pin cap
  720 -> 820 (đo 777). Art direction ghi ở GAME_DESIGN.md §8.
- Verify: EditMode **561/556/0/5**; build release **Succeeded** errors=0
  warnings=4 size=109,400,745; boot **FACE_OK 0 exception** (`s6-boot.log`).
  TempCensus/build script xóa sạch.
- Chưa commit (chờ lệnh).
## 42. S7 - FULL BLOOM (user: "đẩy tới nóc") (2026-09-22)

- User: "Đẹp đấy, đẩy mạnh hơn nữa. Bắt đầu có hồn hơn rồi. Đẩy tới nóc đi."
- Kit chung thêm: `PetalFall` (cánh hoa hồng rơi lả tả, transform-only,
  deterministic, batch-safe), `ButterflyDrift` (bướm pastel bay vòng quanh
  cụm hoa, vỗ cánh), `WorldBeauty.TrunkPost` (thân trụ cho vòm hoa).
- Math hub: 12 cây hoa anh đào + thảm (mọi bãi cỏ), 14 cụm hoa hồng, 20 cánh
  hoa rơi trên lobby, 5 bướm, vương miện hoa anh đào trên cổng entry; trời
  Math chuyển sắc sakura (camera background trong atmosphere swap).
- Sảnh chính: 6 cây hoa anh đào + thảm, 10 cụm hoa, VÒM HOA ANH ĐÀO chào ở
  lối vào (trụ ±1.7 ngoài corridor, tán cao 2.3m+ để không cắt navmesh),
  12 cánh hoa rơi, 2 bướm.
- Budget: đo 870, cap re-pin 820 -> 920 (MATH_HUB_VISUAL_QA.md).
- Pins: P45K mở rộng (petal/butterfly/entry crown), P42A thêm prefix.
- Verify: EditMode **561/556/0/5**; build release **Succeeded** errors=0
  warnings=4 size=109,405,641; boot **FACE_OK 0 exception** (`s7-boot.log`).
  TempCensus/build script xóa sạch.
- Chưa commit (chờ lệnh).
## 43. S8 - FIX CỔNG CẦU SỐ + CÂY DÍNH CẦU + CỘT VÒM HOA (user ảnh) (2026-09-22)

- User (2 ảnh): (1) cổng Cầu Số "còn 1 cái thanh ở trên, Failed"; (2) chỗ cây
  hoa anh đào mới "bị dính vào cổng" (dính vào cầu gỗ thật); (3) vòm hoa ở
  sân spawn có "đám mây" trên cổng nhưng cột trơn — "làm cột cũng phải đẹp
  tương xứng".
- Fix:
  - Cầu Số: bỏ tấm deck gỗ trên vòm + 2 trụ lan can nhỏ; giờ là vòm đá sạch
    + keystone (đá khóa vòm) + boardwalk gỗ dưới chân. P45J đổi motif pin
    sang "MathGateKeystone".
  - Dời cây hoa anh đào: (17.5,-2.5) -> (21,-1.5); (19.5,6.5) -> (21.5,8.5);
    (-9,-8.5) -> (-9.5,-7.5) — hết dính cầu/orchard gate.
  - `WorldBeauty.PrettyPost`: trụ chân loe + thân thuôn + chỏm bi; vòm hoa
    sân spawn dùng trụ này + 2 chùm lá xanh 2 bên.
  - Pin mới P45K: cây ≥3.5m, cụm hoa ≥2.0m cách MỌI gate body + cầu gỗ thật
    + tâm vườn — chống tái phát "dính vào cổng".
- Verify: EditMode **561/556/0/5**; build release **Succeeded** errors=0
  warnings=4 size=109,406,153; boot **FACE_OK 0 exception** (`s8-boot.log`).
  TempCensus math=868. Temp scripts xóa sạch.
- Chưa commit (chờ lệnh).
## 44. S2 PIONEER MICRO-WORLD - COUNTING GARDEN v1 (2026-09-22)

- Mục tiêu: micro-world thật đầu tiên + chuẩn tái sử dụng cho 9 cái sau; tái
  dùng toàn bộ Core (không manager/service/bus/save/camera thứ hai).
- Thêm: `CountingGardenArea.cs` (module scene-local: anchors + beat vào/ra:
  tunnel, warp, staging Tess, camera frame/follow, HUD cache/restore, pure
  state seams), `MicroWorldPortal.cs` (cổng đi bộ 2 chế độ Enter/Exit, cùng
  pattern poll XZ như SubjectGate — 1 component cho cả 9 micro-world sau).
- MathWorldBuilder: staging Vườn Đếm (vòm hoa + bảng tên khóa, walk quanh co,
  giỏ thu hoạch + 3 táo, cây hoa anh đào/thảm/hoa, petal rơi, backdrop), bộ
  anchor vườn `MathGardenPresentationRoot` đủ 8 slot, portal ở miệng cổng
  counting_garden, portal exit ở cổng vườn, contract statics (entry/exit/
  focus/host/camera/reward/hub-return).
- GameInstaller: bind area với refs sống (player/camera/HUD/Tess) + gán vào
  mọi portal; lookup anchor MAIN đổi sang tìm theo tên (tránh registry vườn
  cướp beat arrival).
- Loop dùng nguyên math_counting (find "one" trong hàng đếm 1-2-3 -> mang cho
  Tess -> bloom); Tess được stage vào vườn khi vào, về nook khi ra; adoption/
  re-entry giữ nguyên (P40-P43 xanh).
- Tests: CT-P46 **5/5** (portal hợp đồng, anchors hợp lệ, staging identity,
  state machine không double enter/exit, điểm hạ cánh ngoài bán kính portal).
  Suite **566/561/0/5**. Budget cap re-pin 920 -> 990 (đo 938).
- Verify: build release **Succeeded** errors=0 warnings=4 size=109,414,665;
  boot **FACE_OK 0 exception** (`s2p-boot.log`). Temp scripts xóa sạch.
- Hạn chế v1: loop đếm hiện là quest cũ (tìm "one"), chưa có quest đếm 3 quả
  + 4 câu thoại mới; host là Tess hiện có; chưa chạy journey driver thật.
  Record đầy đủ + checklist human review: `docs/S2_COUNTING_GARDEN.md`.
- Chưa commit (chờ lệnh).
## 44b. S2 v2 - CHUYỂN COUNTING GARDEN THÀNH SCENE RIÊNG, LAZY-LOAD (2026-09-22)

- User chỉnh kiến trúc: Counting Garden phải là SÂN MỚI (scene riêng) giống
  MathScene khi đi từ sảnh chọn môn; warp xong phải tới sân mới; LAZY LOAD
  không load từ đầu; trên sân quây 5 khu vườn theo cánh cung (chỉ quây khu).
- Bỏ staging trong MathScene (v1) — sân Toán trở lại nguyên trạng (khu vườn
  pilot cũ giữ nguyên); cổng Ở Math Hub vẫn là cửa vào micro-world.
- Thêm: `WorldTransition` micro slot (EnterMicroAsync/ExitMicroAsync/MicroScene,
  LAZY, honest failure, chặn ReturnAsync khi đang trong micro), `CountingGardenBuilder`
  (scene contract + sân: ground/hedge/entry arch/exit portal/5 khu arc 50-130°
  r11 có fence + mouth + anchor/nền/đường/dressing S6-S7/anchors 8 slot),
  `GameInstaller.BuildCountingGardenScene` (sceneLoaded -> build lazy -> đẩy
  entry/anchors vào area), `CountingGardenArea` v2 (async beats), scene asset
  `Assets/A_World/CountingGarden/CountingGardenScene.unity` + Build Settings.
- CT-P46 viết lại **6/6** (lazy contract P46B: scene KHÔNG load lúc vào subject,
  chỉ load khi EnterMicro; subject vẫn nằm dưới; exit unload; return bị chặn khi
  trong micro). P37D re-pin: machine sở hữu 2 call site load/unload (subject +
  micro), vẫn là nơi duy nhất chạm ISceneOps. Suite **567/562/0/5**.
- Verify: build release **Succeeded** errors=0 warnings=6 size=109,948,697;
  boot **FACE_OK 0 exception** (`s2v2-boot.log`). Temp scripts/sân tạo bằng
  editor script đã xóa.
- Hạn chế: 5 khu đang trống (đúng lệnh); chưa NPC trong sân mới; chưa chạy
  journey click thật (EditMode + build + boot).
- Chưa commit (chờ lệnh).
## 44c. S2 v3 - FIX "SANG SÂN VƯỜN ĐẾM RỒI BỊ QUAY LẠI MATH" (2026-09-22)

- User: vào được sân vườn đếm nhưng bị trả về Math World ngay.
- Root cause 1: điểm warp vào vườn (0,0,-10) nằm TRONG bán kính cổng exit
  (0,0,-10.4, r1.35) -> cổng exit fire ngay frame đầu. Fix: điểm vào dời ra
  (0,0,-8) + `MicroWorldPortal` cold start (latch khởi tạo TRUE, chỉ arm sau
  khi người chơi ra khỏi bán kính một lần — bài học J4) + pin P46D.
- Root cause 2 (chặn chơi): sân mới CHƯA bake NavMesh -> bé không đi được.
  Fix: `CountingGardenBuilder.BuildNavMesh` (NavMeshSurface trên garden root,
  CollectObjects.Children — đúng bài học J8, không bake chéo scene).
- Verify: EditMode **567/562/0/5**; build **Succeeded** errors=0 warnings=4
  size=109,949,209; boot **FACE_OK 0 exception** (`s2v3-boot.log`). Chưa commit.
## 45. S3-P2V - VISUAL RECONSTRUCTION: COUNTING GARDEN + DEMO (2026-09-22, batch+standalone)

- Lệnh user: nhìn bằng camera thật, chụp trước/sau, RECOMPOSE (không polish mù).
  Bắt buộc: standalone + ảnh thật + GitHub principles. Deliverable: micro-world
  nhìn là hiểu, demo "3 giây hiểu ngay".
- TOOLING TẠM (đã xóa trước commit): `TempS3P2VDriver` (RuntimeInitializeOnLoad,
  inject chuột THẬT qua Input System: click/drag; ScreenCapture full-client;
  tự ẩn MicDialog/PhoneCameraHud/MicStatusHud trước mỗi shot; log phase demo +
  mode camera mỗi shot) + `TempBuildS3P2V` (build 4 scene). Ảnh: 
  `Temp/opencode/s3p2v0-before/` (BEFORE) vs `s3p2v5-after/` (AFTER).
- BUG THẬT DO JOURNEY KHAI: (1) click trong vườn bị nuốt IM LẶNG — router giữ
  bounds Math (60±27) sau warp, đảo ở +120 -> bé KHÔNG đi được trong vườn. Fix:
  `CountingGardenBuilder.BoundX/Z` + `CountingGardenArea.BindRouter` push khi
  vào / restore Math khi ra + GameInstaller wire `_activeBuilder.Router`.
  (2) camera follow kế thừa offset Math (+z) -> nhìn RA NGOÀI vườn (cả thế giới
  sau lưng trẻ). Fix: `CountingGardenBuilder.FollowOffset (0,3.8,-5.0)` — nhìn
  VÀO vườn; area gọi Follow(garden offset) trước beat arrival.
- BEFORE (audit ảnh thật): 5 plot tròn r11 chồng nhau thành "fence snake"; cây
  blossom to phủ kín zone; board demo quay cạnh (edge-on); number lệch khỏi
  frame; follow cao/xa; path/pad z-fight lỗ chữ nhật; rainbow cắt góc card.
- RECOMPOSE: plaza (0,1.5) + number stones 1..5 (bead đếm được); crescent r9.5
  = 4 GARDEN BEDS (soil + crop đếm 3/4/5/2 + post số + fence 3 cạnh + hoa) +
  DEMO THEATRE (index 2) quay mặt plaza; reward pocket tây; crescent walk +
  demo spur; meadows 2 tông; cây dời khỏi sightline (entry/rim), petal carpet
  nhỏ lại; rainbow dời tây xa (-22,16). Beds thay "chuồng rỗng" -> vườn thật.
- DEMO CARD (camera-first): cam (0,2.7,5.4)->look (0,0.9,10.4); trái->phải =
  number board ("2" vàng + 2 chấm đỏ) -> NPC (đứng trong KHE giữa props) ->
  pedestal 2 táo -> basket có rim -> result board ("2" + tick) hiện SAU khi
  xếp; NPC carry neo trước ngực (thấy rõ 2 táo); beat GIỮ khi trẻ đứng xem
  (re-issue 3s), re-arm khi đi xa; arrival cam nâng (0,9.5,-17.5) + entry arch
  lùi z-14.5 khỏi che plaza. Diag xác nhận: arrival beat FIRE (Interaction),
  demo beat HOLD (Interaction suốt loop).
- Re-pin tests có chủ đích: P46D (demo pad/anchor cho zone 2), CT-P47 viết lại
  theo layout mới (beds/number stones/demo theatre/reward/corridor/scale),
  CT-P48 theo tên stage mới. KHÔNG đụng Core/Math/transition/quest/save.
- Verify: full EditMode **577/572/0/5** (baseline 577/572) -> build production
  (KHÔNG driver) **Succeeded** -> boot **ALIVE + FACE_OK + 0 exception**.
  Temp tooling xóa sạch (0 file, Assets/Editor trống).
- CÒN LẠI CHO MẮT NGƯỜI: duyệt ảnh BEFORE/AFTER + tự lái thử (đi bộ, xem loop,
  độ dễ hiểu 3 giây). Chưa làm Phase 3.

## 46. S3-P2W - DEMO V2: TWO-NPC MINI LESSON "TAKE TWO BALLS" (2026-09-22)

- Lệnh user (kèm script diễn đầy đủ): demo phải là TIẾT HỌC MINI 2 NPC —
  CÔ GIÁO (bảng số 2) + HỌC SINH (bé), cô giao bài -> bé lấy ĐÚNG 2 trong 5
  quả bóng -> mang bỏ giỏ -> cô hỏi/xác nhận -> 2 tick -> khen -> reset loop.
  Camera phải ĐỔI SHOT: cô+bảng khi giảng, bé+2 bóng+giỏ khi làm; bảng luôn
  nằm trong composition chính; không zoom quá mất context.
- Stage mới (thay apples cũ): BOARD treo CAO trên đầu cô (post + panel 2.3x1.5
  + "2" khối 3 thanh vàng); 5 BÓNG màu (đỏ/xanh dương/vàng/xanh lá/hồng) trên
  thảm cỏ; GIỎ to bên phải; RESULT board "2 + tick" nâng trên cột (không bị
  người che); cô giáo (-0.6,11.4) + bé (0,10.2) + ball field (0,9.6) + giỏ
  (2.4,9.0) — đúng thứ tự chiều sâu board -> cô -> bé -> bóng -> giỏ.
- 2 SHOT camera scene-authored: A (0,2.2,5.6)->(0,1.35,11.8) = bảng+cô+bé+
  bóng+giỏ; B (0.9,2.1,6.6)->(0.75,0.85,9.9) = bé+2 bóng+giỏ+result. Sequence
  tự đổi shot khi cô giao xong (SetShot(true)) và về A khi reset; beat giữ
  theo shot đang active (re-issue 3s) như cũ.
- Diễn xuất (không AI): cô xoay về bảng/bé/giỏ theo beat + vẫy tay chỉ; bé
  xoay theo, đi tới bóng, PickUp từng quả (bóng bay cung vào ngực — cùng vật
  thể, không teleport), ShowTwo (2 bóng nhìn rõ), mang giỏ, thả từng quả,
  2 quả nằm trên miệng giỏ (y0.76, tách 0.56); celebrate: cả 2 Victory + Happy
  + bé PlayHop; reset: 2 bóng bay về sân, bé về chỗ, result ẩn, bảng giữ "2".
- 12 lời thoại EN (VoiceProfileId npc_female_01 qua IAudioDirector, literal
  trực tiếp — KHÔNG sửa manifest 40 dòng, không audio mới): "Look at the
  board!" / "This is number two." / "Two." / "Today, we take two balls." /
  "Take two balls, please!" / "Put them in the basket!" / "One ball." /
  "Two balls!" / "How many balls?" / "Yes! Two balls! Well done!" (mỗi câu ≤6
  từ; offline = hình vẫn đủ nghĩa).
- Nhận xét vòng capture (5 vòng THẬT standalone, driver tạm inject chuột):
  vòng 1 bảng bị cô che -> nâng bảng; số "2" bị mirror -> đổi sang khối 3 thanh;
  petal rơi ngang card -> dời khỏi sân khấu; cây backdrop sau bảng lộ tán hồng
  -> dời sang (5.2,16); result board bị bé che -> nâng trên cột (2.9,9.3);
  bóng thứ 2 trong giỏ bị khuất -> tách slot. Tooling tạm đã xóa sạch.
- Pin mới CT-P48 (viết lại): stage 5 bóng + shot A/B markers; 2 actor (bé nhỏ
  hơn cô, face kit, click-through, carry anchor); loop 21 phase đúng kịch bản
  (bé lấy bóng #3/#4, confirm có result + 2 bóng ở giỏ, reset đủ 5 bóng về
  home, không teleport); camera beat once/hold/re-arm + reframe A->B.
  CT-P47A/D đổi tên prop cũ (apple/pedestal -> ball field/balls).
- Verify: targeted 15/15; full EditMode **577/572/0/5**; build production
  (không driver) **Succeeded**; boot **ALIVE + FACE_OK 0 exception**. Ảnh
  BEFORE/AFTER: `Temp/opencode/s3p2w*-shots/` (lesson shots A/B).
- Chưa commit trước đó; commit kèm phase này. Chưa làm Phase 3.

## 47. S3-P2W2 - LESSON FIX ROUND (user ảnh: số sai, NPC chồng, camera) (2026-09-22)

- User ảnh + 3 lệnh: (1) số trên bảng hiện KHÔNG đúng; (2) 2 NPC có lúc chồng
  lên nhau; (3) kiểm tra lại camera; (4) làm sinh động nhất có thể.
- Root cause (1): số "2" bị CẮT bởi khung (shot B cũ chỉ thấy nửa dưới của số
  -> đọc thành hình sai). Fix: cả 2 shot đều giữ TRỌN bảng trong frame; số
  dạng 3 thanh (top bar phóng to 1.15w + diagonal + bottom) đọc chuẩn.
- Root cause (2): cô + bé đứng gần như cùng trục -> che nhau. Fix: cô dời
  (-1.35,11.6) đứng BÊN TRÁI mép bảng, bé (0.75,10.1) lệch phải-trước ->
  side-by-side, không còn chồng; bảng "2" cũng hết bị đầu cô che.
- Camera: shot A (0.3,2.5,6.6)->(0.7,1.5,11.6); shot B (0.9,2.3,4.8)->
  (0.85,1.05,10.3) — push-in vừa, giữ trọn board/result. Viewing spot dời về
  (0,2.6) để avatar CỦA BÉ không bao giờ lọt vào shot B (vòng capture trước
  đầu bé ở góc frame). HUD pill không còn che hàng bóng.
- Sinh động (không thêm framework): cô + bé Hop khi khen (Victory + Happy +
  vẫy tay); bé Hop + Surprised khi lấy được bóng 2; Curious cả 2 khi cô hỏi
  "How many balls?"; bảng kết quả POP scale 0.65->1 khi hiện; bóng nảy nhẹ khi
  tiếp đất; 5 bóng trên sân bob nhẹ lệch pha; bé Surprised beat "got it".
- Verify: targeted P47/P48 9/9; full EditMode **577/572/0/5**; build production
  **Succeeded**; boot **FACE_OK 0 exception**. Ảnh vòng 5/6: `Temp/opencode/
  s3p2w8-shots/`. Temp tooling xóa sạch. Commit kèm phase này.

## 48. S3-P2W3 - BUG THẬT: SEGMENT CON XOAY SAI (số sai) + XÓA BỤI CHE (2026-09-22)

- User ảnh: (1) "chưa có số hoàn chỉnh"; (2) "xóa lùm cây kia đi, nó đang che".
- ROOT CAUSE (1) - bug thật của builder, ảnh hưởng MỌI Box(): helper
  `Box/Cylinder/Sphere/Pad/Seg` đều `SetParent(parent)` (worldPositionStays =
  TRUE) và KHÔNG set localRotation -> con của parent XOAY giữ nguyên world
  rotation identity => các thanh NẰM NGANG của số "2" bị xoay 90° thành chĩa
  vào màn hình (nhìn ra ô vuông), số đọc sai. Fix: đổi TẤT CẢ sang
  `SetParent(parent, false)` (21 chỗ) -> con thừa hưởng rotation của parent;
  chỗ nào cần rotation riêng đã set tường minh sau đó (CheckMark, vành miệng
  vườn, soil) nên không đổi gì khác.
- Số "2" nay = 7-segment NỐI LIỀN (A/B/G/E/D, thanh ngang dài w+t, thanh dọc
  dài h/2+t để chồng mối) trên bảng chính (h=1.05 trong panel 2.3x1.5) + bảng
  kết quả (2 + tick). Ảnh crop xác nhận số hoàn chỉnh.
- (2) Bụi backdrop bị đặt sai cung: vòng cũ 65..113° vòng qua HÔNG PHẢI sân
  khấu -> bụi đè bảng kết quả/giỏ. Fix: 3 bụi ở -35/0/+35° (SAU bảng, z 14.4-
  15.1), hết che.
- Kèm round trước (đã commit 4922fcf): tách 2 NPC cạnh nhau, shot A/B giữ trọn
  bảng, viewing spot (0,2.6) để avatar bé không lọt frame, sinh động (hop,
  curious/surprised, result pop, bóng nảy, bob).
- Verify: full EditMode **577/572/0/5**; build production (không tooling)
  **Succeeded**; boot **ALIVE + FACE_OK 0 exception**. Ảnh: s3p2w11-shots +
  crop. Temp tooling xóa sạch. Commit kèm phase này.

## 49. S3-P2L - SYSTEM DIALOGUE LANGUAGE: VI / EN (user order) (2026-09-22)

- Lệnh user: 2 lựa chọn ngôn ngữ hệ thống — (1) Tiếng Việt: MỌI lời thoại NPC
  bằng vi, CHỈ môn Tiếng Anh giữ en; (2) English (mặc định): giữ nguyên tất cả
  như hiện tại.
- Kiến trúc (không manager/service mới): `_SharedKernel/DialogueLang.cs` —
  state tĩnh Current (English=0/Vietnamese=1) + `T(en, vi)` cho text +
  `Language` (vi-VN / en-US) cho DialogueRequest + `EnglishSubjectActive`
  (môn Tiếng Anh luôn en — hook sẵn, môn này chưa có scene) + `Relocalize`
  cho câu HUD đang hiện + `Init(progress)` (đọc save, override CLI `-lang vi`)
  + `ToggleAndPersist()` (load→modify→save như mọi writer khác).
- Save: `PlayerProgress.Language` + DTO `language` int (additive, guard
  Enum.IsDefined) — save cũ thiếu field tự về English, không đổi format.
- Nguồn thoại đã route 100%: Tess (5 câu), Mia (2), Milo (8), CountingDemo
  (11 câu lesson), MarketBootstrap (BallAsk/BallPraise + lang của SayQuestLine)
  — tất cả qua DialogueLang (locale vi-VN khi bật VI). Câu VI đều ≤6 token
  (Milo ≤8) để KHÔNG bị SafetyFilter nuốt im lặng — CT-P49B/C pin từng câu.
- HUD: các câu objective (Choose a gate! / Look around! / Great job! /
  Find the apple|ball / Bring ... to Mia / Hear it again / "X World"→tên môn /
  Vườn Đếm→Counting Garden / Find the one / Bring it to Tess / Entering) đều
  localized; MathQuestDirector gom qua 1 funnel ShowObjective. Thêm CHIP
  top-right "English/Tiếng Việt" (MarketHUD, pattern replay button) — bấm là
  đổi + lưu + relocalize câu đang hiện ngay. Không đè objective (trái) hay
  replay (dưới).
- Bằng chứng THẬT: driver tạm bấm chip trong build → log
  `[S3P2L] boot lang=English / after click lang=Vietnamese / after click2
  lang=English` + ảnh: chip đổi "English"→"Tiếng Việt", HUD "Choose a gate!"
  → relocalize, vào Math HUD hiện "Toán". Worker TTS trả audio/mpeg cho câu VI
  (test trực tiếp 3 câu qua endpoint ?lang=vi-VN) ⇒ đường thoại VI chạy thật.
- Test mới CT-P49 ×5 (state/picker/subject-exception, 15 câu producer EN+VI +
  safety caps, lesson loop VI, save round-trip + migration save cũ, HUD chip).
  Suite **582/577/0/5** (baseline 577/572 + 5). Build production **Succeeded**;
  boot **FACE_OK 0 exception**. Temp tooling xóa sạch.
- Lưu ý: chữ Milo/Mia/Tess + tên môn giữ nguyên; vocab (từ tiếng Anh) vẫn
  en-US theo thiết kế dạy tiếng Anh; pregen pack vẫn EN (câu VI phát qua TTS
  runtime + cache L2). Commit kèm phase này.

## 45. QUY ƯỚC MÁY + CHẠY FOREGROUND (user order 2026-09-23)

- **Máy ASUS (máy này)**: mọi việc batch/background — EditMode suite, build, log,
  kiểm tra tĩnh. Không cần SSH, nhanh hơn.
- **Máy nhà `maynode` (100.124.132.59, user `Admin`, SSH key riêng
  `%USERPROFILE%\.ssh\id_ed25519`)**: CHỈ việc foreground — boot game, journey
  click thật, xem hình (UltraViewer). Chạy qua Scheduled Task LogonType
  Interactive để hiện trên desktop session.
- **Mọi lần chạy foreground ở máy nhà phải FULL MODE + FULL-HD**:
  `-fullworld -screen-width 1920 -screen-height 1080 -screen-fullscreen 0`.
  (Boot xác nhận: screen=1920x1080, HUD 'Talk to Milo', 3x FACE_OK, 0 exception.)
- Trạng thái máy nhà: repo `E:\LWW\learning-world` sync main; Unity 6000.6.0f1
  có license; build tại `E:\LWW\HubBuild\LWE.exe` (~110MB, Succeeded).
- Chưa commit (chờ lệnh).
## 46. S3-P2L+ - BẢNG CHỌN NGÔN NGỮ + SÂN CHÍNH MỘNG MƠ HỒNG (user order 2026-09-23)

- User: mỗi lần vào game hiện bảng chọn ngôn ngữ (VI/EN) thật đẹp; tái design sân
  chính "mộng mơ màu hồng", bố cục gọn, gate chỉn chu (được phép chỉnh gate).
- `A_World/LanguageDialog.cs` (mới): panel uGUI code-built — nền hồng mờ chặn
  click, card kem viền hồng + bóng, tiêu đề "Chọn ngôn ngữ", 2 card lớn
  "Tiếng Việt"/"English" (accent coral/sky), hoa anh đào trang trí, hint
  "Con chọn ngôn ngữ nhé!". Chọn -> DialogueLang.Set + Persist + refresh HUD chip
  + ẩn panel. Hiện MỖI lần vào game (wire trong MarketBootstrap.Build).
- Sân chính mộng mơ: `WorldBeauty.ApplyMainAtmosphere` đổi fog hồng
  (0.93,0.86,0.94 / 22-70m) + trời hồng nhạt + ambient rose; thêm 3 thảm hồng
  lớn + 2 cây hoa anh đào quanh sân; MỖI gate môn học thêm **vòng threshold màu
  accent** + **crown hoa anh đào** trên xà (VN giữ nón lá).
- Verify: suite local **582/577/0/5**; build local Succeeded (110MB, warnings=7);
  copy sang máy nhà `E:\LWW\LangBuild`, chạy foreground full mode Full-HD:
  `[LanguageDialog] shown`, screen=1920x1080, 3x FACE_OK, 0 exception.
- VISUAL: chờ human review (bảng chọn + sân hồng).
- Chưa commit (chờ lệnh).
## 47. HANDOFF PHIÊN SAU — FLOW CHỌN KHU + SÂN TRÒ CHƠI (VƯỜN ĐẾM) (user order 2026-09-23)

### A. TRẠNG THÁI HIỆN TẠI (đã xong — CHƯA COMMIT)
- `A_World/LanguageDialog.cs`: bảng chọn ngôn ngữ — 2 box viền accent giữa màn
  hình, có GraphicRaycaster (fix bug bấm không phản hồi), chờ HẾT thông báo hệ
  thống (Mic/Dependency/Recording) mới hiện (MarketBootstrap.Build -> QueueShow).
- Sân chính mộng mơ hồng: `WorldBeauty.ApplyMainAtmosphere` (fog hồng
  0.93/0.86/0.94, 22-70m, sky hồng) + 3 thảm hồng + 2 cây anh đào; mỗi gate
  môn học thêm `WorldBeauty.GateRing` + `WorldBeauty.BlossomCrown` (VN giữ nón).
- Fix bug dependency mỗi build: `MarketBootstrap.FindToolsDir` tìm nhiều ứng
  viên + mirror tools vào `persistentDataPath/Tools` (đã verify máy nhà
  stableTools=True, hết popup LAN).
- Git: TẤT CẢ thay đổi trên CHƯA COMMIT — `git status` rồi commit/push main
  khi user lệnh.

### B. VIỆC PHIÊN SAU (làm ngay theo thứ tự)
Flow user chốt: sân chọn môn -> sân chọn loại trò chơi (Math Hub) -> Vườn Đếm
(5 khu) -> chọn 1 khu -> sân trò chơi riêng. Đợt này CHỈ khu có demo sẵn
(`Assets/A_World/CountingGarden/CountingDemo.cs`, scene CountingGardenScene);
4 khu skeleton giữ nguyên (chưa gắn panel/play).

1. `GardenZoneSpot` (mới, Assets/A_World/CountingGarden/): gắn cho khu demo.
   Focus khi CLICK vào khu (dùng ClickRouter/IClickTarget như Interactable)
   HOẶC đi sát ≤2m. Focus = camera FrameAnchor (anchors zone camera/look đặt
   trong CountingGardenBuilder) + HUD hiện tên khu.
2. `GardenZonePanel` (mới): style hồng như LanguageDialog (Rounded sprite +
   GraphicRaycaster + viền box), 2 nút "Vào chơi" / "Quay lại".
3. Cancel: DOUBLE-CLICK (2 click trong ~0.4s) vào vùng NGOÀI panel -> bỏ
   focus, camera về Follow (dùng anchor/follow offset của CountingGarden),
   ẩn panel. (User chốt double-click để tránh click nhầm.)
4. "Vào chơi": lazy-load sân trò chơi riêng bằng `WorldTransition.EnterMicroAsync`
   (đã có sẵn từ S2, xem P46B) — tạo `CountingPlayScene` theo đúng pattern
   `CountingGardenScene` (scene asset + builder + EntryPoint + anchors; đăng ký
   Build Settings + handler GameInstaller). Chưa có design trò chơi -> chỉ dựng
   hạ tầng + đưa demo hiện có vào, KHÔNG sáng tác gameplay mới. Exit ->
   ExitMicroAsync về Vườn Đếm.
5. Tests mới `CT-P50_GardenZoneFlow`: 5 spot tồn tại; click/proximity set
   focus; double-click cancel; panel 2 nút; play chỉ mở ở khu demo; tái dùng
   EnterMicro/ExitMicro + chống double-enter. Giữ suite xanh (hiện 582/577/0/5).
6. Verify: suite+build trên ASUS -> copy sang maynode -> foreground full-HD
   (scheduled task) -> đọc log; VISUAL chờ human review.

### C. QUY ƯỚC MÁY (đang hiệu lực)
- ASUS (máy này): batch/background — suite, build, log. Build ra
  `D:\Vscode\LangBuild\LWE.exe`.
- maynode = 100.124.132.59, user `Admin`, SSH key `%USERPROFILE%\.ssh\id_ed25519`
  (scp: `scp -i <key> -r D:\Vscode\LangBuild Admin@100.124.132.59:E:/LWW/`).
  CHỈ foreground: Scheduled Task LogonType Interactive, exe
  `E:\LWW\LangBuild\LWE.exe`, cờ chuẩn `-screen-width 1920 -screen-height 1080
  -screen-fullscreen 0`; MẶC ĐỊNH KHÔNG `-fullworld` (hub sạch, không NPC giữa
  sân — user order); `-fullworld` chỉ khi test quest W1.
- Repo máy nhà: `E:\LWW\learning-world` (Unity 6000.6.0f1, git sync main).

### D. CÒN TREO (các phiên sau)
- Journey click thật + audit UX/UI chuẩn mầm non 4-6 tuổi NỮ (target size,
  contrast, feedback, số bước, text) — user yêu cầu tỉ mỉ, tập trung đường đi
  chính/warp/lỗi ẩn quanh khu chơi (không cần quét cả bản đồ).
- 4 khu skeleton còn lại: chờ demo + design từ user (mỗi khu 1 scene riêng).
- Kiểm tra bấm Tiếng Việt: log kỳ vọng `[LanguageDialog] chosen=Vietnamese`.

## 48. S3-P2X — ZONE PICKER + PLAY ARENA (§47B, xong code + verify) (2026-09-23, máy ASUS)

- Flow chốt: sân chọn môn → Math Hub → Vườn Đếm (5 khu) → chọn 1 khu → sân
  trò chơi riêng. Đợt này chỉ khu 2 (demo theatre) có play; 4 khu skeleton giữ
  nguyên hình (không play).
- Code (không đụng Core/quest/save/camera contracts; 1 slot micro duy nhất):
  - `GardenZoneSpot` (mới): 5 door ở miệng khu — pad phẳng có collider cho
    ClickRouter (IClickTarget) + NavMeshModifier ignoreFromBuild (không bake);
    camera/look riêng từng khu; `playEnabled` chỉ khu 2. Focus = click HOẶC đi
    sát ≤2m (chống ping-pong: chỉ re-arm sau khi đi xa).
  - `GardenZonePanel` (mới, persistent trên bootstrap): card hồng kiểu
    LanguageDialog + GraphicRaycaster, 2 nút "Vào chơi"/"Quay lại"; nút play
    chỉ hiện ở khu có play. Sống sót qua swap scene.
  - `CountingGardenArea`: focus beat (HUD tên khu + frame camera, refresh
    4.5s), double-click ngoài panel (bỏ qua click vào spot) để cancel;
    EnterPlay/ExitPlayToGarden dùng ĐÚNG cặp ExitMicro→EnterMicro của slot
    micro (anti double-enter), play fail thì reload vườn + warp về entry
    (không bao giờ stranded/void), router tắt trong beat.
  - `CountingGardenBuilder`: `BuildDemoStageInto(parent, origin)` static
    (origin-translated) — MỘT layout lesson dùng cho cả khu 2 (scenery) và
    arena; `ZoneSpots` list.
  - `CountingPlayBuilder` + `CountingPlayScene.unity` (mới): island +180x,
    ground 38m, entry z-3 / exit z-11.5 (PlayExit → về Vườn Đếm), demo stage
    y hệt (origin = DemoStageOrigin), anchors đủ 8 slot, bake Children.
  - `CountingDemo`: overload `Build(CountingPlayBuilder,...)` + island offset
    (bỏ hardcode 120) → lesson chạy thật ở arena; wiring demo chuyển từ vườn
    sang arena trong GameInstaller (vườn còn lại stage tĩnh của khu 2).
  - `MicroWorldPortal.PlayExit`; DialogueLang thêm pair "Counting Playground"
    + 5 tên khu (chip ngôn ngữ relocalize được).
- Tests CT-P50 ×7: spots/collider/bake-ignore + 1 play đúng khu 2; focus +
  double-click (2 click ≤0.4s); proximity re-arm; panel 2 nút/raycaster;
  swap micro slot + anti double-enter + failure giữ slot trống; arena scene
  (island/cửa về/entry clear radius/J4/demo layout/anchors); area play seams
  (không play từ skeleton/khi ngoài, CanExit khoá trong arena).
  Suite **589/584/0/5** (baseline 582/577 + 7, 0 regression).
- Build production: **Succeeded errors=0 warnings=11** (toàn bộ warning cũ)
  size=110,010,038 (level4 = CountingPlayScene đã vào build).
- Headless wiring smoke (driver tạm, đã xoá): nav→Math (InSubject)→garden
  (spots=5)→focus 2 (panel play visible)→arena (demo actors + 5 balls, micro=
  CountingPlayScene)→exit play→garden (spots=5, warp về zone 2)→focus 3
  (không có nút play)→cancel→hub→Main (Idle, 2 scenes): **0 exception**.
- maynode foreground (full-HD, không -fullworld): copy `E:\LWW\LangBuild` bị
  LWE cũ khoá file → phải kill LWE + UnityCrashHandler + xoá folder rồi scp
  lại toàn bộ (bài học: scp khi file đích bị lock = build trộn DLL mới/exe cũ,
  luôn kill trước). Task `LWE-Foreground` full-HD: log
  `[Boot] screen=1920x1080 hud='Choose a gate!'` + FACE_OK + 0 exception.
- CHỜ MẮT USER (game đang chạy trên maynode): qua bảng chọn ngôn ngữ (nếu
  đang thấy card mic thì bấm qua) → cổng Toán → cổng Vườn Đếm → bấm/vào khu
  "Sân đếm" (pad vàng) → panel hồng → "Vào chơi" → xem lesson 2 NPC ở arena →
  đi ra disc Về → về vườn; thử double-click ngoài panel để bỏ focus; bấm khu
  khác (cà rốt/dâu/ngô/bí) phải KHÔNG có nút Vào chơi.
- Chưa commit (chờ lệnh).

## 49. S3-P2Y — VÒNG FEEDBACK §47C: RANH GIỚI / DEMO LUÔN CHẠY / PANEL SAU DEMO / MINI → FULL (2026-09-23, máy ASUS)

- Lệnh user (6 ý): (1) khu chơi chưa phân định ranh giới; (2) dù chưa chọn,
  demo vẫn phải chạy; (3) sắp xếp lại bố cục, chia ánh nhìn cho các khu khác
  (không để demo chiếm hết); (4) chỉ hiện panel Vào chơi/Quay lại SAU khi demo
  chơi thử chạy xong; (5) thu bé khu chơi trước khi chọn — chọn xong focus và
  mở rộng toàn màn hình như hiện tại; (6) animation tối đa cho demo ("nhìn vào
  là muốn chơi").
- Code:
  - Ranh giới: mỗi bed thêm `CGZoneNBorder` (đĩa viền gỗ tương phản dưới đất)
    + giữ fence ring; demo plot thêm `CGZone2Border` + fence ring MỚI ở lưng/hông
    (`CGZone2Fence2..8`, đặt ngoài dải walk crescent r8.8 nên không cắt đường
    đi), phía bắc vẫn mở làm miệng nhà hát.
  - Chia ánh nhìn: `GardenZoneVignette` (mới) gắn 4 bed — chuỗi bead pop 1..N
    theo nhịp + crop nhấp nhô nhẹ, chạy mãi (transform-only, không material,
    không gameplay).
  - Demo chạy trước khi chọn: `CountingDemo` ambient mini được wire LẠI vào
    vườn (installer), `CameraBeatsEnabled=false` (camera do focus khu điều
    khiển), actors/FX nằm TRONG mini root nên bay nhảy theo tỉ lệ.
  - Mini → full: `DemoMiniScale = 0.62` + pivot compensation
    `P = C*(1-s)` ⇒ mọi toạ độ authored (bóng, NPC, cam A/B/C) vẫn đúng chỗ;
    camera focus khu 2 kéo sát (0.2,1.9,6.4)→(0,0.9,10.6); "Vào chơi" mở arena
    cỡ thật như cũ.
  - Panel sau demo thử: `FocusZone` khu có play → HUD "Xem nhé!" (pair mới
    "Watch!") + `RestartLesson()`; `TickDemoGate` chỉ mở panel khi
    `demo.LoopCount` vượt mốc lúc focus (loop ~35s); khu skeleton vẫn panel
    ngay (chỉ Quay lại). Cancel/enter play tự huỷ gate.
  - Animation (zero-dep, `DemoJuice.cs` mới): confetti burst + confetti rain,
    sparkle shard, spotlight pool đập nhịp dưới sàn; demo thêm squash & stretch
    khi hop, basket pop khi bóng vào, sparkle lúc nhặt/nhận bóng, **shot C**
    (`CGDemoCamResult`) close-up "2 + tick" lúc confirm, reset về shot A.
- Tests: CT-P50H mới (borders/fence/vignette/bead-crop counts, mini pivot +
  scale + camera trong mini, demo chạy không cần player, gate: focus→awaiting→
  panel sau loop, skeleton panel ngay). P48B re-pin ngưỡng body theo tỉ lệ
  mini (có chủ đích). Suite **590/585/0/5**.
- Build production **Succeeded errors=0 warnings=4** size=110,019,046.
  Headless driver smoke (tạm, đã xoá): ambient loop=1 exc=0; focus khu 2 →
  awaiting=True panelOpen=False; loop xong (30s) → panel mở + play button;
  exit clean **0 exception**.
- maynode: kill LWE + xoá `E:\LWW\LangBuild` rồi scp lại (bài học file bị
  khoá), task `LWE-Foreground` full-HD, PID mới, log FACE_OK 0 exception.
- CHỜ MẮT USER: nhìn vườn — mini lesson chạy liên tục trong khu Sân đếm (bé
  như đồ chơi), 4 bed có bead nhấp nháy + cây cử động, mỗi khu có viền/fence
  rõ; bấm khu Sân đếm → "Xem nhé!" → xem hết bài (~35s) → panel hồng mới hiện
  → "Vào chơi" → arena cỡ thật (2 NPC + confetti + shot cận cảnh "2 tick");
  double-click ngoài panel để bỏ focus; khu skeleton không có nút Vào chơi.
- Chưa commit (chờ lệnh).

## 50. S3-P2Z — JOURNEY CLICK THẬT TRÊN MAYNODE + 7 BUG THẬT ĐƯỢC FIX (2026-09-23)

- User: "camera đang fail" + lệnh chạy journey click thật ở MÁY NHÀ (đã cài
  Tailscale; rule §45/§47C giữ nguyên: ASUS = batch, maynode = foreground).
  Tailscale check: ASUS 100.102.186.96, maynode 100.124.132.59 (SSH key
  `%USERPROFILE%\.ssh\id_ed25519`, user Admin, session console).
- Tooling tạm (đã xoá sạch sau round): `TempP2YJourney` (RuntimeInitializeOnLoad
  inject chuột THẬT qua Input System: `MouseState.WithButton` + giữ press 5
  frame + WarpCursorPosition; screenshot vào persistentDataPath/p2yj-shots;
  double-click thật; không teleport/không gọi state) + `TempBuildP2YJ/Prod`.
  Chạy qua Scheduled Task Interactive full-HD 1920x1080 trên maynode.
- **7 bug thật do journey khui + fix**:
  1. **Camera fail (ảnh user)**: anchor camera focus của khu là CON của pad
     (cylinder scale 2,0.03,2) → thừa hưởng scale → camera ở y≈0.1 nhìn ra rìa
     vườn. Fix: anchor đặt trên garden root (không scale) — ảnh sau fix đẹp.
  2. **Panel Play chết**: `CountingGardenArea.BindPanel` chỉ gán 1 chiều
     (area._panel) mà không gán ngược panel._area → nút Play không làm gì.
     Fix: bind 2 chiều + self-heal FindAnyObjectByType + dev-log
     `[GardenZonePanel] play pressed`.
  3. **Bảng chọn ngôn ngữ KHÔNG BAO GIỜ hiện**: `SystemDialogBusy` kiểm tra
     component tồn tại (`FindObjectOfType != null`) trong khi các dialog được
     tạo từ boot với panel ẩn → busy vĩnh viễn. Fix: dùng `IsShowing` của từng
     dialog (4 dialog đều đã có sẵn property).
  4. **Card demo giữ camera vô hạn**: đứng trong bán kính 5m là card re-issue
     mãi → lối về nằm sau camera, trẻ không bấm được. Fix: budget 3 re-issue +
     explicit `Follow` hand-back (log `beat hold finished`). Pin P48E.
  5. **HUD pill nuốt click đáy màn hình** (hub đặt pill bottom-center, panel
     Image raycastTarget=true) → trẻ bấm cỏ dưới chân không đi. Fix:
     panel + text `raycastTarget=false` (journey walk-north mới chạy được).
  6. **Về từ arena bị auto-focus lại khu** (đứng ngay spot → proximity focus
     → camera kẹt). Fix: `_proximityArmed=false` sau khi hạ cánh.
  7. **Spotlight sân khấu magenta**: `DemoJuice.AttachSpotlight` tạo primitive
     không gán material → default shader built-in không hợp URP → đĩa magenta
     (thấy rõ trong ảnh journey). Fix: gán `DemoJuice.Lit` màu vàng nhạt.
- Journey cuối (real clicks, maynode, 0 exception): language EN → cổng Toán →
  cổng Vườn Đếm → bấm khu Sân đếm → "Watch!" → hết bài → panel → Play → arena
  → lesson card (beats=1) → card nhả camera → walk north 5 bước → cổng về
  arena → về vườn (Follow, không auto-focus) → walk north → cổng về vườn →
  Math hub. Ảnh: `Temp/opencode/p2yj-shots/01..09*.png`, log
  `E:\LWW\P2YJBuild\p2yj-journey.log` (bản driver) + ảnh 04/05/07 đã soi.
- Verify chốt: EditMode **591/586/0/5** (P48E mới pin card release; P48B re-pin
  mini) → build production **Succeeded errors=0 warnings=4** size=110,022,118
  → copy maynode + chạy foreground full-HD (PID mới, log
  `[LanguageDialog] waiting; busy=MicSetupDialog` = card mic đang hiện đúng
  thiết kế; bấm Skip là ra bảng chọn ngôn ngữ — journey đã chứng minh).
- Chưa commit (chờ lệnh).

## 51. S3-P2Z2 — CỔNG VƯỜN ĐẾM KHÔNG WARP (user report) (2026-09-23)

- User: "đi vào cổng vườn đếm sao nó không warp sang sân chơi?"
- Chẩn đoán: log phiên user (maynode) chỉ có `Entered MathScene`, KHÔNG có
  `[CountingGarden] entered scene` → cổng chưa fire. Diagnostic click thật
  (temp, đã xoá) trên maynode: click tâm cổng → player đi từ (60,0,0) →
  (52.9,-2.2) → garden=True (fire đúng khi đi từ phía HUB).
  → Kết luận: trigger cũ nằm ở MIỆNG cổng (EntryAnchor, 1.8m phía hub so với
  tâm vòm, bán kính 1.3m); nếu trẻ đứng TRONG vòm rồi đi xuyên ra (hoặc dừng
  cách miệng ~1.4m) thì đường đi không cắt trigger → không warp.
- Fix: `MathWorldBuilder.BuildMicroWorldGates` — portal dời về **0.7m phía hub
  tính từ TÂM cổng** + `fireRadius 1.8` (phủ trọn miệng + lòng vòm + 1 bước
  phía sau), đĩa threshold to lên 3.0. Trẻ đi vào cổng từ BẤT KỲ hướng nào
  cũng fire.
- Re-pin có chủ đích: CT-P46A giờ pin portal nằm trong lòng vòm (cách tâm
  <1.0m) + miệng/tâm đều nằm trong bán kính; P46F vẫn xanh (landing hub cách
  portal 3.35 > 1.8+0.8).
- Verify: EditMode **591/586/0/5** → build production **Succeeded errors=0
  warnings=4** size=110,022,118 → copy maynode + chạy foreground full-HD
  (PID 14652). Temp diag tooling xoá sạch.
- Lưu ý flow cho user: cổng Vườn Đếm → warp vào VƯỜN (5 khu); muốn sang sân
  trò chơi: bấm khu "Sân đếm" (pad vàng) → xem hết bài demo (~35s) → panel
  hồng hiện → "Vào chơi" → warp sang arena.
- Chưa commit (chờ lệnh).

## 52. S3-P2Z3 — ARENA TRỐNG, CHỜ GAME USER THIẾT KẾ (2026-09-23)

- User (kèm ảnh mini demo): "chưa hài lòng với demo như thế này. Xóa hết demo
  npc ở trong arena đi, trong đó chỉ có trò chơi thôi. Trò chơi đó tôi sẽ
  thiết kế và gửi bạn sau." → hỏi lại phạm vi: user chốt **CHỈ ARENA** (vườn
  giữ nguyên mini demo + cơ chế chờ demo xong mới hiện panel).
- Code:
  - `CountingPlayBuilder`: bỏ hẳn sân khấu demo (không còn
    `BuildDemoStageInto`, không `Demo`/`DemoStageCenter`); anchors dời về
    giữa sân trống (GameplayFocus (0,0,5), Npc (0,0,3), Prompt/Feedback quanh
    đó, CameraLook (0,1,5)) làm chỗ chờ game mới. Hạ tầng giữ nguyên: ground,
    paths, entry arch/threshold, exit portal PlayExit, fences, cây/hoa/bướm.
  - `GameInstaller.BuildCountingPlayScene`: bỏ wiring CountingDemo; log
    "(lazy, empty game field)".
  - `CountingDemo`: bỏ overload Build(CountingPlayBuilder,...) — demo chỉ còn
    ở vườn (miniature).
  - `CountingGardenArea` không đổi (SetPlay nhận anchors mới).
- Tests: CT-P50F viết lại — pin arena TRỐNG (không CountingDemo, không prop
  `CGDemo*`, không spotlight) + hạ tầng/anchors còn nguyên + focus giữa sân.
  Suite **591/586/0/5**.
- Build production **Succeeded errors=0** size=110,021,606 (nhỏ hơn ~500B do
  bỏ sân khấu) → copy maynode + chạy foreground full-HD (PID 19648).
- Flow giữ nguyên: vườn = picker + mini demo + try-run gate; "Vào chơi" →
  arena TRỐNG (chờ game design của user).
- Chưa commit (chờ lệnh).

## 53. S3-P2Z5 — REFERENCE GAMEPLAY "ĐƯA ĐÚNG SỐ LƯỢNG VÀO RỔ" (2026-09-23)

- User brief 38 mục (Lead Gameplay + Experience Designer + Tech Director):
  xây REFERENCE GAMEPLAY đầu tiên cho Micro-World, experience-first, reuse Core,
  journey click thật, không tự tuyên bố visual PASS, commit cuối task.
- Deliverables: `docs/COUNTING_GAME_BLUEPRINT.md` (experience/state/acting/
  animation/spatial/GitHub research ADOPT-ADAPT-REJECT/test matrix/DoD) +
  `docs/COUNTING_GAME_REPORT.md` (12 mục) + shots A-H/P1-P9 (`D:\Vscode\
  p2z5-final\`).
- Code (không manager/singleton/service mới):
  - Arena staging: bảng "2" to + EMISSIVE (đọc rõ mọi góc), cụm 5 bóng tự nhiên
    (không hàng thẳng), rổ cách cụm 2.4m, count display pips xám→vàng, result
    board "2+tick" ngoài sightline spawn, 3 shot camera intro A/B/C giữ khung.
  - `CountingDemo`: layout thành DATA (DemoRefs) + gesture point/nod + intro
    chạy MỘT lần (`LoopForever=false`) → `OnIntroCompleted` → Observing;
    `SkipToObserving` cho re-entry; camera intro re-issue 2.4s.
  - `CountingGame.cs`: `CountingBall` (grounded→carried→basket, arc lên tay +
    follow mượt, bounce, không snap), `BasketZone` (click + proximity),
    `CountingGame` (state, đếm 1→2 với pips + câu cô, wrong path 3 bóng: cô
    đếm 1-2-3 → "bảng ghi số hai" → bóng dư bay về, success + confetti +
    celebrate, re-entry adopt Completed qua ActivityLifecycle của area).
- Bug thật khui bởi journey (đã fix): SmartCamera beat re-issue không bao giờ
  trả camera về Follow (`_hasReturn` bị clear) — sửa Core; collider bị Destroy
  deferred nên check null lúc build tưởng còn → bóng/rổ KHÔNG click được —
  thêm collider vô điều kiện; board kết quả chắn lối spawn; bảng số mờ/xa +
  sọc z-fighting; pips nổi như bóng trên bảng; bóng nằm trên miệng rổ.
- Verify: EditMode **597/592/0/5** (CT-P51 ×6 + re-pin P50F/P46A) → build
  production **Succeeded errors=0** → journey click thật trên maynode PASS cả
  loop (nhặt 2 bóng → rổ → đếm → bóng thứ 3 → cô đếm lại → completed → spam
  an toàn → rời arena → re-entry adopt Completed count=2), **0 exception**.
- Đã COMMIT theo lệnh user: **f6babf8** (gồm toàn bộ việc chưa commit của các
  vòng trước: LanguageDialog, quy ước biển, sân hồng, micro-world, gameplay).
- Game production đang chạy trên maynode (PID 24148) cho user chơi thử; visual
  PASS vẫn chờ mắt user (report ghi rõ TECHNICAL PASS / VISUAL REVIEW
  REQUIRED + known limitations).
## 55. MERGE + PUSH REFERENCE GAMEPLAY (2026-09-23)

- User lệnh push. Remote có `e4afc16` (S3-P2L2 audience gate từ máy nhà) chạm
  cùng CountingDemo → rebase + hợp nhất 2 hành vi:
  * Audience gate giữ nguyên (1 lượt/khán giả, rời cắt tiếng + reset sân,
    re-arm khi đi xa) — hợp với layout DATA (`_islandOffset`, `_refs`).
  * Thêm `AudienceGateEnabled` (game tắt gate sau handover/adopt → không dạy
    lại, không cướp camera khi trẻ chơi); `SkipToObserving` set `_engaged` để
    tableau hoàn thành vẫn thở; FocusZone không replay cưỡng bức (PassDone →
    mở panel ngay).
  * Test P51D/F dời "khán giả" vào bán kính arena; CT-P50H cấp viewer ở
    viewing spot theo contract mới.
- Verify hợp nhất: EditMode **600/595/0/5** (3 test audience-gate của máy nhà
  + 6 test P51 + toàn bộ cũ, 0 regression) → build production **Succeeded
  errors=0** size=110,040,182 → deploy maynode (PID 16228).
- Push: `e4afc16..c5fdc5e main -> main` (4 commit: audience gate, gameplay,
  HANDOFF §53, test fix).
- Chưa làm: journey lại trên bản hợp nhất (audience gate đổi timing vườn:
  bài chỉ chạy khi trẻ đứng viewing spot — cần user chơi xác nhận).

## 54. S3-P2L2 — DEMO AUDIENCE GATE (từ máy nhà, commit e4afc16) (2026-09-23)

- Lỗi user báo: vào Counting Garden là demo tự diễn + TỰ NÓI, và lặp đi lặp lại
  tiếng mãi. Yêu cầu: phát 1 lần; nếu người chơi không chọn (không đứng xem)
  thì tắt âm thanh + trả sân về trạng thái sân chơi.
- Fix (CountingDemo): thêm AUDIENCE GATE —
  * Không có khán giả (ngoài vùng xem): KHÔNG diễn, KHÔNG nói, sân idle
    (bóng bob nhẹ, 2 cô trò đứng thở) => vườn là sân chơi.
  * Trẻ đi vào vùng xem (bán kính 5m quanh viewing spot) => bắt đầu ĐÚNG 1
    lượt từ đầu (BeginLesson: reset sạch + focus Learning).
  * Hết lượt => `_passDone`: giữ nguyên trạng thái cuối, KHÔNG tự lặp; chỉ
    chạy lại khi trẻ đi ra khỏi vùng (re-arm) rồi quay lại.
  * Trẻ rời vùng giữa chừng => AbortLesson: SetAudioFocus(Muted) để CẮT tiếng
    ngay (Director StopAll) rồi trả focus Learning, reset sân về idle (bóng về
    sân, result ẩn, 2 cô trò về chỗ) — hết "nói với phòng trống".
  * Speak() cũng chặn theo `_engaged` (belt & braces).
  * Gate tách khỏi camera: audience chạy cả khi không có SmartCamera (test
    không cần camera), beat camera vẫn như cũ khi ở trong vùng.
- Pin mới CT-P50 ×3: không khán giả = 0 câu nói + idle; 1 lượt rồi im (đứng
  thêm 60s không lặp, không nói thêm); rời giữa chừng = Focus Muted + reset
  sân (bóng về home) + quay lại thì chạy lại. CT-P48C/P49C cập nhật theo
  contract mới (test cần "khán giả" đứng ở viewing spot).
- Verify: full EditMode **585/580/0/5**; build production **Succeeded**; boot
  **FACE_OK 0 exception**. Build review: `Temp/opencode/s3p2l2-Build/LWE.exe`.
- Commit + push kèm phase này (sau khi user hỏi "bản này đã mới nhất chưa" —
  lúc đó 2 file fix còn dở trong worktree, GitHub vẫn ở e808d24).

## 56. S3-P2Z6 - DOOR GATE + SPEECH PACING + CHOOSER FIX (user round, 2026-09-23)

- Lệnh user: (1) demo trong sân "không có cổng, đến giữa sân là tự chọn" — lùi
  auto về sát cửa; (2) âm thanh phải NGẮT NGHỈ, đừng nói một mạch; (3) chạy
  journey; (4) trả lời arena đã chơi được chưa.
- (1) CỔNG + TRIGGER Ở CỬA (garden):
  * Đo được: `DemoViewRadius 5m` quanh viewing spot (0,2.6) => demo tự chạy từ
    z=-2.4 (giữa sân). Fix: bán kính THEO TỪNG SITE — garden 1.4m (bé đứng sát
    ngưỡng cửa mới chạy), arena giữ rộng (bé spawn ngay cửa, intro chào liền).
  * Dựng cổng thật `CGDemoDoorGate` (2 trụ + xà + chỏm vàng + chùm hoa) ngay
    tại viewing spot; xà bake-ignored/collider-free (bài học headroom).
  * Journey xác nhận: "lesson start (audience arrived)" khi player z=1.4 (trước
    đây -2.4) => đúng "sát cửa".
- (2) NHỊP THOẠI: AudioDirector queue cùng priority => các câu P2 đọc liền một
  mạch. Thêm PACER trong CountingDemo: câu kế chỉ submit khi câu trước PHÁT
  XONG (Task của SpeakAsync) + nghỉ `SpeechGapSeconds 0.65s`; câu đến sớm vào
  slot pending (mới nhất thắng, không lag xa hình). Nhịp wrong-path giãn lại
  (0.4/2.0/3.7/5.4/7.2/9.0, kết 10.8s) để mỗi câu đếm đủ chỗ. Journey log
  "say 'Look at the board!'" -> "say 'This is number two.'" ... có khoảng nghỉ
  rõ (trước đây đọc chồng/liền).
- (3) JOURNEY THẬT (driver click chuột Input System, xoá sau khi xong):
  Math gate -> counting portal -> walk to door (demo bắt đầu z=1.4) -> try-run
  1 vòng -> panel -> click "Vào chơi" -> ARENA -> intro tại spawn -> FreePlay ->
  click bóng 1 -> mang tới giỏ (count 1) -> click bóng 2 -> count 2 ->
  phase=Success pips=2 (ảnh `a5_result`: 2 bóng trong giỏ + bảng 2 tick + cô
  trò celebrate) -> đi bộ ra cổng exit -> về garden (arena unloaded) => TRỌN
  VÒNG PASS bằng click thật.
- (3b) BUG THẬT DO JOURNEY KHAI: hộp chọn ngôn ngữ (pulled) NỔI GIỮA MÀN HÌNH
  MÃI cho tới khi bấm — nó đè panel khu và NUỐT nút "Vào chơi" => arena không
  vào được (ảnh `g3_lesson`). Fix: `LanguageDialog` tự đóng khi bé đi xa >3m
  khỏi điểm xuất hiện (Player wire trong MarketBootstrap) + test P49F. Lần
  journey sau: vào arena bình thường.
- (4) TRẢ LỜI: ARENA CHƠI ĐƯỢC — full loop với click thật (pick/carry/place,
  count + pip + result board, celebrate, exit về garden) ✓ 2 lần journey liên
  tiếp đều Success/count=2/pips=2.
- Verify: full EditMode **601/596/0/5** (+P49F); build production **Succeeded**;
  boot **FACE_OK 0 exception** (`s3p2z6prod-Build`). Temp tooling xóa sạch.
