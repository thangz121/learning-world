// _SharedKernel/Services.cs — frozen interfaces (Lead owns). Matches docs/contracts/Services.md v6.1.
// ISpeechProvider is INPUT-only. All OUTPUT speech goes via IAudioDirector.
using System;
using System.Threading;
using System.Threading.Tasks;

public interface IGameEventBus {
  IDisposable Subscribe<T>(Action<T> handler);
  void Publish<T>(T gameEvent);
}
public struct RawSpeechResult {
  public string Transcript;
  public float Confidence, PronScore, FluencyScore, CompletenessScore;
  public string ErrorReason; // NoSpeech, Noise, MixedLang...
}
public struct SpeechResult {
  public WordId Expected; public string Heard;
  public float Similarity; public SpeechLevel Level;
}
public class WordMastery {
  public WordId Id; public MasteryStage Stage;
  public int Exposure, RecognitionHit, RecognitionTotal, SpeakingHit, SpeakingTotal, ContextUse;
  public DateTime NextReview; public float Score;
}
public class QuestState { public QuestId Id; public int ObjectiveIndex; public bool Completed; }
public class QuestHintState { public QuestId Id; public int WrongCount; public float IdleSec; public int Level; }
public class PlayerProgress {
  public System.Collections.Generic.Dictionary<string, WordMastery> Words;
  public System.Collections.Generic.List<string> QuestsDone;
  public float PlayTimeSec;
  public System.Collections.Generic.Dictionary<string, string> NpcVoices; // npcId -> voiceProfileId (Part E)
  public string WorldSeed; // stable per profile (Part E)
}
public interface ILearningService {
  void ReportSeen(WordId id, LearnSource src);
  void ReportSpoken(WordId id, SpeechLevel level, LearnSource src);
  MasteryStage GetStage(WordId id);
}
public interface IQuestService {
  void StartQuest(QuestId questId);
  void ReportAction(PlayerAction action, WordId target);
  QuestState GetState(QuestId questId);
  // Complete ONLY via _eventBus.Publish(new QuestCompletedEvent(...)). No Action<string> event.
}
public interface IHintService {
  QuestHintState GetState(QuestId q);
  void ReportWrong(QuestId q);
  void Tick(QuestId q, float deltaSec, float totalIdleSec);
  void Reset(QuestId q);
}
public interface ISpeechProvider {
  event Action<RawSpeechResult> Raw;
  void StartListening(WordId expected, int timeoutSec);
  void Stop();
}
public interface ISpeechPolicy { SpeechResult Assess(WordId expected, RawSpeechResult raw); }
public interface ISaveService { void Save(PlayerProgress p); PlayerProgress Load(); }
// C# POLICY: LangVersion 9.0 (verified Bee rsp). `record struct` is C# 10 (CS8773),
// so requests are C# 9 readonly structs with record-equivalent value semantics.
// Public readonly FIELDS match sibling structs and serialize under Unity/JsonUtility.
[Serializable]
public readonly struct DialogueRequest : global::System.IEquatable<DialogueRequest> {
  public readonly string Text;
  public readonly VoiceProfileId Voice;
  public readonly LanguageCode Lang;
  public readonly float Rate;
  public readonly float Pitch;
  public readonly SpeechStyle Style;
  public readonly AudioFormat Format;
  public readonly AudioPriority Priority;
  public DialogueRequest(string Text, VoiceProfileId Voice, LanguageCode Lang, float Rate, float Pitch, SpeechStyle Style, AudioFormat Format, AudioPriority Priority) {
    this.Text = Text; this.Voice = Voice; this.Lang = Lang; this.Rate = Rate;
    this.Pitch = Pitch; this.Style = Style; this.Format = Format; this.Priority = Priority;
  }
  public void Deconstruct(out string text, out VoiceProfileId voice, out LanguageCode lang, out float rate, out float pitch, out SpeechStyle style, out AudioFormat format, out AudioPriority priority) {
    text = Text; voice = Voice; lang = Lang; rate = Rate;
    pitch = Pitch; style = Style; format = Format; priority = Priority;
  }
  public bool Equals(DialogueRequest other) {
    return System.Collections.Generic.EqualityComparer<string>.Default.Equals(Text, other.Text)
      && System.Collections.Generic.EqualityComparer<VoiceProfileId>.Default.Equals(Voice, other.Voice)
      && System.Collections.Generic.EqualityComparer<LanguageCode>.Default.Equals(Lang, other.Lang)
      && Rate.Equals(other.Rate) && Pitch.Equals(other.Pitch)
      && Style == other.Style && Format == other.Format && Priority == other.Priority;
  }
  public override bool Equals(object obj) => obj is DialogueRequest o && Equals(o);
  public override int GetHashCode() {
    unchecked {
      int h = 17;
      h = h * 31 + System.Collections.Generic.EqualityComparer<string>.Default.GetHashCode(Text);
      h = h * 31 + System.Collections.Generic.EqualityComparer<VoiceProfileId>.Default.GetHashCode(Voice);
      h = h * 31 + System.Collections.Generic.EqualityComparer<LanguageCode>.Default.GetHashCode(Lang);
      h = h * 31 + Rate.GetHashCode();
      h = h * 31 + Pitch.GetHashCode();
      h = h * 31 + Style.GetHashCode();
      h = h * 31 + Format.GetHashCode();
      h = h * 31 + Priority.GetHashCode();
      return h;
    }
  }
  public static bool operator ==(DialogueRequest l, DialogueRequest r) => l.Equals(r);
  public static bool operator !=(DialogueRequest l, DialogueRequest r) => !l.Equals(r);
  public override string ToString() => $"DialogueRequest {{ Text = {Text}, Voice = {Voice}, Lang = {Lang}, Rate = {Rate}, Pitch = {Pitch}, Style = {Style}, Format = {Format}, Priority = {Priority} }}";
}
[Serializable]
public readonly struct TtsRequest : global::System.IEquatable<TtsRequest> {
  public readonly string Text;
  public readonly VoiceProfileId Voice;
  public readonly LanguageCode Lang;
  public readonly float Rate;
  public readonly float Pitch;
  public readonly SpeechStyle Style;
  public readonly AudioFormat Format;
  public readonly AudioPriority Priority;
  public TtsRequest(string Text, VoiceProfileId Voice, LanguageCode Lang, float Rate, float Pitch, SpeechStyle Style, AudioFormat Format, AudioPriority Priority) {
    this.Text = Text; this.Voice = Voice; this.Lang = Lang; this.Rate = Rate;
    this.Pitch = Pitch; this.Style = Style; this.Format = Format; this.Priority = Priority;
  }
  public void Deconstruct(out string text, out VoiceProfileId voice, out LanguageCode lang, out float rate, out float pitch, out SpeechStyle style, out AudioFormat format, out AudioPriority priority) {
    text = Text; voice = Voice; lang = Lang; rate = Rate;
    pitch = Pitch; style = Style; format = Format; priority = Priority;
  }
  public bool Equals(TtsRequest other) {
    return System.Collections.Generic.EqualityComparer<string>.Default.Equals(Text, other.Text)
      && System.Collections.Generic.EqualityComparer<VoiceProfileId>.Default.Equals(Voice, other.Voice)
      && System.Collections.Generic.EqualityComparer<LanguageCode>.Default.Equals(Lang, other.Lang)
      && Rate.Equals(other.Rate) && Pitch.Equals(other.Pitch)
      && Style == other.Style && Format == other.Format && Priority == other.Priority;
  }
  public override bool Equals(object obj) => obj is TtsRequest o && Equals(o);
  public override int GetHashCode() {
    unchecked {
      int h = 17;
      h = h * 31 + System.Collections.Generic.EqualityComparer<string>.Default.GetHashCode(Text);
      h = h * 31 + System.Collections.Generic.EqualityComparer<VoiceProfileId>.Default.GetHashCode(Voice);
      h = h * 31 + System.Collections.Generic.EqualityComparer<LanguageCode>.Default.GetHashCode(Lang);
      h = h * 31 + Rate.GetHashCode();
      h = h * 31 + Pitch.GetHashCode();
      h = h * 31 + Style.GetHashCode();
      h = h * 31 + Format.GetHashCode();
      h = h * 31 + Priority.GetHashCode();
      return h;
    }
  }
  public static bool operator ==(TtsRequest l, TtsRequest r) => l.Equals(r);
  public static bool operator !=(TtsRequest l, TtsRequest r) => !l.Equals(r);
  public override string ToString() => $"TtsRequest {{ Text = {Text}, Voice = {Voice}, Lang = {Lang}, Rate = {Rate}, Pitch = {Pitch}, Style = {Style}, Format = {Format}, Priority = {Priority} }}";
}
public struct TtsAudioResult { public byte[] Mp3; public string CacheKey; public bool FromCache; }
public interface IAudioDirector {
  Task PlayVocabularyAsync(WordId wordId, VocabularyAudioMode mode);
  Task SpeakAsync(DialogueRequest request);
  void PlaySfx(SfxId id);
  void PlayMusic(MusicId id);
  void SetAudioFocus(AudioFocusMode mode);
}
public interface ISpeechSynthesisProvider {
  Task<TtsAudioResult> SynthesizeAsync(TtsRequest request, CancellationToken ct);
}
public interface INpcVoiceSelector {
  VoiceProfileId Assign(NpcId npc, NpcArchetype archetype);
}
