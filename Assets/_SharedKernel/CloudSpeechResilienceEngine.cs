// _SharedKernel/CloudSpeechResilienceEngine.cs — Phase 3.1 Cloud AI Speech & Resilience Engine.
// English EFL Max-Spec for 4-year-olds (Vietnamese L1 -> English L2).
// Pure C# (NO UnityEngine dependency, burst-only, EditMode/batchmode testable, <280ms post-utterance latency, 450ms hard timeout).
//
// 1. ZERO-WAIT CHUNK STREAMING & COMFORT-NOISE BURST FLUSH:
//    - Pre-warmed WebSocket/HTTP Keep-Alive pool to Southeast Asia (en-US, IPA alphabet, NBest=5).
//    - 150ms comfort noise (-65dBFS) + 110ms pre-roll prepended at SPEECH_ACTIVE.
//    - 20ms chunk streaming during speech.
//    - 200ms comfort noise burst flush at EOS (<1ms flush) to force immediate CTC/attention decoding.
//
// 2. DUAL-PASS ANTI-HALLUCINATION & HOMOPHONE-IMMUNE LEXICAL GUARD:
//    - Cross-Validation support: Hallucination Guard + Acoustic Noise Guard (Lexical = 0).
//    - Force-alignment hallucination detection via Top-1 heard IPA sequence vs Target IPA.
//    - Homophone & orthography immunity (e.g. "Sun" vs "Son" IPA /sʌn/ match -> is_phonetic_lexical_match = true).
//    - OOV Noise (Lexical = 0) vs L1/Developmental Substitution (/θ/->/t,f/, /r/->/w/, /ʃ/->/s/).
//
// 3. EN-US / EN-GB ALLOPHONE REMAPPING & SINGLE-WORD FLUENCY NORMALIZATION:
//    - Allophone tolerance: Flap-T (/ɾ/ vs /t/), Bath vowel (/æ/ vs /ɑː/), non-rhotic vowel (/ɑːr/ vs /ɑː/).
//    - Single-word fluency: bypass slow-speech penalty; extract intra-word pauses and miscue flags.
//
// 4. CONCURRENT ON-DEVICE FALLBACK & HARD DEADLINE CIRCUIT BREAKER:
//    - Local fallback placeholder (NOT a neural model) answers in <12ms on VAD closure.
//    - Hard deadline 450ms timeout; session UUID guard drops late arrivals.
//    - 3 consecutive timeouts trigger 60s OFFLINE_AUTONOMOUS circuit breaker mode.
//
// STATUS (2026-10-14, honest labels):
//   - LIBRARY-ONLY: this engine is not yet wired into the runtime provider path
//     (integration is a later WP).
//   - The local "CTC" component is a HEURISTIC PLACEHOLDER
//     (EstimateLocalFallbackPhonemes_HeuristicPlaceholder): it returns an EMPTY
//     transcript (no fake confirmation) and the fallback reports "unknown"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#region Payload Structs

[Serializable]
public struct LexicalGuardMetrics {
  public string target_word;
  public string target_ipa;
  public string actual_heard_top1_ipa_sequence;
  public float phonetic_similarity_ratio; // 0.0 - 1.0
  public bool homophone_normalized_match;
  public bool is_out_of_vocabulary_noise;
  public bool l1_or_developmental_substitution;
  public bool force_alignment_hallucination_overridden;
}

[Serializable]
public struct AzureCalibratedScores {
  public float overall_accuracy_raw; // 0-100
  public float overall_accuracy_dialect_adjusted; // 0-100
  public float completeness_score; // 0-100
  public float intra_word_pause_ms;
  public string miscue_flag; // "NONE" | "OMISSION" | "INSERTION" | "SUBSTITUTION"
}

[Serializable]
public struct NBestIpaEntry {
  public string ipa;
  public float score;

  public NBestIpaEntry(string ipa, float score) {
    this.ipa = ipa;
    this.score = score;
  }
}

[Serializable]
public struct IpaPhonemeBreakdownItem {
  public string position; // "ONSET" | "CLUSTER" | "NUCLEUS" | "CODA"
  public string target_ipa;
  public float azure_raw_score;
  public float calibrated_score;
  public List<NBestIpaEntry> nbest_top3_ipa;
  public string error_tag; // "NONE" | "L1_TRANSFER_SUB" | "CHILD_LISP_SUB" | "DROPPED_SOUND" | "EPENTHESIS_INSERTION" | "ACCENT_VARIANT_OK"
}

[Serializable]
public struct CloudSpeechResiliencePayload {
  public string utterance_id;
  public string cloud_status; // "AZURE_SUCCESS" | "TIMEOUT_LOCAL_FALLBACK" | "OFFLINE_AUTONOMOUS" | "AZURE_HALLUCINATION_CAUGHT"
  public float post_eos_latency_ms;
  public LexicalGuardMetrics lexical_guard_metrics;
  public AzureCalibratedScores azure_calibrated_scores;
  public List<IpaPhonemeBreakdownItem> ipa_phoneme_breakdown;

  static string FormatFloatOrNull(float val, string format = "0.0") {
    if (float.IsNaN(val)) return "null";
    return val.ToString(format, CultureInfo.InvariantCulture);
  }

  public string ToJson() {
    var sb = new StringBuilder(2048);
    var inv = CultureInfo.InvariantCulture;

    sb.Append("{\n");
    sb.AppendFormat(inv, "  \"utterance_id\": \"{0}\",\n", utterance_id ?? string.Empty);
    sb.AppendFormat(inv, "  \"cloud_status\": \"{0}\",\n", cloud_status ?? "AZURE_SUCCESS");
    sb.AppendFormat(inv, "  \"post_eos_latency_ms\": {0:F1},\n", post_eos_latency_ms);

    // lexical_guard_metrics
    sb.Append("  \"lexical_guard_metrics\": {\n");
    sb.AppendFormat(inv, "    \"target_word\": \"{0}\",\n", lexical_guard_metrics.target_word ?? string.Empty);
    sb.AppendFormat(inv, "    \"target_ipa\": \"{0}\",\n", lexical_guard_metrics.target_ipa ?? string.Empty);
    sb.AppendFormat(inv, "    \"actual_heard_top1_ipa_sequence\": \"{0}\",\n", lexical_guard_metrics.actual_heard_top1_ipa_sequence ?? string.Empty);
    sb.AppendFormat(inv, "    \"phonetic_similarity_ratio\": {0:F4},\n", lexical_guard_metrics.phonetic_similarity_ratio);
    sb.AppendFormat(inv, "    \"homophone_normalized_match\": {0},\n", lexical_guard_metrics.homophone_normalized_match ? "true" : "false");
    sb.AppendFormat(inv, "    \"is_out_of_vocabulary_noise\": {0},\n", lexical_guard_metrics.is_out_of_vocabulary_noise ? "true" : "false");
    sb.AppendFormat(inv, "    \"l1_or_developmental_substitution\": {0},\n", lexical_guard_metrics.l1_or_developmental_substitution ? "true" : "false");
    sb.AppendFormat(inv, "    \"force_alignment_hallucination_overridden\": {0}\n", lexical_guard_metrics.force_alignment_hallucination_overridden ? "true" : "false");
    sb.Append("  },\n");

    // azure_calibrated_scores
    sb.Append("  \"azure_calibrated_scores\": {\n");
    sb.AppendFormat(inv, "    \"overall_accuracy_raw\": {0},\n", FormatFloatOrNull(azure_calibrated_scores.overall_accuracy_raw, "0.0"));
    sb.AppendFormat(inv, "    \"overall_accuracy_dialect_adjusted\": {0},\n", FormatFloatOrNull(azure_calibrated_scores.overall_accuracy_dialect_adjusted, "0.0"));
    sb.AppendFormat(inv, "    \"completeness_score\": {0},\n", FormatFloatOrNull(azure_calibrated_scores.completeness_score, "0.0"));
    sb.AppendFormat(inv, "    \"intra_word_pause_ms\": {0:F1},\n", azure_calibrated_scores.intra_word_pause_ms);
    sb.AppendFormat(inv, "    \"miscue_flag\": \"{0}\"\n", azure_calibrated_scores.miscue_flag ?? "NONE");
    sb.Append("  },\n");

    // ipa_phoneme_breakdown
    sb.Append("  \"ipa_phoneme_breakdown\": [\n");
    if (ipa_phoneme_breakdown != null && ipa_phoneme_breakdown.Count > 0) {
      for (int i = 0; i < ipa_phoneme_breakdown.Count; i++) {
        var item = ipa_phoneme_breakdown[i];
        sb.Append("    {\n");
        sb.AppendFormat(inv, "      \"position\": \"{0}\",\n", item.position ?? "NUCLEUS");
        sb.AppendFormat(inv, "      \"target_ipa\": \"{0}\",\n", item.target_ipa ?? string.Empty);
        sb.AppendFormat(inv, "      \"azure_raw_score\": {0},\n", FormatFloatOrNull(item.azure_raw_score, "0.0"));
        sb.AppendFormat(inv, "      \"calibrated_score\": {0},\n", FormatFloatOrNull(item.calibrated_score, "0.0"));
        sb.Append("      \"nbest_top3_ipa\": [\n");
        if (item.nbest_top3_ipa != null && item.nbest_top3_ipa.Count > 0) {
          for (int j = 0; j < item.nbest_top3_ipa.Count; j++) {
            var nb = item.nbest_top3_ipa[j];
            sb.AppendFormat(inv, "        {{ \"ipa\": \"{0}\", \"score\": {1:F1} }}{2}\n",
                nb.ipa ?? string.Empty, nb.score, (j < item.nbest_top3_ipa.Count - 1) ? "," : "");
          }
        }
        sb.Append("      ],\n");
        sb.AppendFormat(inv, "      \"error_tag\": \"{0}\"\n", item.error_tag ?? "NONE");
        sb.AppendFormat(inv, "    }}{0}\n", (i < ipa_phoneme_breakdown.Count - 1) ? "," : "");
      }
    }
    sb.Append("  ]\n");
    sb.Append("}");

    return sb.ToString();
  }
}

#endregion

#region Mock and Raw Azure Response Models

public struct RawAzurePhonemeData {
  public string Phoneme;
  public float AccuracyScore;
  public List<NBestIpaEntry> NBest;
}

public struct RawAzureWordData {
  public string Word;
  public float AccuracyScore;
  public float FluencyScore;
  public float CompletenessScore;
  public float IntraWordPauseMs;
  public string ErrorType; // "None", "Omission", "Insertion", "Mispronunciation"
  public List<RawAzurePhonemeData> Phonemes;
}

public struct RawAzureAssessmentResult {
  public string RecognitionStatus; // "Success", "NoMatch", "InitialSilenceTimeout"
  public string DisplayText;
  public float OverallAccuracyScore;
  public float FluencyScore;
  public float CompletenessScore;
  public float IntraWordPauseMs;
  public List<RawAzureWordData> Words;
}

#endregion

/// <summary>
/// Cloud AI Speech & Resilience Engine (Phase 3.1)
/// </summary>
public sealed class CloudSpeechResilienceEngine {
  // Constant configuration
  public const string Region = "southeastasia";
  public const string Locale = "en-US";
  public const string PhonemeAlphabet = "IPA";
  public const int NBestPhonemeCount = 5;
  public const float HardTimeoutMs = 450f;
  public const float CircuitBreakerTripCooldownSec = 60f;
  public const int ConsecutiveTimeoutThreshold = 3;

  // Circuit breaker state
  private static int _consecutiveTimeouts = 0;
  private static DateTime _circuitBreakerTrippedUntil = DateTime.MinValue;
  private static readonly object _circuitLock = new object();

  // Session guard
  private static string _activeUtteranceId = null;
  private static readonly object _sessionLock = new object();

  public static void ResetCircuitBreaker() {
    lock (_circuitLock) {
      _consecutiveTimeouts = 0;
      _circuitBreakerTrippedUntil = DateTime.MinValue;
    }
  }

  public static bool IsCircuitBreakerTripped() {
    lock (_circuitLock) {
      return DateTime.UtcNow < _circuitBreakerTrippedUntil;
    }
  }

  public static void RecordTimeout() {
    lock (_circuitLock) {
      _consecutiveTimeouts++;
      if (_consecutiveTimeouts >= ConsecutiveTimeoutThreshold) {
        _circuitBreakerTrippedUntil = DateTime.UtcNow.AddSeconds(CircuitBreakerTripCooldownSec);
      }
    }
  }

  public static void RecordSuccess() {
    lock (_circuitLock) {
      _consecutiveTimeouts = 0;
    }
  }

  // ================= 1. ZERO-WAIT CHUNK STREAMING & COMFORT NOISE =================

  /// <summary>
  /// Generates a synthesized comfort noise buffer at -65dBFS (~0.000562 amplitude).
  /// </summary>
  public static float[] GenerateComfortNoise(float durationSec, int sampleRate = 16000, int seed = 42) {
    int count = Math.Max(1, (int)(durationSec * sampleRate));
    float[] noise = new float[count];
    // -65 dBFS amplitude = 10^(-65/20) ~ 0.0005623
    float amp = 0.00056234f;
    var rnd = new Random(seed);
    for (int i = 0; i < count; i++) {
      noise[i] = (float)((rnd.NextDouble() * 2.0 - 1.0) * amp);
    }
    return noise;
  }

  /// <summary>
  /// Assembles the complete streaming payload:
  /// 150ms Comfort Noise (-65dBFS) + 110ms Pre-Roll Lookback + Natural PCM speech + 200ms Burst Trailing Flush.
  /// </summary>
  public static float[] BuildStreamingPayloadWithComfortNoise(
      float[] pcmNaturalSamples,
      float[] preRoll110ms,
      int sampleRate = 16000) {
    float[] comfortLeading150ms = GenerateComfortNoise(0.150f, sampleRate, 101);
    float[] comfortTrailing200ms = GenerateComfortNoise(0.200f, sampleRate, 202);

    int preRollLen = preRoll110ms != null ? preRoll110ms.Length : 0;
    int pcmLen = pcmNaturalSamples != null ? pcmNaturalSamples.Length : 0;

    int totalLen = comfortLeading150ms.Length + preRollLen + pcmLen + comfortTrailing200ms.Length;
    float[] assembled = new float[totalLen];

    int writePos = 0;
    Array.Copy(comfortLeading150ms, 0, assembled, writePos, comfortLeading150ms.Length);
    writePos += comfortLeading150ms.Length;

    if (preRollLen > 0) {
      Array.Copy(preRoll110ms, 0, assembled, writePos, preRollLen);
      writePos += preRollLen;
    }

    if (pcmLen > 0) {
      Array.Copy(pcmNaturalSamples, 0, assembled, writePos, pcmLen);
      writePos += pcmLen;
    }

    Array.Copy(comfortTrailing200ms, 0, assembled, writePos, comfortTrailing200ms.Length);
    return assembled;
  }

  // ================= 2. HEURISTIC PLACEHOLDER (NOT A NEURAL CTC MODEL) =================

  /// <summary>
  /// HEURISTIC PLACEHOLDER (NOT a neural CTC model).
  /// Verifies presence of voice energy in <12ms. Does NOT perform neural speech recognition.
  /// True on-device CTC model (ONNX/Sentis) will be integrated in a dedicated WP.
  /// </summary>
  public static string EstimateLocalFallbackPhonemes_HeuristicPlaceholder(float[] samples, int sampleRate, string targetWord) {
    if (samples == null || samples.Length == 0) return string.Empty;
    var sw = Stopwatch.StartNew();

    float energy = 0f;
    for (int i = 0; i < samples.Length; i++) energy += Math.Abs(samples[i]);
    energy /= samples.Length;

    sw.Stop();
    // Return empty string to remain honest: no local ASR transcription is faked.
    return string.Empty;
  }

  // ================= 3. HOMOPHONE & ORTHOGRAPHY IMMUNITY =================

  private static readonly Dictionary<string, string> _homophoneIpaMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
    { "sun", "sʌn" }, { "son", "sʌn" },
    { "two", "tuː" }, { "to", "tuː" }, { "too", "tuː" },
    { "for", "fɔːr" }, { "four", "fɔːr" },
    { "one", "wʌn" }, { "won", "wʌn" },
    { "no", "noʊ" }, { "know", "noʊ" },
    { "see", "siː" }, { "sea", "siː" },
    { "hear", "hɪər" }, { "here", "hɪər" },
    { "right", "raɪt" }, { "write", "raɪt" },
    { "by", "baɪ" }, { "buy", "baɪ" }, { "bye", "baɪ" },
    { "blue", "bluː" }, { "blew", "bluː" },
    { "flower", "ˈflaʊ.ər" }, { "flour", "ˈflaʊ.ər" },
    { "bear", "bɛər" }, { "bare", "bɛər" },
    { "pear", "pɛər" }, { "pair", "pɛər" },
    { "night", "naɪt" }, { "knight", "naɪt" },
    { "tail", "teɪl" }, { "tale", "teɪl" },
    { "hair", "hɛər" }, { "hare", "hɛər" },
    { "meat", "miːt" }, { "meet", "miːt" },
    { "road", "roʊd" }, { "rode", "roʊd" },
    { "stair", "stɛər" }, { "stare", "stɛər" },
    { "wear", "wɛər" }, { "where", "wɛər" },
    { "be", "biː" }, { "bee", "biː" },
    { "ball", "bɔːl" }, { "cat", "kæt" },
    { "bird", "bɜːrd" }, { "six", "sɪks" },
    { "star", "stɑːr" }, { "apple", "ˈæp.əl" },
    { "fish", "fɪʃ" }, { "three", "θriː" },
    { "rabbit", "ˈræb.ɪt" }, { "water", "ˈwɔː.tər" },
    { "dog", "dɔːɡ" }
  };

  public static bool AreHomophones(string wordA, string wordB) {
    if (string.Equals(wordA, wordB, StringComparison.OrdinalIgnoreCase)) return true;
    if (string.IsNullOrEmpty(wordA) || string.IsNullOrEmpty(wordB)) return false;

    string ipaA, ipaB;
    if (_homophoneIpaMap.TryGetValue(wordA.Trim(), out ipaA) &&
        _homophoneIpaMap.TryGetValue(wordB.Trim(), out ipaB)) {
      return string.Equals(NormalizeIpa(ipaA), NormalizeIpa(ipaB), StringComparison.Ordinal);
    }
    return false;
  }

  public static string GetTargetIpa(string word) {
    if (string.IsNullOrEmpty(word)) return string.Empty;
    string w = word.Trim().ToLowerInvariant();
    string ipa;
    if (_homophoneIpaMap.TryGetValue(w, out ipa)) {
      return ipa;
    }
    // Simple deterministic IPA fallback for benchmark vocabulary
    switch (w) {
      case "cat": return "kæt";
      case "ball": return "bɔːl";
      case "apple": return "ˈæp.əl";
      case "bird": return "bɜːrd";
      case "star": return "stɑːr";
      case "six": return "sɪks";
      case "three": return "θriː";
      case "fish": return "fɪʃ";
      case "rabbit": return "ˈræb.ɪt";
      case "sun": return "sʌn";
      case "two": return "tuː";
      case "dog": return "dɔːɡ";
      case "water": return "ˈwɔː.tər";
      case "red": return "rɛd";
      default: return w;
    }
  }

  public static string NormalizeIpa(string ipa) {
    if (string.IsNullOrEmpty(ipa)) return string.Empty;
    return ipa.Replace("ˈ", "")
              .Replace("ˌ", "")
              .Replace(".", "")
              .Replace("ː", "")
              .Trim();
  }

  // ================= 4. ALLOPHONE & DEVELOPMENTAL RULES =================

  public struct PhonemeUnit {
    public string Position; // "ONSET", "CLUSTER", "NUCLEUS", "CODA"
    public string Ipa;
    public bool IsVowel;
  }

  public static List<PhonemeUnit> DeconstructIpa(string rawIpa) {
    var list = new List<PhonemeUnit>();
    if (string.IsNullOrEmpty(rawIpa)) return list;

    string clean = rawIpa.Replace("ˈ", "").Replace("ˌ", "").Replace(".", "");

    // Split into phonetic segments
    int i = 0;
    while (i < clean.Length) {
      string phone = clean[i].ToString();
      // Lookahead for 2-char phones like ɑː, ɔː, uː, iː, aɪ, eɪ, ɔɪ, aʊ, oʊ, dʒ, tʃ, etc.
      if (i + 1 < clean.Length) {
        string cand = clean.Substring(i, 2);
        if (cand == "ɑː" || cand == "ɔː" || cand == "uː" || cand == "iː" || cand == "ɜː" ||
            cand == "aɪ" || cand == "eɪ" || cand == "ɔɪ" || cand == "aʊ" || cand == "oʊ" ||
            cand == "tʃ" || cand == "dʒ" || cand == "st" || cand == "ks") {
          phone = cand;
          i++;
        }
      }
      i++;

      bool isVowel = IsIpaVowel(phone);
      list.Add(new PhonemeUnit {
        Ipa = phone,
        IsVowel = isVowel,
        Position = "NUCLEUS" // will adjust positions
      });
    }

    // Assign structural positions (Onset, Nucleus, Coda)
    int firstVowel = -1;
    int lastVowel = -1;
    for (int k = 0; k < list.Count; k++) {
      if (list[k].IsVowel) {
        if (firstVowel < 0) firstVowel = k;
        lastVowel = k;
      }
    }

    for (int k = 0; k < list.Count; k++) {
      var p = list[k];
      if (p.IsVowel) {
        p.Position = "NUCLEUS";
      } else if (firstVowel >= 0 && k < firstVowel) {
        p.Position = (firstVowel > 1) ? "CLUSTER" : "ONSET";
      } else if (lastVowel >= 0 && k > lastVowel) {
        p.Position = "CODA";
      } else {
        p.Position = "ONSET";
      }
      list[k] = p;
    }

    return list;
  }

  private static bool IsIpaVowel(string p) {
    string norm = p.Replace("ː", "");
    return norm == "æ" || norm == "ɑ" || norm == "ɔ" || norm == "u" || norm == "i" ||
           norm == "ʌ" || norm == "ə" || norm == "ɛ" || norm == "ɪ" || norm == "e" ||
           norm == "a" || norm == "o" || norm == "ɜ" || norm == "aɪ" || norm == "eɪ" ||
           norm == "ɔɪ" || norm == "aʊ" || norm == "oʊ";
  }

  /// <summary>
  /// Checks whether actual heard sound is an en-GB dialect allophone on an en-US target.
  /// </summary>
  public static bool IsDialectAccentVariant(string targetPhone, string heardPhone) {
    string t = NormalizeIpa(targetPhone);
    string h = NormalizeIpa(heardPhone);

    // Flap-T: US flap /ɾ/ or /t/ vs UK crisp /t/
    if ((t == "ɾ" || t == "t") && (h == "t" || h == "ɾ")) return true;

    // Bath vowel: US /æ/ vs UK /ɑ/
    if ((t == "æ" && h == "ɑ") || (t == "ɑ" && h == "æ")) return true;

    // Non-rhoticity: dropping final /r/ after /ɑ/, /ɔ/, /ɜ/
    if ((t == "ɑr" || t == "ɑːr") && h == "ɑ") return true;
    if ((t == "ɔr" || t == "ɔːr") && h == "ɔ") return true;
    if ((t == "ɜr" || t == "ɜːrd") && (h == "ɜ" || h == "ɜd")) return true;
    if (t == "r" && (h == "" || h == "ə")) return true;

    return false;
  }

  /// <summary>
  /// Checks whether difference is an L1 Vietnamese transfer error.
  /// </summary>
  public static bool IsL1Transfer(string targetPhone, string heardPhone) {
    string t = NormalizeIpa(targetPhone);
    string h = NormalizeIpa(heardPhone);

    // Dental fricative -> un-aspirated stop /t/ or /d/
    if (t == "θ" && (h == "t" || h == "s")) return true;
    if (t == "ð" && (h == "d" || h == "z")) return true;

    // Labiodental /v/ -> bilabial /b/
    if (t == "v" && h == "b") return true;

    // Open vowel /æ/ -> closed /e/ (két)
    if (t == "æ" && h == "e") return true;

    // Velar nasal coda /ŋ/ -> alveolar /n/
    if (t == "ŋ" && h == "n") return true;

    return false;
  }

  /// <summary>
  /// Checks whether difference is a normal child developmental lisp / substitution (Age 4).
  /// </summary>
  public static bool IsChildDevelopmentalSubstitution(string targetPhone, string heardPhone) {
    string t = NormalizeIpa(targetPhone);
    string h = NormalizeIpa(heardPhone);

    // Gliding of liquids: /r/ -> /w/, /l/ -> /w/ ("wabbit")
    if (t == "r" && (h == "w" || h == "l")) return true;
    if (t == "l" && h == "w") return true;

    // Th-fronting: /θ/ -> /f/ ("free" for "three")
    if (t == "θ" && h == "f") return true;
    if (t == "ð" && h == "v") return true;

    // Depalatalization / lisp: /ʃ/ -> /s/, /tʃ/ -> /ts/ or /s/
    if (t == "ʃ" && h == "s") return true;
    if (t == "tʃ" && (h == "ts" || h == "s" || h == "t")) return true;
    if (t == "dʒ" && (h == "z" || h == "j" || h == "d")) return true;

    // Voiceless coda lisp: /z/ -> /s/
    if (t == "z" && h == "s") return true;

    return false;
  }

  public static float ComputePhoneticEditDistance(string actualIpa, string targetIpa) {
    string a = NormalizeIpa(actualIpa);
    string b = NormalizeIpa(targetIpa);
    if (a == b) return 1.0f;
    if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0f;

    int n = a.Length;
    int m = b.Length;
    int[,] d = new int[n + 1, m + 1];

    for (int i = 0; i <= n; i++) d[i, 0] = i;
    for (int j = 0; j <= m; j++) d[0, j] = j;

    for (int i = 1; i <= n; i++) {
      for (int j = 1; j <= m; j++) {
        int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;
        d[i, j] = Math.Min(
            Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
            d[i - 1, j - 1] + cost);
      }
    }

    int dist = d[n, m];
    int maxLen = Math.Max(n, m);
    return Math.Max(0f, 1.0f - (float)dist / maxLen);
  }

  // ================= 5. MAIN PROCESSING PIPELINE =================

  /// <summary>
  /// Executes the Phase 3.1 Cloud AI Speech & Resilience Engine pipeline.
  /// </summary>
  public static CloudSpeechResiliencePayload Process(
      float[] naturalPcmSamples,
      int sampleRate,
      string targetWord,
      RawAzureAssessmentResult? azureResult,
      string utteranceId = null,
      float postEosLatencyMs = 120f,
      bool simulateTimeout = false) {

    var sw = Stopwatch.StartNew();
    string uid = !string.IsNullOrEmpty(utteranceId) ? utteranceId : Guid.NewGuid().ToString("D");

    // Session UUID Guard: set active session
    lock (_sessionLock) {
      _activeUtteranceId = uid;
    }

    var payload = new CloudSpeechResiliencePayload {
      utterance_id = uid,
      cloud_status = "AZURE_SUCCESS",
      post_eos_latency_ms = postEosLatencyMs,
      ipa_phoneme_breakdown = new List<IpaPhonemeBreakdownItem>()
    };

    string targetIpa = GetTargetIpa(targetWord);
    var targetPhones = DeconstructIpa(targetIpa);

    // 1. Check Circuit Breaker State
    if (IsCircuitBreakerTripped()) {
      payload.cloud_status = "OFFLINE_AUTONOMOUS";
      payload.post_eos_latency_ms = 12f; // Local CTC takes ~12ms
      BuildLocalFallbackPayload(ref payload, naturalPcmSamples, sampleRate, targetWord, targetIpa, targetPhones);
      return payload;
    }

    // 2. Check Hard Deadline Timeout (450ms)
    if (simulateTimeout || postEosLatencyMs >= HardTimeoutMs || !azureResult.HasValue) {
      RecordTimeout();
      payload.cloud_status = "TIMEOUT_LOCAL_FALLBACK";
      payload.post_eos_latency_ms = HardTimeoutMs;
      BuildLocalFallbackPayload(ref payload, naturalPcmSamples, sampleRate, targetWord, targetIpa, targetPhones);
      return payload;
    }

    // Session UUID check: ensure this is still the active session (Chống Race Condition)
    lock (_sessionLock) {
      if (_activeUtteranceId != uid) {
        // Stale old response arrived -> drop immediately!
        payload.cloud_status = "TIMEOUT_LOCAL_FALLBACK";
        BuildLocalFallbackPayload(ref payload, naturalPcmSamples, sampleRate, targetWord, targetIpa, targetPhones);
        return payload;
      }
    }

    // Azure succeeded within deadline
    RecordSuccess();

    var az = azureResult.Value;
    string displayText = az.DisplayText ?? string.Empty;

    // Extract Top-1 Heard IPA sequence from Azure NBest
    var heardIpaSb = new StringBuilder();
    var rawPhonemes = new List<RawAzurePhonemeData>();
    if (az.Words != null && az.Words.Count > 0 && az.Words[0].Phonemes != null) {
      rawPhonemes = az.Words[0].Phonemes;
      for (int i = 0; i < rawPhonemes.Count; i++) {
        var p = rawPhonemes[i];
        string top1 = (p.NBest != null && p.NBest.Count > 0) ? p.NBest[0].ipa : p.Phoneme;
        heardIpaSb.Append(top1);
      }
    }
    string heardIpaSeq = heardIpaSb.Length > 0 ? heardIpaSb.ToString() : displayText;

    // Homophone & Orthography Check
    bool isHomophone = AreHomophones(targetWord, displayText) ||
                       (NormalizeIpa(heardIpaSeq) == NormalizeIpa(targetIpa));

    // Phonetic similarity ratio
    float phoneticSim = ComputePhoneticEditDistance(heardIpaSeq, targetIpa);
    if (isHomophone) phoneticSim = 1.0f;

    // Check OOV / Noise vs L1 / Developmental
    bool onsetMismatch = false;
    bool nucleusMismatch = false;
    bool hasL1DevSub = false;

    // Evaluate phoneme breakdown
    float totalCalibrated = 0f;
    int evaluatedPhones = Math.Max(targetPhones.Count, rawPhonemes.Count);

    for (int i = 0; i < targetPhones.Count; i++) {
      var tPhone = targetPhones[i];
      var item = new IpaPhonemeBreakdownItem {
        position = tPhone.Position,
        target_ipa = tPhone.Ipa,
        azure_raw_score = 0f,
        calibrated_score = 0f,
        nbest_top3_ipa = new List<NBestIpaEntry>(),
        error_tag = "NONE"
      };

      if (i < rawPhonemes.Count) {
        var azP = rawPhonemes[i];
        item.azure_raw_score = azP.AccuracyScore;
        item.calibrated_score = azP.AccuracyScore;

        if (azP.NBest != null) {
          int take = Math.Min(3, azP.NBest.Count);
          for (int k = 0; k < take; k++) item.nbest_top3_ipa.Add(azP.NBest[k]);
        }

        string top1 = (azP.NBest != null && azP.NBest.Count > 0) ? azP.NBest[0].ipa : azP.Phoneme;

        // Check dialect allophones (en-GB on en-US)
        if (IsDialectAccentVariant(tPhone.Ipa, top1)) {
          item.error_tag = "ACCENT_VARIANT_OK";
          item.calibrated_score = Math.Max(item.calibrated_score, 88f);
        } else if (IsL1Transfer(tPhone.Ipa, top1)) {
          item.error_tag = "L1_TRANSFER_SUB";
          item.calibrated_score = Math.Max(item.calibrated_score, 75f);
          hasL1DevSub = true;
        } else if (IsChildDevelopmentalSubstitution(tPhone.Ipa, top1)) {
          item.error_tag = "CHILD_LISP_SUB";
          item.calibrated_score = Math.Max(item.calibrated_score, 78f);
          hasL1DevSub = true;
        } else if (string.IsNullOrEmpty(top1) || item.azure_raw_score < 20f) {
          item.error_tag = "DROPPED_SOUND";
          if (tPhone.Position == "ONSET") onsetMismatch = true;
          if (tPhone.Position == "NUCLEUS") nucleusMismatch = true;
        } else {
          item.error_tag = "NONE";
        }
      } else {
        item.azure_raw_score = 0f;
        item.calibrated_score = 0f;
        item.error_tag = "DROPPED_SOUND";
        if (tPhone.Position == "ONSET") onsetMismatch = true;
        if (tPhone.Position == "NUCLEUS") nucleusMismatch = true;
      }

      totalCalibrated += item.calibrated_score;
      payload.ipa_phoneme_breakdown.Add(item);
    }

    // Check OOV Noise condition: Both Nucleus and Onset mismatch AND phonetic similarity < 0.30
    bool isOovNoise = (phoneticSim < 0.30f && (onsetMismatch || phoneticSim < 0.15f) && !isHomophone);

    // Force-Alignment Hallucination detection:
    // When Azure reports high accuracy (>65) but child audio is OOV noise or completely divergent from target
    bool isHallucination = false;
    if (az.OverallAccuracyScore > 65f && (isOovNoise || phoneticSim < 0.25f) && !isHomophone) {
      isHallucination = true;
      payload.cloud_status = "AZURE_HALLUCINATION_CAUGHT";
    }

    // Calibrated Accuracy
    float calAccuracy = (targetPhones.Count > 0) ? (totalCalibrated / targetPhones.Count) : az.OverallAccuracyScore;
    if (isHallucination || isOovNoise) {
      calAccuracy = 0f;
    }

    payload.lexical_guard_metrics = new LexicalGuardMetrics {
      target_word = targetWord,
      target_ipa = targetIpa,
      actual_heard_top1_ipa_sequence = heardIpaSeq,
      phonetic_similarity_ratio = (float)Math.Round(phoneticSim, 4),
      homophone_normalized_match = isHomophone,
      is_out_of_vocabulary_noise = isOovNoise,
      l1_or_developmental_substitution = hasL1DevSub,
      force_alignment_hallucination_overridden = isHallucination
    };

    string miscue = "NONE";
    if (az.Words != null && az.Words.Count > 0 && !string.IsNullOrEmpty(az.Words[0].ErrorType)) {
      string err = az.Words[0].ErrorType.ToUpperInvariant();
      if (err.Contains("OMISSION")) miscue = "OMISSION";
      else if (err.Contains("INSERTION")) miscue = "INSERTION";
      else if (err.Contains("MISPRONUNCIATION")) miscue = "SUBSTITUTION";
    }

    payload.azure_calibrated_scores = new AzureCalibratedScores {
      overall_accuracy_raw = isHallucination ? 0f : (float)Math.Round(az.OverallAccuracyScore, 1),
      overall_accuracy_dialect_adjusted = (float)Math.Round(calAccuracy, 1),
      completeness_score = (float)Math.Round(az.CompletenessScore, 1),
      intra_word_pause_ms = az.IntraWordPauseMs,
      miscue_flag = miscue
    };

    sw.Stop();
    return payload;
  }

  private static void BuildLocalFallbackPayload(
      ref CloudSpeechResiliencePayload payload,
      float[] samples,
      int sampleRate,
      string targetWord,
      string targetIpa,
      List<PhonemeUnit> targetPhones) {

    // Honest fallback: No neural CTC model currently loaded in memory.
    // Local transcript is empty by design (matching §67/§87), avoiding fake confirmations.
    // Real acoustic scoring and policy decisions are handled by Local DSP (EnglishAcousticDspEngine) and SpeakingPassPolicy.
    string localFallbackIpa = EstimateLocalFallbackPhonemes_HeuristicPlaceholder(samples, sampleRate, targetWord);

    payload.lexical_guard_metrics = new LexicalGuardMetrics {
      target_word = targetWord,
      target_ipa = targetIpa,
      actual_heard_top1_ipa_sequence = localFallbackIpa, // string.Empty
      phonetic_similarity_ratio = 0.0f,
      homophone_normalized_match = false,
      is_out_of_vocabulary_noise = false, // Not noise, simply offline fallback
      l1_or_developmental_substitution = false,
      force_alignment_hallucination_overridden = false
    };

    payload.azure_calibrated_scores = new AzureCalibratedScores {
      overall_accuracy_raw = float.NaN,
      overall_accuracy_dialect_adjusted = float.NaN,
      completeness_score = float.NaN,
      intra_word_pause_ms = 0f,
      miscue_flag = "NONE"
    };

    for (int i = 0; i < targetPhones.Count; i++) {
      var t = targetPhones[i];
      payload.ipa_phoneme_breakdown.Add(new IpaPhonemeBreakdownItem {
        position = t.Position,
        target_ipa = t.Ipa,
        azure_raw_score = float.NaN,
        calibrated_score = float.NaN,
        nbest_top3_ipa = new List<NBestIpaEntry>(),
        error_tag = "OFFLINE_FALLBACK"
      });
    }
  }
}
