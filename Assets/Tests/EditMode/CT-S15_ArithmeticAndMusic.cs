// CT-S15: USER ROUND 2026-09-29 — arithmetic pacing + demo discipline + the
// boot theme tune. Pins:
//  - the 80/12/8 roll mapping in BOTH approved games (arithmetic is rare);
//  - the Demo phase + its gates (thorough demo before EVERY + / - round, once
//    for the first plain round) at the source level;
//  - the persisted theme-music flag.
// C# 9.0 only.
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class CT_S15_ArithmeticAndMusic {
  // A. Ratio: 80% plain / 12% add / 8% sub (pure mapping, no randomness).
  [Test] public void S15A_ArithmeticRatio() {
    Assert.AreEqual(RabbitFeed.RoundKind.Plain, RabbitFeed.KindForRoll(0));
    Assert.AreEqual(RabbitFeed.RoundKind.Plain, RabbitFeed.KindForRoll(79));
    Assert.AreEqual(RabbitFeed.RoundKind.Add, RabbitFeed.KindForRoll(80));
    Assert.AreEqual(RabbitFeed.RoundKind.Add, RabbitFeed.KindForRoll(91));
    Assert.AreEqual(RabbitFeed.RoundKind.Sub, RabbitFeed.KindForRoll(92));
    Assert.AreEqual(RabbitFeed.RoundKind.Sub, RabbitFeed.KindForRoll(99));
    Assert.AreEqual(NumberStairs.RoundKind.Plain, NumberStairs.KindForRoll(0));
    Assert.AreEqual(NumberStairs.RoundKind.Plain, NumberStairs.KindForRoll(79));
    Assert.AreEqual(NumberStairs.RoundKind.Add, NumberStairs.KindForRoll(85));
    Assert.AreEqual(NumberStairs.RoundKind.Sub, NumberStairs.KindForRoll(99));
  }

  // B. Demo discipline: a Demo phase exists in both games and the gates are
  // where they must be (source-level pin — the live beats need scenes).
  [Test] public void S15B_DemoDisciplinePinned() {
    Assert.IsTrue(System.Enum.IsDefined(typeof(RabbitFeed.Phase), "Demo"),
      "rabbit has the demo phase");
    Assert.IsTrue(System.Enum.IsDefined(typeof(NumberStairs.Phase), "Demo"),
      "stairs has the demo phase");
    string rabbit = ReadRepoFile("A_World", "CountingGarden", "RabbitFeed.cs");
    StringAssert.Contains("arith || firstPlain", rabbit,
      "rabbit: demo before EVERY + / - round, once for the first plain round");
    StringAssert.Contains("_plainDemoShown", rabbit, "rabbit: plain demo runs once");
    StringAssert.Contains("ResetBowlToStart", rabbit, "rabbit: start state restored after the demo");
    string stairs = ReadRepoFile("A_World", "CountingGarden", "NumberStairs.cs");
    StringAssert.Contains("BeginDemo(Kind == RoundKind.Add", stairs,
      "stairs: demo before EVERY + / - round");
    StringAssert.Contains("!_plainDemoShown", stairs, "stairs: plain demo runs once");
    StringAssert.Contains("BeginDemo(true", stairs, "stairs: the plain demo is a climb");
  }

  // C. Theme music: on by default, toggleable, test seam without file IO.
  [Test] public void S15C_MusicSettings() {
    Assert.IsTrue(MusicSettings.On, "theme music is on by default");
    MusicSettings.SetForTests(false);
    Assert.IsFalse(MusicSettings.On, "toggle seam flips it");
    MusicSettings.SetForTests(true);
    Assert.IsTrue(MusicSettings.On, "and back");
  }

  static string ReadRepoFile(params string[] parts) {
    string all = Application.dataPath;
    for (int i = 0; i < parts.Length; i++) all = Path.Combine(all, parts[i]);
    return File.ReadAllText(all);
  }
}
