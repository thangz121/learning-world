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
