// Assets/Tests/EditMode/CT-P20_MultiTurnAdaptiveSessionEngine.cs — Phase 5.0 Session Tests.
// Tests TTS Echo Guard, Continuous Baseline Tracker, 3-Attempt Scaffolding State Machine, Fatigue, and BKT.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;

[TestFixture]
public class CT_P20_MultiTurnAdaptiveSessionEngine {

  private CrossValidationFusionPayload MakePhase4Payload(
      string word = "cat",
      string decision = "Partial",
      float score = 60f,
      string primaryError = "DROPPED_CODA",
      string codaStatus = "DROPPED",
      float codaScore = 20f) {

    var syllables = new List<SyllableBreakdownItem> {
      new SyllableBreakdownItem {
        syllable_index = 0,
        syllable_text = word,
        syllable_ipa = "kæt",
        is_stressed_target = true,
        stress_achieved = true,
        syllable_ui_color = "YELLOW",
        sub_components = new SyllableSubComponents {
          onset = new ComponentStatus("k", 85f, "GOOD"),
          nucleus = new ComponentStatus("æ", 80f, "GOOD"),
          coda = new ComponentStatus("t", codaScore, codaStatus)
        }
      }
    };

    return new CrossValidationFusionPayload {
      utterance_id = "utt-test-p5",
      target_word = word,
      total_pipeline_latency_ms = 1.2f,
      active_honesty_rule_triggered = "RULE_3_BALANCED_CONSENSUS",
      normative_decision = decision,
      star_reward = decision == "StrongPass" ? 3 : (decision == "Pass" ? 2 : (decision == "Partial" ? 1 : 0)),
      scores_summary = new ScoresSummary {
        final_fusion_score = score,
        age4_difficulty_tier = "TIER_1_EARLY",
        age4_tolerance_boost_applied = 0f,
        ipa_phoneme_consensus_score = score,
        coda_integrity_consensus_score = codaScore,
        coda_veto_cap_applied = (codaScore < 35f)
      },
      syllable_by_syllable_breakdown = syllables,
      pedagogical_feedback_for_4yo = new PedagogicalFeedbackFor4Yo {
        primary_error_focus = primaryError,
        avatar_mouth_animation_id = "MOUTH_SMILE_HISS",
        highlight_phoneme_index_on_ui = 2,
        voice_coach_prompt_vi = "Bé ơi xì đuôi nào!",
        phonetic_sound_effect_cue = "snake_hiss"
      }
    };
  }

  [Test]
  public void P20_TtsEchoGuard_DetectsBargeInWhenCorrelationExceedsThreshold() {
    var engine = new MultiTurnAdaptiveSessionEngine("session-test-01");

    // 1. Prepare simulated TTS audio (e.g. 1000Hz tone)
    float[] ttsAudio = new float[1600]; // 100ms
    for (int i = 0; i < ttsAudio.Length; i++) {
      ttsAudio[i] = (float)Math.Sin(2.0 * Math.PI * 1000.0 * i / 16000.0);
    }
    long ttsTimestamp = 1000;
    engine.PushTtsPlaybackChunk(ttsAudio, ttsTimestamp);

    // 2. Kid barge-in microphone picks up the same loudspeaker tone
    float[] micAudioBargeIn = new float[320];
    for (int i = 0; i < micAudioBargeIn.Length; i++) {
      micAudioBargeIn[i] = (float)Math.Sin(2.0 * Math.PI * 1000.0 * i / 16000.0); // Exact match
    }

    var result = engine.ProcessBargeInAndEcho(micAudioBargeIn, currentTimestampMs: 1050, vadSpeechTriggered: true);

    Assert.That(result.barge_in_detected, Is.True, "Barge-in should be detected when mic correlates heavily with TTS playback.");
    Assert.That(result.tts_echo_cancelled_ncc, Is.GreaterThan(0.65f));
    Assert.That(result.clean_speech_preserved, Is.True, "Stub contract: clean_speech_preserved stays true (flag only — no real NLMS cancellation applied yet; honest label).");
  }

  [Test]
  public void P20_SpeakerAdaptation_UpdatesBaselineWithEmaAcrossPasses() {
    var engine = new MultiTurnAdaptiveSessionEngine("session-test-02", initialF0: 300f, initialVtln: 0.85f);

    float initialF0 = engine.CurrentF0;
    Assert.That(initialF0, Is.EqualTo(300f));

    // First successful utterance measured F0 = 360Hz
    engine.UpdateSpeakerBaseline(measuredF0: 360f, measuredVtlnAlpha: 0.88f, measuredHnrDb: 15f);

    // If initial baseline uninitialized, sets directly to 360
    Assert.That(engine.CurrentF0, Is.EqualTo(360f));

    // Second utterance with 400Hz -> EMA with beta = 0.25: 0.75 * 360 + 0.25 * 400 = 270 + 100 = 370
    engine.UpdateSpeakerBaseline(measuredF0: 400f, measuredVtlnAlpha: 0.88f, measuredHnrDb: 15f);
    Assert.That(engine.CurrentF0, Is.EqualTo(370f).Within(0.1f));
  }

  [Test]
  public void P20_EvaluateTurn_DoesNotFabricateSpeakerBaseline() {
    var engine = new MultiTurnAdaptiveSessionEngine("session-test-pin", initialF0: 300f, initialVtln: 0.85f);

    var p4 = MakePhase4Payload(word: "cat", decision: "Pass", score: 75f);
    var echo = new EchoAndBargeInGuardPayload { barge_in_detected = false, tts_echo_cancelled_ncc = 0f, clean_speech_preserved = true };

    var payload = engine.EvaluateTurn(
        utteranceId: "utt-pin",
        targetWord: "cat",
        attemptIndex: 1,
        phase4Payload: p4,
        prevAttemptScore: 0f,
        prevAttemptPrimaryError: "NONE",
        reactionLatencyMs: 1000f,
        currentPeakRmsDb: -16f,
        pitchSpanHz: 60f,
        echoGuard: echo);

    Assert.That(engine.CurrentF0, Is.EqualTo(300f), "EvaluateTurn must not inject fabricated F0 measurements into the speaker baseline.");
    Assert.That(payload.speaker_adaptation_profile.baseline_f0_hz, Is.EqualTo(300f).Within(0.1f));
  }

  [Test]
  public void P20_HoarsenessCompensation_TriggersOffsetWhenHnrIsLow() {
    var engine = new MultiTurnAdaptiveSessionEngine("session-test-03");

    // Kid has cold, HNR drops to 8dB (normal > 12dB)
    engine.UpdateSpeakerBaseline(measuredF0: 320f, measuredVtlnAlpha: 0.85f, measuredHnrDb: 8.0f);

    Assert.That(engine.CurrentHnrOffset, Is.GreaterThan(0.0f));
    Assert.That(engine.CurrentHnrOffset, Is.EqualTo(4.0f)); // 12 - 8 = 4dB compensation
  }

  [Test]
  public void P20_Attempt1_StandardEvaluation_RetainsRawScoreAndTriggersDrillOnMultiSyllable() {
    var engine = new MultiTurnAdaptiveSessionEngine("session-test-04");

    var p4 = MakePhase4Payload(word: "elephant", decision: "Partial", score: 55f, primaryError: "DROPPED_CODA");
    // Multi-syllable elephant
    p4.syllable_by_syllable_breakdown.Add(new SyllableBreakdownItem {
      syllable_index = 1,
      syllable_text = "phant",
      syllable_ipa = "fənt",
      sub_components = new SyllableSubComponents {
        onset = new ComponentStatus("f", 80f, "GOOD"),
        nucleus = new ComponentStatus("ə", 80f, "GOOD"),
        coda = new ComponentStatus("nt", 30f, "DROPPED")
      }
    });

    var echo = new EchoAndBargeInGuardPayload { barge_in_detected = false, tts_echo_cancelled_ncc = 0f, clean_speech_preserved = true };

    var result = engine.EvaluateTurn(
        utteranceId: "utt-01",
        targetWord: "elephant",
        attemptIndex: 1,
        phase4Payload: p4,
        prevAttemptScore: 0f,
        prevAttemptPrimaryError: "NONE",
        reactionLatencyMs: 1200f,
        currentPeakRmsDb: -16f,
        pitchSpanHz: 65f,
        echoGuard: echo);

    Assert.That(result.scaffolding_and_mercy_decision.final_session_decision, Is.EqualTo("Partial"));
    Assert.That(result.scaffolding_and_mercy_decision.trigger_decomposed_syllable_drill, Is.True, "Multi-syllable word failed on attempt 1 should trigger drill.");
    Assert.That(result.scaffolding_and_mercy_decision.target_drill_sub_segment, Is.EqualTo("-nt"));
  }

  [Test]
  public void P20_Attempt2_RelativeImprovementRule_GrantsAdaptiveImprovementPass() {
    var engine = new MultiTurnAdaptiveSessionEngine("session-test-05");

    // Attempt 1 was score 42, error DROPPED_CODA
    // Attempt 2 gets score 62 (delta = +20 >= 18), corrected coda!
    var p4 = MakePhase4Payload(word: "cat", decision: "Partial", score: 62f, primaryError: "NONE", codaStatus: "GOOD", codaScore: 78f);
    var echo = new EchoAndBargeInGuardPayload { barge_in_detected = false, tts_echo_cancelled_ncc = 0f, clean_speech_preserved = true };

    var result = engine.EvaluateTurn(
        utteranceId: "utt-02",
        targetWord: "cat",
        attemptIndex: 2,
        phase4Payload: p4,
        prevAttemptScore: 42f,
        prevAttemptPrimaryError: "DROPPED_CODA",
        reactionLatencyMs: 1100f,
        currentPeakRmsDb: -15f,
        pitchSpanHz: 70f,
        echoGuard: echo);

    Assert.That(result.scaffolding_and_mercy_decision.improvement_delta_from_prev_attempt, Is.EqualTo(20f).Within(0.1f));
    Assert.That(result.scaffolding_and_mercy_decision.final_session_decision, Is.EqualTo("Adaptive_Improvement_Pass"),
        "Relative improvement >= 18 on attempt 2 should award Adaptive_Improvement_Pass.");
  }

  [Test]
  public void P20_Attempt3_EffortMercyPass_RescuesGenuineAttemptAndQueuesSpacedRepetition() {
    var engine = new MultiTurnAdaptiveSessionEngine("session-test-06");

    // Attempt 3 still Partial (e.g. score 50)
    var p4 = MakePhase4Payload(word: "cat", decision: "Partial", score: 50f, primaryError: "DROPPED_CODA");
    var echo = new EchoAndBargeInGuardPayload { barge_in_detected = false, tts_echo_cancelled_ncc = 0f, clean_speech_preserved = true };

    var result = engine.EvaluateTurn(
        utteranceId: "utt-03",
        targetWord: "cat",
        attemptIndex: 3,
        phase4Payload: p4,
        prevAttemptScore: 48f,
        prevAttemptPrimaryError: "DROPPED_CODA",
        reactionLatencyMs: 1400f,
        currentPeakRmsDb: -16f,
        pitchSpanHz: 60f,
        echoGuard: echo);

    Assert.That(result.scaffolding_and_mercy_decision.final_session_decision, Is.EqualTo("Effort_Mercy_Pass"),
        "Attempt 3 with genuine effort should trigger Effort_Mercy_Pass.");
    Assert.That(result.scaffolding_and_mercy_decision.queued_for_spaced_repetition, Is.True);
    Assert.That(engine.SpacedRepetitionQueue, Contains.Item("cat"));
  }

  [Test]
  public void P20_Attempt3_PersistentRefusal_SwitchesToPlayfulReceptiveFallback() {
    var engine = new MultiTurnAdaptiveSessionEngine("session-test-07");

    // Attempt 3 is Retry_Blocked (child silent or blocked)
    var p4 = MakePhase4Payload(word: "dog", decision: "Retry_Blocked", score: 10f, primaryError: "SILENCE");
    var echo = new EchoAndBargeInGuardPayload { barge_in_detected = false, tts_echo_cancelled_ncc = 0f, clean_speech_preserved = true };

    var result = engine.EvaluateTurn(
        utteranceId: "utt-04",
        targetWord: "dog",
        attemptIndex: 3,
        phase4Payload: p4,
        prevAttemptScore: 10f,
        prevAttemptPrimaryError: "SILENCE",
        reactionLatencyMs: 3000f,
        currentPeakRmsDb: -35f,
        pitchSpanHz: 10f,
        echoGuard: echo);

    Assert.That(result.scaffolding_and_mercy_decision.final_session_decision, Is.EqualTo("Playful_Receptive_Fallback"),
        "Persistent refusal or silence on attempt 3 should smoothly switch to receptive listening/tap mode.");
  }

  [Test]
  public void P20_VocalFatigueDetector_IdentifiesTiredChildAndSuggestsEarlyWrapUp() {
    var engine = new MultiTurnAdaptiveSessionEngine("session-test-08");

    var p4 = MakePhase4Payload(word: "sun", decision: "Pass", score: 75f);
    var echo = new EchoAndBargeInGuardPayload { barge_in_detected = false, tts_echo_cancelled_ncc = 0f, clean_speech_preserved = true };

    // Simulate 5 turns of increasing latency, dropping volume, compressed pitch
    SessionAdaptiveOrchestrationPayload lastResult = default;
    for (int i = 0; i < 5; i++) {
      lastResult = engine.EvaluateTurn(
          utteranceId: "utt-f-" + i,
          targetWord: "sun",
          attemptIndex: 1,
          phase4Payload: p4,
          prevAttemptScore: 0f,
          prevAttemptPrimaryError: "NONE",
          reactionLatencyMs: 2600f, // > 2200
          currentPeakRmsDb: -26f,   // Initial was first turn, subsequent drops > 5dB
          pitchSpanHz: 30f,        // Compressed pitch span
          echoGuard: echo);
    }

    Assert.That(lastResult.fatigue_and_engagement_telemetry.fatigue_level, Is.EqualTo("HIGH_FATIGUE"));
    Assert.That(lastResult.fatigue_and_engagement_telemetry.suggest_early_session_wrap_up, Is.True);
  }

  [Test]
  public void P20_BayesianKnowledgeTracing_TracksMasteryAndIdentifiesWeakPhonemes() {
    var engine = new MultiTurnAdaptiveSessionEngine("session-test-09");

    // Cat with failed coda /t/
    var p4Failed = MakePhase4Payload(word: "cat", decision: "Partial", score: 55f, codaStatus: "DROPPED", codaScore: 20f);
    var echo = new EchoAndBargeInGuardPayload { barge_in_detected = false, tts_echo_cancelled_ncc = 0f, clean_speech_preserved = true };

    var result1 = engine.EvaluateTurn(
        utteranceId: "utt-bkt-1",
        targetWord: "cat",
        attemptIndex: 1,
        phase4Payload: p4Failed,
        prevAttemptScore: 0f,
        prevAttemptPrimaryError: "NONE",
        reactionLatencyMs: 1000f,
        currentPeakRmsDb: -16f,
        pitchSpanHz: 60f,
        echoGuard: echo);

    // Check delta for Coda /t/
    var codaItem = result1.updated_phoneme_mastery_delta.Find(x => x.ipa_phoneme == "t" && x.position == "CODA");
    Assert.That(codaItem.mastery_status, Is.EqualTo("NEEDS_PRACTICE"));

    var weakList = engine.GetTopWeakPhonemes(3);
    Assert.That(weakList.Count, Is.GreaterThan(0));
    Assert.That(weakList[0], Contains.Substring("/t/ (CODA)"));
  }

  [Test]
  public void P20_ExecutionLatency_ExecutesUnder1Point5Milliseconds() {
    var engine = new MultiTurnAdaptiveSessionEngine("session-test-latency");
    var p4 = MakePhase4Payload(word: "star", decision: "Pass", score: 75f);
    var echo = new EchoAndBargeInGuardPayload { barge_in_detected = false, tts_echo_cancelled_ncc = 0f, clean_speech_preserved = true };

    // Warm-up
    engine.EvaluateTurn("utt-w", "star", 1, p4, 0f, "NONE", 1000f, -16f, 60f, echo);

    int iterations = 100;
    var sw = Stopwatch.StartNew();
    for (int i = 0; i < iterations; i++) {
      engine.EvaluateTurn("utt-" + i, "star", 1, p4, 0f, "NONE", 1000f, -16f, 60f, echo);
    }
    sw.Stop();

    double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
    Assert.That(avgMs, Is.LessThan(1.5), "Phase 5.0 orchestration should complete in < 1.5ms per turn.");
  }

  [Test]
  public void P20_JsonSerialization_MatchesExpectedPayloadStructure() {
    var engine = new MultiTurnAdaptiveSessionEngine("session-test-json");
    var p4 = MakePhase4Payload(word: "star", decision: "StrongPass", score: 88f);
    var echo = new EchoAndBargeInGuardPayload { barge_in_detected = true, tts_echo_cancelled_ncc = 0.72f, clean_speech_preserved = true };

    var payload = engine.EvaluateTurn("utt-json-01", "star", 1, p4, 0f, "NONE", 950f, -15f, 65f, echo);
    string json = payload.ToJson();

    Assert.That(json, Does.Contain("\"session_id\":"));
    Assert.That(json, Does.Contain("\"echo_and_barge_in_guard\":"));
    Assert.That(json, Does.Contain("\"barge_in_detected\": true"));
    Assert.That(json, Does.Contain("\"tts_echo_cancelled_ncc\": 0.720"));
    Assert.That(json, Does.Contain("\"speaker_adaptation_profile\":"));
    Assert.That(json, Does.Contain("\"scaffolding_and_mercy_decision\":"));
    Assert.That(json, Does.Contain("\"fatigue_and_engagement_telemetry\":"));
    Assert.That(json, Does.Contain("\"updated_phoneme_mastery_delta\":"));
  }
}
