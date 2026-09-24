// CT-P51: S3-P2Z4 REFERENCE GAMEPLAY â€” "ÄÆ¯A ÄÃšNG Sá» LÆ¯á»¢NG VÃ€O Rá»”".
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
      Assert.IsNull(game.Carried, "no picking before the assignment was heard (listen circle)");
      game.MarkTaskToldForTests();
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
      game.MarkTaskToldForTests();
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

  // D. S3-P2Z9 (user order): the arena NEVER replays the demo â€” the child came
  // to play. The game starts in FreePlay with the actors observing, and the
  // assignment ("Ä‘á» bÃ i") is announced only once the child reaches the field.
  [Test] public void P51D_NoIntro_TaskAnnouncedOnApproach() {
    GameObject arena;
    CountingPlayBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P51PlayerI");
    // The child spawns at the arena door, FAR from the play field. (In the test
    // the arena root sits at the origin â€” the +180 island offset is applied by
    // GameInstaller at runtime â€” so the player uses plain local-space coords.)
    player.transform.position = new Vector3(0f, 0f, -3f);
    try {
      CountingDemo demo = arena.AddComponent<CountingDemo>();
      demo.LoopForever = false;
      demo.CameraBeatsEnabled = false;
      demo.NoIntroMode = true;
      P51FakeAudio audio = new P51FakeAudio();
      demo.Build(builder, player.transform, null, audio);
      CountingGame game = arena.AddComponent<CountingGame>();
      game.Build(demo, builder, player.transform, player.transform, null, audio);
      Assert.IsTrue(demo.IntroDone, "no intro: the actors already observe");
      Assert.AreEqual(CountingGame.Phase.FreePlay, game.Current,
        "the child may play immediately");
      for (int i = 0; i < 200; i++) demo.Step(0.1f);
      game.TickForTests(0.05f);
      Assert.AreEqual(0, demo.LoopCount, "the demo NEVER replays in the arena");
      Assert.IsFalse(game.TaskTold, "no assignment read while the child is at the door");
      Assert.AreEqual(0, audio.Lines.Count, "silence until the child walks to the field");
      // Walk to the play field (between the balls).
      player.transform.position = builder.Activity.BallStand;
      for (int i = 0; i < 20; i++) { game.TickForTests(0.05f); demo.Step(0.05f); }
      Assert.IsTrue(game.TaskTold, "the assignment is read once the child reaches the field");
      Assert.GreaterOrEqual(audio.Lines.Count, 1, "the teacher speaks the task");
      bool ok = SafetyFilter.ValidateLine(audio.Lines[0], false, out string why);
      Assert.IsTrue(ok, "task line passes SafetyFilter ('" + audio.Lines[0] + "' " + why + ")");
      // The second beat arrives after the speech pacer had room to breathe.
      for (int i = 0; i < 90; i++) { game.TickForTests(0.05f); demo.Step(0.05f); }
      Assert.GreaterOrEqual(audio.Lines.Count, 2, "the board line follows the assignment");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  sealed class P51FakeAudio : IAudioDirector {
    public readonly List<string> Lines = new List<string>();
    public readonly List<string> Sfx = new List<string>();
    public System.Threading.Tasks.Task PlayVocabularyAsync(WordId wordId, VocabularyAudioMode mode) {
      return System.Threading.Tasks.Task.CompletedTask;
    }
    public System.Threading.Tasks.Task SpeakAsync(DialogueRequest request) {
      Lines.Add(request.Text);
      return System.Threading.Tasks.Task.CompletedTask;
    }
    public void PlaySfx(SfxId id) { Sfx.Add(id.Value); }
    public void PlayMusic(MusicId id) { }
    public void SetAudioFocus(AudioFocusMode mode) { }
  }

  // E. Re-entry policy: a completed lifecycle adopts the finished picture
  // (2 balls in the basket, pips + result on, intro skipped) â€” deterministic.
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
      Assert.Greater(Vector3.Distance(game.BallAt(0).transform.localPosition,
        game.BallAt(1).transform.localPosition), 0.3f,
        "each ball has its own designed slot (readable count, no pile)");
      // The VISUAL is the finished picture: the adopted balls must actually
      // REST at the basket, not just claim the state (the demo's actor reset
      // used to move them home again â€” the journey saw an empty basket).
      Vector3 basketLocal = builder.Activity.Basket.localPosition;
      for (int i = 0; i < 2; i++) {
        Vector3 p = game.BallAt(i).transform.localPosition;
        float dx = p.x - basketLocal.x, dz = p.z - basketLocal.z;
        Assert.Less(Mathf.Sqrt(dx * dx + dz * dz), 0.5f,
          "adopted ball " + i + " visually rests in the basket");
      }
      for (int i = 2; i < game.BallCount; i++) {
        Assert.AreEqual(CountingBall.BallState.Removed, game.BallAt(i).State,
          "no mystery leftover balls");
      }
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // G. S3-P2Z10 (user brief Â§5/Â§8): the CHILD's body performs the pickup and
  // the place â€” the player controller carries the real PickUp/Victory clips
  // from the player's own rig, and the gameplay calls them. Source-level pin
  // (the controller asset + the call sites), so a silent regression cannot
  // strip the actions back to a floating ball.
  [Test] public void P51G_PlayerActsPickupAndPlace() {
    string root = Application.dataPath;
    string controller = System.IO.File.ReadAllText(System.IO.Path.Combine(root,
      "A_World", "Visuals", "Resources", "PlayerVisuals", "PlayerController.controller"));
    Assert.IsTrue(controller.Contains("m_Name: PickUp"), "player controller has a PickUp state");
    Assert.IsTrue(controller.Contains("m_Name: Victory"), "player controller has a Victory state");
    // The clips are the player rig's own (Player_CasualMale.fbx guid).
    Assert.IsTrue(controller.Contains("fileID: 7022811679865794533, guid: 0f9afea9c7741bb40ad72bce8462225d"),
      "PickUp state plays the player rig's PickUp clip");
    Assert.IsTrue(controller.Contains("fileID: 5008348457023331957, guid: 0f9afea9c7741bb40ad72bce8462225d"),
      "Victory state plays the player rig's Victory clip");
    Assert.IsTrue(controller.Contains("m_Name: PickUp\n    m_Type: 9"),
      "PickUp is a trigger parameter");
    RuntimeAnimatorController loaded =
      Resources.Load<RuntimeAnimatorController>("PlayerVisuals/PlayerController");
    Assert.IsNotNull(loaded, "the player controller asset still imports (YAML valid)");
    // The clip references must RESOLVE (a wrong fileID would leave the state
    // with a null motion and the child would stand still through the bend).
    bool pickClip = false, victoryClip = false;
    foreach (AnimationClip clip in loaded.animationClips) {
      if (clip == null) continue;
      if (clip.name != null && clip.name.IndexOf("PickUp", System.StringComparison.Ordinal) >= 0) pickClip = true;
      if (clip.name != null && clip.name.IndexOf("Victory", System.StringComparison.Ordinal) >= 0) victoryClip = true;
    }
    Assert.IsTrue(pickClip, "the PickUp state's clip resolves on the player rig");
    Assert.IsTrue(victoryClip, "the Victory state's clip resolves on the player rig");
    string playerVisual = System.IO.File.ReadAllText(System.IO.Path.Combine(root,
      "A_World", "PlayerVisual.cs"));
    Assert.IsTrue(playerVisual.Contains("public void PlayPickup()"), "PlayerVisual exposes PlayPickup");
    Assert.IsTrue(playerVisual.Contains("public void PlayVictory()"), "PlayerVisual exposes PlayVictory");
    Assert.IsTrue(playerVisual.Contains("public void FaceTowards("), "PlayerVisual can face the target");
    string game = System.IO.File.ReadAllText(System.IO.Path.Combine(root,
      "A_World", "CountingGarden", "CountingGame.cs"));
    Assert.IsTrue(game.Contains("_viz.PlayPickup()"), "the gameplay drives the pickup/place action");
    Assert.IsTrue(game.Contains("ball.BeginCarry(PickDelay)"), "the ball waits for the child's bend");
    Assert.IsTrue(game.Contains("ball.BeginPlace(BasketSlot(Count - 1), PlaceDelay)"),
      "the ball rides the hand and drops into the slot");
  }

  // H. S3-P2Z10 action timing: the ball does NOT snap â€” it waits out the bend
  // (pick delay), then travels to the hand; on place it rides the hand while
  // the child reaches and only then drops into its slot. Feedback (pickup pop,
  // basket plop) follows the real moments.
  [Test] public void P51H_ActionTimingAndFeedback() {
    GameObject arena;
    CountingPlayBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P51PlayerA");
    try {
      CountingDemo demo = arena.AddComponent<CountingDemo>();
      demo.LoopForever = false;
      demo.CameraBeatsEnabled = false;
      demo.NoIntroMode = true;
      P51FakeAudio audio = new P51FakeAudio();
      demo.Build(builder, player.transform, null, audio);
      CountingGame game = arena.AddComponent<CountingGame>();
      game.Build(demo, builder, player.transform, player.transform, null, audio);
      Assert.AreEqual(CountingGame.Phase.FreePlay, game.Current, "play mode");
      game.MarkTaskToldForTests();

      CountingBall b0 = game.BallAt(0);
      Vector3 home = b0.transform.localPosition;
      game.TryPick(b0);
      Assert.AreEqual(CountingBall.BallState.Carried, b0.State, "picked immediately (state)");
      Assert.Contains("pickup", audio.Sfx, "the pickup pop plays with the action");
      b0.TickForTests(0.2f);
      Assert.Less(Vector3.Distance(b0.transform.localPosition, home), 0.01f,
        "the ball waits while the child bends down (no snap)");
      for (int i = 0; i < 30; i++) b0.TickForTests(0.05f);
      Assert.Less(Vector3.Distance(b0.transform.position, player.transform.position), 0.6f,
        "the ball then travels into the hand");

      game.TryPlace();
      Assert.AreEqual(1, game.Count, "the place counts at the action");
      b0.TickForTests(0.2f);
      Assert.AreEqual(CountingBall.BallState.InBasket, b0.State, "state is in-basket during the reach");
      for (int i = 0; i < 30; i++) b0.TickForTests(0.05f);
      Assert.Contains("basket", audio.Sfx, "the basket plop plays when the ball lands");
      Vector3 basket = builder.Activity.Basket.localPosition;
      float dx = b0.transform.localPosition.x - basket.x;
      float dz = b0.transform.localPosition.z - basket.z;
      Assert.Less(Mathf.Sqrt(dx * dx + dz * dz), 0.5f, "the ball settled inside the basket");

      // Second ball: the success beat is hung on the landing, not the click.
      CountingBall b1 = game.BallAt(1);
      game.TryPick(b1);
      for (int i = 0; i < 30; i++) b1.TickForTests(0.05f);
      game.TryPlace();
      Assert.AreEqual(CountingGame.Phase.Success, game.Current, "goal met at the action");
      for (int i = 0; i < 30; i++) b1.TickForTests(0.05f);
      for (int i = 0; i < 4; i++) game.TickForTests(0.05f);
      Assert.Contains("success", audio.Sfx, "the success chime plays once the ball lands");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // I. S3-P2Z10 journey bug: the no-intro handover must leave the shared
  // lifecycle ACTIVE (not Ready), or MarkCompleted silently fails and every
  // re-entry starts the activity from zero. This pins the real boot order
  // (fresh lifecycle -> NoIntroMode arena -> play -> Completed -> re-entry).
  [Test] public void P51I_LifecycleSettlesCompletedForReEntry() {
    GameObject arena;
    CountingPlayBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P51PlayerLC");
    ActivityLifecycle life = new ActivityLifecycle("counting_game", "test");
    try {
      CountingDemo demo = arena.AddComponent<CountingDemo>();
      demo.LoopForever = false;
      demo.CameraBeatsEnabled = false;
      demo.NoIntroMode = true;
      demo.Build(builder, player.transform, null, null);
      CountingGame game = arena.AddComponent<CountingGame>();
      game.Build(demo, builder, player.transform, player.transform, life, null);
      Assert.AreEqual(ActivityState.Active, life.State,
        "the arena handover leaves the lifecycle Active (Ready would block completion)");
      game.MarkTaskToldForTests();
      game.TryPick(game.BallAt(0));
      game.TryPlace();
      game.TryPick(game.BallAt(1));
      game.TryPlace();
      Assert.AreEqual(CountingGame.Phase.Success, game.Current, "goal met");
      Assert.AreEqual(ActivityState.Completed, life.State, "the goal settles the shared lifecycle");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
    // A second visit (the arena scene reloaded) adopts the finished picture.
    GameObject arena2;
    CountingPlayBuilder b2 = BuildArena(out arena2);
    GameObject player2 = new GameObject("P51PlayerLC2");
    try {
      CountingDemo demo2 = arena2.AddComponent<CountingDemo>();
      demo2.LoopForever = false;
      demo2.CameraBeatsEnabled = false;
      demo2.NoIntroMode = true;
      demo2.Build(b2, player2.transform, null, null);
      CountingGame game2 = arena2.AddComponent<CountingGame>();
      game2.Build(demo2, b2, player2.transform, player2.transform, life, null);
      Assert.AreEqual(CountingGame.Phase.Completed, game2.Current, "re-entry adopts completed");
      Assert.AreEqual(2, game2.Count, "the finished count is shown again");
    } finally {
      Object.DestroyImmediate(player2);
      Object.DestroyImmediate(arena2);
    }
  }

  // F. Acting layout is DATA: the arena intro uses the arena's stations, not
  // the garden's straight-row constants (the reference must not be nailed to
  // the old test row).
  [Test] public void P51F_LayoutIsDataDriven() {
    GameObject arena;
    CountingPlayBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P51PlayerL");
    player.transform.position = CountingPlayBuilder.WorldOffset + new Vector3(0f, 0f, -3f);
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

  // J. S3-P2Z11 (user order): a FIXED listen circle gates the play — the
  // assignment is read only when the child stands on it, the balls refuse
  // clicks until then (with a gentle call-back, never a dead silent click),
  // and the circle rings + unlocks the play once it fired.
  [Test] public void P51J_ListenCircleGatesPlay() {
    GameObject arena;
    CountingPlayBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P51PlayerListen");
    player.transform.position = new Vector3(0f, 0f, -3f);
    try {
      Assert.IsNotNull(builder.ListenPad, "listen pad built");
      Assert.IsNotNull(builder.ListenRing, "listen ring built");
      Vector3 pad = builder.ListenPad.transform.localPosition;
      Assert.Less(Dist2D(pad, CountingPlayBuilder.ListenLocal), 0.2f,
        "the circle is the authored fixed spot");
      Assert.Less(pad.z, builder.Activity.Center.z - 0.4f, "the circle is before the field");
      Assert.Greater(pad.z, CountingPlayBuilder.EntryLocal.z + 2f, "and a real walk from the door");

      CountingDemo demo = arena.AddComponent<CountingDemo>();
      demo.LoopForever = false;
      demo.CameraBeatsEnabled = false;
      demo.NoIntroMode = true;
      P51FakeAudio audio = new P51FakeAudio();
      demo.Build(builder, player.transform, null, audio);
      CountingGame game = arena.AddComponent<CountingGame>();
      game.Build(demo, builder, player.transform, player.transform, null, audio);

      // Clicking a ball before hearing the assignment: refused + called back.
      game.TryPick(game.BallAt(0));
      Assert.IsNull(game.Carried, "balls are locked until the assignment is heard");
      Assert.IsFalse(game.TaskTold, "nothing is read while the child is at the door");
      for (int i = 0; i < 8; i++) { game.TickForTests(0.05f); demo.Step(0.05f); }
      Assert.GreaterOrEqual(audio.Lines.Count, 1, "the teacher calls the child to the circle");
      Assert.IsTrue(audio.Lines[0].IndexOf("circle", System.StringComparison.OrdinalIgnoreCase) >= 0,
        "the call-back names the circle ('" + audio.Lines[0] + "')");

      // Stand on the circle: the assignment fires, the circle rings, play opens.
      player.transform.position = builder.ListenPad.transform.position;
      for (int i = 0; i < 8; i++) { game.TickForTests(0.05f); demo.Step(0.05f); }
      Assert.IsTrue(game.TaskTold, "standing on the circle reads the assignment");
      Assert.Contains("ding", audio.Sfx, "the circle rings when it unlocks");
      game.TryPick(game.BallAt(0));
      Assert.IsNotNull(game.Carried, "after hearing the question the child plays");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }
}

