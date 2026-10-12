# 00 — DESIGN: SÂN KHẤU NHÍ + BACKEND CHẤM NÓI (English → Speaking)

> **Tóm tắt (VI):** Trẻ nghe từ + thấy hình → NÓI vào micro → **backend chấm** (engine acoustic
> offline + service nhận dạng whisper chạy local) → trẻ nghe ĐÚNG 1 trong 4 mức:
> **Xuất sắc / Đúng rồi / Gần đúng rồi / Thử lại nhé**. Sai từ / chưa nghe thấy / nói nhỏ đều có
> câu riêng, không bao giờ "fail" trẻ. Thiếu micro/service = lỗi MÔI TRƯỜNG (không phải lỗi trẻ),
> tự động hạ cấp xuống chấm acoustic và nhắc nhẹ.

- **Game id:** `speaking_stage` · **Tên:** "Sân Khấu Nhí" · **Skill:** `english_speaking`
- **Scene:** `SpeakingStagePlayScene` (root `SpeakingStageWorld`) · Build Settings #13
- **Trạng thái:** `Implemented` (chưa human-accepted; không tự chốt visual)
- **Điểm tựa (base cũ tận dụng):** `SpeechRecognizer` + `SpeakingPassPolicy` + `SpeechLevel`
  (đã có, chưa từng được lắp) + `LocalAcousticProvider` (engine DSP offline) + mic-gate/phone-mic
  + pack 8 từ của "Vườn Tai Thính" (một pack, hai kỹ năng).

## 1. Luồng một vòng

| bước | trẻ | hệ thống |
|---|---|---|
| 1 | nghe từ ("apple") + thấy hình quả táo | phát audio pack (offline, voice `listen_v1`) |
| 2 | nghe "Con nói nào!" rồi nói vào mic | bắt đầu ghi 5s (dừng sớm khi im 1.5s) |
| 3 | — | **backend chấm** (mục 2) → mức 1..4 |
| 4 | nghe 1 trong 4 câu + thấy hiệu ứng | Xuất sắc/Đúng → sao + sang từ mới; Gần đúng → phát lại (normal) + thử lại; Thử lại → phát lại (chậm) + thử lại |
| 5 | hết 3 lượt mà chưa đạt | "Mình thử từ khác nhé!" → sang từ mới (không kẹt) |

- Một lượt = 8 từ (LV1: apple/ball/banana/orange · LV2: carrot/tomato/watermelon/grape),
  3 lượt/từ; hoàn thành lưu lifecycle như các arena khác (`MarkCompleted`, adopt lần sau).
- **% đúng (nội bộ):** `OverallScore` 0..1 của policy → log dev + property `LastPercent`;
  trẻ chỉ nghe 4 mức, không thấy số.

## 2. Backend chấm (2 tầng, dựa trên base đã có)

```
mic (PC hoặc điện thoại) → GateSpeechCapture → SpeechRecognizer
   → HybridSpeechAssessmentProvider
        ├─ LocalAcousticProvider (LUÔN chạy): chấm closed-set từ mục tiêu (DTW/DSP) — offline 100%
        └─ LocalServiceAssessmentProvider (khi service chạy): gửi WAV 16k → whisper base.en
   → SpeakingPassPolicy (ngưỡng đóng băng: lexical+conf+acoustic, cổng chống hạ bậy)
   → SpeechLevel { Perfect, Great, Almost, TryTogether } → 4 câu tiếng Việt/Anh
```

- **Merge FRR-first (mới):** transcript chỉ được gắn khi không phản bội bằng chứng âm học:
  audio mạnh+sạch (match >= 0.60, không cụt đuôi) mà transcript lệch hẳn (lexical < 0.40)
  → BỎ transcript (chống hallucination của whisper). Ngược lại giữ (đây là bằng chứng
  "đã nói đúng TỪ đó chưa" + bắt sai-từ ngoài cohort).
- **Service chết = game vẫn chạy** (acoustic-only); mic thiếu = lỗi môi trường (nhắc 1 lần,
  lần 2 mời quay lại sau, KHÔNG hoàn thành lượt, KHÔNG mất tiến độ).
- Service: `tools/speak_assess_server.py` (faster-whisper base.en MIT, CPU int8, cổng 8470,
  tự chặn transcript khi no_speech > 0.5). Game tự warm-up nền (`LocalAssessServiceLauncher`),
  không bao giờ block gameplay.

## 3. Hình ảnh (wordless, voice-first)

Sân khấu nhỏ: sàn + rèm 2 bên, **micro vàng có vòng sáng** (nhấp nháy khi đang nghe),
**bục hình** cao giữa sân khấu (ảnh PropKit theo từ), đĩa chơi + cổng vào/ra như các arena.

## 4. Giới hạn đã biết (trung thực)

- Chất lượng chấm trên GIỌNG TRẺ THẬT chưa được người kiểm chứng; whisper với từ đơn của trẻ
  còn yếu (xem §01) — engine acoustic là chính, transcript là bổ trợ.
- Mic trên máy dev hiện không có → QA thật cần PC có mic hoặc điện thoại (phone-mic gateway).
- Chưa nối dialog mic-setup (OfferPhone) vào arena — flow nhắc micro hiện dùng câu thoại +
  gate có sẵn; nối đầy đủ là việc giai đoạn sau.
- Điểm vận hành (ngưỡng 0.60/0.40…) là số đóng băng của policy cũ; muốn tinh chỉnh phải
  theo quy trình WP mới + đo trên trẻ thật.
