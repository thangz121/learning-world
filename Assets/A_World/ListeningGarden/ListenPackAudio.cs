// A_World/ListeningGarden/ListenPackAudio.cs — the shared prompt player for the
// listening pack (voice 'listen_v1', pregen masters ship offline). Used by the
// listening garden AND the speaking stage so one pack serves both skills.
// C# 9.0 only.
using System;

public static class ListenPackAudio {
  // Play the word via the frozen tuple the manifest pins (voice listen_v1,
  // en-US, rate 0.85 normal / 0.70 slow, pitch 0, Clear, Mp3_44100).
  public static void Play(IAudioDirector audio, string wordId, bool slow) {
    if (audio == null || string.IsNullOrEmpty(wordId)) return;
    try {
      var voice = new VoiceProfileId(ListeningWords.VoiceId);
      var lang = new LanguageCode(ListeningWords.Lang);
      float rate = slow ? ListeningWords.RateSlow : ListeningWords.RateNormal;
      var req = new DialogueRequest(ListeningWords.TextEn(wordId), voice, lang,
        rate, 0f, SpeechStyle.Clear, AudioFormat.Mp3_44100, AudioPriority.P1_Pronunciation);
      audio.SpeakAsync(req);
    } catch (Exception) { }
  }

  // Play the instruction "Find the <word>!" via voice listen_v1.
  public static void PlayFind(IAudioDirector audio, string wordId) {
    if (audio == null || string.IsNullOrEmpty(wordId)) return;
    try {
      var voice = new VoiceProfileId(ListeningWords.VoiceId);
      var lang = new LanguageCode(ListeningWords.Lang);
      string spoken = ListeningWords.TextEn(wordId);
      var req = new DialogueRequest("Find the " + spoken + "!", voice, lang,
        ListeningWords.RateNormal, 0f, SpeechStyle.Clear, AudioFormat.Mp3_44100, AudioPriority.P2_Instruction);
      audio.SpeakAsync(req);
    } catch (Exception) { }
  }
}
