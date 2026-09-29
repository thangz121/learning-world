// CT-P62: GAMEPLAY #7 — "VƯỜN KHÁM PHÁ" (Discovery Garden).
// Pins the Math Hub discovery_garden gate (walk-in portal + approach glow +
// the shared IMicroWorldArea seam), the child-scale searchable arena (reference
// board + three pockets + distractors + result board + exit cue), the honest
// item lifecycle (Available -> Found, no carry), the full activity flow
// (teacher intro -> student REAL search with checks -> handoff -> three child
// tasks -> success), the gentle wrong path, duplicate/spam guards, the round
// tiers (core candidates -> full garden), re-entry adopt, the lazy scene
// contract, the ladder/CLI, and the recorded speech safety (both voices).
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class CT_P62_DiscoveryGarden {
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
    public void SetMusicEnabled(bool on) { }
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

  // The test assembly has no AI-Navigation reference by design (contract
  // firewall): probe the bake-ignore flag by component name + reflection.
  static bool IsIgnoredFromBuild(GameObject go) {
    if (go == null) return false;
    Component c = null;
    try { c = go.GetComponent("NavMeshModifier"); } catch (System.Exception) { }
    if (c == null) return false;
    System.Reflection.PropertyInfo p = c.GetType().GetProperty("ignoreFromBuild");
    if (p == null) return false;
    try { return (bool)p.GetValue(c, null); } catch (System.Exception) { return false; }
  }

  static DiscoveryBuilder BuildArena(out GameObject arena) {
    arena = new GameObject("P62DiscoveryWorld");
    DiscoveryBuilder builder = arena.AddComponent<DiscoveryBuilder>();
    builder.BuildContent(arena.transform);
    return builder;
  }

  static DiscoveryGame BuildGame(DiscoveryBuilder builder, GameObject arena,
      GameObject player, FakeAudio audio, ActivityLifecycle life, int round,
      System.Action<int> onCompleted = null) {
    DiscoveryGame game = arena.AddComponent<DiscoveryGame>();
    game.Build(builder, player.transform, null, audio, life, round, onCompleted, null);
    return game;
  }

  static GameObject BuildPlayer(Vector3 at) {
    GameObject player = new GameObject("P62Player");
    player.transform.position = at;
    return player;
  }

  static DiscoveryItem FindByKind(DiscoveryGame game, DiscoveryItem.ItemKind kind) {
    for (int i = 0; i < game.ItemCountTotal; i++) {
      DiscoveryItem it = game.ItemAt(i);
      if (it != null && it.Kind == kind && it.gameObject.activeSelf) return it;
    }
    return null;
  }

  static void AdvanceToTasks(DiscoveryGame game, float maxSeconds = 300f) {
    for (float t = 0f; t < maxSeconds && game.Current != DiscoveryGame.Phase.Tasks; t += 0.1f)
      game.Tick(0.1f);
  }

  // Find one task target the way the child does: interact, then let the
  // praise/advance beats breathe.
  static void FindOne(DiscoveryGame game, DiscoveryItem.ItemKind kind) {
    DiscoveryItem item = FindByKind(game, kind);
    Assert.IsNotNull(item, "an active " + kind + " candidate exists");
    game.TryInteract(item);
    Assert.AreEqual(DiscoveryItem.ItemState.Found, item.State, kind + " found");
    for (int i = 0; i < 20; i++) game.Tick(0.1f);
  }

  // A. Math Hub: the discovery_garden gate owns a walk-in portal (its OWN area
  // id) + the shared seam; the hub return clears the re-arm radius.
  [Test] public void P62A_GatePortalAndSeam() {
    GameObject root = new GameObject("P62MathWorld");
    try {
      MathWorldBuilder builder = root.AddComponent<MathWorldBuilder>();
      builder.BuildContent(root.transform);
      MicroWorldGate gate = builder.FindMicroGate("discovery_garden");
      Assert.IsNotNull(gate, "the discovery_garden gate exists");
      Assert.AreEqual("discovery_garden", gate.gateId, "the human-reviewed gate id intact");
      Assert.IsFalse(string.IsNullOrEmpty(gate.displayName), "the gate keeps its child-facing label");
      MicroWorldPortal portal = builder.DiscoveryPortal;
      Assert.IsNotNull(portal, "the discovery gate has a walk-in portal");
      Assert.IsFalse(portal.ExitMode, "it is an ENTER portal");
      Assert.IsFalse(portal.PlayExit, "it is not a play-arena exit");
      Assert.AreEqual(DiscoveryArea.AreaId, portal.areaId, "it targets the Discovery Garden");
      Assert.AreEqual(1.8f, portal.fireRadius, "whole-arch coverage radius");
      // The portal is hub-side of the gate body (walk-in fires on approach).
      float gateToPortal = Dist2D(portal.transform.position, gate.transform.position);
      Assert.Less(gateToPortal, 1.5f, "the portal hugs the gate");
      Vector3 hubReturn = MathWorldBuilder.WorldOffset + MathWorldBuilder.DiscoveryHubReturnLocal;
      Assert.Greater(Dist2D(hubReturn, portal.transform.position),
        portal.fireRadius + portal.rearmMargin,
        "exit landing clears the portal re-arm radius (no instant re-entry)");
      Assert.IsNotNull(builder.DiscoveryHint, "the gate carries the approach glow");
      Assert.AreSame(portal, builder.DiscoveryHint.Portal, "the glow watches the right portal");
      // The seam: the area implements IMicroWorldArea and the portal dispatches.
      Assert.IsTrue(typeof(IMicroWorldArea).IsAssignableFrom(typeof(DiscoveryArea)),
        "DiscoveryArea implements the seam");
      GameObject areaGo = new GameObject("P62AreaProbe");
      try {
        DiscoveryArea area = areaGo.AddComponent<DiscoveryArea>();
        area.Bind(null, null, null, null, null, Vector3.zero);
        portal.DiscoveryArea = area;
        Assert.AreSame(area, portal.TargetArea(), "the portal dispatches to the bound area");
        Assert.IsTrue(area.TryEnterForTests(), "the area accepts control");
        Assert.IsTrue(area.TryExitForTests(), "and releases it");
      } finally { Object.DestroyImmediate(areaGo); }
    } finally { Object.DestroyImmediate(root); }
  }

  // B. Arena structure: reference board (magnifier + 3 task icons, apple lit),
  // the three pocket landmarks, the item set, result slots, exit cue, cameras.
  [Test] public void P62B_ArenaStructure() {
    GameObject arena;
    DiscoveryBuilder builder = BuildArena(out arena);
    try {
      Assert.IsNotNull(FindDeep(arena.transform, "DGBoardRing"), "the magnifier ring staged");
      Assert.IsNotNull(FindDeep(arena.transform, "DGBoardGlass"), "the magnifier glass staged");
      Assert.IsNotNull(FindDeep(arena.transform, "DGBoardHandle"), "the magnifier handle staged");
      Assert.AreEqual(3, builder.TaskIcons.Length, "three task icons");
      Assert.IsTrue(builder.TaskIcons[0].activeSelf, "the apple icon is lit first");
      Assert.IsFalse(builder.TaskIcons[1].activeSelf, "the flower icon waits");
      Assert.IsFalse(builder.TaskIcons[2].activeSelf, "the butterfly icon waits");
      // Pockets: the apple tree, the flower bed, the butterfly bush.
      Assert.IsNotNull(FindDeep(arena.transform, "DGAppleTrunk"), "the apple tree trunk staged");
      Assert.IsNotNull(FindDeep(arena.transform, "DGAppleCanopy0"), "the apple tree canopy staged");
      Assert.IsNotNull(FindDeep(arena.transform, "DGTreeApple0"), "the hanging-apple clue staged");
      Assert.IsNotNull(FindDeep(arena.transform, "DGFlowerBedSoil"), "the flower bed staged");
      Assert.IsNotNull(FindDeep(arena.transform, "DGFlowerBedFlower0"), "the bed flowers staged");
      Assert.IsNotNull(FindDeep(arena.transform, "DGButterflyBush0"), "the butterfly bush staged");
      Assert.IsNotNull(FindDeep(arena.transform, "DGBushFlower0"), "the bush flowers staged");
      // Items: 9 candidates (2 apples, 2 flowers, 1 butterfly, 2 balls,
      // 1 leaf, 1 mushroom) with the round tiers flagged.
      Assert.AreEqual(9, builder.Items.Count, "nine searchable candidates staged");
      int core = 0, extra = 0;
      for (int i = 0; i < builder.ItemExtras.Length; i++) {
        if (builder.ItemExtras[i]) extra++; else core++;
      }
      Assert.AreEqual(5, core, "five core candidates (round 0)");
      Assert.AreEqual(4, extra, "four extra distractors (round 1)");
      Assert.IsNotNull(FindDeep(arena.transform, "DGButterfly0"), "the butterfly staged");
      // Every item is bake-ignored (they move + must never carve the field).
      for (int i = 0; i < builder.Items.Count; i++) {
        Assert.IsTrue(IsIgnoredFromBuild(builder.Items[i]), "item " + i + " never bakes");
        Transform[] pieces = builder.Items[i].GetComponentsInChildren<Transform>(true);
        for (int p = 0; p < pieces.Length; p++) {
          Assert.IsTrue(IsIgnoredFromBuild(pieces[p].gameObject),
            "item " + i + " piece " + pieces[p].name + " never bakes");
        }
      }
      // Pocket vegetation is bake-ignored too (open walkable field).
      Assert.IsTrue(IsIgnoredFromBuild(FindDeep(arena.transform, "DGAppleTrunk").gameObject),
        "the tree never carves the search field");
      Assert.IsTrue(IsIgnoredFromBuild(FindDeep(arena.transform, "DGButterflyBush0").gameObject),
        "the bush never carves the search field");
      // Result board: hidden, with the three task slots waiting.
      Assert.IsNotNull(builder.Result, "the result board staged");
      Assert.IsFalse(builder.Result.activeSelf, "result hidden before completion");
      Assert.AreEqual(3, builder.ResultSlots.Length, "three result slots");
      Assert.IsFalse(builder.ExitCue.activeSelf, "exit cue hidden before completion");
      foreach (string n in new[] { "DGCamA", "DGLookA", "DGCamB", "DGLookB", "DGCamC", "DGLookC" })
        Assert.IsNotNull(FindDeep(arena.transform, n), "camera marker " + n);
      Assert.IsNotNull(builder.Anchors, "anchor registry");
      Assert.IsNotNull(builder.EntryPoint, "entry marker");
      Assert.IsNotNull(builder.ExitPortal, "exit portal");
      Assert.IsTrue(builder.ExitPortal.ExitMode, "exit returns to the hub");
      Assert.AreEqual(DiscoveryArea.AreaId, builder.ExitPortal.areaId, "exit targets the area");
      // Child scale: entry -> pockets is a short search loop, never a hike.
      Assert.Less(Dist2D(DiscoveryBuilder.EntryLocal, DiscoveryBuilder.AppleTreePos), 8f,
        "the apple pocket is a short walk");
      Assert.Less(Dist2D(DiscoveryBuilder.EntryLocal, DiscoveryBuilder.ButterflyBushPos), 8f,
        "the butterfly pocket is a short walk");
      Assert.Less(Dist2D(DiscoveryBuilder.AppleTreePos, DiscoveryBuilder.ButterflyBushPos), 7f,
        "the pockets sit close enough to search on foot");
    } finally { Object.DestroyImmediate(arena); }
  }

  // C. Tasks + round tiers: round 0 activates the core five, round 1 the full
  // nine; the task order is apple -> flower -> butterfly.
  [Test] public void P62C_TasksAndRounds() {
    Assert.AreEqual(3, DiscoveryBuilder.TaskCount, "three tasks per visit");
    Assert.AreEqual(DiscoveryItem.ItemKind.Apple, DiscoveryBuilder.TaskKinds[0], "task 1 = apple");
    Assert.AreEqual(DiscoveryItem.ItemKind.Flower, DiscoveryBuilder.TaskKinds[1], "task 2 = flower");
    Assert.AreEqual(DiscoveryItem.ItemKind.Butterfly, DiscoveryBuilder.TaskKinds[2], "task 3 = butterfly");
    // Round 0: core only (5 candidates active).
    {
      GameObject arena;
      DiscoveryBuilder builder = BuildArena(out arena);
      GameObject player = BuildPlayer(DiscoveryBuilder.EntryLocal);
      try {
        FakeAudio audio = new FakeAudio();
        DiscoveryGame game = BuildGame(builder, arena, player, audio,
          new ActivityLifecycle("discover_items", "test"), 0);
        Assert.AreEqual(5, game.ActiveItemCount, "round 0 shows five candidates");
        Assert.AreEqual(0, game.Round, "round 0 pinned");
        Object.DestroyImmediate(game);
      } finally {
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(arena);
      }
    }
    // Round 1: the extras wake up (9 candidates active).
    {
      GameObject arena;
      DiscoveryBuilder builder = BuildArena(out arena);
      GameObject player = BuildPlayer(DiscoveryBuilder.EntryLocal);
      try {
        FakeAudio audio = new FakeAudio();
        DiscoveryGame game = BuildGame(builder, arena, player, audio,
          new ActivityLifecycle("discover_items", "test"), 1);
        Assert.AreEqual(9, game.ActiveItemCount, "round 1 shows the full garden");
        Assert.AreEqual(1, game.Round, "round 1 pinned");
        Object.DestroyImmediate(game);
      } finally {
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(arena);
      }
    }
  }

  // D. Full flow: intro -> demo REAL search (2 checks before the find) ->
  // handoff (the demo find tidies) -> child finds apple, flower, butterfly ->
  // success (lifecycle + result + exit cue + notification).
  [Test] public void P62D_FullFlow() {
    GameObject arena;
    DiscoveryBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(DiscoveryBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("discover_items", "test");
      int completed = -1;
      DiscoveryGame game = BuildGame(builder, arena, player, audio, life, 0,
        delegate (int n) { completed = n; });
      AdvanceToTasks(game);
      Assert.AreEqual(DiscoveryGame.Phase.Tasks, game.Current, "the child holds control");
      Assert.AreEqual(2, game.DemoChecks, "the demo checked two spots before the find");
      Assert.IsTrue(game.DemoFound, "the demo found the apple");
      Assert.AreEqual(ActivityState.Active, life.State, "lifecycle active at handoff");
      Assert.AreEqual(0, game.TaskIndex, "the child starts at task 1");
      Assert.AreEqual(DiscoveryItem.ItemKind.Apple, game.CurrentTaskKind, "task 1 = apple");
      DiscoveryItem apple = FindByKind(game, DiscoveryItem.ItemKind.Apple);
      Assert.IsNotNull(apple, "the apple is findable again after the demo tidy");
      Assert.AreEqual(DiscoveryItem.ItemState.Available, apple.State, "the demo find tidied up");
      FindOne(game, DiscoveryItem.ItemKind.Apple);
      Assert.AreEqual(1, game.FoundCount, "one task found");
      Assert.AreEqual(DiscoveryItem.ItemKind.Flower, game.CurrentTaskKind, "task 2 = flower");
      FindOne(game, DiscoveryItem.ItemKind.Flower);
      Assert.AreEqual(2, game.FoundCount, "two tasks found");
      Assert.AreEqual(DiscoveryItem.ItemKind.Butterfly, game.CurrentTaskKind, "task 3 = butterfly");
      FindOne(game, DiscoveryItem.ItemKind.Butterfly);
      for (int i = 0; i < 30 && game.Current != DiscoveryGame.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(DiscoveryGame.Phase.Success, game.Current, "three finds complete");
      Assert.AreEqual(3, game.FoundCount, "all three found");
      Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle completed");
      Assert.AreEqual(3, completed, "the area is notified with the task count");
      Assert.IsTrue(game.ResultShown, "the result board shows");
      Assert.IsTrue(game.ExitCueShown, "the exit cue lights up");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("You found them all! Well done!",
        "Tìm thấy hết rồi! Giỏi!")), "the teacher confirms the whole exploration");
      Assert.IsTrue(audio.Sfx.Contains("found"), "each discovery chirps");
      Assert.IsTrue(audio.Sfx.Contains("success"), "the finale chime plays");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // E. Wrong recovery: distractors correct gently, never reset, never count —
  // and the very next correct candidate still succeeds.
  [Test] public void P62E_WrongRecovery() {
    GameObject arena;
    DiscoveryBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(DiscoveryBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      DiscoveryGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("discover_items", "test"), 0);
      AdvanceToTasks(game);
      DiscoveryItem ball = FindByKind(game, DiscoveryItem.ItemKind.Ball);
      Assert.IsNotNull(ball, "a ball distractor exists");
      game.TryInteract(ball);
      Assert.AreEqual(1, game.WrongAttempts, "the ball is refused");
      Assert.AreEqual(0, game.FoundCount, "a wrong candidate never counts");
      Assert.AreEqual(DiscoveryItem.ItemState.Available, ball.State, "the ball stays searchable");
      Assert.AreEqual(DiscoveryGame.Phase.Tasks, game.Current, "no reset, no fail");
      // Two more wrong attempts (cooldown does not block the guard, only the line).
      game.TryInteract(ball);
      DiscoveryItem leaf = FindByKind(game, DiscoveryItem.ItemKind.Leaf);
      Assert.IsNotNull(leaf, "a leaf distractor exists");
      game.TryInteract(leaf);
      Assert.AreEqual(3, game.WrongAttempts, "three wrong attempts recorded");
      Assert.AreEqual(0, game.FoundCount, "still zero found");
      // The right candidate still works immediately.
      FindOne(game, DiscoveryItem.ItemKind.Apple);
      Assert.AreEqual(1, game.FoundCount, "the correction path still succeeds");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Not that one. Keep looking!",
        "Chưa đúng. Con tìm tiếp nhé!")), "the gentle line plays");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // F. Duplicate/spam guards: a found item never re-fires; interactions during
  // the intro are ignored; inactive extras cannot be found in round 0.
  [Test] public void P62F_DuplicateAndSpam() {
    GameObject arena;
    DiscoveryBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(DiscoveryBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      DiscoveryGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("discover_items", "test"), 0);
      // During the intro nothing may count.
      DiscoveryItem appleProbe = FindByKind(game, DiscoveryItem.ItemKind.Apple);
      game.TryInteract(appleProbe);
      Assert.AreEqual(0, game.FoundCount, "intro interactions are ignored");
      Assert.AreEqual(DiscoveryGame.Phase.Intro, game.Current, "the intro owns the stage");
      AdvanceToTasks(game);
      FindOne(game, DiscoveryItem.ItemKind.Apple);
      int found = game.FoundCount;
      DiscoveryItem apple = FindByKind(game, DiscoveryItem.ItemKind.Apple);
      if (apple != null) game.TryInteract(apple); // already found (round 0 has one apple)
      Assert.AreEqual(found, game.FoundCount, "a found item never counts twice");
      // Round 0: the extra-tier items are inactive and cannot be interacted.
      DiscoveryItem extraApple = null;
      for (int i = 0; i < game.ItemCountTotal; i++) {
        DiscoveryItem it = game.ItemAt(i);
        if (it != null && it.IsExtra && it.Kind == DiscoveryItem.ItemKind.Apple) {
          extraApple = it; break;
        }
      }
      Assert.IsNotNull(extraApple, "the extra apple exists (round-1 tier)");
      Assert.IsFalse(extraApple.gameObject.activeSelf, "it sleeps in round 0");
      Assert.IsFalse(extraApple.IsAvailable, "an inactive extra is never available");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // G. Item lifecycle: Available -> Found with the found mark; the butterfly
  // ORBITS while available and LANDS when found.
  [Test] public void P62G_ItemLifecycleAndButterfly() {
    GameObject arena;
    DiscoveryBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(DiscoveryBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      DiscoveryGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("discover_items", "test"), 0);
      AdvanceToTasks(game);
      DiscoveryItem apple = FindByKind(game, DiscoveryItem.ItemKind.Apple);
      Assert.IsNotNull(apple.FoundMark, "every item carries a found marker");
      Assert.IsFalse(apple.FoundMark.activeSelf, "the marker waits hidden");
      Assert.AreEqual(DiscoveryItem.ItemState.Available, apple.State, "apple starts available");
      game.TryInteract(apple);
      Assert.AreEqual(DiscoveryItem.ItemState.Found, apple.State, "apple found");
      Assert.IsTrue(apple.FoundMark.activeSelf, "the found marker appears");
      apple.SetFound(false);
      Assert.AreEqual(DiscoveryItem.ItemState.Available, apple.State, "state resets honestly");
      Assert.IsFalse(apple.FoundMark.activeSelf, "marker hides again");
      // Butterfly: orbits while available, lands when found.
      DiscoveryItem butterfly = FindByKind(game, DiscoveryItem.ItemKind.Butterfly);
      Assert.IsNotNull(butterfly, "the butterfly exists");
      Vector3 home = butterfly.HomeLocal;
      for (int i = 0; i < 30; i++) butterfly.TickForTests(0.1f);
      float orbited = Dist2D(butterfly.transform.localPosition, home);
      Assert.Greater(orbited, 0.2f, "the butterfly orbits (the movement is the clue)");
      butterfly.SetFound(true);
      for (int i = 0; i < 30; i++) butterfly.TickForTests(0.1f);
      float landed = Dist2D(butterfly.transform.localPosition, home);
      Assert.Less(landed, 0.15f, "the found butterfly lands");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // H. Re-entry adopt: a fresh instance on a Completed lifecycle shows the
  // finished picture (result + slots + cue, every task-kind item found).
  [Test] public void P62H_ReentryAdopt() {
    GameObject arena;
    DiscoveryBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(DiscoveryBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("discover_items", "test");
      DiscoveryGame first = BuildGame(builder, arena, player, audio, life, 0);
      AdvanceToTasks(first);
      FindOne(first, DiscoveryItem.ItemKind.Apple);
      FindOne(first, DiscoveryItem.ItemKind.Flower);
      FindOne(first, DiscoveryItem.ItemKind.Butterfly);
      for (int i = 0; i < 30 && first.Current != DiscoveryGame.Phase.Success; i++) first.Tick(0.1f);
      Assert.AreEqual(ActivityState.Completed, life.State, "setup: lifecycle completed");
      Object.DestroyImmediate(first);
      DiscoveryGame second = BuildGame(builder, arena, player, new FakeAudio(), life, 0);
      Assert.AreEqual(DiscoveryGame.Phase.Success, second.Current, "adopts success, never replays");
      Assert.IsTrue(second.ResultShown, "the finished picture shows the result");
      Assert.IsTrue(second.ExitCueShown, "the way home stays lit");
      for (int i = 0; i < second.ItemCountTotal; i++) {
        DiscoveryItem it = second.ItemAt(i);
        if (it == null || !it.gameObject.activeSelf) continue;
        if (it.Kind == DiscoveryItem.ItemKind.Apple
            || it.Kind == DiscoveryItem.ItemKind.Flower
            || it.Kind == DiscoveryItem.ItemKind.Butterfly) {
          Assert.AreEqual(DiscoveryItem.ItemState.Found, it.State,
            "adopted " + it.Kind + " stays found");
        } else {
          Assert.AreEqual(DiscoveryItem.ItemState.Available, it.State,
            "distractor " + it.Kind + " stays a distractor");
        }
      }
      for (int i = 0; i < 60; i++) second.Tick(0.1f);
      Assert.AreEqual(DiscoveryGame.Phase.Success, second.Current, "adopt never restarts the lesson");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // I. Lazy contract: one micro scene at a time, scene ships in Build Settings.
  [Test] public void P62I_LazySceneContract() {
    Assert.AreEqual("DiscoveryScene", DiscoveryBuilder.SceneName, "scene name pinned");
    Assert.Greater(DiscoveryBuilder.WorldOffset.magnitude, 200f, "separate island (no overlap)");
    var t = new WorldTransition(SubjectIds.Main);
    var ops = new FakeOps();
    var math = new SubjectId("math");
    Assert.IsTrue(t.EnterAsync(ops, math, "MathScene").GetAwaiter().GetResult(), "subject loads");
    Assert.IsFalse(ops.Loaded.Contains(DiscoveryBuilder.SceneName),
      "the discovery garden must NOT be loaded at subject entry (lazy)");
    Assert.IsTrue(t.EnterMicroAsync(ops, RabbitPlayBuilder.SceneName).GetAwaiter().GetResult(),
      "the rabbit arena loads first");
    Assert.IsFalse(t.EnterMicroAsync(ops, DiscoveryBuilder.SceneName).GetAwaiter().GetResult(),
      "the discovery garden cannot stack onto another micro scene");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "the arena unloads");
    Assert.IsTrue(t.EnterMicroAsync(ops, DiscoveryBuilder.SceneName).GetAwaiter().GetResult(),
      "the discovery garden loads into the freed slot");
    Assert.IsTrue(ops.Loaded.Contains(DiscoveryBuilder.SceneName), "it is now the live micro scene");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "and unloads cleanly");
    string scenePath = Path.Combine(Application.dataPath,
      "A_World", "DiscoveryGarden", "DiscoveryScene.unity");
    Assert.IsTrue(File.Exists(scenePath), "DiscoveryScene.unity ships in the project");
    string yaml = File.ReadAllText(scenePath);
    Assert.IsTrue(yaml.Contains("DiscoveryWorld"), "scene root is DiscoveryWorld");
    string buildSettings = File.ReadAllText(Path.Combine(
      Directory.GetParent(Application.dataPath).FullName, "ProjectSettings", "EditorBuildSettings.asset"));
    Assert.IsTrue(buildSettings.Contains("DiscoveryScene.unity"), "the scene ships in the build");
  }

  // J. Round ladder + CLI + area seams: 0 -> 1 -> 0, flag inert when invalid.
  [Test] public void P62J_LadderAndCli() {
    Assert.AreEqual(1, DiscoveryArea.NextRound(0), "round 0 -> 1");
    Assert.AreEqual(0, DiscoveryArea.NextRound(1), "round 1 -> 0");
    Assert.AreEqual(0, DiscoveryArea.NextRound(7), "off-ladder rejoins at 0");
    Assert.AreEqual(1, DiscoveryArea.ParseRoundArg(
      new[] { "exe", "-discovery-round", "1" }, 0), "the flag pins a round");
    Assert.AreEqual(0, DiscoveryArea.ParseRoundArg(
      new[] { "exe", "-discovery-round", "9" }, 0), "out-of-range stays inert");
    Assert.AreEqual(0, DiscoveryArea.ParseRoundArg(new[] { "exe" }, 0), "no flag keeps the ladder");
    GameObject go = new GameObject("P62AreaSeams");
    try {
      DiscoveryArea area = go.AddComponent<DiscoveryArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      Assert.IsFalse(area.IsInside, "starts outside");
      Assert.IsTrue(area.TryEnterForTests(), "enter");
      Assert.IsFalse(area.TryEnterForTests(), "double enter blocked");
      Assert.IsTrue(area.TryExitForTests(), "exit");
      Assert.IsFalse(area.TryExitForTests(), "double exit blocked");
      area.SetRoundForTests(0);
      area.TickProgressionForTests();
      Assert.AreEqual(0, area.Round, "mid-lesson re-entry keeps the round");
      area.NotifyCompleted(3);
      Assert.AreEqual(3, area.LastCompletedTasks, "the area remembers the completed count");
      Assert.AreEqual(0, DiscoveryArea.DefaultRound, "reference round pinned");
    } finally { Object.DestroyImmediate(go); }
  }

  // K. Speech safety: every line a full run can produce passes the NPC cap in
  // the active language (recorded from a REAL run, both voices).
  [Test] public void P62K_LinesSafety() {
    GameObject arena;
    DiscoveryBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(DiscoveryBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      DiscoveryGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("discover_items", "test"), 1);
      AdvanceToTasks(game);
      // Include a wrong candidate so the gentle line is recorded too.
      DiscoveryItem ball = FindByKind(game, DiscoveryItem.ItemKind.Ball);
      if (ball != null) game.TryInteract(ball);
      FindOne(game, DiscoveryItem.ItemKind.Apple);
      FindOne(game, DiscoveryItem.ItemKind.Flower);
      FindOne(game, DiscoveryItem.ItemKind.Butterfly);
      for (int i = 0; i < 40 && game.Current != DiscoveryGame.Phase.Success; i++) game.Tick(0.1f);
      for (int i = 0; i < 60; i++) game.Tick(0.1f); // the way-home line drains
      Assert.Greater(audio.Lines.Count, 8, "a full run speaks plenty");
      foreach (string line in audio.Lines) {
        bool ok = SafetyFilter.ValidateLine(line, false, out string why);
        Assert.IsTrue(ok, "line passes the NPC cap: '" + line + "' (" + why + ")");
      }
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Find the apple!", "Tìm quả táo nhé!")),
        "task 1 was read");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("I found it!", "Con tìm thấy rồi!")),
        "the student's discovery line played on his own voice");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Now find the flower!",
        "Giờ tìm bông hoa nhé!")), "task 2 was read");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // L. The demo never goes straight to the target: the two pocket checks are
  // recorded BEFORE the find (brief §9), and the fork is the first stop.
  [Test] public void P62L_DemoSearchNeverStraight() {
    GameObject arena;
    DiscoveryBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(DiscoveryBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      DiscoveryGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("discover_items", "test"), 0);
      // Tick until the demo finds, then pin the ordering.
      bool checkedBeforeFind = false;
      for (float t = 0f; t < 300f && !game.DemoFound; t += 0.1f) {
        game.Tick(0.1f);
        if (game.Current == DiscoveryGame.Phase.Demo && game.DemoChecks < 2) {
          checkedBeforeFind = true; // still checking while not found
        }
      }
      Assert.IsTrue(game.DemoFound, "the demo eventually finds the apple");
      Assert.AreEqual(2, game.DemoChecks, "both pocket checks ran");
      Assert.IsTrue(checkedBeforeFind, "the checks ran before the find (no straight line)");
      Assert.AreEqual(DiscoveryGame.Phase.Demo, game.Current, "the demo is still settling");
      AdvanceToTasks(game);
      Assert.AreEqual(DiscoveryGame.Phase.Tasks, game.Current, "handoff follows the demo");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // M. Exit cue + reward + no auto-exit: hidden before completion, lit after;
  // success is stable with the child still free.
  [Test] public void P62M_ExitCueAndReward() {
    GameObject arena;
    DiscoveryBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(DiscoveryBuilder.EntryLocal);
    try {
      Assert.IsFalse(builder.ExitCue.activeSelf, "hidden before completion");
      Vector3 cue = builder.ExitCue.transform.localPosition;
      Assert.Greater(cue.y, 2.0f, "the cue floats above the exit arch");
      Assert.Less(Mathf.Abs(cue.z - DiscoveryBuilder.ExitLocal.z), 0.6f, "the cue rides the exit");
      FakeAudio audio = new FakeAudio();
      DiscoveryGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("discover_items", "test"), 0);
      AdvanceToTasks(game);
      FindOne(game, DiscoveryItem.ItemKind.Apple);
      FindOne(game, DiscoveryItem.ItemKind.Flower);
      FindOne(game, DiscoveryItem.ItemKind.Butterfly);
      for (int i = 0; i < 30 && game.Current != DiscoveryGame.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(DiscoveryGame.Phase.Success, game.Current, "completion reached");
      Assert.IsTrue(game.ResultShown, "result board lit");
      Assert.IsTrue(game.ExitCueShown, "exit cue lit");
      for (int i = 0; i < 30; i++) game.Tick(0.1f);
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Time to go home!", "Mình ra cổng nhé!")),
        "the teacher points at the way home (no auto-return)");
      for (int i = 0; i < 100; i++) game.Tick(0.1f);
      Assert.AreEqual(DiscoveryGame.Phase.Success, game.Current, "success is stable, never auto-exits");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }
}
