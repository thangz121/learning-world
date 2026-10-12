# 01 — TTS PIPELINE (free, offline, self-checking) — WP-1.9.32

> **Tóm tắt (VI):** Chọn engine TTS MIỄN PHÍ cho gói nghe: Kokoro bị loại (chưa hỗ trợ Python
> 3.14); **Piper (MIT)** với voice **en_US-libritts_r-medium** (dataset LibriTTS-R, **CC BY 4.0**)
> được chọn — chạy CPU, offline, output không vướng license. Từ được sinh theo vòng
> "sinh N bản → ASR (faster-whisper base.en) chọn bản đọc ĐÚNG" để gói audio tự kiểm.

## Engine research (đã thử thật)

| engine | license code | kết quả trên máy này |
|---|---|---|
| Kokoro-82M | Apache-2.0 | **LOẠI** — pip `kokoro` yêu cầu Python <3.13; venv là 3.14.8 |
| sherpa-onnx TTS (VITS) | Apache-2.0 | dự phòng (đã cài sẵn) — không cần tới |
| gTTS/worker (Google Translate TTS) | free nhưng phụ thuộc dịch vụ online, xám ToS | không dùng cho gói mới |
| **Piper (piper-tts 1.8.0)** | **MIT** | **CHỌN** — cài được trên 3.14, CPU nhanh, 22.05 kHz |

- Voice: `en_US-libritts_r-medium.onnx` tải từ `rhasspy/piper-voices` (HuggingFace); model gốc
  **LibriTTS-R = CC BY 4.0** (được dùng thương mại kèm attribution). Model lưu NGOÀI repo
  (`D:\speech-lab\models\piper`); chỉ MP3 sinh ra được commit.
- Speaker: **id 100** — quét F0 (Praat/parselmouth): 214 Hz (nữ), so với các ứng viên
  174/181/215 Hz; chọn theo khoảng cách tới ~210 Hz.

## Quy trình sinh + tự kiểm (tools/pregen_listen.py)

1. Đọc `Content/listening/listen_garden.json` (từ, voice, rate, levels).
2. Với mỗi từ × {normal (length_scale 1.0), slow (1.25)}: sinh tối đa 6 bản; mỗi bản được
   **faster-whisper base.en** phiên âm; giữ bản ĐẦU TIÊN khớp chính xác từ (chuẩn hoá chữ).
   Không bản nào đạt → giữ bản gần nhất + in WARN (không bao giờ im lặng sai).
3. Hậu kỳ: **KHÔNG cắt silence mạnh** (top_db trim cắt mất phụ âm đầu yếu — "banana" từng bị
   nghe thành "and Anna"); chỉ thêm pad 60 ms, chuẩn hoá đỉnh 0.7, resample 24 kHz mono, ghi MP3
   (libsndfile — frame sync FF F3 hợp lệ cho validator).
4. Ghi đè idempotent các entry `listen_*` trong `Content/audio_manifest.json` + mirror
   `Assets/StreamingAssets/audio/manifest.json`.
5. **Convergence cache:** lần chạy sau giữ nguyên file đã self-check đạt (chỉ sinh lại file lỗi)
   → gói audio hội tụ dần và ổn định.

## Vì sao "grapes"

- "grape" số ít một-mình: 5/5 take ở ls 1.0 đều bị ASR nghe lệch ("Great./FAPE/Brape"), ls 1.1–1.2
  cũng vậy — onset /gr/ của speaker này quá yếu khi từ đứng một mình.
- "grapes" (chùm nho — khớp hình PropKit `grapes`) đạt self-check ổn định; `ListeningWords.TextEn`
  trả "grapes" cho id "grape".
- Piper có thành phần ngẫu nhiên nhẹ giữa các lần chạy — chính vì vậy vòng chọn-theo-ASR là cần
  thiết và file đã chọn được **đóng băng trong git**.

## Số liệu

- Self-check lần chốt: **16/16 khớp chính xác** (2 lần chạy liên tiếp đều 16/16, lần 2 toàn cache).
- Thời gian: ~2–4 phút/lần chạy đầy đủ trên CPU (load model + ASR + synth).
