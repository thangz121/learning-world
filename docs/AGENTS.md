# AGENTS.md — Cách chạy 4 Agent + 1 Lead (v6 audio-first, machine-executable)

> v6: 4 agents (A World&Visual, B NPC&Gameplay, C Learning Content, D Audio&Speech). Gameplay chỉ gọi `IAudioDirector`; cấm gọi Worker/Google/TTS trực tiếp. Xem `contracts/` + `testing/` + `AUDIO_DESIGN.md`.

## 0. Sơ đồ

```
        PRODUCT.md → GAME_DESIGN.md → ARCHITECTURE.md → AUDIO_DESIGN.md → CONTRACT
                                        |
                                 LEAD / INTEGRATOR (Agent 0)
                                        |
          +----------------+----------------+----------------+
          |                |                |                |
     Agent A WORLD    Agent B NPC      Agent C CONTENT  Agent D AUDIO
     & Visual         & Gameplay       Learning         & Speech
     Player+Camera    Tier1/2+Milo     Text+Progress    Director+TTS
     Scene+UI         Quest+Hint       Repetition+QA    Mixer+Cache
```

## 1. Agent 0 — LEAD ARCHITECT & INTEGRATOR

Nhiệm vụ: sở hữu `_SharedKernel` + `docs/adr/` + `docs/contracts/`, review contract, pull 4 nhánh, compile local + chạy test local, fix integration, merge develop/main (v6.4: không CI license, Unity gate chạy local).

Checklist mỗi lần integrate:
* [ ] Local Unity compile clean (Editor, không cần CI license — v6.4 license-free)
* [ ] CT-001..CT-012 + CT-A01..A04 xanh (testing/ContractTests.md)
* [ ] Content+Audio validator pass (Active 15 / Passive 35 / audio.approved / pack 30–40)
* [ ] Không file ngoài CODEOWNERS, không lib cấm
* [ ] Không `Action<string>` / string ID thô trong gameplay (lint pass)
* [ ] Không `new` service ngoài GameInstaller/test; không gọi Worker/Google trực tiếp từ A/B/C
* [ ] VoiceProfile dùng ID (`milo_v1`...), không tên Google trong gameplay/content

Branch: `main (locked) ← develop ← a/world-visual, b/npc-gameplay, c/learning-content, d/audio-speech`
CODEOWNERS:
```
Assets/A_World/* @agentA
Assets/B_Brain/* @agentB
Assets/C_Content/* @agentC
Assets/D_Audio/* @agentD
Assets/_SharedKernel/* @lead
Assets/_Bootstrap/* @lead
Content/* @agentC
docs/adr/* @lead
tools/validate_content.py @lead
```

## 2. Agent A — WORLD & VISUAL

> Chỉ `Assets/A_World/`. Nhận dependency qua constructor từ GameInstaller, cấm new service, cấm gọi TTS/Worker.

**Allowed:** UnityEngine, AI.Navigation, SharedKernel Interfaces (read-only), Content Readers (wordId/prefab).
**Forbidden:** Azure/Google SDK, Worker URL/key, Learning/Quest/Speech/Audio implementation, `FindObjectOfType<Service>`, static EventBus.
**Constrained 3D (CONSTRAINED_3D.md):** topology vùng gameplay, perspective camera context-driven (follow/interaction/cinematic, không bắt buộc 360/collision phức tạp), lighting/depth, NPC embodiment/animation, spatial layout — cấm hotspot-2D/billboard.
**Input:** WordSeenEvent, HintState, QuestVisualState. **Output:** InteractEvent / WordSeenEvent(Source=Object) / NavigationCompletedEvent. Audio: đặt spatial emitter, route qua Director.

**Acceptance:**
```
Given táo Active trên kệ + player ở cửa
When click táo
Then ≤5s: navigate tới kệ (không kẹt), camera InteractionMode, publish WordSeenEvent(apple, Object)
Given idle total 8.0s
Then visual hint L1; 15.0s → Milo point L2 (B/D phối hợp, A chỉ làm visual)
```

## 3. Agent B — NPC & GAMEPLAY (Tier3 stub)

> Chỉ `Assets/B_Brain/`. Implement ILearningService? Không — B dùng Learning qua C cung cấp schema + MasteryFSM ở SharedKernel/B? Giữ: B implement Quest+Hint+NPC, đọc mastery qua interface. Tier3 Memory stub schema only.

**Allowed:** SharedKernel, Content JSON (text/objective), SafetyFilter, SpeechPolicy Assess().
**Forbidden:** Azure/Google/Worker trực tiếp, Camera/Player, Save, fetch audio, LLM không Validator, full history, string action.
**True 3D:** player navigation 3D trong vùng cho phép (NavMesh X/Y/Z, không ray 2D), interaction với object thật trong world (không hotspot→popup), NPC movement/orientation/look-at.
**Input:** InteractEvent, SpeechResult, totalIdle. **Output:** QuestState, HintLevel (freeze 8.0/15.0 + wrong 3/5/7/9), `DialogueRequest` (text+VoiceProfileId, KHÔNG fetch audio), publish Quest events.

**Acceptance:**
```
Given Q1 bước find_apple
When WordSeenEvent(apple,Object) + ReportAction(PlayerAction.Find, apple)
Then objective find done, qua bring, không complete cả quest
Given wrong x3 cùng quest
Then L1 visual; Reset(quest mới) về 0
```

## 4. Agent C — LEARNING CONTENT

> `Assets/C_Content/` + `Content/`. Sở hữu text/progression/repetition/QA metadata (`approved`), KHÔNG fetch/play audio.

**Allowed:** SharedKernel readers, Content JSON, MasteryFSM rules.
**Forbidden:** TTS/Worker/Mixer, Camera/NPC control, tên Google voice trong content (chỉ VoiceProfileId).
**Input:** learning events. **Output:** Content pack + mastery rules + `audio.*` metadata (file/voice/approved) để D pre-gen.
*Ownership audio file (Part K): C sở hữu text + approval intent; D sở hữu generation/QA/file asset. C sửa text → D regen, không sửa cùng file.*
**Acceptance:**
```
Given Content/vocab
When validator chạy
Then Active==15, active có tags+expectedForms+audio.normal/slow/voice/lang/approved=true; quest target tồn tại
Given dialogue pack
When đếm manifest
Then 30–40 câu, mỗi câu có voice + word-count đúng (NPC≤6, Milo≤8)
```

## 5. Agent D — AUDIO & SPEECH

> Chỉ `Assets/D_Audio/`. Sở hữu Director, Resolver, providers, cache L1–L4, Mixer/Focus, VoiceProfiles/Selector, QA/pre-gen tools.

**Allowed:** Unity Audio, HTTP tới Worker (duy nhất `CloudflareTranslateTtsProvider`), Azure STT SDK, Safety rules.
**Forbidden:** Quest/Learning/NPC logic, World prefab, lưu raw audio default, gửi child id + raw voice cho LLM, tên Google voice trong gameplay (chỉ ở provider/config).
**v6.3:** gameplay inject `ISpeechProvider` (= Router) một lần; switch Azure↔Fallback qua `GameInstaller` (inside router) — cấm `new` provider và cấm cache instance riêng để tự switch.
**Input:** DialogueRequest/TtsRequest (Text, Voice, Lang en-US, Rate, Pitch, Style, Format, Priority). **Output:** phát đúng voice/priority/focus; cache key SHA256(text+voice+locale+rate+pitch+style+format).
**Constrained 3D:** spatial audio (world position, distance attenuation, directional) cho NPC/object/ambient trong vùng gameplay; pronunciation giữ focused clarity.
*Ownership (Part K): D sở hữu toàn bộ file audio generated + QA pipeline + pre-gen Worker calls.*

**Acceptance:**
```
Given TTS "abo" pron~60
When Policy Assess
Then Almost + Milo chậm, không điểm số
Given offline
When Azure fail
Then FallbackSpeechProvider (Offline Intent Mode), pre-gen core vẫn phát; Mock chỉ test
Given cùng NpcId 2 session
When spawn lại
Then cùng VoiceProfile (balanced persist); Milo/Learning/Mia fixed
```

## 6. Definition of Done Slice

* [ ] Click táo Active → InteractionMode → WordSeenEvent + phát `apple_normal/slow` đúng LearningVoice
* [ ] Help Mia 3 bước → QuestCompletedEvent → hoa mọc + câu celebrate pre-gen zero latency
* [ ] Nói apple → đúng level khích lệ, không Wrong/điểm
* [ ] 8.0s → L1, 15.0s → L2, stuck → demo/simplify; realtime TTS fail không chặn quest
* [ ] Offline: core vocab/dialogue phát local đầy đủ
* [ ] 10 phút ≤2 interventions + ≥2 Meaningful + 1 Transfer (apple không visual)

## 7. Tests (Lead chạy — freeze CT + CT-A)

```
CT-001..CT-012 (core) + CT-A01 CacheKey_UniquePerVoice + CT-A02 Focus_DucksMusicAmbient
+ CT-A03 OfflineFallback_QuestContinues + CT-A04 NpcVoice_IdentityPersist
```

## 8. Roadmap 8 tuần (4-agent)

W0 Freeze (docs+contracts+content+validator+CI) → W1 Greybox (A) + Mock audio (D) → W2 Quest phím (B) + pre-gen pack (C/D) → W3 NPC+Learning FSM → W4 Voice thật (STT+TTS Worker) → W5 Art+Mix polish → W6 Playtest1 (3 bé) → W7 Fix → W8 Playtest2 (5–8 bé) → Go/No-Go.
