# v6.2 Patch — Contract sync + TRUE 3D lock (không code game, chỉ skeleton _SharedKernel)

> Đánh giá ngoài: 9/10, giữ 4-agent / online-optional / audio architecture / Worker / balanced voices / ownership / hybrid / W0 workflow. Sửa đúng 3 điểm + khóa TRUE 3D.

## Fix 1 — ARCHITECTURE.md đồng bộ Services typed
* §3 GameInstaller doc: thêm `IAudioDirector`, `ISpeechSynthesisProvider`, `INpcVoiceSelector`; `AzureSpeechProvider` → `AzureSttProvider`; Milo.Bind đủ 5 deps; lifetime rows audio; Agent A/B/C/D.
* §6 OUTPUT records typed đầy đủ 8 fields (`Pitch`, `SpeechStyle`, `AudioFormat`); `VocabularyAudioMode/SfxId/MusicId/AudioFocusMode`.
* §2/§4 Ids lines cập nhật. Verify: grep `string Mode/SfxId/MusicId/FocusMode/void Speak` trong ARCHITECTURE = 0 match.

## Fix 2 — GameInstaller skeleton hoàn chỉnh
* Mới `Assets/_SharedKernel/`: `Ids.cs`, `Events.cs`, `Services.cs` (interfaces y hệt contracts), `GameEventBus.cs` (impl đúng spec: no-throw publish, idempotent subscribe — CT-011/012), `BusBehaviour.cs`, `GameInstaller.cs`.
* Installer news đủ 10 services + `SwitchToFallbackSpeech()` (D gọi, không tự new) + `UseMockSpeechForTests()` (tests only). Constructor signature W0 ghi ngay trong header comment.
* Agent D không còn lý do tự tạo service ngoài Installer (lint rule 15 cover).

## Fix 3 — Worker freeze binary+headers
* `WorkerTtsContract.md` v6.2: 200 + audio binary + `Content-Type` + `X-Cache-Key/X-Cache-Status/X-Voice-Used`. Không còn option JSON base64.

## TRUE 3D lock
* Mới `docs/TRUE_3D.md` (10 sections nguyên văn, non-negotiable invariant).
* Refs: GAME_DESIGN pointer, VerticalSliceAcceptance True-3D 8-point test (flatten-2D-FAIL rule), AGENTS A/B/D responsibility lines.

## Không đổi (giữ theo đánh giá)
4-agent, online-optional EXE, audio architecture, Worker single boundary, balanced deterministic voices, C/D ownership, pre-gen+runtime hybrid, W0→parallel→integration.

## Còn lại cho W0-T0 (không thuộc patch)
Unity project scaffold (ProjectVersion 6000.x), service implementations theo signature GameInstaller, Unity test files theo TestManifest, Worker endpoint/auth config, pre-gen audio batch → ship gate xanh.
