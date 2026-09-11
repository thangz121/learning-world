// B_Brain/IntentEngine.cs — Agent B (W0-T1). Tier2 static keyword intent matcher.
// No ML, no history: MatchIntent(npcId, childText) -> one of
// GREETING / HELP_REQUEST / ITEM_REQUEST / CORRECT_ANSWER / WRONG_ANSWER / GOODBYE / FALLBACK.
// Matching is whole-token (word-boundary) on lowercased alpha tokens, so "hi"
// never fires inside "this" and "no" never fires inside "know".
// Priority on overlap: GOODBYE > GREETING > HELP_REQUEST > ITEM_REQUEST >
// CORRECT_ANSWER > WRONG_ANSWER > FALLBACK. npcId is reserved for Tier3
// per-NPC tuning; Tier2 rules are global.
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class IntentEngine {
  public const string Greeting = "GREETING";
  public const string HelpRequest = "HELP_REQUEST";
  public const string ItemRequest = "ITEM_REQUEST";
  public const string CorrectAnswer = "CORRECT_ANSWER";
  public const string WrongAnswer = "WRONG_ANSWER";
  public const string Goodbye = "GOODBYE";
  public const string Fallback = "FALLBACK";

  public static string MatchIntent(string npcId, string childText) {
    if (string.IsNullOrWhiteSpace(childText)) return Fallback;
    string lower = childText.ToLowerInvariant().Replace("'", "").Replace("’", "");
    var tokens = new HashSet<string>();
    foreach (Match m in Regex.Matches(lower, "[a-z]+")) tokens.Add(m.Value);
    if (tokens.Count == 0) return Fallback;
    string padded = " " + lower + " ";

    if (tokens.Contains("bye") || tokens.Contains("goodbye") || tokens.Contains("goodnight")
        || padded.Contains(" see you ") || padded.Contains("good bye ")
        || padded.Contains("good night ")) return Goodbye;

    if (tokens.Contains("hello") || tokens.Contains("hi") || tokens.Contains("hey")
        || tokens.Contains("morning") || padded.Contains("good morning ")
        || padded.Contains("good afternoon ")) return Greeting;

    if (tokens.Contains("help") || tokens.Contains("stuck") || tokens.Contains("confused")
        || padded.Contains("dont know ") || padded.Contains("do not know ") || padded.Contains("can you help ")
        || padded.Contains("help me ") || padded.Contains("i give up ")) return HelpRequest;

    if (tokens.Contains("want") || tokens.Contains("need") || tokens.Contains("give")
        || tokens.Contains("get") || tokens.Contains("bring") || tokens.Contains("take")
        || padded.Contains("give me ") || padded.Contains("can i have ")) return ItemRequest;

    if (tokens.Contains("yes") || tokens.Contains("yeah") || tokens.Contains("yep")
        || tokens.Contains("yup") || tokens.Contains("correct") || tokens.Contains("right"))
      return CorrectAnswer;

    if (tokens.Contains("no") || tokens.Contains("nope") || tokens.Contains("nah")
        || tokens.Contains("wrong")) return WrongAnswer;

    return Fallback;
  }
}
