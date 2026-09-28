// CT-P55: GAMEPLAY #3 — "CHO THỎ ĂN ĐÚNG SỐ" (feed the rabbit the right number).
// Pins the carrot patch door (zone 0), the child-scale arena (board + patch +
// roof rabbit + bowl + play spot + result), the deterministic carrot lifecycle
// (Available -> Picked -> Carried -> Delivered -> Consumed-in-the-bowl), the
// full activity flow (walk to the play spot -> question -> child feeding ->
// success), overshoot correction (guidance, never failure), undershoot nudges
// (capped, S3-P2Z19), spam safety, re-entry adopt, the lazy RabbitPlayScene
// contract, the 1..9 ladder + CLI, and the speech/SFX beats through the
// reference audio path.
// S3-P2Z19 re-pins (user round, deliberate): the in-arena demo/handoff beats
// are gone (the garden miniature teaches; the arena asks on the spot), the pip
// board is gone (the bowl is the counter), and the bunny + bowl live on the
// hutch roof. C# 9.0 only.
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

  // S3-P2Z19: the child must stand on the marked play spot before the question
  // is read (no demo in the arena), so the helper walks them there first.
  static void AdvanceToFeeding(RabbitFeed game, GameObject player, float maxSeconds = 150f) {
    player.transform.position = RabbitPlayBuilder.PlaySpotLocal;
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

  // S3-P2Z23: ring the submit bell and let the Wrong beat settle when it fires.
  static void Submit(RabbitFeed game, GameObject player) {
    player.transform.position = RabbitPlayBuilder.SubmitLocal;
    game.TrySubmit();
    for (int i = 0; i < 60 && game.Current == RabbitFeed.Phase.Wrong; i++) game.Tick(0.1f);
  }

  static RabbitCarrot FirstCarrotInState(RabbitFeed game, RabbitCarrot.CarrotState state) {
    for (int i = 0; i < game.CarrotCount; i++) {
      RabbitCarrot c = game.CarrotAt(i);
      if (c != null && c.State == state) return c;
    }
    return null;
  }

  // Feed the next available carrot (used by the arithmetic rounds where the bowl
  // starts prefilled and the indices are not the familiar 0,1,2...).
  static void FeedNext(RabbitFeed game, GameObject player) {
    RabbitCarrot c = FirstCarrotInState(game, RabbitCarrot.CarrotState.Available);
    Assert.IsNotNull(c, "an available carrot");
    player.transform.position = c.transform.position + new Vector3(0f, 0f, -0.5f);
    game.TryPick(c);
    for (int i = 0; i < 40 && c.State != RabbitCarrot.CarrotState.Carried; i++) game.Tick(0.1f);
    Assert.AreEqual(RabbitCarrot.CarrotState.Carried, c.State, "carrot in hand");
    player.transform.position = RabbitPlayBuilder.RabbitHome + new Vector3(0f, 0f, -1.0f);
    game.TryFeed();
    for (int i = 0; i < 40 && c.State != RabbitCarrot.CarrotState.Consumed; i++) game.Tick(0.1f);
    Assert.AreEqual(RabbitCarrot.CarrotState.Consumed, c.State, "carrot fed");
  }

  // A. Garden: the carrot patch (zone 0) is the rabbit door — staged with its
  // own play scene, panel at once (no garden try-run); the stair hill (zone 1)
  // untouched.
  [Test] public void P55A_GardenRabbitDoor() {
    GameObject garden;
    CountingGardenBuilder builder = BuildGarden(out garden);
    try {
      Assert.AreEqual(0, CountingGardenBuilder.RabbitZoneIndex, "the rabbit door is the carrot patch");
      List<GardenZoneSpot> spots = new List<GardenZoneSpot>(
        garden.GetComponentsInChildren<GardenZoneSpot>());
      Assert.AreEqual(CountingGardenBuilder.ZoneCount, spots.Count, "one spot per plot");
      GardenZoneSpot rabbit = null, stair = null;
      foreach (GardenZoneSpot s in spots) {
        if (s.zoneIndex == CountingGardenBuilder.RabbitZoneIndex) rabbit = s;
        if (s.zoneIndex == CountingGardenBuilder.StairZoneIndex) stair = s;
      }
      Assert.IsNotNull(rabbit, "zone-0 spot exists");
      Assert.IsTrue(rabbit.playEnabled, "the carrot patch opens play");
      Assert.AreEqual(RabbitPlayBuilder.SceneName, CountingGardenArea.PlaySceneFor(rabbit),
        "the carrot patch opens RabbitPlayScene");
      Assert.IsTrue(rabbit.demoGate, "the rabbit panel waits for the garden miniature");
      Assert.IsNotNull(stair, "zone-1 spot exists");
      Assert.IsTrue(stair.playEnabled, "the stair hill is untouched");
      Assert.AreEqual(StairHillBuilder.SceneName, CountingGardenArea.PlaySceneFor(stair),
        "the stair scene unchanged");
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
      // S3-P2Z23: the submit bell (the child turns the count in).
      Assert.IsNotNull(FindDeep(arena.transform, "RPSubmitBell"), "submit bell staged");
      Assert.IsNotNull(builder.SubmitAnchor, "submit door anchor exposed");
      // S3-P2Z19: no pip board (the bowl is the counter); the marked play spot
      // carries the walk-in guidance instead.
      Assert.IsNotNull(FindDeep(arena.transform, "RPPlayRing"), "the play spot ring is staged");
      Assert.IsNotNull(builder.PlaySpot, "the play spot is exposed");
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
      // The bowl sits beside the rabbit ON the hutch roof (S3-P2Z19), close
      // enough to read together; the child feeds from the ground beside it.
      float bowlRabbit = Dist2D(RabbitPlayBuilder.BowlPos, RabbitPlayBuilder.RabbitHome);
      Assert.Less(bowlRabbit, 1.5f, "the bowl is at the rabbit's side");
      Assert.Greater(RabbitPlayBuilder.RabbitHome.y, 1f, "the rabbit lives on the hutch roof");
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

  // D. Full flow at 3: walk to the spot -> question -> child feeds 3 -> rings
  // the submit bell -> success (result + lifecycle + recap).
  [Test] public void P55D_FullFlowAt3() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("rabbit_feed", "test");
      RabbitFeed game = BuildGame(builder, arena, player, audio, life, 3);
      AdvanceToFeeding(game, player);
      Assert.AreEqual(RabbitFeed.Phase.Feeding, game.Current, "the child holds control");
      Assert.AreEqual(ActivityState.Active, life.State, "lifecycle active after the question");
      // S3-P2Z19: no in-arena demo touched the patch — every carrot is pickable.
      for (int i = 0; i < game.CarrotCount; i++)
        Assert.AreEqual(RabbitCarrot.CarrotState.Available, game.CarrotAt(i).State,
          "carrot " + i + " ready for the round");
      FeedOne(game, player, 0);
      Assert.AreEqual(1, game.Count, "one fed");
      FeedOne(game, player, 1);
      FeedOne(game, player, 2);
      // S3-P2Z23: feeding 3 does NOT auto-complete — the child submits.
      Assert.AreEqual(RabbitFeed.Phase.Feeding, game.Current, "still feeding until the bell");
      Assert.IsFalse(game.ResultShown, "no result before the submit");
      Submit(game, player);
      for (int i = 0; i < 20 && game.Current != RabbitFeed.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Success, game.Current, "an exact submit completes");
      Assert.AreEqual(3, game.Count, "count is exactly the target");
      Assert.AreEqual(1, game.Submits, "one bell ring");
      Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle completed");
      Assert.IsTrue(game.ResultShown, "result board shown");
      Assert.AreEqual(3, game.PipCount, "the count state tracks the target");
      for (int i = 0; i < 200; i++) game.Tick(0.1f); // the paced confirm drains
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Three carrots! Well done!", "Ba củ cà rốt! Giỏi!")),
        "the teacher confirms the target");
      Assert.IsTrue(audio.Sfx.Contains("munch"), "the bunny audibly eats");
      Assert.IsTrue(audio.Sfx.Contains("success"), "success chime plays");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // E. S3-P2Z23: the submit can be WRONG — too few AND too many both gently
  // reset the bowl so the child retries; only an exact count wins.
  [Test] public void P55E_WrongSubmitThenRetry() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("rabbit_feed", "test");
      RabbitFeed game = BuildGame(builder, arena, player, audio, life, 3);
      AdvanceToFeeding(game, player);

      // Too few: 2 of 3.
      FeedOne(game, player, 0);
      FeedOne(game, player, 1);
      for (int i = 0; i < 60; i++) game.Tick(0.1f); // let the count lines drain
      player.transform.position = RabbitPlayBuilder.SubmitLocal;
      game.TrySubmit();
      Assert.AreEqual(RabbitFeed.Phase.Wrong, game.Current, "too few is not a win");
      Assert.IsFalse(game.ResultShown, "no result on a wrong submit");
      for (int i = 0; i < 10; i++) game.Tick(0.1f);
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Not enough. Count again!", "Chưa đủ rồi. Đếm lại nhé!")),
        "the teacher names the shortfall gently");
      for (int i = 0; i < 60 && game.Current != RabbitFeed.Phase.Feeding; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Feeding, game.Current, "the bowl reopens for a retry");
      Assert.AreEqual(0, game.Count, "the bowl emptied for the retry");
      for (int i = 0; i < 30 && game.CarrotAt(0).State != RabbitCarrot.CarrotState.Available; i++)
        game.Tick(0.1f); // the returned carrots finish their trip home
      for (int i = 0; i < game.CarrotCount; i++)
        Assert.AreEqual(RabbitCarrot.CarrotState.Available, game.CarrotAt(i).State,
          "carrot " + i + " is back home to retry");

      // Too many: 4 of 3.
      FeedOne(game, player, 0);
      FeedOne(game, player, 1);
      FeedOne(game, player, 2);
      FeedOne(game, player, 3);
      for (int i = 0; i < 60; i++) game.Tick(0.1f); // let the count lines drain
      Assert.AreEqual(4, game.Count, "the child CAN overshoot");
      Assert.AreEqual(RabbitFeed.Phase.Feeding, game.Current, "still feeding after 4");
      player.transform.position = RabbitPlayBuilder.SubmitLocal;
      game.TrySubmit();
      Assert.AreEqual(RabbitFeed.Phase.Wrong, game.Current, "too many is not a win");
      for (int i = 0; i < 10; i++) game.Tick(0.1f);
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Too many. Count again!", "Thừa rồi. Đếm lại nhé!")),
        "the teacher names the excess gently");
      for (int i = 0; i < 60 && game.Current != RabbitFeed.Phase.Feeding; i++) game.Tick(0.1f);
      Assert.AreEqual(0, game.Count, "the bowl emptied again");
      for (int i = 0; i < 30 && game.CarrotAt(0).State != RabbitCarrot.CarrotState.Available; i++)
        game.Tick(0.1f); // the returned carrots finish their trip home

      // Exact: 3 of 3 wins.
      FeedOne(game, player, 0);
      FeedOne(game, player, 1);
      FeedOne(game, player, 2);
      Submit(game, player);
      for (int i = 0; i < 20 && game.Current != RabbitFeed.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Success, game.Current, "an exact submit wins after retries");
      Assert.AreEqual(3, game.Count, "count is exactly the target");
      Assert.AreEqual(0, game.Overshoots, "a wrong count is a wrong submit, never auto-corrected");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // F. Undershoot: 2 of 5 fed and settled — S3-P2Z22 (user: "để trẻ tự suy
  // nghĩ") there is NO "how many more" hint any more; the child is never nudged
  // and never fails, just keeps playing.
  [Test] public void P55F_UndershootNoHint() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena, 5);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      RabbitFeed game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("rabbit_feed", "test"), 5);
      AdvanceToFeeding(game, player);
      FeedOne(game, player, 0);
      FeedOne(game, player, 1);
      Assert.AreEqual(RabbitFeed.Phase.Feeding, game.Current, "short of the target: still feeding");
      player.transform.position = RabbitPlayBuilder.RabbitHome + new Vector3(0f, 0f, -1.0f);
      for (int i = 0; i < 140; i++) game.Tick(0.1f); // settle: no hint fires
      Assert.AreEqual(0, game.UndershootNudges, "no 'how many more' hint (child thinks)");
      Assert.AreEqual(RabbitFeed.Phase.Feeding, game.Current, "no completion while short");
      Assert.IsFalse(audio.Lines.Contains(
        DialogueLang.T("Three more carrots!", "Còn ba củ nữa nhé!")), "no remainder hint spoken");
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
      AdvanceToFeeding(game, player);
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
      AdvanceToFeeding(game, player);
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
      // S3-P2Z19: the fed carrot STAYS visible in the bowl (the counter).
      Assert.IsTrue(c.gameObject.activeSelf, "the fed carrot stays visible in the bowl");
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
      AdvanceToFeeding(first, player);
      FeedOne(first, player, 0);
      FeedOne(first, player, 1);
      FeedOne(first, player, 2);
      Submit(first, player);
      for (int i = 0; i < 20 && first.Current != RabbitFeed.Phase.Success; i++) first.Tick(0.1f);
      Assert.AreEqual(ActivityState.Completed, life.State, "setup: lifecycle completed");
      Object.DestroyImmediate(first);
      RabbitFeed second = BuildGame(builder, arena, player, new FakeAudio(), life, 3);
      Assert.AreEqual(RabbitFeed.Phase.Success, second.Current, "adopts success, never replays");
      Assert.AreEqual(3, second.Count, "the adopted count is the target");
      Assert.IsTrue(second.ResultShown, "the finished picture shows the result");
      Assert.AreEqual(3, second.PipCount, "the adopted count state holds");
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

  // L. Speech safety at 9: EVERY line a full round can produce (intro, count
  // along, win, nudges, correction) passes the NPC cap in the active language —
  // recorded from a REAL target-9 run. S3-P2Z24: the post-win recap is gone, so
  // each count word lands ONCE (fed) and the win line closes the round.
  [Test] public void P55L_LinesSafetyAt9() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena, 9);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      RabbitFeed game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("rabbit_feed", "test"), 9);
      AdvanceToFeeding(game, player, 220f);
      Assert.AreEqual(RabbitFeed.Phase.Feeding, game.Current, "target 9 reaches the child");
      for (int i = 0; i < 9; i++) FeedOne(game, player, i);
      Submit(game, player);
      for (int i = 0; i < 30 && game.Current != RabbitFeed.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Success, game.Current, "target 9 completes on submit");
      for (int i = 0; i < 200; i++) game.Tick(0.1f); // pacer drains
      Assert.Greater(audio.Lines.Count, 10, "a full round speaks plenty");
      foreach (string line in audio.Lines) {
        bool ok = SafetyFilter.ValidateLine(line, false, out string why);
        Assert.IsTrue(ok, "line passes the NPC cap: '" + line + "' (" + why + ")");
      }
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Nine carrots.", "Chín củ cà rốt.")),
        "nine counted");
      // S3-P2Z23: the intro does NOT name the number — the child reads the board.
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("Feed the bunny!", "Cho thỏ ăn nhé!")), "job named, number left to the child");
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
      AdvanceToFeeding(game, player);
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

  // N. S3-P2Z24 (user: "carot bay xuyên qua bục từ dưới lên"): a fed carrot
  // rises clear of the bowl and DROPS straight down onto the slot — it always
  // lands from above, never tunnels up through the pedestal.
  [Test] public void P55N_FeedDropsFromAbove() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      RabbitFeed game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("rabbit_feed", "test"), 3);
      AdvanceToFeeding(game, player);
      RabbitCarrot c = game.CarrotAt(0);
      player.transform.position = c.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(c);
      for (int i = 0; i < 40 && c.State != RabbitCarrot.CarrotState.Carried; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitCarrot.CarrotState.Carried, c.State, "setup: carrot in hand");
      player.transform.position = RabbitPlayBuilder.RabbitHome + new Vector3(0f, 0f, -1.0f);
      game.TryFeed();
      float maxY = float.MinValue;
      for (int i = 0; i < 160 && c.State != RabbitCarrot.CarrotState.Consumed; i++) {
        game.Tick(0.05f);
        if (c.transform.position.y > maxY) maxY = c.transform.position.y;
      }
      Assert.AreEqual(RabbitCarrot.CarrotState.Consumed, c.State, "the carrot lands in the bowl");
      float restY = c.transform.position.y;
      Assert.Greater(maxY, restY + 0.35f,
        "the carrot lifts clear above the bowl before dropping (peak " + maxY.ToString("F2")
        + " vs rest " + restY.ToString("F2") + ")");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // O. S3-P2Z24 (user: "sau khi báo kết quả 'giỏi quá' thì vẫn đếm lại số carot"):
  // the win line closes the round — no second count-up after the praise.
  [Test] public void P55O_SuccessDoesNotRecount() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      RabbitFeed game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("rabbit_feed", "test"), 3);
      AdvanceToFeeding(game, player);
      FeedOne(game, player, 0);
      FeedOne(game, player, 1);
      FeedOne(game, player, 2);
      Submit(game, player);
      for (int i = 0; i < 30 && game.Current != RabbitFeed.Phase.Success; i++) game.Tick(0.1f);
      for (int i = 0; i < 200; i++) game.Tick(0.1f); // any straggler line would land here
      string count3 = DialogueLang.T("Three carrots.", "Ba củ cà rốt.");
      int n = 0;
      foreach (string line in audio.Lines) if (line == count3) n++;
      Assert.AreEqual(1, n, "the count is spoken once per carrot, never recapped after the win");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("Three carrots! Well done!", "Ba củ cà rốt! Giỏi!")), "the win line lands");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // P. S3-P2Z24 (user: "2 bảng tranh nhau chỗ đứng, chỗ nộp bài khó nhìn"): the
  // submit bell now stands on its own marked pad on the open plaza, clear of the
  // result board — the two never share space.
  [Test] public void P55P_SubmitStationClearOfResult() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    try {
      Assert.IsNotNull(FindDeep(arena.transform, "RPSubmitRing"), "submit ring pad staged");
      Assert.IsNotNull(FindDeep(arena.transform, "RPSubmitBell"), "submit bell staged");
      Assert.IsNotNull(builder.SubmitAnchor, "submit door anchor exposed");
      Vector3 result = builder.Result.transform.localPosition;
      float gap = Dist2D(RabbitPlayBuilder.SubmitLocal, result);
      Assert.Greater(gap, 2.5f,
        "the submit bell is clear of the result board (gap " + gap.ToString("F2") + "m)");
      // The submit pad never overlaps the play spot ring (the child's stand).
      float fromSpot = Dist2D(RabbitPlayBuilder.SubmitLocal, RabbitPlayBuilder.PlaySpotLocal);
      Assert.Greater(fromSpot, RabbitPlayBuilder.PlaySpotRadius + 0.5f,
        "the submit pad clears the play spot");
    } finally { Object.DestroyImmediate(arena); }
  }

  static int ActiveNumberDigits(GameObject arena) {
    int n = 0;
    foreach (Transform t in arena.GetComponentsInChildren<Transform>(true))
      if (t.name == "RPNumberDigit" && t.gameObject.activeSelf) n++;
    return n;
  }

  // Q. S3-P2Z25 (user: after a correct submit the bowl clears, the child returns
  // to the spot and the NEXT number appears with a swap animation): drive a full
  // correct round and watch the transition land on the next target.
  [Test] public void P55Q_CorrectSubmitAdvancesNumber() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("rabbit_feed", "test");
      RabbitFeed game = BuildGame(builder, arena, player, audio, life, 3);
      AdvanceToFeeding(game, player);
      FeedOne(game, player, 0);
      FeedOne(game, player, 1);
      FeedOne(game, player, 2);
      Submit(game, player);
      for (int i = 0; i < 30 && game.Current != RabbitFeed.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Success, game.Current, "exact submit wins");
      Assert.AreEqual(3, game.Count, "the bowl holds the target right after the win");
      // Past the hold + walk timeout + digit swap.
      for (int i = 0; i < 160; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Feeding, game.Current, "the next round opens for play");
      Assert.AreEqual(CountingGardenArea.NextRabbitTarget(3), game.Target, "the target advanced");
      Assert.AreEqual(0, game.Count, "the bowl was cleared after the submit");
      Assert.IsFalse(game.ResultShown, "the result board hid again");
      for (int i = 0; i < game.CarrotCount; i++)
        Assert.AreEqual(RabbitCarrot.CarrotState.Available, game.CarrotAt(i).State,
          "carrot " + i + " walked home for the next round");
      Assert.AreEqual(1, ActiveNumberDigits(arena), "exactly one number digit lives after the swap");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Next question!", "Câu hỏi tiếp theo!")),
        "the next-question cue is spoken before the new number");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // R. S3-P2Z25 (user: "nếu sai thì đọc lại câu hỏi"): a wrong submit clears the
  // bowl and the teacher re-reads the task (paced short lines under the NPC cap).
  [Test] public void P55R_WrongSubmitRereadsQuestion() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      RabbitFeed game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("rabbit_feed", "test"), 3);
      AdvanceToFeeding(game, player);
      FeedOne(game, player, 0);
      FeedOne(game, player, 1);
      for (int i = 0; i < 60; i++) game.Tick(0.1f); // let the count lines drain
      player.transform.position = RabbitPlayBuilder.SubmitLocal;
      game.TrySubmit();
      Assert.AreEqual(RabbitFeed.Phase.Wrong, game.Current, "too few is a wrong submit");
      for (int i = 0; i < 60 && game.Current != RabbitFeed.Phase.Feeding; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Feeding, game.Current, "the retry opens");
      Assert.AreEqual(0, game.Count, "the bowl cleared");
      for (int i = 0; i < 200; i++) game.Tick(0.1f); // drain the paced re-ask
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Look at the number!", "Nhìn số trên bảng nhé!")),
        "the re-read names the board");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Put them on the bowl!", "Đặt lên bục nhé!")),
        "the re-read ends on the bowl");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // S. S3-P2Z26 (user: "4-2 thì bục có sẵn 4, trẻ bỏ bớt 2"): a subtraction round
  // prefills the minuend, the child drags two back to the garden, submits the
  // remainder, and the teacher explains the operation.
  [Test] public void P55S_SubtractionRound() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      RabbitFeed game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("rabbit_feed", "test"), 3);
      game.SetRoundForTests((int)RabbitFeed.RoundKind.Sub, 4, 2);
      AdvanceToFeeding(game, player);
      Assert.AreEqual(RabbitFeed.RoundKind.Sub, game.Kind, "subtraction round");
      Assert.AreEqual(2, game.Target, "4 - 2 leaves 2");
      Assert.AreEqual(4, game.Count, "the bowl starts with the minuend (4)");
      Assert.IsNotNull(FindDeep(arena.transform, "RPNumberDigitA"), "left operand digit staged");
      Assert.IsNotNull(FindDeep(arena.transform, "RPNumberDigitB"), "right operand digit staged");
      Assert.IsNotNull(FindDeep(arena.transform, "RPQuestionOpH"), "horizontal (minus) bar staged");
      Assert.IsNull(FindDeep(arena.transform, "RPQuestionOpV"), "no plus stem on a minus question");
      // The child drags two carrots off the bowl back to the garden.
      for (int r = 0; r < 2; r++) {
        RabbitCarrot c = FirstCarrotInState(game, RabbitCarrot.CarrotState.Consumed);
        Assert.IsNotNull(c, "a carrot on the bowl to remove");
        game.ReturnFromBowl(c);
      }
      Assert.AreEqual(2, game.Count, "two removed, two left");
      for (int i = 0; i < 40; i++) game.Tick(0.1f); // the removed carrots fly home
      Submit(game, player);
      for (int i = 0; i < 30 && game.Current != RabbitFeed.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Success, game.Current, "the remainder wins");
      Assert.AreEqual(2, game.Count, "count is the remainder");
      // S3-P2Z27: the board resolves "4 - 2 = 2".
      Assert.IsNotNull(FindDeep(arena.transform, "RPQuestionEq0"), "the equals sign appears on a win");
      Assert.IsNotNull(FindDeep(arena.transform, "RPNumberDigitR"), "the solved result digit appears");
      for (int i = 0; i < 200; i++) game.Tick(0.1f);
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("That's right! This is subtraction.", "Đúng rồi! Đây là phép trừ.")),
        "the teacher names subtraction");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("Four carrots minus two.", "Bốn củ trừ hai củ.")),
        "the teacher states the operation");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("Two carrots are left.", "Còn hai củ trên bục.")),
        "the teacher states the result");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("The bunny has four carrots.", "Thỏ có bốn củ cà rốt.")),
        "the intro asks the have/want question");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("How many to take?", "Bỏ bớt mấy củ?")),
        "the intro asks how many to take");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // T. S3-P2Z26 (user: "3+2 thì bục có sẵn 3, trẻ thêm 2"): an addition round
  // prefills the first addend, the child feeds two more, submits the sum, and the
  // teacher explains the operation.
  [Test] public void P55T_AdditionRound() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      RabbitFeed game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("rabbit_feed", "test"), 3);
      game.SetRoundForTests((int)RabbitFeed.RoundKind.Add, 3, 2);
      AdvanceToFeeding(game, player);
      Assert.AreEqual(RabbitFeed.RoundKind.Add, game.Kind, "addition round");
      Assert.AreEqual(5, game.Target, "3 + 2 = 5");
      Assert.AreEqual(3, game.Count, "the bowl starts with the first addend (3)");
      Assert.IsNotNull(FindDeep(arena.transform, "RPQuestionOpV"), "vertical (plus) stem staged");
      FeedNext(game, player);
      FeedNext(game, player);
      Assert.AreEqual(5, game.Count, "two more fed -> five on the bowl");
      Submit(game, player);
      for (int i = 0; i < 30 && game.Current != RabbitFeed.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Success, game.Current, "the sum wins");
      // S3-P2Z27: the board resolves "3 + 2 = 5".
      Assert.IsNotNull(FindDeep(arena.transform, "RPNumberDigitR"), "the solved result digit appears");
      for (int i = 0; i < 200; i++) game.Tick(0.1f);
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("That's right! This is addition.", "Đúng rồi! Đây là phép cộng.")),
        "the teacher names addition");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("Three carrots plus two.", "Ba củ cộng hai củ.")),
        "the teacher states the operation");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("Five carrots on the bowl.", "Được năm củ trên bục.")),
        "the teacher states the result");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("The bunny has three carrots.", "Thỏ có ba củ cà rốt.")),
        "the intro asks the have/want question");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("How many more?", "Lấy thêm mấy củ?")),
        "the intro asks how many more");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // U. S3-P2Z28 (user: "câu mới phải có sẵn 6 củ trên bục + audio hướng dẫn"):
  // the NEXT arithmetic question staged by a correct submit must PRE-PLACE its
  // first operand on the bowl and read its operation guidance before control.
  [Test] public void P55U_NextArithmeticRoundPrefillsAndGuides() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      RabbitFeed game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("rabbit_feed", "test"), 3);
      AdvanceToFeeding(game, player);
      FeedOne(game, player, 0);
      FeedOne(game, player, 1);
      FeedOne(game, player, 2);
      Submit(game, player);
      for (int i = 0; i < 30 && game.Current != RabbitFeed.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Success, game.Current, "plain 3 wins");
      // Force the transition's next question to be 6 - 5.
      game.ForceNextRoundForTests((int)RabbitFeed.RoundKind.Sub, 6, 5);
      for (int i = 0; i < 160 && game.Current != RabbitFeed.Phase.Feeding; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Feeding, game.Current, "the next round reaches the child");
      Assert.AreEqual(RabbitFeed.RoundKind.Sub, game.Kind, "the forced subtraction is live");
      Assert.AreEqual(1, game.Target, "6 - 5 leaves 1");
      Assert.AreEqual(6, game.Count, "the new bowl is PREFILLED with the minuend (6)");
      int consumed = 0;
      for (int i = 0; i < game.CarrotCount; i++)
        if (game.CarrotAt(i).State == RabbitCarrot.CarrotState.Consumed) consumed++;
      Assert.AreEqual(6, consumed, "six carrots sit on the bowl");
      for (int i = 0; i < 120; i++) game.Tick(0.1f); // the guidance lines drain
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("The bunny has six carrots.", "Thỏ có sáu củ cà rốt.")),
        "the new question reads its have/want guidance");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("How many to take?", "Bỏ bớt mấy củ?")),
        "the new question asks how many to take");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // V. S3-P2Z30 (user: "đọc giải thích trước khi câu kế tiếp"): the correct-submit
  // transition HOLDS until the explanation has finished, and the two-tap removal
  // clears the selection on the returned carrot.
  [Test] public void P55V_ExplanationGateAndTapRemoval() {
    GameObject arena;
    RabbitPlayBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(RabbitPlayBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      RabbitFeed game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("rabbit_feed", "test"), 3);
      game.SetRoundForTests((int)RabbitFeed.RoundKind.Sub, 3, 1);
      AdvanceToFeeding(game, player);
      Assert.AreEqual(2, game.Target, "3 - 1 leaves 2");
      // Tap-select a bowl carrot, then return it (what the garden tap calls).
      RabbitCarrot pick = FirstCarrotInState(game, RabbitCarrot.CarrotState.Consumed);
      Assert.IsNotNull(pick, "a bowl carrot");
      pick.Selected = true;
      game.ReturnFromBowl(pick);
      Assert.IsFalse(pick.Selected, "the returned carrot drops its selection");
      Assert.AreEqual(2, game.Count, "3 - 1 removed leaves 2");
      Submit(game, player);
      for (int i = 0; i < 30 && game.Current != RabbitFeed.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(RabbitFeed.Phase.Success, game.Current, "the exact remainder wins");
      Assert.IsFalse(game.ExplanationSpokenForTests(),
        "the explanation queue holds the transition (not spoken yet)");
      for (int i = 0; i < 400; i++) game.Tick(0.1f); // explanation drains + transition
      Assert.IsTrue(game.ExplanationSpokenForTests(), "the explanation finished");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("That's right! This is subtraction.", "Đúng rồi! Đây là phép trừ.")),
        "the explanation was read");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }
}
