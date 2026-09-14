// C_Content/QuestPattern.cs — Phase 2A (content pipeline, ADDITIVE).
// Reusable quest patterns. The Phase 1 quest LOGIC (QuestManager, presenters,
// camera, HUD) is frozen and untouched: this file only NAMES the shapes that
// quest DATA already takes, so future content can be authored by pattern
// instead of by copying gameplay scripts.
//
//   A FindIdentify  — [Find]: locate / point at the thing.
//   B BringToNpc    — [Find, Bring]: the w1 golden loop (apple, ball).
//   C ChooseCorrect — contains Select: pick the right one among choices.
//   D MatchWord     — [Speak] alone, or [Find, Speak] on one target.
//   E MultiStep     — anything longer/mixed (market_help_mia Find,Bring,Speak).
//
// Classify() is total (never throws, Unknown on empty input) and pinned by
// CT-P06. C# 9.0 only. No UnityEngine, no IO, no gameplay references.
using System.Collections.Generic;

public enum QuestPattern {
  Unknown = 0,
  FindIdentify = 1, // PATTERN A
  BringToNpc = 2,   // PATTERN B
  ChooseCorrect = 3, // PATTERN C
  MatchWord = 4,    // PATTERN D
  MultiStep = 5,    // PATTERN E
}

public static class QuestPatternDef {
  // Ordered rules (first match wins). Same-target checks compare the raw
  // target strings case-insensitively; QuestManager does its own typed
  // comparison at the gameplay boundary, this is content-side only.
  public static QuestPattern Classify(IList<PlayerAction> actions) {
    return Classify(actions, null);
  }

  public static QuestPattern Classify(IList<PlayerAction> actions, IList<string> targets) {
    if (actions == null || actions.Count == 0) return QuestPattern.Unknown;
    if (actions.Count == 1 && actions[0] == PlayerAction.Find) return QuestPattern.FindIdentify;
    for (int i = 0; i < actions.Count; i++) {
      if (actions[i] == PlayerAction.Select) return QuestPattern.ChooseCorrect; // C: a choice exists
    }
    if (actions.Count == 2 && actions[0] == PlayerAction.Find && actions[1] == PlayerAction.Bring)
      return QuestPattern.BringToNpc; // B: the golden loop
    if (actions.Count == 1 && actions[0] == PlayerAction.Speak) return QuestPattern.MatchWord;
    if (actions.Count == 2 && actions[0] == PlayerAction.Find && actions[1] == PlayerAction.Speak
        && SameTarget(targets, 0, 1)) return QuestPattern.MatchWord; // D
    return QuestPattern.MultiStep; // E: longer or mixed chains
  }

  static bool SameTarget(IList<string> targets, int a, int b) {
    if (targets == null || targets.Count <= b) return false;
    string x = targets[a];
    string y = targets[b];
    if (string.IsNullOrEmpty(x) || string.IsNullOrEmpty(y)) return false;
    return string.Equals(x.Trim(), y.Trim(), System.StringComparison.OrdinalIgnoreCase);
  }
}
