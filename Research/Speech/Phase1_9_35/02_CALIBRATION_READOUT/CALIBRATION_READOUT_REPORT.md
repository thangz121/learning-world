# 02 — CALIBRATION READOUT REPORT (WP-1.9.35)

> **Document Status:** FROZEN READOUT REPORT (WP-1.9.35).
> **Authority:** Local Acoustic DSP Engine v2.2 evaluation on frozen child benchmark (WP-1.9.31 standard).
> **Date:** 2026-10-14
> **Reproducible Execution Command:**
> `& "D:\speech-lab\venvs\p0\Scripts\python.exe" "Research/Speech/Phase1_9_35/experiments/p35_readout_runner.py"`

---

## 1. Frozen Criteria Evaluation (c1–c4)

Đối chiếu trực tiếp trên tập kiểm thử độc lập **speechocean762 test split** (64 trẻ em, 1,280 câu nói, 4,326 tokens phụ âm cuối có gán nhãn bởi hội đồng chuyên gia độc lập):

| Tiêu chí đóng băng (WP-1.9.31) | Baseline (D0) | Biến thể D5_or_max (WP-1.9.31) | D5_or + stop-guard (0 delta trên bench; engine v2.2 chỉ thêm telemetry/guard) | Ngưỡng bắt buộc | Kết quả Thẩm định |
|---|---|---|---|---|---|
| **c1: FRR $\le \text{baseline} + 0.05$** | 8.65% | 7.68% | **7.68%** | $\le 13.65\%$ | **PASS** |
| **c2: Missing-evidence giảm $\ge 25\%$** | 16.54% | 12.19% | **12.19%** | $\le 12.40\%$ (−26.3% tương đối) | **PASS** |
| **c3: FAR $\le \text{baseline} + 0.05$** | 54.26% | 42.55% | **42.55%** | $\le 59.26\%$ | **PASS** |
| **c4: Đồng đều ở cả 2 speaker pilot** | Fail | PASS | **PASS** | Cả 2 pilot speakers đều cải thiện | **PASS** |
| **c5: An toàn UX trẻ em (Pin Test)** | N/A | N/A | **100% Khóa an toàn** | Quyết định trao sao không bị ảnh hưởng | **PASS** |

---

## 2. Frozen Cryptographic Hashes (Tính toàn vẹn & Tái lập 100%)

Tất cả các tệp nhãn gốc, trích xuất đặc trưng và artifact đầu ra được niêm phong bằng hàm băm SHA-256:

```json
{
  "expert_labels_bench_test_sha256": "810148C85B6DD4A9D58FC2584F87C1CFBE66EC23DA244D31B396F198B36A8E52",
  "bench_test_features_sha256": "E85676B9A6196C14438FF4F1679E7B1B341AA0CB57AC7485F3BDB1BAB799F4A7",
  "eval_readout_sha256": "3B1FC4DF1E21B4ADE6B56E4D07CC3E6EEDA94249FA8B197909CFE6416A0A9B37",
  "per_phone_csv_sha256": "1A6D259B08FF2D69EA0013DFCDFA5EF5D67F417F4C3DA3AC0B11CACA96781A55",
  "per_speaker_csv_sha256": "344C34C11EDE45EC91781FCCB3615CC4C50C6ECA3311B72EDC684195320A219E"
}
```

---

## 3. Phân tích Kỹ thuật, Minh bạch Dữ liệu & Nhận xét Chuyên môn

1. **Minh bạch về kết quả Benchmark (Benchmark Honesty & Transparency)**:
   - Runner Python (`p35_readout_runner.py`) đọc trực tiếp bảng đặc trưng tĩnh `bench_test_features.csv` từ WP-1.9.31, không trực tiếp chạy engine C# trên sóng âm (WAV) của toàn bộ 4,326 tokens.
   - Nhánh `D5_or + stop-guard` trên bảng đặc trưng tĩnh cho ra kết quả **0 delta** so với `D5_or_max` ($7.68\% / 12.19\% / 42.55\%$). Không tồn tại việc giảm metric xuống $6.92\% / 11.45\% / 41.80\%$ trên tập đóng băng này. Báo cáo ghi nhận trung thực $7.68\% / 12.19\% / 42.55\%$ là số đo thực tế.
   - Toàn bộ code chết `alpha` (VTLN) và các chú thích sai lệch về việc áp dụng bộ lọc VTLN trên bảng đặc trưng CSV đã được dọn sạch khỏi runner.
2. **Vai trò Thực tế của Local Acoustic DSP Engine v2.2**:
   - Engine C# (`EnglishAcousticDspEngine.cs`) vận hành trực tiếp trên luồng PCM 16kHz trong Unity runtime, thực thi các thuật toán âm học:
     - Đo lường thời gian bật hơi Onset VOT và kiểm định hiện tượng chèn âm đệm Schwa.
     - Kiểm định tụt Formant $F_3$ của nguyên âm cong lưỡi (Rhoticity).
     - Bắt xung nổ Release Burst và năng lượng xát cao tần của phụ âm đuôi (Coda).
     - Phân tích tương phản trọng âm từ (Stress Contrast Index).
   - Theo triết lý **Child UX Safety Guard (FRR-First)**: Toàn bộ các mức phạt âm học nội bộ chỉ được ghi vào `EnglishPhoneticPayload` dưới dạng Telemetry phục vụ giáo viên và dashboard theo dõi. Quyết định đạt/hỏng của trẻ 4 tuổi (`SpeakingPassPolicy`) được bảo vệ an toàn để khích lệ trẻ học tập.
3. **Kiểm thử Native Fixture mở rộng**:
   - Bộ kiểm thử EditMode `CT-P17x_EnglishAcousticDspEngine` đã tích hợp kiểm thử hàng loạt trên 12 file WAV trẻ bản ngữ 4.9 tuổi từ Zenodo 200495.
   - Tỷ lệ bắt lỗi oan (False-Flag Rate) trên các clip trẻ bản ngữ nói đúng đạt **0.0%** (0/12 clips bị gắn cờ sai).

---

## 4. Trạng thái Kiểm thử & Sẵn sàng Gộp (WP-1.9.35 Ready)

- [x] Header docstring v2.2 cập nhật chuẩn xác per-phoneme VOT và VTLN $\alpha$.
- [x] Unity EditMode Test Runner: **696 total tests (691 passed, 0 failed, 5 skipped/ignored)**.
  - `CT-P15_ChildAudioFrontend`: 16/16 passed (bao gồm kiểm thử độ trễ `< 5ms`).
  - `CT-P17x_EnglishAcousticDspEngine`: 11/11 passed (bao gồm kiểm thử độ trễ `< 8ms`, 12 native clips Zenodo).
- [x] Đã chuẩn hóa runner `p35_readout_runner.py` (loại bỏ dead code, nhãn trung thực D5_or + stop-guard, 0 delta trên bench).
- [x] Đã niêm phong đầy đủ bảng băm SHA-256 tự nhất quán trong `frozen_hashes.json`.
- [x] Đã có báo cáo thẩm định âm học Human Review trên 4 clip thực tế (`HUMAN_REVIEW_REPORT.md`).
- [x] Standalone Windows 64-bit build: `errors=0, result=Succeeded`.
- [x] **Sẵn sàng để Product Owner gộp nhánh `wp-1.9.35` và push lên remote.**
