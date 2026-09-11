# contracts/Ids.md — Freeze ID types (v6.1: typed audio, FIX-002)

> Raw string chỉ tồn tại ở JSON/network boundary. Parse sang typed ID/enum ngay tại boundary.

> Chống typo "find"/"Find"/"say". Agent nào dùng `string action` / `string questId` / tên Google voice trong gameplay → reject.

```csharp
public readonly struct WordId {
  public readonly string Value;
  public WordId(string v) => Value = v.ToLowerInvariant().Trim();
  public static implicit operator string(WordId id) => id.Value;
}
public readonly struct QuestId {
  public readonly string Value;
  public QuestId(string v) => Value = v.ToLowerInvariant().Trim();
}
public readonly struct NpcId {
  public readonly string Value;
  public NpcId(string v) => Value = v.ToLowerInvariant().Trim();
}
public readonly struct InteractionId {
  public readonly string Value;
  public InteractionId(string v) => Value = v.Trim();
}
// v6 audio: gameplay chỉ biết VoiceProfileId, không biết tên Google voice.
public readonly struct VoiceProfileId {
  public readonly string Value; // milo_v1, learning_v1, mia_v1, npc_female_01..03, npc_male_01..03
  public VoiceProfileId(string v) => Value = v.ToLowerInvariant().Trim();
}
public readonly struct LanguageCode {
  public readonly string Value; // Slice freeze "en-US", giữ field để mở rộng
  public LanguageCode(string v) => Value = v.Trim();
}
public enum PlayerAction { Find, Bring, Speak, Give, Select }
public enum LearnSource { Object, NPC, Quest, Repetition, Milo }
public enum AudioPriority { P0_Safety, P1_Pronunciation, P2_Instruction, P3_Dialogue, P4_Feedback, P5_Sfx, P6_Ambient, P7_Music }
// v6.1 FIX-002: typed audio thay magic string.
public readonly struct SfxId { public readonly string Value; public SfxId(string v) => Value = v.Trim(); }
public readonly struct MusicId { public readonly string Value; public MusicId(string v) => Value = v.Trim(); }
public enum VocabularyAudioMode { Normal, Slow, Syllable }
public enum AudioFocusMode { Learning, Dialogue, Ambient, Muted }
public enum NpcArchetype { Shopkeeper, Parent, Child, Worker, Animal }
public enum SpeechStyle { Clear, Excited, Soft, Neutral }
public enum AudioFormat { Mp3_44100, Wav_44100 }
public enum AudioInterruption { Interrupt, PauseResume, Duck, Queue, Ignore } // Part H
```

JSON vẫn là string, code C# parse sang struct/enum ngay ở boundary. Tên Google voice chỉ tồn tại ở Worker/config, cấm trong `A_World`/`B_Brain`/`C_System` gameplay.
