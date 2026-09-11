// D_Audio/SpeechAssessmentPolicy.cs — Agent D (W0-T1). Pure logic, NO Unity deps.
// Freeze: PronScore>=85 Perfect, 65-85 Great, 40-65 Almost, else TryTogether.
// ErrorReason NoSpeech/Noise/MixedLang with low confidence -> TryTogether (offline/
// noisy input routes to the Hint demo path, never a score). Similarity = PronScore/100.
public sealed class SpeechAssessmentPolicy : ISpeechPolicy {
  public const float PerfectThreshold = 85f;
  public const float GreatThreshold = 65f;
  public const float AlmostThreshold = 40f;

  // W0 heuristic pending QA data: confidence below this counts as "low".
  public const float LowConfidenceThreshold = 0.5f;

  public SpeechResult Assess(WordId expected, RawSpeechResult raw) {
    string heard = (raw.Transcript ?? "").Trim();
    float similarity = raw.PronScore / 100f;
    if (similarity < 0f) similarity = 0f;
    if (similarity > 1f) similarity = 1f;

    SpeechLevel level;
    if (IsLowSignal(raw) || heard.Length == 0) {
      level = SpeechLevel.TryTogether;
    } else if (raw.PronScore >= PerfectThreshold) {
      level = SpeechLevel.Perfect;
    } else if (raw.PronScore >= GreatThreshold) {
      level = SpeechLevel.Great;
    } else if (raw.PronScore >= AlmostThreshold) {
      level = SpeechLevel.Almost;
    } else {
      level = SpeechLevel.TryTogether;
    }

    return new SpeechResult {
      Expected = expected,
      Heard = heard,
      Similarity = similarity,
      Level = level,
    };
  }

  static bool IsLowSignal(RawSpeechResult raw) {
    if (string.IsNullOrEmpty(raw.ErrorReason)) return false;
    string e = raw.ErrorReason.Trim().ToLowerInvariant();
    bool knownLow = e == "nospeech" || e == "noise" || e == "mixedlang";
    return knownLow && raw.Confidence < LowConfidenceThreshold;
  }
}
