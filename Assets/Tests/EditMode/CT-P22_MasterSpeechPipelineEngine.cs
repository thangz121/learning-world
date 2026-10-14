// Assets/Tests/EditMode/CT-P22_MasterSpeechPipelineEngine.cs — Version 6.0 Production Apex Pipeline Tests.
// Unit and End-to-End stress tests verifying Stages 1-5, SLA Latency, Circuit Breaker, 3 Honesty Rules & JSON output.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;

[TestFixture]
public class CT_P22_MasterSpeechPipelineEngine {

  private float[] GenerateSineWaveAudio(float freqHz, float durationSec, int sampleRate = 16000, float amplitude = 0.2f) {
    int count = (int)(durationSec * sampleRate);
    float[] audio = new float[count];
    for (int i = 0; i < count; i++) {
      audio[i] = (float)(Math.Sin(2.0 * Math.PI * freqHz * i / sampleRate) * amplitude);
    }
    return audio;
  }

  [SetUp]
  public void SetUp() {
    CloudSpeechResilienceEngine.ResetCircuitBreaker();
  }

  private ChildFrontendMetadata MakeFrontend(float snr = 20f, bool adultBlocked = false) {
    return new ChildFrontendMetadata {
      SnrDb = snr,
      PromptingDetected = adultBlocked,
      VadState = adultBlocked ? ChildAudioFrontend.VadStateDroppedAdult : ChildAudioFrontend.VadStateSpeech,
      FineGrainedVadState = adultBlocked ? "BLOCKED_ADULT" : "STRESSED_NUCLEUS",
      IsChild = !adultBlocked
    };
  }

  private EnglishPhoneticPayload MakeDspPayload(
      float overallScore = 85f,
      bool codaDropped = false,
      bool aspirationPassed = true,
      bool epenthesis = false,
      string codaType = "STOP") {

    return new EnglishPhoneticPayload {
      utterance_id = "utt-test-p22",
      overall_acoustic_score = overallScore,
      english_phonetic_metrics = new EnglishPhoneticMetrics {
        onset_and_clusters = new OnsetAndClustersMetrics {
          target_onset_ipa = "k",
          aspiration_passed = aspirationPassed,
          schwa_epenthesis_detected = epenthesis,
          score = aspirationPassed && !epenthesis ? 90f : 50f
        },
        nucleus_and_rhoticity = new NucleusAndRhoticityMetrics {
          target_vowel_ipa = "æ",
          vowel_class = "LAX",
          score = 88f
        },
        coda_and_clusters = new CodaAndClustersMetrics {
          target_coda_ipa = "t",
          coda_type = codaType,
          release_burst_detected = !codaDropped,
          high_freq_fricative_energy_ok = !codaDropped,
          is_dropped = codaDropped,
          score = codaDropped ? 15f : 90f
        },
        prosody_and_stress = new ProsodyAndStressMetrics {
          syllable_count = 1,
          flat_robotic_stress_error = false,
          score = 90f
        }
      }
    };
  }

  [Test]
  public void P22_Stage1_AecBargeIn_CancelsSpeakerEchoWhenChildSpeaksOverPrompt() {
    var sessionEngine = new MultiTurnAdaptiveSessionEngine("session-p22-aec");

    // 100ms loudspeaker audio
    float[] ttsAudio = GenerateSineWaveAudio(1000f, 0.100f, 16000, 0.3f);
    long ttsTimestamp = 1000;
    sessionEngine.PushTtsPlaybackChunk(ttsAudio, ttsTimestamp);

    // Micro audio with barge-in match
    float[] micAudio = GenerateSineWaveAudio(1000f, 0.200f, 16000, 0.25f);
    var echoResult = sessionEngine.ProcessBargeInAndEcho(micAudio, currentTimestampMs: 1050, vadSpeechTriggered: true);

    Assert.IsTrue(echoResult.barge_in_detected, "Barge-in echo must be detected on high correlation");
    Assert.Greater(echoResult.tts_echo_cancelled_ncc, 0.65f);
  }

  [Test]
  public void P22_Stage3_TimeoutHardCeiling_Enforces460MsCapAndFallsBackToLocal() {
    float[] audio = GenerateSineWaveAudio(320f, 0.400f, 16000);
    var targetPron = PhonemeTable.FromIds(new WordId("cat"), new[] { "K", "AE", "T" });

    // Simulate Azure cloud taking 480ms (exceeding SLA)
    var payload = MasterEnglishSpeechPipelineEngine.Evaluate(
        pcmNaturalSamples: audio,
        sampleRate: 16000,
        targetWord: "cat",
        targetPron: targetPron,
        attemptIndex: 1,
        prevAttemptScore: 0f,
        prevAttemptPrimaryError: "NONE",
        rawAzure: null,
        externalLatencyEstimateMs: 480f);

    Assert.AreEqual("TIMEOUT_LOCAL_FALLBACK", payload.telemetry_and_sla.cloud_status);
    Assert.LessOrEqual(payload.telemetry_and_sla.post_eos_total_latency_ms, 460.0f, "Hard ceiling latency must clamp to <= 460ms");
  }

  [Test]
  public void P22_Stage4_Rule2_OutOfVocabularyNoise_LocksScoresToZero() {
    float[] audio = GenerateSineWaveAudio(320f, 0.350f, 16000);
    var targetPron = PhonemeTable.FromIds(new WordId("cat"), new[] { "K", "AE", "T" });
    var dspOverride = MakeDspPayload(overallScore: 92f);

    // Azure heard completely unrelated word "dog" -> OOV Noise
    var rawAzure = new RawAzureAssessmentResult {
      RecognitionStatus = "Success",
      DisplayText = "dog",
      OverallAccuracyScore = 15f,
      Words = new List<RawAzureWordData> {
        new RawAzureWordData {
          Word = "dog",
          AccuracyScore = 15f,
          Phonemes = new List<RawAzurePhonemeData> {
            new RawAzurePhonemeData { Phoneme = "d", AccuracyScore = 10f },
            new RawAzurePhonemeData { Phoneme = "ɔː", AccuracyScore = 15f },
            new RawAzurePhonemeData { Phoneme = "ɡ", AccuracyScore = 10f }
          }
        }
      }
    };

    var payload = MasterEnglishSpeechPipelineEngine.Evaluate(
        pcmNaturalSamples: audio,
        sampleRate: 16000,
        targetWord: "cat",
        targetPron: targetPron,
        attemptIndex: 1,
        prevAttemptScore: 0f,
        prevAttemptPrimaryError: "NONE",
        rawAzure: rawAzure,
        externalLatencyEstimateMs: 80f,
        dspOverride: dspOverride);

    Assert.AreEqual("RULE_2_NOISE_LEXICAL_BLOCK", payload.honesty_and_validation_layer.active_rule_triggered);
    Assert.AreEqual("Retry_Blocked", payload.honesty_and_validation_layer.raw_normative_decision);
    Assert.AreEqual(0f, payload.honesty_and_validation_layer.final_fusion_score);
    Assert.AreEqual(0, payload.honesty_and_validation_layer.star_reward);
  }

  [Test]
  public void P22_Stage4_Rule3_CodaVetoGate_CapsDroppedCodaAtPartial() {
    float[] audio = GenerateSineWaveAudio(320f, 0.350f, 16000);
    var targetPron = PhonemeTable.FromIds(new WordId("cat"), new[] { "K", "AE", "T" });
    var dspOverride = MakeDspPayload(overallScore: 75f, codaDropped: true);
    var frontend = MakeFrontend(snr: 20f);

    // Kid omitted coda /t/ ("ca" instead of "cat"), Azure gave high onset/vowel score
    var rawAzure = new RawAzureAssessmentResult {
      RecognitionStatus = "Success",
      DisplayText = "cat",
      OverallAccuracyScore = 86f,
      CompletenessScore = 100f,
      FluencyScore = 90f,
      Words = new List<RawAzureWordData> {
        new RawAzureWordData {
          Word = "cat",
          AccuracyScore = 86f,
          Phonemes = new List<RawAzurePhonemeData> {
            new RawAzurePhonemeData { Phoneme = "k", AccuracyScore = 95f },
            new RawAzurePhonemeData { Phoneme = "æ", AccuracyScore = 92f },
            new RawAzurePhonemeData { Phoneme = "t", AccuracyScore = 15f } // Dropped coda!
          }
        }
      }
    };

    var payload = MasterEnglishSpeechPipelineEngine.Evaluate(
        pcmNaturalSamples: audio,
        sampleRate: 16000,
        targetWord: "cat",
        targetPron: targetPron,
        attemptIndex: 1,
        prevAttemptScore: 0f,
        prevAttemptPrimaryError: "NONE",
        rawAzure: rawAzure,
        externalLatencyEstimateMs: 90f,
        dspOverride: dspOverride,
        frontendOverride: frontend);

    Assert.IsTrue(payload.honesty_and_validation_layer.coda_veto_gate_locked, "Coda veto gate must lock when coda is dropped");
    Assert.LessOrEqual(payload.honesty_and_validation_layer.final_fusion_score, 68.0f, "Dropped coda must be capped at 68.0");
    Assert.AreEqual("Partial", payload.honesty_and_validation_layer.raw_normative_decision);
  }

  [Test]
  public void P22_Stage5_Attempt2_RelativeImprovement_GrantsAdaptiveImprovementPass() {
    float[] audio = GenerateSineWaveAudio(320f, 0.350f, 16000);
    var targetPron = PhonemeTable.FromIds(new WordId("cat"), new[] { "K", "AE", "T" });
    // Overall acoustic score 60 and codaDropped=false, but sub-scores balanced so final fusion score is ~61 (in [58, 68))
    var dspOverride = new EnglishPhoneticPayload {
      utterance_id = "utt-test-p22",
      overall_acoustic_score = 60f,
      english_phonetic_metrics = new EnglishPhoneticMetrics {
        onset_and_clusters = new OnsetAndClustersMetrics { target_onset_ipa = "k", aspiration_passed = true, score = 65f },
        nucleus_and_rhoticity = new NucleusAndRhoticityMetrics { target_vowel_ipa = "æ", vowel_class = "LAX", score = 65f },
        coda_and_clusters = new CodaAndClustersMetrics { target_coda_ipa = "t", coda_type = "STOP", release_burst_detected = true, high_freq_fricative_energy_ok = true, is_dropped = false, score = 55f },
        prosody_and_stress = new ProsodyAndStressMetrics { syllable_count = 1, score = 65f }
      }
    };
    var frontend = MakeFrontend(snr: 20f);

    // Attempt 1 had score 40 with DROPPED_CODA
    // Attempt 2 now achieves score ~61 (+21 improvement!), raw fusion in Partial window [58, 68)
    var rawAzure = new RawAzureAssessmentResult {
      RecognitionStatus = "Success",
      DisplayText = "cat",
      OverallAccuracyScore = 61f,
      CompletenessScore = 100f,
      FluencyScore = 85f,
      Words = new List<RawAzureWordData> {
        new RawAzureWordData {
          Word = "cat",
          AccuracyScore = 61f,
          Phonemes = new List<RawAzurePhonemeData> {
            new RawAzurePhonemeData { Phoneme = "k", AccuracyScore = 65f },
            new RawAzurePhonemeData { Phoneme = "æ", AccuracyScore = 62f },
            new RawAzurePhonemeData { Phoneme = "t", AccuracyScore = 56f }
          }
        }
      }
    };

    var payload = MasterEnglishSpeechPipelineEngine.Evaluate(
        pcmNaturalSamples: audio,
        sampleRate: 16000,
        targetWord: "cat",
        targetPron: targetPron,
        attemptIndex: 2,
        prevAttemptScore: 40f,
        prevAttemptPrimaryError: "DROPPED_CODA",
        rawAzure: rawAzure,
        externalLatencyEstimateMs: 90f,
        dspOverride: dspOverride,
        frontendOverride: frontend);

    Assert.AreEqual("Adaptive_Improvement_Pass", payload.honesty_and_validation_layer.final_session_decision);
    Assert.AreEqual(2, payload.honesty_and_validation_layer.star_reward);
  }

  [Test]
  public void P22_Stage5_Attempt3_EffortMercyPass_QueuesSpacedRepetition() {
    float[] audio = GenerateSineWaveAudio(320f, 0.350f, 16000);
    var targetPron = PhonemeTable.FromIds(new WordId("cat"), new[] { "K", "AE", "T" });
    var sessionEngine = new MultiTurnAdaptiveSessionEngine("session-p22-mercy");
    // Overall acoustic score 48 and sub-scores ~48 so final score stays in Partial window [35, 68)
    var dspOverride = new EnglishPhoneticPayload {
      utterance_id = "utt-test-p22",
      overall_acoustic_score = 48f,
      english_phonetic_metrics = new EnglishPhoneticMetrics {
        onset_and_clusters = new OnsetAndClustersMetrics { target_onset_ipa = "k", aspiration_passed = true, score = 50f },
        nucleus_and_rhoticity = new NucleusAndRhoticityMetrics { target_vowel_ipa = "æ", vowel_class = "LAX", score = 50f },
        coda_and_clusters = new CodaAndClustersMetrics { target_coda_ipa = "t", coda_type = "STOP", release_burst_detected = true, high_freq_fricative_energy_ok = true, is_dropped = false, score = 45f },
        prosody_and_stress = new ProsodyAndStressMetrics { syllable_count = 1, score = 50f }
      }
    };
    var frontend = MakeFrontend(snr: 20f);

    // Attempt 3 still Partial (e.g. 48 points), genuine effort
    var rawAzure = new RawAzureAssessmentResult {
      RecognitionStatus = "Success",
      DisplayText = "cat",
      OverallAccuracyScore = 48f,
      Words = new List<RawAzureWordData> {
        new RawAzureWordData {
          Word = "cat",
          AccuracyScore = 48f,
          Phonemes = new List<RawAzurePhonemeData> {
            new RawAzurePhonemeData { Phoneme = "k", AccuracyScore = 50f },
            new RawAzurePhonemeData { Phoneme = "æ", AccuracyScore = 48f },
            new RawAzurePhonemeData { Phoneme = "t", AccuracyScore = 46f }
          }
        }
      }
    };

    var payload = MasterEnglishSpeechPipelineEngine.Evaluate(
        pcmNaturalSamples: audio,
        sampleRate: 16000,
        targetWord: "cat",
        targetPron: targetPron,
        attemptIndex: 3,
        prevAttemptScore: 48f,
        prevAttemptPrimaryError: "DROPPED_CODA",
        sessionEngine: sessionEngine,
        rawAzure: rawAzure,
        externalLatencyEstimateMs: 80f,
        dspOverride: dspOverride,
        frontendOverride: frontend);

    Assert.AreEqual("Effort_Mercy_Pass", payload.honesty_and_validation_layer.final_session_decision);
    Assert.IsTrue(payload.pedagogical_coach_action_4yo.enqueue_spaced_repetition);
    Assert.Contains("cat", (System.Collections.ICollection)sessionEngine.SpacedRepetitionQueue);
  }

  [Test]
  public void P22_EndToEndSlaLatency_CompletesUnder300Ms() {
    float[] audio = GenerateSineWaveAudio(320f, 0.400f, 16000);
    var targetPron = PhonemeTable.FromIds(new WordId("star"), new[] { "S", "T", "AA", "R" });

    // Warm-up
    MasterEnglishSpeechPipelineEngine.Evaluate(audio, 16000, "star", targetPron, 1, 0f, "NONE");

    int runs = 50;
    var sw = Stopwatch.StartNew();
    for (int i = 0; i < runs; i++) {
      MasterEnglishSpeechPipelineEngine.Evaluate(audio, 16000, "star", targetPron, 1, 0f, "NONE", externalLatencyEstimateMs: 100f);
    }
    sw.Stop();

    double avgMs = sw.Elapsed.TotalMilliseconds / runs;
    Assert.Less(avgMs, 300.0, "Total pipeline latency per evaluation must stay well under 300ms SLA");
  }

  [Test]
  public void P22_MasterPayloadJson_MatchesUnifiedSchema() {
    float[] audio = GenerateSineWaveAudio(320f, 0.350f, 16000);
    var targetPron = PhonemeTable.FromIds(new WordId("star"), new[] { "S", "T", "AA", "R" });

    var payload = MasterEnglishSpeechPipelineEngine.Evaluate(
        pcmNaturalSamples: audio,
        sampleRate: 16000,
        targetWord: "star",
        targetPron: targetPron,
        attemptIndex: 1,
        prevAttemptScore: 0f,
        prevAttemptPrimaryError: "NONE");

    string json = payload.ToJson();

    Assert.IsTrue(json.Contains("\"session_id\":"));
    Assert.IsTrue(json.Contains("\"telemetry_and_sla\":"));
    Assert.IsTrue(json.Contains("\"post_eos_total_latency_ms\":"));
    Assert.IsTrue(json.Contains("\"latency_included_external_estimate\": false"),
        "Default Evaluate must NOT include an external estimate (honest measured-only telemetry)");
    Assert.IsTrue(json.Contains("\"honesty_and_validation_layer\":"));
    Assert.IsTrue(json.Contains("\"syllable_by_syllable_ui_breakdown\":"));
    Assert.IsTrue(json.Contains("\"pedagogical_coach_action_4yo\":"));
  }
}
