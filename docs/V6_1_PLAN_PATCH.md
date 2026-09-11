# v6.1 Plan Patch — Plan-Only (không code game production)

> Audit trên `little-world-english-docs-v6.zip` (94 files, cấu trúc khớp workspace). Mọi lỗi A–L được xác thực tồn tại trước khi sửa. Patch này chỉ khóa contract/docs/content-metadata/tooling/CI — `Assets/` Unity vẫn chưa tồn tại theo đúng thiết kế (tạo ở W0-T0).

## 1. Triage

**BLOCKER (chặn release/CI thật — không che giấu, giải quyết ở W0):**
* B1: Chưa có Unity project (`Assets/`, `ProjectSettings/ProjectVersion.txt` không tồn tại) → game-ci không thể chạy. Đã thêm preflight fail rõ ràng + task W0-T0 scaffold Unity 6 LTS.
* B2: Worker endpoint/auth chưa biết → `WorkerTtsContract.md` để placeholder `REQUIRES USER CONFIG`; D chưa implement provider thật được.
* B3: Chưa có file audio thật → ship gate đỏ theo đúng thiết kế (pre-gen pipeline chạy W0 sau khi có Worker).
* B4: Chưa có Unity test impl → release gate đỏ; skeleton tạo ở W0-T0 (test-first).

**HIGH (đã fix trong patch):**
* H1 FIX-001: `ISpeechProvider.Speak()` song song với `IAudioDirector` → đã xóa, INPUT-only.
* H2: `string Mode/SfxId/MusicId/FocusMode/Archetype` → typed enums/IDs.
* H3: `TtsRequest` thiếu Pitch/Format so với cache key → đủ 8 fields.
* H4: Worker boundary chưa freeze → `WorkerTtsContract.md` + fallback table.
* H5: Validator không check file thật → two-phase (authoring / `--ship`).
* H6: contract-tests chỉ grep tên → mapping 2 chiều + `TestManifest.md`.
* H7: unity-build thiếu preflight/artifact/license rõ → preflight + upload Windows64.
* H8: 9 dialogue thiếu tự nhiên (`Welcome here`, `find apple`, `That's apple`...) → đã sửa trong giới hạn từ.

**MEDIUM (đã fix):** M1 manifest count/dup/compat checks; M2 NpcVoice determinism spec; M3 interruption policy table; M4 platform checklist; M5 pillar acceptance; M6 pipeline ownership C/D; M7 doc sync TestManifest.
**LOW:** L1 version strings lịch sử trong headers (giữ nguyên); L2 bump title plan v6.1 (1 dòng).

## 2. Per-file change log

| File | Reason | Exact change | Owner | Acceptance |
|------|--------|--------------|-------|------------|
| docs/contracts/Services.md | H1,H2,H3,M2 | Xóa `Speak()`; typed audio enums; TtsRequest 8 fields; INpcVoiceSelector deterministic spec | Lead | grep hết `Speak(` trong ISpeechProvider; CT-006 |
| docs/contracts/Ids.md | H2 | Thêm SfxId/MusicId/VocabularyAudioMode/AudioFocusMode/NpcArchetype/SpeechStyle/AudioFormat/AudioInterruption | Lead | CT-007 |
| docs/contracts/Events.md | H2 | VocabularyPlayed.Mode typed | Lead | compile W0 |
| docs/contracts/WorkerTtsContract.md (NEW) | H4 | Request/response/error table, placeholder REQUIRES USER CONFIG | D+Lead | user điền endpoint/auth mới implement |
| docs/AUDIO_DESIGN.md | M2,M3,H3,K | Interruption table, determinism, TtsRequest 8f, pipeline ownership | D+Lead | CT-A02 spec rõ |
| docs/ARCHITECTURE.md | sync | Structure 4-agent, INPUT/OUTPUT split, audio schema, CI v6.1 | Lead | lint pass |
| docs/AGENTS.md | K | Ownership audio file C/D + 4-agent giữ nguyên | Lead | CODEOWNERS |
| docs/testing/VerticalSliceAcceptance.md (NEW) | M4,M5 | Standalone checklist + pillar criteria | Lead+QA | playtest gate |
| docs/testing/TestManifest.md (NEW) | H6 | CT ↔ Unity file mapping 16 dòng | Lead | CI bidir |
| docs/testing/ContractTests.md | M7 | Link TestManifest | Lead | ID check |
| tools/seed_content.py | H8,C | 9 dialogue fixes + audio generated flag + manifest audio paths | C | regen + validator |
| Content/vocab/*.json (50 regen) | C | audio block `{normal,slow,voice,lang,generated:false,approved:false}` | C | validator authoring PASS |
| Content/dialogues/manifest.json+milo.json (regen) | H8,C | text tự nhiên + audio mapping + approved:false | C | word-count PASS |
| tools/validate_content.py | H5,M1,D | Two-phase + file check + dup/compat + anti hand-approved | Lead | authoring PASS / --ship FAIL đúng + negative test PASS |
| .github/workflows/unity-build.yml | H7 | preflight ProjectVersion + Windows64 artifact + license rõ | Lead | PR fail rõ khi thiếu scaffold |
| .github/workflows/contract-tests.yml | H6 | bidir doc↔TestManifest↔Unity (SKIP rõ khi chưa có tests) | Lead | không xanh giả |
| .github/workflows/release-gate.yml (NEW) | C | `--ship` gate cho tag/dispatch | Lead | đỏ tới khi có audio+tests |
| docs/V6_1_PLAN_PATCH.md (NEW) | — | File này | Lead | — |

## 3. Invariants freeze (v6.1)

1. Một đường TTS duy nhất: Gameplay→IAudioDirector→Resolver→Cache/Worker. 2. Typed IDs/enums, raw string chỉ ở boundary. 3. Cache key = 7 fields trong TtsRequest. 4. Worker là boundary duy nhất, `{lang}` bắt buộc, Slice `en-US`, không tên Google trong gameplay/content. 5. Ship cần generated+approved+file thật; approved tay không file = fail mọi mode. 6. NPC ≤6 / Milo ≤8 từ; word-count phục vụ quality, QA tay vẫn bắt buộc. 7. Hint timer 8.0/15.0s + wrong 3/5/7/9; state per-quest reset. 8. NPC voice balanced-deterministic theo NpcId + persist; Milo/Mia/Learning fixed. 9. Interruption table Part H là luật duy nhất. 10. CT-001..012 + CT-A01..04 + TestManifest 2 chiều. 11. Online-optional EXE; realtime fail không chặn core. 12. 4-agent + CODEOWNERS.

## 4. READY FOR W0? YES

Không còn contract mơ hồ nào chặn 4 agent code song song. 4 BLOCKER là input/thứ tự W0, không phải lỗ plan.

**W0 order (song song an toàn):**
* W0-T0 Lead: scaffold Unity 6 LTS (`ProjectVersion.txt` 6000.x, URP, Addressables) + GameInstaller + EventBus + TestManifest skeletons (red-first) + thu Worker config + UNITY_LICENSE secret.
* W0-T1 A: greybox + ClickToMove + SmartCamera + Interactable (Director stub).
* W0-T1 B: QuestManager + HintService/State + Tier1/2 + DialogueRequest (không fetch audio).
* W0-T1 C: shootout QA list + approval intent.
* W0-T1 D: AudioDirector + Resolver + cache L1/L2 + Mixer + provider interface + Fallback + Selector + pre-gen script (chạy thật khi có Worker).
* W0-T2 Lead: integrate + authoring CI xanh + shootout batch → freeze Google mapping → W1.
