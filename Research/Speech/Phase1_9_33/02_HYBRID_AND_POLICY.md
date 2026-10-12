# 02 — HYBRID BACKEND + POLICY (4 mức) — WP-1.9.33

> **Tóm tắt (VI):** `HybridSpeechAssessmentProvider` ghép engine acoustic offline (luôn chạy) với
> service nhận dạng local (khi chạy). Luật ghép FRR-first: mất service không lỗi; transcript bị
> BỎ khi audio mạnh+sạch mà lexical lệch hẳn (chống hallucination). Phán quyết 4 mức vẫn do
> `SpeakingPassPolicy` đóng băng (Perfect/Great/Almost/TryTogether ↔ Xuất sắc/Đúng rồi/Gần đúng
> rồi/Thử lại nhé) — không có ngưỡng mới nào được phát minh.

## Luật ghép (frozen trong code + test CT-S22D)

| tình huống | kết quả |
|---|---|
| capture lỗi / không có tiếng | trả kết quả acoustic nguyên trạng (service không được gọi) |
| service lỗi / transcript rỗng | acoustic-only (KHÔNG phải lỗi — game chạy tiếp) |
| transcript có + audio yếu/không sạch | gắn transcript (đường lexical: bắt đúng-từ/sai-từ) |
| transcript có + audio MẠNH+SẠCH (match ≥ 0.60, không cụt/đuôi yếu) + lexical < 0.40 | **BỎ transcript** (guard FRR-first: không phạt sản xuất tốt vì whisper nghe nhầm) |

- Sau ghép, mọi thứ đi qua policy cũ: lexical+confidence cho pass, `DecideAcoustic` khi không
  transcript, và cổng hạ cấp "lexical pass + acoustic xấu → Partial" (đã có từ trước).
- Provider id trong evidence: `hybrid/<service engine>` khi có transcript; giữ id acoustic khi không.

## Bốn mức (đã có trong base, nay nối vào game)

| SpeechLevel | Câu trẻ nghe (VI / EN) | Điều kiện điển hình |
|---|---|---|
| Perfect (Xuất sắc) | "Xuất sắc!" / "Excellent!" | StrongPass: hiểu rõ + bằng chứng mạnh |
| Great (Đúng rồi) | "Đúng rồi!" / "That's right!" | Pass |
| Almost (Gần đúng rồi) | "Gần đúng rồi!" / "Almost there!" | Partial / PossibleAttempt (thiếu đuôi, nhỏ, gần từ) |
| TryTogether (Thử lại nhé) | "Thử lại nhé!" / "Try again!" | chưa rõ/sai từ/nói nhỏ — KHÔNG BAO GIỜ là "fail" |

- `SpeakingFeedback.PhraseFor` là nguồn duy nhất của 4 câu (test khoá đúng chuỗi tiếng Việt).
- Nói nhỏ/quá yếu → câu riêng "Con nói to hơn nhé!" (vẫn đếm lượt, vẫn thân thiện).

## Offline & chi phí

- Acoustic: 100% offline, không model ngoài repo (bảng phoneme trong code + DTW/DSP).
- Service: localhost 8470, faster-whisper base.en (MIT) — KHÔNG cloud, KHÔNG trả phí; nếu không
  chạy, game đọc log "acoustic-only" và mọi vòng chơi vẫn hoạt động.
- Pregen/keys: prompt audio dùng đúng pack `listen_v1` của WP-1.9.32 (16 master offline).
