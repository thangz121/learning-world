// Tests/EditMode/CT-P15_ChildAudioFrontend.cs — Unit & Stress Tests for Child-VAD & Adult Suppression Engine.
using NUnit.Framework;
using System;
using System.Diagnostics;

public class CT_P15_ChildAudioFrontend {

  static float[] GenerateSineWave(float frequency, float durationSec, int sampleRate, float amplitude) {
    int totalSamples = (int)(durationSec * sampleRate);
    float[] samples = new float[totalSamples];
    float angularFreq = 2f * (float)Math.PI * frequency / sampleRate;
    for (int i = 0; i < totalSamples; i++) {
      samples[i] = amplitude * (float)Math.Sin(angularFreq * i);
    }
    return samples;
  }

  [Test]
  public void P13_BandpassFilter_AttenuatesLowAndHighFrequencies() {
    int sampleRate = 16000;
    // 50Hz (rumble) should be attenuated heavily by 180Hz highpass
    float[] low50Hz = GenerateSineWave(50f, 0.1f, sampleRate, 1.0f);
    float[] filteredLow = ChildAudioFrontend.ApplyBandpass(low50Hz, sampleRate, 180f, 7000f);

    float lowInEnergy = VoiceActivity.MeanAbsolute(low50Hz);
    float lowOutEnergy = VoiceActivity.MeanAbsolute(filteredLow);
    Assert.Less(lowOutEnergy, lowInEnergy * 0.35f, "50Hz desk vibration should be attenuated significantly");

    // 350Hz (child fundamental frequency) should pass through clearly
    float[] child350Hz = GenerateSineWave(350f, 0.1f, sampleRate, 0.5f);
    float[] filteredChild = ChildAudioFrontend.ApplyBandpass(child350Hz, sampleRate, 180f, 7000f);
    float childOutEnergy = VoiceActivity.MeanAbsolute(filteredChild);
    Assert.Greater(childOutEnergy, 0.3f, "350Hz child voice fundamental frequency must pass through cleanly");
  }

  [Test]
  public void P13_AutoGainAndLimiter_TamesScreamingChildWithoutClipping() {
    int sampleRate = 16000;
    // Loud scream (amplitude 2.5)
    float[] scream = GenerateSineWave(400f, 0.1f, sampleRate, 2.5f);
    float[] limited = ChildAudioFrontend.ApplyAgcAndLimiter(scream);

    float maxPeak = 0f;
    for (int i = 0; i < limited.Length; i++) {
      float abs = Math.Abs(limited[i]);
      if (abs > maxPeak) maxPeak = abs;
    }

    // Soft-limiter must compress peaks strictly below 1.0 (anti-clipping)
    Assert.LessOrEqual(maxPeak, 0.96f, "Screaming peaks must be soft-limited strictly below 1.0f");
  }

  [Test]
  public void P13_ChildVAD_Detects4YearOldVoiceAccurately() {
    int sampleRate = 16000;
    // 320Hz child tone for 0.5 seconds
    float[] childVoice = GenerateSineWave(320f, 0.5f, sampleRate, 0.3f);
    ChildFrontendMetadata meta;
    float[] processed = ChildAudioFrontend.Process(childVoice, sampleRate, out meta);

    Assert.Greater(processed.Length, 0, "Valid child speech should NOT be dropped");
    Assert.AreEqual("SPEECH", meta.VadState);
    Assert.IsTrue(meta.IsChild, "Should be recognized as child voice");
    Assert.GreaterOrEqual(meta.F0Avg, 250f);
    Assert.LessOrEqual(meta.F0Avg, 480f);
    Assert.IsFalse(meta.PromptingDetected);
  }

  [Test]
  public void P13_AdultVoiceSuppression_DropsPromptingParentVoice() {
    int sampleRate = 16000;
    // 130Hz deep adult father voice prompting for 250ms (> 120ms threshold)
    float[] adultVoice = GenerateSineWave(130f, 0.25f, sampleRate, 0.4f);
    ChildFrontendMetadata meta;
    float[] processed = ChildAudioFrontend.Process(adultVoice, sampleRate, out meta);

    Assert.AreEqual(0, processed.Length, "Adult prompting stream MUST be completely dropped");
    Assert.AreEqual("DROPPED_ADULT", meta.VadState);
    Assert.IsTrue(meta.PromptingDetected, "Adult prompting flag must be TRUE");
    Assert.IsFalse(meta.IsChild, "Adult voice must NOT be tagged as child");
  }

  [Test]
  public void P13_ZeroLatency_ExecutesUnder5Milliseconds() {
    int sampleRate = 16000;
    // 1.0 second of audio (16,000 samples)
    float[] oneSecAudio = GenerateSineWave(300f, 1.0f, sampleRate, 0.2f);

    // Warm-up
    ChildFrontendMetadata warmMeta;
    ChildAudioFrontend.Process(oneSecAudio, sampleRate, out warmMeta);

    // Measure latency
    Stopwatch sw = Stopwatch.StartNew();
    ChildFrontendMetadata meta;
    ChildAudioFrontend.Process(oneSecAudio, sampleRate, out meta);
    sw.Stop();

    Assert.Less(sw.ElapsedMilliseconds, 5, $"DSP processing latency for 1s audio was {sw.ElapsedMilliseconds}ms, must be < 5ms");
  }
}
