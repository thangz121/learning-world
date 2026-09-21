// C_Content/NpcRoster.cs — Phase 2E (NPC template, ADDITIVE).
// Canonical NPC identity: stable id ("mia") vs display name ("Mia") vs
// quest-alias ("shopkeeper_mia") vs voice profile ("mia_v1").
//
// WHY this exists (2B P1): QuestEntry.npc was write-only (parsed, validated,
// consumed by NOTHING) while Bootstrap hardcoded "Milo"/"Mia"/2.35m in
// three places. The roster is the single source that:
//   - resolves any shipped quest's npc value to a stable identity
//     (covers all 7 quest JSONs today — pinned by CT-P09),
//   - supplies display names + label heights to the composition root
//     (Bootstrap reads the roster; presenters never touch strings),
//   - exposes voice profiles for 2F narration wiring.
// Deliberately NOT included (YAGNI, 2F owns behavior): roles, quest lists,
// spawn logic, behavior dispatch. An NPC id here never drives gameplay —
// identity answers "who is in the quest", never "what happens".
// C# 9.0 only. No UnityEngine, no IO.
using System;
using System.Collections.Generic;

[Serializable]
public sealed class NpcDefinition {
  public string id;            // stable identity, e.g. "mia" (lookup key)
  public string displayName;   // world label + HUD text, e.g. "Mia"
  public string voice;         // VoiceProfileId, e.g. "mia_v1"
  public List<string> questNpc; // QuestEntry.npc aliases, e.g. "shopkeeper_mia"
  public float labelHeight;    // world-space label height (m), e.g. 2.35
}

public static class NpcRoster {
  // The cast, today. Third NPC = third entry here + its presenter/visual;
  // no roster mechanics change (no spawning framework, no behavior tree).
  static readonly List<NpcDefinition> Definitions = new List<NpcDefinition> {
    new NpcDefinition {
      id = "milo", displayName = "Milo", voice = "milo_v1",
      questNpc = new List<string> { "milo" }, labelHeight = 2.35f,
    },
    new NpcDefinition {
      id = "mia", displayName = "Mia", voice = "mia_v1",
      questNpc = new List<string> { "shopkeeper_mia", "mia" }, labelHeight = 2.35f,
    },
    // Phase 3.0.x S3: Tess hosts the Math world lobby (Counting Garden pilot).
    // Third entry amends the 2E "exactly Milo + Mia" pin (see CT-P09 Phase
    // 3.0.x notes): Main-world quests still stage Mia, math_* quests stage Tess.
    new NpcDefinition {
      id = "tess", displayName = "Tess", voice = "npc_female_01",
      questNpc = new List<string> { "math_host", "tess" }, labelHeight = 2.35f,
    },
  };

  public static NpcDefinition Get(string npcId) {
    if (string.IsNullOrEmpty(npcId)) return null;
    foreach (NpcDefinition d in Definitions) {
      if (d != null && string.Equals(d.id, npcId.Trim(),
          StringComparison.OrdinalIgnoreCase)) return d;
    }
    return null;
  }

  // QuestEntry.npc consumption point (2B false-reusability #4 closed):
  // story/quest code resolves the CONTENT alias, never branches on it.
  public static NpcDefinition ResolveQuestNpc(string questNpcValue) {
    if (string.IsNullOrEmpty(questNpcValue)) return null;
    string v = questNpcValue.Trim();
    foreach (NpcDefinition d in Definitions) {
      if (d == null || d.questNpc == null) continue;
      foreach (string alias in d.questNpc) {
        if (string.Equals(alias, v, StringComparison.OrdinalIgnoreCase)) return d;
      }
    }
    return null;
  }

  public static int Count {
    get { return Definitions.Count; }
  }
}
