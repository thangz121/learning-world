# SESSION REPORT - WP-1.9.33

> **Tóm tắt (VI):** Lần đầu LẮP hệ thống NÓI vào game: trẻ nói → backend chấm (engine acoustic
> offline + service whisper local) → 4 mức "Xuất sắc / Đúng rồi / Gần đúng rồi / Thử lại nhé" +
> % nội bộ. Arena mới "Sân Khấu Nhí" (`speaking_stage`, English→Speaking), 8 từ dùng chung pack
> nghe (đã bổ sung phonemes cho 6 từ hoa quả). Suite **751/746/0/5**, build **Succeeded
> errors=0 warnings=0**, service smoke PASS trên WAV trẻ thật. Dựa trên base cũ: Recognizer/
> Policy/SpeechLevel/LocalAcoustic/phone-mic/pack nghe — tất cả tái dùng, không phát minh lại.

- **DATE:** 2026-10-12
- **MACHINE:** TECHVKITCHEN01 (Unity 6000.6.0f1)
- **BRANCH:** `main` (= `ux/math-arenas-hotfix-20260930`)
- **BASE COMMIT:** `ab77bb5` (WP-1.9.32 listening foundation)
- **FINAL COMMIT:** this commit
- **SCOPE:** English→Speaking foundation (game + backend chấm) — IMPLEMENTED, not accepted
- **PRODUCTION FLAGS:** untouched; no training; approved math games untouched.

## Mission

Trẻ nói từ tiếng Anh; backend nghe và phân tích "đã nói đúng từ đó chưa / đúng bao nhiêu %",
đánh giá bằng 4 mức (thử lại, gần đúng rồi, đúng rồi, xuất sắc) — dựa trên base đã làm.

## Work performed

1. **Recon base:** phát hiện 2 stack — stack Phase-2.x thật (`ISpeechRecognizer`,
   `SpeakingPassPolicy` với đúng 4 mức `SpeechLevel`, `LocalAcousticProvider` offline,
   `UnityMicrophoneCapture`/`NetworkMicrophoneCapture`) **chưa từng được dựng trong game**;
   stack legacy Azure stub không dùng.
2. **Benchmark ENGINE trên giọng trẻ thật (SO762 + Zenodo):** base.en vs small.en;
   token-recall câu trẻ 75.6% vs 77.0%; từ-đơn 39.2% vs 36.3%; hallucination khi im lặng phát
   hiện thật; latency 10–420ms → chọn base.en; thiết kế acounstic-primary + transcript-boost.
3. **Backend hybrid:** `LocalAssessServiceClient` (WAV+JSON thuần, test được) →
   `LocalServiceAssessmentProvider` (transcript+conf, honest) → `HybridSpeechAssessmentProvider`
   (merge FRR-first, guard chống hallucination, mất service không lỗi) → policy cũ quyết 4 mức.
4. **Service local free:** `tools/speak_assess_server.py` (faster-whisper base.en MIT, port 8470,
   no_speech gate) + `start_speak_assess.ps1`; warm-up nền từ game (`LocalAssessServiceLauncher`).
5. **Composition root:** `GameInstaller` dựng stack nói lần đầu (catalog 12 từ từ mirror
   `SpeakingPack` → acoustic + service + hybrid + `GateSpeechCapture` PC/phone → `SpeechRecognizer`).
6. **Gameplay "Sân Khấu Nhí":** arena + scene + Build Settings; 8 từ × 2 cấp, 3 lượt/từ, câu
   theo 4 mức + câu riêng cho nói-nhỏ/sai-từ/thiếu-mic; % nội bộ (log + property); lifecycle
   adopt; diorama cổng; prompt audio dùng chung `ListenPackAudio` (offline).
7. **Content:** +phonemes (CMUdict) cho banana/orange/carrot/tomato/watermelon/grape ("grape"→
   "grapes"); mirror runtime 12 từ + test khoá code==content.
8. **Tests:** CT-S22 (12 test) + cập nhật CT-S10 (7 games) + CT-P13 (12 từ); toàn bộ giữ xanh.

## Metrics

| item | value |
|---|---|
| EditMode suite | total 751 · passed **746** · failed **0** · skipped 5 (exit 0) |
| Production build | **Succeeded errors=0 warnings=0** |
| Service smoke (WAV thật) | health OK; trẻ bản ngữ 423ms/clip; silence → transcript "" (no_speech 0.78) |
| Benchmark | câu trẻ 75.6% token recall (base.en); từ-đơn trẻ-case lộn xộn 39.2% (sàn bi quan) |
| Offline | acoustic 100% offline; service optional; prompt audio pregen `listen_v1` |

## Decisions / gates

- Engine: **faster-whisper base.en** (MIT, local; small.en không hơn trên giọng trẻ, load 47s).
- Kiến trúc: acoustic-primary + transcript-boost, guard FRR-first — không đổi ngưỡng policy.
- 4 mức = `SpeechLevel` có sẵn; câu tiếng Việt/Anh qua `DialogueLang`; % chỉ ở log/property.
- Lỗi môi trường (mic/service) = không phải lỗi trẻ; lượt vẫn retryable, không hoàn thành giả.
- Không license trả phí; không cloud; nhóm 3 file TickCount cũ vẫn để nguyên (ngoài phạm vi).

## Files (xem §03 đầy đủ)

- Mới: `Assets/A_World/SpeakingStage/*`, `Assets/D_Audio/{LocalAssessServiceClient,
  LocalServiceAssessmentProvider, HybridSpeechAssessmentProvider, GateSpeechCapture,
  LocalAssessServiceLauncher}.cs`, `Assets/A_World/ListeningGarden/ListenPackAudio.cs`,
  `Assets/Tests/EditMode/CT-S22_SpeakingStage.cs`, `tools/speak_assess_server.py`,
  `tools/start_speak_assess.ps1`, scene `SpeakingStagePlayScene`.
- Sửa: LearningMap, GameInstaller, SelectionYardArea, SelectionYardBuilder, TargetPronunciationCatalog,
  EditorBuildSettings, ListeningGarden (dùng ListenPackAudio), CT-S10, CT-P13, 6 vocab JSONs.

## Git actions

- This commit: speaking foundation + reports + index update → push `main` (fast-forward).

## Open items / next steps

1. **QA người thật với mic/phone-mic** (chơi hết lượt, cảm nhận 4 mức + nhịp); ghi case cụ thể nếu
   chấm lệch để mở WP tinh chỉnh (đo trên trẻ thật, không sửa ngưỡng tùy tiện).
2. Nối panel mic-setup (OfferPhone) khi trẻ vào bàn nói (hiện có gate + câu nhắc).
3. Cân nhắc sentence-level (LV3+) khi từ đơn ổn định; và/hoặc nhiều giọng đọc mẫu.
4. Journey driver cho tuyến English (listen→speak) — cùng đợt với listening.
