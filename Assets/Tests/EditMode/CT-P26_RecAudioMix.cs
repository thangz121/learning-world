// CT-P26: recording audio mix (player report 2026-09-17: exports were
// voice-only + mic too quiet). Pins RecAudioMixer: same-length output,
// mic gain, game add, clamp, null/short-game tolerance. Pure C#, no Unity
// audio hardware. C# 9.0 only.
using NUnit.Framework;

public class CT_P26_RecAudioMix {
  static byte[] Pcm16(params short[] samples) {
    var b = new byte[samples.Length * 2];
    for (int i = 0; i < samples.Length; i++) {
      b[i * 2] = (byte)(samples[i] & 0xFF);
      b[i * 2 + 1] = (byte)((samples[i] >> 8) & 0xFF);
    }
    return b;
  }

  static short Get(byte[] pcm, int i) {
    return (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
  }

  [Test] public void P26A_LengthPreserved() {
    byte[] mic = Pcm16(1000, -1000, 0, 8000);
    byte[] game = Pcm16(500, 500, 500, 500);
    byte[] mixed = RecAudioMixer.MixMicWithGame(mic, game, 2f, 1f);
    Assert.AreEqual(mic.Length, mixed.Length, "mix never changes chunk length (counts stay honest)");
  }

  [Test] public void P26B_MicGainApplied() {
    byte[] mic = Pcm16(4000, -4000);
    byte[] mixed = RecAudioMixer.MixMicWithGame(mic, null, 2f, 1f);
    Assert.AreEqual(8000, Get(mixed, 0), 2, "silent game + micGain 2.0 doubles the voice");
    Assert.AreEqual(-8000, Get(mixed, 1), 2);
  }

  [Test] public void P26C_GameAddsIn() {
    byte[] mic = Pcm16(0, 0);
    byte[] game = Pcm16(4000, -4000);
    byte[] mixed = RecAudioMixer.MixMicWithGame(mic, game, 2f, 1f);
    Assert.AreEqual(4000, Get(mixed, 0), 2, "game dialogue lands in the file even when the mic is silent");
    Assert.AreEqual(-4000, Get(mixed, 1), 2);
  }

  [Test] public void P26D_ClampNeverWraps() {
    byte[] mic = Pcm16(30000);
    byte[] game = Pcm16(30000);
    byte[] mixed = RecAudioMixer.MixMicWithGame(mic, game, 2f, 1f);
    short v = Get(mixed, 0);
    Assert.Greater(v, 30000, "loud + loud stays loud");
    Assert.LessOrEqual(v, 32767, "clamped, never wrapped negative");
  }

  [Test] public void P26E_ShortGameZeroPads() {
    byte[] mic = Pcm16(1000, 1000, 1000, 1000);
    byte[] game = Pcm16(1000, 1000);
    byte[] mixed = RecAudioMixer.MixMicWithGame(mic, game, 1f, 1f);
    Assert.AreEqual(2000, Get(mixed, 0), 2);
    Assert.AreEqual(1000, Get(mixed, 2), 2, "missing game tail pads with silence, never garbage");
    Assert.AreEqual(1000, Get(mixed, 3), 2);
  }

  [Test] public void P26F_BadInputNeverThrows() {
    Assert.AreEqual(0, RecAudioMixer.MixMicWithGame(null, null, 2f, 1f).Length);
    Assert.AreEqual(0, RecAudioMixer.MixMicWithGame(new byte[1], null, 2f, 1f).Length);
    Assert.AreEqual(2.0f, RecAudioMixer.SanitizeGain(float.NaN, 2f), 0.001f);
    Assert.AreEqual(0f, RecAudioMixer.SanitizeGain(-3f, 2f), 0.001f);
    Assert.AreEqual(RecAudioMixer.MaxGain, RecAudioMixer.SanitizeGain(99f, 2f), 0.001f);
  }

  [Test] public void P26G_ConfigDefaultsCarryMix() {
    var d = MediaRecordingConfig.Default;
    string reason;
    Assert.IsTrue(d.Validate(out reason), reason);
    Assert.AreEqual(RecAudioMixer.DefaultMicGain, d.MicGain, 0.001f);
    Assert.AreEqual(RecAudioMixer.DefaultGameGain, d.GameGain, 0.001f);
    Assert.IsTrue(d.MixGameAudio, "game audio mixes into exports by default");
    var bad = MediaRecordingConfig.Default;
    bad.MicGain = 0f;
    Assert.IsFalse(bad.Validate(out reason), "zero mic gain is refused, never a silent file");
  }
}
