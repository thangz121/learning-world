// CT-P53: S3-P2Z12 GAMEPLAY #2 — "BẬC THANG CON SỐ" (number stairs).
// Pins the garden's sixth plot (number-stair hill), the child-scale stair
// geometry + the deterministic step identity (StairRun), the lazy StairPlayScene
// contract, the full activity flow (teacher intro -> student demo -> handoff ->
// child climb -> success), backtracking (the count follows the feet), overshoot
// guidance (never a game over), deterministic re-entry (lifecycle adopt), and
// the new speech/SFX beats through the reference audio path.
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class CT_P53_StairGame {
  sealed class FakeOps : ISceneOps {
    public readonly HashSet<string> Loaded = new HashSet<string>();
    public bool FailLoads;
    public bool IsLoaded(string sceneName) { return Loaded.Contains(sceneName); }
    public Task LoadAdditiveAsync(string sceneName) {
      if (FailLoads) throw new System.InvalidOperationException("fake load failure");
      Loaded.Add(sceneName);
      return Task.CompletedTask;
    }
    public Task UnloadAsync(string sceneName) {
      Loaded.Remove(sceneName);
      return Task.CompletedTask;
    }
  }

  sealed class FakeAudio : IAudioDirector {
    public readonly List<string> Lines = new List<string>();
    public readonly List<string> Sfx = new List<string>();
    public Task PlayVocabularyAsync(WordId wordId, VocabularyAudioMode mode) {
      return Task.CompletedTask;
    }
    public Task SpeakAsync(DialogueRequest request) {
      Lines.Add(request.Text);
      return Task.CompletedTask;
    }
    public void PlaySfx(SfxId id) { Sfx.Add(id.Value); }
    public void PlayMusic(MusicId id) { }
    public void SetAudioFocus(AudioFocusMode mode) { }
  }

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

  static bool DistanceToSeg2D(Vector3 p, Vector3 a, Vector3 b, out float dist) {
    float abx = b.x - a.x, abz = b.z - a.z;
    float len2 = abx * abx + abz * abz;
    if (len2 < 0.0001f) { dist = Dist2D(p, a); return false; }
    float t = Mathf.Clamp01(((p.x - a.x) * abx + (p.z - a.z) * abz) / len2);
    dist = Dist2D(p, new Vector3(a.x + abx * t, 0f, a.z + abz * t));
    return true;
  }

  static CountingGardenBuilder BuildGarden(out GameObject garden) {
    garden = new GameObject("P53GardenWorld");
    CountingGardenBuilder builder = garden.AddComponent<CountingGardenBuilder>();
    builder.BuildContent(garden.transform);
    return builder;
  }

  static StairHillBuilder BuildArena(out GameObject arena) {
    arena = new GameObject("P53StairWorld");
    StairHillBuilder builder = arena.AddComponent<StairHillBuilder>();
    builder.BuildContent(arena.transform);
    return builder;
  }

  static Vector3 WorldStand(StairHillBuilder builder, int step) {
    return builder.Stairs.transform.TransformPoint(builder.Stairs.StandLocal(step));
  }

  static void AdvanceToClimb(NumberStairs game, float maxSeconds = 60f) {
    for (float t = 0f; t < maxSeconds && game.Current != NumberStairs.Phase.Climb; t += 0.1f)
      game.Tick(0.1f);
  }

  // Teleport-walk: the settle window (0.3s) commits each band exactly once.
  static void SettleTo(NumberStairs game, GameObject player, StairHillBuilder builder, int step) {
    player.transform.position = WorldStand(builder, step);
    for (int i = 0; i < 5; i++) game.Tick(0.1f);
  }

  // A. Garden: the sixth plot is the number-stair hill, with the bed contract
  // (border/pad/fence/anchor/gate) and the mini stair identity; the crescent
  // walk reaches its mouth and the existing plots are untouched.
  [Test] public void P53A_GardenStairPlot() {
    GameObject garden;
    CountingGardenBuilder builder = BuildGarden(out garden);
    try {
      Assert.AreEqual(6, CountingGardenBuilder.ZoneCount, "six plots");
      Assert.AreEqual(5, CountingGardenBuilder.StairZoneIndex, "the hill is the sixth plot");
      Vector3 center = builder.ZoneCenters[CountingGardenBuilder.StairZoneIndex];
      Vector3 outDir = (center - CountingGardenBuilder.ArcCenter).normalized;
      Vector3 mouth = center - outDir * 1.35f;
      // Bed contract (the picker + layout tests read one shape for every plot).
      foreach (string n in new[] { "CGZone5Border", "CGZone5Pad", "CGZone5Fence0",
          "CGZone5Fence6", "CGZone5Anchor", "CGZone5Post", "CGZone5Vignette" }) {
        Assert.IsNotNull(FindDeep(garden.transform, n), "stair plot piece " + n);
      }
      // The numbered gate post carries THREE beads (the arena's target), not six.
      for (int b = 0; b < 3; b++)
        Assert.IsNotNull(FindDeep(garden.transform, "CGZone5PostBead" + b), "target bead " + b);
      Assert.IsNull(FindDeep(garden.transform, "CGZone5PostBead3"), "no fourth bead on the post");
      // S3-P2Z12b: the diorama (mini lesson + board + steps) is a RUNTIME
      // component (StairLessonDemo, like the ball theatre's CountingDemo) — the
      // builder stages only the plot contract. Pinned by P53J below.
      Assert.IsNull(FindDeep(garden.transform, "CGStairDemoMiniRoot"),
        "no runtime demo in a plain BuildContent (staged by the installer)");
      // The crescent walk reaches the stair mouth (the child can walk there).
      float best = 999f;
      foreach (Transform t in garden.GetComponentsInChildren<Transform>(true)) {
        if (t == null || !t.name.StartsWith("CGPathCrescent")) continue;
        float d = Dist2D(t.localPosition, mouth);
        if (d < best) best = d;
      }
      Assert.Less(best, 3.0f, "the crescent walk passes the stair hill's mouth");
      // Existing east tree keeps its distance (the hill must not crowd it).
      // BlossomTree builds named PARTS (Trunk/Canopy*), the trunk carries the pos.
      Transform tree = FindDeep(garden.transform, "CGBlossomTree5Trunk");
      Assert.IsNotNull(tree, "east framing tree still there");
      Assert.Greater(Dist2D(tree.localPosition, center), 3.0f, "the hill never crowds the tree");
      // The hill fits the island (hedge r19).
      Assert.Less(Dist2D(center, CountingGardenBuilder.ArcCenter), 16f, "hill inside the island");
    } finally { Object.DestroyImmediate(garden); }
  }

  // B. Step identity: ONE deterministic function (StairRun) decides the current
  // step from a world position — bands tile, nothing is guessed by height, and
  // standing under/beside the stairs is never a phantom step.
  [Test] public void P53B_StairRunStepIdentity() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    try {
      StairRun run = builder.Stairs;
      Assert.IsNotNull(run, "the stair run is exposed");
      Assert.AreEqual(9, run.stepCount, "MAXIMUM = 9 physical steps (brief §2)");
      Assert.Greater(run.rise, 0.10f, "a real riser");
      Assert.Less(run.rise, 0.20f, "NHIỀU BẬC THẤP: no vaulting (brief §5)");
      Assert.Greater(run.tread, 0.35f, "deep enough to stand on a tread");
      Assert.Greater(run.width, 2.0f, "wide enough for a child + the camera read");
      // Every tread reports its own step (1..6).
      for (int i = 1; i <= run.stepCount; i++) {
        Assert.AreEqual(i, run.BandAt(WorldStand(builder, i)), "band " + i);
        Assert.AreEqual(i, run.StepAt(WorldStand(builder, i)), "step " + i);
      }
      // The landing reads stepCount+1 (overshoot above the target).
      Vector3 landing = arena.transform.TransformPoint(new Vector3(0f,
        run.stepCount * run.rise, run.baseZ + run.stepCount * run.tread + 1.0f));
      Assert.AreEqual(run.stepCount + 1, run.StepAt(landing), "top landing");
      // Ground in front, beside, and UNDER the stairs is never a step.
      Vector3 rootPos = arena.transform.position;
      Assert.AreEqual(0, run.StepAt(rootPos + new Vector3(0f, 0f, 1.0f)), "ground before the stairs");
      Assert.AreEqual(0, run.StepAt(rootPos + new Vector3(2.6f, 0f, 6.2f)), "beside the stairs");
      Assert.AreEqual(0, run.StepAt(rootPos + new Vector3(0f, 0f, 6.2f)),
        "under the stairs (same XZ as step 3, ground height)");
      Assert.AreEqual(0, run.StepAt(rootPos + new Vector3(0f, 0f, -8f)), "the entry walk");
      // Deterministic backtrack: 3 -> 1 reads 1 (no accumulation, brief §20).
      Assert.AreEqual(3, run.StepAt(WorldStand(builder, 3)));
      Assert.AreEqual(1, run.StepAt(WorldStand(builder, 1)));
      // Height check: exact tops pass, a body floating 1m above a tread does not.
      Vector3 floating = WorldStand(builder, 2) + new Vector3(0f, 1.0f, 0f);
      Assert.AreEqual(0, run.StepAt(floating), "no phantom step from a high body");
    } finally { Object.DestroyImmediate(arena); }
  }

  // C. Arena structure: separate island, own exit door (back to the garden),
  // the round's target board + hidden result digit, beads per tread (1..9),
  // landing + goal arch, and the runtime NavMesh discipline (overheads
  // bake-ignored).
  [Test] public void P53C_ArenaStructure() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    try {
      Assert.Greater(Vector3.Distance(StairHillBuilder.WorldOffset, CountingGardenBuilder.WorldOffset), 40f,
        "separate island from the garden");
      Assert.Greater(Vector3.Distance(StairHillBuilder.WorldOffset, CountingPlayBuilder.WorldOffset), 40f,
        "separate island from the ball arena");
      Assert.Greater(Vector3.Distance(StairHillBuilder.WorldOffset, MathWorldBuilder.WorldOffset), 40f,
        "separate island from Math");
      Assert.IsNotNull(builder.ExitPortal, "the hill has its way home");
      Assert.IsTrue(builder.ExitPortal.ExitMode, "it is an exit portal");
      Assert.IsTrue(builder.ExitPortal.PlayExit, "it returns to the GARDEN (not the Math hub)");
      Assert.AreEqual(CountingGardenArea.AreaId, builder.ExitPortal.areaId, "targets the garden area");
      MicroWorldPortal[] portals = arena.GetComponentsInChildren<MicroWorldPortal>(true);
      Assert.AreEqual(1, portals.Length, "one door only");
      float spawnClear = Vector2.Distance(
        new Vector2(builder.EntryPoint.localPosition.x, builder.EntryPoint.localPosition.z),
        new Vector2(builder.ExitPortal.transform.localPosition.x, builder.ExitPortal.transform.localPosition.z));
      Assert.GreaterOrEqual(spawnClear, builder.ExitPortal.fireRadius + builder.ExitPortal.rearmMargin,
        "entry spawn clears the exit re-arm radius (J4 lesson)");
      // Target board + hidden result digit (default round = 3).
      Assert.IsNotNull(FindDeep(arena.transform, "SHBoardPanel"), "board panel");
      Assert.IsNotNull(FindDeep(arena.transform, "SHNumberDigit"), "the target digit is staged");
      Assert.IsNotNull(builder.Result, "result board exists");
      Assert.IsFalse(builder.Result.activeSelf, "result hidden until the child stands on target");
      Assert.IsNotNull(FindDeep(arena.transform, "SHResultDigit"), "result digit staged");
      // Steps + tread caps + bead rows (N beads = the step's number).
      Assert.AreEqual(9, builder.StepCues.Length, "one cue per step");
      for (int i = 1; i <= 9; i++) {
        Assert.IsNotNull(FindDeep(arena.transform, "SHStep" + i), "step " + i);
        Assert.IsNotNull(FindDeep(arena.transform, "SHStepTop" + i), "tread cap " + i);
        Transform cue = FindDeep(arena.transform, "SHStepCue" + i);
        Assert.IsNotNull(cue, "bead row " + i);
        Assert.AreEqual(i, cue.childCount, "step " + i + " carries " + i + " beads");
      }
      Assert.IsNotNull(FindDeep(arena.transform, "SHLanding"), "top landing");
      Assert.IsNotNull(FindDeep(arena.transform, "SHGoalBeam"), "goal arch beam");
      // The treads ARE the click path: they must keep colliders, or clicks
      // fall through to the ground behind the hill (the child walks past).
      for (int i = 1; i <= 9; i++) {
        Transform step = FindDeep(arena.transform, "SHStep" + i);
        Assert.IsNotNull(step.GetComponent<Collider>(), "step " + i + " is clickable");
      }
      Assert.IsNotNull(FindDeep(arena.transform, "SHLanding").GetComponent<Collider>(),
        "the landing is clickable");
      // Overhead geometry over walk corridors is bake-ignored (headroom rule).
      foreach (string n in new[] { "SHEntryBeam", "SHGoalBeam" }) {
        Transform t = FindDeep(arena.transform, n);
        Component mod = null;
        try { mod = t.GetComponent("NavMeshModifier"); } catch (System.Exception) { }
        Assert.IsNotNull(mod, n + " carries a NavMeshModifier");
        System.Reflection.PropertyInfo p = mod.GetType().GetProperty("ignoreFromBuild");
        Assert.IsTrue((bool)p.GetValue(mod, null), n + " never cuts NavMesh headroom");
      }
      // Anchors: the arrival + the acting registry.
      ActivityAnchors a = builder.Anchors;
      Assert.IsNotNull(a, "anchor registry");
      foreach (Transform slot in new[] { a.Entry, a.GameplayFocus, a.Npc, a.Camera, a.CameraLook,
          a.Prompt, a.Feedback, a.Reward, a.Exit }) {
        Assert.IsNotNull(slot, "anchor slot present");
      }
      Assert.Less(Dist2D(a.GameplayFocus.localPosition,
        new Vector3(0f, 0f, StairHillBuilder.BaseZ + StairHillBuilder.Tread)), 0.1f,
        "focus = the stair foot");
      Assert.Less(Dist2D(a.Exit.localPosition, StairHillBuilder.ExitLocal), 0.1f, "exit = the door");
      // The builder stages NO controllers: the activity is a runtime component.
      Assert.IsNull(arena.GetComponentInChildren<NumberStairs>(true), "no activity in the built scene");
      Assert.IsNull(arena.GetComponentInChildren<CountingDemo>(true), "no ball demo here");
    } finally { Object.DestroyImmediate(arena); }
  }

  // D. Full flow: intro -> student demo (3 counted steps) -> handoff -> the
  // child's climb; the count follows the feet (1->2->1->3), and a stable stand
  // on step 3 settles the activity through the shared lifecycle.
  [Test] public void P53D_FlowClimbBacktrackSuccess() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P53Player");
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("number_stairs", "test");
      NumberStairs game = arena.AddComponent<NumberStairs>();
      game.Build(builder, player.transform, null, audio, life);
      Assert.AreEqual(NumberStairs.Phase.Intro, game.Current, "fresh entry plays the lesson");
      Assert.AreEqual(ActivityState.Ready, life.State, "staged until the handoff");
      AdvanceToClimb(game);
      Assert.AreEqual(NumberStairs.Phase.Climb, game.Current, "handoff reached");
      Assert.AreEqual(ActivityState.Active, life.State, "the child owns the input");
      Assert.AreEqual(3, game.DemoStepsClimbed, "the student demonstrated three steps");
      Assert.GreaterOrEqual(audio.Sfx.FindAll(s => s == "step").Count, 3, "each demo step sounded");
      // Climb: 1 -> 2 (count follows the feet) -> back to 1 -> up to 3.
      SettleTo(game, player, builder, 1);
      Assert.AreEqual(1, game.CurrentStep, "step 1");
      SettleTo(game, player, builder, 2);
      Assert.AreEqual(2, game.CurrentStep, "step 2");
      SettleTo(game, player, builder, 1);
      Assert.AreEqual(1, game.CurrentStep, "backtrack reads 1 (never 1+1+1)");
      Assert.AreEqual(NumberStairs.Phase.Climb, game.Current, "no accidental success");
      for (int i = 0; i < 30; i++) game.Tick(0.1f);
      Assert.AreEqual(NumberStairs.Phase.Climb, game.Current, "a long stand on step 1 never completes");
      // Step 3 + a stable dwell -> success.
      SettleTo(game, player, builder, 3);
      Assert.AreEqual(3, game.CurrentStep, "step 3");
      Assert.IsFalse(game.ResultShown, "settle alone never completes");
      for (int i = 0; i < 20 && game.Current != NumberStairs.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(NumberStairs.Phase.Success, game.Current, "stable stand settles the activity");
      Assert.IsTrue(game.ResultShown, "the 3-tick board is shown");
      Assert.AreEqual(ActivityState.Completed, life.State, "shared lifecycle completed");
      Assert.GreaterOrEqual(game.HighestStep, 3, "the climb was tracked");
      Assert.GreaterOrEqual(audio.Sfx.FindAll(s => s == "success").Count, 1, "success chime");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // E. Overshoot: step 4+ is guidance, never a game over — no completion, the
  // teacher calls the child back, and standing on 3 afterwards still succeeds.
  [Test] public void P53E_OvershootGuidance() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P53PlayerO");
    try {
      NumberStairs game = arena.AddComponent<NumberStairs>();
      ActivityLifecycle life = new ActivityLifecycle("number_stairs", "test");
      game.Build(builder, player.transform, null, new FakeAudio(), life);
      AdvanceToClimb(game);
      SettleTo(game, player, builder, 4);
      Assert.AreEqual(4, game.CurrentStep, "the body is on step 4");
      Assert.AreEqual(1, game.Overshoots, "guided once");
      Assert.AreEqual(NumberStairs.Phase.Climb, game.Current, "no fail, no reset (brief §19)");
      for (int i = 0; i < 30; i++) game.Tick(0.1f);
      Assert.AreEqual(NumberStairs.Phase.Climb, game.Current, "standing high never completes");
      Assert.IsFalse(game.ResultShown, "no result yet");
      // Return to the target: the same stand settles it.
      player.transform.position = WorldStand(builder, 3);
      for (int i = 0; i < 25 && game.Current != NumberStairs.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(NumberStairs.Phase.Success, game.Current, "coming back to 3 succeeds");
      Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle settles once");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // F. Lazy contract: the stair scene is a separate micro scene — never loaded
  // at boot, loaded only through the shared slot (one micro at a time), shipped
  // in Build Settings with its authored shell.
  [Test] public void P53F_LazySceneContract() {
    var ops = new FakeOps();
    var t = new WorldTransition(SubjectIds.Main);
    var math = new SubjectId("math");
    Assert.IsTrue(t.EnterAsync(ops, math, "MathScene").GetAwaiter().GetResult(), "subject loads");
    Assert.IsFalse(ops.Loaded.Contains(StairHillBuilder.SceneName),
      "the stair scene must NOT be loaded at subject entry (lazy)");
    Assert.IsTrue(t.EnterMicroAsync(ops, CountingGardenBuilder.SceneName).GetAwaiter().GetResult(),
      "garden loads first");
    Assert.IsFalse(t.EnterMicroAsync(ops, StairHillBuilder.SceneName).GetAwaiter().GetResult(),
      "the stair scene cannot stack onto the garden (one micro slot)");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "garden unloads");
    Assert.IsTrue(t.EnterMicroAsync(ops, StairHillBuilder.SceneName).GetAwaiter().GetResult(),
      "the stair scene loads into the freed slot");
    Assert.IsTrue(ops.Loaded.Contains(StairHillBuilder.SceneName), "it is now the live micro scene");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "and unloads cleanly");
    // The authored shell + its shipping registration (source-level pins).
    string scenePath = Path.Combine(Application.dataPath,
      "A_World", "CountingGarden", "StairPlayScene.unity");
    Assert.IsTrue(File.Exists(scenePath), "StairPlayScene.unity ships in the project");
    string yaml = File.ReadAllText(scenePath);
    Assert.IsTrue(yaml.Contains("StairPlayWorld"), "scene root is StairPlayWorld");
    Assert.IsTrue(yaml.Contains("StairHillBuilder"), "scene carries the stair builder");
    string buildSettings = File.ReadAllText(Path.Combine(
      Directory.GetParent(Application.dataPath).FullName, "ProjectSettings", "EditorBuildSettings.asset"));
    Assert.IsTrue(buildSettings.Contains("StairPlayScene.unity"),
      "the scene is registered in Build Settings (lazily loadable in the player)");
  }

  // G. Re-entry: a completed lifecycle adopts the finished picture (lesson
  // skipped, result board shown, actors observing) — deterministic, no replay.
  [Test] public void P53G_ReEntryAdoptsCompleted() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P53PlayerR");
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("number_stairs", "test");
      life.MarkAvailable("test");
      life.BeginEnter("test");
      life.MarkReady("test");
      life.Begin("test");
      life.MarkCompleted("test");
      NumberStairs game = arena.AddComponent<NumberStairs>();
      game.Build(builder, player.transform, null, audio, life);
      Assert.AreEqual(NumberStairs.Phase.Success, game.Current, "adopts the settled state");
      Assert.IsTrue(game.ResultShown, "the finished picture is shown");
      Assert.AreEqual(0, game.DemoStepsClimbed, "no demo replay");
      Assert.AreEqual(0, audio.Lines.Count, "no speech on adopt");
      for (int i = 0; i < 200; i++) game.Tick(0.1f);
      Assert.AreEqual(NumberStairs.Phase.Success, game.Current, "the lesson never replays on re-entry");
      Assert.AreEqual(0, game.DemoStepsClimbed, "and the student stays put");
      // The child may still climb freely on a re-entry (no lock, no re-check).
      player.transform.position = WorldStand(builder, 4);
      game.Tick(0.1f);
      Assert.AreEqual(0, game.Overshoots, "an adopted activity does not nag");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // J. Garden mini lesson (S3-P2Z12b, user report "NPC dạy trẻ chơi ở đâu?"):
  // the stair plot hosts the SAME two-NPC teaching miniature as the ball plot —
  // a scaled diorama with 3 chunky steps (beads 1..3), the "3" board, the goal
  // arch and both actors — and a focused run completes a pass, opens the panel
  // gate and resets when the focus is released.
  [Test] public void P53J_GardenMiniLesson() {
    GameObject garden;
    CountingGardenBuilder builder = BuildGarden(out garden);
    GameObject demoGo = new GameObject("P53StairDemo");
    try {
      demoGo.transform.SetParent(garden.transform, false);
      StairLessonDemo demo = demoGo.AddComponent<StairLessonDemo>();
      FakeAudio audio = new FakeAudio();
      demo.Build(builder, null, audio);
      Assert.IsTrue(demo.ActorsBuilt, "teacher + student built in the plot");
      Assert.IsNotNull(demo.MiniRoot, "diorama root built");
      Assert.Less(demo.MiniRoot.localScale.x, 1f, "the diorama is a miniature");
      Assert.AreEqual(StairLessonDemo.DemoSteps, 3, "the preview shows the goal (3 steps)");
      for (int i = 1; i <= StairLessonDemo.DemoSteps; i++) {
        Assert.IsNotNull(FindDeep(garden.transform, "CGStairMiniStep" + i), "mini step " + i);
        Assert.IsNotNull(FindDeep(garden.transform, "CGStairMiniStepTop" + i), "mini cap " + i);
        Transform cue = FindDeep(garden.transform, "CGStairMiniCue" + i);
        Assert.IsNotNull(cue, "mini bead row " + i);
        Assert.AreEqual(i, cue.childCount, "step " + i + " carries " + i + " beads");
        Assert.IsNull(FindDeep(garden.transform, "CGStairMiniStep" + i).GetComponent<Collider>(),
          "scenery is click-through (the plot keeps the garden's ground clicks)");
      }
      Assert.IsNotNull(FindDeep(garden.transform, "CGStairMiniBoard3"), "mini board digit");
      Assert.IsNotNull(FindDeep(garden.transform, "CGStairMiniGoalBeam"), "mini goal arch");
      // Colours must be REAL: a round-1 bug made the demo's colour fields
      // self-referential (Gold = Gold) and every "gold" prop rendered black.
      Transform bead = FindDeep(garden.transform, "CGStairMiniBead1_0");
      Assert.IsNotNull(bead, "bead built");
      Color beadColor = bead.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
      Assert.Greater(beadColor.r + beadColor.g + beadColor.b, 0.5f,
        "the counting bead is gold, not black (colour fields sane)");
      Transform digitSeg = FindDeep(garden.transform, "CGStairMiniBoard3A");
      Assert.IsNotNull(digitSeg, "digit segment built");
      Color digitColor = digitSeg.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
      Assert.Greater(digitColor.r + digitColor.g + digitColor.b, 0.5f,
        "the number 3 is gold, not black");
      Assert.IsTrue(FindDeep(garden.transform, "CGStairMiniTeacherVisual") != null
        || FindDeep(garden.transform, "CGStairMiniTeacher") != null, "teacher staged");
      // Focused run = the zone's try-run: a full pass, then the gate opens.
      Assert.IsFalse(demo.PassDone, "no pass before the run");
      demo.StartFocusedLesson();
      Assert.IsTrue(demo.Engaged, "focused run engaged");
      int guard = 0;
      while (demo.LoopCount < 1 && guard < 400) { demo.Step(0.1f); guard++; }
      Assert.GreaterOrEqual(demo.LoopCount, 1, "one full pass done");
      Assert.IsTrue(demo.PassDone, "the panel gate is opened");
      Assert.AreEqual(3, demo.StudentStep, "the student reached step 3");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Three.", "Ba.")),
        "the teacher counts the steps");
      Assert.GreaterOrEqual(audio.Sfx.FindAll(s => s == "step").Count, 3, "each step sounded");
      // Releasing the focus (panel Back / another plot) resets the stage.
      demo.StopFocusedLesson();
      Assert.IsFalse(demo.Engaged, "release stops the run");
      Assert.IsFalse(demo.FocusRun, "and clears the focused flag");
    } finally {
      Object.DestroyImmediate(demoGo);
      Object.DestroyImmediate(garden);
    }
  }

  // K. Zone-demo routing (S3-P2Z12b): the area resolves EACH staged plot's own
  // garden demo — zone 5's focus starts the stair lesson and its completion
  // opens the panel (the ball theatre keeps its own binding).
  [Test] public void P53K_ZoneDemoRouting() {
    GameObject garden;
    CountingGardenBuilder builder = BuildGarden(out garden);
    GameObject areaGo = new GameObject("P53AreaRoute");
    try {
      CountingGardenArea area = areaGo.AddComponent<CountingGardenArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      area.SetGarden(CountingGardenBuilder.WorldOffset + CountingGardenBuilder.EntryLocal,
        builder.Anchors, builder.ZoneSpots);
      FakeDemo stairDemo = new FakeDemo();
      FakeDemo ballDemo = new FakeDemo();
      area.BindDemo(CountingGardenBuilder.StairZoneIndex, stairDemo);
      area.BindDemo(2, ballDemo); // the ball theatre's own binding
      Assert.AreEqual(ballDemo, area.DemoFor(2), "zone 2 keeps the ball theatre demo");
      Assert.AreEqual(stairDemo, area.DemoFor(CountingGardenBuilder.StairZoneIndex),
        "zone 5 routes to the stair lesson demo");
      Assert.IsTrue(area.TryEnterForTests(), "enter the garden");
      area.FocusZone(CountingGardenBuilder.StairZoneIndex);
      Assert.IsTrue(stairDemo.Started, "the focused stair plot starts its demo");
      Assert.IsTrue(area.AwaitingDemo, "the panel waits for the try-run");
      stairDemo.LoopCount++;
      area.TickDemoGateForTests();
      Assert.IsFalse(area.AwaitingDemo, "the completed pass opens the gate");
      Assert.IsFalse(ballDemo.Started, "the ball demo is untouched by the stair focus");
      area.CancelFocus();
      Assert.IsTrue(stairDemo.Stopped, "leaving the plot releases the demo (voice cut + reset)");
    } finally {
      Object.DestroyImmediate(areaGo);
      Object.DestroyImmediate(garden);
    }
  }

  sealed class FakeDemo : IGardenZoneDemo {
    public int LoopCount { get; set; }
    public bool PassDone { get; set; }
    public bool Started { get; private set; }
    public bool Stopped { get; private set; }
    public void StartFocusedLesson() { Started = true; }
    public void StopFocusedLesson() { Stopped = true; }
  }

  // I. Operator window flag (S3-P2Z12 user order: "game nhỏ nhất, góc phải
  // dưới" on the demo machine): the placement is launch-flag driven, inert
  // without the flag, and uses the supported Screen API.
  [Test] public void P53I_WindowPlacementFlagInert() {
    string path = Path.Combine(Application.dataPath, "A_World", "WindowPlacement.cs");
    Assert.IsTrue(File.Exists(path), "WindowPlacement.cs ships");
    string src = File.ReadAllText(path);
    Assert.IsTrue(src.Contains("-window-bottom-right"), "the launch flag is documented");
    Assert.IsTrue(src.Contains("MoveMainWindowTo"), "uses the supported Screen API");
    Assert.IsTrue(src.Contains("if (!want) return;"), "inert unless the flag is passed");
    Assert.IsTrue(src.Contains("[RuntimeInitializeOnLoadMethod"), "applies after the first scene load");
  }

  // H. Speech: every new line passes the SafetyFilter (NPC cap = 6 words) in
  // both languages, and the recorded beats match the brief (board -> three ->
  // stairs -> counts -> turn -> success).
  [Test] public void P53H_SpeechAndSafety() {
    var pairs = new (string en, string vi)[] {
      ("Look at the board!", "Nhìn lên bảng nhé!"),
      ("This is number three.", "Đây là số ba."),
      ("Three.", "Ba."),
      ("Today, we climb three steps.", "Hôm nay leo ba bậc."),
      ("Let's count!", "Cùng đếm nhé!"),
      ("Watch your friend!", "Xem bạn làm nhé!"),
      ("One.", "Một."),
      ("Two.", "Hai."),
      ("Yes! Three steps!", "Đúng rồi! Ba bậc!"),
      ("Now it's your turn!", "Giờ đến lượt con!"),
      ("Climb three steps!", "Con lên ba bậc nhé!"),
      ("We only need three.", "Mình chỉ cần ba."),
      ("Come back to three!", "Quay lại bậc ba nhé!"),
      ("Three steps! Well done!", "Ba bậc! Giỏi!"),
    };
    foreach (var p in pairs) {
      bool okEn = SafetyFilter.ValidateLine(p.en, false, out string whyEn);
      Assert.IsTrue(okEn, "EN line passes SafetyFilter: '" + p.en + "' (" + whyEn + ")");
      bool okVi = SafetyFilter.ValidateLine(p.vi, false, out string whyVi);
      Assert.IsTrue(okVi, "VI line passes SafetyFilter: '" + p.vi + "' (" + whyVi + ")");
    }
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P53PlayerS");
    try {
      FakeAudio audio = new FakeAudio();
      NumberStairs game = arena.AddComponent<NumberStairs>();
      game.Build(builder, player.transform, null, audio, new ActivityLifecycle("number_stairs", "test"));
      AdvanceToClimb(game);
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("This is number three.", "Đây là số ba.")),
        "the teacher names the number at the board");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Today, we climb three steps.", "Hôm nay leo ba bậc.")),
        "the teacher links the number to the climb");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Now it's your turn!", "Giờ đến lượt con!")),
        "the handoff line is spoken");
      // Climb to the target and settle: the success line lands too.
      SettleTo(game, player, builder, 3);
      for (int i = 0; i < 30 && game.Current != NumberStairs.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(NumberStairs.Phase.Success, game.Current, "settled");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Three steps! Well done!", "Ba bậc! Giỏi!")),
        "the confirmation line is spoken");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }
}
