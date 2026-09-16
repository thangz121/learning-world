// CT-P24: Phase 2.3e laptop-mic bridge — deterministic suite.
// The converter (any channels/rate -> mono PCM16 @16k) is pure and pinned
// here with synthetic vectors. Hardware capture itself (no mics headless)
// is proven in the user run; the service reuses the proven bounded audio
// queue/pump/wav, phone keeps precedence (no mixing), switches flag.
using NUnit.Framework;
using System;

public class CT_P24_LocalMicCapture {
  static float[] ConstTone(int frames, int channels, float level) {
    var b = new float[frames * channels];
    for (int i = 0; i < b.Length; i++) b[i] = level;
    return b;
  }

  static float PeakPcm16(byte[] pcm) {
    float peak = 0f;
    for (int i = 0; i + 1 < pcm.Length; i += 2) {
      short s = (short)(pcm[i] | (pcm[i + 1] << 8));
      float a = Math.Abs(s / 32768f);
      if (a > peak) peak = a;
    }
    return peak;
  }

  [Test] public void P24A_MonoPassthroughBitNearExact() {
    byte[] pcm = AudioChunkConverter.ToMono16(ConstTone(1600, 1, 0.25f), 1, 16000);
    Assert.AreEqual(3200, pcm.Length);
    Assert.AreEqual(0.25f, PeakPcm16(pcm), 0.001f);
  }

  [Test] public void P24B_StereoDownmixAverages() {
    var stereo = new float[1600 * 2];
    for (int i = 0; i < 1600; i++) {
      stereo[i * 2] = 0.5f;
      stereo[i * 2 + 1] = -0.5f;
    }
    byte[] pcm = AudioChunkConverter.ToMono16(stereo, 2, 16000);
    Assert.AreEqual(3200, pcm.Length);
    Assert.AreEqual(0f, PeakPcm16(pcm), 0.001f, "opposite phases cancel");
    var stereo2 = new float[800 * 2];
    for (int i = 0; i < stereo2.Length; i++) stereo2[i] = 0.4f;
    byte[] pcm2 = AudioChunkConverter.ToMono16(stereo2, 2, 16000);
    Assert.AreEqual(0.4f, PeakPcm16(pcm2), 0.001f);
  }

  [Test] public void P24C_Resample4824To16() {
    byte[] pcm = AudioChunkConverter.ToMono16(ConstTone(4800, 1, 0.3f), 1, 48000, 16000);
    Assert.AreEqual(3200, pcm.Length, "4800 @48k -> 1600 @16k");
    Assert.AreEqual(0.3f, PeakPcm16(pcm), 0.002f, "level preserved through resample");
  }

  [Test] public void P24D_ClipsAndGuards() {
    byte[] over = AudioChunkConverter.ToMono16(new float[] { 2f, -3f, 0.5f }, 1, 16000);
    Assert.AreEqual(6, over.Length);
    Assert.AreEqual(1f, PeakPcm16(over), 0.001f, "clamped, never wraps");
    Assert.AreEqual(0, AudioChunkConverter.ToMono16(null, 1, 16000).Length);
    Assert.AreEqual(0, AudioChunkConverter.ToMono16(new float[0], 1, 16000).Length);
    Assert.AreEqual(0, AudioChunkConverter.ToMono16(new float[] { 0.1f }, 1, 5, 16000).Length);
    Assert.AreEqual(0, AudioChunkConverter.ToMono16(new float[] { 0.1f }, 0, 16000).Length);
  }

  [Test] public void P24E_TargetRateConst() {
    Assert.AreEqual(16000, AudioChunkConverter.TargetRate);
  }
}
