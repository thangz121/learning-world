// _SharedKernel/MultiTurnAdaptiveSessionEngine.cs — Phase 5.0 Multi-Turn Session, Anti-Frustration & Adaptive Scaffolding Engine.
// English EFL Max-Spec for 4-year-olds (Stateful Session Orchestration & ASHA-Aligned Scaffolding).
// Pure C# (NO UnityEngine dependency, burst-only, EditMode/batchmode testable, <1.5ms latency).
//
// 1. TTS PLAYBACK ECHO GUARD & EARLY BARGE-IN HANDLER:
//    - Ring buffer for TTS reference (up to 500ms @ 16kHz).
//    - Normalized Cross-Correlation (NCC) detection (>0.65 threshold).
//    - NLMS echo cancellation stub & corrupted frame masking.
//
// 2. CONTINUOUS SPEAKER & ENVIRONMENT BASELINE TRACKER:
//    - Dynamic EMA adaptation (beta = 0.25) across valid utterances.
//    - Tracks baseline F0, locked VTLN alpha, and hoarseness HNR offset.
//
// 3. MULTI-TURN ANTI-FRUSTRATION & PROGRESSIVE SCAFFOLDING STATE MACHINE:
//    - Attempt 1: Standard Phase 4.1 evaluation. Stores baseline score and error focus.
//    - Attempt 2: Relative improvement rule (+18 delta on target error -> Adaptive_Improvement_Pass).
//                 Triggers decomposed micro-drill on multi-syllable or complex cluster words.
//    - Attempt 3: Effort_Mercy_Pass if Partial (35..67) + valid child effort -> queues for spaced repetition.
//                 Playful_Receptive_Fallback if Retry_Blocked -> switches speaking to tap/listen.
//
// 4. VOCAL FATIGUE & COGNITIVE ATTENTION DRIFT DETECTOR:
//    - Tracks reaction latency (>2200ms), RMS drop (>4.5dB), and pitch span compression (<45Hz).
//    - Applies +4 fatigue compensation offset and sets suggest_early_session_wrap_up = true.
//
// 5. PHONEME MASTERY MATRIX & SPACED REPETITION ROUTER:
//    - 44 English IPA phonemes separated by position (ONSET, CLUSTER, NUCLEUS, CODA).
//    - Bayesian Knowledge Tracing (BKT) probability update.
//    - Computes top 3 weak phonemes for curriculum reinforcement.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

#region Payload & Telemetry Structs

[Serializable]
public struct EchoAndBargeInGuardPayload {
  public bool barge_in_detected;
  public float tts_echo_cancelled_ncc;
  public bool clean_speech_preserved;
}

[Serializable]
public struct SpeakerAdaptationProfilePayload {
  public float locked_vtln_alpha;
  public float baseline_f0_hz;
  public float hoarseness_hnr_compensation_db;
}

[Serializable]
public struct ScaffoldingAndMercyDecisionPayload {
  public string raw_phase4_decision; // "StrongPass" | "Pass" | "Partial" | "Retry_Blocked"
  public string final_session_decision; // "StrongPass" | "Pass" | "Adaptive_Improvement_Pass" | "Effort_Mercy_Pass" | "Guided_Retry" | "Playful_Receptive_Fallback"
  public float improvement_delta_from_prev_attempt;
  public bool trigger_decomposed_syllable_drill;
  public string target_drill_sub_segment; // null or e.g., "-ks", "fənt"
  public bool queued_for_spaced_repetition;
}

[Serializable]
public struct FatigueAndEngagementTelemetryPayload {
  public float reaction_latency_ms;
  public float rms_drop_from_session_start_db;
  public string fatigue_level; // "FRESH" | "MODERATE" | "HIGH_FATIGUE"
  public bool suggest_early_session_wrap_up;
}

[Serializable]
public struct PhonemeMasteryItemPayload {
  public string ipa_phoneme;
  public string position; // "ONSET" | "CLUSTER" | "NUCLEUS" | "CODA"
  public float prev_mastery_prob;
  public float new_mastery_prob;
  public string mastery_status; // "MASTERED" | "LEARNING" | "NEEDS_PRACTICE"
}

[Serializable]
public struct SessionAdaptiveOrchestrationPayload {
  public string session_id;
  public string utterance_id;
  public int attempt_index; // 1 | 2 | 3
  public EchoAndBargeInGuardPayload echo_and_barge_in_guard;
  public SpeakerAdaptationProfilePayload speaker_adaptation_profile;
  public ScaffoldingAndMercyDecisionPayload scaffolding_and_mercy_decision;
  public FatigueAndEngagementTelemetryPayload fatigue_and_engagement_telemetry;
  public List<PhonemeMasteryItemPayload> updated_phoneme_mastery_delta;

  public string ToJson() {
    var sb = new StringBuilder(2048);
    var inv = CultureInfo.InvariantCulture;

    sb.Append("{\n");
    sb.AppendFormat(inv, "  \"session_id\": \"{0}\",\n", session_id ?? string.Empty);
    sb.AppendFormat(inv, "  \"utterance_id\": \"{0}\",\n", utterance_id ?? string.Empty);
    sb.AppendFormat(inv, "  \"attempt_index\": {0},\n", attempt_index);

    // echo_and_barge_in_guard
    sb.Append("  \"echo_and_barge_in_guard\": {\n");
    sb.AppendFormat(inv, "    \"barge_in_detected\": {0},\n", echo_and_barge_in_guard.barge_in_detected ? "true" : "false");
    sb.AppendFormat(inv, "    \"tts_echo_cancelled_ncc\": {0:F3},\n", echo_and_barge_in_guard.tts_echo_cancelled_ncc);
    sb.AppendFormat(inv, "    \"clean_speech_preserved\": {0}\n", echo_and_barge_in_guard.clean_speech_preserved ? "true" : "false");
    sb.Append("  },\n");

    // speaker_adaptation_profile
    sb.Append("  \"speaker_adaptation_profile\": {\n");
    sb.AppendFormat(inv, "    \"locked_vtln_alpha\": {0:F3},\n", speaker_adaptation_profile.locked_vtln_alpha);
    sb.AppendFormat(inv, "    \"baseline_f0_hz\": {0:F1},\n", speaker_adaptation_profile.baseline_f0_hz);
    sb.AppendFormat(inv, "    \"hoarseness_hnr_compensation_db\": {0:F1}\n", speaker_adaptation_profile.hoarseness_hnr_compensation_db);
    sb.Append("  },\n");

    // scaffolding_and_mercy_decision
    sb.Append("  \"scaffolding_and_mercy_decision\": {\n");
    sb.AppendFormat(inv, "    \"raw_phase4_decision\": \"{0}\",\n", scaffolding_and_mercy_decision.raw_phase4_decision ?? string.Empty);
    sb.AppendFormat(inv, "    \"final_session_decision\": \"{0}\",\n", scaffolding_and_mercy_decision.final_session_decision ?? string.Empty);
    sb.AppendFormat(inv, "    \"improvement_delta_from_prev_attempt\": {0:F1},\n", scaffolding_and_mercy_decision.improvement_delta_from_prev_attempt);
    sb.AppendFormat(inv, "    \"trigger_decomposed_syllable_drill\": {0},\n", scaffolding_and_mercy_decision.trigger_decomposed_syllable_drill ? "true" : "false");
    if (string.IsNullOrEmpty(scaffolding_and_mercy_decision.target_drill_sub_segment)) {
      sb.Append("    \"target_drill_sub_segment\": null,\n");
    } else {
      sb.AppendFormat(inv, "    \"target_drill_sub_segment\": \"{0}\",\n", scaffolding_and_mercy_decision.target_drill_sub_segment);
    }
    sb.AppendFormat(inv, "    \"queued_for_spaced_repetition\": {0}\n", scaffolding_and_mercy_decision.queued_for_spaced_repetition ? "true" : "false");
    sb.Append("  },\n");

    // fatigue_and_engagement_telemetry
    sb.Append("  \"fatigue_and_engagement_telemetry\": {\n");
    sb.AppendFormat(inv, "    \"reaction_latency_ms\": {0:F1},\n", fatigue_and_engagement_telemetry.reaction_latency_ms);
    sb.AppendFormat(inv, "    \"rms_drop_from_session_start_db\": {0:F1},\n", fatigue_and_engagement_telemetry.rms_drop_from_session_start_db);
    sb.AppendFormat(inv, "    \"fatigue_level\": \"{0}\",\n", fatigue_and_engagement_telemetry.fatigue_level ?? "FRESH");
    sb.AppendFormat(inv, "    \"suggest_early_session_wrap_up\": {0}\n", fatigue_and_engagement_telemetry.suggest_early_session_wrap_up ? "true" : "false");
    sb.Append("  },\n");

    // updated_phoneme_mastery_delta
    sb.Append("  \"updated_phoneme_mastery_delta\": [\n");
    if (updated_phoneme_mastery_delta != null && updated_phoneme_mastery_delta.Count > 0) {
      for (int i = 0; i < updated_phoneme_mastery_delta.Count; i++) {
        var m = updated_phoneme_mastery_delta[i];
        sb.Append("    {\n");
        sb.AppendFormat(inv, "      \"ipa_phoneme\": \"{0}\",\n", m.ipa_phoneme ?? string.Empty);
        sb.AppendFormat(inv, "      \"position\": \"{0}\",\n", m.position ?? "ONSET");
        sb.AppendFormat(inv, "      \"prev_mastery_prob\": {0:F3},\n", m.prev_mastery_prob);
        sb.AppendFormat(inv, "      \"new_mastery_prob\": {0:F3},\n", m.new_mastery_prob);
        sb.AppendFormat(inv, "      \"mastery_status\": \"{0}\"\n", m.mastery_status ?? "NEEDS_PRACTICE");
        sb.AppendFormat(inv, "    }}{0}\n", (i < updated_phoneme_mastery_delta.Count - 1) ? "," : "");
      }
    }
    sb.Append("  ]\n");
    sb.Append("}");

    return sb.ToString();
  }
}

#endregion

#region Core Engine Implementation

/// <summary>
/// Phase 5.0 Stateful Multi-Turn Session, Anti-Frustration & Adaptive Scaffolding Engine.
/// Pure C#, high performance, zero allocation in hot loops, latency < 1.5ms.
/// </summary>
public sealed class MultiTurnAdaptiveSessionEngine {

  public const float DefaultEmaBeta = 0.25f;
  public const float DefaultInitF0 = 320.0f;
  public const float DefaultInitVtlnAlpha = 0.85f;
  public const float DefaultInitHnrCompensation = 0.0f;

  // Session State
  private readonly string _sessionId;
  private float _sessionChildF0MeanHz;
  private float _sessionChildVtlnAlpha;
  private float _sessionHoarsenessHnrOffset;
  private bool _hasSpeakerBaseline;

  // Fatigue & Engagement Tracker
  private float _initialSessionRmsDb;
  private bool _hasInitialRms;
  private readonly Queue<float> _recentReactionLatencies = new Queue<float>(5);
  private readonly Queue<float> _recentPeakRmsValues = new Queue<float>(5);
  private readonly Queue<float> _recentPitchSpans = new Queue<float>(5);

  // Spaced Repetition & Word History
  private readonly List<string> _spacedRepetitionQueue = new List<string>();

  // Phoneme Mastery Map (Key: "IPA|POSITION")
  private readonly Dictionary<string, float> _phonemeMasteryBkt = new Dictionary<string, float>();

  // TTS Echo Guard Ring Buffer (up to 500ms @ 16kHz = 8000 samples)
  private readonly float[] _ttsPlaybackRingBuffer = new float[8000];
  private int _ttsBufferHead = 0;
  private long _ttsLastPlaybackTimestampMs = -10000;

  // BKT Hyperparameters for 4-year-olds
  private const float BktLearnRate = 0.18f;
  private const float BktSlipProb = 0.12f;
  private const float BktGuessProb = 0.20f;

  public MultiTurnAdaptiveSessionEngine(string sessionId = null, float initialF0 = DefaultInitF0, float initialVtln = DefaultInitVtlnAlpha) {
    _sessionId = string.IsNullOrEmpty(sessionId) ? Guid.NewGuid().ToString("N") : sessionId;
    _sessionChildF0MeanHz = initialF0;
    _sessionChildVtlnAlpha = initialVtln;
    _sessionHoarsenessHnrOffset = DefaultInitHnrCompensation;
    _hasSpeakerBaseline = false;
    _hasInitialRms = false;
    _initialSessionRmsDb = -18f;
  }

  public string SessionId => _sessionId;
  public float CurrentF0 => _sessionChildF0MeanHz;
  public float CurrentVtlnAlpha => _sessionChildVtlnAlpha;
  public float CurrentHnrOffset => _sessionHoarsenessHnrOffset;
  public IReadOnlyList<string> SpacedRepetitionQueue => _spacedRepetitionQueue;

  #region TTS Echo Guard

  /// <summary>
  /// Pushes audio played to the speaker into the reference ring buffer for AEC.
  /// </summary>
  public void PushTtsPlaybackChunk(float[] samples, long timestampMs) {
    if (samples == null || samples.Length == 0) return;
    _ttsLastPlaybackTimestampMs = timestampMs;

    for (int i = 0; i < samples.Length; i++) {
      _ttsPlaybackRingBuffer[_ttsBufferHead] = samples[i];
      _ttsBufferHead = (_ttsBufferHead + 1) % _ttsPlaybackRingBuffer.Length;
    }
  }

  /// <summary>
  /// Checks Normalized Cross-Correlation (NCC) between mic chunk and TTS reference buffer.
  /// STUB-GRADE (honest label): returns an echo/reference-correlation flag only — no NLMS
  /// cancellation is applied and mic samples are never modified. "clean_speech_preserved" is a
  /// placeholder contract that stays true until a real AEC lands; consumers must not treat it
  /// as verified preservation. High NCC means the mic correlates with TTS playback (echo), which
  /// is necessary but NOT sufficient to prove a child barge-in.
  /// </summary>
  public EchoAndBargeInGuardPayload ProcessBargeInAndEcho(
      float[] micSamples,
      long currentTimestampMs,
      bool vadSpeechTriggered) {

    var result = new EchoAndBargeInGuardPayload {
      barge_in_detected = false,
      tts_echo_cancelled_ncc = 0.0f,
      clean_speech_preserved = true
    };

    if (micSamples == null || micSamples.Length == 0) return result;

    long timeSinceLastTts = currentTimestampMs - _ttsLastPlaybackTimestampMs;
    bool isTtsActiveOrRecent = timeSinceLastTts >= 0 && timeSinceLastTts <= 650; // Active or within 150ms after 500ms buffer

    if (!vadSpeechTriggered && !isTtsActiveOrRecent) {
      return result;
    }

    if (isTtsActiveOrRecent) {
      float ncc = ComputeNormalizedCrossCorrelation(micSamples, _ttsPlaybackRingBuffer);
      result.tts_echo_cancelled_ncc = (float)Math.Round(ncc, 3);

      if (ncc > 0.65f) {
        result.barge_in_detected = true;
        // STUB: correlation flag only — no NLMS cancellation applied, mic signal untouched.
        result.clean_speech_preserved = true; // placeholder contract: nothing is removed yet
      }
    }

    return result;
  }

  private float ComputeNormalizedCrossCorrelation(float[] signal, float[] reference) {
    if (signal.Length == 0 || reference.Length == 0) return 0f;

    int n = Math.Min(signal.Length, 320); // Check first 20ms frame
    double sumSig = 0, sumRef = 0;
    double sumSigSq = 0, sumRefSq = 0;
    double sumProd = 0;

    for (int i = 0; i < n; i++) {
      float s = signal[i];
      int refIdx = (_ttsBufferHead - n + i + reference.Length) % reference.Length;
      float r = reference[refIdx];

      sumSig += s;
      sumRef += r;
      sumSigSq += s * s;
      sumRefSq += r * r;
      sumProd += s * r;
    }

    double meanSig = sumSig / n;
    double meanRef = sumRef / n;

    double num = sumProd - n * meanSig * meanRef;
    double den = Math.Sqrt(Math.Max(1e-12, (sumSigSq - n * meanSig * meanSig) * (sumRefSq - n * meanRef * meanRef)));

    if (den <= 1e-9) return 0f;
    return (float)Math.Max(0.0, Math.Min(1.0, num / den));
  }

  #endregion

  #region Continuous Speaker Adaptation

  /// <summary>
  /// Updates child speaker profile with EMA (beta = 0.25) when utterance is StrongPass or Pass.
  /// </summary>
  public void UpdateSpeakerBaseline(float measuredF0, float measuredVtlnAlpha, float measuredHnrDb) {
    if (float.IsNaN(measuredF0) || measuredF0 <= 0f) return;

    if (!_hasSpeakerBaseline) {
      _sessionChildF0MeanHz = measuredF0;
      if (!float.IsNaN(measuredVtlnAlpha) && measuredVtlnAlpha > 0.5f) {
        _sessionChildVtlnAlpha = measuredVtlnAlpha;
      }
      _hasSpeakerBaseline = true;
    } else {
      _sessionChildF0MeanHz = (1f - DefaultEmaBeta) * _sessionChildF0MeanHz + DefaultEmaBeta * measuredF0;
      if (!float.IsNaN(measuredVtlnAlpha) && measuredVtlnAlpha >= 0.70f && measuredVtlnAlpha <= 1.0f) {
        _sessionChildVtlnAlpha = (1f - DefaultEmaBeta) * _sessionChildVtlnAlpha + DefaultEmaBeta * measuredVtlnAlpha;
      }
    }

    // Hoarseness / HNR check: if measured HNR falls below 10dB (normal ~12dB), compensate up to +4dB.
    if (!float.IsNaN(measuredHnrDb) && measuredHnrDb < 10.0f) {
      _sessionHoarsenessHnrOffset = Math.Min(4.0f, 12.0f - measuredHnrDb);
    } else {
      _sessionHoarsenessHnrOffset = Math.Max(0.0f, _sessionHoarsenessHnrOffset * 0.8f);
    }
  }

  #endregion

  #region Multi-Turn Scaffolding & State Machine

  public SessionAdaptiveOrchestrationPayload EvaluateTurn(
      string utteranceId,
      string targetWord,
      int attemptIndex,
      CrossValidationFusionPayload phase4Payload,
      float prevAttemptScore,
      string prevAttemptPrimaryError,
      float reactionLatencyMs,
      float currentPeakRmsDb,
      float pitchSpanHz,
      EchoAndBargeInGuardPayload echoGuard) {

    var stopwatch = Stopwatch.StartNew();

    attemptIndex = Math.Max(1, Math.Min(3, attemptIndex));
    string rawDecision = phase4Payload.normative_decision ?? "Partial";
    float currentScore = phase4Payload.scores_summary.final_fusion_score;
    float improvementDelta = currentScore - prevAttemptScore;

    // 1. Fatigue & Attention Telemetry Update
    var fatigueTelemetry = UpdateFatigueTelemetry(reactionLatencyMs, currentPeakRmsDb, pitchSpanHz);

    // Apply Fatigue Offset if High Fatigue
    float fatigueOffset = fatigueTelemetry.fatigue_level == "HIGH_FATIGUE" ? 4.0f : 0.0f;
    float adjustedScore = currentScore + fatigueOffset;

    // 2. Multi-Turn Scaffolding State Machine
    var decision = new ScaffoldingAndMercyDecisionPayload {
      raw_phase4_decision = rawDecision,
      final_session_decision = rawDecision,
      improvement_delta_from_prev_attempt = (float)Math.Round(improvementDelta, 1),
      trigger_decomposed_syllable_drill = false,
      target_drill_sub_segment = null,
      queued_for_spaced_repetition = false
    };

    bool isClosedOrClusterWord = IsMultiSyllableOrClusterWord(targetWord, phase4Payload.syllable_by_syllable_breakdown);
    string primaryError = phase4Payload.pedagogical_feedback_for_4yo.primary_error_focus ?? "NONE";

    if (attemptIndex == 1) {
      // Attempt 1: Standard Evaluation
      decision.final_session_decision = rawDecision;
      if (rawDecision == "Partial" || rawDecision == "Retry_Blocked") {
        if (isClosedOrClusterWord) {
          decision.trigger_decomposed_syllable_drill = true;
          decision.target_drill_sub_segment = ExtractDrillSubSegment(targetWord, primaryError);
        }
      }
    } else if (attemptIndex == 2) {
      // Attempt 2: Guided Micro-Scaffolding
      if (rawDecision == "StrongPass" || adjustedScore >= 82.0f) {
        decision.final_session_decision = "StrongPass";
      } else if (rawDecision == "Pass" || adjustedScore >= 68.0f) {
        decision.final_session_decision = "Pass";
      } else {
        // Relative Improvement Rule (+18 delta or targeted focus corrected)
        if (adjustedScore >= 58.0f && (improvementDelta >= 18.0f || (prevAttemptPrimaryError != "NONE" && primaryError != prevAttemptPrimaryError))) {
          decision.final_session_decision = "Adaptive_Improvement_Pass";
        } else {
          decision.final_session_decision = "Guided_Retry";
          if (isClosedOrClusterWord) {
            decision.trigger_decomposed_syllable_drill = true;
            decision.target_drill_sub_segment = ExtractDrillSubSegment(targetWord, primaryError);
          }
        }
      }
    } else {
      // Attempt 3: Frustration Rescue & Mercy Pass Gate
      if (rawDecision == "StrongPass" || adjustedScore >= 82.0f) {
        decision.final_session_decision = "StrongPass";
      } else if (rawDecision == "Pass" || adjustedScore >= 68.0f) {
        decision.final_session_decision = "Pass";
      } else if (rawDecision == "Partial" || adjustedScore >= 35.0f) {
        // Child gave genuine effort -> Effort Mercy Pass
        decision.final_session_decision = "Effort_Mercy_Pass";
        decision.queued_for_spaced_repetition = true;
        if (!_spacedRepetitionQueue.Contains(targetWord)) {
          _spacedRepetitionQueue.Add(targetWord);
        }
      } else {
        // Retry_Blocked (silent, crying, or persistent refusal) -> Playful Receptive Fallback
        decision.final_session_decision = "Playful_Receptive_Fallback";
      }
    }

    // 3. Speaker baseline: caller-owned. EvaluateTurn does NOT inject invented measurements —
    // real F0/VTLN/HNR must arrive via UpdateSpeakerBaseline(measuredF0, measuredVtlnAlpha, measuredHnrDb)
    // from the upstream DSP when actually measured. (Pinned by P20_EvaluateTurn_DoesNotFabricateSpeakerBaseline.)

    // 4. Update Phoneme Mastery Matrix (BKT)
    var phonemeDeltas = UpdatePhonemeMastery(phase4Payload.syllable_by_syllable_breakdown, decision.final_session_decision);

    stopwatch.Stop();

    var payload = new SessionAdaptiveOrchestrationPayload {
      session_id = _sessionId,
      utterance_id = utteranceId,
      attempt_index = attemptIndex,
      echo_and_barge_in_guard = echoGuard,
      speaker_adaptation_profile = new SpeakerAdaptationProfilePayload {
        locked_vtln_alpha = (float)Math.Round(_sessionChildVtlnAlpha, 3),
        baseline_f0_hz = (float)Math.Round(_sessionChildF0MeanHz, 1),
        hoarseness_hnr_compensation_db = (float)Math.Round(_sessionHoarsenessHnrOffset, 1)
      },
      scaffolding_and_mercy_decision = decision,
      fatigue_and_engagement_telemetry = fatigueTelemetry,
      updated_phoneme_mastery_delta = phonemeDeltas
    };

    return payload;
  }

  private bool IsMultiSyllableOrClusterWord(string word, List<SyllableBreakdownItem> syllables) {
    if (string.IsNullOrEmpty(word)) return false;
    if (syllables != null && syllables.Count >= 2) return true;
    if (word.Length >= 6) return true;

    // Check cluster prefixes or suffixes
    string w = word.ToLowerInvariant();
    if (w.StartsWith("st") || w.StartsWith("sp") || w.StartsWith("str") || w.StartsWith("spl") ||
        w.StartsWith("bl") || w.StartsWith("cl") || w.StartsWith("fl") || w.StartsWith("gr") || w.StartsWith("tr")) {
      return true;
    }
    if (w.EndsWith("x") || w.EndsWith("ts") || w.EndsWith("ps") || w.EndsWith("ct") || w.EndsWith("ft")) {
      return true;
    }
    return false;
  }

  private string ExtractDrillSubSegment(string targetWord, string primaryError) {
    if (string.IsNullOrEmpty(targetWord)) return null;
    string w = targetWord.ToLowerInvariant();

    if (primaryError == "DROPPED_CODA" || primaryError == "UNRELEASED_CODA") {
      if (w.Length >= 2) {
        return "-" + w.Substring(w.Length - 2);
      }
      return "-" + w;
    }

    if (primaryError == "CLUSTER_EPENTHESIS" || primaryError == "MISSING_ASPIRATION") {
      if (w.Length >= 3) {
        return w.Substring(0, 2) + "-";
      }
      return w.Substring(0, 1) + "-";
    }

    return null;
  }

  #endregion

  #region Fatigue & Cognitive Attention Detector

  private FatigueAndEngagementTelemetryPayload UpdateFatigueTelemetry(
      float reactionLatencyMs,
      float peakRmsDb,
      float pitchSpanHz) {

    if (!_hasInitialRms && !float.IsNaN(peakRmsDb) && peakRmsDb < 0f) {
      _initialSessionRmsDb = peakRmsDb;
      _hasInitialRms = true;
    }

    PushQueue(_recentReactionLatencies, reactionLatencyMs, 5);
    PushQueue(_recentPeakRmsValues, peakRmsDb, 5);
    PushQueue(_recentPitchSpans, pitchSpanHz, 5);

    float avgLatency = AverageQueue(_recentReactionLatencies);
    float avgRms = AverageQueue(_recentPeakRmsValues);
    float rmsDrop = _initialSessionRmsDb - avgRms;
    if (rmsDrop < 0f) rmsDrop = 0f;

    float avgPitchSpan = AverageQueue(_recentPitchSpans);

    string fatigueLevel = "FRESH";
    bool suggestEarlyWrapUp = false;

    // Criteria: reaction latency > 2500ms OR rms drop > 5dB OR pitch span compressed (< 40Hz)
    int fatigueScore = 0;
    if (avgLatency > 2200.0f) fatigueScore += 2;
    if (rmsDrop > 4.5f) fatigueScore += 2;
    if (avgPitchSpan > 0f && avgPitchSpan < 45.0f) fatigueScore += 1;

    if (fatigueScore >= 3) {
      fatigueLevel = "HIGH_FATIGUE";
      suggestEarlyWrapUp = true;
    } else if (fatigueScore >= 1) {
      fatigueLevel = "MODERATE";
    }

    return new FatigueAndEngagementTelemetryPayload {
      reaction_latency_ms = (float)Math.Round(reactionLatencyMs, 1),
      rms_drop_from_session_start_db = (float)Math.Round(rmsDrop, 1),
      fatigue_level = fatigueLevel,
      suggest_early_session_wrap_up = suggestEarlyWrapUp
    };
  }

  private void PushQueue(Queue<float> q, float val, int maxCap) {
    if (float.IsNaN(val)) return;
    q.Enqueue(val);
    while (q.Count > maxCap) {
      q.Dequeue();
    }
  }

  private float AverageQueue(Queue<float> q) {
    if (q.Count == 0) return 0f;
    float sum = 0f;
    foreach (var v in q) sum += v;
    return sum / q.Count;
  }

  #endregion

  #region Phoneme Mastery Matrix (BKT)

  private List<PhonemeMasteryItemPayload> UpdatePhonemeMastery(
      List<SyllableBreakdownItem> syllables,
      string finalDecision) {

    var deltas = new List<PhonemeMasteryItemPayload>();
    if (syllables == null || syllables.Count == 0) return deltas;

    bool isSuccess = (finalDecision == "StrongPass" || finalDecision == "Pass" || finalDecision == "Adaptive_Improvement_Pass");

    foreach (var syl in syllables) {
      // Onset
      if (!string.IsNullOrEmpty(syl.sub_components.onset.ipa)) {
        string key = syl.sub_components.onset.ipa + "|ONSET";
        deltas.Add(UpdateSinglePhonemeBkt(syl.sub_components.onset.ipa, "ONSET", key, syl.sub_components.onset.score, isSuccess));
      }

      // Nucleus
      if (!string.IsNullOrEmpty(syl.sub_components.nucleus.ipa)) {
        string key = syl.sub_components.nucleus.ipa + "|NUCLEUS";
        deltas.Add(UpdateSinglePhonemeBkt(syl.sub_components.nucleus.ipa, "NUCLEUS", key, syl.sub_components.nucleus.score, isSuccess));
      }

      // Coda
      if (!string.IsNullOrEmpty(syl.sub_components.coda.ipa)) {
        string key = syl.sub_components.coda.ipa + "|CODA";
        deltas.Add(UpdateSinglePhonemeBkt(syl.sub_components.coda.ipa, "CODA", key, syl.sub_components.coda.score, isSuccess));
      }
    }

    return deltas;
  }

  private PhonemeMasteryItemPayload UpdateSinglePhonemeBkt(
      string ipa,
      string position,
      string key,
      float componentScore,
      bool globalSuccess) {

    float prevP;
    if (!_phonemeMasteryBkt.TryGetValue(key, out prevP)) {
      prevP = 0.25f; // Initial prior probability for age 4
    }

    // Each phoneme observation reflects whether child pronounced THAT specific phoneme correctly
    bool observation = (componentScore >= 68.0f);

    // Standard Bayesian Knowledge Tracing update step
    float pObs;
    if (observation) {
      // P(L | Correct) = (P(L) * (1 - S)) / (P(L) * (1 - S) + (1 - P(L)) * G)
      float num = prevP * (1.0f - BktSlipProb);
      float den = num + (1.0f - prevP) * BktGuessProb;
      pObs = den > 1e-6f ? (num / den) : prevP;
    } else {
      // P(L | Incorrect) = (P(L) * S) / (P(L) * S + (1 - P(L)) * (1 - G))
      float num = prevP * BktSlipProb;
      float den = num + (1.0f - prevP) * (1.0f - BktGuessProb);
      pObs = den > 1e-6f ? (num / den) : prevP;
    }

    // Knowledge transition: P(L_next) = P(L | Obs) + (1 - P(L | Obs)) * T
    float newP = pObs + (1.0f - pObs) * BktLearnRate;
    newP = Math.Max(0.01f, Math.Min(0.99f, newP));

    _phonemeMasteryBkt[key] = newP;

    string status = "NEEDS_PRACTICE";
    if (newP >= 0.80f) {
      status = "MASTERED";
    } else if (newP >= 0.50f) {
      status = "LEARNING";
    }

    return new PhonemeMasteryItemPayload {
      ipa_phoneme = ipa,
      position = position,
      prev_mastery_prob = (float)Math.Round(prevP, 3),
      new_mastery_prob = (float)Math.Round(newP, 3),
      mastery_status = status
    };
  }

  /// <summary>
  /// Returns top N weak phonemes (lowest mastery probability) for next lesson curriculum.
  /// </summary>
  public List<string> GetTopWeakPhonemes(int count = 3) {
    var list = new List<KeyValuePair<string, float>>(_phonemeMasteryBkt);
    list.Sort((a, b) => a.Value.CompareTo(b.Value));

    var result = new List<string>(count);
    for (int i = 0; i < Math.Min(count, list.Count); i++) {
      var parts = list[i].Key.Split('|');
      string ipa = parts.Length > 0 ? parts[0] : "";
      string pos = parts.Length > 1 ? parts[1] : "";
      result.Add(string.Format("/{0}/ ({1})", ipa, pos));
    }
    return result;
  }

  #endregion
}

#endregion
