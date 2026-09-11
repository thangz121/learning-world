// CT-009: "apple"x10/5s same interaction counts as 1 Meaningful. Owner: Agent C+D (W0-T1).
// D-half GREEN: AudioDirector dedupes identical text+voice within 2s to ONE synthesis+play.
// (C Meaningful-voice counter lands with C's W0-T1; this test locks D's anti-spam side.)
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class CT_009_MeaningfulDedup {
  sealed class CT009_StubTts : ISpeechSynthesisProvider {
    public int Calls;
    public Task<TtsAudioResult> SynthesizeAsync(TtsRequest request, CancellationToken ct) {
      Calls++;
      return Task.FromResult(new TtsAudioResult {
        Mp3 = new byte[] { 1, 2, 3 }, CacheKey = "ct009", FromCache = false,
      });
    }
  }

  sealed class CT009_CountingDirector : AudioDirector {
    public int Plays;
    static AudioClip _clip;
    public CT009_CountingDirector(IGameEventBus bus, ISpeechSynthesisProvider tts)
      : base(bus, tts) { }
    protected override Task<AudioClip> DecodeAudioAsync(
        byte[] mp3, string cacheKey, CancellationToken ct) {
      if (_clip == null) _clip = AudioClip.Create("ct009", 100, 1, 44100, false);
      return Task.FromResult(_clip);
    }
    protected override Task PlayClipAsync(
        AudioClip clip, AudioPriority priority, CancellationToken ct) {
      Plays++;
      return Task.CompletedTask;
    }
    protected override void StoreCaches(string key, AudioClip clip, byte[] mp3) {
      // No disk in tests: memory path is bypassed, decode is stubbed above.
    }
  }

  [Test] public async Task CT_009() {
    var tts = new CT009_StubTts();
    var dir = new CT009_CountingDirector(new GameEventBus(), tts);
    var req = new DialogueRequest("Apple", new VoiceProfileId("learning_v1"),
      new LanguageCode("en-US"), 0.85f, 0f, SpeechStyle.Clear,
      AudioFormat.Mp3_44100, AudioPriority.P1_Pronunciation);
    for (int i = 0; i < 10; i++) await dir.SpeakAsync(req);
    Assert.AreEqual(1, dir.Plays, "10 rapid identical requests must play exactly once");
    Assert.AreEqual(1, tts.Calls, "10 rapid identical requests must synthesize exactly once");
  }
}
