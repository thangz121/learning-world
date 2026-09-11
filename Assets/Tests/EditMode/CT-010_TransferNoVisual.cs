// CT-010: apple Produced Q1, Q4 no-visual correct -> ContextUse+1. Owner: Agent B (W0-T1).
using NUnit.Framework;

public class CT_010_TransferNoVisual {
  [Test] public void CT_010() {
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var apple = new WordId("apple");

    // Q1 expository path: 3 Seen -> Exposed.
    learning.ReportSeen(apple, LearnSource.Quest);
    learning.ReportSeen(apple, LearnSource.Quest);
    learning.ReportSeen(apple, LearnSource.Quest);
    Assert.AreEqual(MasteryStage.Exposed, learning.GetStage(apple));

    // Recognition: 4/5 selection hits -> Recognized.
    learning.ReportSelected(apple, true, LearnSource.Quest);
    learning.ReportSelected(apple, true, LearnSource.Quest);
    learning.ReportSelected(apple, true, LearnSource.Quest);
    learning.ReportSelected(apple, true, LearnSource.Quest);
    learning.ReportSelected(apple, false, LearnSource.Quest);
    Assert.AreEqual(MasteryStage.Recognized, learning.GetStage(apple));

    // Production: 2/4 spoken Great+ (here 4x Great) -> Produced.
    learning.ReportSpoken(apple, SpeechLevel.Great, LearnSource.Quest);
    learning.ReportSpoken(apple, SpeechLevel.Great, LearnSource.Quest);
    learning.ReportSpoken(apple, SpeechLevel.Great, LearnSource.Quest);
    Assert.AreEqual(MasteryStage.Recognized, learning.GetStage(apple), "3/4 spoken must not reach Produced");
    learning.ReportSpoken(apple, SpeechLevel.Great, LearnSource.Quest);
    Assert.AreEqual(MasteryStage.Produced, learning.GetStage(apple));

    // Q4 no-visual correct use -> transfer: ContextUse+1 -> UsedInContext.
    learning.ReportContextUse(apple);
    Assert.AreEqual(1, learning.GetMastery(apple).ContextUse);
    Assert.AreEqual(MasteryStage.UsedInContext, learning.GetStage(apple));

    // Review passing -> Retained (ForceReviewDue stands in for elapsed time).
    learning.ForceReviewDue(apple);
    Assert.AreEqual(MasteryStage.Retained, learning.GetStage(apple));
  }
}
