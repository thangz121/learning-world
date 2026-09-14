# HANDOFF — Visual Polish Milestone (session tiếp theo đọc file này trước)

Ngày: 2026-09-12 (pass storytelling-loop mới nhất). Project: `D:\Vscode\little-world-english` (Unity 6000.6.0f1, URP 17.0.3).

> LOCKED (2026-09-12): Golden Character Standard v1 + Story Interaction Loop
> (NPC speech → bubble → choice/distractor → StoryMoment → reactions/retry →
> camera focus/return) are frozen. Extend, don't duplicate/fork/bypass/silently
> modify. Deviation needs justification + migration plan + regression validation
> (EditMode + real player build) + approval.

## 1. Mục tiêu milestone
World coherence + nhân vật trông như character thật (không còn placeholder), trên **player build thật**.
Không làm: gender-selection UI, Phase-2 content, story redesign, gameplay systems mới.

## 2. Trạng thái hiện tại: GOLDEN STANDARD DONE ở mức evidence (2026-09-12)
- Chuẩn chi tiết: `docs/GOLDEN_CHARACTER_STANDARD.md` (template cho mọi character tương lai).
- Player spawn FACING CAMERA (`MarketBuilder.FacePlayerToCamera`); face anchor theo đúng hướng.
- Quest complete → Happy baseline DURABLE + Celebrate; Happy mouth ×1.45/×1.3 đọc ở 1.5m+.
- Compile 0 errors. EditMode **24/24 PASS** (chạy lại sau golden pass).
- Temp diag đã xóa sạch; `manifest.json` revert (không còn screencapture pin).
- Môi trường: đúng màu, Forward lighting + shadow ổn định, hết whiteout, hết `Default Renderer is missing` (~10k → 0).
- Player: humanoid Casual_Male (~1.3m), không còn capsule xanh. Idle/Walk qua `Moving` bool.
- Milo/Mia (~1.65m): mặt doll đọc được (pupil đen + glint trắng + miệng), blink, breathing, attention glance, expression theo event.
- Quest táo full loop qua path thật: seen → bring (`completed=True`) → HUD `Great job!` → hoa nở (`active=True`).
- Compile 0 errors. EditMode **24/24 PASS**.

## 3. File thay đổi (working tree, CHƯA commit — xem `git status`)
Giữ lại (production):
- `Assets/_SharedKernel/CharacterPresentation.cs` (mới) — API: `Set/PulseExpression`, `LookAt`, `BlinkNow`.
- `Assets/A_World/PlayerVisual.cs` + `A_World/Visuals/Resources/PlayerVisuals/` (mới).
- `Assets/ThirdParty/Quaternius/Player_CasualMale.fbx` (mới) + `README.md` provenance.
- `isReadable: 1` trong 3 `.fbx.meta` (Worker_M/F, Player) — BẮT BUỘC cho face measurement.
- `MiloPresenter.cs`, `MiaPresenter.cs`, `MarketBuilder.cs`, `SmartCamera.cs` (sửa visual-only).
- `Milo/MiaController.controller` (Idle loop + Victory + PickUp), `PlayerController` tương tự.
- `UniversalForwardPipeline/Renderer.asset` (mới) + `Graphics/QualitySettings` trỏ sang.
- `BootstrapScene.unity`: gỡ stale baked GI (`m_LightingDataAsset → {0}`, `m_BakeOnSceneLoad 0`).
Đã xóa hết temp diag (Editor clean, scene không còn GO TEMP).

## 4. Bài học xương máu (đừng lặp lại)
1. **Whiteout** = stale baked Enlighten data + Forward renderer (không phải material/light). Fix = de-bake scene.
2. **Face setup PHẢI chạy ở Update frame 2**, không phải Awake/Start (world transform đọc sớm bị stale-identity).
3. **Không raycast từ trong sọ ra** (PhysX cull backface) — đã chuyển sang bone+vertex math, không physics.
4. `SetParent(head, true)` đã giữ world pose — KHÔNG chia scale lần nữa (từng làm mặt teo 50×).
5. Batch `ScreenCapture` không bao giờ flush; `Camera.Render()` tay ra ảnh trắng. **Validate bằng player build thật.**
6. Player chạy background bị present-stall (nhìn như treo): phải `AppActivate` foreground. D3D12 chạy được; D3D11 gần như đứng hình trên máy này.
7. Player log bị buffer: mirror marker ra sidecar qua `Application.logMessageReceived` (code mẫu đã xóa cùng temp — viết lại khi cần).
8. Không poll blind 30s/vòng: xem `C:\Users\ASUS\AppData\Local\Temp\opencode\W1CapWatch.ps1` (còn giữ) — heartbeat + step + CPU delta + new-log-only.

## 5. Việc còn lại (không blocker)
- Blink/expression mới verify qua stills, chưa frame-by-frame.
- `Walk_Carry` chưa wire (đã có prop táo trong tay).
- `PregenSeeder seeded 0` trong player (mp3 có ship) → audio chạy fallback, cần fix packaging riêng.
- Temp diag (W1Val/W1Side/W1Build/W1Normals + FaceDiag*) đã xóa sau pass storytelling-loop
  (xem §8); recipe build bên dưới thay cho file W1Build đã xóa.

## 8. Pass storytelling-loop 2026-09-12 (eye depth + story presentation)
- Eye root cause (PROVEN bằng FaceDiag số + ray): global max ≈ +0.36 là hat/hair
  NGOÀI face window (không phải nose tip), cao hơn mặt ~0.12. Fix = eye-band p95
  (`CharacterPresentation.MeasureBandFront`), eyes+mouths SHARED pane (ray chứng minh
  mặt phẳng +0.24..0.25 từ eye line tới mouth line). Per-height mouth estimate đã thử
  và REVERT (throat/jaw verts kéo p95 ≈ −0.14 → chôn miệng Mia/Player).
- HUD: bỏ quiz text ("Find the apple!"/"Bring it to Mia!" → "Helping Mia…"/"Apple found!");
  speech (Milo) + WorldQuestionBubble own narrative. Quest logic untouched.
- Tests: CT-S01A..G mới (distractor/correct/routing/bubble/camera/fallback-face/real-mesh
  eye+mouth seat). EditMode **31/31 PASS** sau cleanup.
- Story loop reuse nguyên kiến trúc cũ (StoryMomentEvent, DistractorChoice,
  WorldQuestionBubble, SmartCamera.FocusOnFor) — không rebuild system nào.
- Evidence mới (post-fix, player build thật): `e-front/threeq/side/miaside/playerside/player34/`
  `gameplay/arm.png`, `w-mia/w-wrong/w-complete.png` + `prefix/` giữ shots pre-fix.
- Infra build (quan trọng): batch Unity là GUI app → PowerShell `&` KHÔNG block;
  dùng `Start-Process -Wait`. File-lock races Library/Bee khi kill dotnet worker:
  xóa `Library/Bee/TundraBuildState.state(.map)` để rebuild sạch. W1Build cũ có
  settle-sleep 90s trước BuildPlayer (tránh startup backend đè player backend);
  đã xóa cùng file — viết lại khi cần theo recipe §6 (thêm sleep + retry tương tự).

## 6. Recipe validate lại (khi cần)
- Build: Unity batch `-executeMethod W1Build.Run` (viết lại file temp ~20 dòng, xem git history nếu cần) → `Temp/opencode/PlayerBuild/`.
- Chạy: `LittleWorldEnglish.exe -w1val -screen-windowed 1` + AppActivate foreground → shots `Temp/opencode/v-*.png`.
- Driver temp mẫu: `W1Val.cs` (đã xóa; ý tưởng: establish → MoveTo → close-up → quest probe → topdown → Quit).
- EditMode: `-runTests -testPlatform EditMode` (không `-quit`).

## 7. Screenshot cuối (pass)
`C:\Users\ASUS\AppData\Local\Temp\opencode\v-establish/move/milo/mia/complete.png` — player human, Milo/Mia có pupil đen + glint, quest `Great job!` + hoa nở.

## 9. Pass grounding 2026-09-12 (tiếp nối sau mất điện giữa tour-run)
- Vấn đề: GndDiag2 báo Idle lún (Player −0.31, Milo −0.49 world); tour cũ đi sát cây làm agent leo NavMesh (rootY=0.83).
- Đo live bằng W1Tour probe (BakeMesh đúng pose đang render, tại spawn + stand):
  Milo/Mia root 0 / minVert −0.493 (lún tới gối); Player root 0.83 / minVert tương đối −0.317.
  Nguyên nhân root 0.83: `NavMeshAgent.baseOffset` đọc 1.000 trên fresh AddComponent ở Unity bản này.
- Fix (production, đã verify):
  - `PlayerVisual` / `MiloPresenter` / `MiaPresenter`: VisualRoot `localPosition.y` lift vĩnh viễn
    (Player 0.395, Milo/Mia 0.493 — **parent-space**, không phải chain tổng).
    Animator luôn play clip (rest bind pose không bao giờ render) nên 1 hằng số là đủ;
    `CharacterPresentation.SetupFace` capture base nên breathing/hop giữ nguyên.
  - `MarketBuilder.BuildPlayer`: pin `agent.baseOffset=0`, height 1.6, radius 0.4 theo capsule thật.
  - `MarketBuilder.BuildNavCarves` (mới): carve tĩnh quanh cây/quầy/thùng/bệ/hàng rào
    (bake All-geometry tạo đảo walkable trên nóc prop → agent leo cây/đứng rào).
- Verify (player build thật, `-w1tour`): GROUND spawn+stand Player root 0.03/minVert 0.036–0.038,
  Milo/Mia 0.000–0.010; quest loop nguyên vẹn (wrong=1, completed=True, `Great job!`); EditMode 34/34.
- Evidence: `C:\Users\ASUS\AppData\Local\Temp\opencode\t-spawn/t-discover/t-intro/t-question/`
  `t-wrong/t-celebrate/t-walk/t-standside/t-walkside.png` + `w1tour-live.log` (run cuối).
- Temp diag đã xóa sạch (W1Tour/W1Build/GndDiag/GndDiag2 + GO trong scene, SceneRoots 1 root).
  Recipe build lại: viết lại file temp ~20 dòng `W1Build.Run` (sleep settle 90s + `Start-Process -Wait`).
- Bài học mới:
  9. TeraBox sync khóa file Library/Temp/Bee giữa build (sharing-violation roulette) → kill
     TeraBoxUnite ngay trước build rồi build luôn (nó tự restart sau ~1–2 phút).
  10. Bee state poison sau khi kill worker: xóa `Library/Bee/TundraBuildState.state(.map)` rồi build lại.
  11. `localPosition` là parent-space: lift chia cho parent scale, không phải chain tổng (đã từng set gấp đôi).
  12. Tour route phải tránh cây/prop (walk target cũ (−5.5,3.5) sát tán cây + carve chưa tồn tại lúc đó).
- Follow-up (không blocker): `Walk_Carry` chưa wire, PregenSeeder audio fallback (từ §5).

## 10. Pass label-legibility 2026-09-12 (tiếp nối W1Audit — session này)
- Vấn đề (W1Audit build 3:49pm): (1) k-correct camera nằm TRONG mái che (chỉ thấy
  sọc awning) — hóa ra là STALE BUILD (code celebrate cũ); build 4:02pm đã chứa
  pose `FramePointFor((-0.8,2,1.2), miaHead, 3.2s)` và verify OK ở run 4:44pm
  (`campos=(-0.80,2.00,1.19)` settled, Mia celebrate tay giơ cao).
- (2) Name label đọc không được: 2 builds liên tiếp cho thấy text ngược
  ("oliM"/"siM", crop zoom bicubic 3-4x xác nhận đảo ORDER + shape chữ 'a';
  hexdump string sạch ASCII nên loại trừ bidi; Player.log 0 errors nên loại trừ
  null-font). Root cause kép:
  - Chiều cao 2.05m nằm trong mũ cứng Milo / tóc Mia → đầu che chữ ở góc thấp.
  - Billboard `LookRotation(cam - pos)` (+Z về camera) show MẶT SAU của uGUI
    world-space text (mặt đọc được là -Z) → mirror toàn bộ.
- Fix (production):
  - `MarketBootstrap`: label height 2.05 → **2.35m** (clear mũ/tóc ~0.3m).
  - `WorldNameLabel`: `BillboardRotation()` mới (+Z RA XA camera) + test
    EditMode `CT-S01M_NameLabelFacesCamera` (4 góc cam audit, dot > 0.99).
- Verify (build pbuild7 + run 5:02pm): EditMode **37/37 PASS**; player build
  Success errors=0; audit 12 beats COMPLETE (wrong=1, completed=True,
  `Great job!`, bubble ẩn); `m-label.png` đọc **"Milo"** crisp + khe hở trên mũ,
  `k-correct.png` đọc **"Mia"** trên Mia celebrate; Player.log 0 errors.
- Temp cleanup sau pass (theo protocol): xóa `W1Audit.cs`, `W1Build.cs`,
  GO `W1Audit(TEMP-DIAG)` trong scene. Recipe build lại giữ nguyên §6
  (viết lại `W1Build.Run` ~20 dòng + `Start-Process -Wait`).
- Bài học mới:
  - 13. Đọc shot cũ phải đối chiếu MTIME file source vs giờ build: shot 3:49pm
     chạy code pre-3:49 (fix 3:57-3:59 chưa vào) → suýt fix nhầm bug đã hết.
  - 14. World-space uGUI text: verify mặt đọc được bằng close-up, đừng tin
     convention +Z; đã có CT-S01M khóa contract.
- Follow-up (không blocker, giữ từ §5/§9): `Walk_Carry` chưa wire,
  PregenSeeder audio fallback.

## 11. Pass R4 2026-09-13 (Phase-1 final-polish: golden-template closure)
- Phạm vi: đối chiếu 16 shots R3 với checklist 23 mục của Phase-1 polish spec.
  Khóa thêm: objective chip (compact + whisper, CT-P03A/B), thought-bubble
  (shell/outline/tail/apple+stem+leaf, 0 collider, CT-P03C), giày 3 rig
  (CT-P03D/E), da smoothness 0.5/metallic 0, identity cam/coral/blue,
  stall gọn, staging, story speech-led, wrong close-up + correct celebrate +
  retry, MSAA 4x. R4 sửa 3 prod + 2 survey:
- (1) Player bay 7.6cm: GROUND minMapped=+0.076 (ảnh gnd-player thấy khe + bóng
  dưới giày; Milo 0.009/Mia 0.000 đã đạt) → `PlayerVisual.GroundLiftLocal`
  0.395 → **0.300** (trừ đúng float đo được, parent scale 0.8).
- (2) Celebrate bị Milo che: cam (−0.2,1.9,−0.2)→Mia đi xuyên Milo (đứng cách
  cam 1.9m vs Mia 4m, foreground khổng lồ) → pose mới **(−3.3,1.8,−0.2)**
  (nam Mia, cùng x): Milo + player rơi khỏi ray thành witness.
- (3) Path cháy trắng: (0.92,0.78,0.55) × sun 1.1 + ambient 0.75 → trắng bệch
  → **(0.76,0.60,0.40)** render đúng warm tan.
- (4) Survey: stall-W follow-cam nhìn đất trống → `StallView` authored
  (cam (−6.2,2,0.3) → counter); `FaceMacro` 1.4→1.6m + aim +0.12 (label lọt khung).
- Verify (build R4 payload tươi: LWE.World/Bootstrap.dll 10:45): EditMode
  **52/52 PASS**; survey16 COMPLETE (wrong=1, completed=True, `Great job!`,
  sightlines CLEAR); GROUND player/milo/mia **0.000/0.007/0.000**; celebrate
  Mia center-frame tay giơ cao (Milo off-ray); stall-W đủ Milo+Mia+label+stall+
  táo+player+path; giày player chạm đất; label Milo đọc crisp + khe hở trên mũ.
- Lockdown: xóa `P1Survey.cs(.meta)` → revert `LWE.Bootstrap.asmdef`
  (InputSystem ref chỉ phục vụ temp) → FINAL clean build **Succeeded errors=0**
  → boot check alive 50s+ **3×FACE_OK 0 exceptions** → xóa `P1Build.cs(.meta)` +
  `Assets/Editor(.meta)` → FINAL EditMode **52/52** trên cây khóa → commit.
- Bài học mới:
  - 15. Probe số quyết định, ảnh xác nhận: giảm lift ĐÚNG bằng minMapped đo
    được (0.095 local), không đoán; re-probe sau fix (0.000).
  - 16. Khi NPC đổi chỗ (Milo ra stall-front), mọi authored camera pose cũ
    phải audit lại sightline — khoảng cách tới cam quan trọng hơn khoảng cách
    tới ray.
  - 17. Màu material phải chấm dưới đúng ánh sáng build (sun+ambient), không
    chấm bằng giá trị RGB trên giấy.
- Watch items (non-blocking, giữ nguyên có lý do): chân gầy + facet phẳng +
  tay mitten = style flat-shade pack Quaternius (đồng nhất cả cast, giữ import
  normals); 1 TIMEOUT mode=5 lẻ ở step stall-W survey (shot vẫn đúng, COMPLETE);
  `Walk_Carry` chưa wire, PregenSeeder fallback (từ §5/§9).

## 12. Pass R5p 2026-09-13 (shoe seat: minima gated 0.8m + log mode) — VERDICT: FAIL, không lock
- Phạm vi: fix giày lơ lửng 15-36cm (R5l proof) bằng seat sole-relative gated +
  log mode; giữ nguyên face/lift/surface (R5d/k) và survey beats.
- Prod (working tree, CHƯA commit):
  - `CharacterPresentation`: `SoleSeatFor` seat = minima thấp nhất của foot-bone
    dominant (.L/.R theo tên giày) trong gate 0.8m quanh foot bone; fallback
    side → near → drop; log `SHOE_SEAT name mode seat foot verts minDist retry`
    + `SHOE_DEFER` khi mesh chưa settle (retry tới 180 frames, force cho tests).
  - Toán mapping là bone-bind (`bones[dom]*bindpose*bindVert`, như face kit đã
    chứng minh), CẤM BakeMesh+renderer.localToWorld ở prod seat.
  - `R5Survey` (temp): mirror thêm `SHOE_SEAT/SHOE_DEFER` vào r5survey-live.log;
    giữ nguyên beats (low/shin/front-low/stance-servo/magenta/blendshape).
- Gate (build39 + survey37, 2026-09-13): EditMode **52/52 PASS** (editmode31/32/33);
  build **Succeeded errors=0** (build37/38/39); survey **COMPLETE** (wrong=1,
  completed=True, `Great job!`, Player.log 0 exceptions).
- Số (survey37, build39 payload tươi):
  - Seat: **6/6 mode=side, retry=0, minDist 0.016-0.021m** (survey32 cũ: 6/6 drop,
    minDist 3.1-4.9m). Gate engage ngay frame-2, không defer.
  - Shoe world (Renderer.bounds render thật, lossyScale đúng 0.13/0.10/0.26):
    player idle y=0.318, walk-end y=0.317-0.319; Milo y=0.528; Mia y=0.530.
  - GapProbe (mapping cũ, chỉ tham khảo): player idle gap=-0.032 / walk -0.036
    (sunk), Milo +0.002, Mia +0.004; MINV bone=Foot.R cả 3 rig.
  - Stance servo (mode=12): **TIMEOUT** lần 4 liên tiếp (survey32/34/35/37).
- Ảnh (survey37): low-player PASS (giày trên path, chân chạm cap); shin/front-low
  CẢI THIỆN (cap đã chạm/overlap đầu chân thay vì blob rời như survey34) nhưng
  vẫn daylight + bóng dưới giày ở điểm grass (walk-end/shin); walk-stance float
  ~20-30cm; low-milo/mia: Milo/Mia ngồi/đứng trên quầy stall (staging, không phải
  defect grounding) nhưng probe raycast Ground 0 gây hiểu lầm.
- Root cause mới (PROVEN bằng số, bài học 18-20):
  - 18. BakeMesh+localToWorld DOUBLE-SCALE qua Body 40-50x (diag survey36:
    bounds 20m rộng × 52-85m cao trong khi render thật 1.3-1.65m). Mọi
    minMapped/gap từ R4 trở đi chỉ đúng tình cờ ở min-Y, không nhạy pose
    (idle vs walk chỉ lệch 4mm mapped), và XZ offset bị phóng ~40x nên gate
    0.8m reject toàn bộ (survey32: 6/6 drop). R5o "verts 4m out" thực chất là
    bind-pose ở origin trong lúc root đã ở spawn (player 4.5m/Mia 4.3m/Milo 2.1m).
  - 19. Seat frame-2 đo offset ở pose chưa steady: sole(bind, frame-2) cách foot
    2cm nhưng sole(idle) cách foot ~10-20cm+ (clip sink skeleton ~0.49m không
    rigid giữa bone và mesh) → cap chạm đầu chân nhưng cả cụm vẫn float ~27cm
    trên grass. Seat phải đo ở Idle steady (defer tới Animator ổn định), không
    phải frame-2.
  - 20. Stance-servo TIMEOUT là artifact timing script (servo start khi walk chỉ
    còn ~1s, arrival → velocity=0 → false vĩnh viễn), không phải bằng chứng
    không-stance. Muốn verdict stance phải trigger servo SỚM (cùng lúc walk-a)
    hoặc widen window.
- VERDICT: **FAIL — không lock, không commit, không xóa temp** (`R5Survey`,
  `R5Build`, `Assets/Editor`, InputSystem ref giữ lại cho vòng sau). R5p-seat
  (gated minima + log) coi như DONE ở mức engage (6/6 side); vòng sau: (a) seat
  ở Idle-steady + (b) viết lại GapProbe/GroundProbe bằng bone-bind math + (c) tune
  lại lift trên số đúng + (d) sửa stance-servo timing; chỉ PASS khi shin/front-low
  hết daylight, stance COMPLETE không TIMEOUT, gap<=0.01 trên ảnh.
- Follow-up giữ nguyên (từ §5/§9/§11): `Walk_Carry` chưa wire, PregenSeeder
  audio fallback.

## 13. Pass R5V 2026-09-13 (world-presentation polish, 4 build→play→photo cycles) — VERDICT: FAIL, không lock
- Phạm vi: R5 grounding/face/debug + R5V-1 (ground/lighting/shadow/boundary) +
  R5V-2 (Milo/Mia composition, apple/distractor, zones) +   R5V-3 (stall/HUD/
  celebrate/Mia-identity). Không đụng quest/audio/NavMesh/architecture.
- Session mở đầu: phát hiện cây working-tree KHÔNG compile
  (`MarketBuilder.cs` thiếu `}` đóng `BuildTree` → `BuildBush`/`BuildFence` nest
  trái phép, CS1513). Fix 1 brace. EditMode **52/52 PASS** sau fix (không chạy
  lại sau các batch R5V-b/c vì không đụng API nào có test cover; compile được
  chứng minh bởi 4/4 player build).
- Chuỗi evidence (build Succeeded errors=0 + payload DLL fresh + play COMPLETE):
  - B1 (15:52Z? 15:55 local): tree R5V-a (R5V-1/2 edits có sẵn). Play COMPLETE.
  - B2 (16:17): R5V-b (stall-north, celebrate-cam→east, sun 68°/shadow 0.65,
    MiloMat, survey framing+diag). Play COMPLETE.
  - B3 (16:24): R5V-c (Mia hat hồng + celebrate ensemble). Play COMPLETE.
  - B4 (16:31): survey settle tweak (complete 2.8s). Play COMPLETE.
  - Mọi run: wrong=1, completed=True, `Great job!`, Player.log 0 exceptions.
- Web research (fresh, §4): Unity URP Shadows docs (shadowStrength/bias/normal-
  bias/shadow-distance = DIRECT params; chọn strength 0.65 + sun 68° = DERIVED);
  Roblox Creator docs — onboarding bằng visual (không chữ), younger users thích
  explore > compete, visual language nhất quán, đừng chỉ dựa vào màu sắc,
  proximity prompts, legibility/contrast (DIRECT principles); Haigh-Hutchinson
  GDC camera (tránh occlusion, minimize motion, smooth transitions = DIRECT);
  GameDeveloper third-person composition (centered = central meaning).
  Quy ước ghi: DIRECT (nguồn nói) / DERIVED (suy từ scale 1.65m NPC của project)
  / VISUAL DESIGN DECISION. "3.6m/1.8m/15°" là DERIVED, không phải luật chung.
- Screenshot verdicts (mỗi shot đọc trực tiếp, format SHOULD/ACTUAL/PASS):
  - spawn (first impression): PASS-leaning. Path/mat/fence/stall/crate/ball/
    trees/bushes/outer-green phân lớp rõ; Milo center trên mat xanh, Mia ở stall,
    táo đỏ vs bóng xanh tách hẳn. Trừ: Milo label bị crop trên khung hình spawn.
  - low-player/shin/spawn-crop-3x: PLAYER IDLE GROUNDED ×3 vị trí (sole chạm
    path/cỏ, không daylight gap). PASS.
  - low-mia run-4 (macro nam, counter không chắn): MIA FLOAT ~0.25-0.35m
    (giày treo ngang thân counter, dưới là mặt counter không phải cỏ). FAIL.
  - Milo: cùng họ rig Worker + cùng lift 0.493 + parallax "ngồi lên counter"
    lặp lại → float cùng họ (chưa có macro sạch như Mia). FAIL (inference).
  - walk-a/stance: mid-stride flight là bình thường; stance-servo mode-12 đã
    FIRE 1 lần (run-2/3) nhưng framing 3/4 + bóng ngang làm gap +/- vài cm
    không đọc chắc. Walk = INCONCLUSIVE (cần side-view stride series).
  - faces (front macros cả 2 NPC + 4m): PASS. Không white ellipse plane, không
    extra geometry; vết shading nhạt = sculpt gốc (đọc như brow/blush búp bê).
    Side macro void (NPC xoay mặt về camera) nhưng chứng minh mắt gắn chặt.
  - skeleton/debug: PASS (TREE dump sạch PoleTarget/label nodes, ảnh không có
    white-T/helper/geometry lạ).
  - talk/wrong/found/face4m: PASS (Milo close-up đẹp, Mia sad đọc được, táo vs
    bóng tách 3.22m + đỏ vs xanh + crate vs pedestal, labels crisp).
  - Mia staging: stall-north (-0.9→-1.4, counter front -3.4, Mia clear 0.9m)
    giảm hẳn "ngồi lên counter" ở wrong/run-2 (đứng trước stall tự nhiên). PASS-leaning.
  - celebrate: run-1 (player che mặt Mia) → run-3 ensemble (hết che nhưng dính
    turn-lag: lưng đầu) → run-4 settle 2.8s (lố window 3.2s, rơi về follow).
    Ensemble là composition đúng; mặt Mia ở beat chưa lần nào đọc to. BORDERLINE.
  - Mia hat hồng (MAT census chứng minh submesh `Hat` tồn tại cả 2 rig):
    identity WIN (vàng/cam vs hồng/coral). PASS.
  - MiloMat xanh (no-collider, post-NavMesh): "Milo's place" đọc ngay ở spawn. PASS.
  - label gating: LABEL diag `milo=True mia=null` (Find chỉ thấy active) +
    TREE sau đó cả 2 active → Mia ẩn pre-talk đúng. "Mia pill ở spawn run-1" là
    đọc nhầm (run-2/3/4 không còn). PASS.
  - HUD: chip compact + adaptive fade đã yield trong beats; overlap còn lại chỉ
    là chip mờ sau label (chấp nhận được). PASS.
  - lighting/shadow (sun 68°, strength 0.65, ambient 0.68/0.71/0.75, env matte):
    mặt đọc được, hết cháy path; sọc fence + bóng stall còn lớn nhưng đỡ.
    BORDERLINE-PASS.
  - world depth/layers/island: PASS (FG path-mat / MG char-stall / BG fence-
    trees-outer; boundary rõ).
  - liveliness: PARTIAL (blink/breath/glance/hop tồn tại trong code; session này
    không có proof chuyển động; ambient motion vắng) → NOT PROVEN cho lock.
- Bài học mới:
  - 21. Mọi đo CPU-side đều đã thua ảnh 3 lần: BakeMesh+localToWorld (40-50x),
    GapProbe (0.000 trong khi ảnh float), SOLE2 bone-bind (off ≈ lift một cách
    đáng ngờ). Từ nay grounding = ảnh + contact shadow; số chỉ để tham khảo.
  - 22. "Mia/Milo ngồi lên counter" mà các pass trước gọi là STAGING chính là
    FLOAT ~0.3-0.5m bị đọc nhầm (lift 0.493 từ thời minMapped rác; TRUE Worker
    idle sink ≈ 0). High-angle/small-in-frame che giấu lỗi decimeter.
  - 23. NPC quay mặt về camera làm void side-macro (phải khóa rotation khi audit
    mắt), nhưng vô tình chứng minh mắt gắn chặt.
  - 24. Celebrate beat window (3.2s) vs settle: shot 1.2s dính transition,
    2.8s lố window. Beat sau này cần dài hơn (5s) nếu muốn ảnh settled.
  - 25. `Wait()` đừng fix cứng settle: đã thêm tham số `settleAfter` (temp).
- Matrix tổng (lock gate): player-idle PASS; Milo/Mia-idle FAIL; walk
  INCONCLUSIVE; shoes/face/debug PASS; human-quality YES×3; spacing số PASS +
  composition BORDERLINE; greet/stop-distance NOT PROVEN số (chưa chạy test
  hành vi; quest clicks thật vẫn PASS chức năng); distractor PASS; wrong PASS;
  celebrate BORDERLINE; camera PHƯƠNG preserved PASS; stall/HUD PASS; env/
  island/depth PASS; lighting/shadow BORDERLINE-PASS; liveliness NOT PROVEN;
  first-impression PASS-leaning.
- VERDICT: **FAIL — không lock, không commit, không xóa temp** (`R5Survey`,
  `R5Build`, `Assets/Editor`, InputSystem ref giữ cho vòng sau).
- NEXT (1 fix trọng tâm, đã chín bằng 2 dòng evidence độc lập — ảnh float ≈
  lift + SOLE2 off ≈ lift ⇒ TRUE Worker sink ≈ 0): Milo/Mia
  `0.493 → ~0.05` (candidate, revert sẵn) + macro nam sạch + ảnh quyết định.
  Sau đó: Mia-happy close-up hướng Tây, ambient-motion pass, rồi mới R6/R7.
  CẤM Phase 2.
- Follow-up giữ nguyên: `Walk_Carry` chưa wire, PregenSeeder fallback.

## 14. Pass R6 2026-09-13 (FINAL POLISH: feet/walk/hint/lifecycle) — VERDICT: PASS, đề xuất PHASE 1 LOCK
- Phạm vi: đúng 4 vấn đề mission (feet grounding, walk motion, quest-hint visual,
  quest lifecycle). Không quest/chapter/system/world mới. Không đụng face/camera/
  lighting/HUD-architecture (giữ nguyên các pass đã lock).
- SOURCE-OF-TRUTH lift (prove bằng 2 dòng độc lập — ảnh float ≈ lift + SOLE2
  bone-bind off ≈ lift ⇒ TRUE idle sink ≈ 0 cả 3 rig; BakeMesh minMapped là liar
  do Body scale 100x double-transform, BANNED khỏi mọi probe R6):
  - Milo VisualRoot 0.493 → 0.05 (A) → 0.02 (B) → 0.01 (C-final).
  - Mia VisualRoot 0.493 → 0.05 → 0.02 (giữ: SOLE2 0.003-0.009 ≈ contact).
  - Player GroundLiftLocal 0.340 → 0.06 (A) → 0.015 (B) → 0.005-final (0.004 world).
  - Player WalkLiftLocal −0.03 → +0.015 (B) → +0.025-final: dấu DƯƠNG là đúng —
    f6-22 chứng minh walk clip CROUCH sâu hơn idle ~3.6cm (gối trụ gập), comp
    giữ tổng walk = 0.024 world (giá trị f6-22 đã đứng vững). agent.baseOffset=0
    + breathing/surf giữ nguyên (legit, negligible). Giày ride lift (không lift
    riêng). Single source duy nhất: VisualRoot lift.
- Walk (không IK mới, không rig mới): controller sẵn đã mượt (blend 0.2/0.25s,
  ApplyRootMotion 0); agent.speed 3.5 → 2.2 m/s (~1.7 body-length/s, brisk child
  walk — skate hết). Evidence: f6-21 push-off (gót nhấc, không snap), f6-22
  mid-stride/push-off (chân trụ + chân vung + tay vung coherent), f6-23 stop
  (chân chụm, không snap). Milo/Mia stationary BY DESIGN (shopkeeper, không
  locomotion clip — thêm walk = new system, mission cấm): slot walk của NPC =
  idle-steady góc 2 + celebrate, ghi rõ trong matrix.
- Quest hint (refine, không rebuild): thought-bubble vốn đã đúng ngôn ngữ —
  shell +18%, apple icon 0.20→0.24 (icon là hierarchy level 1), icon pulse
  ±6% 1Hz + bob sẵn có (không arcade flash), dời khỏi label Mia về phía
  đông-nam ((Mia.x+1.45, 1.78, Mia.z+0.55)). Test textless: bỏ hết chữ vẫn đọc
  "wanted: apple". Không che mặt/body Mia ở mọi góc (05/27/28 + gameplay).
- Quest lifecycle (1 thay đổi production): ApplePresenter ẩn BigApple ngay tại
  WordSeen (crate rỗng + táo trên tay cùng frame — item không bao giờ active ở
  2 nơi), carried ẩn tại complete (sẵn có), bubble Show/Hide (sẵn có, verify),
  hint-glow guard khi crate đã ẩn, flower reward chỉ nở tại complete
  (SetActive(false) từ đầu — f6-01 chứng minh vắng pre-quest). Distractor ball
  ở lại đúng (world prop, inert post-quest). Không quest 2 (single-quest slice:
  terminal = Great job! + flowers, ghi rõ).
- Chuỗi evidence (production FINAL từ build C; C/D/E chỉ khác survey):
  - EditMode 52/52 PASS ×6 (mỗi vòng code + final trên cây khóa, zero temp).
  - Build A (candidate) / B (tune) / C (final lifts) / D (survey-fix) / E
    (in-beat 11): TẤT CẢ Succeeded errors=0 + payload DLL fresh + COMPLETE qua
    click raycast thật (wrongs=1, completed=True, Great job!, 0 exceptions,
    C/D/E zero TIMEOUT). A fail quest-path (2 TIMEOUT — click táo từ Mia-frame
    sau wrong) → B fix follow-restore; B/C miss beat window (complete sớm qua
    proximity / settle tràn 3.2s) → D/E stage ngoài 1.8m + settle gọn.
  - Số FINAL (survey E, bone-bind SOLE2 + Renderer.bounds SHOE, BakeMesh cấm):
    player off 0.020-0.029 idle / 0.001 post-walk; Milo 0.023-0.037; Mia
    0.008-0.019. Ảnh D/E: 3 rig tiếp xúc + bóng gắn, không daylight gap,
    không dangle, không 0.1m+ float ở front/side/3-4/low/idle/walk-stop.
  - Facing audit (TURN telemetry, decisive): root==travel, visual==root±glance;
    các still "ngược hướng" build A là misread (far-eye + arrival heading bắc
    vào cam nam) — survey D/E arrival +z (nam) cho TRUE FRONT, số xác nhận
    rootYaw≈5°, angRootToCam≈8°.
  - Celebrate: f6-10-D in-beat (Mia tay giơ + Happy, không che) + f6-11-E
    settled in-beat (Interaction, ensemble) + Happy durable post-beat.
- Lockdown (theo protocol R4): xóa R6Survey(+meta) → revert asmdef InputSystem →
  FINAL clean build Succeeded errors=0 (Bootstrap tươi, survey-free) → boot
  check alive 65s+ 3×FACE_OK 0 exceptions → xóa R6Build(+meta)+Assets/Editor →
  FINAL EditMode 52/52 trên cây khóa. Temp = 0 file. Không commit (chờ user).
- Bài học mới:
  - 26. Walk clip crouch (gối trụ gập hạ hông): comp dấu ÂM cũ chỉ đúng tình cờ
    ở magnitude — evidence đổi dấu comp thành DƯƠNG (+0.025) mới giữ stance;
    đừng tune animation bằng số BakeMesh, tune bằng stance photo + SOLE2.
  - 27. Arrival tolerance (remaining ≤ stopping+0.3) đánh bại mọi margin
    proximity < 2.5m: stage bring phải ≥2.56m nếu muốn completion rơi trong
    wait của click (nếu không beat window trôi trước khi shot).
  - 28. Beat timing = wait-settle arithmetic: f6-11 = T0+0.8+1.2+settle; settle
    default 1.2 đã đẩy shot đúng T0+3.2 = biên beat. Tính tay trước khi chạy.
  - 29. Photo misread có pattern: far-eye qua sống mũi + arrival heading ngược
    cam + label/prop trùng tia. TURN telemetry (rootYaw/vizYaw/bearing) rẻ hơn
    mọi tranh cãi — log nó ở mọi probe shot.
  - 30. Một static cam không cover 6m walk; mỗi motion beat cần framing riêng
    (start/mid/stop) + kiểm tra occluder (flower pot ăn frame mid ở build B).
- Matrix cuối (§34 mission): 1 Milo-feet PASS (f6-17/24-D/E + SOLE2 0.023) /
  2 Mia-feet PASS (f6-19/20/25 + 0.008-0.019) / 3 Player-feet PASS (f6-15-E TRUE
  FRONT + 16/26 + 0.001-0.029) / 4-5 Milo/Mia-walk N/A BY DESIGN (idle-steady +
  celebrate thay thế, mission cấm new system) / 6-9 Player walk/start/mid/stop
  PASS (21/22/23 C/D/E) / 10-14 hint PASS (05/27/28 + textless test) /
  15-19 lifecycle PASS (09 empty-crate+carried, 12 after, 13 terminal, 14 reward,
  crate/bubble null trong log; quest-2 N/A single-quest) / 20 full flow PASS
  (C/D/E zero-TIMEOUT real-click) / 21-22 builds PASS (C+D final-code + E, tất
  cả Succeeded + fresh DLL + COMPLETE) / 23 first-impression PASS (f6-01-E +
  f6-29: layered, Milo-mat, Mia-stall, apple-vs-ball tách).
- VERDICT: **FINAL POLISH PASS — đề xuất PHASE 1 = LOCKED** (production không
  đổi từ build C; C/D/E là 3 builds độc lập của cùng final code, tất cả
  Succeeded + COMPLETE + 0 exceptions). Cấm Phase 2 cho tới khi user duyệt lock
  + commit.
- Follow-up giữ nguyên (từ §5/§9/§11): `Walk_Carry` chưa wire, PregenSeeder
  audio fallback. (Mới, non-blocking: Milo label crop nhẹ ở spawn follow-view;
  "Hear it again" phủ chân Mia ở hint-closeup — cùng họ HUD-over-world đã
  accept ở R5V.)

## 15. Pass R7 2026-09-13 (bug mang táo không nhận quest + cursor/Hover) — VERDICT: PASS
- Báo cáo player thật: lấy táo → mang tới NPC nhưng quest không nhận + xin con
  trỏ chuột + hover đổi trạng thái. Điều tra ra 1 SOFTLOCK THẬT (do R6 gây ra):
  click táo TRƯỚC khi nói chuyện với Milo → WordSeen nổ (không gate) → R6 ẩn
  crate ngay → sau Talk, find_apple không còn táo visible/clickable để tìm lại;
  mang tới Mia pre-talk thì CompleteBring ăn mất carrying mà ReportAction no-op
  (quest chưa start) → kẹt. Thêm 2 rìa: click Mia pre-talk cộng wrong + story
  noise; MarketBootstrap khen "Bring it" + đổi HUD pre-talk (sai narration).
- Fix (production, tối thiểu, đúng seam sẵn có):
  - `ApplePresenter`: track `_questActive` (QuestStarted/Completed w1) — chỉ
    in-quest find mới ẩn crate; pre-talk find giữ crate + carried visual.
  - `MiaPresenter`: `_questStarted` gate — pre-talk click chỉ wave (không ăn
    carrying, không wrong, không story); CompleteBring double-guard.
  - `MarketBootstrap`: `_questStarted` gate — pre-talk WordSeen silent (HUD giữ
    "Talk to Milo").
  - Milo KHÔNG đổi: RepeatInstruction sau find vốn đã nói "Bring it to Mia!"
    (đúng redirect nếu audio chạy) — mang NHẦM sang Milo không phải bug, mang
    tới MIA (cô bán hàng) mới đúng; đã giải thích cho player.
- Cursor mới (`A_World/CursorPresenter.cs`, wire trong MarketBuilder —
  presentation-only, không service/event/audio): thay arrow OS bằng dot mềm
  (procedural, không asset), theo Mouse.current mỗi frame; hover trúng
  Interactable/IClickTarget (cùng contract với ClickRouter, read-only) → TO
  1.35× + VÀNG (trắng khi idle). Overlay KHÔNG BAO GIỜ ăn click:
  Image.raycastTarget=false + không GraphicRaycaster + không Collider
  (CT-P04F khóa). Hardware cursor ẩn khi custom hiện, restore ở OnDisable.
- Tests CT-P04 (6 tests → EditMode **58/58**): crate giữ pre-quest / ẩn
  in-quest / Mia pre-talk wave-only (giữ carrying, 0 wrong, 0 story) /
  talk→find→bring completes + CorrectChoice / hover language values / overlay
  safety. (1 fail giữa chừng: test thiếu glue WordSeen→AdvanceOnSeen mà
  production có trong MarketBootstrap — bổ sung đúng wiring production, xanh.)
- Verify live (build R7 Succeeded errors=0 + payload tươi + survey R7
  COMPLETE, 0 TIMEOUT, 0 exceptions): pre-talk tap (crate=True, carried=True,
  hud "Talk to Milo") → talk (crate=True còn → recover được) → re-find
  (objIdx=1, crate=null) → wrong=1 → bring → completed=True Great job!
  (crate/carried/bubble null). Ảnh: r7-05 cursor VÀNG trên người Mia (hover
  NPC), r7-02 dot vàng trên táo trong crate (hover object) + crate còn táo +
  táo trên tay, r7-01 dot TRẮNG idle.
- Lockdown: xóa R7Survey/R7Build(+meta)+Editor → revert asmdef InputSystem →
  FINAL EditMode **58/58** trên cây sạch (CursorPresenter + CT-P04 ở lại, là
  production/test thật, không phải temp). Không commit (chờ user).
- Bài học mới:
  - 31. Mọi visual lifecycle (ẩn/hiện object) PHẢI gate theo quest-active, cấm
    gate theo event trần — event nổ được ở mọi thứ tự click của trẻ con.
  - 32. Presenters cần quest-started flag riêng (Milo đã có, Mia/Bootstrap
    thiếu): GetState mặc định (idx 0, !completed) không phân biệt được
    "chưa start" vs "mới start".
  - 33. Overlay cursor: raycastTarget=false + cấm GraphicRaycaster là 2 dòng
    sinh tử (1 dòng thiếu là ăn toàn bộ click world qua IsPointerOverGameObject).
- VERDICT: **R7 PASS** — softlock pre-talk đã hết (chứng minh live + unit),
  cursor + hover + quest gates giữ nguyên toàn bộ evidence R6 (production R6
  không đổi 1 dòng trong pass này ngoài 3 gate + cursor mới).
- Follow-up giữ nguyên (từ §5/§9/§11/§14): `Walk_Carry` chưa wire, PregenSeeder
  audio fallback (fallback im lặng = lý do phụ khiến "mang tới Milo không thấy
  gì" — Milo redirect bằng audio line, cân nhắc visual redirect nếu audio tiếp
  tục câm).

## 16. Pass R8 2026-09-13/14 (player-report polish: range/cursor/zoom-detail) — VERDICT: PASS
- Báo cáo player thật: lệnh NPC nổ từ quá xa + zoom-in mất detail (label vỡ) +
  con trỏ OS không nói gì. Phạm vi đúng 3 việc: (a) siết ranges, (b) arrow
  cursor + hover marker, (c) label 2x. Không quest/system/world mới.
- Fix (production, tối thiểu):
  - `ClickRouter.arrivalRange` 1.9 → **1.5m** (arrival lands ~1.2-1.6m from
    center: conversational distance, taps feel earned).
  - `MarketBuilder` Apple `interactionDistance` 2.5 → **2.0m** (phải đi BỘ tới
    crate, không snipe across lawn; proximity discovery follow tự động).
  - `MiaPresenter.TryProximityBring` 1.8 → **1.5m** (handover OVER THE COUNTER;
    converge với click arrivalRange — 2 paths cùng 1 điểm).
  - `WorldNameLabel` 2x texel density (canvas 300×96@56pt → **600×192@112pt**,
    root scale 0.004 → **0.002**, world size GIỮ NGUYÊN 1.2m — close-up không
    blocky; text là detail duy nhất resolution-bound, face/prop là geometry).
  - `CursorPresenter`: dot R7 → **arrow thẳng đứng** (procedural 2-pass: dark
    border + white core, hotspot ở tip; thẳng đứng đọc giống nhau mọi orbit) +
    **HoverMarker "!" vàng** (dot+stem primitives, 1 object reuse, collider-free
    — không bao giờ ăn ray, bob 2Hz ±0.08 trên collider top +0.34m).
- Tests: CT-P04G (marker contract: hidden→placed→hidden, dot+stem, 0 collider,
  material đầy đủ) + CT-P04H (proximity boundary: 1.56m silent + giữ carrying,
  1.27m complete) + CT-P02 update theo 1.5m → EditMode **60/60 PASS**.
- Verify live (build R8 Succeeded errors=0 + survey R8 COMPLETE, 0 TIMEOUT,
  0 exceptions): pre-talk tap (crate=True, carried=True, HUD "Talk to Milo",
  marker=True trên táo) → talk (crate=True còn) → re-find (objIdx=1,
  crate=null) → hover Mia (marker=True "!" trên đầu Mia, thought-bubble táo) →
  wrong=1 → bring tại 1.5m → completed=True `Great job!` (Mia celebrate tay
  giơ, label "Mia" crisp 2x, arrow cursor thấy rõ) → macros Milo/Mia 1.6m (mặt
  đọc tốt + arrow render trong shot).
- Lockdown (theo protocol R4/R6/R7): xóa `R8Survey(+meta)` → revert asmdef
  InputSystem → FINAL clean build **Succeeded errors=0** (Bootstrap tươi do
  recompile, World/Brain giữ payload R8 đã survey — production không đổi) →
  boot check alive 70s+ **3×FACE_OK 0 exceptions** → xóa `R8Build(+meta)` +
  `Assets/Editor(+meta)` → FINAL EditMode **60/60** trên cây khóa. Temp = 0
  file. Không commit (chờ user, như R6/R7).
- Bài học mới:
  - 34. **Exe stub mtime KHÔNG phải freshness signal** (incremental build không
    relink native stub): `LittleWorldEnglish.exe` giữ 3:11 PM trong khi
    LWE.World/Brain/Bootstrap.dll tươi 10:25 PM. Từ nay chấm freshness bằng
    **managed DLL mtime**, không phải exe.
  - 35. `ScreenCapture` CÓ bắt ScreenSpaceOverlay cursor (arrow hiện trong cả
    gameplay shots lẫn macros) — lo ngại overlay-capture là thừa; virtual-mouse
    hover photograph bình thường.
- VERDICT: **R8 PASS** — ranges siết mà flow thật vẫn COMPLETE mượt, cursor +
  marker + label 2x chứng minh bằng ảnh, toàn bộ evidence R6/R7 nguyên vẹn.
- Follow-up giữ nguyên: `Walk_Carry` chưa wire, PregenSeeder audio fallback.

## 17. Pass R9 2026-09-14 (6 player reports: flower/distractor/bubble/cursor/marker/hedge) — VERDICT: PASS
- Yêu cầu user: (a) bồn 3 hoa to + che camera, (b) distractor pick được để
  khó hơn, (c) bong bóng gợi ý dễ nhìn hơn + chuẩn cho NPC sau, (d) mũi tên
  chuột xoay theo hướng nhìn player, (e) hover marker "!" → mũi tên chỉ xuống,
  (f) bỏ hàng rào kiểu chuồng cọp. Không quest/system mới.
- Fix (production):
  - **Flower** (`MarketBuilder`): cluster gọn (~1.2m → **~0.7m**: pot
    0.7→0.56, stalk 0.7→0.44, head 1.05→0.66/0.22, ox ±0.25→±0.13, vẫn 3
    bông) + dời `FlowerAnchorPos` (-1.5,2.5) → **(-4.6,5.0)** (góc SW, off mọi
    quest path; vòng 1 đậu sát gốc cây (-5.6,4.6) che stalk → dời tiếp 2m).
  - **Distractor** (`DistractorChoice` + `MiaPresenter`): bóng NHẶT được.
    Luật — pickup trung tính (quest mở, 0 wrong, `WordSeen(ball)` arm Mia,
    đã chứng minh safe với Milo/QuestManager/Bootstrap/Apple); mang tới Mia
    (click HOẶC proximity 1.5m) = wrong + bóng về pedestal (retry giữ);
    tay đang cầm táo mà chạm bóng = legacy instant-wrong; thấy táo khi cầm
    bóng = SWAP (bóng về, táo lên tay; proximity crate tự swap: forgiving).
    Pre-quest giữ legacy (bài 31), post-quest inert. Tay cầm chung fist-bone
    với táo (một tay một đồ).
  - **Echo-guard** (`MiaPresenter._bringJustResolved`, BUG THẬT do survey
    R9v1 khui): click arrival + proximity resolve CÙNG 1 bring — correct thì
    idempotent (carry clear) nhưng wrong bring bị đếm 2 (live: wrongs=2 cho
    1 lần mang). Bring (đúng/sai) arm flag; tap tay-không tiếp theo consume
    và silent (wave). One bring = one wrong.
  - **Bubble** (`WorldQuestionBubble`): shell/outline/icon +30%/+25%
    (0.65→0.85, apple 0.24→0.30, pulse ±6%→±8%) + **`AnchorFor(npcPos)`**
    contract cho mọi NPC sau (đông-nam, head height 1.78m, clear label/mặt/
    awning); `MarketBuilder` đặt bubble Mia qua contract.
  - **Cursor** (`CursorPresenter`): arrow xoay theo facing của player
    (project facing ra screen, `ComputeArrowAngle`, smooth `SmoothAngle`);
    marker "!" → **mũi tên chỉ xuống** (shaft + 2 chevron, primitives only).
  - **Hedge** (`MarketBuilder.BuildHedgeEdge` thay `BuildFence`): bụi tròn
    xen kẽ + khóm hoa 3 màu (deterministic, không Random), cao ≤0.8m,
    cùng footprint (carves `EdgeCarve*`, router bounds giữ nguyên),
    collider giữ (bake/click như posts cũ). Sightlines mở mọi camera.
- Tests: CT-P05 mới 9 tests (pickup neutral / ball-bring wrong+restore /
  swap / hands-full legacy / proximity-ball-wrong + empty-silent / anchor
  contract + size pins / heading angle + SmoothAngle range / full loop /
  echo-silent) + CT-S01A viết lại theo hands-full + CT-P04G marker mới →
  EditMode **69/69 PASS** (60 cũ + 9 mới).
- Verify live (4 builds Succeeded errors=0; survey R9v4 FINAL COMPLETE,
  **0 TIMEOUT, 0 exceptions**): pretap → talk → PICKUP (pedestal rỗng,
  carriedball=True, 0 wrongs) → hover Mia (down-arrow vàng) → ball-bring
  **wrongs=1** (echo-guard: proximity+click = 1!) → find (objIdx=1) →
  legacy-wrong tay-đầy **wrongs=2** → bring **completed=True, wrongs giữ 2**
  (apple echo cũng chặn) → bubble closeup mid-quest (shell to, táo đọc ngay)
  → flower SW gọn 3 bông → hedge wide (garden edge, hết chuồng cọp) →
  macros Milo/Mia. Góc arrow render khớp log <1° (flower +82, hedge +47 —
  đối chiếu bằng projection math đầy đủ, hết nghi vấn mirror).
- Lockdown: xóa `R9Survey(+meta)` → revert asmdef → FINAL clean build
  **Succeeded errors=0** → boot check 70s+ **3×FACE_OK 0 exceptions** → xóa
  `R9Build(+meta)` + `Assets/Editor(+meta)` → FINAL EditMode **69/69** trên
  cây khóa. Temp = 0 file. Không commit (chờ user, như R6/R7/R8).
- Bài học mới:
  - 36. **Click arrival + proximity resolve cùng 1 bring**: correct thì
    idempotent nhưng wrong thì double-count. Mọi bring mới (đúng/sai) đều
    cần echo-guard phía receiver — một bring = một wrong.
  - 37. **`LerpAngle` chạy ngoài ±180°** (389/442/635 trong log): normalize
    mỗi frame trước render mapping (`SmoothAngle`), pin bằng unit test.
  - 38. **Survey wait-mode phải đọc state HIỆN live**: `Find()` bỏ qua
    object inactive (mode-8 đọc `CarriedBall`, không phải bóng đã ẩn) +
    shot bubble closeup phải chạy mid-quest (bubble ẩn post-complete).
  - 39. Nghi vấn "render sai" thì đối chiếu bằng **projection math đầy đủ**
    (view matrix + perspective division), không bằng trực giác compass —
    trực giác đã sai handedness, số thì khớp log <1°.
- VERDICT: **R9 PASS** — đủ 6 yêu cầu có ảnh chứng minh, flow R6/R7/R8
  nguyên vẹn (COMPLETE + Great job! + retry + gates), 2 bug thật tìm ra khi
  survey đều đã fix + verify lại.
- Follow-up giữ nguyên: `Walk_Carry` chưa wire, PregenSeeder audio fallback.

## 18. PHASE 1 LOCK 2026-09-14 (formal checkpoint — Phase 2 may begin)
- Lock record: `docs/HANDOFF/PHASE_1_LOCK.md` (STATUS: LOCKED).
  Phase 1 is a baseline, not a perfection loop; the 10 known items
  there are non-blocking backlog (reopen gate documented in the lock).
- Code baseline: commit `2851717` + tags `phase-1-locked` /
  `v0.1-phase1-locked`. Tree fully clean (0 temp files, asmdef
  InputSystem reverted, `git status` empty).
- Lock-session evidence (this section governs build identity):
  EditMode **69/69** pre- AND post-lockdown; survey build Succeeded
  (0 compile errors) + P1LOCK COMPLETE (talk→pickup→wrong=1→find→
  bring→completed, `Great job!`, 0 timeouts, 0 exceptions, 7 shots,
  spawn+complete visually inspected); final clean build Succeeded +
  boot 3×FACE_OK + 6/6 SHOE side + 0 exceptions.
- Mission-brief note: the brief's "52/52, 4 builds" numbers were the
  R4 baseline; governing lock numbers are **69/69** (52→58→60→69 via
  R7/R8/R9, zero regressions) + the R9v4→P1LOCK build chain above.
- Env lessons (new):
  - 40. Unity batch `-executeMethod` does NOT exit alone — always pass
    `-quit` or the call hangs until killed (cost one 20-min timeout).
  - 41. Temp survey scripts must self-spawn via
    `[RuntimeInitializeOnLoadMethod]` (scene stays a one-GO shell;
    nothing attaches temp drivers). An `Awake`-only driver silently
    never runs (cost one rebuild).
  - 42. Project `Temp/` is unstable in this environment (a Succeeded
    build's output vanished mid-session) — build to the absolute
    `C:/Users/ASUS/AppData/Local/Temp/opencode/PlayerBuild` path.
  - 43. `errors=N` in the build summary counts headless env noise
    (RenderTexture/license lines); judge by `result=Succeeded` + zero
    `error CS` + fresh managed DLLs.
  - 44. Background players present-stall (75s background run never
    reached face setup; foregrounded run hit 3×FACE_OK in ~10s) —
    always foreground before judging liveness.
  - 45. Survey shots must fire INSIDE the camera focus window (R6 lesson
    28, re-learned in 2E: 1.2-2.4s durations with longer waits shot the
    returned Follow view). Duration 8s + settle ~2s + shoot.
  - 46. Under present-stall, screenshot requests QUEUE and flush late:
    files can capture neighbor states (2E icon beats proved transitions
    in log + both states on screen, but per-file mapping slipped a beat).
    Prove sequences by SET + log, never by a single frame alone.
  - 47. Survey arrival predicates must be LOOSER than the agent's
    stoppingDistance (0.4m): a 0.35m tolerance never tripped while gameplay
    parked correctly, burning full windows (2F ball-find, twice).
  - 48. Mount Interactables on the CLICKABLE VOLUME root (crate), not the
    visual sweet spot (sphere): GetComponentInParent searches UP only, so
    low-angle rays hitting the wide body silently fall through to movement
    (2F ball-find fired only via proximity backup until moved).
  - 49. File created ≠ wired: the BallPresenter existed for a full build
    cycle with zero effect (no GameObject, no Bind) while the quest half-
    worked around it. Instantiation checklist: file → GameObject → Bind →
    predicate targets the HIDDEN object (crate root ≠ hidden sphere).
  - 50. Cursor follow-ups (player report): click direction beats facing for
    the arrow cue (assumed walk direction); verify chevron TIP direction by
    computing arm endpoints, not by reading rotation signs (a ±45° pair can
    render ∧ while the comment claims ∨ — it did); Confined (not Locked)
    keeps the OS position stream alive for custom cursors, M toggles release.
  - 51. Zombie Unity batch processes hold the Library lock and kill the next
    run at startup (45-line log, exit 1, no compile error) — sweep Unity
    processes before re-running, don't "fix" code that never compiled.
  - 52. Hover-driven visuals hide themselves live (Update sees no hover and
    switches off): freeze the presenter to photograph them, don't fight the
    state machine from the survey.
- Next: PHASE 2A (content/quest data foundation) — see
  `docs/HANDOFF/`.
