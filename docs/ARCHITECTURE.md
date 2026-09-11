# ARCHITECTURE.md — Architecture Bible (v6: 4-agent + audio-first)

> v6: 4 agents (A World&Visual, B NPC&Gameplay, C Learning Content, D Audio&Speech). TTS Google qua Worker duy nhất. Chi tiết audio: `AUDIO_DESIGN.md` + `ADR-007`.

## 1. Stack + Quyết định DI (xem ADR-002)

* Unity 6 LTS (pin 1 version) + URP + Addressables + NavMesh
* C# .NET Standard, **LangVersion 9.0 (verified, NOT assumed from Editor version)** — Unity 6000.6.0f1 emits `-langversion:9.0` for every assembly (verified `Library/Bee/.../LWE.*.rsp`), no `csc.rsp` override exists, asmdefs carry no LangVersion field. Canonical policy: write C# 9-compatible code only. BANNED: `record struct`/`record class` (C# 10, CS8773), `with` expressions, `global using`, file-scoped namespaces, `init`-only setters without shim, `required` members, raw string literals. Do NOT "fix" by adding `csc.rsp -langversion:10`: unofficial, unsupported on players/build farm, needs per-assembly `IsExternalInit` shims, and Unity serialization still rejects records.
* C# .NET Standard, **Manual Composition Root — KHÔNG dùng VContainer/Zenject ở W0**. Lý do: rõ ràng hơn với AI coding agent, ít magic, dễ review. Có thể cân nhắc DI framework ở Phase 2.
* Speech INPUT: Azure STT + Pronunciation Assessment duy nhất qua `ISpeechProvider` (router). Speech OUTPUT: Google TTS qua Cloudflare Worker là TTS duy nhất (`ISpeechSynthesisProvider`, Agent D). Azure không làm TTS. TTS chậm cho kids thực hiện bằng rate trong `TtsRequest` + pre-gen slow audio.
* Save Slice: local JSON. Backend cloud Phase 2.

## 2. Project Structure + Ownership

```
Assets/
  _Bootstrap/          // @lead-only — composition root assembly (LWE.Bootstrap)
    GameInstaller.cs    // Manual CompositionRoot duy nhất được new service
    LWE.Bootstrap.asmdef // refs: SharedKernel + World + Brain + Content + Audio.
                         // Feature assemblies MUST NOT ref Bootstrap (no cycle).
  _SharedKernel/        // @lead-only, freeze, dependency-light (no Brain/Audio/World/Content refs)
    GameEventBus.cs     // IGameEventBus impl (Dictionary<Type, Delegate>)
    LocalSave.cs        // ISaveService impl (local JSON, persistentDataPath/lwe_save.json)
    Events.cs           // typed readonly structs (gameplay + audio, C# 9)
    Ids.cs              // WordId, QuestId, NpcId, InteractionId, VoiceProfileId, LanguageCode
    ILearningService.cs
    ISpeechProvider.cs + ISpeechPolicy.cs   // INPUT: STT/Assessment
    IAudioDirector.cs + ISpeechSynthesisProvider.cs // OUTPUT: Google TTS qua Worker (Agent D)
    INPCService.cs + INpcVoiceSelector.cs
    IQuestService.cs
    ISaveService.cs
    IHintService.cs
  A_World/              // @agentA World & Visual
    Supermarket/MarketScene.unity
    Player/ClickToMove.cs
    Camera/SmartCamera.cs
    Interactable.cs     // chỉ publish event, cấm gọi TTS/Worker
  B_Brain/              // @agentB NPC & Gameplay (Tier1/2 + Milo constrained, Tier3 stub)
    NPC/, Dialogue/ (text + voice id, không fetch audio), Quests/QuestManager.cs
    Companion/Milo.cs   // gọi IAudioDirector, không gọi provider
  C_Content/            // @agentC Learning Content (text, progression, repetition, approved metadata)
    ContentReaders/     // đọc Content/*.json, cấm fetch audio
  D_Audio/              // @agentD Audio & Speech
    Audio/AudioDirector.cs + SpeechAudioResolver.cs
    Speech/CloudflareTranslateTtsProvider.cs  // boundary Worker duy nhất, giữ API key ngoài Unity (canonical name; alias CloudflareGoogleTtsProvider đã xóa W1)
    Speech/AzureSttProvider.cs + SpeechAssessmentPolicy.cs
    Speech/FallbackSpeechProvider.cs       // runtime offline (Mock chỉ test/CI)
    Audio/AudioMixer + AudioFocus/Ducking
    Audio/VoiceProfiles + NpcVoiceProfileSelector.cs
    Audio/QA + PreGen pipeline
Content/
  vocab/*.json (50: audio.normal/slow/approved/voice)
  quests/*.json (5)
  dialogues/*.json + manifest.json (pack 30–40 câu)
Backend/ ParentDashboard/ // Phase 2, cấm làm ở Slice
```

## 3. Manual CompositionRoot — 1 hướng duy nhất (Fix 1 bắt buộc)

Flow cứng:

```
BootstrapScene (scene trắng, chỉ có GameInstaller)
     ↓
GameInstaller.CreateServices() — tạo 1 lần
     ↓
GameInstaller.InjectServices() — inject qua constructor
     ↓
Load MarketScene (Additive)
```

```csharp
// GameInstaller.cs — file duy nhất được phép `new` service. Agent A/B/C/D cấm `new` service của nhau.
// Location: Assets/_Bootstrap/GameInstaller.cs, assembly LWE.Bootstrap (Lead sở hữu).
// LWE.Bootstrap refs SharedKernel+World+Brain+Content+Audio; feature assemblies MUST NOT ref Bootstrap.
public class GameInstaller : MonoBehaviour {
  public IGameEventBus EventBus { get; private set; }
  public ILearningService Learning { get; private set; }
  public IQuestService Quests { get; private set; }
  public ISpeechProvider SpeechProvider => _speechRouter; // v6.3 ROUTER — inject một lần, switch inside
  public ISpeechPolicy SpeechPolicy { get; private set; }
  public ISaveService Save { get; private set; }
  public IHintService Hints { get; private set; }
  public IAudioDirector Audio { get; private set; }                 // v6.2: Agent D
  public ISpeechSynthesisProvider Tts { get; private set; }        // v6.2: boundary Worker duy nhất
  public INpcVoiceSelector Voices { get; private set; }            // v6.2: deterministic assignment

  SpeechProviderRouter _speechRouter;

  void Awake() {
    DontDestroyOnLoad(gameObject);
    EventBus = new GameEventBus();                    // Application
    Save = new LocalSave();                           // Application
    // Application: router bọc AzureSttProvider (default online); Mock chỉ test
    _speechRouter = new SpeechProviderRouter(new AzureSttProvider());
    SpeechPolicy = new SpeechAssessmentPolicy();      // Application (pure logic)
    Learning = new LearningService(EventBus);            // Session
    Hints = new HintService(EventBus);                   // Session logic, state per-quest (QuestHintState)
    Quests = new QuestManager(EventBus, Learning, Hints);// Scene/Session — reset khi đổi world
    Tts = new CloudflareTranslateTtsProvider();             // Application — Translate source, endpoint config ngoài repo, no key
    Audio = new AudioDirector(EventBus, Tts);            // Application — cache L1/L2 + Mixer + Focus
    Voices = new NpcVoiceProfileSelector(Save);          // Application — đọc save.npcVoices + worldSeed
    // Inject tường minh (constructor), Milo/B/C/D không tự new:
    Milo.Bind(EventBus, Quests, Learning, Hints, Audio);
  }

  // v6.3: switch INSIDE router — mọi consumer đã inject chuyển cùng lúc, không kẹt instance cũ.
  // public void SwitchToFallbackSpeech() => _speechRouter.SwitchTo(new FallbackSpeechProvider());
  // public void SwitchToOnlineSpeech() => _speechRouter.SwitchTo(new AzureSttProvider());
  }
}
}
```

### Service Lifetime Rules (v4 — tách Service vs State)

| Service | Lifetime | Ghi chú |
|---------|----------|---------|
| EventBus | Application (DontDestroy) | 1 instance duy nhất, sống suốt game |
| SpeechProvider | Application | giữ mic handle. Runtime: Azure ↔ FallbackSpeechProvider. Test: MockSpeechProvider |
| SpeechPolicy | Application (stateless) | pure function, không state |
| SaveService | Application | cache + flush |
| LearningService | Session (1 child / 1 lần chơi) | reset khi đổi profile, giữ khi đổi scene |
| HintService | Session (logic) + QuestHintState per-quest | service sống xuyên session, state reset mỗi quest mới để không carry frustration |
| QuestService | Scene/Session | reset quest khi load world mới, giữ mastery. Complete chỉ qua EventBus, cấm event Action<string> |
| AudioDirector | Application | cache L1/L2 + Mixer + Focus, Agent D duy nhất new |
| SpeechSynthesisProvider | Application | CloudflareTranslateTtsProvider, config ngoài repo |
| NpcVoiceSelector | Application | đọc save.npcVoices + worldSeed |
| NPC Instance | Scene | destroy khi unload scene |
| UI Controller | Scene | destroy khi unload scene |

```csharp
// Tách logic vs state (Fix cam 3):
class QuestHintState { public QuestId Id; public int WrongCount; public float IdleSec; public int Level; }
interface IHintService {
  QuestHintState GetState(QuestId q);
  void ReportWrong(QuestId q);   // Wrong 3→L1, 5→L2, 7→L3, ≥9→L4 (freeze §8 GAME_DESIGN)
  void Tick(QuestId q, float dt, float totalIdle);
  void Reset(QuestId q);         // StartQuest mới bắt buộc Reset, tránh carry L3 sang quest khác
}
```

Luật: Agent A/B/C/D nhận dependency qua constructor từ GameInstaller, cấm `ServiceLocator.Instance`, cấm `FindObjectOfType<Service>`, cấm `new QuestManager()/AudioDirector()` trôi nổi. Muốn mock test → tạo GameInstaller test với MockSpeechProvider (tests only).

## 4. Typed EventBus — đơn giản, predictable (Fix 2, xem ADR-003)

IDs freeze ở `contracts/Ids.md`: WordId, QuestId, NpcId, InteractionId, VoiceProfileId, LanguageCode, SfxId, MusicId + PlayerAction/AudioPriority/audio enums. JSON string parse sang struct ngay ở boundary, không so string thô trong gameplay.

```csharp
// C# 9 canonical form (NO record — CS8773 at LangVersion 9.0). Full impl in
// Assets/_SharedKernel/Events.cs: readonly struct + IEquatable<T> + ==/!= +
// GetHashCode + ToString + Deconstruct, public readonly fields.
public readonly struct WordSeenEvent : IEquatable<WordSeenEvent> { public readonly WordId WordId; public readonly LearnSource Source; public readonly DateTime At; /* ... */ }
public readonly struct QuestCompletedEvent : IEquatable<QuestCompletedEvent> { public readonly QuestId QuestId; public readonly DateTime At; /* ... */ } // Quest complete CHỈ qua event này, cấm event Action<string> trong IQuestService
public readonly struct QuestStartedEvent : IEquatable<QuestStartedEvent> { public readonly QuestId QuestId; public readonly DateTime At; /* ... */ }

public interface IGameEventBus {
  IDisposable Subscribe<T>(Action<T> handler);
  void Publish<T>(T gameEvent);
}
// Impl: Dictionary<Type, Delegate>. KHÔNG weak-ref trong core. Đơn giản, testable.
```

Contract cứng:
* Subscriber owns subscription. BẮT BUỘC dispose ở OnDisable/OnDestroy. Quên = leak = reject PR.
* Publish KHÔNG được throw nếu subscriber đã destroy (bus check disposed trước khi invoke).
* Duplicate Subscribe cùng handler: idempotent (chỉ nhận 1 lần) hoặc reject rõ ràng — cấm gọi handler 2 lần lặng lẽ. Cover bởi CT-011/CT-012.
* BusBehaviour chỉ là helper, không phải magic:

```csharp
public abstract class BusBehaviour : MonoBehaviour {
  readonly List<IDisposable> subs = new();
  protected void On<T>(Action<T> h, IGameEventBus bus) => subs.Add(bus.Subscribe(h));
  protected virtual void OnDisable() { subs.ForEach(s => s.Dispose()); subs.Clear(); }
}
```

Vì sao bỏ weak-ref: AI agent sẽ implement 3 kiểu khác nhau, bug khó debug hơn leak. Explicit dispose dễ review + dễ test hơn.

## 5. Learning — Mastery State Machine (thay % đơn)

```
UNKNOWN → EXPOSED → RECOGNIZED → PRODUCED → USED_IN_CONTEXT → RETAINED
```

```csharp
public enum MasteryStage { Unknown, Exposed, Recognized, Produced, UsedInContext, Retained }
public class WordMastery {
  public WordId Id;
  public MasteryStage Stage;
  public int Exposure, RecognitionHit, RecognitionTotal, SpeakingHit, SpeakingTotal, ContextUse;
  public DateTime NextReview; // spaced repetition ẩn
  public float Score; // 0..1 trong stage, chỉ dashboard thấy
}
```

Quy tắc lên stage: Exposed ≥3 lần thấy/nghe → Recognized nếu chọn đúng 4/5 → Produced nếu nói đạt Great+ 2/4 → UsedInContext nếu dùng đúng 1/3 tình huống → Retained nếu recall sau 7 ngày. Nói 10 lần liên tục không farm được mastery (rate-limit + yêu cầu đa dạng Source).

## 6. Speech INPUT + Audio OUTPUT (v6: tách STT và TTS, xem AUDIO_DESIGN + ADR-007)

INPUT (STT/Assessment, Agent D):
```csharp
// D implement, trả raw, không quyết định Perfect/Great
public struct RawSpeechResult {
  public string Transcript; // "abo", "a bow", "apple"
  public float Confidence;
  public float PronScore, FluencyScore, CompletenessScore;
  public string ErrorReason; // NoSpeech, Noise, MixedLang...
}
public interface ISpeechProvider {
  event Action<RawSpeechResult> OnRawResult; // typed, chỉ cấm Action<string>
  void StartListening(WordId expected, int timeoutSec);
  void StopListening();
}
public struct SpeechResult {
  public WordId Expected; public string Heard;
  public float Similarity; public SpeechLevel Level;
}
public interface ISpeechPolicy {
  SpeechResult Assess(WordId expected, RawSpeechResult raw);
}
```

OUTPUT (Google Translate TTS source qua Worker, Agent D duy nhất — đồng bộ Services.md v6.4.1):
```csharp
// C# 9 canonical form (NO record — CS8773 at LangVersion 9.0). Full impl in
// Assets/_SharedKernel/Services.cs: readonly struct + IEquatable<T>, public readonly fields.
readonly struct DialogueRequest { string Text; VoiceProfileId Voice; LanguageCode Lang; float Rate; float Pitch; SpeechStyle Style; AudioFormat Format; AudioPriority Priority; }
readonly struct TtsRequest { string Text; VoiceProfileId Voice; LanguageCode Lang; float Rate; float Pitch; SpeechStyle Style; AudioFormat Format; AudioPriority Priority; }
interface IAudioDirector {
  Task PlayVocabularyAsync(WordId wordId, VocabularyAudioMode Mode);
  Task SpeakAsync(DialogueRequest request);
  void PlaySfx(SfxId id); void PlayMusic(MusicId id);
  void SetAudioFocus(AudioFocusMode mode);
}
interface ISpeechSynthesisProvider {
  Task<TtsAudioResult> SynthesizeAsync(TtsRequest request, CancellationToken ct);
} // Impl duy nhất: CloudflareTranslateTtsProvider = Translate source (WorkerTtsContract: GET, audio/mpeg, no auth; language YES, voice/pitch/SSML NO)
```

Policy STT: PronScore ≥85 Perfect, 65–85 Great, 40–65 Almost, <40 TryTogether; NoSpeech/MixedLang → demo + simplify, không phạt.
Pipeline IN: `Mic → Denoise → VAD → STT → LangDetect → Raw → Policy → EventBus`. Pipeline OUT: `DialogueRequest → Validator (từ + allowlist) → Resolver → Cache L1–L4 → Worker/Google → Mixer (Focus/ducking)`.
STT impl: `AzureSttProvider` online, `FallbackSpeechProvider` offline (không giả nghe được), `MockSpeechProvider` test-only.

## 7. NPC scope Slice — Tier3 stub only (chống creep, Fix cam)

* Slice có: Tier1 Background (BT) + Tier2 Interactive (10–50 intents local) + Milo Constrained Companion.
* Tier3 Memory ở Slice: **architecture stub only** — giữ interface/schema (`memory[]`, friendship) để Tier2/Milo đọc, KHÔNG làm dynamic memory engine (không relationship graph, không long-term recall engine). Sang Phase 4 mới implement sâu. Agent B không ôm thêm Memory engine ở Slice.
* Mọi response: `Intent → SafetyFilter → EduContext → ResponseGen → Validator → TTS`. LLM không nói trực tiếp với trẻ. 80% local / 15% small / 5% large (intention).

### AI Decision Policy (implementation contract, không phải slogan)

```
IF child input matches known intent (confidence ≥0.7)
  → deterministic scripted response (không gọi model)

ELSE IF input trong safe expected topic (quest hiện tại / object trước mặt / learning target)
  → constrained small model với allowlist + template

ELSE (linh tinh / ngoài chủ đề / tiếng Việt dài / im lặng)
  → fallback scripted + Hint Escalation (Milo demo / simplify)
```

### Giới hạn Slice (Lead reject nếu vượt)

* NPC thường: **≤6 từ**. Milo encouragement / fixed feedback: **≤8 từ** (hết conflict GAME_DESIGN §2). Validator đếm từ trước TTS.
* Max context gửi model: **Current Quest + Current Object + NPC Intent + Learning Target**. Cấm gửi toàn bộ lịch sử, toàn bộ profile.
* LLM NEVER receives: child identifier (tên thật, id), raw voice/audio, persistent personal data (địa chỉ, trường, ảnh). Chỉ nhận: `questId, wordId, intent, raw transcript đã khử PII`.
* Mọi câu AI sinh ra phải qua Validator: đúng trần từ, chỉ Level-1 vocab, không idiom/sarcasm, có trong allowlist → mới TTS.

## 8. Content Schema mở rộng (semantic graph)

```json
{
  "id": "apple",
  "language": "en",
  "display": {"en": "apple", "vi": "quả táo"},
  "learning": {"minAge": 4, "difficulty": 1, "skills": ["listen","recognize","speak","use"]},
  "semantic": {"category": "food", "tags": ["fruit","red","round","sweet","market"]},
  "speech": {"expectedForms": ["apple"], "phonetic": "ˈæpəl"},
  "assets": {"prefab": "Addressables/Food/Apple", "image": "apple.png"},
  "audio": {"normal": "audio/apple_normal.mp3", "slow": "audio/apple_slow.mp3", "voice": "learning_v1", "lang": "en-US", "approved": true}
}
```

→ Learning Engine suy ra quan hệ Apple-Food-Fruit-Red để tạo câu hỏi “What fruit is red?” mà không code thêm.

## 9. Save + Privacy + Cost

* Slice: `LocalSave` JSON (`words: {id: stage+score}`, questsDone, playTime). Delete raw audio default, opt-in mới lưu. NO ADS, NO stranger chat.
* Cost: 80% local intent, 15% small model, 5% large. Cache TTS câu lặp.

## 10. CI + Testing — pipeline thật từ W0 (v6)

```
PR → Content+Audio validate (python) → CT IDs + TestManifest check → Arch lint (rg) → Lead Review → Merge
      + UNITY LOCAL GATE (manual): compile clean → EditMode green → PlayMode green → StandaloneWindows64 build
```

* Workflows license-free ở `.github/workflows/` (v6.4: không game-ci, không UNITY_LICENSE): `contract-tests.yml` (validator + CT IDs + mapping 2 chiều, Unity status NOT RUN), `content-schema.yml` (validator incl. `audio.approved`), `architecture-lint.yml` (rg fail nếu: ServiceLocator, `new` service ngoài Installer, `Action<string>`, string ID thô, gọi Worker/Google/TTS trực tiếp từ `A_World`/`B_Brain`/`C_Content`), `release-gate.yml` (ship gate non-Unity + nhắc UNITY LOCAL GATE tay).
* Unity EditMode tests ở `Assets/Tests/EditMode/CT*.cs` (CT-001..CT-012 + CT-A01 cache key, CT-A02 focus/ducking, CT-A03 offline fallback, CT-A04 voice identity persist).
