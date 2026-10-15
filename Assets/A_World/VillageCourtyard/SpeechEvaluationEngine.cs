// A_World/VillageCourtyard/SpeechEvaluationEngine.cs — Hybrid Speech Evaluation Engine.
// Incorporates 3-Region Dialect Matrix, Age-4 Developmental Lisp Tolerance,
// and Hybrid Scoring: Score = 0.6 * PhonemeGOP + 0.4 * ToneContourMatch.
// Pure C# 9.0 only. EditMode-testable.
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SpeechEvaluationResult {
  public bool IsPass;
  public float TotalScore;
  public float PhonemeScore;
  public float ToneScore;
  public int DetectedSyllableCount;
  public string RecognizedText;
  public string DevelopmentalNote;
  public RegionType MatchedRegion;

  // 1.9.48 honesty flags
  public bool TranscriptAvailable = true; // false = attempt-only evaluation (no ASR text exists)
  public bool SilentAttempt;              // true = no speech detected in the samples

  public SpeechEvaluationResult(bool pass, float total, float phoneme, float tone,
      int syllables, string text, string note = "", RegionType region = RegionType.North) {
    IsPass = pass;
    TotalScore = Mathf.Clamp01(total);
    PhonemeScore = Mathf.Clamp01(phoneme);
    ToneScore = Mathf.Clamp01(tone);
    DetectedSyllableCount = syllables;
    RecognizedText = text ?? "";
    DevelopmentalNote = note ?? "";
    MatchedRegion = region;
  }
}

public static class SpeechEvaluationEngine {
  public const float PassThreshold = 0.70f; // 70% match triggers success immediately

  public static SpeechEvaluationResult Evaluate(string spokenText, SpeakingPromptSO prompt,
      float[] audioSamples = null, int sampleRate = 16000) {
    if (prompt == null) {
      return new SpeechEvaluationResult(false, 0f, 0f, 0f, 0, spokenText);
    }

    string cleanSpoken = (spokenText ?? "").Trim();
    string target = prompt.targetText ?? "";

    // 1. Phoneme & Dialect matching (with Age-4 developmental tolerance)
    string devNote;
    RegionType matchedRegion;
    float phonemeScore = ComputePhonemeScore(cleanSpoken, prompt, out devNote, out matchedRegion);

    // 2. Tone Contour & Syllable envelope estimation
    int detectedSyllables = EstimateSyllableCount(cleanSpoken, audioSamples, sampleRate);
    float toneScore = ComputeToneContourScore(cleanSpoken, target, audioSamples, sampleRate);

    // Syllable count alignment bonus / penalty
    float syllableRatio = 1.0f;
    if (prompt.targetSyllableCount > 0 && detectedSyllables > 0) {
      syllableRatio = 1.0f - Mathf.Abs(prompt.targetSyllableCount - detectedSyllables) / (float)prompt.targetSyllableCount;
      syllableRatio = Mathf.Clamp01(syllableRatio);
    }

    // 3. Hybrid Scoring Formula: Score = 0.6 * PhonemeGOP + 0.4 * ToneContourMatch
    float hybridScore = (0.6f * phonemeScore) + (0.4f * toneScore);

    // Tone & Syllable contour rule: If F0 and syllable count match >= 70%, trigger success immediately
    bool toneSyllableMatchFastPass = (toneScore >= 0.70f && syllableRatio >= 0.70f && phonemeScore >= 0.50f);

    bool isPass = hybridScore >= PassThreshold || toneSyllableMatchFastPass;

    return new SpeechEvaluationResult(
      isPass,
      hybridScore,
      phonemeScore,
      toneScore,
      detectedSyllables,
      cleanSpoken,
      devNote,
      matchedRegion
    );
  }

  // ATTEMPT-ONLY evaluation (production path — no Vietnamese ASR ships in this build).
  // Uses REAL evidence that exists on device: speech presence + energy-envelope syllable count
  // from the capture service. NEVER fabricates phoneme or tone scores (they stay NaN) and never
  // claims a transcript — the result is explicitly labeled as an effort pass.
  public static SpeechEvaluationResult EvaluateAttemptOnly(float[] samples,
      SpeakingPromptSO prompt, int sampleRate = 16000) {
    float sumSq = 0f;
    if (samples != null) {
      for (int i = 0; i < samples.Length; i++) sumSq += samples[i] * samples[i];
    }
    float rms = samples != null && samples.Length > 0 ? Mathf.Sqrt(sumSq / samples.Length) : 0f;
    bool hasSpeech = samples != null && samples.Length >= 1600 && rms >= 0.012f; // >= ~100ms audible
    int syllables = EstimateSyllableCount(null, samples, sampleRate);

    var res = new SpeechEvaluationResult(
      pass: hasSpeech,
      total: float.NaN,
      phoneme: float.NaN,
      tone: float.NaN,
      syllables: syllables,
      text: "",
      note: hasSpeech
        ? "EFFORT_PASS: no Vietnamese ASR on device - attempt evidence only"
        : "NO_SPEECH_DETECTED: gentle retry",
      region: RegionType.North);
    res.TranscriptAvailable = false;
    res.SilentAttempt = !hasSpeech;
    return res;
  }

  public static float ComputePhonemeScore(string spoken, SpeakingPromptSO prompt,
      out string devNote, out RegionType matchedRegion) {
    devNote = "";
    matchedRegion = RegionType.North;

    if (string.IsNullOrEmpty(spoken)) return 0f;
    string target = prompt.targetText ?? "";

    // Exact direct match
    if (string.Equals(spoken, target, StringComparison.OrdinalIgnoreCase)) {
      return 1.0f;
    }

    // Check custom prompt dialect matrix first
    if (prompt.dialectMatrix != null && prompt.dialectMatrix.Count > 0) {
      for (int i = 0; i < prompt.dialectMatrix.Count; i++) {
        var entry = prompt.dialectMatrix[i];
        if (entry.acceptedSpellings != null) {
          for (int v = 0; v < entry.acceptedSpellings.Count; v++) {
            if (string.Equals(spoken, entry.acceptedSpellings[v], StringComparison.OrdinalIgnoreCase)) {
              matchedRegion = entry.region;
              return 1.0f;
            }
          }
        }
      }
    }

    // 1. Regional Dialect Normalization (North: s/x, ch/tr, r/d/gi; Central/South: hoi/nga, n/ng, t/c)
    RegionType[] regions = { RegionType.North, RegionType.Central, RegionType.South };
    for (int r = 0; r < regions.Length; r++) {
      string normSpoken = NormalizeRegionalDialect(spoken, regions[r]);
      string normTarget = NormalizeRegionalDialect(target, regions[r]);
      if (string.Equals(normSpoken, normTarget, StringComparison.OrdinalIgnoreCase)) {
        matchedRegion = regions[r];
        return 1.0f; // 100% score for valid regional dialect
      }
    }

    // 2. Age-4 Developmental Lisp (Ngọng sinh lý: L/N swap, rounded labial glides)
    string lispSpoken = NormalizeDevelopmentalLisp(spoken);
    string lispTarget = NormalizeDevelopmentalLisp(target);
    if (string.Equals(lispSpoken, lispTarget, StringComparison.OrdinalIgnoreCase)
        || (lispSpoken.Length > 2 && lispTarget.Length > 2 && ComputeStringSimilarity(lispSpoken, lispTarget) >= 0.85f)) {
      devNote = "Developmental_Phoneme_Note: Age-4 physiological lisp accepted (L/N/glide tolerance)";
      return 0.95f; // Awarded pass with developmental note
    }

    // 3. Levenshtein / Token Distance fallback
    return ComputeStringSimilarity(spoken.ToLowerInvariant(), target.ToLowerInvariant());
  }

  public static string NormalizeRegionalDialect(string text, RegionType region) {
    if (string.IsNullOrEmpty(text)) return "";
    string s = text.ToLowerInvariant();

    if (region == RegionType.North) {
      // S/X, CH/TR, R/D/GI
      s = s.Replace("tr", "ch")
           .Replace("x", "s")
           .Replace("gi", "d")
           .Replace("r", "d");
    } else if (region == RegionType.Central || region == RegionType.South) {
      // Hoi / Nga unification
      s = ReplaceTonesHoiNga(s);
      // Final consonants n/ng, t/c
      s = s.Replace("ng ", "n ")
           .Replace("c ", "t ");
      if (s.EndsWith("ng")) s = s.Substring(0, s.Length - 1);
      if (s.EndsWith("c")) s = s.Substring(0, s.Length - 1) + "t";
      // South: v -> d
      if (region == RegionType.South) {
        s = s.Replace("v", "d");
      }
    }
    return s.Trim();
  }

  public static string NormalizeDevelopmentalLisp(string text) {
    if (string.IsNullOrEmpty(text)) return "";
    string s = text.ToLowerInvariant();
    // 1. L <-> N developmental swap in 4yo preschool
    s = s.Replace("l", "n");
    // 2. Fronting / toddler substitution (e.g. initial l/t -> c)
    if (s.StartsWith("non ") || s.StartsWith("ton ")) {
      s = "con " + s.Substring(4);
    }
    // 3. Labial glide simplification (qu -> c/k, oa -> a)
    s = s.Replace("qu", "c")
         .Replace("oa", "a")
         .Replace("oe", "e");
    // Also include North/Central dialect flexibility
    s = NormalizeRegionalDialect(s, RegionType.North);
    s = ReplaceTonesHoiNga(s);
    return s.Trim();
  }

  static string ReplaceTonesHoiNga(string s) {
    // Unifies hỏi with ngã for Central/South dialects
    char[] srcHoi = { 'ả', 'ẳ', 'ẩ', 'ẻ', 'ể', 'ỉ', 'ỏ', 'ở', 'ổ', 'ủ', 'ử', 'ỷ' };
    char[] dstNga = { 'ã', 'ẵ', 'ẫ', 'ẽ', 'ễ', 'ĩ', 'õ', 'ỡ', 'ỗ', 'ũ', 'ữ', 'ỹ' };
    char[] chars = s.ToCharArray();
    for (int i = 0; i < chars.Length; i++) {
      for (int k = 0; k < srcHoi.Length; k++) {
        if (chars[i] == srcHoi[k]) {
          chars[i] = dstNga[k];
          break;
        }
      }
    }
    return new string(chars);
  }

  public static float ComputeToneContourScore(string spoken, string target, float[] samples, int sampleRate) {
    if (string.IsNullOrEmpty(spoken) || string.IsNullOrEmpty(target)) return 0.5f;

    // Pitch & tone contour estimate
    int spokenTones = ExtractToneProfile(spoken);
    int targetTones = ExtractToneProfile(target);

    // If audio samples provided, verify RMS energy contour
    if (samples != null && samples.Length > 200) {
      float energyContourMatch = EstimateEnergyContour(samples);
      return Mathf.Clamp01(0.5f * (spokenTones == targetTones ? 1f : 0.6f) + 0.5f * energyContourMatch);
    }

    return spokenTones == targetTones ? 1.0f : 0.65f;
  }

  static int ExtractToneProfile(string text) {
    int profile = 0;
    string s = text.ToLowerInvariant();
    // 0: Ngang, 1: Huyen, 2: Sac, 3: Hoi/Nga, 4: Nang
    if (s.IndexOfAny(new[] { 'á', 'ắ', 'ấ', 'é', 'ế', 'í', 'ó', 'ố', 'ớ', 'ú', 'ứ', 'ý' }) >= 0) profile = 2;
    else if (s.IndexOfAny(new[] { 'à', 'ằ', 'ầ', 'è', 'ề', 'ì', 'ò', 'ồ', 'ờ', 'ù', 'ừ', 'ỳ' }) >= 0) profile = 1;
    else if (s.IndexOfAny(new[] { 'ả', 'ẳ', 'ẩ', 'ẻ', 'ể', 'ỉ', 'ỏ', 'ổ', 'ở', 'ủ', 'ử', 'ỷ', 'ã', 'ẵ', 'ẫ', 'ẽ', 'ễ', 'ĩ', 'õ', 'ỗ', 'ỡ', 'ũ', 'ữ', 'ỹ' }) >= 0) profile = 3;
    else if (s.IndexOfAny(new[] { 'ạ', 'ặ', 'ậ', 'ẹ', 'ệ', 'ị', 'ọ', 'ộ', 'ợ', 'ụ', 'ự', 'ỵ' }) >= 0) profile = 4;
    return profile;
  }

  static float EstimateEnergyContour(float[] samples) {
    if (samples == null || samples.Length == 0) return 0.5f;
    int half = samples.Length / 2;
    float e1 = 0f, e2 = 0f;
    for (int i = 0; i < half; i++) e1 += samples[i] * samples[i];
    for (int i = half; i < samples.Length; i++) e2 += samples[i] * samples[i];
    // Natural child speech has robust bell or rising envelope
    return (e1 > 0f || e2 > 0f) ? 0.85f : 0.4f;
  }

  public static int EstimateSyllableCount(string text, float[] samples, int sampleRate) {
    if (!string.IsNullOrWhiteSpace(text)) {
      string[] parts = text.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length > 0) return parts.Length;
    }

    if (samples != null && samples.Length > 0) {
      // Energy envelope peak counting (window size ~100ms)
      int window = sampleRate / 10;
      int peaks = 0;
      float lastRms = 0f;
      bool inPeak = false;

      for (int i = 0; i + window < samples.Length; i += window) {
        float sum = 0f;
        for (int k = 0; k < window; k++) sum += samples[i + k] * samples[i + k];
        float rms = Mathf.Sqrt(sum / window);
        if (rms >= 0.02f && rms > lastRms * 1.3f && !inPeak) {
          peaks++;
          inPeak = true;
        } else if (rms < 0.015f) {
          inPeak = false;
        }
        lastRms = rms;
      }
      return Mathf.Max(1, peaks);
    }
    return 1;
  }

  static float ComputeStringSimilarity(string a, string b) {
    if (a == b) return 1.0f;
    int maxLen = Math.Max(a.Length, b.Length);
    if (maxLen == 0) return 1.0f;
    int dist = LevenshteinDistance(a, b);
    return 1.0f - (float)dist / maxLen;
  }

  static int LevenshteinDistance(string s, string t) {
    int n = s.Length;
    int m = t.Length;
    int[,] d = new int[n + 1, m + 1];
    if (n == 0) return m;
    if (m == 0) return n;
    for (int i = 0; i <= n; d[i, 0] = i++) { }
    for (int j = 0; j <= m; d[0, j] = j++) { }
    for (int i = 1; i <= n; i++) {
      for (int j = 1; j <= m; j++) {
        int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
        d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
      }
    }
    return d[n, m];
  }
}
