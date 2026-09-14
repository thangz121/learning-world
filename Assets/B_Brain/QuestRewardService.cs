// B_Brain/QuestRewardService.cs — Agent B (W0-T1). Quest completion rewards.
// Plain C# service, deterministic. Constructed ONLY by GameInstaller:
//   new QuestRewardService(IGameEventBus)
// Subscribes to QuestCompletedEvent (the ONLY completion signal, CT-006) and
// applies the quest's content reward: friendship ledger + world-change set.
// Reward source is IQuestContentProvider (same content QuestManager uses); the
// default built-in mirror covers market_help_mia so the GameInstaller path
// needs zero file IO (source of truth stays Content/quests/*.json).
// Ownership split (ContractTests CT-003, Agent A+B): this service owns reward
// STATE (ledger + applied world-change ids). Agent A binds HasWorldChange to
// visuals (prefab growth) in W1; no GameObject/Scene work happens here.
// Scope note: W0-T1 contract tracks a single friendship ledger (mia). Multi-NPC
// relationship state is Phase 4 (Tier3 memory engine explicitly out of Slice).
using System;
using System.Collections.Generic;

public sealed class QuestRewardService {
  public const string MiaNpcId = "mia";

  readonly IGameEventBus _bus;
  readonly IQuestContentProvider _content;

  readonly HashSet<QuestId> _applied = new HashSet<QuestId>();
  readonly Dictionary<string, int> _friendship = new Dictionary<string, int>();
  readonly HashSet<string> _worldChanges = new HashSet<string>();

  public QuestRewardService(IGameEventBus bus, IQuestContentProvider content = null) {
    _bus = bus ?? throw new ArgumentNullException(nameof(bus));
    _content = content ?? new BuiltInRewardContent();
    _bus.Subscribe<QuestCompletedEvent>(OnQuestCompleted);
  }

  public int GetFriendship(NpcId npc) {
    string key = npc.Value ?? "";
    int v;
    return _friendship.TryGetValue(key, out v) ? v : 0;
  }

  public bool HasWorldChange(string changeId) {
    if (string.IsNullOrEmpty(changeId)) return false;
    return _worldChanges.Contains(changeId);
  }

  void OnQuestCompleted(QuestCompletedEvent e) {
    if (!_applied.Add(e.QuestId)) return; // each quest's reward applies exactly once
    QuestData data = null;
    try { data = _content.Get(e.QuestId); } catch (Exception) { return; }
    if (data == null) return;
    if (data.rewardFriendshipMia != 0) {
      string key = new NpcId(MiaNpcId).Value ?? MiaNpcId;
      int cur;
      _friendship.TryGetValue(key, out cur);
      _friendship[key] = cur + data.rewardFriendshipMia;
    }
    if (!string.IsNullOrEmpty(data.rewardWorldChange)) {
      _worldChanges.Add(data.rewardWorldChange);
    }
  }

  // Fallback content mirroring Content/quests/w1_mia_apple.json and
  // Content/quests/market_help_mia.json reward blocks (source of truth stays
  // the JSON; this keeps the GameInstaller path working with zero file IO).
  // Objectives live in QuestManager's own mirror; rewards are the only fields
  // this service reads.
  sealed class BuiltInRewardContent : IQuestContentProvider {
    public QuestData Get(QuestId questId) {
      if (questId.Value == "w1_mia_apple") {
        return new QuestData {
          id = "w1_mia_apple",
          rewardFriendshipMia = 10,
          rewardWorldChange = "flower_pot",
        };
      }
      if (questId.Value == "w1_mia_ball") {
        return new QuestData {
          id = "w1_mia_ball",
          rewardFriendshipMia = 10,
          rewardWorldChange = "flower_pot",
        };
      }
      if (questId.Value != "market_help_mia") return null;
      return new QuestData {
        id = "market_help_mia",
        rewardFriendshipMia = 10,
        rewardWorldChange = "flower_pot",
      };
    }
  }
}
