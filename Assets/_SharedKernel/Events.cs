// _SharedKernel/Events.cs — typed events (Lead owns). Matches docs/contracts/Events.md v6.1.
// New event = new readonly struct below + Lead PR. No Action<string>.
// C# POLICY: Unity 6000.6 compiles this project at LangVersion 9.0 (verified in
// Library/Bee rsp: -langversion:9.0, no csc.rsp override). `record struct` is C# 10
// and fails with CS8773, so events are C# 9 readonly structs with record-equivalent
// value semantics (IEquatable<T>, ==, !=, GetHashCode, ToString, Deconstruct).
// Public readonly FIELDS (not properties) match sibling SharedKernel structs
// (RawSpeechResult/SpeechResult/TtsAudioResult) and serialize under Unity/JsonUtility.
using System;
using System.Collections.Generic;

[Serializable]
public readonly struct WordSeenEvent : IEquatable<WordSeenEvent> {
  public readonly WordId WordId;
  public readonly LearnSource Source;
  public readonly DateTime At;
  public WordSeenEvent(WordId WordId, LearnSource Source, DateTime At) {
    this.WordId = WordId; this.Source = Source; this.At = At;
  }
  public void Deconstruct(out WordId wordId, out LearnSource source, out DateTime at) {
    wordId = WordId; source = Source; at = At;
  }
  public bool Equals(WordSeenEvent other) {
    return EqualityComparer<WordId>.Default.Equals(WordId, other.WordId)
      && Source == other.Source
      && At.Equals(other.At);
  }
  public override bool Equals(object obj) => obj is WordSeenEvent o && Equals(o);
  public override int GetHashCode() {
    unchecked {
      int h = 17;
      h = h * 31 + EqualityComparer<WordId>.Default.GetHashCode(WordId);
      h = h * 31 + Source.GetHashCode();
      h = h * 31 + At.GetHashCode();
      return h;
    }
  }
  public static bool operator ==(WordSeenEvent l, WordSeenEvent r) => l.Equals(r);
  public static bool operator !=(WordSeenEvent l, WordSeenEvent r) => !l.Equals(r);
  public override string ToString() => $"WordSeenEvent {{ WordId = {WordId}, Source = {Source}, At = {At} }}";
}

[Serializable]
public readonly struct WordSpokenEvent : IEquatable<WordSpokenEvent> {
  public readonly WordId WordId;
  public readonly SpeechResult Result;
  public readonly LearnSource Source;
  public WordSpokenEvent(WordId WordId, SpeechResult Result, LearnSource Source) {
    this.WordId = WordId; this.Result = Result; this.Source = Source;
  }
  public void Deconstruct(out WordId wordId, out SpeechResult result, out LearnSource source) {
    wordId = WordId; result = Result; source = Source;
  }
  public bool Equals(WordSpokenEvent other) {
    return EqualityComparer<WordId>.Default.Equals(WordId, other.WordId)
      && EqualityComparer<SpeechResult>.Default.Equals(Result, other.Result)
      && Source == other.Source;
  }
  public override bool Equals(object obj) => obj is WordSpokenEvent o && Equals(o);
  public override int GetHashCode() {
    unchecked {
      int h = 17;
      h = h * 31 + EqualityComparer<WordId>.Default.GetHashCode(WordId);
      h = h * 31 + EqualityComparer<SpeechResult>.Default.GetHashCode(Result);
      h = h * 31 + Source.GetHashCode();
      return h;
    }
  }
  public static bool operator ==(WordSpokenEvent l, WordSpokenEvent r) => l.Equals(r);
  public static bool operator !=(WordSpokenEvent l, WordSpokenEvent r) => !l.Equals(r);
  public override string ToString() => $"WordSpokenEvent {{ WordId = {WordId}, Result = {Result}, Source = {Source} }}";
}

[Serializable]
public readonly struct QuestCompletedEvent : IEquatable<QuestCompletedEvent> {
  public readonly QuestId QuestId;
  public readonly DateTime At;
  public QuestCompletedEvent(QuestId QuestId, DateTime At) {
    this.QuestId = QuestId; this.At = At;
  }
  public void Deconstruct(out QuestId questId, out DateTime at) {
    questId = QuestId; at = At;
  }
  public bool Equals(QuestCompletedEvent other) {
    return EqualityComparer<QuestId>.Default.Equals(QuestId, other.QuestId)
      && At.Equals(other.At);
  }
  public override bool Equals(object obj) => obj is QuestCompletedEvent o && Equals(o);
  public override int GetHashCode() {
    unchecked {
      int h = 17;
      h = h * 31 + EqualityComparer<QuestId>.Default.GetHashCode(QuestId);
      h = h * 31 + At.GetHashCode();
      return h;
    }
  }
  public static bool operator ==(QuestCompletedEvent l, QuestCompletedEvent r) => l.Equals(r);
  public static bool operator !=(QuestCompletedEvent l, QuestCompletedEvent r) => !l.Equals(r);
  public override string ToString() => $"QuestCompletedEvent {{ QuestId = {QuestId}, At = {At} }}";
}

[Serializable]
public readonly struct QuestStartedEvent : IEquatable<QuestStartedEvent> {
  public readonly QuestId QuestId;
  public readonly DateTime At;
  public QuestStartedEvent(QuestId QuestId, DateTime At) {
    this.QuestId = QuestId; this.At = At;
  }
  public void Deconstruct(out QuestId questId, out DateTime at) {
    questId = QuestId; at = At;
  }
  public bool Equals(QuestStartedEvent other) {
    return EqualityComparer<QuestId>.Default.Equals(QuestId, other.QuestId)
      && At.Equals(other.At);
  }
  public override bool Equals(object obj) => obj is QuestStartedEvent o && Equals(o);
  public override int GetHashCode() {
    unchecked {
      int h = 17;
      h = h * 31 + EqualityComparer<QuestId>.Default.GetHashCode(QuestId);
      h = h * 31 + At.GetHashCode();
      return h;
    }
  }
  public static bool operator ==(QuestStartedEvent l, QuestStartedEvent r) => l.Equals(r);
  public static bool operator !=(QuestStartedEvent l, QuestStartedEvent r) => !l.Equals(r);
  public override string ToString() => $"QuestStartedEvent {{ QuestId = {QuestId}, At = {At} }}";
}

[Serializable]
public readonly struct HintLevelChanged : IEquatable<HintLevelChanged> {
  public readonly QuestId QuestId;
  public readonly int Level; // 0 none,1 visual,2 point,3 demo,4 simplify
  public HintLevelChanged(QuestId QuestId, int Level) {
    this.QuestId = QuestId; this.Level = Level;
  }
  public void Deconstruct(out QuestId questId, out int level) {
    questId = QuestId; level = Level;
  }
  public bool Equals(HintLevelChanged other) {
    return EqualityComparer<QuestId>.Default.Equals(QuestId, other.QuestId)
      && Level == other.Level;
  }
  public override bool Equals(object obj) => obj is HintLevelChanged o && Equals(o);
  public override int GetHashCode() {
    unchecked {
      int h = 17;
      h = h * 31 + EqualityComparer<QuestId>.Default.GetHashCode(QuestId);
      h = h * 31 + Level.GetHashCode();
      return h;
    }
  }
  public static bool operator ==(HintLevelChanged l, HintLevelChanged r) => l.Equals(r);
  public static bool operator !=(HintLevelChanged l, HintLevelChanged r) => !l.Equals(r);
  public override string ToString() => $"HintLevelChanged {{ QuestId = {QuestId}, Level = {Level} }}";
}

[Serializable]
public readonly struct NavigationCompleted : IEquatable<NavigationCompleted> {
  public readonly InteractionId Target;
  public readonly NpcId ByNpc;
  public NavigationCompleted(InteractionId Target, NpcId ByNpc) {
    this.Target = Target; this.ByNpc = ByNpc;
  }
  public void Deconstruct(out InteractionId target, out NpcId byNpc) {
    target = Target; byNpc = ByNpc;
  }
  public bool Equals(NavigationCompleted other) {
    return EqualityComparer<InteractionId>.Default.Equals(Target, other.Target)
      && EqualityComparer<NpcId>.Default.Equals(ByNpc, other.ByNpc);
  }
  public override bool Equals(object obj) => obj is NavigationCompleted o && Equals(o);
  public override int GetHashCode() {
    unchecked {
      int h = 17;
      h = h * 31 + EqualityComparer<InteractionId>.Default.GetHashCode(Target);
      h = h * 31 + EqualityComparer<NpcId>.Default.GetHashCode(ByNpc);
      return h;
    }
  }
  public static bool operator ==(NavigationCompleted l, NavigationCompleted r) => l.Equals(r);
  public static bool operator !=(NavigationCompleted l, NavigationCompleted r) => !l.Equals(r);
  public override string ToString() => $"NavigationCompleted {{ Target = {Target}, ByNpc = {ByNpc} }}";
}

[Serializable]
public readonly struct DialogueRequested : IEquatable<DialogueRequested> {
  public readonly VoiceProfileId Voice;
  public readonly LanguageCode Lang;
  public readonly AudioPriority Priority;
  public readonly string Text;
  public DialogueRequested(VoiceProfileId Voice, LanguageCode Lang, AudioPriority Priority, string Text) {
    this.Voice = Voice; this.Lang = Lang; this.Priority = Priority; this.Text = Text;
  }
  public void Deconstruct(out VoiceProfileId voice, out LanguageCode lang, out AudioPriority priority, out string text) {
    voice = Voice; lang = Lang; priority = Priority; text = Text;
  }
  public bool Equals(DialogueRequested other) {
    return EqualityComparer<VoiceProfileId>.Default.Equals(Voice, other.Voice)
      && EqualityComparer<LanguageCode>.Default.Equals(Lang, other.Lang)
      && Priority == other.Priority
      && EqualityComparer<string>.Default.Equals(Text, other.Text);
  }
  public override bool Equals(object obj) => obj is DialogueRequested o && Equals(o);
  public override int GetHashCode() {
    unchecked {
      int h = 17;
      h = h * 31 + EqualityComparer<VoiceProfileId>.Default.GetHashCode(Voice);
      h = h * 31 + EqualityComparer<LanguageCode>.Default.GetHashCode(Lang);
      h = h * 31 + Priority.GetHashCode();
      h = h * 31 + EqualityComparer<string>.Default.GetHashCode(Text);
      return h;
    }
  }
  public static bool operator ==(DialogueRequested l, DialogueRequested r) => l.Equals(r);
  public static bool operator !=(DialogueRequested l, DialogueRequested r) => !l.Equals(r);
  public override string ToString() => $"DialogueRequested {{ Voice = {Voice}, Lang = {Lang}, Priority = {Priority}, Text = {Text} }}";
}

[Serializable]
public readonly struct VocabularyPlayed : IEquatable<VocabularyPlayed> {
  public readonly WordId WordId;
  public readonly VocabularyAudioMode Mode;
  public readonly bool FromCache;
  public VocabularyPlayed(WordId WordId, VocabularyAudioMode Mode, bool FromCache) {
    this.WordId = WordId; this.Mode = Mode; this.FromCache = FromCache;
  }
  public void Deconstruct(out WordId wordId, out VocabularyAudioMode mode, out bool fromCache) {
    wordId = WordId; mode = Mode; fromCache = FromCache;
  }
  public bool Equals(VocabularyPlayed other) {
    return EqualityComparer<WordId>.Default.Equals(WordId, other.WordId)
      && Mode == other.Mode
      && FromCache == other.FromCache;
  }
  public override bool Equals(object obj) => obj is VocabularyPlayed o && Equals(o);
  public override int GetHashCode() {
    unchecked {
      int h = 17;
      h = h * 31 + EqualityComparer<WordId>.Default.GetHashCode(WordId);
      h = h * 31 + Mode.GetHashCode();
      h = h * 31 + FromCache.GetHashCode();
      return h;
    }
  }
  public static bool operator ==(VocabularyPlayed l, VocabularyPlayed r) => l.Equals(r);
  public static bool operator !=(VocabularyPlayed l, VocabularyPlayed r) => !l.Equals(r);
  public override string ToString() => $"VocabularyPlayed {{ WordId = {WordId}, Mode = {Mode}, FromCache = {FromCache} }}";
}
