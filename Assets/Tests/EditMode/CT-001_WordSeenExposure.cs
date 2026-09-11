// CT-001: WordSeen x3 -> Exposed. Owner: Agent B (W0-T1 LearningService).
// Given mastery apple=Unknown. When WordSeenEvent(apple,Object) x3. Then stage=Exposed.
using NUnit.Framework;

public class CT_001_WordSeenExposure {
  [Test] public void CT_001() {
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var apple = new WordId("apple");

    Assert.AreEqual(MasteryStage.Unknown, learning.GetStage(apple), "fresh word must be Unknown");

    learning.ReportSeen(apple, LearnSource.Object);
    learning.ReportSeen(apple, LearnSource.Object);
    Assert.AreEqual(MasteryStage.Unknown, learning.GetStage(apple), "2 exposures must not reach Exposed");

    learning.ReportSeen(apple, LearnSource.Object);
    Assert.AreEqual(MasteryStage.Exposed, learning.GetStage(apple), "3rd Seen/Heard must promote to Exposed");
    Assert.AreEqual(3, learning.GetMastery(apple).Exposure);
  }

  [Test] public void CT_001_UnknownWord_StaysUnknown() {
    var learning = new LearningService(new GameEventBus());
    Assert.AreEqual(MasteryStage.Unknown, learning.GetStage(new WordId("banana")));
  }
}
