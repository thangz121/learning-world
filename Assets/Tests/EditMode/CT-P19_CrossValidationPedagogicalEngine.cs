// Assets/Tests/EditMode/CT-P19_CrossValidationPedagogicalEngine.cs — Phase 4.1 Cross-Validation Tests.
// Tests the 3 Honesty Rules, ASHA Age-4 Norms, and Single-Focus Coach.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;

[TestFixture]
public class CT_P19_CrossValidationPedagogicalEngine {

  private ChildFrontendMetadata MakePhase1(float snr = 20f, bool adultBlocked = false) {
    var meta = new ChildFrontendMetadata {
      SnrDb = snr,
      PromptingDetected = adultBlocked,
      VadState = adultBlocked ? ChildAudioFrontend.VadStateDroppedAdult : ChildAudioFrontend.VadStateSpeech,
      FineGrainedVadState = adultBlocked ? "BLOCKED_ADULT" : "STRESSED_NUCLEUS"
    };
    return meta;
  }

  private EnglishPhoneticPayload MakePhase2(
      float overallScore = 85f,
      bool codaDropped = false,
      bool aspirationPassed = true,
      bool epenthesis = false,
      string codaType = "STOP") {

    return new EnglishPhoneticPayload {
      utterance_id = "utt-test-p2",
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
          score = codaDropped ? 20f : 90f
        },
        prosody_and_stress = new ProsodyAndStressMetrics {
          syllable_count = 1,
          flat_robotic_stress_error = false,
          score = 90f
        }
      }
    };
  }

  private CloudSpeechResiliencePayload MakePhase3(
      float cloudScore = 90f,
      bool isOov = false,
      bool isHallucination = false,
      string cloudStatus = "AZURE_SUCCESS",
      float codaPhonemeScore = 90f) {

    return new CloudSpeechResiliencePayload {
      utterance_id = "utt-test-p3",
      cloud_status = cloudStatus,
      post_eos_latency_ms = 120f,
      lexical_guard_metrics = new LexicalGuardMetrics {
        target_word = "cat",
        target_ipa = "kæt",
        actual_heard_top1_ipa_sequence = isOov ? "dɔːɡ" : "kæt",
        phonetic_similarity_ratio = isOov ? 0.1f : 1.0f,
        homophone_normalized_match = false,
        is_out_of_vocabulary_noise = isOov,
        l1_or_developmental_substitution = false,
        force_alignment_hallucination_overridden = isHallucination
      },
      azure_calibrated_scores = new AzureCalibratedScores {
        overall_accuracy_raw = cloudScore,
        overall_accuracy_dialect_adjusted = cloudScore,
        completeness_score = 100f,
        intra_word_pause_ms = 0f,
        miscue_flag = "NONE"
      },
      ipa_phoneme_breakdown = new List<IpaPhonemeBreakdownItem> {
        new IpaPhonemeBreakdownItem { position = "ONSET", target_ipa = "k", calibrated_score = 90f },
        new IpaPhonemeBreakdownItem { position = "NUCLEUS", target_ipa = "æ", calibrated_score = 90f },
        new IpaPhonemeBreakdownItem { position = "CODA", target_ipa = "t", calibrated_score = codaPhonemeScore }
      }
    };
  }

  [Test]
  public void P19_Rule2_AcousticNoiseGuard_BlocksFakeScoresOnOutOfVocabulary() {
    var p1 = MakePhase1();
    var p2 = MakePhase2(overallScore: 95f); // Local DSP got high acoustic energy from noise
    var p3 = MakePhase3(cloudScore: 10f, isOov: true); // Cloud confirmed OOV

    var payload = CrossValidationPedagogicalEngine.Fuse("cat", p1, p2, p3);

    Assert.AreEqual("RULE_2_NOISE_LEXICAL_BLOCK", payload.active_honesty_rule_triggered);
    Assert.AreEqual("Retry_Blocked", payload.normative_decision);
    Assert.AreEqual(0, payload.star_reward);
    Assert.AreEqual(0f, payload.scores_summary.final_fusion_score);
  }

  [Test]
  public void P19_Rule2_AdultPromptingGuard_BlocksScoresWhenAdultSpeaks() {
    var p1 = MakePhase1(adultBlocked: true); // Adult detected by Phase 1.2
    var p2 = MakePhase2(overallScore: 92f);
    var p3 = MakePhase3(cloudScore: 90f);

    var payload = CrossValidationPedagogicalEngine.Fuse("cat", p1, p2, p3);

    Assert.AreEqual("RULE_2_NOISE_LEXICAL_BLOCK", payload.active_honesty_rule_triggered);
    Assert.AreEqual("Retry_Blocked", payload.normative_decision);
    Assert.AreEqual("ADULT_INTERFERENCE", payload.pedagogical_feedback_for_4yo.primary_error_focus);
    Assert.IsTrue(payload.pedagogical_feedback_for_4yo.voice_coach_prompt_vi.Contains("tự mình"));
  }

  [Test]
  public void P19_Rule1_HallucinationRescue_ProtectsKidEffortInNoisyRoom() {
    var p1 = MakePhase1(snr: 10f); // Low SNR (noise in room)
    var p2 = MakePhase2(overallScore: 88f); // Local DSP confident
    var p3 = MakePhase3(cloudScore: 30f); // Cloud penalized due to noise

    var payload = CrossValidationPedagogicalEngine.Fuse("cat", p1, p2, p3, localCtcPhoneticSimilarity: 0.85f);

    Assert.AreEqual("RULE_1_HALLUCINATION_RESCUE", payload.active_honesty_rule_triggered);
    // Score should be dominated by Local DSP (85% * 88 + 15% * 30 = 74.8 + 4.5 = 79.3)
    Assert.Greater(payload.scores_summary.final_fusion_score, 70f);
    Assert.AreNotEqual("Retry_Blocked", payload.normative_decision);
  }

  [Test]
  public void P19_Rule3_CodaVetoGate_CapsDroppedCodaAtPartial() {
    var p1 = MakePhase1();
    // Kid said "ca" instead of "cat" -> dropped coda
    var p2 = MakePhase2(overallScore: 85f, codaDropped: true);
    var p3 = MakePhase3(cloudScore: 80f, codaPhonemeScore: 25f);

    var payload = CrossValidationPedagogicalEngine.Fuse("cat", p1, p2, p3);

    Assert.IsTrue(payload.scores_summary.coda_veto_cap_applied, "Coda Veto Gate must be applied when coda is dropped");
    Assert.LessOrEqual(payload.scores_summary.final_fusion_score, 68.0f, "Final fusion score must be capped at 68.0");
    Assert.AreEqual("Partial", payload.normative_decision, "Dropped coda cannot receive Pass or StrongPass");
    Assert.AreEqual(1, payload.star_reward);
  }

  [Test]
  public void P19_Rule3_OpenSyllable_NoCodaWeight_PenalizesCodaInsertion() {
    var p1 = MakePhase1();
    var p2 = MakePhase2(overallScore: 90f, codaType: "NONE");
    // Target is "bee" (open syllable), but kid inserted a stop burst or sibilant
    p2.english_phonetic_metrics.coda_and_clusters.high_freq_fricative_energy_ok = true;
    var p3 = MakePhase3(cloudScore: 90f);

    var payload = CrossValidationPedagogicalEngine.Fuse("bee", p1, p2, p3);

    Assert.AreEqual("RULE_3_BALANCED_CONSENSUS", payload.active_honesty_rule_triggered);
    // Score penalized by 15 points
    Assert.LessOrEqual(payload.scores_summary.final_fusion_score, 85f);
  }

  [Test]
  public void P19_AshaAge4Tiering_AppliesCorrectOffset() {
    var p1 = MakePhase1();
    var p2 = MakePhase2(overallScore: 80f);
    var p3 = MakePhase3(cloudScore: 80f);

    // Tier 1: "cat"
    var resCat = CrossValidationPedagogicalEngine.Fuse("cat", p1, p2, p3);
    Assert.AreEqual("TIER_1_EARLY", resCat.scores_summary.age4_difficulty_tier);
    Assert.AreEqual(0f, resCat.scores_summary.age4_tolerance_boost_applied);

    // Tier 3: "three" (contains /θ/)
    var resThree = CrossValidationPedagogicalEngine.Fuse("three", p1, p2, p3);
    Assert.AreEqual("TIER_3_LATE_AGE4", resThree.scores_summary.age4_difficulty_tier);
    Assert.AreEqual(8.0f, resThree.scores_summary.age4_tolerance_boost_applied);
  }

  [Test]
  public void P19_DecisionMatrix_MapsScoresToCorrectStarRewards() {
    var p1 = MakePhase1();

    // 1. StrongPass: 3 Stars
    var p2Strong = MakePhase2(overallScore: 95f);
    var p3Strong = MakePhase3(cloudScore: 95f);
    var resStrong = CrossValidationPedagogicalEngine.Fuse("cat", p1, p2Strong, p3Strong);
    Assert.AreEqual("StrongPass", resStrong.normative_decision);
    Assert.AreEqual(3, resStrong.star_reward);
    Assert.AreEqual("CELEBRATE_3STAR", resStrong.pedagogical_feedback_for_4yo.avatar_mouth_animation_id);

    // 2. Pass: 2 Stars
    var p2Pass = MakePhase2(overallScore: 72f);
    p2Pass.english_phonetic_metrics.coda_and_clusters.score = 72f;
    var p3Pass = MakePhase3(cloudScore: 72f, codaPhonemeScore: 70f);
    var resPass = CrossValidationPedagogicalEngine.Fuse("cat", p1, p2Pass, p3Pass);
    Assert.AreEqual("Pass", resPass.normative_decision);
    Assert.AreEqual(2, resPass.star_reward);
  }

  [Test]
  public void P19_SingleFocusCoach_DroppedCodaHasHighestPriority() {
    var p1 = MakePhase1();
    // Both coda dropped AND aspiration missed -> Coda must win priority!
    var p2 = MakePhase2(overallScore: 60f, codaDropped: true, aspirationPassed: false);
    var p3 = MakePhase3(cloudScore: 60f, codaPhonemeScore: 30f);

    var res = CrossValidationPedagogicalEngine.Fuse("cat", p1, p2, p3);

    Assert.AreEqual("DROPPED_CODA", res.pedagogical_feedback_for_4yo.primary_error_focus);
    Assert.AreEqual("MOUTH_POP_BURST", res.pedagogical_feedback_for_4yo.avatar_mouth_animation_id);
    Assert.IsTrue(res.pedagogical_feedback_for_4yo.voice_coach_prompt_vi.Contains("tiếng trống"));
  }

  [Test]
  public void P19_Rule1_NoRescueWithoutRealLocalConfidence_HonestDefault() {
    var p1 = MakePhase1(snr: 10f);          // noisy room
    var p2 = MakePhase2(overallScore: 88f); // Local DSP confident
    var p3 = MakePhase3(cloudScore: 30f);   // Cloud penalized
    // NO local CTC confidence passed: the default must be "unknown" (0.0), so a
    // placeholder confidence can never fabricate a Rule 1 rescue.
    var payload = CrossValidationPedagogicalEngine.Fuse("cat", p1, p2, p3);
    Assert.AreNotEqual("RULE_1_HALLUCINATION_RESCUE", payload.active_honesty_rule_triggered,
      "Rule 1 must not trigger from a fabricated default local confidence");
  }

  [Test]
  public void P19_ExecutionLatency_ExecutesUnder2Milliseconds() {
    var p1 = MakePhase1();
    var p2 = MakePhase2();
    var p3 = MakePhase3();

    // Warm-up
    CrossValidationPedagogicalEngine.Fuse("apple", p1, p2, p3);

    var sw = Stopwatch.StartNew();
    const int runs = 5;
    for (int i = 0; i < runs; i++) {
      CrossValidationPedagogicalEngine.Fuse("apple", p1, p2, p3);
    }
    sw.Stop();

    float avgMs = (float)sw.Elapsed.TotalMilliseconds / runs;
    Assert.Less(avgMs, 2.0f, $"Cross-validation fusion must execute in < 2ms, measured {avgMs:F3}ms");
  }

  [Test]
  public void P19_JsonPayloadSerialization_MatchesExpectedSchema() {
    var p1 = MakePhase1();
    var p2 = MakePhase2();
    var p3 = MakePhase3();

    var payload = CrossValidationPedagogicalEngine.Fuse("apple", p1, p2, p3);
    string json = payload.ToJson();

    Assert.IsTrue(json.Contains("\"utterance_id\""));
    Assert.IsTrue(json.Contains("\"target_word\": \"apple\""));
    Assert.IsTrue(json.Contains("\"active_honesty_rule_triggered\""));
    Assert.IsTrue(json.Contains("\"normative_decision\""));
    Assert.IsTrue(json.Contains("\"star_reward\""));
    Assert.IsTrue(json.Contains("\"scores_summary\""));
    Assert.IsTrue(json.Contains("\"syllable_by_syllable_breakdown\""));
    Assert.IsTrue(json.Contains("\"pedagogical_feedback_for_4yo\""));
    Assert.IsTrue(json.Contains("\"avatar_mouth_animation_id\""));
    Assert.IsTrue(json.Contains("\"voice_coach_prompt_vi\""));
  }
}
