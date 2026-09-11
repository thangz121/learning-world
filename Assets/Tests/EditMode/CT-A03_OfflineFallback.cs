// CT-A03: Worker fail -> pre-gen local + FallbackSpeechProvider continues quest. Owner: Agent D (W0-T1 GREEN).
// Real chain: FallbackSpeechProvider emits Offline Raw -> SpeechAssessmentPolicy -> TryTogether
// (Hint demo path, never a score); AudioDirector TTS failure -> pre-gen fallback plays,
// SpeakAsync never throws (quest never blocks on realtime TTS).
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class CT_A03_OfflineFallback {
  sealed class CTA03_FailingTts : ISpeechSynthesisProvider {
    public Task<TtsAudioResult> SynthesizeAsync(TtsRequest request, CancellationToken ct) {
      throw new TtsException("simulated worker 500", 500);
    }
  }

  sealed class CTA03_PregenDirector : AudioDirector {
    public int Plays;
    public byte[] PregenBytes;
    static AudioClip _clip;
    public CTA03_PregenDirector(IGameEventBus bus, ISpeechSynthesisProvider tts)
      : base(bus, tts) { }
    protected override Task<byte[]> TryLoadPregenAsync(
        TtsRequest req, string cacheKey, CancellationToken ct) {
      return Task.FromResult(PregenBytes);
    }
    protected override Task<AudioClip> DecodeAudioAsync(
        byte[] mp3, string cacheKey, CancellationToken ct) {
      if (_clip == null) _clip = AudioClip.Create("cta03", 100, 1, 44100, false);
      return Task.FromResult(_clip);
    }
    protected override Task PlayClipAsync(
        AudioClip clip, AudioPriority priority, CancellationToken ct) {
      Plays++;
      return Task.CompletedTask;
    }
    protected override void StoreCaches(string key, AudioClip clip, byte[] mp3) { }
  }

  [Test] public async Task CT_A03() {
    // 1. Fallback emits exactly one Offline Raw and completes.
    var fallback = new FallbackSpeechProvider();
    RawSpeechResult got = default(RawSpeechResult);
    int fires = 0;
    fallback.Raw += r => { got = r; fires++; };
    Assert.DoesNotThrow(() => fallback.StartListening(new WordId("apple"), 5));
    Assert.AreEqual(1, fires, "fallback fires exactly once");
    Assert.AreEqual("Offline", got.ErrorReason);
    Assert.AreEqual("", got.Transcript);

    // 2. Policy routes Offline input to TryTogether (Hint demo, quest continues).
    SpeechResult assessed = new SpeechAssessmentPolicy().Assess(new WordId("apple"), got);
    Assert.AreEqual(SpeechLevel.TryTogether, assessed.Level);

    // 3. Online stub documents its W1 state instead of failing silently.
    var azure = new AzureSttProvider();
    Assert.Throws<InvalidOperationException>(
      () => azure.StartListening(new WordId("apple"), 5));
    Assert.DoesNotThrow(() => azure.Stop());

    // 4. Director survives worker failure via pre-gen, never throws.
    var dir = new CTA03_PregenDirector(new GameEventBus(), new CTA03_FailingTts()) {
      PregenBytes = new byte[] { 9, 9, 9 },
    };
    var req = new DialogueRequest("Apple", new VoiceProfileId("learning_v1"),
      new LanguageCode("en-US"), 0.85f, 0f, SpeechStyle.Clear,
      AudioFormat.Mp3_44100, AudioPriority.P1_Pronunciation);
    // NOTE: Unity's NUnit fork exposes only DoesNotThrowAsync(TestDelegate):void,
    // so `await` on it is CS4008. Explicit try/await preserves the intent on all versions.
    Exception thrown = null;
    try { await dir.SpeakAsync(req); } catch (Exception ex) { thrown = ex; }
    Assert.IsNull(thrown, "SpeakAsync must never throw (quest continues on TTS failure)");
    await dir.SpeakAsync(req);
    Assert.AreEqual(1, dir.Plays, "pre-gen fallback plays once (repeat call deduped)");
  }
}
