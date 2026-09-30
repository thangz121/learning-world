# SESSION BRIEF — BEAUTY PASS SÂN + ARENA (2026-09-30, ASUS)

> Phiên sau đọc DUY NHẤT file này để vào việc. Luật + việc đang mở ở `HANDOFF.md`
> (đã rút ngắn). Plan: `tái cấu trúc.md`.

## ⛔ LUẬT ĐANG HIỆU LỰC (bắt buộc)
- **TUYỆT ĐỐI KHÔNG mở game / journey trên ASUS** (kể cả windowed). Trên ASUS chỉ:
  **EditMode suite (batchmode) + production build (batchmode)**.
- Journey (`-journeys3` / `-journeyfull` / `-playlog`) **CHỈ chạy trên maynode**.
- **maynode đang OFFLINE** — khi user báo bật lại: scp bundle → fetch/ff → node
  build → node journey → kéo log/shots về → phân tích → báo user.
- Commit mọi tooling/driver đi kèm bundle. **Không push origin từ ASUS.**
- Không tự chốt "visual pass" — human review.

## 0. Máy / repo / git
- Máy: **ASUS**. Repo `D:\Vscode\little-world-english`, nhánh `main`,
  Unity 6000.6.0f1 `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`.
- **HEAD = `43e86e2`**, cây sạch (commit handoff nối tiếp là docs-only).
- `origin/main = 54a9126`; dòng reset `54a9126..43e86e2` gồm 21 commit
  Node đã ở `43e86e2` (bundle 2237) + build + journey 28/28.
- **Bundle verify cuối đã để sẵn: `D:\asus-20260930-0804.bundle`**
  (verify OK, HEAD 43e86e2, requires 54a9126 + bbfed86) + `-diff.patch`.

## 1. Đã xong phiên này (verify batch xanh)
- Reset kiến trúc theo `tái cấu trúc.md`: 5 môn / 21 kỹ năng / ĐÚNG 2 game
  HUMAN_ACCEPTED (rabbit_feeding + number_stairs); hub = Sân chọn Môn (5 cổng,
  KHÁM PHÁ mới); B/C = một `SelectionYardScene` data-driven; C→GAME→C đã khôi
  phục qua `SelectionYardArea` (lifecycle re-home từ CountingGardenArea cũ).
- Feedback các vòng user tự chơi: nhãn yard world-staged + best-fit; walkway
  Toán/Tư duy bẻ vòng tránh cổng Việt/Anh; voice-first (bỏ chữ trong arena);
  diorama không chữ trước cổng game; **demo NPC đủ bước** (đi–nhặt–bưng–đặt /
  leo từng bậc có settle); tỉ lệ bài **80/12/8**; **nhạc baroque** Pachelbel-D +
  nút "Nhạc: bật/tắt" dưới nút ngôn ngữ; **game bảng đáp án 3 ô** (thỏ, cứ 3
  lượt tìm số 1 lượt — CT-S16).
- **Verify batch cuối (ASUS): suite `723/718/0/5`** (`Temp/s19-suite.xml`) + build errors=0.
- `comparison_market` (Khu Chợ Của Bé, IMPLEMENTED): LV3→LV10 một chợ, CT-S18 7/7.
- `classification_city` (Thành Phố Phân Loại, IMPLEMENTED): LV3→LV10 một thành phố, CT-S19 7/7.
  Journey cuối trên NODE vẫn là bản cũ: `-journeys3` **28/28** (chưa có beauty pass).

## 1b. Beauty pass (2026-09-30, chưa human review)
- Sân B/C không còn thảm cỏ trống: cây anh đào, đường đá, đèn, hoa, bướm,
  hàng rào nở hoa, biển tên gỗ sau nhãn, cổng vào có vương miện hoa.
- Sân trống (chưa có trò) có ao + cây, không còn mỗi cái biển.
- Thỏ: tai dài hơn, má + nơ. Hàng rào arena thỏ/thang nở hoa.
- Dressing collider-free + `ignoreFromBuild` (CT-S12G). Không đụng cổng/click/NavMesh lối đi.
- KHÔNG tự chốt visual pass. Không mở game trên ASUS.

## 2. Chờ HUMAN REVIEW (không tự chốt)
- Cỡ nhãn trong yard + HUD cắt chữ dài (đã best-fit — xem lại).
- Feel của demo / nhạc baroque / lượt bảng đáp án (user chơi trên node).
- **Finding tồn:** cổng TƯ DUY không vào được bằng click từ hub — xử lý ở PHASE 5.
- **Log phiên chơi cuối còn trên node:** `E:\LWW\play-session.log` +
  `E:\LWW\play-shots` (kéo về + phân tích khi node bật).

## 3. Việc tiếp theo (thứ tự)
1. Khi node bật: scp `D:\asus-20260930-0804.bundle` → node ff → build → journey
   → kéo về `s4j-*`, `play-session.log`, `play-shots` → phân tích log chơi user.
2. PHASE 5 dọn legacy (MathScene/CountingGardenScene/DiscoveryScene + catalog/
   districts/tests cũ; Build Settings còn 5 scene; xử lý luôn cổng Tư duy).
3. PHASE 6 content/catalog reset → PHASE 7 test/build reset + retarget
   `TempFullJourney` → PHASE 8 final verification + report §18.

## 4. Lệnh verify nhanh (ASUS — BATCH-ONLY)
Unity.exe là GUI app → PowerShell luôn dùng `Start-Process -Wait`.
1. **Suite**: `-batchmode -projectPath D:\Vscode\little-world-english -runTests
   -testPlatform EditMode -testResults <xml> -logFile <log>` → kỳ vọng
    **723/718/0/5**.
2. **Build**: `-batchmode -quit -executeMethod TempBuildP62.Build -logFile <log>`
   → kỳ vọng **Succeeded errors=0**.
3. ⛔ KHÔNG chạy: `LWE.exe -journeys3` / `-journeyfull` / `-playlog` trên ASUS.
   Journey node: `schtasks /IT` (SSH không có DXGI — lỗi 887a0022).
