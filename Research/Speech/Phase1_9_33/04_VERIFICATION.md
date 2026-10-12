# 04 — VERIFICATION — WP-1.9.33

> **Tóm tắt (VI):** Suite EditMode **751 / 746 passed / 0 failed / 5 skipped** (12 test mới;
> các pin cũ cập nhật trung thực: 7 games, 12 từ có phonemes). Production build **Succeeded
> errors=0 warnings=0**. Smoke test service thật: health OK, trẻ thật ~0.42s/clip, silence bị
> chặn transcript đúng thiết kế. Còn lại: QA giọng trẻ thật + mic (cần người + phần cứng).

## Batch verification (máy này, Unity 6000.6.0f1)

| bước | kết quả |
|---|---|
| Suite EditMode | exit=0 · total **751** · passed **746** · failed **0** · skipped 5 |
| Production build (`TempBuildP62.Build`) | **result=Succeeded errors=0 warnings=0** |

## Service smoke test (WAV thật)

| input | kết quả |
|---|---|
| `/health` | `{"ok":true,"service":"speak-assess","engine":"base.en","ready":true}` |
| Trẻ bản ngữ (Zenodo) "one.wav", target=one | transcript "One." conf 0.63 · **423 ms** |
| Trẻ SO762 "WE CALL IT BEAR", target=bear | "We call it Bill." conf 0.54 · 412 ms (đuôi /r/ trẻ khó — đúng như báo cáo §01) |
| Silence, target=apple | transcript "" (no_speech 0.78) — cổng chống hallucination hoạt động |

- Không cần mạng, không license trả phí (faster-whisper base.en MIT + model MIT).

## Việc còn lại cho người (không tự chốt)

1. **QA bằng giọng trẻ thật + mic thật** (PC có mic hoặc điện thoại qua phone-mic gateway):
   chơi hết một lượt 8 từ; xem nhịp prompt→nói→phản hồi, độ nhạy 4 mức, vị trí micro/vòng sáng.
2. Nếu thấy chấm quá dễ/quá khắt khe: ghi lại case cụ thể (từ + mức + video/âmm) → mở WP tinh chỉnh
   theo quy trình (đo trên trẻ thật, không sửa ngưỡng tùy tiện).
3. Cân nhắc nối panel mic-setup (OfferPhone) trực tiếp khi trẻ bấm vào bàn nói — hiện đã có câu
   nhắc + gate; nối dialog đầy đủ là việc giai đoạn sau.

## Ghi chú vận hành

- Service tự warm-up khi vào arena (log "speak-assess service ready/unavailable"); chạy tay:
  `tools/start_speak_assess.ps1` (port 8470). Không chạy service = chấm acoustic-only (vẫn đủ 4 mức).
- Log dev mỗi lượt: `[SpeakingStage] target=… level=… score=…% evidence={…}` — đây là chỗ đọc "%".
- Pin chống hồi quy: đổi params prompt audio phải khớp manifest (CT-S21F/CT-P08B/TryParseReply…).
