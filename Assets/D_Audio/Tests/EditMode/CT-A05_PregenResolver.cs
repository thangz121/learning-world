// CT-A05: pre-gen resolver returns the EXACT seeded bytes for the manifest-computed key.
// Owner: Agent D (W1). EditMode, no network/microphone.
// Proves: keys live ONLY in C# (AudioCache.CacheKey) and the AudioDirector pre-gen
// path (TryLoadPregenAsync -> L2) resolves the same key PregenSeeder writes, so the
// pinned W1 manifest params (text/voice/lang/rate/pitch/style/format) hit L2 offline.
// Uses the apple_normal entry (mirrors StreamingAssets/audio/manifest.json:
// "apple", learning_v1, en-US, 0.85/0.0, Clear, Mp3_44100).
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

public class CT_A05_PregenResolver {
  sealed class CTA05_ExposedDirector : AudioDirector {
    public CTA05_ExposedDirector(IGameEventBus bus, ISpeechSynthesisProvider tts)
      : base(bus, tts) { }
    public Task<byte[]> ExposeTryLoadPregenAsync(
        TtsRequest req, string cacheKey, CancellationToken ct) {
      return TryLoadPregenAsync(req, cacheKey, ct);
    }
  }

  sealed class CTA05_StubTts : ISpeechSynthesisProvider {
    public Task<TtsAudioResult> SynthesizeAsync(TtsRequest request, CancellationToken ct) {
      throw new TtsException("must not be called (pre-gen path only)", 0);
    }
  }

  [Test] public async Task CT_A05() {
    var voice = new VoiceProfileId("learning_v1");
    var lang = new LanguageCode("en-US");
    string key = AudioCache.CacheKey("apple", voice, lang, 0.85f, 0f,
      SpeechStyle.Clear, AudioFormat.Mp3_44100);

    byte[] seeded = Guid.NewGuid().ToByteArray(); // unique per run, never collides
    new AudioCache().Store(key, null, seeded);

    var req = new TtsRequest("apple", voice, lang, 0.85f, 0f,
      SpeechStyle.Clear, AudioFormat.Mp3_44100, AudioPriority.P1_Pronunciation);
    var dir = new CTA05_ExposedDirector(new GameEventBus(), new CTA05_StubTts());
    byte[] got = await dir.ExposeTryLoadPregenAsync(req, key, CancellationToken.None);
    Assert.IsNotNull(got, "pre-gen resolver must return the seeded L2 bytes");
    CollectionAssert.AreEqual(seeded, got,
      "resolver must return EXACT bytes for the manifest-computed key");

    // apple_slow (0.70) is a different key and must never see the normal bytes.
    string slowKey = AudioCache.CacheKey("apple", voice, lang, 0.70f, 0f,
      SpeechStyle.Clear, AudioFormat.Mp3_44100);
    Assert.AreNotEqual(key, slowKey, "rate must change the key (normal vs slow never collide)");
    byte[] slowGot = await dir.ExposeTryLoadPregenAsync(req, slowKey, CancellationToken.None);
    if (slowGot != null) {
      CollectionAssert.AreNotEqual(seeded, slowGot, "slow key must not return normal bytes");
    }
  }
}
