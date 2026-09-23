// B_Brain/Milo.cs — Agent B (W0-T1). Milo companion voice (Tier3 stub).
// Static class; bound EXACTLY as GameInstaller calls:
//   Milo.Bind(IGameEventBus, IQuestService, ILearningService, IHintService, IAudioDirector)
// All output goes via IAudioDirector.SpeakAsync with VoiceProfileId milo_v1 —
// Milo NEVER calls providers (TTS/STT/Worker) directly. Quest/Learning/Hint refs
// are retained for Tier3 context-aware lines; W0-T1 + W1 lines are fixed content.
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
    Say(T("Great! Let's try together!", "Giỏi! Mình thử nhé!"), SpeechStyle.Excited, AudioPriority.P4_Feedback);
  }

  // "Perfect(1) Good(2) job(3)"
  public static void Celebrate() {
    Say(T("Perfect! Good job!", "Tuyệt vời! Giỏi lắm!"), SpeechStyle.Excited, AudioPriority.P4_Feedback);
  }

  // "Come(1) with(2) me(3)"
  public static void PointHint() {
    Say(T("Come with me!", "Đi với mình nhé!"), SpeechStyle.Clear, AudioPriority.P3_Dialogue);
  }

  // "Watch(1) me(2) do(3) it(4)"
  public static void DemoHint() {
    Say(T("Watch me do it!", "Xem mình làm nhé!"), SpeechStyle.Clear, AudioPriority.P3_Dialogue);
  }

  // "Hello(1) I(2) am(3) Milo(4)"
  public static void Greet() {
    Say(T("Hello! I am Milo!", "Xin chào! Mình là Milo!"), SpeechStyle.Clear, AudioPriority.P3_Dialogue);
  }

  // "Find(1) the(2) apple(3)"
  public static void InstructFind() {
    Say(T("Find the apple!", "Tìm quả táo nhé!"), SpeechStyle.Clear, AudioPriority.P2_Instruction);
  }

  // "Bring(1) it(2) to(3) Mia(4)"
  public static void InstructBring() {
    Say(T("Bring it to Mia!", "Mang cho cô Mia!"), SpeechStyle.Clear, AudioPriority.P2_Instruction);
  }

  // "You(1) found(2) it(3)"
  public static void PraiseFound() {
    Say(T("You found it!", "Con tìm thấy rồi!"), SpeechStyle.Excited, AudioPriority.P4_Feedback);
  }

  static int _lastInstructionIndex; // default 0 -> find.

  // Records the instruction for RepeatInstruction, then says it now:
  // 0 -> InstructFind, anything else -> InstructBring.
  public static void SetInstructionTarget(int objectiveIndex) {
    _lastInstructionIndex = objectiveIndex;
    if (objectiveIndex == 0) InstructFind();
    else InstructBring();
  }

  // Re-says the last instruction set via SetInstructionTarget (default: find).
  public static void RepeatInstruction() {
    if (_lastInstructionIndex == 0) InstructFind();
    else InstructBring();
  }

  static string T(string en, string vi) { return DialogueLang.T(en, vi); }

  static void Say(string text, SpeechStyle style, AudioPriority priority) {
    if (!_bound || _audio == null) return;
    var req = new DialogueRequest(text, MiloVoice, DialogueLang.Language, 1f, 1f, style, AudioFormat.Mp3_44100, priority);
    _audio.SpeakAsync(req); // fire-and-forget: the Director owns playback/caching.
  }
}
