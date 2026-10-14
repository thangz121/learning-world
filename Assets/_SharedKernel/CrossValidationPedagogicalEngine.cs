// _SharedKernel/CrossValidationPedagogicalEngine.cs — Phase 4.1 Cross-Validation & Pedagogical Engine.
// English EFL Max-Spec for 4-year-olds (ASHA Age-4 Norms & Three Honesty Rules).
// Pure C# (NO UnityEngine dependency, burst-only, EditMode/batchmode testable, <2ms latency).
//
// 1. STATE-MACHINE ARBITRATION FOR RULE 1 & RULE 2 (NO CONFLICTS):
//    - Rule 2 (Acoustic Noise & OOV Guard): Evaluated FIRST. Blocks fake scores when kid says wrong word or adult prompts.
//    - Rule 1 (Hallucination & Noise-Immunity Guard): Evaluated SECOND. Rescues kid's genuine effort if cloud fails in noisy room.
//
// 2. RULE 3: TOPOLOGY-AWARE BALANCED CONSENSUS & CODA VETO GATE:
//    - Closed Syllables: 45% Calibrated IPA + 35% Coda Integrity + 20% DSP Onset/Nucleus/Stress.
//    - Non-linear Coda Veto Gate: If coda dropped in DSP AND Azure coda < 35 -> hard cap at Partial (max 68.0).
//    - Open Syllables: 55% Calibrated IPA + 30% Nucleus Trajectory + 15% Prosody/Stress. Anti-insertion coda penalty (-15).
//
// 3. ASHA AGE-4 DEVELOPMENTAL THRESHOLDING & NORMATIVE DECISION:
//    - TIER_1_EARLY (Offset = 0), TIER_2_MID (Offset = +4), TIER_3_LATE_AGE4 (Offset = +8).
//    - StrongPass (>=82 + Coda>=75 + NoEpenthesis, 3 Stars), Pass (68..82, 2 Stars), Partial (35..68, 1 Star), Retry_Blocked (<35, 0 Stars).
//
// 4. SYLLABLE-BY-SYLLABLE DIAGNOSTIC & SINGLE-FOCUS PEDAGOGICAL FEEDBACK:
//    - Syllable breakdown with color coding (GREEN/YELLOW/RED).
//    - Single-Focus Pedagogical Coach prioritizing: Dropped Coda > Epenthesis > Vowel > Aspiration > Stress.
//    - Age-4 Onomatopoeia metaphors (snake hiss, drum pop, tongue peek, lion roar).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

#region Payload Structs

[Serializable]
public struct ScoresSummary {
  public float final_fusion_score; // 0-100
  public string age4_difficulty_tier; // "TIER_1_EARLY" | "TIER_2_MID" | "TIER_3_LATE_AGE4"
  public float age4_tolerance_boost_applied;
  public float ipa_phoneme_consensus_score;
  public float coda_integrity_consensus_score;
  public bool coda_veto_cap_applied;
}

[Serializable]
public struct ComponentStatus {
  public string ipa;
  public float score;
  public string status; // e.g. "GOOD", "L1_UNASPIRATED", "DROPPED", etc.

  public ComponentStatus(string ipa, float score, string status) {
    this.ipa = ipa;
    this.score = score;
    this.status = status;
  }
}

[Serializable]
public struct SyllableSubComponents {
  public ComponentStatus onset;
  public ComponentStatus nucleus;
  public ComponentStatus coda;
}

[Serializable]
public struct SyllableBreakdownItem {
  public int syllable_index;
  public string syllable_text;
  public string syllable_ipa;
  public bool is_stressed_target;
  public bool stress_achieved;
  public string syllable_ui_color; // "GREEN" | "YELLOW" | "RED"
  public SyllableSubComponents sub_components;
}

[Serializable]
public struct PedagogicalFeedbackFor4Yo {
  public string primary_error_focus; // "NONE" | "DROPPED_CODA" | "UNRELEASED_CODA" | "CLUSTER_EPENTHESIS" | "VOWEL_OPENNESS" | "MISSING_ASPIRATION" | "FLAT_ROBOTIC_STRESS" | "ADULT_INTERFERENCE"
  public string avatar_mouth_animation_id; // "CELEBRATE_3STAR" | "MOUTH_SMILE_HISS" | "MOUTH_POP_BURST" | "MOUTH_TONGUE_PEEK" | "MOUTH_WIDE_A" | "MOUTH_ROUND_R" | "HAND_BIG_SMALL_WAVE"
  public int? highlight_phoneme_index_on_ui;
  public string voice_coach_prompt_vi;
  public string phonetic_sound_effect_cue;
}

[Serializable]
public struct CrossValidationFusionPayload {
  public string utterance_id;
  public string target_word;
  public float total_pipeline_latency_ms;
  public string active_honesty_rule_triggered; // "RULE_1_HALLUCINATION_RESCUE" | "RULE_2_NOISE_LEXICAL_BLOCK" | "RULE_3_BALANCED_CONSENSUS"
  public string normative_decision; // "StrongPass" | "Pass" | "Partial" | "Retry_Blocked"
  public int star_reward; // 3 | 2 | 1 | 0
  public ScoresSummary scores_summary;
  public List<SyllableBreakdownItem> syllable_by_syllable_breakdown;
  public PedagogicalFeedbackFor4Yo pedagogical_feedback_for_4yo;

  public string ToJson() {
    var sb = new StringBuilder(2048);
    var inv = CultureInfo.InvariantCulture;

    sb.Append("{\n");
    sb.AppendFormat(inv, "  \"utterance_id\": \"{0}\",\n", utterance_id ?? string.Empty);
    sb.AppendFormat(inv, "  \"target_word\": \"{0}\",\n", target_word ?? string.Empty);
    sb.AppendFormat(inv, "  \"total_pipeline_latency_ms\": {0:F1},\n", total_pipeline_latency_ms);
    sb.AppendFormat(inv, "  \"active_honesty_rule_triggered\": \"{0}\",\n", active_honesty_rule_triggered ?? "RULE_3_BALANCED_CONSENSUS");
    sb.AppendFormat(inv, "  \"normative_decision\": \"{0}\",\n", normative_decision ?? "Partial");
    sb.AppendFormat(inv, "  \"star_reward\": {0},\n", star_reward);

    // scores_summary
    sb.Append("  \"scores_summary\": {\n");
    sb.AppendFormat(inv, "    \"final_fusion_score\": {0:F1},\n", scores_summary.final_fusion_score);
    sb.AppendFormat(inv, "    \"age4_difficulty_tier\": \"{0}\",\n", scores_summary.age4_difficulty_tier ?? "TIER_1_EARLY");
    sb.AppendFormat(inv, "    \"age4_tolerance_boost_applied\": {0:F1},\n", scores_summary.age4_tolerance_boost_applied);
    sb.AppendFormat(inv, "    \"ipa_phoneme_consensus_score\": {0:F1},\n", scores_summary.ipa_phoneme_consensus_score);
    sb.AppendFormat(inv, "    \"coda_integrity_consensus_score\": {0:F1},\n", scores_summary.coda_integrity_consensus_score);
    sb.AppendFormat(inv, "    \"coda_veto_cap_applied\": {0}\n", scores_summary.coda_veto_cap_applied ? "true" : "false");
    sb.Append("  },\n");

    // syllable_by_syllable_breakdown
    sb.Append("  \"syllable_by_syllable_breakdown\": [\n");
    if (syllable_by_syllable_breakdown != null && syllable_by_syllable_breakdown.Count > 0) {
      for (int i = 0; i < syllable_by_syllable_breakdown.Count; i++) {
        var s = syllable_by_syllable_breakdown[i];
        sb.Append("    {\n");
        sb.AppendFormat(inv, "      \"syllable_index\": {0},\n", s.syllable_index);
        sb.AppendFormat(inv, "      \"syllable_text\": \"{0}\",\n", s.syllable_text ?? string.Empty);
        sb.AppendFormat(inv, "      \"syllable_ipa\": \"{0}\",\n", s.syllable_ipa ?? string.Empty);
        sb.AppendFormat(inv, "      \"is_stressed_target\": {0},\n", s.is_stressed_target ? "true" : "false");
        sb.AppendFormat(inv, "      \"stress_achieved\": {0},\n", s.stress_achieved ? "true" : "false");
        sb.AppendFormat(inv, "      \"syllable_ui_color\": \"{0}\",\n", s.syllable_ui_color ?? "GREEN");
        sb.Append("      \"sub_components\": {\n");
        sb.AppendFormat(inv, "        \"onset\": {{ \"ipa\": \"{0}\", \"score\": {1:F1}, \"status\": \"{2}\" }},\n",
            s.sub_components.onset.ipa ?? string.Empty, s.sub_components.onset.score, s.sub_components.onset.status ?? "GOOD");
        sb.AppendFormat(inv, "        \"nucleus\": {{ \"ipa\": \"{0}\", \"score\": {1:F1}, \"status\": \"{2}\" }},\n",
            s.sub_components.nucleus.ipa ?? string.Empty, s.sub_components.nucleus.score, s.sub_components.nucleus.status ?? "GOOD");
        sb.AppendFormat(inv, "        \"coda\": {{ \"ipa\": \"{0}\", \"score\": {1:F1}, \"status\": \"{2}\" }}\n",
            s.sub_components.coda.ipa ?? string.Empty, s.sub_components.coda.score, s.sub_components.coda.status ?? "GOOD");
        sb.Append("      }\n");
        sb.AppendFormat(inv, "    }}{0}\n", (i < syllable_by_syllable_breakdown.Count - 1) ? "," : "");
      }
    }
    sb.Append("  ],\n");

    // pedagogical_feedback_for_4yo
    sb.Append("  \"pedagogical_feedback_for_4yo\": {\n");
    sb.AppendFormat(inv, "    \"primary_error_focus\": \"{0}\",\n", pedagogical_feedback_for_4yo.primary_error_focus ?? "NONE");
    sb.AppendFormat(inv, "    \"avatar_mouth_animation_id\": \"{0}\",\n", pedagogical_feedback_for_4yo.avatar_mouth_animation_id ?? "CELEBRATE_3STAR");
    if (pedagogical_feedback_for_4yo.highlight_phoneme_index_on_ui.HasValue) {
      sb.AppendFormat(inv, "    \"highlight_phoneme_index_on_ui\": {0},\n", pedagogical_feedback_for_4yo.highlight_phoneme_index_on_ui.Value);
    } else {
      sb.Append("    \"highlight_phoneme_index_on_ui\": null,\n");
    }
    sb.AppendFormat(inv, "    \"voice_coach_prompt_vi\": \"{0}\",\n", pedagogical_feedback_for_4yo.voice_coach_prompt_vi ?? string.Empty);
    sb.AppendFormat(inv, "    \"phonetic_sound_effect_cue\": \"{0}\"\n", pedagogical_feedback_for_4yo.phonetic_sound_effect_cue ?? string.Empty);
    sb.Append("  }\n");

    sb.Append("}");
    return sb.ToString();
  }
}

#endregion

/// <summary>
/// Phase 4.1 Cross-Validation & Pedagogical Engine.
/// Asynchronously fuses Phase 1.2 (VAD), Phase 2.2 (Local DSP), and Phase 3.1 (Cloud/Resilience) in <2ms.
/// </summary>
public static class CrossValidationPedagogicalEngine {

  public static CrossValidationFusionPayload Fuse(
      string targetWord,
      ChildFrontendMetadata phase1,
      EnglishPhoneticPayload phase2,
      CloudSpeechResiliencePayload phase3,
      // HONEST DEFAULT (2026-10-14): 0.0 = "unknown". The local fallback is a
      // heuristic placeholder (returns an EMPTY transcript; no real CTC model
      // yet), so Rule 1 rescue must NEVER trigger from a fabricated default.
      // Callers may pass a REAL local confidence once an on-device model lands.
      float localCtcPhoneticSimilarity = 0.0f) {

    var sw = Stopwatch.StartNew();
    string uid = !string.IsNullOrEmpty(phase3.utterance_id)
        ? phase3.utterance_id
        : (!string.IsNullOrEmpty(phase2.utterance_id) ? phase2.utterance_id : Guid.NewGuid().ToString("D"));

    string word = !string.IsNullOrEmpty(targetWord) ? targetWord.Trim().ToLowerInvariant() : "apple";
    string targetIpa = CloudSpeechResilienceEngine.GetTargetIpa(word);

    // Syllables and Coda topology check
    bool isClosedSyllable = HasCoda(word, phase2);
    string age4Tier = DetermineAge4Tier(targetIpa);
    float tierOffset = GetTierOffset(age4Tier);

    var payload = new CrossValidationFusionPayload {
      utterance_id = uid,
      target_word = word,
      syllable_by_syllable_breakdown = new List<SyllableBreakdownItem>()
    };

    // =========================================================================
    // 1. STATE-MACHINE ARBITRATION (RULE 2 BEFORE RULE 1)
    // =========================================================================

    // QUY TẮC 2: ACOUSTIC NOISE & OUT-OF-VOCABULARY GUARD (Chạy kiểm tra trước)
    bool isAdultInterference = phase1.PromptingDetected ||
        phase1.VadState == ChildAudioFrontend.VadStateDroppedAdult ||
        phase1.FineGrainedVadState == "BLOCKED_ADULT";

    bool isOovNoise = phase3.lexical_guard_metrics.is_out_of_vocabulary_noise &&
                      !phase3.lexical_guard_metrics.l1_or_developmental_substitution &&
                      !phase3.lexical_guard_metrics.homophone_normalized_match;

    if (isOovNoise || isAdultInterference) {
      // RULE 2 TRIGGERED: Khóa cứng điểm âm học về 0, yêu cầu bé nói lại
      payload.active_honesty_rule_triggered = "RULE_2_NOISE_LEXICAL_BLOCK";
      payload.normative_decision = "Retry_Blocked";
      payload.star_reward = 0;

      payload.scores_summary = new ScoresSummary {
        final_fusion_score = 0f,
        age4_difficulty_tier = age4Tier,
        age4_tolerance_boost_applied = 0f,
        ipa_phoneme_consensus_score = 0f,
        coda_integrity_consensus_score = 0f,
        coda_veto_cap_applied = false
      };

      payload.pedagogical_feedback_for_4yo = new PedagogicalFeedbackFor4Yo {
        primary_error_focus = isAdultInterference ? "ADULT_INTERFERENCE" : "NONE",
        avatar_mouth_animation_id = "HAND_BIG_SMALL_WAVE",
        highlight_phoneme_index_on_ui = null,
        voice_coach_prompt_vi = isAdultInterference ? "Bé tự mình thử nói nhé!" : "Bé hãy nói lại từ này nhé!",
        phonetic_sound_effect_cue = "sfx_gentle_bell.wav"
      };

      PopulateSyllableBreakdown(payload.syllable_by_syllable_breakdown, word, targetIpa, 0f, 0f, 0f, "RED", "SUBSTITUTED", "DISTORTED", "DROPPED");
      sw.Stop();
      payload.total_pipeline_latency_ms = (float)sw.Elapsed.TotalMilliseconds;
      return payload;
    }

    // QUY TẮC 1: HALLUCINATION & NOISE-IMMUNITY GUARD (Bảo vệ nỗ lực thật khi Cloud nghe nhầm)
    bool cloudLowInNoise = (float.IsNaN(phase3.azure_calibrated_scores.overall_accuracy_dialect_adjusted) ||
                            phase3.azure_calibrated_scores.overall_accuracy_dialect_adjusted < 45f ||
                            phase3.cloud_status == "TIMEOUT_LOCAL_FALLBACK")
                           && (phase1.SnrDb < 14f);

    bool localEngineConfident = localCtcPhoneticSimilarity >= 0.70f && phase2.overall_acoustic_score >= 72f;

    bool rule1Triggered = cloudLowInNoise && localEngineConfident;

    // =========================================================================
    // 2. TOPOLOGY-AWARE BALANCED CONSENSUS & CODA VETO GATE
    // =========================================================================

    float cloudScore = !float.IsNaN(phase3.azure_calibrated_scores.overall_accuracy_dialect_adjusted)
        ? phase3.azure_calibrated_scores.overall_accuracy_dialect_adjusted
        : phase2.overall_acoustic_score;

    float dspScore = phase2.overall_acoustic_score;
    float codaDspScore = phase2.english_phonetic_metrics.coda_and_clusters.score;
    float onsetDspScore = phase2.english_phonetic_metrics.onset_and_clusters.score;
    float nucDspScore = phase2.english_phonetic_metrics.nucleus_and_rhoticity.score;
    float prosodyScore = phase2.english_phonetic_metrics.prosody_and_stress.score;

    // Coda Integrity Score (Azure Coda IPA + Local DSP Coda)
    float azureCodaScore = cloudScore;
    if (phase3.ipa_phoneme_breakdown != null) {
      for (int i = 0; i < phase3.ipa_phoneme_breakdown.Count; i++) {
        if (phase3.ipa_phoneme_breakdown[i].position == "CODA") {
          azureCodaScore = phase3.ipa_phoneme_breakdown[i].calibrated_score;
          break;
        }
      }
    }
    float codaIntegrityScore = isClosedSyllable ? (0.50f * azureCodaScore + 0.50f * codaDspScore) : 100f;

    float rawFusionScore;

    if (rule1Triggered) {
      payload.active_honesty_rule_triggered = "RULE_1_HALLUCINATION_RESCUE";
      // Weight 85% Local DSP + 15% Cloud
      rawFusionScore = 0.85f * dspScore + 0.15f * cloudScore;
    } else {
      payload.active_honesty_rule_triggered = "RULE_3_BALANCED_CONSENSUS";
      if (isClosedSyllable) {
        // Closed syllables: 45% Calibrated IPA + 35% Coda Integrity + 20% DSP Onset/Nucleus/Stress
        float onsetNucleusStressDsp = (onsetDspScore * 0.4f + nucDspScore * 0.4f + prosodyScore * 0.2f);
        rawFusionScore = (0.45f * cloudScore) + (0.35f * codaIntegrityScore) + (0.20f * onsetNucleusStressDsp);
      } else {
        // Open syllables: 55% Calibrated IPA + 30% Nucleus Trajectory DSP + 15% Prosody/Stress
        rawFusionScore = (0.55f * cloudScore) + (0.30f * nucDspScore) + (0.15f * prosodyScore);
        // Anti-insertion coda guard: if target has no coda but child inserted stop/sibilant
        if (phase2.english_phonetic_metrics.coda_and_clusters.high_freq_fricative_energy_ok ||
            phase2.english_phonetic_metrics.coda_and_clusters.release_burst_detected) {
          rawFusionScore = Math.Max(0f, rawFusionScore - 15f);
        }
      }
    }

    // NON-LINEAR CODA VETO GATE:
    // If coda is dropped in DSP AND Azure Coda < 35 -> hard cap at Partial (max 68.0)
    bool codaVetoCapApplied = false;
    if (isClosedSyllable && phase2.english_phonetic_metrics.coda_and_clusters.is_dropped && azureCodaScore < 35f) {
      codaVetoCapApplied = true;
      if (rawFusionScore > 68f) {
        rawFusionScore = 68f;
      }
    }

    // ASHA Age-4 Tolerance Boost
    float finalScoreWithBoost = Math.Min(100f, rawFusionScore + tierOffset);

    // =========================================================================
    // 3. NORMATIVE DECISION MAPPING (ASHA AGE-4 NORMS)
    // =========================================================================

    string decision;
    int stars;

    bool noClusterEpenthesis = !phase2.english_phonetic_metrics.onset_and_clusters.schwa_epenthesis_detected;
    bool codaNotDropped = !phase2.english_phonetic_metrics.coda_and_clusters.is_dropped || !isClosedSyllable;

    if (finalScoreWithBoost >= 82f && codaIntegrityScore >= 75f && noClusterEpenthesis && !codaVetoCapApplied) {
      decision = "StrongPass";
      stars = 3;
    } else if (finalScoreWithBoost >= 68f && codaNotDropped && !codaVetoCapApplied) {
      decision = "Pass";
      stars = 2;
    } else if (finalScoreWithBoost >= 35f || codaVetoCapApplied || phase3.lexical_guard_metrics.l1_or_developmental_substitution) {
      decision = "Partial";
      stars = 1;
    } else {
      decision = "Retry_Blocked";
      stars = 0;
    }

    payload.normative_decision = decision;
    payload.star_reward = stars;

    payload.scores_summary = new ScoresSummary {
      final_fusion_score = (float)Math.Round(finalScoreWithBoost, 1),
      age4_difficulty_tier = age4Tier,
      age4_tolerance_boost_applied = tierOffset,
      ipa_phoneme_consensus_score = (float)Math.Round(cloudScore, 1),
      coda_integrity_consensus_score = (float)Math.Round(codaIntegrityScore, 1),
      coda_veto_cap_applied = codaVetoCapApplied
    };

    // =========================================================================
    // 4. SYLLABLE BREAKDOWN & SINGLE-FOCUS PEDAGOGICAL COACH
    // =========================================================================

    string uiColor = stars >= 2 ? "GREEN" : (stars == 1 ? "YELLOW" : "RED");
    string onsetStatus = phase2.english_phonetic_metrics.onset_and_clusters.schwa_epenthesis_detected
        ? "CLUSTER_EPENTHESIS"
        : (!phase2.english_phonetic_metrics.onset_and_clusters.aspiration_passed ? "L1_UNASPIRATED" : "GOOD");
    string nucStatus = "GOOD";
    string codaStatus = phase2.english_phonetic_metrics.coda_and_clusters.is_dropped
        ? "DROPPED"
        : (!phase2.english_phonetic_metrics.coda_and_clusters.release_burst_detected ? "UNRELEASED_VIETNAMESE_STYLE" : "GOOD");

    PopulateSyllableBreakdown(payload.syllable_by_syllable_breakdown, word, targetIpa, onsetDspScore, nucDspScore, codaDspScore, uiColor, onsetStatus, nucStatus, codaStatus);

    payload.pedagogical_feedback_for_4yo = SelectSingleFocusCoach(
        word, targetIpa, decision, phase2, phase3, isClosedSyllable);

    sw.Stop();
    payload.total_pipeline_latency_ms = (float)sw.Elapsed.TotalMilliseconds;
    return payload;
  }

  // =========================================================================
  // HELPER METHODS: SYLLABLE DECOMPOSITION & COACH SELECTION
  // =========================================================================

  private static bool HasCoda(string word, EnglishPhoneticPayload phase2) {
    if (phase2.english_phonetic_metrics.coda_and_clusters.coda_type != null &&
        phase2.english_phonetic_metrics.coda_and_clusters.coda_type != "NONE") {
      return true;
    }
    string w = word.ToLowerInvariant();
    return w == "cat" || w == "dog" || w == "six" || w == "fish" || w == "bird" ||
           w == "ball" || w == "apple" || w == "rabbit" || w == "sun" || w == "red";
  }

  private static string DetermineAge4Tier(string ipa) {
    string norm = CloudSpeechResilienceEngine.NormalizeIpa(ipa);
    // Tier 3: /θ, ð, r, ʒ/ and clusters (st, sk, str, spl, ks, ts)
    if (norm.Contains("θ") || norm.Contains("ð") || norm.Contains("r") ||
        norm.Contains("st") || norm.Contains("ks") || norm.Contains("bl")) {
      return "TIER_3_LATE_AGE4";
    }
    // Tier 2: /f, v, s, z, ʃ, tʃ, dʒ, l/ and diphthongs
    if (norm.Contains("f") || norm.Contains("v") || norm.Contains("s") || norm.Contains("z") ||
        norm.Contains("ʃ") || norm.Contains("tʃ") || norm.Contains("dʒ") || norm.Contains("l") ||
        norm.Contains("aɪ") || norm.Contains("eɪ") || norm.Contains("ɔɪ") || norm.Contains("aʊ") || norm.Contains("oʊ")) {
      return "TIER_2_MID";
    }
    // Tier 1: Early plosives /m, b, p, d, t, k, g, n, w, h/ + simple vowels
    return "TIER_1_EARLY";
  }

  private static float GetTierOffset(string tier) {
    switch (tier) {
      case "TIER_3_LATE_AGE4": return 8.0f;
      case "TIER_2_MID": return 4.0f;
      default: return 0.0f;
    }
  }

  private static void PopulateSyllableBreakdown(
      List<SyllableBreakdownItem> list,
      string word,
      string ipa,
      float onsetScore,
      float nucScore,
      float codaScore,
      string uiColor,
      string onsetStatus,
      string nucStatus,
      string codaStatus) {

    if (word == "apple") {
      list.Add(new SyllableBreakdownItem {
        syllable_index = 0,
        syllable_text = "Ap",
        syllable_ipa = "æp",
        is_stressed_target = true,
        stress_achieved = true,
        syllable_ui_color = uiColor,
        sub_components = new SyllableSubComponents {
          onset = new ComponentStatus("", 100f, "GOOD"),
          nucleus = new ComponentStatus("æ", nucScore, nucStatus),
          coda = new ComponentStatus("p", codaScore, codaStatus)
        }
      });
      list.Add(new SyllableBreakdownItem {
        syllable_index = 1,
        syllable_text = "ple",
        syllable_ipa = "əl",
        is_stressed_target = false,
        stress_achieved = true,
        syllable_ui_color = uiColor,
        sub_components = new SyllableSubComponents {
          onset = new ComponentStatus("p", onsetScore, onsetStatus),
          nucleus = new ComponentStatus("ə", nucScore, "GOOD"),
          coda = new ComponentStatus("l", 85f, "GOOD")
        }
      });
    } else {
      list.Add(new SyllableBreakdownItem {
        syllable_index = 0,
        syllable_text = word,
        syllable_ipa = ipa,
        is_stressed_target = true,
        stress_achieved = true,
        syllable_ui_color = uiColor,
        sub_components = new SyllableSubComponents {
          onset = new ComponentStatus(word.Length > 0 ? word.Substring(0, 1) : "", onsetScore, onsetStatus),
          nucleus = new ComponentStatus("vowel", nucScore, nucStatus),
          coda = new ComponentStatus("coda", codaScore, codaStatus)
        }
      });
    }
  }

  /// <summary>
  /// Single-Focus Pedagogical Coach: selects exactly ONE highest priority error to guide a 4yo child.
  /// Priority: [1. Dropped Coda] -> [2. Epenthesis Cluster] -> [3. Vowel Openness] -> [4. Onset Aspiration] -> [5. Stress]
  /// </summary>
  private static PedagogicalFeedbackFor4Yo SelectSingleFocusCoach(
      string word,
      string targetIpa,
      string decision,
      EnglishPhoneticPayload phase2,
      CloudSpeechResiliencePayload phase3,
      bool isClosedSyllable) {

    if (decision == "StrongPass") {
      return new PedagogicalFeedbackFor4Yo {
        primary_error_focus = "NONE",
        avatar_mouth_animation_id = "CELEBRATE_3STAR",
        highlight_phoneme_index_on_ui = null,
        voice_coach_prompt_vi = "Bé nói xuất sắc lắm!",
        phonetic_sound_effect_cue = "sfx_fanfare_3star.wav"
      };
    }

    // 1. Nuốt âm cuối (Dropped Coda) - Priority 1
    if (isClosedSyllable && phase2.english_phonetic_metrics.coda_and_clusters.is_dropped) {
      string codaIpa = phase2.english_phonetic_metrics.coda_and_clusters.target_coda_ipa ?? "";
      if (codaIpa.Contains("s") || codaIpa.Contains("z") || word == "six" || word == "fish") {
        return new PedagogicalFeedbackFor4Yo {
          primary_error_focus = "DROPPED_CODA",
          avatar_mouth_animation_id = "MOUTH_SMILE_HISS",
          highlight_phoneme_index_on_ui = word.Length - 1,
          voice_coach_prompt_vi = "Bé xì tiếng rắn 'ssss' ở cuối nhé!",
          phonetic_sound_effect_cue = "sfx_snake_hiss.wav"
        };
      } else {
        return new PedagogicalFeedbackFor4Yo {
          primary_error_focus = "DROPPED_CODA",
          avatar_mouth_animation_id = "MOUTH_POP_BURST",
          highlight_phoneme_index_on_ui = word.Length - 1,
          voice_coach_prompt_vi = "Bé bật nhẹ tiếng trống ở cuối chữ nhé!",
          phonetic_sound_effect_cue = "sfx_drum_pop.wav"
        };
      }
    }

    // 2. Chèn âm "ờ" vào cụm phụ âm (Cluster Epenthesis) - Priority 2
    if (phase2.english_phonetic_metrics.onset_and_clusters.schwa_epenthesis_detected) {
      return new PedagogicalFeedbackFor4Yo {
        primary_error_focus = "CLUSTER_EPENTHESIS",
        avatar_mouth_animation_id = "MOUTH_SMILE_HISS",
        highlight_phoneme_index_on_ui = 0,
        voice_coach_prompt_vi = "Con đọc dứt khoát, không thêm chữ 'sờ' nhé!",
        phonetic_sound_effect_cue = "sfx_quick_slide.wav"
      };
    }

    // Special: Th-sound in "three"
    if (targetIpa.Contains("θ") && !phase3.lexical_guard_metrics.homophone_normalized_match) {
      return new PedagogicalFeedbackFor4Yo {
        primary_error_focus = "NONE",
        avatar_mouth_animation_id = "MOUTH_TONGUE_PEEK",
        highlight_phoneme_index_on_ui = 0,
        voice_coach_prompt_vi = "Bé kẹp nhẹ đầu lưỡi rồi thổi gió nhé!",
        phonetic_sound_effect_cue = "sfx_tongue_wind.wav"
      };
    }

    // 3. Sai độ mở nguyên âm chính (Vowel Openness) - Priority 3
    if (phase2.english_phonetic_metrics.nucleus_and_rhoticity.vowel_class == "LAX" || word == "cat") {
      if (phase2.english_phonetic_metrics.nucleus_and_rhoticity.score < 70f) {
        return new PedagogicalFeedbackFor4Yo {
          primary_error_focus = "VOWEL_OPENNESS",
          avatar_mouth_animation_id = "MOUTH_WIDE_A",
          highlight_phoneme_index_on_ui = 1,
          voice_coach_prompt_vi = "Bé mở to miệng như bạn sư tử gầm nhé!",
          phonetic_sound_effect_cue = "sfx_lion_roar.wav"
        };
      }
    }

    // 4. Thiếu bật hơi Onset (Missing Aspiration) - Priority 4
    if (!phase2.english_phonetic_metrics.onset_and_clusters.aspiration_passed) {
      return new PedagogicalFeedbackFor4Yo {
        primary_error_focus = "MISSING_ASPIRATION",
        avatar_mouth_animation_id = "MOUTH_POP_BURST",
        highlight_phoneme_index_on_ui = 0,
        voice_coach_prompt_vi = "Bé thổi hơi mạnh hơn ở đầu từ nhé!",
        phonetic_sound_effect_cue = "sfx_drum_pop.wav"
      };
    }

    // 5. Đọc ngang phè không trọng âm (Flat Robotic Stress) - Priority 5
    if (phase2.english_phonetic_metrics.prosody_and_stress.flat_robotic_stress_error) {
      return new PedagogicalFeedbackFor4Yo {
        primary_error_focus = "FLAT_ROBOTIC_STRESS",
        avatar_mouth_animation_id = "HAND_BIG_SMALL_WAVE",
        highlight_phoneme_index_on_ui = 0,
        voice_coach_prompt_vi = "Chữ đầu con đọc to và cao lên nhé!",
        phonetic_sound_effect_cue = "sfx_wave_up_down.wav"
      };
    }

    // Default supportive prompt
    return new PedagogicalFeedbackFor4Yo {
      primary_error_focus = "NONE",
      avatar_mouth_animation_id = "CELEBRATE_3STAR",
      highlight_phoneme_index_on_ui = null,
      voice_coach_prompt_vi = "Bé làm rất tốt, cố lên nhé!",
      phonetic_sound_effect_cue = "sfx_cheer.wav"
    };
  }
}
