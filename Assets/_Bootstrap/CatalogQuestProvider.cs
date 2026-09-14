// _Bootstrap/CatalogQuestProvider.cs — Phase 2A (content pipeline, ADDITIVE).
// Runtime adapter: serves QuestData from catalog QuestEntry records through
// the FROZEN IQuestContentProvider boundary, so QuestManager and
// QuestRewardService run data-authored quests with zero logic changes.
//
// LAYERING (why this lives in Bootstrap, not C_Content): IQuestContentProvider
// + QuestData are declared in LWE.Brain (QuestManager.cs) while QuestEntry
// lives in LWE.Content — and neither assembly references the other (both
// point only at SharedKernel). Only the composition root references both,
// and GameInstaller is the only site allowed to construct services. Nothing
// constructs this yet (2F wires it when runtime JSON loading lands); CT-P06D
// proves it against the real QuestManager + QuestRewardService today.
// C# 9.0 only. No IO here (entries arrive parsed; loading is 2D work).
using System.Collections.Generic;

public sealed class CatalogQuestProvider : IQuestContentProvider {
  readonly Dictionary<string, QuestData> _byId = new Dictionary<string, QuestData>();

  public CatalogQuestProvider(IEnumerable<QuestEntry> quests) {
    if (quests == null) return;
    foreach (QuestEntry q in quests) {
      if (q == null || string.IsNullOrEmpty(q.id) || _byId.ContainsKey(q.id)) continue;
      var data = new QuestData {
        id = q.id,
        objectives = new List<ObjectiveData>(),
        rewardFriendshipMia = q.rewardFriendship,
        rewardWorldChange = q.rewardWorldChange,
      };
      if (q.objectives != null) {
        foreach (QuestObjective o in q.objectives) {
          if (o == null) continue;
          data.objectives.Add(new ObjectiveData {
            id = o.id, action = o.action.ToString(), target = o.target,
          });
        }
      }
      _byId[q.id] = data;
    }
  }

  public QuestData Get(QuestId questId) {
    QuestData d;
    if (_byId.TryGetValue(questId.Value, out d)) return d;
    return null;
  }
}
