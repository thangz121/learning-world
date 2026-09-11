// D_Audio/SafetyFilter.cs — Agent D (W0-T1). Level-1 line validator, pure logic.
// Runs BEFORE any Speak path in AudioDirector. Level-1 scope ONLY:
//   1. word-count (NPC <= 6, Milo <= 8, mirrors tools/validate_content.py + AUDIO_DESIGN §10)
//   2. reject list (Level-1 idioms + sarcasm markers — young learners take these literally)
// Everything else is allowlist passthrough (deeper pedagogy QA lives in C/W1, not here).
public static class SafetyFilter {
  public const int NpcMaxWords = 6;
  public const int MiloMaxWords = 8;

  // W0 freeze. Case-insensitive substring match on the raw line.
  static readonly string[] BlockedPhrases = {
    "piece of cake", // idiom
    "break a leg",   // idiom
    "yeah right",    // sarcasm marker
    "oh great",      // sarcasm marker (L1 cannot detect tone, reject literal)
    "as if",         // sarcasm marker
    "big deal",      // sarcasm marker
  };

  // Returns true when the line may be spoken. reason is string.Empty when valid,
  // otherwise a short machine-readable code for logs ("empty" | "word-count ..." | "blocked-phrase ...").
  public static bool ValidateLine(string text, bool isMilo, out string reason) {
    if (string.IsNullOrWhiteSpace(text)) {
      reason = "empty";
      return false;
    }
    int limit = isMilo ? MiloMaxWords : NpcMaxWords;
    int words = CountWords(text);
    if (words > limit) {
      reason = "word-count " + words + ">" + limit;
      return false;
    }
    string lower = text.ToLowerInvariant();
    for (int i = 0; i < BlockedPhrases.Length; i++) {
      if (lower.Contains(BlockedPhrases[i])) {
        reason = "blocked-phrase '" + BlockedPhrases[i] + "'";
        return false;
      }
    }
    reason = string.Empty;
    return true;
  }

  static int CountWords(string text) {
    return text.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries).Length;
  }
}
