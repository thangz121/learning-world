# SESSION BRIEF — S3-P2Z32 BẬC THANG HỌC +/− + FIX JOURNEY 6/6 + NÂNG UX/UI (2026-09-28, maynode)

> Mục đích: phiên sau chỉ cần đọc DUY NHẤT file này để vào việc — không phải đọc
> lại HANDOFF.md. Cập nhật file này mỗi khi xong một bước.

## ⛔ BACKUP BẤT BIẾN — LẤY Ở ĐÂU
- Thư mục: **`E:\LWW\learning-world-BACKUP-2026-09-28-P2Z32`** (KHÔNG SỬA).
- Toàn bộ source working tree 2026-09-28, gồm **71 mục CHƯA COMMIT** + `.git`.
- Tag bất biến: `DO_NOT_MODIFY.md` + 1087 file READ-ONLY. Xem HANDOFF §90.
- Khôi phục: copy đè lên `E:\LWW\learning-world`, xoá `Library/`+`Temp/`, mở Unity.

## 0. Máy / repo / trạng thái git (bắt buộc biết)

- Máy hiện tại: **Maynode** (foreground). Repo `E:\LWW\learning-world`, nhánh
  `asus-merge-check`, Unity **6000.6.0f1**:
  `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`.
- **CHƯA COMMIT** toàn bộ (vòng §80-§89 + P2Z32). Quy ước bất di bất dịch:
  **KHÔNG commit/push khi chưa có lệnh user.**
- Baseline verify cuối: EditMode **705/700/0/5** (`E:\LWW\z15.xml`); build
  production + journey **Succeeded errors=0**; FULL JOURNEY **6/6 PASS,
  errors=0 severe=0**, game **tự đóng** (`E:\LWW\z-journey14.log` + `z-jshots14`).
- **P1→P5 đã xong + push** (`origin/asus-merge-check`): P1 `f46242c`, P2 `4942a46`,
  P3 `11e5323`, P4 `19b5eff`, P5 `c4798c2`. Cây làm việc sạch.

## 1. VIỆC NHỊP NÀY ĐÃ XONG (§89 trong HANDOFF)

### Bậc thang học +/− (S3-P2Z32, mirror nguyên lý cà rốt — nhưng core khác: thân
thể là trục số)
- 3 loại câu `Plain/Add/Sub`; câu sau RANDOM từ **bậc đang đứng**; cộng = leo
  lên, trừ = đi xuống; guard không âm/<=9. Win = đứng đúng bậc + dừng 1.1s.
- Board biểu thức `A+B` / `A−B`, khi đúng giải `A+B=C`; audio have/want/how-many;
  đọc lời giải rồi mới sang câu kế; **nhắc lại nhẹ** khi đứng sai bậc.
- **Dải trục số** dưới chân (`SHNumberRailOrb1..9`) sáng theo bước.
- Demo vườn xoay vòng plain "3" -> `2+1` -> `3−1`.
- `ArithmeticEnabled=true` chỉ ở live (`GameInstaller`); test/ladder cũ giữ xanh.

### Fix journey
- `hub.match`: pad collider CAO làm tia chạm mặt gần (0.5m < stoppingDistance) ->
  bé đứng im; đổi sang **collider dẹt sát đất** (route xuống tâm pad).
- `garden.rabbit`: `CanDragNow` gate theo khoảng cách (`PlayerAtBowl`); nhặt củ
  không còn tính là tap vườn; **dời trạm nộp** sang `(-4.8,0,-0.6)` thoát
  sightline luống cà rốt; harness chờ feed bằng proximity.
- `TempFullJourney`: sau `FULLJOURNEY_END` -> `FULLJOURNEY_QUIT` -> tự đóng game.

## 2. NHIỆM VỤ ĐANG MỞ (lệnh user, toàn quyền)

User: "fix luôn. Nghiên cứu github / open source áp dụng vào phần chơi các trò
chơi; **UX và UI ở mức cao nhất**; nghiên cứu gameplay các trò chơi khác; chắt
lọc cái hay để nâng UX/UI; **toàn quyền quyết định kể cả thay đổi core gameplay
đáng kể**. Không giới hạn thời gian/số lượng code."

→ Chưa bắt đầu. Hướng dự kiến: khảo sát OSS (Unity educational/mini-game, game
juice, accessibility, feedback layers), chắt ra danh sách cải tiến, rồi triển
khai từng bước có test + verify (suite/build/journey), cập nhật handoff.

## 3. NỢ CÒN LẠI

- Commit/push toàn bộ — chờ lệnh user.
- Phạm vi đề 1..9 hay mở tới 10 (cần digit 2 chữ số + "mười") — chờ user.
- Xoá `Assets/Editor/TempBuildP56.cs` (+meta) khi hết vòng.
- Save format / Phase 3.1 — ngoài scope.

## 4. Lệnh verify nhanh (maynode, Unity 6000.6.0f1)

Unity.exe là GUI app → trong PowerShell luôn dùng `Start-Process -Wait`.

1. **Suite**: `-batchmode -projectPath E:\LWW\learning-world -runTests
   -testPlatform EditMode -testResults E:\LWW\x.xml -logFile E:\LWW\x.log`
   (KHÔNG `-quit`) → kỳ vọng **705/700/0/5**.
2. **Build**: `-executeMethod TempBuildP56.Build` / `.BuildJourney` (+`-quit`).
3. **Journey full**: `E:\LWW\P56JBuild\LWE.exe -screen-width 1920
   -screen-height 1080 -screen-fullscreen 0 -logFile <log> -journeyfull -lang en
   -shot-dir E:/LWW/<dir>` → kỳ vọng **6/6 STAGE_PASS, errors=0 severe=0** và
   **tự đóng** (`FULLJOURNEY_QUIT`). (Có thể `-journey-from <stage>` để chạy 1
   stagE lẻ.)
4. **Boot smoke production**: FACE_OK + 0 Exception.
