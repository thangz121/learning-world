// CT-S16: USER ROUND 2026-09-29 (round 2) — the carrot arena's picture-answer
// rounds (3 boards, tap the right count) + the no-skipped-steps demo contract.
// C# 9.0 only.
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class CT_S16_RabbitQuizAndDemos {
  // A. Quiz pacing: first two plain rounds are physical; every 3rd after that
  // is a picture-answer round (pure mapping).
  [Test] public void S16A_QuizPacing() {
    Assert.IsFalse(RabbitFeed.IsQuizTurn(0), "round 1 stays physical");
    Assert.IsFalse(RabbitFeed.IsQuizTurn(1), "round 2 stays physical");
    Assert.IsTrue(RabbitFeed.IsQuizTurn(2), "round 3 answers on the boards");
    Assert.IsFalse(RabbitFeed.IsQuizTurn(3));
    Assert.IsFalse(RabbitFeed.IsQuizTurn(4));
    Assert.IsTrue(RabbitFeed.IsQuizTurn(5), "every 3rd plain round after the first two");
  }

  // B. Answer counts: three DISTINCT boards, the target included, 1..9.
  [Test] public void S16B_QuizCounts() {
    for (int target = 1; target <= 9; target++) {
      for (int roll = 0; roll < 12; roll++) {
        int[] c = RabbitFeed.QuizCounts(target, roll);
        Assert.AreEqual(3, c.Length);
        bool hasTarget = c[0] == target || c[1] == target || c[2] == target;
        Assert.IsTrue(hasTarget, "the correct board is among the three (t=" + target + ")");
        Assert.AreNotEqual(c[0], c[1], "distinct boards");
        Assert.AreNotEqual(c[1], c[2], "distinct boards");
        Assert.AreNotEqual(c[0], c[2], "distinct boards");
        for (int i = 0; i < 3; i++)
          Assert.IsTrue(c[i] >= 1 && c[i] <= 9, "board counts stay 1..9");
      }
    }
  }

  // C. The no-skipped-steps demo contract + the quiz wiring (source pins: the
  // live beats need scenes).
  [Test] public void S16C_DemoAndQuizWiringPinned() {
    string rabbit = ReadRepoFile("A_World", "CountingGarden", "RabbitFeed.cs");
    StringAssert.Contains("BeginDemoCarry", rabbit,
      "the demo student carries bowl carrots out (subtraction flow)");
    StringAssert.Contains("DemoWalk", rabbit, "the demo student WALKS every step");
    StringAssert.Contains("_demoStage", rabbit, "the full action machine");
    StringAssert.Contains("TryAnswer", rabbit, "answer boards dispatch into the game");
    StringAssert.Contains("HideQuizBoards", rabbit, "boards leave with the round");
    StringAssert.Contains("PlainRoundsDone", rabbit, "quiz pacing counts plain rounds");
    string builder = ReadRepoFile("A_World", "CountingGarden", "RabbitPlayBuilder.cs");
    StringAssert.Contains("BuildQuizTiles", builder, "the three answer boards are built");
    StringAssert.Contains("RPAnswerPick", builder, "the boards keep a real click face");
    string stairs = ReadRepoFile("A_World", "CountingGarden", "NumberStairs.cs");
    StringAssert.Contains("DemoWalkTo(_demoAnchor", stairs,
      "stairs demo walks to the same start the child used (circle or step)");
    StringAssert.Contains("_demoSettleT >= 0.35f", stairs,
      "stairs demo settles on each step like the child must");
  }

  static string ReadRepoFile(params string[] parts) {
    string all = Application.dataPath;
    for (int i = 0; i < parts.Length; i++) all = Path.Combine(all, parts[i]);
    return File.ReadAllText(all);
  }
}
