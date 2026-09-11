// CT-004: idle 8.0s->L1, 15.0s->L2, stuck->L3/L4; wrong 3/5/7/9. Owner: Agent B (W0-T1 HintService).
using NUnit.Framework;
using System.Collections.Generic;

public class CT_004_HintTimerFreeze {
  [Test] public void CT_004() {
    var bus = new GameEventBus();
    var hints = new HintService(bus);
    var levels = new List<int>();
    bus.Subscribe<HintLevelChanged>(e => levels.Add(e.Level));

    // Idle freeze: 8.0s -> L1 visual, 15.0s -> L2 point.
    var qIdle = new QuestId("quest_idle");
    hints.Tick(qIdle, 8f, 8f);
    Assert.AreEqual(1, hints.GetState(qIdle).Level, "totalIdle 8.0s must raise L1");
    hints.Tick(qIdle, 7f, 15f);
    Assert.AreEqual(2, hints.GetState(qIdle).Level, "totalIdle 15.0s must raise L2");

    // Below freeze: no level, no publish.
    var qCalm = new QuestId("quest_calm");
    hints.Tick(qCalm, 5f, 5f);
    Assert.AreEqual(0, hints.GetState(qCalm).Level);

    // Wrong ladder: 3->L1, 5->L2, 7->L3, 9->L4.
    var qWrong = new QuestId("quest_wrong");
    for (int i = 0; i < 2; i++) hints.ReportWrong(qWrong);
    Assert.AreEqual(0, hints.GetState(qWrong).Level, "2 wrongs must stay L0");
    hints.ReportWrong(qWrong); // 3
    Assert.AreEqual(1, hints.GetState(qWrong).Level);
    hints.ReportWrong(qWrong); // 4
    Assert.AreEqual(1, hints.GetState(qWrong).Level);
    hints.ReportWrong(qWrong); // 5
    Assert.AreEqual(2, hints.GetState(qWrong).Level);
    hints.ReportWrong(qWrong); // 6
    hints.ReportWrong(qWrong); // 7
    Assert.AreEqual(3, hints.GetState(qWrong).Level);
    hints.ReportWrong(qWrong); // 8
    Assert.AreEqual(3, hints.GetState(qWrong).Level);
    hints.ReportWrong(qWrong); // 9
    Assert.AreEqual(4, hints.GetState(qWrong).Level);

    // Explicit stuck path: Demo -> L3, Simplify -> L4, each published on change.
    var qStuck = new QuestId("quest_stuck");
    hints.Demo(qStuck);
    Assert.AreEqual(3, hints.GetState(qStuck).Level);
    hints.Simplify(qStuck);
    Assert.AreEqual(4, hints.GetState(qStuck).Level);

    // Every level change above published exactly its new level in order.
    var expected = new List<int> { 1, 2, 1, 2, 3, 4, 3, 4 };
    CollectionAssert.AreEqual(expected, levels, "HintLevelChanged must fire once per change with the new level");
  }
}
