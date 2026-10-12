# 04 — VERIFICATION — WP-1.9.32

> **Tóm tắt (VI):** Suite EditMode: **739 test / 734 passed / 0 failed / 5 skipped** (tăng 9 test
> mới; các pin cũ được cập nhật trung thực: Games 5→6, mirror 12→28). Production build batch:
> **Succeeded errors=0**; không có warning nào từ file mới. Kiểm định audio: validator PASS,
> ASR self-check 16/16. Còn lại: human/visual QA (quy tắc dự án — không tự chốt).

## Batch verification (máy này, Unity 6000.6.0f1)

| bước | lệnh | kết quả |
|---|---|---|
| Suite EditMode | `Unity.exe -batchmode -projectPath … -runTests -testPlatform EditMode -testResults Temp\s21c-suite.xml` | exit=0 · total **739** · passed **734** · failed **0** · skipped 5 |
| Production build | `Unity.exe -batchmode -quit … -executeMethod TempBuildP62.Build` | **result=Succeeded errors=0 warnings=190** (toàn bộ warning là cũ: TempP55/TempP62 journeys, NetworkMicrophoneCapture…; **0 warning** từ file ListeningGarden/CT-S21) |

- Test mới: `CT-S21_ListeningGarden` A→I (9 test) — tất cả xanh.
- Pin cũ cập nhật: `CT-S10` (6 games, asserts mới), `CT-P08A` (28 entry).
- Chưa có journey/visual run cho arena này (các driver hiện chỉ đi chuỗi Toán) — bước human.

## Audio verification

| kiểm | kết quả |
|---|---|
| `tools/pregen_listen.py` self-check (faster-whisper base.en) | **16/16 khớp chính xác** |
| Chạy lại lần 2 (convergence) | 16/16, toàn bộ cache (file không đổi) |
| `tools/validate_content.py …` | **PASS** (voice pool + mirror + binary + MPEG sync) |
| CT-P08A/B | mirror 28 entry hợp lệ; key pinning vocab/Milo cũ nguyên vẹn |
| CT-S21F | 16/16 khoá cache runtime == manifest (ngoại tuyến 100%) |

## Việc còn lại cho người (không tự chốt)

1. Chơi thử thật: vào hub → Tiếng Anh → Listening → Vườn Tai Thính; xem cỡ ghế/hình, vị trí
   chuông, cảm giác nhịp đọc từ (normal/slow), câu khen tiếng Việt/Anh theo cài đặt ngôn ngữ.
2. Xác nhận ánh xạ hình–từ (đặc biệt "grapes" hiển thị chùm nho) và tỉ lệ LV4 (4 hình từ 8 từ).
3. Nếu muốn mở rộng: thêm từ vào `Content/listening/listen_garden.json` → chạy `tools/pregen_listen.py`
   → thêm vào pool trong `ListeningRounds.Pools` (JSON đang là nguồn chuẩn; CT-S21G giữ khớp).

## Ghi chú cập nhật tương lai

- Ai sửa param runtime (rate/pitch/style/voice) PHẢI cập nhật manifest tương ứng, nếu không
  CT-S21F/CT-P08B sẽ đỏ (đúng thiết kế: khoá chống "silent fallback").
- Model Piper nằm ngoài git (`D:\speech-lab\models\piper`) — cần khi sinh thêm audio; máy khác
  chỉ cần file MP3 đã commit là chơi được.
