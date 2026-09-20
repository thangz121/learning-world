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
- (Update: user bắt đúng — có tường VÔ HÌNH lớn chắn lối gạch → đi vòng. Xem §16.)
  Chưa push (chờ user gom lệnh).

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
