// CT-003: QuestCompletedEvent -> flower_pot + friendship_mia+10. Owner: Agent A+B (W0-T1).
// B implements reward STATE (QuestRewardService over QuestData rewards, mirroring
// Content/quests/market_help_mia.json reward {friendship_mia:10, world_change:flower_pot}).
// A binds QuestRewardService.HasWorldChange to visuals (prefab growth) in W1;
// no GameObject work is asserted here (EditMode).
using System;
using System.Collections.Generic;
using NUnit.Framework;

public class CT_003_QuestChangesWorld {
  sealed class RewardQuest : IQuestContentProvider {
    public QuestData Get(QuestId questId) {
      return new QuestData {
        id = questId.Value,
        objectives = new List<ObjectiveData> {
          new ObjectiveData { id = "find_apple", action = "find", target = "apple" }
        },
        rewardFriendshipMia = 10,
        rewardWorldChange = "flower_pot",
      };
    }
  }

  [Test] public void CT_003() {
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var content = new RewardQuest();
    var quests = new QuestManager(bus, learning, hints, content);
    var rewards = new QuestRewardService(bus, content);
    var q = new QuestId("reward_quest");
    var apple = new WordId("apple");
    var mia = new NpcId("mia");

    quests.StartQuest(q);
    Assert.IsFalse(rewards.HasWorldChange("flower_pot"), "no world change before completion");
    Assert.AreEqual(0, rewards.GetFriendship(mia), "no friendship before completion");

    quests.AdvanceOnSeen(apple); // completes find_apple -> publishes QuestCompletedEvent
    Assert.IsTrue(quests.GetState(q).Completed, "quest must complete");
    Assert.IsTrue(rewards.HasWorldChange("flower_pot"), "completion must grow flower_pot");
    Assert.AreEqual(10, rewards.GetFriendship(mia), "completion must grant friendship_mia +10");

    bus.Publish(new QuestCompletedEvent(q, DateTime.UtcNow)); // duplicate delivery
    Assert.AreEqual(10, rewards.GetFriendship(mia), "each quest reward applies exactly once");
    Assert.IsTrue(rewards.HasWorldChange("flower_pot"));
  }
}
