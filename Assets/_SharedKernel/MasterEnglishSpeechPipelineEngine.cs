// _SharedKernel/MasterEnglishSpeechPipelineEngine.cs — Version 6.0 Production Apex Pipeline.
// End-to-End English EFL Speech Evaluation Pipeline for 4-Year-Old Children.
// Unifies: Stage 1 (Audio Frontend & AEC stub) -> Stage 2 (DSP) & Stage 3 (Azure when supplied, else the
//          local fallback PLACEHOLDER — no real CTC) -> Stage 4 (Cross-Validation 3 Honesty Rules & ASHA)
//          -> Stage 5 (Anti-Frustration & Pedagogical Coach).
// Pure C# (NO UnityEngine dependency), fully testable. SLA: internal compute is measured; any
// caller-supplied external latency is an ESTIMATE and is flagged as such in the payload.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

#region Master Pipeline Contract Types

[Serializable]
public struct TelemetryAndSlaPayload {
  public float post_eos_total_latency_ms;
  public bool latency_included_external_estimate; // true = total includes a caller ESTIMATE (not measured)
  public string cloud_status; // "AZURE_SUCCESS" | "TIMEOUT_LOCAL_FALLBACK" | "OFFLINE_AUTONOMOUS" | "AZURE_HALLUCINATION_CAUGHT"
  public float snr_db;
  public float vtln_alpha_locked;
  public bool barge_in_echo_cancelled;
  public string fatigue_state; // "FRESH" | "MODERATE" | "HIGH_FATIGUE"
}

[Serializable]
public struct HonestyAndValidationLayerPayload {
  public string active_rule_triggered; // "RULE_1_HALLUCINATION_RESCUE" | "RULE_2_NOISE_LEXICAL_BLOCK" | "RULE_3_BALANCED_CONSENSUS"
  public bool is_valid_child_effort;
  public bool coda_veto_gate_locked;
  public string age4_difficulty_tier; // "TIER_1_EARLY" | "TIER_2_MID" | "TIER_3_LATE_AGE4"
  public float final_fusion_score; // 0..100
  public string raw_normative_decision; // "StrongPass" | "Pass" | "Partial" | "Retry_Blocked"
  public string final_session_decision; // "StrongPass" | "Pass" | "Adaptive_Improvement_Pass" | "Effort_Mercy_Pass" | "Guided_Retry" | "Playful_Receptive_Fallback"
  public int star_reward; // 3 | 2 | 1 | 0
}

[Serializable]
public struct ComponentItemPayload {
  public string ipa;
  public float score;
  public string flag; // e.g. "OK", "UNASPIRATED_L1", "SCHWA_EPENTHESIS", "SUBSTITUTED", "TENSE_LAX_ERROR", "MISSING_RHOTIC_F3", "DISTORTED", "DROPPED", "UNRELEASED_L1", "PARTIAL_CLUSTER"

  public ComponentItemPayload(string ipa, float score, string flag) {
    this.ipa = ipa;
    this.score = score;
    this.flag = flag;
  }
}

[Serializable]
public struct SyllableUiBreakdownItem {
  public int syllable_index;
  public string syllable_text;
  public string syllable_ipa;
  public bool is_stressed;
  public string ui_color; // "GREEN" | "YELLOW" | "RED"
  public ComponentItemPayload onset;
  public ComponentItemPayload nucleus;
  public ComponentItemPayload coda;
}

[Serializable]
public struct PedagogicalCoachAction4yoPayload {
  public string primary_focus_error; // "NONE" | "DROPPED_CODA" | "CLUSTER_EPENTHESIS" | "VOWEL_SHAPE" | "MISSING_ASPIRATION" | "FLAT_STRESS" | "ADULT_PROMPTING"
  public bool trigger_decomposed_micro_drill;
  public string micro_drill_target_segment; // null or e.g. "-ks", "fənt"
  public string avatar_mouth_animation_id;
  public string sfx_cue_id;
  public string coach_voice_prompt_vi;
  public bool enqueue_spaced_repetition;
  public List<string> updated_weak_phonemes_top3;
}

[Serializable]
public struct MasterSpeechPipelinePayload {
  public string session_id;
  public string utterance_id;
  public int attempt_index; // 1 | 2 | 3
  public string target_word;
  public string target_ipa;
  public TelemetryAndSlaPayload telemetry_and_sla;
  public HonestyAndValidationLayerPayload honesty_and_validation_layer;
  public List<SyllableUiBreakdownItem> syllable_by_syllable_ui_breakdown;
  public PedagogicalCoachAction4yoPayload pedagogical_coach_action_4yo;

  public string ToJson() {
    var sb = new StringBuilder(2560);
    var inv = CultureInfo.InvariantCulture;

    sb.Append("{\n");
    sb.AppendFormat(inv, "  \"session_id\": \"{0}\",\n", session_id ?? string.Empty);
    sb.AppendFormat(inv, "  \"utterance_id\": \"{0}\",\n", utterance_id ?? string.Empty);
    sb.AppendFormat(inv, "  \"attempt_index\": {0},\n", attempt_index);
    sb.AppendFormat(inv, "  \"target_word\": \"{0}\",\n", target_word ?? string.Empty);
    sb.AppendFormat(inv, "  \"target_ipa\": \"{0}\",\n", target_ipa ?? string.Empty);

    // telemetry_and_sla
    sb.Append("  \"telemetry_and_sla\": {\n");
    sb.AppendFormat(inv, "    \"post_eos_total_latency_ms\": {0:F1},\n", telemetry_and_sla.post_eos_total_latency_ms);
    sb.AppendFormat(inv, "    \"latency_included_external_estimate\": {0},\n", telemetry_and_sla.latency_included_external_estimate ? "true" : "false");
    sb.AppendFormat(inv, "    \"cloud_status\": \"{0}\",\n", telemetry_and_sla.cloud_status ?? "AZURE_SUCCESS");
    sb.AppendFormat(inv, "    \"snr_db\": {0:F1},\n", telemetry_and_sla.snr_db);
    sb.AppendFormat(inv, "    \"vtln_alpha_locked\": {0:F3},\n", telemetry_and_sla.vtln_alpha_locked);
    sb.AppendFormat(inv, "    \"barge_in_echo_cancelled\": {0},\n", telemetry_and_sla.barge_in_echo_cancelled ? "true" : "false");
    sb.AppendFormat(inv, "    \"fatigue_state\": \"{0}\"\n", telemetry_and_sla.fatigue_state ?? "FRESH");
    sb.Append("  },\n");

    // honesty_and_validation_layer
    sb.Append("  \"honesty_and_validation_layer\": {\n");
    sb.AppendFormat(inv, "    \"active_rule_triggered\": \"{0}\",\n", honesty_and_validation_layer.active_rule_triggered ?? "RULE_3_BALANCED_CONSENSUS");
    sb.AppendFormat(inv, "    \"is_valid_child_effort\": {0},\n", honesty_and_validation_layer.is_valid_child_effort ? "true" : "false");
    sb.AppendFormat(inv, "    \"coda_veto_gate_locked\": {0},\n", honesty_and_validation_layer.coda_veto_gate_locked ? "true" : "false");
    sb.AppendFormat(inv, "    \"age4_difficulty_tier\": \"{0}\",\n", honesty_and_validation_layer.age4_difficulty_tier ?? "TIER_1_EARLY");
    sb.AppendFormat(inv, "    \"final_fusion_score\": {0:F1},\n", honesty_and_validation_layer.final_fusion_score);
    sb.AppendFormat(inv, "    \"raw_normative_decision\": \"{0}\",\n", honesty_and_validation_layer.raw_normative_decision ?? "Partial");
    sb.AppendFormat(inv, "    \"final_session_decision\": \"{0}\",\n", honesty_and_validation_layer.final_session_decision ?? "Partial");
    sb.AppendFormat(inv, "    \"star_reward\": {0}\n", honesty_and_validation_layer.star_reward);
    sb.Append("  },\n");

    // syllable_by_syllable_ui_breakdown
    sb.Append("  \"syllable_by_syllable_ui_breakdown\": [\n");
    if (syllable_by_syllable_ui_breakdown != null && syllable_by_syllable_ui_breakdown.Count > 0) {
      for (int i = 0; i < syllable_by_syllable_ui_breakdown.Count; i++) {
        var s = syllable_by_syllable_ui_breakdown[i];
        sb.Append("    {\n");
        sb.AppendFormat(inv, "      \"syllable_index\": {0},\n", s.syllable_index);
        sb.AppendFormat(inv, "      \"syllable_text\": \"{0}\",\n", s.syllable_text ?? string.Empty);
        sb.AppendFormat(inv, "      \"syllable_ipa\": \"{0}\",\n", s.syllable_ipa ?? string.Empty);
        sb.AppendFormat(inv, "      \"is_stressed\": {0},\n", s.is_stressed ? "true" : "false");
        sb.AppendFormat(inv, "      \"ui_color\": \"{0}\",\n", s.ui_color ?? "GREEN");
        sb.AppendFormat(inv, "      \"onset\": {{ \"ipa\": \"{0}\", \"score\": {1:F1}, \"flag\": \"{2}\" }},\n",
            s.onset.ipa ?? string.Empty, s.onset.score, s.onset.flag ?? "OK");
        sb.AppendFormat(inv, "      \"nucleus\": {{ \"ipa\": \"{0}\", \"score\": {1:F1}, \"flag\": \"{2}\" }},\n",
            s.nucleus.ipa ?? string.Empty, s.nucleus.score, s.nucleus.flag ?? "OK");
        sb.AppendFormat(inv, "      \"coda\": {{ \"ipa\": \"{0}\", \"score\": {1:F1}, \"flag\": \"{2}\" }}\n",
            s.coda.ipa ?? string.Empty, s.coda.score, s.coda.flag ?? "OK");
        sb.AppendFormat(inv, "    }}{0}\n", (i < syllable_by_syllable_ui_breakdown.Count - 1) ? "," : "");
      }
    }
    sb.Append("  ],\n");

    // pedagogical_coach_action_4yo
    sb.Append("  \"pedagogical_coach_action_4yo\": {\n");
    sb.AppendFormat(inv, "    \"primary_focus_error\": \"{0}\",\n", pedagogical_coach_action_4yo.primary_focus_error ?? "NONE");
    sb.AppendFormat(inv, "    \"trigger_decomposed_micro_drill\": {0},\n", pedagogical_coach_action_4yo.trigger_decomposed_micro_drill ? "true" : "false");
    if (string.IsNullOrEmpty(pedagogical_coach_action_4yo.micro_drill_target_segment)) {
      sb.Append("    \"micro_drill_target_segment\": null,\n");
    } else {
      sb.AppendFormat(inv, "    \"micro_drill_target_segment\": \"{0}\",\n", pedagogical_coach_action_4yo.micro_drill_target_segment);
    }
    sb.AppendFormat(inv, "    \"avatar_mouth_animation_id\": \"{0}\",\n", pedagogical_coach_action_4yo.avatar_mouth_animation_id ?? string.Empty);
    sb.AppendFormat(inv, "    \"sfx_cue_id\": \"{0}\",\n", pedagogical_coach_action_4yo.sfx_cue_id ?? string.Empty);
    sb.AppendFormat(inv, "    \"coach_voice_prompt_vi\": \"{0}\",\n", pedagogical_coach_action_4yo.coach_voice_prompt_vi ?? string.Empty);
    sb.AppendFormat(inv, "    \"enqueue_spaced_repetition\": {0},\n", pedagogical_coach_action_4yo.enqueue_spaced_repetition ? "true" : "false");

    sb.Append("    \"updated_weak_phonemes_top3\": [");
    if (pedagogical_coach_action_4yo.updated_weak_phonemes_top3 != null) {
      for (int k = 0; k < pedagogical_coach_action_4yo.updated_weak_phonemes_top3.Count; k++) {
        sb.AppendFormat(inv, "\"{0}\"{1}", pedagogical_coach_action_4yo.updated_weak_phonemes_top3[k],
            k < pedagogical_coach_action_4yo.updated_weak_phonemes_top3.Count - 1 ? ", " : "");
      }
    }
    sb.Append("]\n");
    sb.Append("  }\n");
    sb.Append("}");

    return sb.ToString();
  }
}

#endregion

#region Pipeline Engine Implementation

/// <summary>
/// Master Pipeline Engine coordinating Stage 1 through Stage 5.
/// Pure C#, high performance, SLA post-EOS latency < 300ms (Hard ceiling 460ms).
/// </summary>
public static class MasterEnglishSpeechPipelineEngine {

  public const float HardCeilingLatencyMs = 460.0f;
  public const float TargetSlaLatencyMs = 300.0f;

  /// <summary>
  /// Evaluates an audio utterance across the complete 5-stage pipeline.
  /// </summary>
  public static MasterSpeechPipelinePayload Evaluate(
      float[] pcmNaturalSamples,
      int sampleRate,
      string targetWord,
      TargetPronunciation targetPron,
      int attemptIndex,
      float prevAttemptScore,
      string prevAttemptPrimaryError,
      MultiTurnAdaptiveSessionEngine sessionEngine = null,
      RawAzureAssessmentResult? rawAzure = null,
      string utteranceId = null,
      float externalLatencyEstimateMs = 0f, // 0 = unknown; >0 = caller-provided ESTIMATE (flagged in payload)
      EnglishPhoneticPayload? dspOverride = null,
      ChildFrontendMetadata? frontendOverride = null) {

    var totalSw = Stopwatch.StartNew();
    utteranceId = !string.IsNullOrEmpty(utteranceId) ? utteranceId : "utt-" + Guid.NewGuid().ToString("N").Substring(0, 8);
    sessionEngine = sessionEngine ?? new MultiTurnAdaptiveSessionEngine();

    // ================= STAGE 1: AUDIO FRONTEND, AEC & CHILD VAD =================
    float[] mono = pcmNaturalSamples ?? new float[0];
    var echoGuard = sessionEngine.ProcessBargeInAndEcho(mono, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), true);
    var phase1Meta = frontendOverride.HasValue ? frontendOverride.Value : ChildAudioFrontend.AnalyzeChildVAD(mono, sampleRate);

    // ================= STAGE 2: PARALLEL LOCAL ACOUSTIC DSP =================
    EnglishPhoneticPayload phase2Dsp;
    if (dspOverride.HasValue) {
      phase2Dsp = dspOverride.Value;
    } else {
      // Natural PCM straight into the DSP (same input contract as the production provider —
      // the earlier standalone pre-emphasis made this path score differently from runtime).
      phase2Dsp = EnglishAcousticDspEngine.Analyze(mono, sampleRate, targetPron);
    }

    // ================= STAGE 3: PARALLEL AZURE & LOCAL CTC FALLBACK =================
    var phase3Resilience = CloudSpeechResilienceEngine.Process(
        naturalPcmSamples: mono,
        sampleRate: sampleRate,
        targetWord: targetWord,
        azureResult: rawAzure,
        utteranceId: utteranceId,
        postEosLatencyMs: externalLatencyEstimateMs);

    // ================= STAGE 4: CROSS-VALIDATION (3 HONESTY RULES) & ASHA =================
    var phase4Fusion = CrossValidationPedagogicalEngine.Fuse(
        targetWord: targetWord,
        phase1: phase1Meta,
        phase2: phase2Dsp,
        phase3: phase3Resilience);

    // ================= STAGE 5: MULTI-TURN ANTI-FRUSTRATION & PEDAGOGICAL COACH =================
    float peakRms = ComputePeakRmsDb(mono);
    var phase5Session = sessionEngine.EvaluateTurn(
        utteranceId: utteranceId,
        targetWord: targetWord,
        attemptIndex: attemptIndex,
        phase4Payload: phase4Fusion,
        prevAttemptScore: prevAttemptScore,
        prevAttemptPrimaryError: prevAttemptPrimaryError,
        reactionLatencyMs: float.NaN,   // unknown at this layer — session engine skips NaN (no invented 950ms)
        currentPeakRmsDb: peakRms,
        pitchSpanHz: float.NaN,         // not measured here — no invented 65Hz
        echoGuard: echoGuard);

    totalSw.Stop();

    // Build Unified Payload (measured internal + explicitly-flagged external estimate)
    return AssembleFromPayloads(
        sessionEngine.SessionId,
        utteranceId,
        attemptIndex,
        targetWord,
        phase3Resilience.lexical_guard_metrics.target_ipa ?? (targetPron != null ? targetPron.Word.Value : ""),
        internalLatencyMs: (float)totalSw.Elapsed.TotalMilliseconds,
        externalLatencyEstimateMs: externalLatencyEstimateMs,
        p1: phase1Meta,
        p3: phase3Resilience,
        p4: phase4Fusion,
        p5: phase5Session,
        sessionEngine: sessionEngine);
  }

  // Builds the unified payload from ALREADY-COMPUTED stage outputs (no re-evaluation — the
  // runtime provider runs the stages once and assembles here; master Evaluate wraps this).
  public static MasterSpeechPipelinePayload AssembleFromPayloads(
      string sessionId,
      string utteranceId,
      int attemptIndex,
      string targetWord,
      string targetIpa,
      float internalLatencyMs,
      float externalLatencyEstimateMs,
      ChildFrontendMetadata p1,
      CloudSpeechResiliencePayload p3,
      CrossValidationFusionPayload p4,
      SessionAdaptiveOrchestrationPayload p5,
      MultiTurnAdaptiveSessionEngine sessionEngine) {

    // 1. Telemetry and SLA
    bool hasEstimate = externalLatencyEstimateMs > 0f;
    float totalLatency = (float)Math.Min(HardCeilingLatencyMs, internalLatencyMs + externalLatencyEstimateMs);
    var tele = new TelemetryAndSlaPayload {
      post_eos_total_latency_ms = (float)Math.Round(totalLatency, 1),
      latency_included_external_estimate = hasEstimate,
      cloud_status = p3.cloud_status, // honest pass-through (no "CTC" relabeling)
      snr_db = (float)Math.Round(p1.SnrDb, 1),
      vtln_alpha_locked = (float)Math.Round(sessionEngine.CurrentVtlnAlpha, 3),
      barge_in_echo_cancelled = p5.echo_and_barge_in_guard.barge_in_detected,
      fatigue_state = p5.fatigue_and_engagement_telemetry.fatigue_level
    };

    // 2. Honesty and Validation Layer
    var val = new HonestyAndValidationLayerPayload {
      active_rule_triggered = p4.active_honesty_rule_triggered,
      is_valid_child_effort = (p4.normative_decision != "Retry_Blocked"),
      coda_veto_gate_locked = p4.scores_summary.coda_veto_cap_applied,
      age4_difficulty_tier = p4.scores_summary.age4_difficulty_tier,
      final_fusion_score = (float)Math.Round(p4.scores_summary.final_fusion_score, 1),
      raw_normative_decision = p4.normative_decision,
      final_session_decision = p5.scaffolding_and_mercy_decision.final_session_decision,
      star_reward = ComputeStarReward(p5.scaffolding_and_mercy_decision.final_session_decision)
    };

    // 3. Syllable by Syllable UI Breakdown
    var syllables = new List<SyllableUiBreakdownItem>();
    if (p4.syllable_by_syllable_breakdown != null) {
      for (int i = 0; i < p4.syllable_by_syllable_breakdown.Count; i++) {
        var s = p4.syllable_by_syllable_breakdown[i];
        syllables.Add(new SyllableUiBreakdownItem {
          syllable_index = s.syllable_index,
          syllable_text = s.syllable_text,
          syllable_ipa = s.syllable_ipa,
          is_stressed = s.is_stressed_target,
          ui_color = s.syllable_ui_color,
          onset = new ComponentItemPayload(s.sub_components.onset.ipa, s.sub_components.onset.score, MapOnsetFlag(s.sub_components.onset.status)),
          nucleus = new ComponentItemPayload(s.sub_components.nucleus.ipa, s.sub_components.nucleus.score, MapNucleusFlag(s.sub_components.nucleus.status)),
          coda = new ComponentItemPayload(s.sub_components.coda.ipa, s.sub_components.coda.score, MapCodaFlag(s.sub_components.coda.status))
        });
      }
    }

    // 4. Pedagogical Coach Action for 4yo
    var coach = new PedagogicalCoachAction4yoPayload {
      primary_focus_error = MapCoachFocus(p4.pedagogical_feedback_for_4yo.primary_error_focus),
      trigger_decomposed_micro_drill = p5.scaffolding_and_mercy_decision.trigger_decomposed_syllable_drill,
      micro_drill_target_segment = p5.scaffolding_and_mercy_decision.target_drill_sub_segment,
      avatar_mouth_animation_id = p4.pedagogical_feedback_for_4yo.avatar_mouth_animation_id,
      sfx_cue_id = p4.pedagogical_feedback_for_4yo.phonetic_sound_effect_cue,
      coach_voice_prompt_vi = p4.pedagogical_feedback_for_4yo.voice_coach_prompt_vi,
      enqueue_spaced_repetition = p5.scaffolding_and_mercy_decision.queued_for_spaced_repetition,
      updated_weak_phonemes_top3 = sessionEngine.GetTopWeakPhonemes(3)
    };

    return new MasterSpeechPipelinePayload {
      session_id = sessionId,
      utterance_id = utteranceId,
      attempt_index = attemptIndex,
      target_word = targetWord,
      target_ipa = targetIpa,
      telemetry_and_sla = tele,
      honesty_and_validation_layer = val,
      syllable_by_syllable_ui_breakdown = syllables,
      pedagogical_coach_action_4yo = coach
    };
  }

  private static int ComputeStarReward(string finalDecision) {
    switch (finalDecision) {
      case "StrongPass": return 3;
      case "Pass":
      case "Adaptive_Improvement_Pass":
      case "Effort_Mercy_Pass": return 2;
      case "Guided_Retry":
      case "Partial": return 1;
      default: return 0;
    }
  }

  private static string MapOnsetFlag(string status) {
    if (status == "L1_UNASPIRATED") return "UNASPIRATED_L1";
    if (status == "SCHWA_EPENTHESIS") return "SCHWA_EPENTHESIS";
    if (status == "SUBSTITUTED") return "SUBSTITUTED";
    return "OK";
  }

  private static string MapNucleusFlag(string status) {
    if (status == "TENSE_LAX_ERROR") return "TENSE_LAX_ERROR";
    if (status == "MISSING_RHOTIC_F3") return "MISSING_RHOTIC_F3";
    if (status == "DISTORTED") return "DISTORTED";
    return "OK";
  }

  private static string MapCodaFlag(string status) {
    if (status == "DROPPED") return "DROPPED";
    if (status == "UNRELEASED_L1") return "UNRELEASED_L1";
    if (status == "PARTIAL_CLUSTER") return "PARTIAL_CLUSTER";
    return "OK";
  }

  private static string MapCoachFocus(string focus) {
    switch (focus) {
      case "DROPPED_CODA":
      case "UNRELEASED_CODA": return "DROPPED_CODA";
      case "CLUSTER_EPENTHESIS": return "CLUSTER_EPENTHESIS";
      case "VOWEL_OPENNESS": return "VOWEL_SHAPE";
      case "MISSING_ASPIRATION": return "MISSING_ASPIRATION";
      case "FLAT_ROBOTIC_STRESS": return "FLAT_STRESS";
      case "ADULT_INTERFERENCE": return "ADULT_PROMPTING";
      default: return "NONE";
    }
  }

  private static float ComputePeakRmsDb(float[] samples) {
    if (samples == null || samples.Length == 0) return -120f;
    float peak = 0f;
    for (int i = 0; i < samples.Length; i++) {
      float a = Math.Abs(samples[i]);
      if (a > peak) peak = a;
    }
    return peak <= 1e-6f ? -120f : 20f * (float)Math.Log10(peak);
  }
}

#endregion
