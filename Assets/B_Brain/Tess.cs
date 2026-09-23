// B_Brain/Tess.cs — Phase 3.0.x S3. Tess Math-host voice (mirrors Mia.cs).
// Static class; bound by GameInstaller with the shared IAudioDirector.
// All output goes via IAudioDirector.SpeakAsync with VoiceProfileId
// npc_female_01 — Tess NEVER calls providers (TTS/STT/Worker) directly.
// Ask/praise lines mirror Content/dialogues/manifest.json (math_01/math_02,
// pinned by CT-P36); name/instruction/celebrate lines are Tess literals pinned
// by CT-P36 (manifest cap is frozen at 40, math pack grows in Phase 3.1).
// Every line is <= 4 words (non-milo cap is 6). C# 9.0 only.
using System;

public static class Tess {
  static IAudioDirector _audio;
  static bool _bound;

  static readonly VoiceProfileId TessVoice = new VoiceProfileId("npc_female_01");

  public static bool IsBound {
    get { return _bound; }
  }

  public static void Bind(IAudioDirector audio) {
    _audio = audio;
    _bound = true;
  }

  // Manifest math_01: quest instruction (first talk + repeats).
  // "Find(1) the(2) one(3)"
  public static void SayFind() {
    Say(T("Find the one!", "Tìm số một!"), SpeechStyle.Clear, AudioPriority.P2_Instruction);
  }

  // Instruction after the find completes (bring phase).
  // "Bring(1) it(2) to(3) Tess(4)"
  public static void SayBring() {
    Say(T("Bring it to Tess!", "Mang cho cô Tess!"), SpeechStyle.Clear, AudioPriority.P2_Instruction);
  }

  // Manifest math_02: find praise.
  // "Great(1) One(2)"
  public static void PraiseFound() {
    Say(T("Great! One!", "Giỏi! Một!"), SpeechStyle.Excited, AudioPriority.P4_Feedback);
  }

  // Quest completion celebration.
  // "Perfect(1) Well(2) done(3)"
  public static void Celebrate() {
    Say(T("Perfect! Well done!", "Tuyệt vời! Giỏi lắm!"), SpeechStyle.Excited, AudioPriority.P4_Feedback);
  }

  // Name readout (every Tess click reads who she is).
  // "I(1) am(2) Tess(3)"
  public static void SayName() {
    Say(T("I am Tess!", "Cô là Tess!"), SpeechStyle.Clear, AudioPriority.P3_Dialogue);
  }

  static string T(string en, string vi) { return DialogueLang.T(en, vi); }

  static void Say(string text, SpeechStyle style, AudioPriority priority) {
    if (!_bound || _audio == null) return;
    var req = new DialogueRequest(text, TessVoice, DialogueLang.Language, 1f, 1f, style, AudioFormat.Mp3_44100, priority);
    _audio.SpeakAsync(req); // fire-and-forget: the Director owns playback/caching.
  }
}
