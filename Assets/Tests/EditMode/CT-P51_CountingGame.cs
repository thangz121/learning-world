// CT-P51: S3-P2Z4 REFERENCE GAMEPLAY — "ĐƯA ĐÚNG SỐ LƯỢNG VÀO RỔ".
// Pins the activity staging (board/cluster/basket/count/result/cams), the ball
// state machine + basket counting, the intro->play handover, the gentle wrong
// path (3rd ball returns home, count returns to 2), and the deterministic
// re-entry policy through the shared ActivityLifecycle.
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class CT_P51_CountingGame {
  static Transform FindDeep(Transform t, string name) {
    if (t == null) return null;
    if (t.name == name) return t;
    for (int i = 0; i < t.childCount; i++) {
      Transform f = FindDeep(t.GetChild(i), name);
      if (f != null) return f;
    }
    return null;
  }

  static float Dist2D(Vector3 a, Vector3 b) {
    float dx = a.x - b.x, dz = a.z - b.z;
    return Mathf.Sqrt(dx * dx + dz * dz);
  }

  static CountingPlayBuilder BuildArena(out GameObject arena) {
    arena = new GameObject("P51Arena");
    CountingPlayBuilder builder = arena.AddComponent<CountingPlayBuilder>();
    builder.BuildContent(arena.transform);
    return builder;
  }

  static CountingGame BuildGame(CountingPlayBuilder builder, Transform player, CountingDemo demo) {
    CountingGame game = builder.gameObject.AddComponent<CountingGame>();
    game.Build(demo, builder, player, player, null, null);
    return game;
  }

  // A. Reference staging: board "2", a NATURAL 5-ball cluster (not a test row),
  // a short basket loop, count display + result board, intro camera shots.
  [Test] public void P51A_ActivityStaging() {
    GameObject arena;
    CountingPlayBuilder builder = BuildArena(out arena);
    try {
      CountingGardenBuilder.DemoRefs r = builder.Activity;
      Assert.IsNotNull(r, "activity refs exposed");
      Assert.IsNotNull(r.Number, "number board digit built");
      Assert.IsNotNull(FindDeep(arena.transform, "CPBoardPanel"), "board panel");
      Assert.AreEqual(5, r.Balls.Count, "five balls to choose from (user: 5-7)");
      // Natural cluster: real 2D spread, never a straight row on one z.
      float minGap = 999f, maxDz = 0f;
      for (int i = 0; i < r.Balls.Count; i++) {
        Vector3 a = r.Balls[i].transform.localPosition;
        for (int j = i + 1; j < r.Balls.Count; j++) {
          Vector3 b = r.Balls[j].transform.localPosition;
          float gap = Dist2D(a, b);
          if (gap < minGap) minGap = gap;
          float dz = Mathf.Abs(a.z - b.z);
          if (dz > maxDz) maxDz = dz;
        }
      }
      Assert.Greater(minGap, 0.4f, "balls read as separate objects");
      Assert.Greater(maxDz, 0.6f, "the cluster is arranged in depth, not a straight row");
      // Basket: a short loop from the cluster, reachable in child-sized legs.
      Assert.IsNotNull(r.Basket, "basket built");
      float loop = Dist2D(r.Basket.localPosition, r.Center);
      Assert.Greater(loop, 1.5f, "balls -> basket is a real little walk");
      Assert.Less(loop, 4.5f, "and never a walking simulator");
      Assert.IsNotNull(builder.CountDisplay, "count display built");
      Assert.AreEqual(2, builder.CountPips.Length, "two count pips");
      Assert.IsTrue(builder.CountPips[0].activeSelf, "pip slots are visible from the start (empty grey)");
      Assert.IsFalse(r.Result.activeSelf, "result board hidden until success");
      Assert.IsNotNull(r.CamA, "intro shot A");
      Assert.IsNotNull(r.CamB, "intro shot B");
      Assert.IsNotNull(r.CamC, "intro shot C");
      // Acting layout: teacher beside the board, student beside the teacher,
      // both in front of it (the child sees them together with the number).
      Assert.Less(Dist2D(r.NpcStart, r.BoardPoint), 3.0f, "teacher near the board");
      Assert.Less(Dist2D(r.StudentStart, r.NpcStart), 3.0f, "student beside the teacher");
      Assert.Less(Dist2D(r.BallStand, r.Center), 1.6f, "student fetches at the cluster");
      Assert.Less(Dist2D(r.BasketStand, r.Basket.localPosition), 1.6f, "student places at the basket");
      // The builder itself stages NO NPCs/controllers (runtime components only).
      Assert.IsNull(arena.GetComponentInChildren<CountingDemo>(true), "no demo in the built scene");
      Assert.IsNull(arena.GetComponentInChildren<CountingGame>(true), "no game in the built scene");
      foreach (string n in new[] { "CGDemoNumber2", "CGDemoBall0", "CGDemoBasket" }) {
        Assert.IsNull(FindDeep(arena.transform, n), "old garden demo prop removed: " + n);
      }
    } finally { Object.DestroyImmediate(arena); }
  }

  // B. Ball + basket flow: pick refused during the intro, then 1 -> 2 -> SUCCESS
  // with pips, result board and the shared lifecycle completed.
  [Test] public void P51B_FlowAndSuccess() {
    GameObject arena;
    CountingPlayBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P51Player");
    try {
      CountingGame game = BuildGame(builder, player.transform, null);
      CountingBall b0 = game.BallAt(0);
      CountingBall b1 = game.BallAt(1);
      Assert.IsNotNull(b0, "balls are game objects");
      Assert.IsNotNull(b0.GetComponent<Collider>(), "balls are clickable");
      Assert.AreEqual(CountingBall.BallState.Grounded, b0.State, "starts grounded");
      game.TryPick(b0);
      Assert.IsNull(game.Carried, "no picking during the intro (control passes after)");
      game.SetPhaseForTests(CountingGame.Phase.FreePlay);
      game.TryPick(b0);
      Assert.AreEqual(b0, game.Carried, "ball picked");
      Assert.AreEqual(CountingBall.BallState.Carried, b0.State, "ball is carried");
      game.TryPick(b1);
      Assert.AreEqual(b0, game.Carried, "one ball at a time");
      game.TryPlace();
      Assert.AreEqual(1, game.Count, "first ball counted");
      Assert.AreEqual(1, game.PipCount, "one slot filled (the other stays an empty grey slot)");
      Assert.IsTrue(builder.CountPips[0].activeSelf, "pip 1 visible");
      Assert.IsTrue(builder.CountPips[1].activeSelf, "pip 2 visible as an empty slot");
      Assert.AreEqual(CountingBall.BallState.InBasket, b0.State, "ball went to the basket");
      game.TryPick(b1);
      game.TryPlace();
      Assert.AreEqual(2, game.Count, "second ball counted");
      Assert.AreEqual(2, game.PipCount, "both slots filled");
      Assert.IsTrue(builder.Activity.Result.activeSelf, "result board shows on success");
      Assert.AreEqual(CountingGame.Phase.Success, game.Current,
        "the goal is confirmed but the field stays open (a 3rd ball = gentle correction)");
      Assert.AreEqual(CountingBall.BallState.InBasket, b1.State, "both balls rest in the basket");
      Assert.AreEqual(CountingBall.BallState.Grounded, game.BallAt(2).State,
        "leftover balls stay pickable after the goal (no dead field)");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // C. Gentle wrong path: a 3rd ball triggers the counting correction; after
  // the teacher's beats the extra ball returns home and the count is 2 again.
  [Test] public void P51C_WrongPathIsGentle() {
    GameObject arena;
    CountingPlayBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P51PlayerW");
    try {
      CountingGame game = BuildGame(builder, player.transform, null);
      game.SetPhaseForTests(CountingGame.Phase.FreePlay);
      for (int i = 0; i < 3; i++) {
        game.TryPick(game.BallAt(i));
        game.TryPlace();
      }
      Assert.AreEqual(3, game.Count, "three balls were placed");
      Assert.AreEqual(CountingGame.Phase.Wrong, game.Current, "wrong path engaged");
      Assert.AreEqual(CountingBall.BallState.InBasket, game.BallAt(2).State,
        "the extra ball visibly lands in the basket first");
      // Tick the teacher's correction beats (deterministic timer).
      for (int i = 0; i < 140; i++) game.TickWrongForTests(0.1f);
      Assert.AreEqual(2, game.Count, "count returns to the board's number");
      Assert.AreEqual(CountingGame.Phase.Completed, game.Current, "correction completes the activity");
      Assert.IsTrue(builder.Activity.Result.activeSelf, "result confirmed after the correction");
      Assert.AreNotEqual(CountingBall.BallState.InBasket, game.BallAt(2).State,
        "the extra ball left the basket (flying home)");
      Assert.AreEqual(1, game.WrongCount, "one gentle correction recorded");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // D. Intro handover: with LoopForever=false the lesson runs ONCE, then the
  // actors observe and the game accepts the child's input.
  [Test] public void P51D_IntroHandsOverControl() {
    GameObject arena;
    CountingPlayBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P51PlayerI");
    try {
      CountingDemo demo = arena.AddComponent<CountingDemo>();
      demo.LoopForever = false;
      demo.CameraBeatsEnabled = false;
      demo.Build(builder, player.transform, null, null);
      CountingGame game = BuildGame(builder, player.transform, demo);
      int guard = 0;
      while (!demo.IntroDone && guard < 4000) { demo.Step(0.1f); guard++; }
      Assert.IsTrue(demo.IntroDone, "the lesson finishes (no endless loop)");
      Assert.AreEqual(CountingGame.Phase.FreePlay, game.Current,
        "the intro completion handed control to the child exactly once");
      for (int i = 0; i < 400; i++) demo.Step(0.1f);
      Assert.AreEqual(1, demo.LoopCount, "the intro never re-loops in the arena");
      Assert.IsTrue(game.IntroDone, "game is in play mode");
      Assert.AreEqual(CountingBall.BallState.Grounded, game.BallAt(0).State,
        "balls are back home for the child's turn");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // E. Re-entry policy: a completed lifecycle adopts the finished picture
  // (2 balls in the basket, pips + result on, intro skipped) — deterministic.
  [Test] public void P51E_ReEntryAdoptsCompleted() {
    GameObject arena;
    CountingPlayBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P51PlayerR");
    try {
      ActivityLifecycle life = new ActivityLifecycle("counting_game", "test");
      life.MarkAvailable("test");
      life.BeginEnter("test");
      life.MarkReady("test");
      life.Begin("test");
      life.MarkCompleted("test");
      CountingDemo demo = arena.AddComponent<CountingDemo>();
      demo.LoopForever = false;
      demo.CameraBeatsEnabled = false;
      demo.Build(builder, player.transform, null, null);
      CountingGame game = arena.AddComponent<CountingGame>();
      game.Build(demo, builder, player.transform, player.transform, life, null);
      Assert.AreEqual(CountingGame.Phase.Completed, game.Current, "adopts completed");
      Assert.IsTrue(demo.IntroDone, "no intro replay on re-entry");
      Assert.AreEqual(2, game.Count, "the finished count is shown");
      Assert.AreEqual(2, game.PipCount, "adopted pips filled");
      Assert.IsTrue(builder.Activity.Result.activeSelf, "result tick visible");
      Assert.AreEqual(CountingBall.BallState.InBasket, game.BallAt(0).State, "ball 1 in basket");
      Assert.AreEqual(CountingBall.BallState.InBasket, game.BallAt(1).State, "ball 2 in basket");
      for (int i = 2; i < game.BallCount; i++) {
        Assert.AreEqual(CountingBall.BallState.Removed, game.BallAt(i).State,
          "no mystery leftover balls");
      }
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // F. Acting layout is DATA: the arena intro uses the arena's stations, not
  // the garden's straight-row constants (the reference must not be nailed to
  // the old test row).
  [Test] public void P51F_LayoutIsDataDriven() {
    GameObject arena;
    CountingPlayBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P51PlayerL");
    try {
      CountingGardenBuilder.DemoRefs r = builder.Activity;
      Assert.Greater(Dist2D(r.NpcStart, CountingGardenBuilder.DemoNpcStart), 3f,
        "arena teacher station differs from the garden stage");
      Assert.Greater(Dist2D(r.Basket.localPosition, CountingGardenBuilder.DemoBasketStand), 3f,
        "arena basket differs from the garden stage");
      CountingDemo demo = arena.AddComponent<CountingDemo>();
      demo.LoopForever = false;
      demo.CameraBeatsEnabled = false;
      demo.Build(builder, player.transform, null, null);
      for (int i = 0; i < 40; i++) demo.Step(0.1f);
      Transform student = FindDeep(arena.transform, "CGDemoStudent");
      Assert.IsNotNull(student, "student actor built");
      Assert.Less(Dist2D(student.localPosition, r.StudentStart), 1.0f,
        "student starts at the ARENA station");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }
}
