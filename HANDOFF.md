# HANDOFF — hiện trạng (2026-09-30, ASUS)

Phiên sau đọc `SESSION_BRIEF.md` trước. File này chỉ giữ luật còn hiệu lực,
việc đang mở, và bài học không được lặp. Nhật ký cũ (§0–§93, world/vườn đếm
trước reset) đã xóa khỏi file này; bản đầy đủ vẫn nằm ở git `43e86e2`.

Plan làm tiếp: `tái cấu trúc.md` (PHASE 5→8).

## Luật

- Không mở game / journey trên ASUS (kể cả windowed). ASUS chỉ EditMode batchmode
  + production build batchmode. Journey (`-journeys3` / `-journeyfull` / `-playlog`)
  chỉ trên maynode, và SSH không có DXGI (lỗi 887a0022) — phải `schtasks /IT`.
- maynode đang OFFLINE. Không push origin từ ASUS. Không commit khi chưa được lệnh.
- Không tự chốt visual pass.
- C# 9. Composition root duy nhất: `GameInstaller`. Không `new` service ngoài Installer.
- Câu NPC ≤ 6 token (`SafetyFilter`). `PacedVoice` chỉ giữ câu mới nhất — đừng
  `Say` thêm một câu ngay trước lời khen, nó nuốt lời khen.
- NavMesh bake đọc RENDER MESH, không phải collider. Xà ngang đường phải
  `ignoreFromBuild`. Carve chữ U phải theo trục đường.
- Arena voice-first (`ActivityFeedback.TextHidden`). Tỉ lệ bài 80/12/8.
- Không làm yếu assertion.

## Đang có

- 5 môn / 21 kỹ năng / đúng 2 game HUMAN_ACCEPTED: `rabbit_feeding`, `number_stairs`.
- Hình học: `shape_builder` / Lắp Hình Vui Nhộn / `GeometryPlayScene` — IMPLEMENTED
  (không HUMAN_ACCEPTED). LV3→LV10 trong một xưởng, 4 hình, màu không phải đáp án.
- Hub = sân chọn môn (5 cổng). B và C = một `SelectionYardScene` data-driven.
- Cà rốt: nhặt–bưng–đặt–bấm chuông. Lệch 1 củ thì giữ bát ("Thêm một củ" /
  "Bỏ một củ về"); lệch ≥2 thì xóa bát. Bát đủ số: thỏ nhảy + "Bấm chuông nhé!".
- Bậc thang: leo, đứng yên 1.1s trên đúng bậc thì thắng. Đúng bậc thì bậc nhấp
  một lần (không thêm câu).
- Demo NPC đủ bước. Cứ 3 lượt tìm số, 1 lượt bảng 3 ô (thỏ, CT-S16).
- Nhạc baroque + nút bật/tắt. Sân B/C có cây/hoa/đèn/biển gỗ; sân trống có ao.
  Dressing không collider, `ignoreFromBuild` (CT-S12G).
- Suite hiện tại: **709/704/0/5**. Journey node cuối (bản `43e86e2`, chưa có
  beauty/age-4/geometry): `-journeys3` 28/28.

## Còn mở

- Nhìn mắt: cỡ nhãn yard, HUD cắt chữ dài, feel demo/nhạc/bảng đáp án, sân mới.
- Cổng TƯ DUY không vào được bằng click từ hub — xử lý ở PHASE 5.
- Log chơi user còn trên node: `E:\LWW\play-session.log` + `E:\LWW\play-shots`.
- Bundle sẵn (HEAD `43e86e2`, chưa gồm beauty/age-4): `D:\asus-20260930-0804.bundle`.
  Khi node bật: scp → `git fetch` + `merge --ff-only` → build node → journey `/IT`
  → kéo log/shots về. Beauty/age-4 cần bundle mới sau khi commit.

## Tiếp theo

1. PHASE 5: xóa `MathScene` / `CountingGardenScene` / `DiscoveryScene` + catalog,
   district, test legacy. Build Settings còn 5 scene. Kèm cổng Tư duy.
2. PHASE 6 catalog → PHASE 7 test/build + retarget journey → PHASE 8 report §18
   trong `tái cấu trúc.md`.

## Verify (ASUS)

Unity.exe là GUI app → `Start-Process -Wait`. Không kèm `-quit` khi `-runTests`.

- Suite: `-batchmode -projectPath D:\Vscode\little-world-english -runTests -testPlatform EditMode -testResults <xml> -logFile <log>` → **709/704/0/5**
- Build: `-batchmode -quit -projectPath D:\Vscode\little-world-english -executeMethod TempBuildP62.Build -logFile <log>` → **Succeeded errors=0**
