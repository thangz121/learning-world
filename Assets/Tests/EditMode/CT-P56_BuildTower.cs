// CT-P56: GAMEPLAY #4 — "XÂY THÁP THEO SỐ" (build the tower by number).
// Pins the Math Hub build_yard gate (walk-in portal + progress landmark), the
// child-scale Build Yard arena (board + block yard + build pad + ghost slot +
// result + cameras), the deterministic block lifecycle (Available -> Picked ->
// Carried -> Placed, stack index at place time), the full activity flow
// (teacher intro -> student demo -> handoff -> child build -> success),
// overshoot correction (guidance, never failure), undershoot nudges, spam
// safety, re-entry adopt, the lazy BuildTowerScene contract, the 1..9 ladder +
// CLI, the speech/SFX beats through the reference audio path, carry robustness
// and the tower-height success camera formula.
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class CT_P56_BuildTower {
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

  static BuildTowerBuilder BuildArena(out GameObject arena, int target = 3) {
    arena = new GameObject("P56BuildWorld");
    BuildTowerBuilder builder = arena.AddComponent<BuildTowerBuilder>();
    builder.BoardTarget = BuildTowerBuilder.ClampTarget(target);
    builder.BuildContent(arena.transform);
    return builder;
  }

  static BuildTowerGame BuildGame(BuildTowerBuilder builder, GameObject arena,
      GameObject player, FakeAudio audio, ActivityLifecycle life, int target,
      System.Action<int> onCompleted = null) {
    BuildTowerGame game = arena.AddComponent<BuildTowerGame>();
    game.Build(builder, player.transform, null, audio, life, target, onCompleted);
    return game;
  }

  static GameObject BuildPlayer(Vector3 at) {
    GameObject player = new GameObject("P56Player");
    player.transform.position = at;
    return player;
  }

  static Vector3 PadWorld(GameObject arena) {
    return arena.transform.TransformPoint(BuildTowerBuilder.PadPos);
  }

  static void AdvanceToBuilding(BuildTowerGame game, float maxSeconds = 220f) {
    for (float t = 0f; t < maxSeconds && game.Current != BuildTowerGame.Phase.Building; t += 0.1f)
      game.Tick(0.1f);
  }

  // Drive one full player placement: stand at the block, pick, wait for the
  // carry, stand at the pad, place, wait for the real landing.
  static void PlaceOne(BuildTowerGame game, GameObject arena, GameObject player, int idx) {
    TowerBlock b = game.BlockAt(idx);
    Assert.IsNotNull(b, "block " + idx + " exists");
    player.transform.position = b.transform.position + new Vector3(0f, 0f, -0.5f);
    game.TryPick(b);
    for (int i = 0; i < 40 && b.State != TowerBlock.BlockState.Carried; i++) game.Tick(0.1f);
    Assert.AreEqual(TowerBlock.BlockState.Carried, b.State, "block " + idx + " rides the hand");
    player.transform.position = PadWorld(arena) + new Vector3(0f, 0f, -1.0f);
    game.TryPlace();
    for (int i = 0; i < 40 && b.State != TowerBlock.BlockState.Placed; i++) game.Tick(0.1f);
    Assert.AreEqual(TowerBlock.BlockState.Placed, b.State, "block " + idx + " landed on the tower");
  }

  // A. Math Hub: the build_yard gate owns a walk-in portal (whole-arch, cold
  // start, its OWN area id) + the progress landmark; the hub return spot
  // clears the portal's re-arm radius.
  [Test] public void P56A_GatePortalAndLandmark() {
    GameObject root = new GameObject("P56MathWorld");
    try {
      MathWorldBuilder builder = root.AddComponent<MathWorldBuilder>();
      builder.BuildContent(root.transform);
      MicroWorldGate gate = builder.FindMicroGate("build_yard");
      Assert.IsNotNull(gate, "the build_yard gate exists");
      Assert.AreEqual("build_yard", gate.gateId, "the human-reviewed gate id intact");
      Assert.IsFalse(string.IsNullOrEmpty(gate.displayName), "the gate keeps its child-facing label");
      MicroWorldPortal portal = builder.BuildTowerPortal;
      Assert.IsNotNull(portal, "the build gate has a walk-in portal");
      Assert.IsFalse(portal.ExitMode, "it is an ENTER portal");
      Assert.IsFalse(portal.PlayExit, "it is not a play-arena exit");
      Assert.AreEqual(BuildTowerArea.AreaId, portal.areaId, "it targets the Build Yard area");
      Assert.AreEqual(1.8f, portal.fireRadius, "whole-arch coverage radius");
      Vector3 toHub = -new Vector3(gate.transform.position.x, 0f, gate.transform.position.z).normalized;
      Vector3 want = gate.transform.position + toHub * 0.7f;
      Assert.Less(Dist2D(portal.transform.position, want), 0.05f,
        "the portal covers the whole arch (0.7m hub-side of the gate centre)");
      Vector3 hubReturn = MathWorldBuilder.WorldOffset + MathWorldBuilder.BuildYardHubReturnLocal;
      Assert.Greater(Dist2D(hubReturn, portal.transform.position),
        portal.fireRadius + portal.rearmMargin,
        "exit landing clears the portal re-arm radius (no instant re-entry)");
      // The progress landmark: 9 stack slots + a 3-block preview, bake-safe.
      BuildYardLandmark landmark = builder.BuildTowerLandmark;
      Assert.IsNotNull(landmark, "the gate carries the progress landmark");
      Assert.AreEqual(3, landmark.ShownHeight, "preview height before any completion");
      for (int i = 0; i < BuildTowerBuilder.MaxTarget; i++)
        Assert.IsNotNull(FindDeep(landmark.transform, "MathBuildYardTower" + i), "landmark block " + i);
      Assert.IsNull(FindDeep(landmark.transform, "MathBuildYardTower" + BuildTowerBuilder.MaxTarget),
        "no tenth landmark block");
      landmark.ShowHeight(5);
      Assert.AreEqual(5, landmark.ShownHeight, "the landmark reflects a completed height");
      Transform b5 = FindDeep(landmark.transform, "MathBuildYardTower4");
      Assert.IsTrue(b5 != null && b5.gameObject.activeSelf, "block 5 shown");
      Transform b6 = FindDeep(landmark.transform, "MathBuildYardTower5");
      Assert.IsTrue(b6 != null && !b6.gameObject.activeSelf, "block 6 hidden");
    } finally { Object.DestroyImmediate(root); }
  }

  // B. Arena structure: board + yard + pad + ghost + result + cameras,
  // child-scale loop, and the block stack geometry stays sane at 9.
  [Test] public void P56B_ArenaStructure() {
    GameObject arena;
    BuildTowerBuilder builder = BuildArena(out arena);
    try {
      Assert.IsNotNull(FindDeep(arena.transform, "BTNumberDigit"), "board digit staged");
      for (int i = 0; i < BuildTowerBuilder.BlockCount; i++)
        Assert.IsNotNull(FindDeep(arena.transform, "BTBlock" + i), "block " + i + " staged");
      Assert.AreEqual(10, builder.Blocks.Count, "ten blocks: the target plus a spare");
      Assert.IsNotNull(FindDeep(arena.transform, "BTPadPlatform"), "build pad staged");
      for (int i = 0; i < 4; i++)
        Assert.IsNotNull(FindDeep(arena.transform, "BTPadPeg" + i), "pad peg " + i);
      Assert.IsNotNull(builder.Ghost, "ghost slot marker staged");
      Assert.IsFalse(builder.Ghost.activeSelf, "ghost hidden until a block is carried");
      Assert.IsNotNull(builder.PadAnchor, "pad door anchor exposed");
      Assert.IsNotNull(builder.Result, "result board staged");
      Assert.IsFalse(builder.Result.activeSelf, "result hidden before success");
      foreach (string n in new[] { "BTCamA", "BTLookA", "BTCamB", "BTLookB", "BTCamC", "BTLookC" })
        Assert.IsNotNull(FindDeep(arena.transform, n), "camera marker " + n);
      Assert.IsNotNull(builder.Anchors, "anchor registry");
      Assert.IsNotNull(builder.EntryPoint, "entry marker");
      Assert.IsNotNull(builder.ExitPortal, "exit portal");
      Assert.IsTrue(builder.ExitPortal.ExitMode, "exit returns to the hub");
      Assert.AreEqual(BuildTowerArea.AreaId, builder.ExitPortal.areaId, "exit targets the area");
      // Child scale: entry -> yard -> pad is a short loop, never a hike.
      float entryYard = Dist2D(BuildTowerBuilder.EntryLocal, BuildTowerBuilder.YardStand);
      float yardPad = Dist2D(BuildTowerBuilder.YardCenter, BuildTowerBuilder.PadPos);
      Assert.Less(entryYard, 6f, "entry to block yard is a short walk");
      Assert.Less(yardPad, 6f, "yard to pad is a short walk");
      // Blocks are pickable-sized, bake-ignored (they MOVE), collider-stripped
      // until the game re-adds the click collider.
      GameObject block0 = builder.Blocks[0];
      Assert.Greater(block0.transform.localScale.x, 0.35f, "block readable for small hands");
      Assert.Less(block0.transform.localScale.x, 0.75f, "block kid-scaled");
      Assert.IsTrue(IsIgnoredFromBuild(block0), "a movable block never bakes");
      Assert.IsTrue(IsIgnoredFromBuild(FindDeep(arena.transform, "BTPadPlatform").gameObject),
        "the pad graphic never bakes (the ground does)");
      // Stack geometry at 9: rising slots, touching (never intersecting) faces,
      // top inside the arena silhouette.
      Vector3 pad = BuildTowerBuilder.PadPos;
      for (int i = 0; i < BuildTowerBuilder.MaxTarget; i++) {
        Vector3 slot = BuildTowerBuilder.StackSlot(i, pad);
        Assert.AreEqual(pad.x, slot.x, 0.001f, "slot x rides the pad");
        Assert.AreEqual(pad.z, slot.z, 0.001f, "slot z rides the pad");
        Assert.AreEqual(BuildTowerBuilder.PadTopY + BuildTowerBuilder.BlockHalfH
          + i * BuildTowerBuilder.BlockRise, slot.y, 0.001f, "slot " + i + " height");
      }
      Assert.GreaterOrEqual(BuildTowerBuilder.BlockRise, BuildTowerBuilder.BlockSize.y - 0.001f,
        "stacking rise >= block height: blocks touch, never intersect");
      float top9 = BuildTowerBuilder.TowerTopY(9);
      Assert.Greater(top9, 3.5f, "a 9-tower really rises");
      Assert.Less(top9, 5.0f, "a 9-tower stays under the arena silhouette");
    } finally { Object.DestroyImmediate(arena); }
  }

  // C. Target validity 1..9: clamp + staged digit + game target (brief §17).
  [Test] public void P56C_TargetValidity() {
    Assert.AreEqual(1, BuildTowerBuilder.ClampTarget(0), "0 clamps to 1");
    Assert.AreEqual(9, BuildTowerBuilder.ClampTarget(10), "10 clamps to 9");
    Assert.AreEqual(9, BuildTowerBuilder.MaxTarget, "maximum is 9");
    for (int t = 1; t <= 9; t++) {
      GameObject arena;
      BuildTowerBuilder builder = BuildArena(out arena, t);
      GameObject player = BuildPlayer(BuildTowerBuilder.EntryLocal);
      try {
        Assert.IsNotNull(FindDeep(arena.transform, "BTNumberDigit"), "digit staged at " + t);
        FakeAudio audio = new FakeAudio();
        BuildTowerGame game = BuildGame(builder, arena, player, audio,
          new ActivityLifecycle("build_tower", "test"), t);
        Assert.AreEqual(t, game.Target, "game builds target " + t);
        Assert.GreaterOrEqual(game.BlockCountTotal, t, "the yard always has enough blocks");
        Object.DestroyImmediate(game);
      } finally {
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(arena);
      }
    }
  }

  // D. Full flow at 3: intro -> demo (3 real stackings) -> handoff (the demo
  // tower tidies home) -> child builds 3 -> success (lifecycle + result +
  // landmark notification + recap).
  [Test] public void P56D_FullFlowAt3() {
    GameObject arena;
    BuildTowerBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(BuildTowerBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("build_tower", "test");
      int completed = -1;
      BuildTowerGame game = BuildGame(builder, arena, player, audio, life, 3,
        delegate (int n) { completed = n; });
      AdvanceToBuilding(game);
      Assert.AreEqual(BuildTowerGame.Phase.Building, game.Current, "the child holds control");
      Assert.AreEqual(3, game.DemoBlocksPlaced, "the demo stacked exactly 3");
      Assert.AreEqual(ActivityState.Active, life.State, "lifecycle active at handoff");
      // The demo tower tidied home: every block is pickable again.
      for (int i = 0; i < game.BlockCountTotal; i++)
        Assert.AreEqual(TowerBlock.BlockState.Available, game.BlockAt(i).State,
          "block " + i + " reset for the round");
      Assert.AreEqual(0, game.TowerHeight, "the pad starts empty for the child");
      PlaceOne(game, arena, player, 0);
      Assert.AreEqual(1, game.Count, "one placed");
      Assert.AreEqual(0, game.PlacedAt(0).PlacedIndex, "the first block is stack index 0");
      PlaceOne(game, arena, player, 1);
      PlaceOne(game, arena, player, 2);
      for (int i = 0; i < 20 && game.Current != BuildTowerGame.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(BuildTowerGame.Phase.Success, game.Current, "three blocks complete");
      Assert.AreEqual(3, game.Count, "count is exactly the target");
      Assert.AreEqual(3, game.TowerHeight, "the tower height IS the count");
      Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle completed");
      Assert.IsTrue(game.ResultShown, "result board shown");
      Assert.AreEqual(3, completed, "the area is notified with the completed target");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Three blocks! Well done!", "Ba khối! Giỏi!")),
        "the teacher confirms the target");
      for (int i = 0; i < 40; i++) game.Tick(0.1f);
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("You built a tower of three!", "Con xây được tháp ba khối!")),
        "the teacher names the finished tower (brief §23)");
      Assert.IsTrue(audio.Sfx.Contains("block"), "each placement clacks");
      Assert.IsTrue(audio.Sfx.Contains("success"), "success chime plays");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // E. Undershoot: 2 of 5 placed and settled earns a gentle "how many more" —
  // never a fail, never a completion.
  [Test] public void P56E_UndershootNudge() {
    GameObject arena;
    BuildTowerBuilder builder = BuildArena(out arena, 5);
    GameObject player = BuildPlayer(BuildTowerBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      BuildTowerGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("build_tower", "test"), 5);
      AdvanceToBuilding(game);
      PlaceOne(game, arena, player, 0);
      PlaceOne(game, arena, player, 1);
      Assert.AreEqual(BuildTowerGame.Phase.Building, game.Current, "short of the target: still building");
      player.transform.position = PadWorld(arena) + new Vector3(0f, 0f, -1.0f);
      for (int i = 0; i < 140; i++) game.Tick(0.1f); // settle: the nudge fires
      Assert.GreaterOrEqual(game.UndershootNudges, 1, "a gentle nudge names the remainder");
      Assert.AreEqual(BuildTowerGame.Phase.Building, game.Current, "no completion while short");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("Three more blocks!", "Còn ba khối nữa nhé!")), "the nudge counts the rest");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // F. Overshoot at 3: the 4th block is NOT success — gentle correction
  // ("Đủ ba khối rồi."), the extra hops home, the tower stays exactly 3.
  [Test] public void P56F_OvershootAt3() {
    GameObject arena;
    BuildTowerBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(BuildTowerBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("build_tower", "test");
      BuildTowerGame game = BuildGame(builder, arena, player, audio, life, 3);
      AdvanceToBuilding(game);
      PlaceOne(game, arena, player, 0);
      PlaceOne(game, arena, player, 1);
      PlaceOne(game, arena, player, 2);
      for (int i = 0; i < 20 && game.Current != BuildTowerGame.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(BuildTowerGame.Phase.Success, game.Current, "setup: success at 3");
      TowerBlock extra = game.BlockAt(3);
      player.transform.position = extra.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(extra);
      for (int i = 0; i < 40 && extra.State != TowerBlock.BlockState.Carried; i++) game.Tick(0.1f);
      player.transform.position = PadWorld(arena) + new Vector3(0f, 0f, -1.0f);
      game.TryPlace();
      Assert.AreEqual(BuildTowerGame.Phase.Correct, game.Current, "the 4th place corrects, never succeeds");
      Assert.AreEqual(1, game.Overshoots, "one overshoot recorded");
      for (int i = 0; i < 30 && extra.State != TowerBlock.BlockState.Placed; i++) game.Tick(0.1f);
      Assert.AreEqual(TowerBlock.BlockState.Placed, extra.State, "the extra really lands on top first");
      for (int i = 0; i < 220 && game.Current != BuildTowerGame.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(BuildTowerGame.Phase.Success, game.Current, "the correction lands back on success");
      Assert.AreEqual(3, game.Count, "count stays exactly the target");
      Assert.AreEqual(3, game.TowerHeight, "the tower is exactly 3 blocks high");
      for (int i = 0; i < 40 && extra.State != TowerBlock.BlockState.Available; i++) game.Tick(0.1f);
      Assert.AreEqual(TowerBlock.BlockState.Available, extra.State, "the extra hops home");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Three is enough.", "Đủ ba khối rồi.")),
        "the teacher names the limit gently");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("The board says three.", "Bảng ghi số ba.")),
        "the teacher points back at the board");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // G. Spam safety: double-pick, pick-while-carrying, place-with-empty-hands —
  // none of them double-count (brief §12/§27: no double-count, no punishment).
  [Test] public void P56G_SpamSafety() {
    GameObject arena;
    BuildTowerBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(BuildTowerBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      BuildTowerGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("build_tower", "test"), 3);
      AdvanceToBuilding(game);
      TowerBlock b0 = game.BlockAt(0);
      TowerBlock b1 = game.BlockAt(1);
      player.transform.position = b0.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(b0);
      game.TryPick(b0); // same block twice
      game.TryPick(b1); // while carrying
      for (int i = 0; i < 30; i++) game.Tick(0.1f);
      Assert.AreEqual(b0, game.Carried, "exactly one block carried");
      Assert.AreEqual(TowerBlock.BlockState.Available, b1.State, "the second block untouched");
      game.TryPlace();
      int afterFirst = game.Count;
      game.TryPlace(); // empty hands
      Assert.AreEqual(afterFirst, game.Count, "empty-hand places never count");
      for (int i = 0; i < 30 && b0.State != TowerBlock.BlockState.Placed; i++) game.Tick(0.1f);
      Assert.AreEqual(1, game.Count, "exactly one placement counted");
      game.TryPick(b0); // a placed block never picks again
      Assert.IsNull(game.Carried, "placed blocks stay placed");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // H. Block lifecycle + stack index determinism (brief §9/§10).
  [Test] public void P56H_BlockLifecycleAndStackIndex() {
    GameObject arena;
    BuildTowerBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(BuildTowerBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      BuildTowerGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("build_tower", "test"), 3);
      AdvanceToBuilding(game);
      TowerBlock b = game.BlockAt(4);
      Assert.AreEqual(TowerBlock.BlockState.Available, b.State, "starts available");
      player.transform.position = b.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(b);
      Assert.AreEqual(TowerBlock.BlockState.Picked, b.State, "picked while the hand comes down");
      for (int i = 0; i < 30 && b.State != TowerBlock.BlockState.Carried; i++) game.Tick(0.1f);
      Assert.AreEqual(TowerBlock.BlockState.Carried, b.State, "then carried");
      int slot = game.Count;
      player.transform.position = PadWorld(arena) + new Vector3(0f, 0f, -1.0f);
      game.TryPlace();
      for (int i = 0; i < 30 && b.State != TowerBlock.BlockState.Placed; i++) game.Tick(0.1f);
      Assert.AreEqual(TowerBlock.BlockState.Placed, b.State, "then placed");
      Assert.AreEqual(slot, b.PlacedIndex, "the stack index is the real placed order");
      Vector3 slotLocal = BuildTowerBuilder.StackSlot(slot, BuildTowerBuilder.PadPos);
      Assert.Less(Vector3.Distance(b.transform.localPosition, slotLocal), 0.001f,
        "the block sits exactly on its slot (snapped)");
      // The correction trip home restores availability (the spare block).
      TowerBlock e = game.BlockAt(5);
      player.transform.position = e.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(e);
      for (int i = 0; i < 30 && e.State != TowerBlock.BlockState.Carried; i++) game.Tick(0.1f);
      e.BeginReturnHome();
      for (int i = 0; i < 30 && e.State != TowerBlock.BlockState.Available; i++) game.Tick(0.1f);
      Assert.AreEqual(TowerBlock.BlockState.Available, e.State, "returned blocks pick again");
      Assert.AreEqual(-1, e.PlacedIndex, "a returned block forgets its index");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // I. Re-entry adopt: a fresh instance on a Completed lifecycle shows the
  // finished tower (parked blocks, result up, no replay, field stays playable).
  [Test] public void P56I_ReentryAdopt() {
    GameObject arena;
    BuildTowerBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(BuildTowerBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("build_tower", "test");
      BuildTowerGame first = BuildGame(builder, arena, player, audio, life, 3);
      AdvanceToBuilding(first);
      PlaceOne(first, arena, player, 0);
      PlaceOne(first, arena, player, 1);
      PlaceOne(first, arena, player, 2);
      for (int i = 0; i < 20 && first.Current != BuildTowerGame.Phase.Success; i++) first.Tick(0.1f);
      Assert.AreEqual(ActivityState.Completed, life.State, "setup: lifecycle completed");
      Object.DestroyImmediate(first);
      BuildTowerGame second = BuildGame(builder, arena, player, new FakeAudio(), life, 3);
      Assert.AreEqual(BuildTowerGame.Phase.Success, second.Current, "adopts success, never replays");
      Assert.AreEqual(3, second.Count, "the adopted tower keeps its height");
      Assert.IsTrue(second.ResultShown, "the finished picture shows the result");
      for (int i = 0; i < 3; i++) {
        TowerBlock b = second.BlockAt(i);
        Assert.AreEqual(TowerBlock.BlockState.Placed, b.State, "adopted block " + i + " stays placed");
        Assert.AreEqual(i, b.PlacedIndex, "adopted stack order " + i);
      }
      for (int i = 3; i < second.BlockCountTotal; i++)
        Assert.AreEqual(TowerBlock.BlockState.Removed, second.BlockAt(i).State,
          "unused blocks are scenery on re-entry");
      for (int i = 0; i < 60; i++) second.Tick(0.1f);
      Assert.AreEqual(BuildTowerGame.Phase.Success, second.Current, "adopt never restarts the lesson");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // J. Lazy contract: one micro scene at a time (the shared slot refuses a
  // second load), the scene ships in Build Settings, never at boot.
  [Test] public void P56J_LazySceneContract() {
    Assert.AreEqual("BuildTowerScene", BuildTowerBuilder.SceneName, "scene name pinned");
    Assert.Greater(BuildTowerBuilder.WorldOffset.magnitude, 200f, "separate island (no overlap)");
    var t = new WorldTransition(SubjectIds.Main);
    var ops = new FakeOps();
    var math = new SubjectId("math");
    Assert.IsTrue(t.EnterAsync(ops, math, "MathScene").GetAwaiter().GetResult(), "subject loads");
    Assert.IsFalse(ops.Loaded.Contains(BuildTowerBuilder.SceneName),
      "the build yard must NOT be loaded at subject entry (lazy)");
    Assert.IsTrue(t.EnterMicroAsync(ops, CountingGardenBuilder.SceneName).GetAwaiter().GetResult(),
      "the garden loads first");
    Assert.IsFalse(t.EnterMicroAsync(ops, BuildTowerBuilder.SceneName).GetAwaiter().GetResult(),
      "the build yard cannot stack onto the garden (one micro slot)");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "garden unloads");
    Assert.IsTrue(t.EnterMicroAsync(ops, BuildTowerBuilder.SceneName).GetAwaiter().GetResult(),
      "the build yard loads into the freed slot");
    Assert.IsTrue(ops.Loaded.Contains(BuildTowerBuilder.SceneName), "it is now the live micro scene");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "and unloads cleanly");
    string scenePath = Path.Combine(Application.dataPath,
      "A_World", "BuildYard", "BuildTowerScene.unity");
    Assert.IsTrue(File.Exists(scenePath), "BuildTowerScene.unity ships in the project");
    string yaml = File.ReadAllText(scenePath);
    Assert.IsTrue(yaml.Contains("BuildTowerWorld"), "scene root is BuildTowerWorld");
    string buildSettings = File.ReadAllText(Path.Combine(
      Directory.GetParent(Application.dataPath).FullName, "ProjectSettings", "EditorBuildSettings.asset"));
    Assert.IsTrue(buildSettings.Contains("BuildTowerScene.unity"), "the scene ships in the build");
  }

  // K. Ladder + CLI: 3 -> 5 -> 7 -> 9 -> 1 -> 3 (brief test targets 1,3,5,7,9
  // all reachable), diagnostic flag inert, area seams.
  [Test] public void P56K_LadderAndCli() {
    int[] expect = { 5, 7, 9, 1, 3 };
    int t = 3;
    foreach (int n in expect) {
      t = BuildTowerArea.NextTarget(t);
      Assert.AreEqual(n, t, "ladder rung");
    }
    Assert.AreEqual(3, BuildTowerArea.NextTarget(6), "off-ladder rejoins at 3");
    Assert.AreEqual(7, BuildTowerArea.ParseTargetArg(
      new[] { "exe", "-build-target", "7" }, 3), "flag pins a target");
    Assert.AreEqual(3, BuildTowerArea.ParseTargetArg(
      new[] { "exe", "-build-target", "99" }, 3), "out-of-range flag stays inert");
    Assert.AreEqual(3, BuildTowerArea.ParseTargetArg(new[] { "exe" }, 3), "no flag keeps the ladder");
    GameObject go = new GameObject("P56AreaSeams");
    try {
      BuildTowerArea area = go.AddComponent<BuildTowerArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      Assert.IsFalse(area.IsInside, "starts outside");
      Assert.IsTrue(area.TryEnterForTests(), "enter");
      Assert.IsFalse(area.TryEnterForTests(), "double enter blocked");
      Assert.IsTrue(area.TryExitForTests(), "exit");
      Assert.IsFalse(area.TryExitForTests(), "double exit blocked");
      area.SetTargetForTests(3);
      Assert.AreEqual(3, area.Target, "ladder starts at the reference 3");
      area.TickProgressionForTests();
      Assert.AreEqual(3, area.Target, "mid-lesson re-entry keeps the target");
      BuildYardLandmark landmark = new GameObject("P56Landmark").AddComponent<BuildYardLandmark>();
      landmark.Build(go.transform, Vector3.zero);
      area.Landmark = landmark;
      area.NotifyCompleted(7);
      Assert.AreEqual(7, landmark.ShownHeight, "a completed tower grows the hub landmark");
      Assert.AreEqual(7, area.LastCompletedTarget, "the area remembers the completed height");
    } finally { Object.DestroyImmediate(go); }
  }

  // L. Speech safety at 9: EVERY line a full round can produce (intro, demo
  // counts, handoff, player counts, recap, nudges, correction) passes the NPC
  // cap in the active language — recorded from a REAL target-9 run.
  [Test] public void P56L_LinesSafetyAt9() {
    GameObject arena;
    BuildTowerBuilder builder = BuildArena(out arena, 9);
    GameObject player = BuildPlayer(BuildTowerBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      BuildTowerGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("build_tower", "test"), 9);
      AdvanceToBuilding(game, 420f);
      Assert.AreEqual(BuildTowerGame.Phase.Building, game.Current, "target 9 reaches the child");
      Assert.AreEqual(9, game.DemoBlocksPlaced, "the demo stacked all nine");
      for (int i = 0; i < 9; i++) PlaceOne(game, arena, player, i);
      for (int i = 0; i < 30 && game.Current != BuildTowerGame.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(BuildTowerGame.Phase.Success, game.Current, "target 9 completes");
      Assert.AreEqual(9, game.TowerHeight, "a real nine-block tower stands");
      for (int i = 0; i < 220; i++) game.Tick(0.1f); // recap drains
      Assert.Greater(audio.Lines.Count, 25, "a full round speaks plenty");
      foreach (string line in audio.Lines) {
        bool ok = SafetyFilter.ValidateLine(line, false, out string why);
        Assert.IsTrue(ok, "line passes the NPC cap: '" + line + "' (" + why + ")");
      }
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Nine blocks.", "Chín khối.")),
        "nine counted");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("Build a tower of nine!", "Xây tháp chín khối nhé!")), "target named");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("You built a tower of nine!", "Con xây được tháp chín khối!")),
        "the closer names the finished tower (brief §23)");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // M. Carry robustness (brief §8): a picked block never drops mid-walk, never
  // vanishes on a long trek, and places fine afterwards.
  [Test] public void P56M_CarryRobustness() {
    GameObject arena;
    BuildTowerBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(BuildTowerBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      BuildTowerGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("build_tower", "test"), 3);
      AdvanceToBuilding(game);
      TowerBlock b = game.BlockAt(2);
      player.transform.position = b.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(b);
      for (int i = 0; i < 20 && b.State != TowerBlock.BlockState.Carried; i++) game.Tick(0.1f);
      // A long trek around the arena and back: still carried, never duplicated.
      player.transform.position = BuildTowerBuilder.YardStand;
      for (int i = 0; i < 80; i++) game.Tick(0.1f);
      Assert.AreEqual(TowerBlock.BlockState.Carried, b.State, "the block survives the walk back");
      player.transform.position = BuildTowerBuilder.EntryLocal;
      for (int i = 0; i < 80; i++) game.Tick(0.1f);
      Assert.AreEqual(TowerBlock.BlockState.Carried, b.State, "the block survives the far trek");
      Assert.AreEqual(b, game.Carried, "the hand never loses it");
      Assert.AreEqual(0, game.Count, "a carried block is never counted early");
      player.transform.position = PadWorld(arena) + new Vector3(0f, 0f, -1.0f);
      game.TryPlace();
      for (int i = 0; i < 40 && b.State != TowerBlock.BlockState.Placed; i++) game.Tick(0.1f);
      Assert.AreEqual(TowerBlock.BlockState.Placed, b.State, "it places fine afterwards");
      Assert.AreEqual(1, game.Count, "counted exactly once");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // N. Tower-height success camera (brief §18): the framing formula grows with
  // the REAL tower; a 9-tower stays inside the arena and inside the frame.
  [Test] public void P56N_CameraHeightFormula() {
    Assert.AreEqual(BuildTowerBuilder.PadTopY, BuildTowerBuilder.TowerTopY(0), 0.001f,
      "an empty pad tops out at the pad");
    float prev = -1f;
    for (int n = 0; n <= 9; n++) {
      float top = BuildTowerBuilder.TowerTopY(n);
      Assert.Greater(top, prev, "the tower really rises with each block");
      prev = top;
    }
    // The success formula the game uses: cam rises 0.15*top, look sits at
    // mid-tower — one proportional formula, no per-target constants.
    Vector3 camBase = new Vector3(2.9f, 2.0f, -0.6f);
    float top9 = BuildTowerBuilder.TowerTopY(9);
    Vector3 cam = camBase + new Vector3(0f, top9 * 0.15f, -top9 * 0.20f);
    Vector3 look = BuildTowerBuilder.PadPos + new Vector3(0f, BuildTowerBuilder.PadTopY + top9 * 0.5f, 0f);
    Assert.Greater(cam.y, camBase.y, "the camera rises with a tall tower");
    Assert.Less(cam.z, camBase.z, "and pulls back so the top stays in frame");
    Assert.Less(Mathf.Abs(cam.x), BuildTowerBuilder.BoundX, "camera stays in the arena");
    Assert.Less(Mathf.Abs(cam.z), BuildTowerBuilder.BoundZ, "camera stays in the arena");
    Assert.Greater(look.y, 1.0f, "the look point rises above the pad");
    float distance = Dist2D(cam, look);
    float halfFrame = Mathf.Tan(30f * Mathf.Deg2Rad) * distance;
    Assert.Greater(look.y + halfFrame, top9, "the tower top stays inside the frame");
    Assert.Less(look.y - halfFrame, BuildTowerBuilder.PadTopY + 0.6f,
      "the pad stays inside the frame");
  }

  static void CompleteLife(ActivityLifecycle life) {
    life.MarkAvailable("test");
    life.BeginEnter("test");
    life.MarkReady("test");
    life.Begin("test");
    life.MarkCompleted("test");
  }

  // O. The carried block rides the child's REAL fist (brief §13): with a hand
  // handed in, the block follows it across the arena — never glued to the root
  // — and the installer passes PlayerVisual.HandBone down to the activity.
  [Test] public void P56O_CarryFollowsTheHand() {
    GameObject arena;
    BuildTowerBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(BuildTowerBuilder.EntryLocal);
    GameObject hand = new GameObject("P56Hand");
    hand.transform.position = BuildTowerBuilder.EntryLocal + new Vector3(0f, 1.1f, 0f);
    try {
      FakeAudio audio = new FakeAudio();
      BuildTowerGame game = arena.AddComponent<BuildTowerGame>();
      game.Build(builder, player.transform, null, audio,
        new ActivityLifecycle("build_tower", "test"), 3, null, hand.transform);
      AdvanceToBuilding(game);
      TowerBlock b = game.BlockAt(0);
      player.transform.position = b.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(b);
      for (int i = 0; i < 40 && b.State != TowerBlock.BlockState.Carried; i++) game.Tick(0.1f);
      Assert.AreEqual(TowerBlock.BlockState.Carried, b.State, "block carried");
      Vector3 acrossTheArena = BuildTowerBuilder.EntryLocal + new Vector3(2.4f, 1.3f, 0f);
      hand.transform.position = acrossTheArena;
      for (int i = 0; i < 30; i++) game.Tick(0.05f);
      Assert.Less(Vector3.Distance(b.transform.position, acrossTheArena), 0.25f,
        "the block rides the fist");
      Assert.Greater(Vector3.Distance(b.transform.position, player.transform.position), 2.0f,
        "a root-glued block would sit at the child's feet — impossible here");
      // Source pin: the installer resolves the hand bone and passes it through.
      string installer = File.ReadAllText(Path.Combine(Application.dataPath,
        "_Bootstrap", "GameInstaller.cs")).Replace("\r\n", "\n");
      Assert.IsTrue(installer.Contains("_buildArea.NotifyCompleted : null,\n          hand);"),
        "GameInstaller passes the resolved hand into BuildTowerGame.Build");
    } finally {
      Object.DestroyImmediate(hand);
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // P. Ladder timing: a finished round KEEPS its target while the child is
  // still inside (a spare block runs the correction lesson, never a silent
  // next rung); leaving the yard advances exactly one rung with a fresh
  // lifecycle, and an unfinished round never advances.
  [Test] public void P56P_LadderAdvancesOnLeave() {
    GameObject go = new GameObject("P56AreaLeave");
    try {
      BuildTowerArea area = go.AddComponent<BuildTowerArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      area.SetTargetForTests(3);
      Assert.IsTrue(area.TryEnterForTests(), "enter");
      CompleteLife(area.Lifecycle);
      Assert.AreEqual(3, area.Target, "a completed round keeps its target while inside");
      Assert.IsTrue(area.TryExitForTests(), "leave after completing");
      Assert.AreEqual(5, area.Target, "leaving advances exactly one rung");
      ActivityLifecycle next = area.Lifecycle;
      Assert.IsNotNull(next, "the next rung gets a fresh lifecycle");
      Assert.AreEqual(ActivityState.Unavailable, next.State, "the fresh life starts clean");
      Assert.IsFalse(area.TryExitForTests(), "double leave is refused");
      Assert.AreEqual(5, area.Target, "no double advance");
      Assert.IsTrue(area.TryEnterForTests(), "back in for the next rung");
      Assert.IsTrue(area.TryExitForTests(), "leave mid-lesson");
      Assert.AreEqual(5, area.Target, "an unfinished round never advances");
      Assert.AreEqual(ActivityState.Unavailable, area.Lifecycle.State, "still the same clean life");
    } finally { Object.DestroyImmediate(go); }
  }

  // Q. Gate interaction cue (brief §2): a presentation-only approach glow at
  // the build gate that reads near/far without touching the portal's own
  // walk-in trigger + cold-start debounce.
  [Test] public void P56Q_GateApproachCue() {
    GameObject root = new GameObject("P56MathWorldQ");
    try {
      MathWorldBuilder builder = root.AddComponent<MathWorldBuilder>();
      builder.BuildContent(root.transform);
      BuildYardGateHint hint = builder.BuildYardHint;
      Assert.IsNotNull(hint, "the build gate carries the approach cue");
      Assert.AreSame(builder.BuildTowerPortal, hint.Portal, "the cue follows the gate portal");
      Assert.IsNotNull(hint.Glow, "the glow disc exists");
      Assert.IsFalse(hint.Glow.gameObject.activeSelf, "hidden until the child approaches");
      Assert.IsTrue(IsIgnoredFromBuild(hint.Glow.gameObject), "the cue never bakes");
      Assert.AreEqual(1f, hint.NearForTests(hint.transform.position + new Vector3(2f, 0f, 0f)),
        0.001f, "inside the approach radius");
      Assert.AreEqual(0f, hint.NearForTests(hint.transform.position + new Vector3(12f, 0f, 0f)),
        0.001f, "far away stays silent");
    } finally { Object.DestroyImmediate(root); }
  }

  // R. Silent next-step cue (brief §30): ONE available block breathes while the
  // child still owes blocks; it clears while carrying and after completion.
  [Test] public void P56R_NextBlockHint() {
    GameObject arena;
    BuildTowerBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(BuildTowerBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      BuildTowerGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("build_tower", "test"), 3);
      AdvanceToBuilding(game);
      game.Tick(0.1f);
      TowerBlock hint = game.HintForTests;
      Assert.IsNotNull(hint, "one block carries the cue");
      Assert.IsTrue(hint.IsAvailable, "the cue is an available block");
      for (int i = 0; i < game.BlockCountTotal; i++) {
        TowerBlock b = game.BlockAt(i);
        if (b != hint) Assert.IsFalse(b.IsHint, "exactly one block carries the cue");
      }
      player.transform.position = hint.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(hint);
      for (int i = 0; i < 40 && hint.State != TowerBlock.BlockState.Carried; i++) game.Tick(0.1f);
      Assert.IsNull(game.HintForTests, "no cue while a block is in hand");
      Assert.IsFalse(hint.IsHint, "the carried block stops breathing");
      player.transform.position = PadWorld(arena) + new Vector3(0f, 0f, -1.0f);
      game.TryPlace();
      for (int i = 0; i < 40 && hint.State != TowerBlock.BlockState.Placed; i++) game.Tick(0.1f);
      game.Tick(0.1f);
      Assert.IsNotNull(game.HintForTests, "the next unplaced block takes the cue");
      Assert.AreNotSame(hint, game.HintForTests, "a placed block never hints again");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // S. Full rounds at target 1 (the smallest lesson) and target 7 (mid ladder):
  // the demo builds exactly N real blocks, the child rebuilds them, and the
  // completion + reward beats fire for every target the brief lists.
  [Test] public void P56S_FlowsAt1And7() {
    int[] targets = { 1, 7 };
    foreach (int target in targets) {
      GameObject arena;
      BuildTowerBuilder builder = BuildArena(out arena, target);
      GameObject player = BuildPlayer(BuildTowerBuilder.EntryLocal);
      try {
        FakeAudio audio = new FakeAudio();
        ActivityLifecycle life = new ActivityLifecycle("build_tower", "test");
        int completed = -1;
        BuildTowerGame game = BuildGame(builder, arena, player, audio, life, target,
          delegate (int n) { completed = n; });
        AdvanceToBuilding(game, 420f);
        Assert.AreEqual(BuildTowerGame.Phase.Building, game.Current,
          "target " + target + " reaches the child");
        Assert.AreEqual(target, game.DemoBlocksPlaced, "demo stacked exactly " + target);
        for (int i = 0; i < target; i++) PlaceOne(game, arena, player, i);
        for (int i = 0; i < 30 && game.Current != BuildTowerGame.Phase.Success; i++) game.Tick(0.1f);
        Assert.AreEqual(BuildTowerGame.Phase.Success, game.Current, "target " + target + " completes");
        Assert.AreEqual(target, game.TowerHeight, "tower height is " + target);
        Assert.AreEqual(target, completed, "the reward path reports " + target);
        Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle completed at " + target);
      } finally {
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(arena);
      }
    }
  }

  // T. Entry discipline (J4): the arrival spawn sits clear of the yard's exit
  // portal radius, so the child never instantly re-triggers the way home.
  [Test] public void P56T_EntryClearsExit() {
    GameObject arena;
    BuildTowerBuilder builder = BuildArena(out arena);
    try {
      Assert.IsNotNull(builder.ExitPortal, "exit portal staged");
      float d = Dist2D(builder.EntryPoint.position, builder.ExitPortal.transform.position);
      Assert.Greater(d, builder.ExitPortal.fireRadius + builder.ExitPortal.rearmMargin,
        "entry spawn clears the exit re-arm radius");
      Assert.Less(d, 12f, "entry is not across the arena");
    } finally { Object.DestroyImmediate(arena); }
  }
}
