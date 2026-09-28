// CT-P59: GAMEPLAY #6 — "GHÉP ĐÚNG CẶP" (match the pair, Match Meadow).
// Pins the Math Hub match_meadow gate (walk-in portal + approach glow + the
// shared IMicroWorldArea seam), the independent lazy meadow, the WORLD-BASED
// matching loop (reference -> search -> pick -> carry -> place -> pair), the
// round composition (1 pair balls / 2 flowers / 3 blocks), the gentle wrong
// match (never a fail, the object walks home), spam safety, re-entry adopt,
// the pair ladder + CLI, recorded-line safety at 3 pairs and carry-on-fist.
// C# 9.0 only.
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class CT_P59_MatchGame {
  sealed class FakeOps : ISceneOps {
    public readonly HashSet<string> Loaded = new HashSet<string>();
    public bool FailLoads;
    public bool IsLoaded(string sceneName) { return Loaded.Contains(sceneName); }
    public Task LoadAdditiveAsync(string sceneName) {
      if (FailLoads) throw new InvalidOperationException("fake load failure");
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

  static bool IsIgnoredFromBuild(GameObject go) {
    if (go == null) return false;
    Component c = null;
    try { c = go.GetComponent("NavMeshModifier"); } catch (Exception) { }
    if (c == null) return false;
    System.Reflection.PropertyInfo p = c.GetType().GetProperty("ignoreFromBuild");
    if (p == null) return false;
    try { return (bool)p.GetValue(c, null); } catch (Exception) { return false; }
  }

  static MatchMeadowBuilder BuildArena(out GameObject arena, int pairs = 1) {
    arena = new GameObject("P59MatchWorld");
    MatchMeadowBuilder builder = arena.AddComponent<MatchMeadowBuilder>();
    builder.BoardPairs = Mathf.Clamp(pairs, 1, MatchMeadowBuilder.MaxPairs);
    builder.BuildContent(arena.transform);
    return builder;
  }

  static MatchGame BuildGame(MatchMeadowBuilder builder, GameObject arena, GameObject player,
      FakeAudio audio, ActivityLifecycle life, int pairs, Action<int> onCompleted = null,
      Transform hand = null) {
    MatchGame game = arena.AddComponent<MatchGame>();
    game.Build(builder, player.transform, null, audio, life, pairs, onCompleted, hand);
    return game;
  }

  static GameObject BuildPlayer(Vector3 at) {
    GameObject player = new GameObject("P59Player");
    player.transform.position = at;
    return player;
  }

  static Vector3 PadWorld(GameObject arena) {
    return arena.transform.TransformPoint(new Vector3(0f, 0f, 4.4f));
  }

  // S3-P2Z19 user round: after entering the meadow the child walks to the
  // marked play spot; the question is read only there, then control passes over.
  static Vector3 PlaySpotWorld(GameObject arena) {
    return arena.transform.TransformPoint(MatchMeadowBuilder.PlaySpotLocal);
  }

  static void AdvanceToPlaying(MatchGame game, GameObject arena, GameObject player,
      float maxSeconds = 90f) {
    player.transform.position = PlaySpotWorld(arena);
    for (float t = 0f; t < maxSeconds && game.Current != MatchGame.Phase.Playing; t += 0.1f)
      game.Tick(0.1f);
  }

  // One full player match: stand at the object, pick, wait for the carry, stand
  // at the pairing pad, place, wait for the real landing.
  static void MatchOne(MatchGame game, GameObject arena, GameObject player, MatchItem item) {
    Assert.IsNotNull(item, "candidate exists");
    player.transform.position = item.transform.position + new Vector3(0f, 0f, -0.5f);
    game.TryPick(item);
    for (int i = 0; i < 40 && item.State != MatchItem.ItemState.Carried; i++) game.Tick(0.1f);
    Assert.AreEqual(MatchItem.ItemState.Carried, item.State, "object rides the hand");
    player.transform.position = PadWorld(arena) + new Vector3(0f, 0f, -1.0f);
    game.TryPlace();
    for (int i = 0; i < 40 && item.State != MatchItem.ItemState.Placed; i++) game.Tick(0.1f);
    Assert.AreEqual(MatchItem.ItemState.Placed, item.State, "object lands in its pair slot");
  }

  static void CompleteLife(ActivityLifecycle life) {
    life.MarkAvailable("test");
    life.BeginEnter("test");
    life.MarkReady("test");
    life.Begin("test");
    life.MarkCompleted("test");
  }

  // A. Math Hub: the match_meadow gate owns a walk-in portal (whole-arch, its
  // OWN area id) + the approach glow; the hub return clears the re-arm radius.
  [Test] public void P59A_GatePortalAndHint() {
    GameObject root = new GameObject("P59MathWorld");
    try {
      MathWorldBuilder builder = root.AddComponent<MathWorldBuilder>();
      builder.BuildContent(root.transform);
      MicroWorldGate gate = builder.FindMicroGate("match_meadow");
      Assert.IsNotNull(gate, "the match_meadow gate exists");
      Assert.AreEqual("match_meadow", gate.gateId, "the human-reviewed gate id intact");
      MicroWorldPortal portal = builder.MatchPortal;
      Assert.IsNotNull(portal, "the match gate has a walk-in portal");
      Assert.IsFalse(portal.ExitMode, "it is an ENTER portal");
      Assert.AreEqual(MatchArea.AreaId, portal.areaId, "it targets the Match Meadow area");
      Assert.AreEqual(1.8f, portal.fireRadius, "whole-arch coverage radius");
      Vector3 toHub = -new Vector3(gate.transform.position.x, 0f, gate.transform.position.z).normalized;
      Vector3 want = gate.transform.position + toHub * 0.7f;
      Assert.Less(Dist2D(portal.transform.position, want), 0.05f,
        "the portal covers the whole arch (0.7m hub-side of the gate centre)");
      Vector3 hubReturn = MathWorldBuilder.WorldOffset + MathWorldBuilder.MatchHubReturnLocal;
      Assert.Greater(Dist2D(hubReturn, portal.transform.position),
        portal.fireRadius + portal.rearmMargin,
        "exit landing clears the portal re-arm radius (no instant re-entry)");
      MicroGateHint hint = builder.MatchHint;
      Assert.IsNotNull(hint, "the gate carries the approach cue");
      Assert.AreSame(portal, hint.Portal, "the cue follows the gate portal");
      Assert.IsNotNull(hint.Glow, "the glow disc exists");
      Assert.IsTrue(IsIgnoredFromBuild(hint.Glow.gameObject), "the cue never bakes");
      Assert.AreEqual(1f, hint.NearForTests(hint.transform.position + new Vector3(2f, 0f, 0f)),
        0.001f, "inside the approach radius");
      Assert.AreEqual(0f, hint.NearForTests(hint.transform.position + new Vector3(12f, 0f, 0f)),
        0.001f, "far away stays silent");
    } finally { UnityEngine.Object.DestroyImmediate(root); }
  }

  // B. The meadow arena: board + result + references/pedestals + pair slots +
  // candidate spots + reward arch + exit door, child-scale, items bake-ignored.
  [Test] public void P59B_ArenaStructure() {
    GameObject arena;
    MatchMeadowBuilder builder = BuildArena(out arena, 2);
    try {
      Assert.IsNotNull(FindDeep(arena.transform, "MMNumberDigit"), "board digit staged");
      Assert.IsNotNull(FindDeep(arena.transform, "MMResultDigit"), "result digit staged");
      Assert.IsFalse(builder.Result.activeSelf, "result hidden before completion");
      Assert.IsNotNull(builder.RewardBloom, "reward bloom staged");
      Assert.IsFalse(builder.RewardBloom.activeSelf, "reward hidden before completion");
      Assert.IsNotNull(builder.ExitPortal, "exit portal staged");
      Assert.IsTrue(builder.ExitPortal.ExitMode, "exit returns to the hub");
      Assert.AreEqual(MatchArea.AreaId, builder.ExitPortal.areaId, "exit targets the meadow area");
      Assert.AreEqual(MatchMeadowBuilder.MaxPairs, builder.RefPedestals.Length, "three reference slots");
      for (int i = 0; i < MatchMeadowBuilder.MaxPairs; i++) {
        Assert.IsNotNull(builder.RefPedestals[i], "reference anchor " + i);
        Assert.IsNotNull(FindDeep(arena.transform, "MMPairSlot" + i), "pair slot " + i);
      }
      for (int i = 0; i < MatchMeadowBuilder.CandidateSpotLocal.Length; i++)
        Assert.IsNotNull(FindDeep(arena.transform, "MMSpot" + i), "candidate spot " + i);
      Assert.AreEqual(MatchMeadowBuilder.FamilyCount * MatchMeadowBuilder.ColorCount * 2,
        builder.Items.Count, "the full pool (family x colour x ref+cand)");
      for (int i = 0; i < builder.Items.Count; i++)
        Assert.IsTrue(IsIgnoredFromBuild(builder.Items[i]), "pool item " + i + " never bakes");
      Assert.IsNotNull(builder.Anchors, "anchor registry");
      Assert.IsNotNull(builder.EntryPoint, "entry marker");
      // Child-scale loop: entry -> field -> pairing mat.
      float entryField = Dist2D(builder.EntryPoint.position,
        builder.transform.TransformPoint(new Vector3(0f, 0f, 0.5f)));
      float fieldPad = Dist2D(new Vector3(0f, 0f, 0.5f), new Vector3(0f, 0f, 4.4f));
      Assert.Less(entryField, 8f, "entry to the search field is a short walk");
      Assert.Less(fieldPad, 7f, "field to the pairing mat is a short walk");
      // Entry spawn clears the exit door (J4).
      float entryExit = Dist2D(builder.EntryPoint.position, builder.ExitPortal.transform.position);
      Assert.Greater(entryExit, builder.ExitPortal.fireRadius + builder.ExitPortal.rearmMargin,
        "entry clears the exit re-arm radius");
    } finally { UnityEngine.Object.DestroyImmediate(arena); }
  }

  // C. Round composition: 1 pair = balls/blue, 2 = flowers/blue+red,
  // 3 = blocks/blue+red+yellow; candidates = pairs + one distractor.
  [Test] public void P59C_RoundComposition() {
    foreach (int pairs in new[] { 1, 2, 3 }) {
      GameObject arena;
      MatchMeadowBuilder builder = BuildArena(out arena, pairs);
      GameObject player = BuildPlayer(PadWorld(arena));
      try {
        FakeAudio audio = new FakeAudio();
        MatchGame game = BuildGame(builder, arena, player, audio,
          new ActivityLifecycle("match_pairs", "test"), pairs);
        Assert.AreEqual(MatchMeadowBuilder.FamilyForPairs(pairs), game.Family,
          "family for " + pairs + " pairs");
        for (int i = 0; i < pairs; i++)
          Assert.IsNotNull(game.ReferenceAt(i), "reference " + i + " staged for " + pairs + " pairs");
        Assert.IsNull(game.ReferenceAt(pairs), "no extra reference beyond the pair count");
        for (int i = 0; i < pairs; i++) {
          Assert.AreEqual(MatchMeadowBuilder.ColorsForPairs(pairs)[i], game.RoundColorAt(i),
            "round colour " + i + " at " + pairs + " pairs");
        }
        Assert.AreEqual(pairs + 1, game.CandidateCount, "candidates = pairs + distractor");
        Assert.AreEqual(MatchMeadowBuilder.DistractorForPairs(pairs), game.DistractorColor,
          "distractor colour at " + pairs + " pairs");
        bool distractorIsTarget = false;
        for (int i = 0; i < pairs; i++) {
          if (game.RoundColorAt(i) == game.DistractorColor) distractorIsTarget = true;
        }
        Assert.IsFalse(distractorIsTarget, "the distractor is never a target colour");
        for (int i = 0; i < game.CandidateCount; i++) {
          MatchItem cand = game.CandidateAt(i);
          Assert.IsNotNull(cand, "candidate " + i);
          Assert.IsFalse(cand.IsReference, "candidate " + i + " is not a reference");
          Assert.AreEqual(game.Family, cand.Family, "candidate " + i + " family");
        }
        UnityEngine.Object.DestroyImmediate(game);
      } finally {
        UnityEngine.Object.DestroyImmediate(player);
        UnityEngine.Object.DestroyImmediate(arena);
      }
    }
  }

  // D. Full flow at 1 pair: intro -> demo (student matches for real) -> handoff
  // -> the child matches -> completion (lifecycle + result + reward).
  [Test] public void P59D_FullFlowAt1Pair() {
    GameObject arena;
    MatchMeadowBuilder builder = BuildArena(out arena, 1);
    GameObject player = BuildPlayer(PadWorld(arena));
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("match_pairs", "test");
      int completed = -1;
      MatchGame game = BuildGame(builder, arena, player, audio, life, 1,
        delegate (int n) { completed = n; });
      AdvanceToPlaying(game, arena, player);
      Assert.AreEqual(MatchGame.Phase.Playing, game.Current, "the child holds control");
      Assert.AreEqual(ActivityState.Active, life.State, "lifecycle active at handoff");
      Assert.AreEqual(0, game.MatchedPairs, "the demo pair tidied home for the child");
      // Every candidate is available again after the tidy.
      for (int i = 0; i < game.CandidateCount; i++)
        Assert.AreEqual(MatchItem.ItemState.Available, game.CandidateAt(i).State,
          "candidate " + i + " reset for the round");
      MatchOne(game, arena, player, game.CandidateAt(0));
      for (int i = 0; i < 30 && game.Current != MatchGame.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(MatchGame.Phase.Success, game.Current, "one pair completes");
      Assert.AreEqual(1, game.MatchedPairs, "exactly one pair matched");
      Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle completed");
      Assert.IsTrue(game.ResultShown, "result board shown");
      Assert.IsTrue(game.RewardShown, "reward bloom shown");
      Assert.AreEqual(1, completed, "the area is notified with the pair count");
      for (int i = 0; i < 40; i++) game.Tick(0.1f); // the paced speech drains
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Look at the board!", "Nhìn lên bảng nhé!")),
        "board line");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Find the same!", "Tìm vật giống mẫu nhé!")),
        "task line");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("You found all the pairs!", "Con ghép đúng hết rồi!")),
        "completion line");
      Assert.IsTrue(audio.Sfx.Contains("pickup") && audio.Sfx.Contains("success"),
        "pickup + success feedback");
    } finally {
      UnityEngine.Object.DestroyImmediate(player);
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }

  // E. Flow at 3 pairs: all three pairs complete; the round uses blocks.
  [Test] public void P59E_FlowAt3Pairs() {
    GameObject arena;
    MatchMeadowBuilder builder = BuildArena(out arena, 3);
    GameObject player = BuildPlayer(PadWorld(arena));
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("match_pairs", "test");
      MatchGame game = BuildGame(builder, arena, player, audio, life, 3);
      AdvanceToPlaying(game, arena, player);
      Assert.AreEqual(2, game.Family, "three pairs use blocks");
      for (int i = 0; i < 3; i++) MatchOne(game, arena, player, game.CandidateAt(i));
      for (int i = 0; i < 30 && game.Current != MatchGame.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(MatchGame.Phase.Success, game.Current, "all three pairs complete");
      Assert.AreEqual(3, game.MatchedPairs, "three pairs matched");
      Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle completed");
      for (int i = 0; i < 30; i++) game.Tick(0.1f);
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Two blue blocks!", "Hai khối xanh dương!")),
        "the first pair is praised on landing");
    } finally {
      UnityEngine.Object.DestroyImmediate(player);
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }

  // F. Wrong match is gentle: no completion, a correction line, the object
  // walks home, then the right object still completes the pair.
  [Test] public void P59F_WrongMatchIsGentle() {
    GameObject arena;
    MatchMeadowBuilder builder = BuildArena(out arena, 1);
    GameObject player = BuildPlayer(PadWorld(arena));
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("match_pairs", "test");
      MatchGame game = BuildGame(builder, arena, player, audio, life, 1);
      AdvanceToPlaying(game, arena, player);
      MatchItem distractor = game.CandidateAt(game.CandidateCount - 1);
      Assert.AreEqual(game.DistractorColor, distractor.ColorId, "the last candidate is the distractor");
      player.transform.position = distractor.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(distractor);
      for (int i = 0; i < 40 && distractor.State != MatchItem.ItemState.Carried; i++) game.Tick(0.1f);
      player.transform.position = PadWorld(arena) + new Vector3(0f, 0f, -1.0f);
      game.TryPlace();
      Assert.AreEqual(MatchGame.Phase.Playing, game.Current, "a wrong match never completes");
      Assert.AreEqual(0, game.MatchedPairs, "no pair counted");
      Assert.AreEqual(1, game.WrongMatches, "the refusal is recorded");
      for (int i = 0; i < 40 && distractor.State != MatchItem.ItemState.Available; i++) game.Tick(0.1f);
      Assert.AreEqual(MatchItem.ItemState.Available, distractor.State, "the wrong object walks home");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Not that one. Find the same!", "Chưa đúng rồi. Tìm lại nhé!")),
        "the teacher corrects gently");
      MatchOne(game, arena, player, game.CandidateAt(0));
      for (int i = 0; i < 30 && game.Current != MatchGame.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(MatchGame.Phase.Success, game.Current, "the right object still completes");
      Assert.AreEqual(1, game.MatchedPairs, "one pair matched");
    } finally {
      UnityEngine.Object.DestroyImmediate(player);
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }

  // G. Spam safety: double pick, pick while carrying, placed objects never
  // re-pick, and a matched pair can never be counted twice.
  [Test] public void P59G_SpamSafety() {
    GameObject arena;
    MatchMeadowBuilder builder = BuildArena(out arena, 2);
    GameObject player = BuildPlayer(PadWorld(arena));
    try {
      FakeAudio audio = new FakeAudio();
      MatchGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("match_pairs", "test"), 2);
      AdvanceToPlaying(game, arena, player);
      MatchItem a = game.CandidateAt(0);
      MatchItem b = game.CandidateAt(1);
      player.transform.position = a.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(a);
      game.TryPick(a); // same object twice
      game.TryPick(b); // while carrying
      for (int i = 0; i < 30; i++) game.Tick(0.1f);
      Assert.AreEqual(a, game.Carried, "exactly one object carried");
      Assert.AreEqual(MatchItem.ItemState.Available, b.State, "the second object untouched");
      game.TryPlace();
      game.TryPlace(); // empty hands
      Assert.AreEqual(0, game.MatchedPairs, "empty-hand places never count (still flying)");
      for (int i = 0; i < 40 && a.State != MatchItem.ItemState.Placed; i++) game.Tick(0.1f);
      Assert.AreEqual(1, game.MatchedPairs, "exactly one pair counted");
      game.TryPick(a); // a placed object never picks again
      Assert.IsNull(game.Carried, "matched objects stay matched");
      // The same-colour candidate cannot match the same reference twice: there
      // is exactly one candidate per target colour.
      int sameColor = 0;
      for (int i = 0; i < game.CandidateCount; i++) {
        MatchItem c = game.CandidateAt(i);
        if (c.ColorId == a.ColorId) sameColor++;
      }
      Assert.AreEqual(1, sameColor, "one candidate per target colour (no duplicate pair)");
    } finally {
      UnityEngine.Object.DestroyImmediate(player);
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }

  // H. Re-entry adopt: a fresh instance on a Completed lifecycle shows the
  // matched pairs (parked objects, result + reward up, no replay).
  [Test] public void P59H_ReentryAdopt() {
    GameObject arena;
    MatchMeadowBuilder builder = BuildArena(out arena, 2);
    GameObject player = BuildPlayer(PadWorld(arena));
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("match_pairs", "test");
      MatchGame first = BuildGame(builder, arena, player, audio, life, 2);
      AdvanceToPlaying(first, arena, player);
      MatchOne(first, arena, player, first.CandidateAt(0));
      MatchOne(first, arena, player, first.CandidateAt(1));
      for (int i = 0; i < 30 && first.Current != MatchGame.Phase.Success; i++) first.Tick(0.1f);
      Assert.AreEqual(ActivityState.Completed, life.State, "setup: lifecycle completed");
      UnityEngine.Object.DestroyImmediate(first);
      MatchGame second = BuildGame(builder, arena, player, new FakeAudio(), life, 2);
      Assert.AreEqual(MatchGame.Phase.Success, second.Current, "adopts success, never replays");
      Assert.AreEqual(2, second.MatchedPairs, "the adopted meadow keeps both pairs");
      Assert.IsTrue(second.ResultShown, "the finished picture shows the result");
      Assert.IsTrue(second.RewardShown, "the reward stays shown");
      for (int i = 0; i < 60; i++) second.Tick(0.1f);
      Assert.AreEqual(MatchGame.Phase.Success, second.Current, "adopt never restarts the lesson");
    } finally {
      UnityEngine.Object.DestroyImmediate(player);
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }

  // I. Lazy contract: one micro scene at a time; the meadow ships in Build
  // Settings as a shell with the expected root.
  [Test] public void P59I_LazySceneContract() {
    Assert.AreEqual("MatchMeadowScene", MatchMeadowBuilder.SceneName, "scene name pinned");
    Assert.Greater(MatchMeadowBuilder.WorldOffset.magnitude, 400f, "separate island");
    var ops = new FakeOps();
    var t = new WorldTransition(SubjectIds.Main);
    var math = new SubjectId("math");
    Assert.IsTrue(t.EnterAsync(ops, math, "MathScene").GetAwaiter().GetResult(), "subject loads");
    Assert.IsFalse(ops.Loaded.Contains(MatchMeadowBuilder.SceneName),
      "the meadow must NOT load at subject entry (lazy)");
    Assert.IsTrue(t.EnterMicroAsync(ops, CountingGardenBuilder.SceneName).GetAwaiter().GetResult(),
      "the garden loads first");
    Assert.IsFalse(t.EnterMicroAsync(ops, MatchMeadowBuilder.SceneName).GetAwaiter().GetResult(),
      "the meadow cannot stack onto the garden (one micro slot)");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "garden unloads");
    Assert.IsTrue(t.EnterMicroAsync(ops, MatchMeadowBuilder.SceneName).GetAwaiter().GetResult(),
      "the meadow loads into the freed slot");
    Assert.IsTrue(ops.Loaded.Contains(MatchMeadowBuilder.SceneName), "it is the live micro scene");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "and unloads cleanly");
    string scenePath = Path.Combine(Application.dataPath,
      "A_World", "MatchMeadow", "MatchMeadowScene.unity");
    Assert.IsTrue(File.Exists(scenePath), "MatchMeadowScene.unity ships in the project");
    string yaml = File.ReadAllText(scenePath);
    Assert.IsTrue(yaml.Contains("MatchMeadowWorld"), "scene root is MatchMeadowWorld");
    string buildSettings = File.ReadAllText(Path.Combine(
      Directory.GetParent(Application.dataPath).FullName, "ProjectSettings", "EditorBuildSettings.asset"));
    Assert.IsTrue(buildSettings.Contains("MatchMeadowScene.unity"), "the scene ships in the build");
  }

  // J. Ladder + CLI + area seams: 1 -> 2 -> 3 -> 1; a completed round advances
  // on leave, an unfinished one never advances.
  [Test] public void P59J_LadderAndCli() {
    int[] expect = { 2, 3, 1 };
    int p = 1;
    foreach (int n in expect) {
      p = MatchArea.NextPairs(p);
      Assert.AreEqual(n, p, "pair ladder rung");
    }
    Assert.AreEqual(1, MatchArea.NextPairs(7), "off-ladder rejoins at 1");
    Assert.AreEqual(3, MatchArea.ParsePairsArg(new[] { "exe", "-match-pairs", "3" }, 1),
      "the flag pins a pair count");
    Assert.AreEqual(1, MatchArea.ParsePairsArg(new[] { "exe", "-match-pairs", "9" }, 1),
      "out-of-range flag stays inert");
    GameObject go = new GameObject("P59AreaSeams");
    try {
      MatchArea area = go.AddComponent<MatchArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      area.SetPairsForTests(1);
      Assert.IsTrue(area.TryEnterForTests(), "enter");
      area.TickProgressionForTests();
      Assert.AreEqual(1, area.Pairs, "mid-round keeps the pair count");
      CompleteLife(area.Lifecycle);
      Assert.IsTrue(area.TryExitForTests(), "leave after completing");
      Assert.AreEqual(2, area.Pairs, "leaving advances exactly one rung");
      Assert.AreEqual(ActivityState.Unavailable, area.Lifecycle.State, "fresh life for the next round");
      Assert.IsFalse(area.TryExitForTests(), "double leave is refused");
      Assert.AreEqual(2, area.Pairs, "no double advance");
      Assert.IsTrue(area.TryEnterForTests(), "back in for the next round");
      Assert.IsTrue(area.TryExitForTests(), "leave mid-round");
      Assert.AreEqual(2, area.Pairs, "an unfinished round never advances");
    } finally { UnityEngine.Object.DestroyImmediate(go); }
  }

  // K. Speech safety at 3 pairs: every line a full round can produce passes the
  // NPC cap in the active language (recorded from a real 3-pair run).
  [Test] public void P59K_LinesSafetyAt3() {
    GameObject arena;
    MatchMeadowBuilder builder = BuildArena(out arena, 3);
    GameObject player = BuildPlayer(PadWorld(arena));
    try {
      FakeAudio audio = new FakeAudio();
      MatchGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("match_pairs", "test"), 3);
      AdvanceToPlaying(game, arena, player);
      // One deliberate wrong match to record the correction line too.
      MatchItem distractor = game.CandidateAt(game.CandidateCount - 1);
      player.transform.position = distractor.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(distractor);
      for (int i = 0; i < 40 && distractor.State != MatchItem.ItemState.Carried; i++) game.Tick(0.1f);
      player.transform.position = PadWorld(arena) + new Vector3(0f, 0f, -1.0f);
      game.TryPlace();
      for (int i = 0; i < 40 && distractor.State != MatchItem.ItemState.Available; i++) game.Tick(0.1f);
      for (int i = 0; i < 3; i++) MatchOne(game, arena, player, game.CandidateAt(i));
      for (int i = 0; i < 30 && game.Current != MatchGame.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(MatchGame.Phase.Success, game.Current, "three pairs complete");
      for (int i = 0; i < 60; i++) game.Tick(0.1f);
      Assert.Greater(audio.Lines.Count, 5, "a full round speaks plenty");
      foreach (string line in audio.Lines) {
        bool ok = SafetyFilter.ValidateLine(line, false, out string why);
        Assert.IsTrue(ok, "line passes the NPC cap: '" + line + "' (" + why + ")");
      }
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Two red blocks!", "Hai khối đỏ!")),
        "the second pair is praised with its colour");
    } finally {
      UnityEngine.Object.DestroyImmediate(player);
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }

  // L. Carry rides the child's REAL fist (same lesson as #4/#5).
  [Test] public void P59L_CarryFollowsTheHand() {
    GameObject arena;
    MatchMeadowBuilder builder = BuildArena(out arena, 1);
    GameObject player = BuildPlayer(PadWorld(arena));
    GameObject hand = new GameObject("P59Hand");
    try {
      FakeAudio audio = new FakeAudio();
      MatchGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("match_pairs", "test"), 1, null, hand.transform);
      AdvanceToPlaying(game, arena, player);
      MatchItem item = game.CandidateAt(0);
      player.transform.position = item.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(item);
      for (int i = 0; i < 40 && item.State != MatchItem.ItemState.Carried; i++) game.Tick(0.1f);
      Assert.AreEqual(MatchItem.ItemState.Carried, item.State, "object carried");
      Vector3 across = PadWorld(arena) + new Vector3(2.4f, 1.3f, 0f);
      hand.transform.position = across;
      for (int i = 0; i < 30; i++) game.Tick(0.05f);
      Assert.Less(Vector3.Distance(item.transform.position, across), 0.3f,
        "the object rides the fist");
      Assert.Greater(Vector3.Distance(item.transform.position, player.transform.position), 2.0f,
        "a root-glued object would sit at the child's feet — impossible here");
    } finally {
      UnityEngine.Object.DestroyImmediate(hand);
      UnityEngine.Object.DestroyImmediate(player);
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }
}
