// B_Brain/Milo.cs — Agent B (W0-T1). Milo companion voice (Tier3 stub).
// Static class; bound EXACTLY as GameInstaller calls:
//   Milo.Bind(IGameEventBus, IQuestService, ILearningService, IHintService, IAudioDirector)
// All output goes via IAudioDirector.SpeakAsync with VoiceProfileId milo_v1 —
// Milo NEVER calls providers (TTS/STT/Worker) directly. Quest/Learning/Hint refs
// are retained for Tier3 context-aware lines; W0-T1 lines are fixed content.
// Lines mirror Content/dialogues/milo.json; every line is <= 8 words.
using System;

public static class Milo {
  static IGameEventBus _bus;
  static IQuestService _quests;
  static ILearningService _learning;
  static IHintService _hints;
  static IAudioDirector _audio;
  static bool _bound;

  static readonly VoiceProfileId MiloVoice = new VoiceProfileId("milo_v1");
  static readonly LanguageCode EnUs = new LanguageCode("en-US");

  public static bool IsBound {
    get { return _bound; }
  }

  public static void Bind(IGameEventBus bus, IQuestService quests, ILearningService learning, IHintService hints, IAudioDirector audio) {
    _bus = bus;
    _quests = quests;
    _learning = learning;
    _hints = hints;
    _audio = audio;
    _bound = true;
  }

  // "Great(1) Let's(2) try(3) together(4)"
  public static void Encourage() {
    Say("Great! Let's try together!", SpeechStyle.Excited, AudioPriority.P4_Feedback);
  }

  // "Perfect(1) Good(2) job(3)"
  public static void Celebrate() {
    Say("Perfect! Good job!", SpeechStyle.Excited, AudioPriority.P4_Feedback);
  }

  // "Come(1) with(2) me(3)"
  public static void PointHint() {
    Say("Come with me!", SpeechStyle.Clear, AudioPriority.P3_Dialogue);
  }

  // "Watch(1) me(2) do(3) it(4)"
  public static void DemoHint() {
    Say("Watch me do it!", SpeechStyle.Clear, AudioPriority.P3_Dialogue);
  }

  static void Say(string text, SpeechStyle style, AudioPriority priority) {
    if (!_bound || _audio == null) return;
    var req = new DialogueRequest(text, MiloVoice, EnUs, 1f, 1f, style, AudioFormat.Mp3_44100, priority);
    _audio.SpeakAsync(req); // fire-and-forget: the Director owns playback/caching.
  }
}
