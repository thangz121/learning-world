// A_World/SpeakingStage/SpeakingPack.cs — English -> Speaking word pack + the
// four-level feedback mapping (WP-1.9.33). Same 8 words as the listening pack
// (one picture pack, two skills). Phoneme lists mirror Content/vocab
// speech.phonemes (pinned by CT-S22); the runtime catalog is built from here so
// the build needs no JSON loader. Pure C# 9.
using System.Collections.Generic;

public static class SpeakingPack {
  // The spoken word set (same as the listening pack).
  public static readonly string[] Words = ListeningWords.All;

  static readonly string[] Level1Words = { "apple", "ball", "banana", "orange" };
  static readonly string[] Level2Words = { "carrot", "tomato", "watermelon", "grape" };
  public const int LastLevel = 2;
  public const int RoundsPerLevel = 4;
  public const int AttemptsPerWord = 3;

  // Pronunciation data, mirrored from Content/vocab/*.json speech.phonemes
  // (12 words: 6 benchmark + 6 pack words; "grape" carries the spoken "grapes").
  static readonly Dictionary<string, string[]> Phonemes =
    new Dictionary<string, string[]> {
      { "apple", new[] { "AE", "P", "AH", "L" } },
      { "ball", new[] { "B", "AO", "L" } },
      { "banana", new[] { "B", "AH", "N", "AE", "N", "AH" } },
      { "orange", new[] { "AO", "R", "AH", "N", "JH" } },
      { "carrot", new[] { "K", "AE", "R", "AH", "T" } },
      { "tomato", new[] { "T", "AH", "M", "EY", "T", "OW" } },
      { "watermelon", new[] { "W", "AO", "T", "ER", "M", "EH", "L", "AH", "N" } },
      { "grape", new[] { "G", "R", "EY", "P", "S" } },
      { "red", new[] { "R", "EH", "D" } },
      { "one", new[] { "W", "AH", "N" } },
      { "please", new[] { "P", "L", "IY", "Z" } },
      { "teddy", new[] { "T", "EH", "D", "IY" } },
    };

  public static string[] WordsFor(int level) {
    if (level <= 1) return (string[])Level1Words.Clone();
    return (string[])Level2Words.Clone();
  }

  public static string WordFor(int level, int roundIndex) {
    string[] words = WordsFor(level);
    int i = roundIndex % words.Length;
    if (i < 0) i += words.Length;
    return words[i];
  }

  public static int RoundsFor(int level) { return RoundsPerLevel; }

  public static int TotalRounds() { return RoundsPerLevel * LastLevel; }

  public static string[] PhonemesFor(string id) {
    string[] hit;
    if (id != null && Phonemes.TryGetValue(id, out hit)) return (string[])hit.Clone();
    return null;
  }

  // The runtime engine cohort (12 words with pronunciation data). The caller
  // (composition root / tests) owns the catalog type; A_World stays free of
  // C_Content references (asmdef edge kept clean).
  public static void Apply(RegisterPhonemes register) {
    if (register == null) return;
    foreach (KeyValuePair<string, string[]> kv in Phonemes)
      register(kv.Key, kv.Value);
  }

  public delegate void RegisterPhonemes(string word, string[] phonemeIds);
}

// The four child-facing evaluation levels, mapped 1:1 onto the frozen
// SpeechLevel enum (Perfect=Xuất sắc, Great=Đúng rồi, Almost=Gần đúng rồi,
// TryTogether=Thử lại nhé). Lines stay <= 6 words (SafetyFilter).
public static class SpeakingFeedback {
  public static void PhraseFor(SpeechLevel level, out string en, out string vi) {
    switch (level) {
      case SpeechLevel.Perfect: en = "Excellent!"; vi = "Xuất sắc!"; return;
      case SpeechLevel.Great: en = "That's right!"; vi = "Đúng rồi!"; return;
      case SpeechLevel.Almost: en = "Almost there!"; vi = "Gần đúng rồi!"; return;
      default: en = "Try again!"; vi = "Thử lại nhé!"; return;
    }
  }
}
