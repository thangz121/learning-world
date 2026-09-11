// _SharedKernel/Ids.cs — frozen IDs (Lead owns). Matches docs/contracts/Ids.md v6.1.
// Raw string only at JSON/network boundary; parse to these types immediately.
using System;

[Serializable] public readonly struct WordId {
  public readonly string Value;
  public WordId(string v) => Value = v.ToLowerInvariant().Trim();
  public override string ToString() => Value;
  public static implicit operator string(WordId id) => id.Value;
}
[Serializable] public readonly struct QuestId {
  public readonly string Value;
  public QuestId(string v) => Value = v.ToLowerInvariant().Trim();
  public override string ToString() => Value;
  public static implicit operator string(QuestId id) => id.Value;
}
[Serializable] public readonly struct NpcId {
  public readonly string Value;
  public NpcId(string v) => Value = v.ToLowerInvariant().Trim();
  public override string ToString() => Value;
  public static implicit operator string(NpcId id) => id.Value;
}
[Serializable] public readonly struct InteractionId {
  public readonly string Value;
  public InteractionId(string v) => Value = v.Trim();
  public override string ToString() => Value;
  public static implicit operator string(InteractionId id) => id.Value;
}
[Serializable] public readonly struct VoiceProfileId {
  public readonly string Value; // milo_v1, learning_v1, mia_v1, npc_female_01..03, npc_male_01..03
  public VoiceProfileId(string v) => Value = v.ToLowerInvariant().Trim();
  public override string ToString() => Value;
  public static implicit operator string(VoiceProfileId id) => id.Value;
}
[Serializable] public readonly struct LanguageCode {
  public readonly string Value; // Slice freeze "en-US"
  public LanguageCode(string v) => Value = v.Trim();
  public override string ToString() => Value;
  public static implicit operator string(LanguageCode id) => id.Value;
}
[Serializable] public readonly struct SfxId {
  public readonly string Value;
  public SfxId(string v) => Value = v.Trim();
  public override string ToString() => Value;
  public static implicit operator string(SfxId id) => id.Value;
}
[Serializable] public readonly struct MusicId {
  public readonly string Value;
  public MusicId(string v) => Value = v.Trim();
  public override string ToString() => Value;
  public static implicit operator string(MusicId id) => id.Value;
}
public enum PlayerAction { Find, Bring, Speak, Give, Select }
public enum LearnSource { Object, NPC, Quest, Repetition, Milo }
public enum SpeechLevel { Perfect, Great, Almost, TryTogether }
public enum MasteryStage { Unknown, Exposed, Recognized, Produced, UsedInContext, Retained }
public enum AudioPriority { P0_Safety, P1_Pronunciation, P2_Instruction, P3_Dialogue, P4_Feedback, P5_Sfx, P6_Ambient, P7_Music }
public enum VocabularyAudioMode { Normal, Slow, Syllable }
public enum AudioFocusMode { Learning, Dialogue, Ambient, Muted }
public enum NpcArchetype { Shopkeeper, Parent, Child, Worker, Animal }
public enum SpeechStyle { Clear, Excited, Soft, Neutral }
public enum AudioFormat { Mp3_44100, Wav_44100 }
public enum AudioInterruption { Interrupt, PauseResume, Duck, Queue, Ignore }
