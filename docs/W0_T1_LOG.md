# W0-T1 Log — 4 agent song song (Lead integrated)

## Agent A (World): DONE
`ClickToMove` (NavMesh + idle timer + NavigationCompleted), `SmartCamera` (Follow/Interaction/Cinematic, constrained), `Interactable` (typed IDs, WordSeenEvent). Quyết định Lead: giữ layout flat (ARCH map Player/Camera subfolders bỏ qua); `NavigationCompleted.ByNpc` = caller-supplied; `Bind()` thay ctor (MonoBehaviour).

## Agent B (Brain): DONE + 5 tests xanh (harness local, cần Unity xác nhận)
`LearningService` FSM + dedup Meaningful tách exposure (CT-001 + CT-009 cùng pass), `HintService` monotonic + Reset im lặng, `QuestManager` (IQuestContentProvider nội bộ + fallback market_help_mia mirror JSON, AdvanceOnSeen không advance Speak / AdvanceOnSpoken cần Great+), `IntentEngine` keyword, `Milo.Bind` đúng signature GameInstaller. CT-001/002/004/005/010 xanh local.

## Agent C (Content): DONE
DTOs JsonUtility-compatible, `ContentDatabase` (parse + ValidateWordCount + BuildPreGenList metadata-only), README Part K. Quyết định Lead (defer W1): manifest header DTO, reward key per-NPC, pregen suffix convention, ValidateWordCount flag — giữ nguyên lựa chọn của C.

## Agent D (Audio): DONE + 5 tests xanh (Roslyn compile 0 err + 44 asserts)
Director (priority table + dedupe + queue + CTS), AssessmentPolicy, SafetyFilter, Cache SHA256, Translate provider (GET subset, chunk+concat, TtsException), Selector FNV1a balanced, 3 STT providers, `pregen_audio.py` + **probe Worker THÀNH CÔNG (200 audio/mpeg 6720B)**. Alias `CloudflareGoogleTtsProvider` giữ cho GameInstaller compile — W1 quyết định rename hay giữ.

## Lead verify
* Lint scans: không `new` service ngoài Installer (toàn comments), không Action<string>/ServiceLocator/voice-names (1 comment).
* Milo.Bind khớp GameInstaller. Validator authoring PASS. Bidir mapping PASS 16/16 (test files tồn tại).
* Unity EditMode run + Hub verify + pre-gen batch 51 files: sang W1 (cần máy dev có Unity + ffmpeg cho time-stretch slow).
