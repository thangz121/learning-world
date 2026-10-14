// Assets/Tests/EditMode/CT-P18_CloudSpeechResilienceEngine.cs — Phase 3.1 Cloud Speech & Resilience Tests.
// Unit & stress tests for CloudSpeechResilienceEngine (English EFL Max-Spec).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;

[TestFixture]
public class CT_P18_CloudSpeechResilienceEngine {

  [SetUp]
  public void SetUp() {
    CloudSpeechResilienceEngine.ResetCircuitBreaker();
  }

  [TearDown]
  public void TearDown() {
    CloudSpeechResilienceEngine.ResetCircuitBreaker();
  }

  [Test]
  public void P18_ComfortNoise_AmplitudeMatchesMinus65DbFS() {
    float[] noise = CloudSpeechResilienceEngine.GenerateComfortNoise(0.150f, 16000, 42);
    Assert.AreEqual(2400, noise.Length, "150ms @ 16kHz should be exactly 2400 samples");

    float maxAbs = 0f;
    for (int i = 0; i < noise.Length; i++) {
      float a = Math.Abs(noise[i]);
      if (a > maxAbs) maxAbs = a;
    }

    // -65 dBFS amplitude = 10^(-65/20) ~ 0.0005623
    Assert.LessOrEqual(maxAbs, 0.0006f, "Noise amplitude must stay below -65dBFS threshold");
    Assert.Greater(maxAbs, 0.0001f, "Noise must have non-zero energy");
  }

  [Test]
  public void P18_StreamingPayload_PrependsNoiseAndPreRollAndTrailingFlush() {
    int sampleRate = 16000;
    float[] pcm = new float[1600]; // 100ms
    float[] preRoll = new float[1760]; // 110ms

    float[] payload = CloudSpeechResilienceEngine.BuildStreamingPayloadWithComfortNoise(pcm, preRoll, sampleRate);
    // 150ms noise (2400) + 110ms pre-roll (1760) + 100ms pcm (1600) + 200ms flush (3200) = 8960
    Assert.AreEqual(8960, payload.Length, "Assembled payload must include leading noise, pre-roll, speech, and trailing flush");
  }

  [Test]
  public void P18_HomophoneImmunity_SunAndSonRecognizedAsPhoneticMatch() {
    var raw = new RawAzureAssessmentResult {
      RecognitionStatus = "Success",
      DisplayText = "Son",
      OverallAccuracyScore = 95f,
      CompletenessScore = 100f,
      FluencyScore = 90f,
      IntraWordPauseMs = 0f,
      Words = new List<RawAzureWordData> {
        new RawAzureWordData {
          Word = "Son",
          AccuracyScore = 95f,
          Phonemes = new List<RawAzurePhonemeData> {
            new RawAzurePhonemeData { Phoneme = "s", AccuracyScore = 95f },
            new RawAzurePhonemeData { Phoneme = "ʌ", AccuracyScore = 95f },
            new RawAzurePhonemeData { Phoneme = "n", AccuracyScore = 95f }
          }
        }
      }
    };

    float[] audio = new float[16000];
    var res = CloudSpeechResilienceEngine.Process(audio, 16000, "Sun", raw, "utt-homo-1");

    Assert.IsTrue(res.lexical_guard_metrics.homophone_normalized_match, "Sun and Son must be recognized as homophone match");
    Assert.IsFalse(res.lexical_guard_metrics.is_out_of_vocabulary_noise, "Homophone must never be flagged as OOV noise");
    Assert.AreEqual("AZURE_SUCCESS", res.cloud_status);
  }

  [Test]
  public void P18_AntiHallucination_ForceAlignmentOnScreamingOrNoiseOverridden() {
    // Child screamed or said 'dog', but Azure hallucinated high score for 'apple'
    var raw = new RawAzureAssessmentResult {
      RecognitionStatus = "Success",
      DisplayText = "dog",
      OverallAccuracyScore = 88f, // Fake high score from Azure force alignment
      CompletenessScore = 100f,
      Words = new List<RawAzureWordData> {
        new RawAzureWordData {
          Word = "dog",
          AccuracyScore = 88f,
          Phonemes = new List<RawAzurePhonemeData> {
            new RawAzurePhonemeData { Phoneme = "d", AccuracyScore = 88f },
            new RawAzurePhonemeData { Phoneme = "ɔː", AccuracyScore = 88f },
            new RawAzurePhonemeData { Phoneme = "ɡ", AccuracyScore = 88f }
          }
        }
      }
    };

    float[] audio = new float[16000];
    var res = CloudSpeechResilienceEngine.Process(audio, 16000, "apple", raw, "utt-hallu-1");

    Assert.IsTrue(res.lexical_guard_metrics.force_alignment_hallucination_overridden, "Force-alignment hallucination must be overridden");
    Assert.AreEqual("AZURE_HALLUCINATION_CAUGHT", res.cloud_status);
    Assert.AreEqual(0f, res.azure_calibrated_scores.overall_accuracy_dialect_adjusted, "Calibrated score must be clamped to 0 for hallucinated noise");
  }

  [Test]
  public void P18_OutOfVocabularyNoise_FlagsZeroLexicalScore() {
    var raw = new RawAzureAssessmentResult {
      RecognitionStatus = "Success",
      DisplayText = "random_noise",
      OverallAccuracyScore = 10f,
      Words = new List<RawAzureWordData> {
        new RawAzureWordData {
          Word = "noise",
          AccuracyScore = 10f,
          Phonemes = new List<RawAzurePhonemeData> {
            new RawAzurePhonemeData { Phoneme = "z", AccuracyScore = 10f }
          }
        }
      }
    };

    float[] audio = new float[16000];
    var res = CloudSpeechResilienceEngine.Process(audio, 16000, "cat", raw, "utt-oov-1");

    Assert.IsTrue(res.lexical_guard_metrics.is_out_of_vocabulary_noise, "Unrelated acoustic noise must be flagged as OOV");
  }

  [Test]
  public void P18_L1AndDevelopmentalSubstitution_PreservesNonZeroScoreWithFlag() {
    // Child said "free" or "tree" /friː/ for target "three" /θriː/ (developmental th-fronting)
    var raw = new RawAzureAssessmentResult {
      RecognitionStatus = "Success",
      DisplayText = "three",
      OverallAccuracyScore = 65f,
      Words = new List<RawAzureWordData> {
        new RawAzureWordData {
          Word = "three",
          AccuracyScore = 65f,
          Phonemes = new List<RawAzurePhonemeData> {
            new RawAzurePhonemeData {
              Phoneme = "θ",
              AccuracyScore = 40f,
              NBest = new List<NBestIpaEntry> { new NBestIpaEntry("f", 75f) }
            },
            new RawAzurePhonemeData {
              Phoneme = "r",
              AccuracyScore = 90f,
              NBest = new List<NBestIpaEntry> { new NBestIpaEntry("r", 90f) }
            },
            new RawAzurePhonemeData {
              Phoneme = "iː",
              AccuracyScore = 95f,
              NBest = new List<NBestIpaEntry> { new NBestIpaEntry("iː", 95f) }
            }
          }
        }
      }
    };

    float[] audio = new float[16000];
    var res = CloudSpeechResilienceEngine.Process(audio, 16000, "three", raw, "utt-dev-1");

    Assert.IsTrue(res.lexical_guard_metrics.l1_or_developmental_substitution, "Th-fronting must be recognized as developmental substitution");
    Assert.IsFalse(res.lexical_guard_metrics.is_out_of_vocabulary_noise, "Developmental substitution must NOT be flagged as noise");
    Assert.Greater(res.azure_calibrated_scores.overall_accuracy_dialect_adjusted, 70f, "Score should be calibrated favorably for kid substitution");
  }

  [Test]
  public void P18_DialectAllophoneTolerance_RemapsBritishTAndBathVowel() {
    // Target is "water" on en-US, child pronounces crisp British /t/ instead of flap-T
    var raw = new RawAzureAssessmentResult {
      RecognitionStatus = "Success",
      DisplayText = "water",
      OverallAccuracyScore = 70f,
      Words = new List<RawAzureWordData> {
        new RawAzureWordData {
          Word = "water",
          AccuracyScore = 70f,
          Phonemes = new List<RawAzurePhonemeData> {
            new RawAzurePhonemeData { Phoneme = "w", AccuracyScore = 95f },
            new RawAzurePhonemeData { Phoneme = "ɔː", AccuracyScore = 90f },
            new RawAzurePhonemeData {
              Phoneme = "t",
              AccuracyScore = 55f, // Azure penalized British crisp T on en-US
              NBest = new List<NBestIpaEntry> { new NBestIpaEntry("t", 88f) }
            },
            new RawAzurePhonemeData { Phoneme = "ər", AccuracyScore = 85f }
          }
        }
      }
    };

    float[] audio = new float[16000];
    var res = CloudSpeechResilienceEngine.Process(audio, 16000, "water", raw, "utt-dialect-1");

    var tItem = res.ipa_phoneme_breakdown.Find(p => p.target_ipa.Contains("t"));
    Assert.AreEqual("ACCENT_VARIANT_OK", tItem.error_tag, "Crisp British T must be accepted as ACCENT_VARIANT_OK");
    Assert.GreaterOrEqual(tItem.calibrated_score, 88f, "Allophone score must be restored to >= 88");
  }

  [Test]
  public void P18_HeuristicPlaceholder_ExecutesUnder12Milliseconds() {
    float[] audio = new float[16000]; // 1.0s audio
    var rnd = new Random(42);
    for (int i = 0; i < audio.Length; i++) audio[i] = (float)(rnd.NextDouble() * 0.1 - 0.05);

    // Warm-up
    CloudSpeechResilienceEngine.EstimateLocalFallbackPhonemes_HeuristicPlaceholder(audio, 16000, "cat");

    var sw = Stopwatch.StartNew();
    string ipa = CloudSpeechResilienceEngine.EstimateLocalFallbackPhonemes_HeuristicPlaceholder(audio, 16000, "cat");
    sw.Stop();

    Assert.Less(sw.Elapsed.TotalMilliseconds, 12.0, "Heuristic energy placeholder must execute in < 12ms");
    // Honest contract: returns empty string (no local ASR model is faked)
    Assert.AreEqual(string.Empty, ipa, "Heuristic placeholder must return empty string, never a fake transcript");
  }

  [Test]
  public void P18_HardDeadlineTimeout_FallsBackToLocalAt450Ms() {
    float[] audio = new float[16000];
    var res = CloudSpeechResilienceEngine.Process(audio, 16000, "cat", null, "utt-timeout-1", postEosLatencyMs: 460f);

    Assert.AreEqual("TIMEOUT_LOCAL_FALLBACK", res.cloud_status, "Post-EOS latency >= 450ms must trigger the local fallback");
    Assert.AreEqual(450f, res.post_eos_latency_ms);
    Assert.AreEqual(string.Empty, res.lexical_guard_metrics.actual_heard_top1_ipa_sequence);
    Assert.IsTrue(float.IsNaN(res.azure_calibrated_scores.overall_accuracy_raw));
  }

  [Test]
  public void P18_CircuitBreaker_TripsAfterThreeConsecutiveTimeouts() {
    float[] audio = new float[16000];
    CloudSpeechResilienceEngine.ResetCircuitBreaker();

    // 1st timeout
    CloudSpeechResilienceEngine.Process(audio, 16000, "cat", null, "utt-cb-1", simulateTimeout: true);
    Assert.IsFalse(CloudSpeechResilienceEngine.IsCircuitBreakerTripped());

    // 2nd timeout
    CloudSpeechResilienceEngine.Process(audio, 16000, "cat", null, "utt-cb-2", simulateTimeout: true);
    Assert.IsFalse(CloudSpeechResilienceEngine.IsCircuitBreakerTripped());

    // 3rd timeout -> should trip
    var res3 = CloudSpeechResilienceEngine.Process(audio, 16000, "cat", null, "utt-cb-3", simulateTimeout: true);
    Assert.IsTrue(CloudSpeechResilienceEngine.IsCircuitBreakerTripped(), "Circuit breaker must trip after 3 consecutive timeouts");

    // 4th request while tripped -> immediately OFFLINE_AUTONOMOUS
    var res4 = CloudSpeechResilienceEngine.Process(audio, 16000, "cat", null, "utt-cb-4");
    Assert.AreEqual("OFFLINE_AUTONOMOUS", res4.cloud_status, "Subsequent calls while breaker is open must return OFFLINE_AUTONOMOUS");
    Assert.AreEqual(12f, res4.post_eos_latency_ms, "Autonomous fallback must report immediate ~12ms local latency");
  }

  [Test]
  public void P18_JsonPayloadSerialization_MatchesRequiredSchema() {
    float[] audio = new float[16000];
    var raw = new RawAzureAssessmentResult {
      RecognitionStatus = "Success",
      DisplayText = "cat",
      OverallAccuracyScore = 92f,
      CompletenessScore = 100f,
      FluencyScore = 85f,
      IntraWordPauseMs = 0f,
      Words = new List<RawAzureWordData> {
        new RawAzureWordData {
          Word = "cat",
          AccuracyScore = 92f,
          ErrorType = "None",
          Phonemes = new List<RawAzurePhonemeData> {
            new RawAzurePhonemeData { Phoneme = "k", AccuracyScore = 95f },
            new RawAzurePhonemeData { Phoneme = "æ", AccuracyScore = 90f },
            new RawAzurePhonemeData { Phoneme = "t", AccuracyScore = 91f }
          }
        }
      }
    };

    var res = CloudSpeechResilienceEngine.Process(audio, 16000, "cat", raw, "00000000-0000-0000-0000-000000000001", postEosLatencyMs: 140f);
    string json = res.ToJson();

    Assert.IsTrue(json.Contains("\"utterance_id\": \"00000000-0000-0000-0000-000000000001\""));
    Assert.IsTrue(json.Contains("\"cloud_status\": \"AZURE_SUCCESS\""));
    Assert.IsTrue(json.Contains("\"lexical_guard_metrics\""));
    Assert.IsTrue(json.Contains("\"homophone_normalized_match\":"));
    Assert.IsTrue(json.Contains("\"azure_calibrated_scores\""));
    Assert.IsTrue(json.Contains("\"overall_accuracy_dialect_adjusted\""));
    Assert.IsTrue(json.Contains("\"ipa_phoneme_breakdown\""));
    Assert.IsTrue(json.Contains("\"position\": \"ONSET\""));
    Assert.IsTrue(json.Contains("\"position\": \"NUCLEUS\""));
    Assert.IsTrue(json.Contains("\"position\": \"CODA\""));
  }
}
