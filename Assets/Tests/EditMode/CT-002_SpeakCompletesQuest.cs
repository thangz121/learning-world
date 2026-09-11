// CT-002: Policy Great + ReportAction(Speak,apple) completes say_apple objective -> QuestCompletedEvent.
// Owner: Agent B (W0-T1 QuestManager). No Action<string> event allowed.
using NUnit.Framework;
using System.Collections.Generic;

public class CT_002_SpeakCompletesQuest {
  sealed class SingleSpeakQuest : IQuestContentProvider {
    public QuestData Get(QuestId questId) {
      return new QuestData {
        id = questId.Value,
        objectives = new List<ObjectiveData> {
          new ObjectiveData { id = "say_apple", action = "speak", target = "apple" }
        }
      };
    }
  }

  [Test] public void CT_002() {
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var quests = new QuestManager(bus, learning, hints, new SingleSpeakQuest());
    var q = new QuestId("say_apple_quest");
    var apple = new WordId("apple");

    QuestStartedEvent? started = null;
    QuestCompletedEvent? done = null;
    bus.Subscribe<QuestStartedEvent>(e => started = e);
    bus.Subscribe<QuestCompletedEvent>(e => done = e);

    quests.StartQuest(q);
    Assert.IsTrue(started.HasValue, "StartQuest must publish QuestStartedEvent");
    Assert.AreEqual(q.Value, started.Value.QuestId.Value);
    Assert.IsFalse(quests.GetState(q).Completed);

    // Speech path: assessed Great, reported to learning, then the Speak action.
    learning.ReportSpoken(apple, SpeechLevel.Great, LearnSource.Quest);
    quests.ReportAction(PlayerAction.Speak, apple);

    Assert.IsTrue(quests.GetState(q).Completed, "ReportAction(Speak,apple) must complete say_apple");
    Assert.IsTrue(done.HasValue, "completion must publish QuestCompletedEvent (only via EventBus)");
    Assert.AreEqual(q.Value, done.Value.QuestId.Value);
  }

  [Test] public void CT_002_PartialProgress_DoesNotComplete() {
    // market_help_mia via the built-in provider: find -> bring -> speak, one at a time.
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var quests = new QuestManager(bus, learning, hints);
    var q = new QuestId("market_help_mia");
    var apple = new WordId("apple");
    bool done = false;
    bus.Subscribe<QuestCompletedEvent>(e => done = true);

    quests.StartQuest(q);
    quests.AdvanceOnSeen(apple); // completes find_apple only
    Assert.AreEqual(1, quests.GetState(q).ObjectiveIndex);
    Assert.IsFalse(quests.GetState(q).Completed);
    Assert.IsFalse(done);

    quests.ReportAction(PlayerAction.Bring, apple); // completes bring_apple
    Assert.AreEqual(2, quests.GetState(q).ObjectiveIndex);
    Assert.IsFalse(quests.GetState(q).Completed);

    quests.AdvanceOnSpoken(apple, SpeechLevel.Great); // completes speak_apple
    Assert.IsTrue(quests.GetState(q).Completed);
    Assert.IsTrue(done);
  }

  [Test] public void CT_002_SpeakBelowGreat_DoesNotAdvance() {
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var quests = new QuestManager(bus, learning, hints, new SingleSpeakQuest());
    var q = new QuestId("say_apple_quest");
    quests.StartQuest(q);
    quests.AdvanceOnSpoken(new WordId("apple"), SpeechLevel.Almost);
    Assert.IsFalse(quests.GetState(q).Completed, "Almost must not advance a Speak objective");
  }
}
