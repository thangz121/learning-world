# 03 — IMPLEMENTATION (speaking foundation) — WP-1.9.33

> **Tóm tắt (VI):** Hệ thống nói đã được LẮP vào game lần đầu tiên: stack speech (Recognizer/
> providers/captures) trước đây chỉ tồn tại trong test, nay được composition root dựng thật;
> thêm arena "Sân Khấu Nhí", service local, provider hybrid, capture gate, 6 từ hoa quả được
> bổ sung phonemes, 12 test EditMode mới.

## Code mới

| file | vai trò |
|---|---|
| `Assets/A_World/SpeakingStage/SpeakingPack.cs` | 8 từ + kế hoạch 2 cấp + mirror phonemes 12 từ + 4 câu feedback (pure) |
| `Assets/A_World/SpeakingStage/SpeakingStageBuilder.cs` | sân khấu: rèm, micro vàng + vòng sáng, bục hình, cổng vào/ra, NavMesh, anchors |
| `Assets/A_World/SpeakingStage/SpeakingStage.cs` | vòng chơi: Wait→Prompt→SayReady→Listening→Feedback, 3 lượt/từ, câu theo mức, lỗi môi trường thân thiện, % nội bộ, lifecycle |
| `Assets/A_World/SpeakingStage/SpeakingStagePlayScene.unity` (+meta) | scene bare-root (GUID `c8f3b2a1…` trong Build Settings) |
| `Assets/A_World/ListeningGarden/ListenPackAudio.cs` | helper phát prompt dùng chung 2 game (listen-pack voice, offline) |
| `Assets/D_Audio/LocalAssessServiceClient.cs` | HTTP client + WAV encode (pure, test được) + JSON parse |
| `Assets/D_Audio/LocalServiceAssessmentProvider.cs` | provider transcript từ service (honest: transcript+conf; lỗi = provider_error) |
| `Assets/D_Audio/HybridSpeechAssessmentProvider.cs` | ghép acoustic + service, guard FRR-first |
| `Assets/D_Audio/GateSpeechCapture.cs` | chọn capture theo MicSetupGate (PC / điện thoại / mic_unavailable) |
| `Assets/D_Audio/LocalAssessServiceLauncher.cs` | warm-up service nền (best-effort, không block) |
| `tools/speak_assess_server.py` | service local: /health + /assess, faster-whisper base.en, chặn no_speech |
| `tools/start_speak_assess.ps1` | chạy service thủ công |
| `Assets/Tests/EditMode/CT-S22_SpeakingStage.cs` | 12 test (A→L) |

## Wiring đã sửa

| file | thay đổi |
|---|---|
| `_SharedKernel/LearningMap.cs` | game `speaking_stage` → `english_speaking`, scene const, `Implemented` |
| `_Bootstrap/GameInstaller.cs` | **lần đầu dựng stack nói thật**: catalog (12 từ) → LocalAcousticProvider + service provider + hybrid + capture gate (PC/phone) → `SpeechRecognizer`; dispatch + `BuildSpeakingStagePlayScene`; warm-up service nền |
| `A_World/SelectionYard/SelectionYardArea.cs` | `SpeakingLifecycle` + `SpeakingStage` + `BindSpeakingStage` |
| `A_World/SelectionYard/SelectionYardBuilder.cs` | diorama `SYPreview_speaking_stage` (mic vàng + sao) |
| `C_Content/TargetPronunciationCatalog.cs` | + `Register(word, phonemes)` (additive, cho runtime mirror) |
| `ProjectSettings/EditorBuildSettings.asset` | + SpeakingStagePlayScene |
| `Content/vocab/{banana,orange,carrot,tomato,watermelon,grape}.json` | + `speech.phonemes` (+ expectedForms; "grape" = "grapes") |
| `Assets/A_World/ListeningGarden/ListeningGarden.cs` | PlayWord dùng chung `ListenPackAudio` |

## Test cập nhật

- `CT-S10`: Games 6→7, asserts cho speaking_stage, skip-list + `english_speaking`.
- `CT-P13`: catalog thật 6→12 từ (2 chỗ liên quan; chỗ scripted 6 từ giữ nguyên).
- `CT-S22` (mới): đăng ký + yard + preview; pack/rounds/4 câu; mirror content 12 từ;
  hybrid merge (4 tình huống + guard); service provider mapping (gọi/không gọi/khi lỗi);
  WAV encode + JSON parse; gate capture định tuyến PC/phone; arena flow (Perfect→"Xuất sắc!",
  Great→"Đúng rồi!", retry 3 lượt→sang từ, hoàn thành lượt + lifecycle); lỗi môi trường
  (2 lần → dừng nhẹ, lifecycle retryable); re-entry; scene pin.
