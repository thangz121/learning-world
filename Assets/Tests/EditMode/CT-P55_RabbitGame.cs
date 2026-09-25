// CT-P55: GAMEPLAY #3 — "CHO THỎ ĂN ĐÚNG SỐ" (feed the rabbit the right number).
// Pins the carrot patch door (zone 0), the child-scale arena (board + patch +
// rabbit + bowl + pips + result), the deterministic carrot lifecycle
// (Available -> Picked -> Carried -> Delivered -> Consumed), the full activity
// flow (teacher intro -> student demo -> handoff -> child feeding -> success),
// overshoot correction (guidance, never failure), undershoot nudges, spam
// safety, re-entry adopt, the lazy RabbitPlayScene contract, the 1..9 ladder +
// CLI, and the speech/SFX beats through the reference audio path.
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class CT_P55_RabbitGame {
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

  static CountingGardenBuilder BuildGarden(out GameObject garden) {
    garden = new GameObject("P55GardenWorld");
    CountingGardenBuilder builder = garden.AddComponent<CountingGardenBuilder>();
    builder.BuildContent(garden.transform);
    return builder;
  }

  static RabbitPlayBuilder BuildArena(out GameObject arena, int target = 3) {
    arena = new GameObject("P55RabbitWorld");
    RabbitPlayBuilder builder = arena.AddComponent<RabbitPlayBuilder>();
    builder.BoardTarget = RabbitPlayBuilder.ClampTarget(target);
    builder.BuildContent(arena.transform);
    return builder;
  }

  static RabbitFeed BuildGame(RabbitPlayBuilder builder, GameObject arena,
      GameObject player, FakeAudio audio, ActivityLifecycle life, int target) {
    RabbitFeed game = arena.AddComponent<RabbitFeed>();
    game.Build(builder, player.transform, null, audio, life, target);
    return game;
  }

  static GameObject BuildPlayer(Vector3 at) {
    GameObject player = new GameObject("P55Player");
    player.transform.position = at;
    return player;
  }

  static void AdvanceToFeeding(RabbitFeed game, float maxSeconds = 150f) {
    for (float t = 0f; t < maxSeconds && game.Current != RabbitFeed.Phase.Feeding; t += 0.1f)
      game.Tick(0.1f);
  }

  // Drive one full player feed: stand at the carrot, pick, wait for the carry,
  // stand at the bunny, feed, wait for the munch.
  static void FeedOne(RabbitFeed game, GameObject player, int idx) {
    RabbitCarrot c = game.CarrotAt(idx);
    Assert.IsNotNull(c, "carrot " + idx + " exists");
    player.transform.position = c.transform.position + new Vector3(0f, 0f, -0.5f);
    game.TryPick(c);
    for (int i = 0; i < 40 && c.State != RabbitCarrot.CarrotState.Carried; i++) game.Tick(0.1f);
    Assert.AreEqual(RabbitCarrot.CarrotState.Carried, c.State, "carrot " + idx + " rides the hand");
    player.transform.position = game.CarrotAt(idx).transform.position; // carried: moves along
    player.transform.position = RabbitPlayBuilder.RabbitHome + new Vector3(0f, 0f, -1.0f);
    game.TryFeed();
    for (int i = 0; i < 40 && c.State != RabbitCarrot.CarrotState.Consumed; i++) game.Tick(0.1f);
    Assert.AreEqual(RabbitCarrot.CarrotState.Consumed, c.State, "carrot " + idx + " is eaten");
  }

  // A. Garden: the carrot patch (zone 0) is the rabbit door — staged with its
  // own play scene, panel at once (no garden try-run); zones 2/5 untouched.
  [Test] public void P55A_GardenRabbitDoor() {
    GameObject garden;
    CountingGardenBuilder builder = BuildGarden(out garden);
    try {
      Assert.AreEqual(0, CountingGardenBuilder.RabbitZoneIndex, "the rabbit door is the carrot patch");
      List<GardenZoneSpot> spots = new List<GardenZoneSpot>(
        garden.GetComponentsInChildren<GardenZoneSpot>());
      Assert.AreEqual(CountingGardenBuilder.ZoneCount, spots.Count, "one spot per plot");
      GardenZoneSpot rabbit = null, stair = null, theatre = null;
      foreach (GardenZoneSpot s in spots) {
        if (s.zoneIndex == 0) rabbit = s;
        if (s.zoneIndex == 2) theatre = s;
        if (s.zoneIndex == 5) stair = s;
      }
      Assert.IsNotNull(rabbit, "zone-0 spot exists");
      Assert.IsTrue(rabbit.playEnabled, "the carrot patch opens play");
      Assert.AreEqual(RabbitPlayBuilder.SceneName, CountingGardenArea.PlaySceneFor(rabbit),
        "the carrot patch opens RabbitPlayScene");
      Assert.IsFalse(rabbit.demoGate, "the rabbit panel opens at once (lesson runs in-arena)");
      Assert.IsTrue(theatre.playEnabled && stair.playEnabled, "games #1/#2 untouched");
      Assert.AreEqual(CountingPlayBuilder.SceneName, CountingGardenArea.PlaySceneFor(theatre),
        "game #1 scene unchanged");
      Assert.AreEqual(StairHillBuilder.SceneName, CountingGardenArea.PlaySceneFor(stair),
        "game #2 scene unchanged");
    } finally { Object.DestroyImmediate(garden); }
  }

  // B. Arena structure: board + patch + rabbit + bowl + pips + result +
  // cameras, all child-scale (brief §11: never a meaningless long walk).
  [Test] public void P55B_ArenaStructure() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    try {
      Assert.IsNotNull(FindDeep(arena.transform, "RPNumberDigit"), "board digit staged");
      for (int i = 0; i < RabbitPlayBuilder.CarrotCount; i++)
        Assert.IsNotNull(FindDeep(arena.transform, "RPCarrot" + i), "carrot " + i + " staged");
      Assert.AreEqual(10, builder.Carrots.Count, "ten carrots: the target plus a spare");
      Assert.IsNotNull(FindDeep(arena.transform, "RPRabbit"), "the rabbit staged");
      Assert.IsNotNull(FindDeep(arena.transform, "RPHead"), "rabbit head staged");
      Assert.IsNotNull(FindDeep(arena.transform, "RPEarL"), "rabbit left ear staged");
      Assert.IsNotNull(FindDeep(arena.transform, "RPEarR"), "rabbit right ear staged");
      Assert.IsNotNull(FindDeep(arena.transform, "RPFeedBowl"), "feeding bowl staged");
      Assert.IsNotNull(builder.FeedAnchor, "feed door anchor exposed");
      Assert.AreEqual(9, builder.CountPips.Length, "nine pip slots for targets 1..9");
      Assert.IsNotNull(builder.Result, "result board staged");
      Assert.IsFalse(builder.Result.activeSelf, "result hidden before success");
      foreach (string n in new[] { "RPCamA", "RPLookA", "RPCamB", "RPLookB", "RPCamC", "RPLookC" })
        Assert.IsNotNull(FindDeep(arena.transform, n), "camera marker " + n);
      Assert.IsNotNull(builder.Anchors, "anchor registry");
      Assert.IsNotNull(builder.EntryPoint, "entry marker");
      Assert.IsNotNull(builder.ExitPortal, "exit portal");
      Assert.IsTrue(builder.ExitPortal.PlayExit, "exit returns to the garden");
      // Child scale: entry -> patch -> rabbit is a short loop, never a hike.
      float entryPatch = Dist2D(RabbitPlayBuilder.EntryLocal, RabbitPlayBuilder.PatchStand);
      float patchRabbit = Dist2D(RabbitPlayBuilder.PatchCenter, RabbitPlayBuilder.RabbitHome);
      Assert.Less(entryPatch, 6f, "entry to patch is a short walk");
      Assert.Less(patchRabbit, 6f, "patch to rabbit is a short walk");
      // The bowl sits in front of the rabbit (reachable, visible together).
      float bowlRabbit = Dist2D(RabbitPlayBuilder.BowlPos, RabbitPlayBuilder.RabbitHome);
      Assert.Less(bowlRabbit, 1.5f, "the bowl is at the rabbit's mouth");
    } finally { Object.DestroyImmediate(arena); }
  }

  // C. Target validity 1..9: clamp + staged digit + game target (brief §12).
  [Test] public void P55C_TargetValidity() {
    Assert.AreEqual(1, RabbitPlayBuilder.ClampTarget(0), "0 clamps to 1");
    Assert.AreEqual(9, RabbitPlayBuilder.ClampTarget(10), "10 clamps to 9");
    Assert.AreEqual(9, RabbitPlayBuilder.MaxTarget, "maximum is 9");
    for (int t = 1; t <= 9; t++) {
      GameObject arena;
      RabbitPlayBuilder builder = BuildArena(out arena, t);
      GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
      try {
        Assert.IsNotNull(FindDeep(arena.transform, "RPNumberDigit"), "digit staged at " + t);
        FakeAudio audio = new FakeAudio();
        RabbitFeed game = BuildGame(builder, arena, player, audio,
          new ActivityLifecycle("rabbit_feed", "test"), t);
        Assert.AreEqual(t, game.Target, "game plays target " + t);
        Object.DestroyImmediate(game);
      } finally {
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(arena);
      }
    }
  }

  // D. Full flow at 3: intro -> demo (3 real feeds) -> handoff -> child feeds
  // 3 -> success (result + lifecycle + recap).
  [Test] public void P55D_FullFlowAt3() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("rabbit_feed", "test");
      RabbitFeed game = BuildGame(builder, arena, player, audio, life, 3);
      AdvanceToFeeding(game);
      Assert.AreEqual(RabbitFeed.Phase.Feeding, game.Current, "the child holds control");
      Assert.AreEqual(3, game.DemoCarrotsFed, "the demo fed exactly 3");
      Assert.AreEqual(ActivityState.Active, life.State, "lifecycle active at handoff");
      // The demo grew the patch back: every carrot is pickable again.
      for (int i = 0; i < game.CarrotCount; i++)
        Assert.AreEqual(RabbitCarrot.CarrotState.Available, game.CarrotAt(i).State,
          "carrot " + i + " reset for the round");
      FeedOne(game, player, 0);
      Assert.AreEqual(1, game.Count, "one fed");
      FeedOne(game, player, 1);
      FeedOne(game, player, 2);
      for (int i = 0; i < 20 && game.Current != RabbitFeed.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Success, game.Current, "three fed completes");
      Assert.AreEqual(3, game.Count, "count is exactly the target");
      Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle completed");
      Assert.IsTrue(game.ResultShown, "result board shown");
      Assert.AreEqual(3, game.PipCount, "three pips gold");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Three carrots! Well done!", "Ba củ cà rốt! Giỏi!")),
        "the teacher confirms the target");
      Assert.IsTrue(audio.Sfx.Contains("munch"), "the bunny audibly eats");
      Assert.IsTrue(audio.Sfx.Contains("success"), "success chime plays");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // E. Overshoot at 3: the 4th carrot is NOT success — gentle correction
  // ("Đủ ba củ rồi."), the extra hops home, count stays 3, then Success again.
  [Test] public void P55E_OvershootAt3() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("rabbit_feed", "test");
      RabbitFeed game = BuildGame(builder, arena, player, audio, life, 3);
      AdvanceToFeeding(game);
      FeedOne(game, player, 0);
      FeedOne(game, player, 1);
      FeedOne(game, player, 2);
      for (int i = 0; i < 20 && game.Current != RabbitFeed.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Success, game.Current, "setup: success at 3");
      RabbitCarrot extra = game.CarrotAt(3);
      player.transform.position = extra.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(extra);
      for (int i = 0; i < 40 && extra.State != RabbitCarrot.CarrotState.Carried; i++) game.Tick(0.1f);
      player.transform.position = RabbitPlayBuilder.RabbitHome + new Vector3(0f, 0f, -1.0f);
      game.TryFeed();
      Assert.AreEqual(RabbitFeed.Phase.Correct, game.Current, "the 4th feed corrects, never succeeds");
      Assert.AreEqual(1, game.Overshoots, "one overshoot recorded");
      for (int i = 0; i < 200 && game.Current != RabbitFeed.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Success, game.Current, "the correction lands back on success");
      Assert.AreEqual(3, game.Count, "count stays exactly the target");
      Assert.AreEqual(RabbitCarrot.CarrotState.Available, extra.State, "the extra hops home");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Three is enough.", "Đủ ba củ rồi.")),
        "the teacher names the limit gently");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // F. Undershoot: 2 of 5 fed and settled earns a gentle "how many more" —
  // never a fail, never a completion.
  [Test] public void P55F_UndershootNudge() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena, 5);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      RabbitFeed game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("rabbit_feed", "test"), 5);
      AdvanceToFeeding(game);
      FeedOne(game, player, 0);
      FeedOne(game, player, 1);
      Assert.AreEqual(RabbitFeed.Phase.Feeding, game.Current, "short of the target: still feeding");
      player.transform.position = RabbitPlayBuilder.RabbitHome + new Vector3(0f, 0f, -1.0f);
      for (int i = 0; i < 140; i++) game.Tick(0.1f); // settle: the nudge fires
      Assert.GreaterOrEqual(game.UndershootNudges, 1, "a gentle nudge names the remainder");
      Assert.AreEqual(RabbitFeed.Phase.Feeding, game.Current, "no completion while short");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("Three more carrots!", "Còn ba củ nữa nhé!")), "the nudge counts the rest");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // G. Spam safety: double-pick, pick-while-carrying, feed-with-empty-hands —
  // none of them double-count (brief: no double-count, no punishment).
  [Test] public void P55G_SpamSafety() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      RabbitFeed game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("rabbit_feed", "test"), 3);
      AdvanceToFeeding(game);
      RabbitCarrot c0 = game.CarrotAt(0);
      RabbitCarrot c1 = game.CarrotAt(1);
      player.transform.position = c0.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(c0);
      game.TryPick(c0); // same carrot twice
      game.TryPick(c1); // while carrying
      for (int i = 0; i < 30; i++) game.Tick(0.1f);
      Assert.AreEqual(c0, game.Carried, "exactly one carrot carried");
      Assert.AreEqual(RabbitCarrot.CarrotState.Available, c1.State, "the second carrot untouched");
      game.TryFeed();
      int afterFirst = game.Count;
      game.TryFeed(); // empty hands
      Assert.AreEqual(afterFirst, game.Count, "empty-hand feeds never count");
      for (int i = 0; i < 30 && c0.State != RabbitCarrot.CarrotState.Consumed; i++) game.Tick(0.1f);
      Assert.AreEqual(1, game.Count, "exactly one feed counted");
      game.TryPick(c0); // a consumed carrot never picks again
      Assert.IsNull(game.Carried, "consumed carrots stay eaten");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // H. Carrot lifecycle: Available -> Picked -> Carried -> Delivered ->
  // Consumed, plus the correction trip home and the demo reset (brief §6).
  [Test] public void P55H_CarrotLifecycle() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      RabbitFeed game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("rabbit_feed", "test"), 3);
      AdvanceToFeeding(game);
      RabbitCarrot c = game.CarrotAt(4);
      Assert.AreEqual(RabbitCarrot.CarrotState.Available, c.State, "starts available");
      player.transform.position = c.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(c);
      Assert.AreEqual(RabbitCarrot.CarrotState.Picked, c.State, "picked while the hand comes down");
      for (int i = 0; i < 30 && c.State != RabbitCarrot.CarrotState.Carried; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitCarrot.CarrotState.Carried, c.State, "then carried");
      player.transform.position = RabbitPlayBuilder.RabbitHome + new Vector3(0f, 0f, -1.0f);
      game.TryFeed();
      Assert.AreEqual(RabbitCarrot.CarrotState.Delivered, c.State, "then delivered to the mouth");
      for (int i = 0; i < 30 && c.State != RabbitCarrot.CarrotState.Consumed; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitCarrot.CarrotState.Consumed, c.State, "then consumed");
      Assert.IsTrue(audio.Sfx.Contains("munch"), "the munch beat fires on arrival");
      Assert.IsFalse(c.gameObject.activeSelf, "the eaten carrot leaves the world");
      // The correction trip home restores availability (the extra carrot).
      RabbitCarrot e = game.CarrotAt(5);
      player.transform.position = e.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(e);
      for (int i = 0; i < 30 && e.State != RabbitCarrot.CarrotState.Carried; i++) game.Tick(0.1f);
      e.BeginReturnHome();
      for (int i = 0; i < 30 && e.State != RabbitCarrot.CarrotState.Available; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitCarrot.CarrotState.Available, e.State, "returned carrots pick again");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // I. Re-entry adopt: a fresh instance on a Completed lifecycle shows the
  // finished picture (bowl full, result up, actors observing) — no replay.
  [Test] public void P55I_ReentryAdopt() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("rabbit_feed", "test");
      RabbitFeed first = BuildGame(builder, arena, player, audio, life, 3);
      AdvanceToFeeding(first);
      FeedOne(first, player, 0);
      FeedOne(first, player, 1);
      FeedOne(first, player, 2);
      for (int i = 0; i < 20 && first.Current != RabbitFeed.Phase.Success; i++) first.Tick(0.1f);
      Assert.AreEqual(ActivityState.Completed, life.State, "setup: lifecycle completed");
      Object.DestroyImmediate(first);
      RabbitFeed second = BuildGame(builder, arena, player, new FakeAudio(), life, 3);
      Assert.AreEqual(RabbitFeed.Phase.Success, second.Current, "adopts success, never replays");
      Assert.AreEqual(3, second.Count, "the adopted count is the target");
      Assert.IsTrue(second.ResultShown, "the finished picture shows the result");
      Assert.AreEqual(3, second.PipCount, "pips stay gold");
      for (int i = 0; i < 3; i++)
        Assert.AreEqual(RabbitCarrot.CarrotState.Consumed, second.CarrotAt(i).State,
          "fed carrot " + i + " stays in the bowl");
      for (int i = 0; i < 60; i++) second.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Success, second.Current, "adopt never restarts the lesson");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // J. Lazy contract: one micro scene at a time (the shared slot refuses a
  // second load), the scene ships in Build Settings, never at boot.
  [Test] public void P55J_LazySceneContract() {
    Assert.AreEqual("RabbitPlayScene", RabbitPlayBuilder.SceneName, "scene name pinned");
    Assert.Greater(RabbitPlayBuilder.WorldOffset.magnitude, 200f, "separate island (no overlap)");
    var t = new WorldTransition(SubjectIds.Main);
    var ops = new FakeOps();
    var math = new SubjectId("math");
    Assert.IsTrue(t.EnterAsync(ops, math, "MathScene").GetAwaiter().GetResult(), "subject loads");
    Assert.IsFalse(ops.Loaded.Contains(RabbitPlayBuilder.SceneName),
      "the rabbit scene must NOT be loaded at subject entry (lazy)");
    Assert.IsTrue(t.EnterMicroAsync(ops, CountingGardenBuilder.SceneName).GetAwaiter().GetResult(),
      "the garden loads first");
    Assert.IsFalse(t.EnterMicroAsync(ops, RabbitPlayBuilder.SceneName).GetAwaiter().GetResult(),
      "the rabbit scene cannot stack onto the garden (one micro slot)");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "garden unloads");
    Assert.IsTrue(t.EnterMicroAsync(ops, RabbitPlayBuilder.SceneName).GetAwaiter().GetResult(),
      "the rabbit arena loads into the freed slot");
    Assert.IsTrue(ops.Loaded.Contains(RabbitPlayBuilder.SceneName), "it is now the live micro scene");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "and unloads cleanly");
    // The authored shell + its shipping registration (source-level pins).
    string scenePath = Path.Combine(Application.dataPath,
      "A_World", "CountingGarden", "RabbitPlayScene.unity");
    Assert.IsTrue(File.Exists(scenePath), "RabbitPlayScene.unity ships in the project");
    string yaml = File.ReadAllText(scenePath);
    Assert.IsTrue(yaml.Contains("RabbitPlayWorld"), "scene root is RabbitPlayWorld");
    string buildSettings = File.ReadAllText(Path.Combine(
      Directory.GetParent(Application.dataPath).FullName, "ProjectSettings", "EditorBuildSettings.asset"));
    Assert.IsTrue(buildSettings.Contains("RabbitPlayScene.unity"), "the scene ships in the build");
  }

  // K. Ladder + CLI: 3 -> 4 -> 5 -> 6 -> 7 -> 8 -> 9 -> 1 -> 2 -> 3 (brief §12:
  // every target 1..9 is reachable through revisits), diagnostic flag inert.
  [Test] public void P55K_LadderAndCli() {
    int[] expect = { 4, 5, 6, 7, 8, 9, 1, 2, 3 };
    int t = 3;
    foreach (int n in expect) {
      t = CountingGardenArea.NextRabbitTarget(t);
      Assert.AreEqual(n, t, "ladder rung");
    }
    Assert.AreEqual(3, CountingGardenArea.NextRabbitTarget(99), "off-ladder rejoins at 3");
    Assert.AreEqual(5, CountingGardenArea.ParseRabbitTargetArg(
      new[] { "exe", "-rabbit-target", "5" }, 3), "flag pins a target");
    Assert.AreEqual(3, CountingGardenArea.ParseRabbitTargetArg(
      new[] { "exe", "-rabbit-target", "99" }, 3), "out-of-range flag stays inert");
    Assert.AreEqual(3, CountingGardenArea.ParseRabbitTargetArg(
      new[] { "exe" }, 3), "no flag keeps the ladder");
    GameObject go = new GameObject("P55AreaLadder");
    try {
      CountingGardenArea area = go.AddComponent<CountingGardenArea>();
      area.SetRabbitTargetForTests(3);
      Assert.AreEqual(3, area.RabbitTarget, "ladder starts at the reference 3");
      // Mid-lesson re-entry replays the same target (not Completed: no advance).
      area.TickRabbitProgressionForTests();
      Assert.AreEqual(3, area.RabbitTarget, "mid-lesson re-entry keeps the target");
    } finally { Object.DestroyImmediate(go); }
  }

  // L. Speech safety at 9: EVERY line a full round can produce (intro, demo
  // counts, handoff, player counts, recap, nudges, correction) passes the NPC
  // cap in the active language — recorded from a REAL target-9 run.
  [Test] public void P55L_LinesSafetyAt9() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena, 9);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      RabbitFeed game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("rabbit_feed", "test"), 9);
      AdvanceToFeeding(game, 220f);
      Assert.AreEqual(RabbitFeed.Phase.Feeding, game.Current, "target 9 reaches the child");
      for (int i = 0; i < 9; i++) FeedOne(game, player, i);
      for (int i = 0; i < 30 && game.Current != RabbitFeed.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Success, game.Current, "target 9 completes");
      for (int i = 0; i < 200; i++) game.Tick(0.1f); // recap drains
      Assert.Greater(audio.Lines.Count, 25, "a full round speaks plenty");
      foreach (string line in audio.Lines) {
        bool ok = SafetyFilter.ValidateLine(line, false, out string why);
        Assert.IsTrue(ok, "line passes the NPC cap: '" + line + "' (" + why + ")");
      }
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Nine carrots.", "Chín củ cà rốt.")),
        "nine counted");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("Nine carrots for bunny!", "Chín củ cà rốt cho thỏ!")), "target named in the intro");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // M. Carry robustness (brief §7 C/D/F): a picked carrot never drops
  // mid-walk, never vanishes on a long trek, and feeds fine afterwards.
  [Test] public void P55M_CarryRobustness() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      RabbitFeed game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("rabbit_feed", "test"), 3);
      AdvanceToFeeding(game);
      RabbitCarrot c = game.CarrotAt(2);
      player.transform.position = c.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(c);
      for (int i = 0; i < 20 && c.State != RabbitCarrot.CarrotState.Carried; i++) game.Tick(0.1f);
      // A long trek back to the patch, far away, and back: still carried.
      player.transform.position = RabbitPlayBuilder.PatchStand;
      for (int i = 0; i < 100; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitCarrot.CarrotState.Carried, c.State, "the carrot survives the walk back");
      player.transform.position = RabbitPlayBuilder.EntryLocal;
      for (int i = 0; i < 100; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitCarrot.CarrotState.Carried, c.State, "the carrot survives the far trek");
      Assert.AreEqual(c, game.Carried, "the hand never loses it");
      player.transform.position = RabbitPlayBuilder.RabbitHome + new Vector3(0f, 0f, -1.0f);
      game.TryFeed();
      for (int i = 0; i < 40 && c.State != RabbitCarrot.CarrotState.Consumed; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitCarrot.CarrotState.Consumed, c.State, "it feeds fine afterwards");
      Assert.AreEqual(1, game.Count, "counted exactly once");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }
}
