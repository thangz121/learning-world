# 01 — ENGINE BENCHMARK (nhận dạng từ trên GIỌNG TRẺ THẬT) — WP-1.9.33

> **Tóm tắt (VI):** Thử faster-whisper base.en vs small.en (CPU int8, free/MIT) trên giọng trẻ
> thật: câu trẻ (SO762, 100 câu) token-recall **75.6% / 77.0%**; từ-đơn trẻ (Zenodo numbers, set
> lộn xộn) chỉ **39.2% / 36.3%** + CẢNH BÁO hallucination khi im lặng ("Bye bye."). Độ trễ
> ~10–420ms/clip. Kết luận: chọn **base.en** (small không hơn, nạp chậm 47s); **engine acoustic
> offline là thẩm phán chính, transcript là tín hiệu bổ trợ có cổng chống hallucination**.

## Số đo (máy này, CPU)

| test | base.en | small.en |
|---|---|---|
| Câu trẻ SO762 (100 câu): token recall | 418/553 = **75.6%** | 441/573 = **77.0%** |
| Câu trẻ SO762: câu đúng hoàn toàn | 41/100 | 37/100 |
| Từ-đơn trẻ (Zenodo số 1–10, 102 clip) | **39.2%** | 36.3% |
| Nạp model | ~0.8 s | ~47 s |
| Độ trễ suy luận | ~10ms (câu ngắn) … ~420ms (clip thật qua HTTP) | tương tự |

- small.en KHÔNG cải thiện trên giọng trẻ (thậm chí kém hơn ở set lộn xộn) → chọn base.en
  (đã có sẵn từ WP-1.9.32, không tải thêm).
- **Hallucination khi im lặng:** whisper tạo câu vô nghĩa ("Bye bye.", "Mmm.") trên clip gần-im —
  vì vậy service chặn transcript khi `no_speech_prob > 0.5` (đo thật: silence → transcript "").
- Set từ-đơn Zenodo là bản ghi phiên HRI lộn xộn (người lớn + trẻ + tạp âm), không phải từ sạch;
  39% là SÀN bi quan, không phải chất lượng thật của từng từ đơn sạch.
- Vosk không cần: base.en nhanh + đã local + nhánh acoustic đã là chính; thêm engine = thêm phức tạp.

## Hệ quả thiết kế

1. Acoustic engine (closed-set, target-constrained) quyết định chính — như thiết kế gốc của
   `SpeakingPassPolicy` (nhánh `DecideAcoustic` đã đóng băng từ trước).
2. Transcript chỉ vào cuộc khi: (a) audio không "mạnh + sạch", hoặc (b) transcript khớp gần từ
   mục tiêu; bị BỎ khi audio mạnh+sạch mà lexical quá thấp (guard FRR-first, xem §02).
3. `no_speech` gate ở service là tuyến phòng thủ thứ hai; tuyến thứ nhất là VAD/energy của
   capture + policy (đã có).

## Lệnh tái lập

- Benchmark: script tạm `speak_bench2.py` (mẫu trong báo cáo này; chạy với venv p0).
- Service: `python tools/speak_assess_server.py --port 8470` (hoặc `tools/start_speak_assess.ps1`).
