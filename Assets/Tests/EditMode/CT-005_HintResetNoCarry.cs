// CT-005: Reset(newQuest) clears Level/WrongCount (no carry). Owner: Agent B (W0-T1).
using NUnit.Framework;

public class CT_005_HintResetNoCarry {
  [Test] public void CT_005() {
    var bus = new GameEventBus();
    var hints = new HintService(bus);
    var q = new QuestId("market_help_mia");

    hints.ReportWrong(q);
    hints.ReportWrong(q);
    hints.ReportWrong(q);
    hints.Tick(q, 15f, 15f);
    Assert.AreEqual(2, hints.GetState(q).Level, "precondition: quest earned L2");
    Assert.AreEqual(3, hints.GetState(q).WrongCount);

    hints.Reset(q);
    Assert.AreEqual(0, hints.GetState(q).Level, "Reset must clear Level");
    Assert.AreEqual(0, hints.GetState(q).WrongCount, "Reset must clear WrongCount");
    Assert.AreEqual(0f, hints.GetState(q).IdleSec, "Reset must clear IdleSec");

    // No carry: a single wrong after Reset must NOT restore L1 (ladder restarts).
    hints.ReportWrong(q);
    Assert.AreEqual(1, hints.GetState(q).WrongCount);
    Assert.AreEqual(0, hints.GetState(q).Level, "1 wrong on a fresh quest must stay L0");
    hints.ReportWrong(q);
    hints.ReportWrong(q);
    Assert.AreEqual(1, hints.GetState(q).Level, "fresh ladder reaches L1 again at 3 wrongs");
  }

  [Test] public void CT_005_ResetOneQuest_LeavesOthersUntouched() {
    var hints = new HintService(new GameEventBus());
    var q1 = new QuestId("quest_one");
    var q2 = new QuestId("quest_two");
    hints.ReportWrong(q1);
    hints.ReportWrong(q1);
    hints.ReportWrong(q1);
    hints.ReportWrong(q2);
    hints.Reset(q1);
    Assert.AreEqual(0, hints.GetState(q1).Level);
    Assert.AreEqual(1, hints.GetState(q2).WrongCount, "Reset(q1) must not touch q2");
  }
}
