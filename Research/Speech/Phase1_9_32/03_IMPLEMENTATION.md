# 03 — IMPLEMENTATION (listening foundation) — WP-1.9.32

> **Tóm tắt (VI):** Hệ thống "Vườn Tai Thính" đã nối đủ theo pattern arena hiện có:
> scene riêng + builder + game + 2 component click + logic thuần; LearningMap/GameInstaller/
> SelectionYardArea/SelectionYardBuilder đã nối; diorama cổng không chữ; 9 test EditMode mới.

## Files mới (Assets/A_World/ListeningGarden/)

| file | vai trò |
|---|---|
| `ListeningWords.cs` | registry 8 từ + voice/param/audio-id + text "grapes" + nhãn VI + prop |
| `ListeningRounds.cs` | logic thuần: pool/choices/rounds cho LV1–LV4, chọn đáp án + distractor + xoay vị trí (xác định, test được không cần scene) |
| `ListeningGardenBuilder.cs` | sân khấu: lawn/plaza/stage strip, entry beam + đĩa exit portal, chuông nghe-lại, pod hình (pedestal + PropKit/primitive), NavMesh bake, anchors, dressing |
| `ListeningGarden.cs` | vòng chơi: Wait→Play→Done; phát từ P1 (normal/slow), chuông, xử lý đúng/sai, tiến độ, lifecycle (adopt khi Completed), `SetLevelForTests` |
| `ListeningGardenChoice.cs` | pod click (IClickTarget) |
| `ListeningGardenBell.cs` | chuông click (IClickTarget) |
| `ListeningGardenPlayScene.unity` (+ meta, GUID trong Build Settings) | scene root `ListeningGardenWorld` (Transform only) — cùng kiểu bare-root như market/city |
| `Assets/Tests/EditMode/CT-S21_ListeningGarden.cs` | 9 test (A→I) |

## Wiring đã sửa

| file | thay đổi |
|---|---|
| `_SharedKernel/LearningMap.cs` | game id `listen_garden`, scene const, GameEntry (English→Listening, `Implemented`) |
| `_Bootstrap/GameInstaller.cs` | nhánh dispatch + `BuildListeningGardenPlayScene` (SetPlay, exit portal → yard, lifecycle bind) |
| `A_World/SelectionYard/SelectionYardArea.cs` | `ListeningLifecycle` + `ListeningGame` + `BindListeningGame` |
| `A_World/SelectionYard/SelectionYardBuilder.cs` | diorama `SYPreview_listen_garden` (chuông vàng + 2 thẻ hình) |
| `ProjectSettings/EditorBuildSettings.asset` | + ListeningGardenPlayScene (guid `b7e4a1c9…`) |
| `tools/validate_content.py` | pool voice + `listen_v1` |

## Nội dung/âm thanh đã sửa

- `Content/listening/listen_garden.json` (nguồn chuẩn: từ + level + rate + voice).
- `Content/audio_manifest.json` + `Assets/StreamingAssets/audio/manifest.json`: +16 entry `listen_*`.
- 16 master MP3 trong `Assets/StreamingAssets/audio/`.
- `tools/pregen_listen.py`: pipeline sinh + tự kiểm + cập nhật manifest (idempotent, có cache hội tụ).

## Test cập nhật

- `CT-S10` (LearningMap): Games 5→6, thêm asserts cho listen_garden, skip-list thêm `english_listening`.
- `CT-P08A` (inventory): 12→28 entry đã đóng gói.
- `CT-S21` (mới): đăng ký taxonomy; round planner (xoay đáp án, distractor không trùng, xác định);
  cổng yard + guard launch + back target; luồng arena (chuông, sai→slow replay, đúng→tiến vòng,
  SafetyFilter mọi câu nói); hoàn thành 1 lượt + lifecycle; khoá offline audio key; mirror nội dung
  JSON ↔ C#; scene pin + Build Settings; re-entry không nhân bản.
