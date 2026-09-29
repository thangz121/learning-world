// CT-S14: FULL ARCHITECTURE RESET — PHASE 4 (C -> GAME -> C for the approved
// games). Pins: the re-homed lifecycles + target ladders on SelectionYardArea,
// the game-door launch guards (right yard / real game only), the play state
// seams, and the exit-portal preference that routes arena exits back to the
// Game Yard. The arenas' own gameplay tests (CT-P53/P54/P55) stay untouched.
// C# 9.0 only.
using NUnit.Framework;
using UnityEngine;

public class CT_S14_GameYardFlow {
  static SelectionYardArea BuildArea() {
    GameObject go = new GameObject("S14Area");
    SelectionYardArea area = go.AddComponent<SelectionYardArea>();
    area.Bind(null, null, null, null, null, Vector3.zero);
    return area;
  }

  // A. The two approved games resolve through the yard (scene names frozen).
  [Test] public void S14A_ApprovedGamesResolve() {
    Assert.AreEqual("RabbitPlayScene", LearningMap.Game("rabbit_feeding").SceneName);
    Assert.AreEqual("StairPlayScene", LearningMap.Game("number_stairs").SceneName);
    Assert.IsTrue(LearningMap.IsPlayable("rabbit_feeding"));
    Assert.IsTrue(LearningMap.IsPlayable("number_stairs"));
    Assert.AreEqual(2, LearningMap.GamesOf("math_counting").Length, "no third game");
  }

  // B. Game doors only launch IN their own yard, only real games.
  [Test] public void S14B_PlayGuards() {
    SelectionYardArea area = BuildArea();
    try {
      // Wrong level: refused (no request recorded).
      area.SetCurrentForTests(SelectionYardArea.YardLevel.Skill, "math", "");
      area.PlayGame("rabbit_feeding");
      Assert.AreEqual(0, area.PlayRequests, "skill yard never launches a game");
      // Right level, wrong skill: refused.
      area.SetCurrentForTests(SelectionYardArea.YardLevel.Game, "", "math_geometry");
      area.PlayGame("rabbit_feeding");
      Assert.AreEqual(0, area.PlayRequests, "another skill's game never launches here");
      // Unknown game: refused.
      area.SetCurrentForTests(SelectionYardArea.YardLevel.Game, "", "math_counting");
      area.PlayGame("nope");
      Assert.AreEqual(0, area.PlayRequests, "unknown game refused");
      // Right context: recorded; with no live refs it must NOT pretend to travel.
      area.PlayGame("number_stairs");
      Assert.AreEqual(1, area.PlayRequests, "counting game yard launches its games");
      Assert.AreEqual("number_stairs", area.LastPlayRequest, "request memorized");
      Assert.IsFalse(area.IsInPlay, "no live refs: no fake travel");
    } finally { Object.DestroyImmediate(area.gameObject); }
  }

  // C. Play state seams mirror the real guards.
  [Test] public void S14C_PlaySeams() {
    SelectionYardArea area = BuildArea();
    try {
      Assert.IsFalse(area.TryEnterPlayForTests(), "cannot play from no yard");
      area.SetCurrentForTests(SelectionYardArea.YardLevel.Game, "", "math_counting");
      Assert.IsTrue(area.TryEnterPlayForTests(), "game yard can launch");
      Assert.IsTrue(area.IsInPlay, "in play");
      area.ExitGameToYard(); // guard: no live refs, must be a safe no-op
      Assert.IsTrue(area.IsInPlay, "no refs: exit beat never fakes progress");
      Assert.IsTrue(area.TryExitPlayForTests(), "exit seam");
      Assert.IsFalse(area.IsInPlay);
    } finally { Object.DestroyImmediate(area.gameObject); }
  }

  // D. Stair ladder re-homed: default 3, advance only on a COMPLETED life.
  [Test] public void S14D_StairLadder() {
    SelectionYardArea area = BuildArea();
    try {
      Assert.AreEqual(3, area.StairTarget, "reference target first");
      area.TickStairProgressionForTests();
      Assert.AreEqual(3, area.StairTarget, "idle life never advances");
      Complete(area.StairLifecycle);
      area.TickStairProgressionForTests();
      Assert.AreEqual(5, area.StairTarget, "completed life advances 3 -> 5");
      Assert.AreNotEqual(ActivityState.Completed, area.StairLifecycle.State,
        "the next visit gets a fresh lesson life");
      Complete(area.StairLifecycle);
      area.TickStairProgressionForTests();
      Assert.AreEqual(7, area.StairTarget, "5 -> 7");
      Assert.AreEqual(3, SelectionYardArea.NextStairTarget(1), "1 loops back to 3");
      Assert.AreEqual(3, SelectionYardArea.NextStairTarget(42), "off-ladder rejoins at 3");
      Assert.AreEqual(5, SelectionYardArea.ParseStairTargetArg(
        new string[] { "-stair-target", "5" }, 3), "CLI pins the target");
      Assert.AreEqual(3, SelectionYardArea.ParseStairTargetArg(
        new string[] { "-stair-target", "99" }, 3), "invalid CLI stays inert");
    } finally { Object.DestroyImmediate(area.gameObject); }
  }

  // E. Rabbit ladder re-homed: deterministic ladder (tests keep it), 3 -> 4.
  [Test] public void S14E_RabbitLadder() {
    SelectionYardArea area = BuildArea();
    try {
      Assert.AreEqual(3, area.RabbitTarget, "reference target first");
      Assert.IsFalse(area.RandomRabbitTargets, "deterministic by default (tests)");
      Complete(area.RabbitLifecycle);
      area.TickRabbitProgressionForTests();
      Assert.AreEqual(4, area.RabbitTarget, "completed life advances 3 -> 4");
      Assert.AreEqual(9, SelectionYardArea.NextRabbitTarget(8), "8 -> 9");
      Assert.AreEqual(1, SelectionYardArea.NextRabbitTarget(9), "9 -> 1 breather");
      Assert.AreEqual(3, SelectionYardArea.NextRabbitTarget(2), "2 loops back to 3");
      Assert.AreEqual(3, SelectionYardArea.ParseRabbitTargetArg(
        new string[] { "-rabbit-target", "abc" }, 3), "invalid CLI stays inert");
    } finally { Object.DestroyImmediate(area.gameObject); }
  }

  // F. The arena's own exit portal prefers the Game Yard (the legacy garden
  // routing stays as the fallback for the old scenes until PHASE 5).
  [Test] public void S14F_ExitPortalPrefersYard() {
    GameObject portalGo = new GameObject("S14Portal");
    GameObject areaGo = null;
    try {
      MicroWorldPortal portal = portalGo.AddComponent<MicroWorldPortal>();
      portal.PlayExit = true;
      Assert.IsNull(portal.TargetArea(), "no target: inert portal");
      areaGo = new GameObject("S14YardForPortal");
      SelectionYardArea yard = areaGo.AddComponent<SelectionYardArea>();
      yard.Bind(null, null, null, null, null, Vector3.zero);
      portal.YardArea = yard;
      Assert.AreSame(yard, portal.TargetArea(), "the game yard takes the exit");
      // With no yard bound, the legacy garden slot still answers.
      portal.YardArea = null;
      GameObject gardenGo = new GameObject("S14GardenForPortal");
      try {
        CountingGardenArea garden = gardenGo.AddComponent<CountingGardenArea>();
        portal.Area = garden;
        Assert.AreSame(garden, portal.TargetArea(), "legacy fallback intact");
      } finally { Object.DestroyImmediate(gardenGo); }
    } finally {
      Object.DestroyImmediate(portalGo);
      if (areaGo != null) Object.DestroyImmediate(areaGo);
    }
  }

  static void Complete(ActivityLifecycle life) {
    life.MarkAvailable("t");
    life.Begin("t");
    life.MarkCompleted("t");
  }
}
