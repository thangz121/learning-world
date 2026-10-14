// Assets/Tests/EditMode/CT-P21_MaxSpecAdaptiveSpeechProvider.cs — WP A runtime-chaining honesty pins.
// Pins: no fabricated transcript/confidence, real payload attachment, per-word attempt tracking.

using System;
using System.Threading;
using NUnit.Framework;

[TestFixture]
public class CT_P21_MaxSpecAdaptiveSpeechProvider {

  static CapturedSpeech VoicedTone(float seconds = 0.7f, int sampleRate = 16000, float amp = 0.3f) {
    int n = (int)(seconds * sampleRate);
    var s = new float[n];
    for (int i = 0; i < n; i++) {
      s[i] = amp * (float)Math.Sin(2.0 * Math.PI * 220.0 * i / sampleRate);
    }
    return new CapturedSpeech {
      Samples = s,
      SampleRate = sampleRate,
      Channels = 1,
      DurationSec = seconds,
      MeanEnergy = amp * 0.63f,
      VoicedSec = seconds * 0.8f,
      Cancelled = false,
      Error = ""
    };
  }

  [Test]
  public void P21_OfflineProvider_DoesNotFabricateTranscriptOrConfidence() {
    var provider = new MaxSpecAdaptiveSpeechProvider(targets: null);
    var result = provider.RecognizeAsync(VoicedTone(), new WordId("cat"), CancellationToken.None).Result;

    Assert.That(provider.ProvidesTranscript, Is.False,
        "No ASR text exists offline — provider must not claim transcript capability.");
    Assert.That(result.HasSpeech, Is.True);
    Assert.That(result.Transcript, Is.EqualTo(string.Empty),
        "Must not echo the target word as a fake transcript.");
    Assert.That(result.RecognitionConfidence, Is.EqualTo(0f),
        "Must not invent confidence (was a fake 0.85/0.2 readout).");
  }

  [Test]
  public void P21_OfflineProvider_AttachesRealFusionAndSessionPayloads() {
    var provider = new MaxSpecAdaptiveSpeechProvider(targets: null);

    var pe1 = provider.RecognizeAsync(VoicedTone(), new WordId("cat"), CancellationToken.None).Result.Pronunciation;
    var pe2 = provider.RecognizeAsync(VoicedTone(), new WordId("cat"), CancellationToken.None).Result.Pronunciation;

    Assert.That(pe1.HasPhonemeData, Is.True);
    Assert.That(pe1.HasFusionPayload, Is.True, "Fusion payload must be the real Phase 4.1 output.");
    Assert.That(pe1.HasSessionPayload, Is.True, "Session payload must be the real Phase 5.0 output.");
    Assert.That(pe1.FluencyScore, Is.EqualTo(0f), "Fluency must be 0 (unavailable), not a fake 0.9.");
    Assert.That(pe2.SessionPayload.attempt_index, Is.EqualTo(2), "Attempt index must increment per word.");
  }
}
