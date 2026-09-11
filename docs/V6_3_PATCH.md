# v6.3 Patch — 3 điểm lên 9.7 (đánh giá ngoài)

## Fix 1 — Dòng Azure TTS cũ (ARCHITECTURE.md §1)
Cũ: `Speech: Azure AI Speech (STT + Pronunciation Assessment). TTS chậm cho kids.` Mới: Azure INPUT-only (STT+Assessment qua Router); Google TTS qua Worker là output duy nhất; TTS chậm = rate trong TtsRequest + pre-gen slow.

## Fix 2 — contract-tests phase-aware (không xanh giả)
* Pre-scaffold W0 (chưa có `ProjectSettings/ProjectVersion.txt`): SKIP có ghi rõ + release gate vẫn FAIL.
* Đã scaffold nhưng thiếu `Assets/Tests/EditMode`: FAIL.
* main branch thiếu test files: FAIL tuyệt đối. `release-gate.yml` thêm check đủ 16 file test.

## Fix 3 — SpeechProviderRouter (switch toàn game)
* Mới `Assets/_SharedKernel/SpeechProviderRouter.cs`: implement `ISpeechProvider`, delegate inner, `SwitchTo()` resubscribe event, idempotent, không throw.
* `GameInstaller`: `SpeechProvider => _speechRouter`; `SwitchToFallbackSpeech/Online/UseMock` gọi `router.SwitchTo(...)`. Consumer inject một lần, không kẹt Azure cũ.
* Docs sync: Services.md + ARCHITECTURE §3 snippet + AGENTS D + lint mở rộng (cấm new provider ngoài Installer).

## Giữ nguyên (10/10): True 3D, Standalone, 4-agent, contracts, audio 9.5.
## READY FOR W0: YES — 9.7/10.
