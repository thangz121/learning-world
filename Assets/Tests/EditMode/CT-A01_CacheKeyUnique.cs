// CT-A01: same text, different VoiceProfile/rate -> different cache keys. Owner: Agent D (W0-T1 GREEN).
// Real: AudioCache.CacheKey SHA256(text|voice|locale|rate|pitch|style|format).
using NUnit.Framework;

public class CT_A01_CacheKeyUnique {
  static string Key(string voice, float rate) {
    return AudioCache.CacheKey("Apple", new VoiceProfileId(voice), new LanguageCode("en-US"),
      rate, 0f, SpeechStyle.Clear, AudioFormat.Mp3_44100);
  }

  [Test] public void CT_A01() {
    string learn = Key("learning_v1", 0.85f);
    string milo = Key("milo_v1", 0.85f);
    string slow = Key("learning_v1", 0.70f);
    Assert.AreNotEqual(learn, milo, "voice must change the key (Milo vs Learning never collide)");
    Assert.AreNotEqual(learn, slow, "rate must change the key");
    Assert.AreEqual(learn, Key("learning_v1", 0.85f), "same inputs must hash stable");
    Assert.AreEqual(64, learn.Length, "SHA256 hex length");
    foreach (char c in learn) {
      bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
      Assert.IsTrue(hex, "lowercase hex only");
    }
  }
}
