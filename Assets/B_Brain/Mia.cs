// B_Brain/Mia.cs — Agent B. Mia shopkeeper voice (mirrors Milo.cs).
// Static class; bound by GameInstaller with the shared IAudioDirector.
// All output goes via IAudioDirector.SpeakAsync with VoiceProfileId mia_v1 —
// Mia NEVER calls providers (TTS/STT/Worker) directly.
// Lines mirror Content/dialogues/manifest.json (mia_v1 entries); CT-P30 pins
// the mirrors so code and content can never drift.
using System;

public static class Mia {
  static IAudioDirector _audio;
  static bool _bound;

  static readonly VoiceProfileId MiaVoice = new VoiceProfileId("mia_v1");

  public static bool IsBound {
    get { return _bound; }
  }

  public static void Bind(IAudioDirector audio) {
    _audio = audio;
    _bound = true;
  }

  // Manifest mia_01: name readout (every Mia click reads who she is).
  // "I(1) am(2) Mia(3)"
  public static void SayName() {
    Say(DialogueLang.T("I am Mia!", "Cô là Mia!"), SpeechStyle.Clear, AudioPriority.P3_Dialogue);
  }

  // Manifest retry_03: wrong bring (sad + "choose again").
  // "Try(1) again(2) please(3)"
  public static void SayRetry() {
    Say(DialogueLang.T("Try again please!", "Thử lại nhé!"), SpeechStyle.Clear, AudioPriority.P4_Feedback);
  }

  static void Say(string text, SpeechStyle style, AudioPriority priority) {
    if (!_bound || _audio == null) return;
    var req = new DialogueRequest(text, MiaVoice, DialogueLang.Language, 1f, 1f, style, AudioFormat.Mp3_44100, priority);
    _audio.SpeakAsync(req); // fire-and-forget: the Director owns playback/caching.
  }
}
