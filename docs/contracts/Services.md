# contracts/Services.md — Interfaces (freeze v6.1: single TTS path + typed audio)

> v6.1 FIX-001: `ISpeechProvider` INPUT-only (STT/mic/raw). Mọi OUTPUT speech qua `IAudioDirector` → `ISpeechSynthesisProvider`. Không tồn tại hai đường TTS song song.

```csharp
// ---- Ids (freeze, xem contracts/Ids.md) ----
readonly struct WordId { string Value; }
readonly struct QuestId { string Value; }
readonly struct NpcId { string Value; }
readonly struct InteractionId { string Value; }
readonly struct VoiceProfileId { string Value; } // milo_v1, learning_v1, mia_v1, npc_female_01.., npc_male_01..
readonly struct LanguageCode { string Value; }   // Slice freeze en-US
readonly struct SfxId { string Value; }          // v6.1 FIX-002, vd "sfx_pickup"
readonly struct MusicId { string Value; }        // v6.1 FIX-002, vd "music_market_day"

enum PlayerAction { Find, Bring, Speak, Give, Select }
enum AudioPriority { P0_Safety, P1_Pronunciation, P2_Instruction, P3_Dialogue, P4_Feedback, P5_Sfx, P6_Ambient, P7_Music }
enum VocabularyAudioMode { Normal, Slow, Syllable }            // v6.1 FIX-002
enum AudioFocusMode { Learning, Dialogue, Ambient, Muted }     // v6.1 FIX-002
enum NpcArchetype { Shopkeeper, Parent, Child, Worker, Animal }// v6.1 FIX-002
enum SpeechStyle { Clear, Excited, Soft, Neutral }             // v6.1 FIX-002
enum AudioFormat { Mp3_44100, Wav_44100 }                      // v6.1 FIX-002
enum AudioInterruption { Interrupt, PauseResume, Duck, Queue, Ignore } // v6.1 Part H

interface IGameEventBus { IDisposable Subscribe<T>(Action<T> h); void Publish<T>(T e); }

interface ILearningService {
  void ReportSeen(WordId id, LearnSource src);
  void ReportSpoken(WordId id, SpeechLevel level, LearnSource src);
  MasteryStage GetStage(WordId id);
}

interface IQuestService {
  void StartQuest(QuestId questId);
  void ReportAction(PlayerAction action, WordId target);
  QuestState GetState(QuestId questId);
  // CẤM event Action<string> Completed. Complete bằng:
  // _eventBus.Publish(new QuestCompletedEvent(questId, DateTime.UtcNow));
}

interface IHintService {
  // Service Session: logic chung. State per-quest, reset khi quest mới để không carry frustration.
  QuestHintState GetState(QuestId q);
  void ReportWrong(QuestId q);
  void Tick(QuestId q, float deltaSec, float totalIdleSec);
  void Reset(QuestId q);
}
class QuestHintState { public QuestId Id; public int WrongCount; public float IdleSec; public int Level; }

interface ISpeechProvider {
  // INPUT-ONLY (FIX-001): STT + microphone + raw result. Không có Speak/TTS ở đây.
  event Action<RawSpeechResult> Raw; // typed, cho phép. Cấm Action<string>.
  void StartListening(WordId expected, int timeoutSec);
  void Stop();
}
// v6.3 Fix 3: gameplay systems inject Router (as ISpeechProvider) một lần và giữ mãi.
// Online<->offline switch xảy ra INSIDE router → không consumer nào kẹt Azure instance cũ.
// Impl: SpeechProviderRouter (Lead, _SharedKernel). Installer là nơi duy nhất new provider + gọi SwitchTo.
interface ISpeechPolicy { SpeechResult Assess(WordId expected, RawSpeechResult raw); }
interface ISaveService {
  void Save(PlayerProgress p); PlayerProgress Load();
  // save.npcVoices: {npcId: voiceProfileId} + save.worldSeed (Part E). Load ưu tiên assignment cũ.
}

// ---- v6.1 Audio (Agent D sở hữu, A/B/C chỉ gọi IAudioDirector) ----
// C# 9 form (canonical, xem Events.md policy): readonly struct + IEquatable,
// public readonly fields. Cấm `record struct` (C# 10, CS8773).
// readonly struct DialogueRequest { string Text; VoiceProfileId Voice; LanguageCode Lang; float Rate; float Pitch; SpeechStyle Style; AudioFormat Format; AudioPriority Priority; }
// readonly struct TtsRequest { (same shape) }
// FIX-003: đủ field cache key nội bộ. Worker Translate hiện tại chỉ dùng text/lang/rate (xem WorkerTtsContract mapping + capability).
record TtsAudioResult(byte[] Mp3, string CacheKey, bool FromCache);

interface IAudioDirector {
  Task PlayVocabularyAsync(WordId wordId, VocabularyAudioMode Mode);
  Task SpeakAsync(DialogueRequest request);
  void PlaySfx(SfxId id);
  void PlayMusic(MusicId id);
  void SetAudioFocus(AudioFocusMode mode); // ducking P1 pronunciation
}
interface ISpeechSynthesisProvider {
  // Boundary duy nhất tới Cloudflare Worker → Google Translate TTS (WorkerTtsContract v6.4.1: GET, text≤200, lang, rate normal|slow, audio/mpeg, no auth).
  // Capability freeze: language YES, rate LIMITED, voice/pitch/SSML NO. Không fake voice switching.
  Task<TtsAudioResult> SynthesizeAsync(TtsRequest request, CancellationToken ct);
}
interface INpcVoiceSelector {
  // Part E deterministic: assignment = StableShuffle(pools, hash(NpcId + worldSeed)) cân bằng F/M theo population;
  // persist save.npcVoices; load ưu tiên cũ; NPC mới lấy slot cân bằng còn trống. Cấm Random.Range không persist.
  // Milo/Mia/LearningVoice luôn fixed (không qua selector).
  VoiceProfileId Assign(NpcId npc, NpcArchetype archetype);
}
```

Lifetime: xem ARCHITECTURE §3. Chỉ GameInstaller `new`. Nhận qua constructor.
Lint: FAIL nếu grep `Action<string>` ngoài allowlist test (xem .github/workflows/architecture-lint.yml).
