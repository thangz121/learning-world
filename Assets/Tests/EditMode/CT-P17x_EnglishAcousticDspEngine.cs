// Tests/EditMode/CT-P17x_EnglishAcousticDspEngine.cs — Comprehensive Unit & Fixture Tests for Local Acoustic DSP Engine v2.2.
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;

public class CT_P17x_EnglishAcousticDspEngine {

  static float[] GenerateSine(float freq, float durationSec, int sampleRate, float amp) {
    int n = (int)(durationSec * sampleRate);
    float[] s = new float[n];
    float w = 2f * (float)Math.PI * freq / sampleRate;
    for (int i = 0; i < n; i++) s[i] = amp * (float)Math.Sin(w * i);
    return s;
  }

  static TargetPronunciation MakePronunciation(WordId word, string[] phoneIds, int syllables) {
    var pron = new TargetPronunciation {
      Word = word,
      SyllableCount = syllables,
      Phonemes = new TargetPhoneme[phoneIds.Length]
    };
    float totalW = 0f;
    for (int i = 0; i < phoneIds.Length; i++) {
      TargetPhoneme tp;
      if (PhonemeTable.TryResolve(phoneIds[i], out tp)) {
        pron.Phonemes[i] = tp;
        totalW += tp.DurationWeight;
      }
    }
    pron.TotalWeight = totalW;
    return pron;
  }

  static float[] LoadWavMono(string fullPath, out int sampleRate) {
    sampleRate = 16000;
    if (!File.Exists(fullPath)) return null;

    byte[] bytes = File.ReadAllBytes(fullPath);
    if (bytes.Length < 44) return null;

    // Parse standard RIFF WAV header
    sampleRate = BitConverter.ToInt32(bytes, 24);
    short channels = BitConverter.ToInt16(bytes, 22);
    short bitsPerSample = BitConverter.ToInt16(bytes, 34);

    int dataOffset = 12;
    while (dataOffset + 8 <= bytes.Length) {
      string chunkId = "" + (char)bytes[dataOffset] + (char)bytes[dataOffset + 1] + (char)bytes[dataOffset + 2] + (char)bytes[dataOffset + 3];
      int chunkSize = BitConverter.ToInt32(bytes, dataOffset + 4);
      dataOffset += 8;
      if (chunkId == "data") {
        int sampleCount = chunkSize / (bitsPerSample / 8) / channels;
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++) {
          int byteIdx = dataOffset + i * channels * (bitsPerSample / 8);
          if (byteIdx + 1 < bytes.Length) {
            short val = BitConverter.ToInt16(bytes, byteIdx);
            samples[i] = val / 32768.0f;
          }
        }
        return samples;
      }
      dataOffset += chunkSize;
    }
    return null;
  }

  [Test]
  public void P17x_VtlnAlpha_ClampedInValidChildRange() {
    int sampleRate = 16000;
    // 350Hz fundamental + 3200Hz median child F3
    float[] audio = new float[sampleRate / 2]; // 0.5s
    for (int i = 0; i < audio.Length; i++) {
      audio[i] = 0.2f * (float)Math.Sin(2f * Math.PI * 350 * i / sampleRate)
               + 0.1f * (float)Math.Sin(2f * Math.PI * 3200 * i / sampleRate);
    }
    var target = MakePronunciation(new WordId("ball"), new[] { "B", "AO", "L" }, 1);
    var payload = EnglishAcousticDspEngine.Analyze(audio, sampleRate, target);

    Assert.GreaterOrEqual(payload.vtln_alpha_factor, 0.78f);
    Assert.LessOrEqual(payload.vtln_alpha_factor, 0.92f);
  }

  [Test]
  public void P17x_OnsetAspiration_DetectsEnglishVotAndUnaspiratedTransfer() {
    int sampleRate = 16000;
    var target = MakePronunciation(new WordId("ten"), new[] { "T", "EH", "N" }, 1);

    // Case A: Aspirated English stop /t/ (VOT ~60ms)
    float[] aspiratedAudio = new float[sampleRate / 2];
    int burstSample = (int)(0.015f * sampleRate);
    aspiratedAudio[burstSample] = 0.8f; // Plosive release spike

    var rnd = new Random(42);
    for (int i = burstSample + 1; i < burstSample + (int)(0.055f * sampleRate); i++) {
      aspiratedAudio[i] = (float)(rnd.NextDouble() * 0.15 - 0.075);
    }
    int voiceStart = burstSample + (int)(0.060f * sampleRate);
    for (int i = voiceStart; i < aspiratedAudio.Length; i++) {
      aspiratedAudio[i] = 0.3f * (float)Math.Sin(2f * Math.PI * 320 * i / sampleRate);
    }

    var payloadA = EnglishAcousticDspEngine.Analyze(aspiratedAudio, sampleRate, target);
    Assert.IsTrue(payloadA.english_phonetic_metrics.onset_and_clusters.aspiration_passed, "Aspirated plosive with VOT ~60ms must pass");
    Assert.GreaterOrEqual(payloadA.english_phonetic_metrics.onset_and_clusters.score, 80f);

    // Case B: Unaspirated Vietnamese Transfer (VOT < 20ms, voicing starts almost immediately)
    float[] unaspiratedAudio = new float[sampleRate / 2];
    unaspiratedAudio[burstSample] = 0.8f;
    int quickVoiceStart = burstSample + (int)(0.010f * sampleRate); // 10ms VOT
    for (int i = quickVoiceStart; i < unaspiratedAudio.Length; i++) {
      unaspiratedAudio[i] = 0.3f * (float)Math.Sin(2f * Math.PI * 320 * i / sampleRate);
    }

    var payloadB = EnglishAcousticDspEngine.Analyze(unaspiratedAudio, sampleRate, target);
    Assert.IsFalse(payloadB.english_phonetic_metrics.onset_and_clusters.aspiration_passed, "VOT < 20ms with confident burst must trigger unaspirated transfer flag");
    Assert.LessOrEqual(payloadB.english_phonetic_metrics.onset_and_clusters.score, 60f);
  }

  [Test]
  public void P17x_Nucleus_RhoticF3Drop_VerifiesRColoredVowels() {
    int sampleRate = 16000;
    var target = MakePronunciation(new WordId("star"), new[] { "S", "T", "AA", "R" }, 1);

    // Simulate child voice with strong F3 drop (< 2100Hz, chụm sát vào F2)
    float[] rhoticAudio = new float[sampleRate / 2];
    for (int i = 0; i < rhoticAudio.Length; i++) {
      float f1 = 650f;
      float f2 = 1750f;
      float f3 = 2050f; // F3 dropped close to F2 (delta = 300Hz < 400Hz)
      rhoticAudio[i] = 0.3f * (float)Math.Sin(2f * Math.PI * f1 * i / sampleRate)
                     + 0.2f * (float)Math.Sin(2f * Math.PI * f2 * i / sampleRate)
                     + 0.15f * (float)Math.Sin(2f * Math.PI * f3 * i / sampleRate);
    }

    var payload = EnglishAcousticDspEngine.Analyze(rhoticAudio, sampleRate, target);
    Assert.IsTrue(payload.english_phonetic_metrics.nucleus_and_rhoticity.rhotic_f3_drop_verified, "F3 drop < 2100Hz must verify rhoticity");
    Assert.AreEqual("RHOTIC", payload.english_phonetic_metrics.nucleus_and_rhoticity.vowel_class);
    Assert.AreEqual(100f, payload.english_phonetic_metrics.nucleus_and_rhoticity.score);
  }

  [Test]
  public void P17x_Coda_FricativeSibilantEnergy_CatchesDroppedCoda() {
    int sampleRate = 16000;
    var target = MakePronunciation(new WordId("six"), new[] { "S", "IH", "K", "S" }, 1);

    // Case A: Audio has high-frequency sibilant tail (>4000Hz, high ZCR)
    float[] goodSix = new float[sampleRate / 2];
    for (int i = 0; i < sampleRate / 4; i++) {
      goodSix[i] = 0.3f * (float)Math.Sin(2f * Math.PI * 400 * i / sampleRate);
    }
    var rnd = new Random(123);
    for (int i = sampleRate / 4; i < sampleRate / 4 + (int)(0.1f * sampleRate); i++) {
      goodSix[i] = (float)((rnd.NextDouble() * 2.0 - 1.0) * 0.2);
    }

    var payloadA = EnglishAcousticDspEngine.Analyze(goodSix, sampleRate, target);
    Assert.IsFalse(payloadA.english_phonetic_metrics.coda_and_clusters.is_dropped, "Realized sibilant coda must not be dropped");

    // Case B: Audio truncates right after vowel (no sibilant coda)
    float[] droppedSix = new float[sampleRate / 4];
    for (int i = 0; i < droppedSix.Length; i++) {
      droppedSix[i] = 0.3f * (float)Math.Sin(2f * Math.PI * 400 * i / sampleRate);
    }
    var payloadB = EnglishAcousticDspEngine.Analyze(droppedSix, sampleRate, target);
    Assert.IsTrue(payloadB.english_phonetic_metrics.coda_and_clusters.is_dropped, "Missing ending must be tagged as dropped coda");
    Assert.LessOrEqual(payloadB.english_phonetic_metrics.coda_and_clusters.score, 35f);
  }

  [Test]
  public void P17x_Prosody_LexicalStress_DetectsFlatRoboticTransfer() {
    int sampleRate = 16000;
    var target = MakePronunciation(new WordId("apple"), new[] { "AE", "P", "AH", "L" }, 2);

    // Case A: Natural Stress (Syllable 1 is loud and longer, Syllable 2 is weak)
    float[] naturalApple = new float[sampleRate * 6 / 10]; // 0.6s
    int mid = naturalApple.Length / 2;
    for (int i = 0; i < mid; i++) naturalApple[i] = 0.5f * (float)Math.Sin(2f * Math.PI * 350 * i / sampleRate);
    for (int i = mid; i < naturalApple.Length; i++) naturalApple[i] = 0.1f * (float)Math.Sin(2f * Math.PI * 300 * i / sampleRate);

    var payloadA = EnglishAcousticDspEngine.Analyze(naturalApple, sampleRate, target);
    Assert.IsFalse(payloadA.english_phonetic_metrics.prosody_and_stress.flat_robotic_stress_error, "Natural stress contrast must pass");

    // Case B: Flat Robotic Syllable-Timed Speech ("Áp - Pồ" equally loud)
    float[] flatApple = new float[sampleRate * 6 / 10];
    for (int i = 0; i < flatApple.Length; i++) flatApple[i] = 0.4f * (float)Math.Sin(2f * Math.PI * 350 * i / sampleRate);

    var payloadB = EnglishAcousticDspEngine.Analyze(flatApple, sampleRate, target);
    Assert.IsTrue(payloadB.english_phonetic_metrics.prosody_and_stress.flat_robotic_stress_error, "Equally loud syllables must trigger flat stress error");
  }

  [Test]
  public void P17x_ExecutionLatency_Under8Milliseconds() {
    int sampleRate = 16000;
    float[] audio = new float[sampleRate * 3 / 2]; // 1.5s audio
    var rnd = new Random(99);
    for (int i = 0; i < audio.Length; i++) audio[i] = (float)(rnd.NextDouble() * 0.1 - 0.05);

    var target = MakePronunciation(new WordId("cat"), new[] { "K", "AE", "T" }, 1);

    // Stabilize GC state before measuring (suite allocates heavily before this test;
    // cold-start runs inflate wall-time)
    System.GC.Collect();
    System.GC.WaitForPendingFinalizers();
    System.GC.Collect();

    // Warm-up JIT
    EnglishAcousticDspEngine.Analyze(audio, sampleRate, target);
    EnglishAcousticDspEngine.Analyze(audio, sampleRate, target);

    // Benchmark 5 runs; track best (min) to absorb OS scheduling jitter — same policy as P13/P15.
    double minMs = double.MaxValue;
    for (int i = 0; i < 5; i++) {
      var sw = Stopwatch.StartNew();
      EnglishAcousticDspEngine.Analyze(audio, sampleRate, target);
      sw.Stop();
      if (sw.Elapsed.TotalMilliseconds < minMs) minMs = sw.Elapsed.TotalMilliseconds;
    }

    Assert.LessOrEqual(minMs, 8.0,
        $"Execution latency must stay within the 8ms design budget (best-of-5; measured {minMs:F2}ms)");
  }

  [Test]
  public void P17x_JsonPayloadSerialization_ValidFormat() {
    int sampleRate = 16000;
    float[] audio = new float[sampleRate / 4];
    var target = MakePronunciation(new WordId("cat"), new[] { "K", "AE", "T" }, 1);
    var payload = EnglishAcousticDspEngine.Analyze(audio, sampleRate, target, "utt-test-123");

    string json = payload.ToJson();
    Assert.IsNotNull(json);
    Assert.IsTrue(json.Contains("\"utterance_id\":\"utt-test-123\""));
    Assert.IsTrue(json.Contains("\"vtln_alpha_factor\""));
    Assert.IsTrue(json.Contains("\"onset_and_clusters\""));
    Assert.IsTrue(json.Contains("\"nucleus_and_rhoticity\""));
    Assert.IsTrue(json.Contains("\"coda_and_clusters\""));
    Assert.IsTrue(json.Contains("\"prosody_and_stress\""));
  }

  [Test]
  public void P17x_NegativeControl_CleanChildSpeech_NoFalseAlarms() {
    // Negative-Control: When a child speaks correctly (aspirated /t/, clear vowel /æ/, clean coda /t/),
    // the engine MUST NOT produce any false alarm error flags.
    int sampleRate = 16000;
    var target = MakePronunciation(new WordId("cat"), new[] { "K", "AE", "T" }, 1);

    float[] cleanAudio = new float[sampleRate * 4 / 10]; // 400ms
    // 1. Initial aspiration burst (45ms)
    int burstSample = (int)(0.02f * sampleRate);
    cleanAudio[burstSample] = 0.7f;
    var rnd = new Random(77);
    for (int i = burstSample + 1; i < burstSample + (int)(0.045f * sampleRate); i++) {
      cleanAudio[i] = (float)(rnd.NextDouble() * 0.12 - 0.06);
    }
    // 2. Open /æ/ vowel nucleus (F1=850Hz, F2=1900Hz for child)
    int vowelStart = burstSample + (int)(0.050f * sampleRate);
    int vowelEnd = vowelStart + (int)(0.150f * sampleRate);
    for (int i = vowelStart; i < vowelEnd; i++) {
      cleanAudio[i] = 0.35f * (float)Math.Sin(2f * Math.PI * 850 * i / sampleRate)
                    + 0.20f * (float)Math.Sin(2f * Math.PI * 1900 * i / sampleRate);
    }
    // 3. Stop closure (40ms) then release burst (10ms)
    int closureStart = vowelEnd;
    int burstCoda = closureStart + (int)(0.040f * sampleRate);
    if (burstCoda < cleanAudio.Length) cleanAudio[burstCoda] = 0.4f;

    var payload = EnglishAcousticDspEngine.Analyze(cleanAudio, sampleRate, target);

    Assert.IsTrue(payload.english_phonetic_metrics.onset_and_clusters.aspiration_passed, "Clean aspiration must not be flagged");
    Assert.IsFalse(payload.english_phonetic_metrics.onset_and_clusters.schwa_epenthesis_detected, "No schwa inserted");
    Assert.GreaterOrEqual(payload.english_phonetic_metrics.nucleus_and_rhoticity.score, 85f, "Open vowel must receive high score");
    Assert.IsFalse(payload.english_phonetic_metrics.coda_and_clusters.is_dropped, "Realized stop coda must not be dropped");
    Assert.GreaterOrEqual(payload.overall_acoustic_score, 80f, "Overall clean production must score >= 80");
  }

  [Test]
  public void P17x_RealChildWavFixture_ZenodoNativeSix_RealizedSibilantCoda() {
    // Real native 4.9-year-old child production of "six" from Zenodo 200495.
    string zenodoPath = Path.Combine(
        "Research", "Speech", "ExternalData", "zenodo_200495", "extracted",
        "english_children", "english_words_sentences", "06_M_native", "studio_mic", "numbers", "six.wav");

    if (!File.Exists(zenodoPath)) {
      Assert.Ignore("Zenodo child fixture file not found on current path: " + zenodoPath);
      return;
    }

    int fileRate;
    float[] samples = LoadWavMono(zenodoPath, out fileRate);
    Assert.IsNotNull(samples, "WAV fixture must load valid PCM samples");
    Assert.Greater(samples.Length, 1000);

    var target = MakePronunciation(new WordId("six"), new[] { "S", "IH", "K", "S" }, 1);
    var payload = EnglishAcousticDspEngine.Analyze(samples, fileRate, target);

    // Native child speaking "six" must realize sibilant coda and not be tagged dropped
    Assert.IsFalse(payload.english_phonetic_metrics.coda_and_clusters.is_dropped, "Real native child production of 'six' must retain coda");
    Assert.Less(payload.dsp_latency_ms, 8.0f, "Real WAV processing must execute within < 8ms");
    Assert.GreaterOrEqual(payload.overall_acoustic_score, 70f, "Real native child must receive a strong passing acoustic score");
  }

  [Test]
  public void P17x_ChildUxSafetyGuard_InternalPenaltiesDoNotAlterChildFacingDecision() {
    // Verify that internal analytic penalties (e.g. 40% VOT deduction) stay purely internal
    // and never degrade the child-facing celebratory decision tiers (FRR-first invariant).
    var policy = SpeakingPassPolicy.Default();
    var recResult = new SpeechRecognitionResult {
      ProviderId = "hybrid-azure-dsp",
      HasSpeech = true,
      MeanEnergy = 0.08f,
      SpeechDurationSec = 0.65f,
      AudioDurationSec = 0.70f,
      RecognitionConfidence = 0.85f,
      Transcript = "cat",
      Pronunciation = new PronunciationEvidence {
        Source = "acoustic-local-dsp",
        HasPhonemeData = true,
        AccuracyScore = 0.82f, // Strong Acoustic Match
        HasPhoneticPayload = true,
        PhoneticPayload = new EnglishPhoneticPayload {
          overall_acoustic_score = 55.0f // Internal penalty applied (e.g. unaspirated stop)
        }
      }
    };

    var assessment = policy.Decide(recResult, new WordId("cat"), "child-att-001");

    // The child-facing verdict must remain Pass or StrongPass based on policy,
    // and must never crash or output harsh reason codes.
    Assert.IsTrue(assessment.Decision == SpeakingDecision.Pass || assessment.Decision == SpeakingDecision.StrongPass,
        "Child-facing decision must remain celebratory and protected from internal technical deductions");
    Assert.IsFalse(assessment.IsEnvironmentError);
  }

  struct RealChildClipTestItem {
    public string Speaker;
    public string Filename;
    public string TargetWord;
    public string[] Phones;
  }

  [Test]
  public void P17x_BatchNegativeControl_RealChildSpeech_12Clips_MeasuresFalseFlagRate() {
    // Systematic Negative-Control: Run 12 real native child clips (4.9-year-olds) from Zenodo 200495.
    // Verifies that correct native child productions have a near-zero false-flag rate (FRR-first).
    var items = new[] {
      new RealChildClipTestItem { Speaker = "06_M_native", Filename = "six.wav", TargetWord = "six", Phones = new[] { "S", "IH", "K", "S" } },
      new RealChildClipTestItem { Speaker = "06_M_native", Filename = "ten.wav", TargetWord = "ten", Phones = new[] { "T", "EH", "N" } },
      new RealChildClipTestItem { Speaker = "06_M_native", Filename = "eight.wav", TargetWord = "eight", Phones = new[] { "EY", "T" } },
      new RealChildClipTestItem { Speaker = "06_M_native", Filename = "two.wav", TargetWord = "two", Phones = new[] { "T", "UW" } },
      new RealChildClipTestItem { Speaker = "06_M_native", Filename = "five.wav", TargetWord = "five", Phones = new[] { "F", "AY", "V" } },
      new RealChildClipTestItem { Speaker = "06_M_native", Filename = "nine.wav", TargetWord = "nine", Phones = new[] { "N", "AY", "N" } },
      new RealChildClipTestItem { Speaker = "07_F_native", Filename = "six.wav", TargetWord = "six", Phones = new[] { "S", "IH", "K", "S" } },
      new RealChildClipTestItem { Speaker = "07_F_native", Filename = "ten.wav", TargetWord = "ten", Phones = new[] { "T", "EH", "N" } },
      new RealChildClipTestItem { Speaker = "07_F_native", Filename = "eight.wav", TargetWord = "eight", Phones = new[] { "EY", "T" } },
      new RealChildClipTestItem { Speaker = "07_F_native", Filename = "two.wav", TargetWord = "two", Phones = new[] { "T", "UW" } },
      new RealChildClipTestItem { Speaker = "11_M_native", Filename = "six.wav", TargetWord = "six", Phones = new[] { "S", "IH", "K", "S" } },
      new RealChildClipTestItem { Speaker = "11_M_native", Filename = "ten.wav", TargetWord = "ten", Phones = new[] { "T", "EH", "N" } },
    };

    int tested = 0;
    int falseFlags = 0;
    string basePath = Path.Combine(
        "Research", "Speech", "ExternalData", "zenodo_200495", "extracted",
        "english_children", "english_words_sentences");
    if (!Directory.Exists(basePath)) {
      Assert.Ignore("zenodo native child clips not present on this machine (dataset-gated test; see Research notes)");
    }

    foreach (var it in items) {
      string fullPath = Path.Combine(basePath, it.Speaker, "studio_mic", "numbers", it.Filename);
      if (!File.Exists(fullPath)) continue;

      int sRate;
      float[] audio = LoadWavMono(fullPath, out sRate);
      if (audio == null || audio.Length < 500) continue;

      var target = MakePronunciation(new WordId(it.TargetWord), it.Phones, 1);
      var payload = EnglishAcousticDspEngine.Analyze(audio, sRate, target);
      tested++;

      // Integrity checks:
      Assert.Less(payload.dsp_latency_ms, 8.0f, $"Clip {it.Speaker}/{it.Filename} latency must be < 8ms");
      Assert.GreaterOrEqual(payload.vtln_alpha_factor, 0.78f);
      Assert.LessOrEqual(payload.vtln_alpha_factor, 0.92f);

      // Check false flags on native child speech:
      // A correct native utterance should NOT trigger epenthesis or flat robotic stress error.
      if (payload.english_phonetic_metrics.onset_and_clusters.schwa_epenthesis_detected) {
        falseFlags++;
      }
      if (payload.english_phonetic_metrics.prosody_and_stress.flat_robotic_stress_error) {
        falseFlags++;
      }
    }

    Assert.GreaterOrEqual(tested, 10, "Must test at least 10 real native child clips");
    float falseFlagRate = (float)falseFlags / tested;
    Assert.LessOrEqual(falseFlagRate, 0.10f, $"False-flag rate on native child speech must be <= 10%, measured {falseFlagRate * 100:F1}%");
  }
}
