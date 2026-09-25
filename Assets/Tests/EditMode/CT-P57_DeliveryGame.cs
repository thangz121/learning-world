// CT-P57: GAMEPLAY #5 — "GIAO HÀNG ĐÚNG SỐ" (deliver the apples).
// Pins the Math Hub delivery_village gate (walk-in portal + the shared
// IMicroWorldArea seam), the child-scale Delivery Village (order board +
// apple stall + lane arrows + receiving booth + delivered crate + exit cue),
// the deterministic item lifecycle (Available -> Picked -> Carried ->
// Delivered with the two-leg handover), the full activity flow (teacher order
// -> student demo -> handoff -> child delivery -> success), overshoot
// correction (guidance, never failure), the wrong-item guard, undershoot
// nudges, spam safety, re-entry adopt, the lazy DeliveryScene contract, the
// 1..9 ladder + CLI, the recorded speech safety at 9 (both voices), carry
// robustness and the exit-cue/reward picture.
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class CT_P57_DeliveryGame {
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

  static DeliveryBuilder BuildArena(out GameObject arena, int target = 4) {
    arena = new GameObject("P57DeliveryWorld");
    DeliveryBuilder builder = arena.AddComponent<DeliveryBuilder>();
    builder.BoardTarget = DeliveryBuilder.ClampTarget(target);
    builder.BuildContent(arena.transform);
    return builder;
  }

  static DeliveryGame BuildGame(DeliveryBuilder builder, GameObject arena,
      GameObject player, FakeAudio audio, ActivityLifecycle life, int target,
      System.Action<int> onCompleted = null) {
    DeliveryGame game = arena.AddComponent<DeliveryGame>();
    game.Build(builder, player.transform, null, audio, life, target, onCompleted);
    return game;
  }

  static GameObject BuildPlayer(Vector3 at) {
    GameObject player = new GameObject("P57Player");
    player.transform.position = at;
    return player;
  }

  static Vector3 ReceiverWorld(GameObject arena) {
    return arena.transform.TransformPoint(DeliveryBuilder.MiaStart);
  }

  static void AdvanceToDelivering(DeliveryGame game, float maxSeconds = 420f) {
    for (float t = 0f; t < maxSeconds && game.Current != DeliveryGame.Phase.Delivering; t += 0.1f)
      game.Tick(0.1f);
  }

  // Drive one full player delivery: stand at the apple, pick, wait for the
  // carry, stand at the booth, hand over, wait for the real handover.
  static void DeliverOne(DeliveryGame game, GameObject arena, GameObject player, int idx) {
    DeliveryItem b = game.ItemAt(idx);
    Assert.IsNotNull(b, "apple " + idx + " exists");
    player.transform.position = b.transform.position + new Vector3(0f, 0f, -0.5f);
    game.TryPick(b);
    for (int i = 0; i < 40 && b.State != DeliveryItem.ItemState.Carried; i++) game.Tick(0.1f);
    Assert.AreEqual(DeliveryItem.ItemState.Carried, b.State, "apple " + idx + " rides the hand");
    player.transform.position = ReceiverWorld(arena) + new Vector3(0f, 0f, -1.1f);
    game.TryDeliver();
    for (int i = 0; i < 40 && b.State != DeliveryItem.ItemState.Delivered; i++) game.Tick(0.1f);
    Assert.AreEqual(DeliveryItem.ItemState.Delivered, b.State, "apple " + idx + " handed over");
  }

  // A. Math Hub: the delivery_village gate owns a walk-in portal with its OWN
  // area id; the hub return clears the re-arm radius; every micro-world area
  // satisfies the shared IMicroWorldArea seam (portal dispatch).
  [Test] public void P57A_GatePortalAndSeam() {
    GameObject root = new GameObject("P57MathWorld");
    try {
      MathWorldBuilder builder = root.AddComponent<MathWorldBuilder>();
      builder.BuildContent(root.transform);
      MicroWorldGate gate = builder.FindMicroGate("delivery_village");
      Assert.IsNotNull(gate, "the delivery_village gate exists");
      Assert.AreEqual("delivery_village", gate.gateId, "the human-reviewed gate id intact");
      Assert.IsFalse(string.IsNullOrEmpty(gate.displayName), "the gate keeps its child-facing label");
      MicroWorldPortal portal = builder.DeliveryPortal;
      Assert.IsNotNull(portal, "the delivery gate has a walk-in portal");
      Assert.IsFalse(portal.ExitMode, "it is an ENTER portal");
      Assert.IsFalse(portal.PlayExit, "it is not a play-arena exit");
      Assert.AreEqual(DeliveryArea.AreaId, portal.areaId, "it targets the Delivery Village");
      Assert.AreEqual(1.8f, portal.fireRadius, "whole-arch coverage radius");
      Vector3 toHub = -new Vector3(gate.transform.position.x, 0f, gate.transform.position.z).normalized;
      Vector3 want = gate.transform.position + toHub * 0.7f;
      Assert.Less(Dist2D(portal.transform.position, want), 0.05f,
        "the portal covers the whole arch (0.7m hub-side of the gate centre)");
      Vector3 hubReturn = MathWorldBuilder.WorldOffset + MathWorldBuilder.DeliveryHubReturnLocal;
      Assert.Greater(Dist2D(hubReturn, portal.transform.position),
        portal.fireRadius + portal.rearmMargin,
        "exit landing clears the portal re-arm radius (no instant re-entry)");
      // The seam: all three micro-world areas satisfy IMicroWorldArea and the
      // portal dispatches to whichever one is bound.
      Assert.IsTrue(typeof(IMicroWorldArea).IsAssignableFrom(typeof(DeliveryArea)),
        "DeliveryArea implements the seam");
      Assert.IsTrue(typeof(IMicroWorldArea).IsAssignableFrom(typeof(BuildTowerArea)),
        "BuildTowerArea implements the seam");
      Assert.IsTrue(typeof(IMicroWorldArea).IsAssignableFrom(typeof(CountingGardenArea)),
        "CountingGardenArea implements the seam");
      GameObject areaGo = new GameObject("P57DeliveryAreaProbe");
      try {
        DeliveryArea area = areaGo.AddComponent<DeliveryArea>();
        area.Bind(null, null, null, null, null, Vector3.zero);
        portal.DeliveryArea = area;
        Assert.AreSame(area, portal.TargetArea(), "the portal dispatches to the bound area");
        Assert.IsTrue(area.TryEnterForTests(), "the area accepts control");
        Assert.IsTrue(area.TryExitForTests(), "and releases it");
      } finally { Object.DestroyImmediate(areaGo); }
    } finally { Object.DestroyImmediate(root); }
  }

  // B. Arena structure: order board (digit + apple icon) + stall + lane arrows
  // + receiving booth + delivered crate + exit cue + cameras, child scale.
  [Test] public void P57B_ArenaStructure() {
    GameObject arena;
    DeliveryBuilder builder = BuildArena(out arena);
    try {
      Assert.IsNotNull(FindDeep(arena.transform, "DVNumberDigit"), "order digit staged");
      Assert.IsNotNull(FindDeep(arena.transform, "DVBoardApple"), "order apple icon staged");
      for (int i = 0; i < DeliveryBuilder.ItemCount; i++)
        Assert.IsNotNull(FindDeep(arena.transform, "DVApple" + i), "apple " + i + " staged");
      Assert.AreEqual(10, builder.Apples.Count, "ten apples: the order plus a spare");
      Assert.IsNotNull(FindDeep(arena.transform, "DVStallCounter"), "stall counter staged");
      Assert.IsNotNull(FindDeep(arena.transform, "DVStallAwningA"), "stall awning staged");
      Assert.IsNotNull(FindDeep(arena.transform, "DVStallTrayA"), "apple tray staged");
      foreach (string n in new[] { "DVArrowA", "DVArrowB", "DVArrowC" })
        Assert.IsNotNull(FindDeep(arena.transform, n), "lane arrow " + n);
      Assert.IsNotNull(FindDeep(arena.transform, "DVBoothCounter"), "receiving booth staged");
      Assert.IsNotNull(FindDeep(arena.transform, "DVCrateBase"), "delivered crate staged");
      for (int i = 0; i < 4; i++)
        Assert.IsNotNull(FindDeep(arena.transform, "DVCrateWall" + i), "crate wall " + i);
      Assert.IsNotNull(builder.DeliveryAnchor, "delivery door anchor exposed");
      Assert.IsNotNull(builder.Crate, "crate exposed");
      Assert.IsNotNull(builder.ExitCue, "exit cue staged");
      Assert.IsFalse(builder.ExitCue.activeSelf, "exit cue hidden before completion");
      Assert.IsNotNull(builder.Result, "result board staged");
      Assert.IsFalse(builder.Result.activeSelf, "result hidden before success");
      foreach (string n in new[] { "DVCamA", "DVLookA", "DVCamB", "DVLookB", "DVCamC", "DVLookC" })
        Assert.IsNotNull(FindDeep(arena.transform, n), "camera marker " + n);
      Assert.IsNotNull(builder.Anchors, "anchor registry");
      Assert.IsNotNull(builder.EntryPoint, "entry marker");
      Assert.IsNotNull(builder.ExitPortal, "exit portal");
      Assert.IsTrue(builder.ExitPortal.ExitMode, "exit returns to the hub");
      Assert.AreEqual(DeliveryArea.AreaId, builder.ExitPortal.areaId, "exit targets the area");
      // Child scale: entry -> stall -> receiver is a short loop, never a hike.
      float entryStall = Dist2D(DeliveryBuilder.EntryLocal, DeliveryBuilder.StallStand);
      float stallReceiver = Dist2D(DeliveryBuilder.StallCenter, DeliveryBuilder.MiaStart);
      Assert.Less(entryStall, 7f, "entry to stall is a short walk");
      Assert.Less(stallReceiver, 7f, "stall to receiver is a short walk");
      // The apples are pickable-sized and bake-ignored (they MOVE).
      GameObject apple0 = builder.Apples[0];
      Assert.Greater(apple0.transform.localPosition.y, 0.3f, "apples rest on the trays");
      Assert.Less(apple0.transform.localPosition.y, 0.7f, "apples sit at child reach");
      Assert.IsTrue(IsIgnoredFromBuild(apple0), "a movable apple never bakes");
      // The crate slots: 9 distinct positions on the counter, inside its top.
      for (int i = 0; i < DeliveryBuilder.MaxTarget; i++) {
        Vector3 slot = DeliveryBuilder.CrateSlot(i, DeliveryBuilder.BoothCounter);
        Assert.AreEqual(DeliveryBuilder.CounterTopY + 0.16f, slot.y, 0.001f, "slot " + i + " height");
        Assert.Less(Mathf.Abs(slot.x - DeliveryBuilder.BoothCounter.x), 0.4f, "slot x on the counter");
        Assert.Less(Mathf.Abs(slot.z - DeliveryBuilder.BoothCounter.z), 0.4f, "slot z on the counter");
        for (int j = 0; j < i; j++) {
          Vector3 other = DeliveryBuilder.CrateSlot(j, DeliveryBuilder.BoothCounter);
          Assert.Greater(Vector3.Distance(slot, other), 0.2f, "slots never overlap (" + i + "," + j + ")");
        }
      }
    } finally { Object.DestroyImmediate(arena); }
  }

  static int CountDeep(Transform t, string name) {
    int n = t != null && t.name == name ? 1 : 0;
    if (t != null) {
      for (int i = 0; i < t.childCount; i++) n += CountDeep(t.GetChild(i), name);
    }
    return n;
  }

  // C. Target validity 1..9: clamp + staged digit + game target.
  [Test] public void P57C_TargetValidity() {
    Assert.AreEqual(1, DeliveryBuilder.ClampTarget(0), "0 clamps to 1");
    Assert.AreEqual(9, DeliveryBuilder.ClampTarget(10), "10 clamps to 9");
    Assert.AreEqual(9, DeliveryBuilder.MaxTarget, "maximum is 9");
    for (int t = 1; t <= 9; t++) {
      GameObject arena;
      DeliveryBuilder builder = BuildArena(out arena, t);
      GameObject player = BuildPlayer(DeliveryBuilder.EntryLocal);
      try {
        Assert.IsNotNull(FindDeep(arena.transform, "DVNumberDigit"), "digit staged at " + t);
        FakeAudio audio = new FakeAudio();
        DeliveryGame game = BuildGame(builder, arena, player, audio,
          new ActivityLifecycle("deliver_apples", "test"), t);
        Assert.AreEqual(t, game.Target, "game delivers target " + t);
        Assert.GreaterOrEqual(game.ItemCountTotal, t, "the stall always has enough apples");
        Object.DestroyImmediate(game);
      } finally {
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(arena);
      }
    }
  }

  // D. Full flow at 4: intro -> demo (4 real handovers) -> handoff (the demo
  // crate tidies home) -> child delivers 4 -> success (lifecycle + result +
  // exit cue + landmark notification + recap).
  [Test] public void P57D_FullFlowAt4() {
    GameObject arena;
    DeliveryBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(DeliveryBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("deliver_apples", "test");
      int completed = -1;
      DeliveryGame game = BuildGame(builder, arena, player, audio, life, 4,
        delegate (int n) { completed = n; });
      AdvanceToDelivering(game);
      Assert.AreEqual(DeliveryGame.Phase.Delivering, game.Current, "the child holds control");
      Assert.AreEqual(4, game.DemoApplesDelivered, "the demo delivered exactly 4");
      Assert.AreEqual(ActivityState.Active, life.State, "lifecycle active at handoff");
      // The demo crate tidied home: every apple is pickable again.
      for (int i = 0; i < game.ItemCountTotal; i++)
        Assert.AreEqual(DeliveryItem.ItemState.Available, game.ItemAt(i).State,
          "apple " + i + " reset for the round");
      Assert.AreEqual(0, game.CrateCount, "the crate starts empty for the child");
      DeliverOne(game, arena, player, 0);
      Assert.AreEqual(1, game.Count, "one delivered");
      Assert.AreEqual(0, game.DeliveredAt(0).CrateIndex, "the first apple takes crate slot 0");
      DeliverOne(game, arena, player, 1);
      DeliverOne(game, arena, player, 2);
      DeliverOne(game, arena, player, 3);
      for (int i = 0; i < 20 && game.Current != DeliveryGame.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(DeliveryGame.Phase.Success, game.Current, "four apples complete the order");
      Assert.AreEqual(4, game.Count, "count is exactly the order");
      Assert.AreEqual(4, game.CrateCount, "the crate IS the count");
      Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle completed");
      Assert.IsTrue(game.ResultShown, "result board shown");
      Assert.IsTrue(game.ExitCueShown, "exit cue lit after completion");
      Assert.AreEqual(4, completed, "the area is notified with the completed order");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Four apples! Well done!", "Bốn quả táo! Giỏi!")),
        "the teacher confirms the order");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Thank you!", "Cảm ơn con!")),
        "the receiver thanks the child");
      Assert.IsTrue(audio.Sfx.Contains("give"), "each handover chirps");
      Assert.IsTrue(audio.Sfx.Contains("success"), "success chime plays");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // E. Undershoot: 2 of 5 delivered and settled earns a gentle "how many more"
  // — never a fail, never a completion.
  [Test] public void P57E_UndershootNudge() {
    GameObject arena;
    DeliveryBuilder builder = BuildArena(out arena, 5);
    GameObject player = BuildPlayer(DeliveryBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      DeliveryGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("deliver_apples", "test"), 5);
      AdvanceToDelivering(game);
      DeliverOne(game, arena, player, 0);
      DeliverOne(game, arena, player, 1);
      Assert.AreEqual(DeliveryGame.Phase.Delivering, game.Current, "short of the order: still delivering");
      player.transform.position = ReceiverWorld(arena) + new Vector3(0f, 0f, -1.1f);
      for (int i = 0; i < 140; i++) game.Tick(0.1f); // settle: the nudge fires
      Assert.GreaterOrEqual(game.UndershootNudges, 1, "a gentle nudge names the remainder");
      Assert.AreEqual(DeliveryGame.Phase.Delivering, game.Current, "no completion while short");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("Three more apples!", "Còn ba quả nữa nhé!")), "the nudge counts the rest");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // F. Overshoot at 4: the 5th apple is NOT accepted as success — gentle
  // correction ("Đủ bốn quả rồi."), the extra goes home, the crate stays 4.
  [Test] public void P57F_OvershootAt4() {
    GameObject arena;
    DeliveryBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(DeliveryBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("deliver_apples", "test");
      DeliveryGame game = BuildGame(builder, arena, player, audio, life, 4);
      AdvanceToDelivering(game);
      DeliverOne(game, arena, player, 0);
      DeliverOne(game, arena, player, 1);
      DeliverOne(game, arena, player, 2);
      DeliverOne(game, arena, player, 3);
      for (int i = 0; i < 20 && game.Current != DeliveryGame.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(DeliveryGame.Phase.Success, game.Current, "setup: success at 4");
      DeliveryItem extra = game.ItemAt(4);
      player.transform.position = extra.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(extra);
      for (int i = 0; i < 40 && extra.State != DeliveryItem.ItemState.Carried; i++) game.Tick(0.1f);
      player.transform.position = ReceiverWorld(arena) + new Vector3(0f, 0f, -1.1f);
      game.TryDeliver();
      Assert.AreEqual(DeliveryGame.Phase.Correct, game.Current, "the 5th handover corrects, never succeeds");
      Assert.AreEqual(1, game.Overshoots, "one overshoot recorded");
      for (int i = 0; i < 40 && extra.State != DeliveryItem.ItemState.Delivered; i++) game.Tick(0.1f);
      Assert.AreEqual(DeliveryItem.ItemState.Delivered, extra.State,
        "the extra really reaches her hands first");
      for (int i = 0; i < 220 && game.Current != DeliveryGame.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(DeliveryGame.Phase.Success, game.Current, "the correction lands back on success");
      Assert.AreEqual(4, game.Count, "count stays exactly the order");
      Assert.AreEqual(4, game.CrateCount, "the crate is exactly 4 apples");
      for (int i = 0; i < 40 && extra.State != DeliveryItem.ItemState.Available; i++) game.Tick(0.1f);
      Assert.AreEqual(DeliveryItem.ItemState.Available, extra.State, "the extra hops home");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Four is enough.", "Đủ bốn quả rồi.")),
        "the teacher names the limit gently");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("The board says four.", "Bảng ghi số bốn.")),
        "the teacher points back at the board");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // G. Wrong item: the receiver refuses anything that is not an apple — gently,
  // without counting (brief §15C).
  [Test] public void P57G_WrongItemRefused() {
    GameObject arena;
    DeliveryBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(DeliveryBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      DeliveryGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("deliver_apples", "test"), 4);
      AdvanceToDelivering(game);
      DeliveryItem item = game.ItemAt(0);
      player.transform.position = item.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(item);
      for (int i = 0; i < 40 && item.State != DeliveryItem.ItemState.Carried; i++) game.Tick(0.1f);
      Assert.AreEqual(DeliveryItem.ItemState.Carried, item.State, "carried");
      // Synthesize a wrong kind (pioneer content is apples only — the guard is
      // the receiver's accept contract).
      item.Kind = DeliveryItem.ItemKind.Other;
      player.transform.position = ReceiverWorld(arena) + new Vector3(0f, 0f, -1.1f);
      game.TryDeliver();
      Assert.AreEqual(1, game.WrongItemRefusals, "the receiver refuses it");
      Assert.AreEqual(0, game.Count, "a wrong item never counts");
      Assert.AreEqual(item, game.Carried, "the child keeps it");
      Assert.AreEqual(DeliveryItem.ItemState.Carried, item.State, "state untouched");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Not an apple!", "Không phải táo!")),
        "the refusal is gentle and spoken");
      // Back to an apple: the very same item delivers fine (no sticky refusal).
      item.Kind = DeliveryItem.ItemKind.Apple;
      game.TryDeliver();
      for (int i = 0; i < 40 && item.State != DeliveryItem.ItemState.Delivered; i++) game.Tick(0.1f);
      Assert.AreEqual(DeliveryItem.ItemState.Delivered, item.State, "the guard is per-kind, not sticky");
      Assert.AreEqual(1, game.Count, "counted exactly once");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // H. Item lifecycle + crate slot determinism: Available -> Picked -> Carried
  // -> Delivered, parked exactly on its slot, never re-pickable.
  [Test] public void P57H_ItemLifecycleAndSlots() {
    GameObject arena;
    DeliveryBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(DeliveryBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      DeliveryGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("deliver_apples", "test"), 4);
      AdvanceToDelivering(game);
      DeliveryItem b = game.ItemAt(6);
      Assert.AreEqual(DeliveryItem.ItemState.Available, b.State, "starts available");
      player.transform.position = b.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(b);
      Assert.AreEqual(DeliveryItem.ItemState.Picked, b.State, "picked while the hand comes down");
      for (int i = 0; i < 30 && b.State != DeliveryItem.ItemState.Carried; i++) game.Tick(0.1f);
      Assert.AreEqual(DeliveryItem.ItemState.Carried, b.State, "then carried");
      int slot = game.Count;
      player.transform.position = ReceiverWorld(arena) + new Vector3(0f, 0f, -1.1f);
      game.TryDeliver();
      for (int i = 0; i < 30 && b.State != DeliveryItem.ItemState.Delivered; i++) game.Tick(0.1f);
      Assert.AreEqual(DeliveryItem.ItemState.Delivered, b.State, "then delivered");
      Assert.AreEqual(slot, b.CrateIndex, "the crate index is the real delivered order");
      for (int i = 0; i < 40 && b.IsFlying; i++) game.Tick(0.1f);
      Vector3 want = DeliveryBuilder.CrateSlot(slot, DeliveryBuilder.BoothCounter);
      Assert.Less(Vector3.Distance(b.transform.localPosition, want), 0.001f,
        "the apple settles exactly on its crate slot");
      // A delivered apple never picks again.
      game.TryPick(b);
      Assert.IsNull(game.Carried, "delivered apples stay delivered");
      // The correction trip home restores availability (the spare apple).
      DeliveryItem e = game.ItemAt(7);
      player.transform.position = e.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(e);
      for (int i = 0; i < 30 && e.State != DeliveryItem.ItemState.Carried; i++) game.Tick(0.1f);
      e.BeginReturnHome();
      for (int i = 0; i < 30 && e.State != DeliveryItem.ItemState.Available; i++) game.Tick(0.1f);
      Assert.AreEqual(DeliveryItem.ItemState.Available, e.State, "returned apples pick again");
      Assert.AreEqual(-1, e.CrateIndex, "a returned apple forgets its crate index");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // I. Re-entry adopt: a fresh instance on a Completed lifecycle shows the
  // finished picture (crate full, result up, exit cue lit, actors observing).
  [Test] public void P57I_ReentryAdopt() {
    GameObject arena;
    DeliveryBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(DeliveryBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("deliver_apples", "test");
      DeliveryGame first = BuildGame(builder, arena, player, audio, life, 4);
      AdvanceToDelivering(first);
      DeliverOne(first, arena, player, 0);
      DeliverOne(first, arena, player, 1);
      DeliverOne(first, arena, player, 2);
      DeliverOne(first, arena, player, 3);
      for (int i = 0; i < 20 && first.Current != DeliveryGame.Phase.Success; i++) first.Tick(0.1f);
      Assert.AreEqual(ActivityState.Completed, life.State, "setup: lifecycle completed");
      Object.DestroyImmediate(first);
      DeliveryGame second = BuildGame(builder, arena, player, new FakeAudio(), life, 4);
      Assert.AreEqual(DeliveryGame.Phase.Success, second.Current, "adopts success, never replays");
      Assert.AreEqual(4, second.Count, "the adopted crate keeps its count");
      Assert.IsTrue(second.ResultShown, "the finished picture shows the result");
      Assert.IsTrue(second.ExitCueShown, "the way home stays lit");
      for (int i = 0; i < 4; i++) {
        DeliveryItem b = second.ItemAt(i);
        Assert.AreEqual(DeliveryItem.ItemState.Delivered, b.State, "adopted apple " + i + " stays delivered");
        Assert.AreEqual(i, b.CrateIndex, "adopted crate order " + i);
      }
      for (int i = 4; i < second.ItemCountTotal; i++)
        Assert.AreEqual(DeliveryItem.ItemState.Removed, second.ItemAt(i).State,
          "unused apples are scenery on re-entry");
      for (int i = 0; i < 60; i++) second.Tick(0.1f);
      Assert.AreEqual(DeliveryGame.Phase.Success, second.Current, "adopt never restarts the lesson");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // J. Lazy contract: one micro scene at a time (the shared slot refuses a
  // second load), the scene ships in Build Settings, never at boot.
  [Test] public void P57J_LazySceneContract() {
    Assert.AreEqual("DeliveryScene", DeliveryBuilder.SceneName, "scene name pinned");
    Assert.Greater(DeliveryBuilder.WorldOffset.magnitude, 200f, "separate island (no overlap)");
    var t = new WorldTransition(SubjectIds.Main);
    var ops = new FakeOps();
    var math = new SubjectId("math");
    Assert.IsTrue(t.EnterAsync(ops, math, "MathScene").GetAwaiter().GetResult(), "subject loads");
    Assert.IsFalse(ops.Loaded.Contains(DeliveryBuilder.SceneName),
      "the delivery village must NOT be loaded at subject entry (lazy)");
    Assert.IsTrue(t.EnterMicroAsync(ops, CountingGardenBuilder.SceneName).GetAwaiter().GetResult(),
      "the garden loads first");
    Assert.IsFalse(t.EnterMicroAsync(ops, DeliveryBuilder.SceneName).GetAwaiter().GetResult(),
      "the delivery village cannot stack onto the garden (one micro slot)");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "garden unloads");
    Assert.IsTrue(t.EnterMicroAsync(ops, DeliveryBuilder.SceneName).GetAwaiter().GetResult(),
      "the delivery village loads into the freed slot");
    Assert.IsTrue(ops.Loaded.Contains(DeliveryBuilder.SceneName), "it is now the live micro scene");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "and unloads cleanly");
    string scenePath = Path.Combine(Application.dataPath,
      "A_World", "DeliveryVillage", "DeliveryScene.unity");
    Assert.IsTrue(File.Exists(scenePath), "DeliveryScene.unity ships in the project");
    string yaml = File.ReadAllText(scenePath);
    Assert.IsTrue(yaml.Contains("DeliveryWorld"), "scene root is DeliveryWorld");
    string buildSettings = File.ReadAllText(Path.Combine(
      Directory.GetParent(Application.dataPath).FullName, "ProjectSettings", "EditorBuildSettings.asset"));
    Assert.IsTrue(buildSettings.Contains("DeliveryScene.unity"), "the scene ships in the build");
  }

  // K. Ladder + CLI: 4 -> 5 -> 7 -> 9 -> 1 -> 3 -> 4 (brief test targets
  // 1,3,5,7,9 all reachable), diagnostic flag inert, area seams.
  [Test] public void P57K_LadderAndCli() {
    int[] expect = { 5, 7, 9, 1, 3, 4 };
    int t = 4;
    foreach (int n in expect) {
      t = DeliveryArea.NextTarget(t);
      Assert.AreEqual(n, t, "ladder rung");
    }
    Assert.AreEqual(4, DeliveryArea.NextTarget(6), "off-ladder rejoins at 4");
    Assert.AreEqual(7, DeliveryArea.ParseTargetArg(
      new[] { "exe", "-deliver-target", "7" }, 4), "flag pins a target");
    Assert.AreEqual(4, DeliveryArea.ParseTargetArg(
      new[] { "exe", "-deliver-target", "99" }, 4), "out-of-range flag stays inert");
    Assert.AreEqual(4, DeliveryArea.ParseTargetArg(new[] { "exe" }, 4), "no flag keeps the ladder");
    GameObject go = new GameObject("P57AreaSeams");
    try {
      DeliveryArea area = go.AddComponent<DeliveryArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      Assert.IsFalse(area.IsInside, "starts outside");
      Assert.IsTrue(area.TryEnterForTests(), "enter");
      Assert.IsFalse(area.TryEnterForTests(), "double enter blocked");
      Assert.IsTrue(area.TryExitForTests(), "exit");
      Assert.IsFalse(area.TryExitForTests(), "double exit blocked");
      area.SetTargetForTests(4);
      Assert.AreEqual(4, area.Target, "ladder starts at the reference 4");
      area.TickProgressionForTests();
      Assert.AreEqual(4, area.Target, "mid-lesson re-entry keeps the target");
      Assert.AreEqual(4, DeliveryArea.DefaultTarget, "reference order pinned");
    } finally { Object.DestroyImmediate(go); }
  }

  // L. Speech safety at 9: EVERY line a full round can produce (intro, demo
  // counts, handoff, player counts, receiver thanks, recap, nudges, correction)
  // passes the NPC cap in the active language — recorded from a REAL run.
  [Test] public void P57L_LinesSafetyAt9() {
    GameObject arena;
    DeliveryBuilder builder = BuildArena(out arena, 9);
    GameObject player = BuildPlayer(DeliveryBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      DeliveryGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("deliver_apples", "test"), 9);
      AdvanceToDelivering(game, 600f);
      Assert.AreEqual(DeliveryGame.Phase.Delivering, game.Current, "target 9 reaches the child");
      Assert.AreEqual(9, game.DemoApplesDelivered, "the demo delivered all nine");
      for (int i = 0; i < 9; i++) DeliverOne(game, arena, player, i);
      for (int i = 0; i < 30 && game.Current != DeliveryGame.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(DeliveryGame.Phase.Success, game.Current, "target 9 completes");
      Assert.AreEqual(9, game.CrateCount, "a real nine-apple crate stands");
      for (int i = 0; i < 260; i++) game.Tick(0.1f); // recap drains
      Assert.Greater(audio.Lines.Count, 30, "a full round speaks plenty");
      foreach (string line in audio.Lines) {
        bool ok = SafetyFilter.ValidateLine(line, false, out string why);
        Assert.IsTrue(ok, "line passes the NPC cap: '" + line + "' (" + why + ")");
      }
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Nine apples.", "Chín quả táo.")),
        "nine counted");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("Mia needs nine apples!", "Mia cần chín quả táo!")), "the order named");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // M. Carry robustness (brief §15 D/E/F/G): a picked apple never drops
  // mid-walk, never vanishes on a long trek, and delivers fine afterwards.
  [Test] public void P57M_CarryRobustness() {
    GameObject arena;
    DeliveryBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(DeliveryBuilder.EntryLocal);
    try {
      FakeAudio audio = new FakeAudio();
      DeliveryGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("deliver_apples", "test"), 4);
      AdvanceToDelivering(game);
      DeliveryItem b = game.ItemAt(2);
      player.transform.position = b.transform.position + new Vector3(0f, 0f, -0.5f);
      game.TryPick(b);
      for (int i = 0; i < 20 && b.State != DeliveryItem.ItemState.Carried; i++) game.Tick(0.1f);
      // A long trek back to the stall, then to the entry, then back: still
      // carried, never duplicated, never counted early.
      player.transform.position = DeliveryBuilder.StallStand;
      for (int i = 0; i < 80; i++) game.Tick(0.1f);
      Assert.AreEqual(DeliveryItem.ItemState.Carried, b.State, "the apple survives the walk back");
      player.transform.position = DeliveryBuilder.EntryLocal;
      for (int i = 0; i < 80; i++) game.Tick(0.1f);
      Assert.AreEqual(DeliveryItem.ItemState.Carried, b.State, "the apple survives the far trek");
      Assert.AreEqual(b, game.Carried, "the hand never loses it");
      Assert.AreEqual(0, game.Count, "a carried apple is never counted early");
      player.transform.position = ReceiverWorld(arena) + new Vector3(0f, 0f, -1.1f);
      game.TryDeliver();
      for (int i = 0; i < 40 && b.State != DeliveryItem.ItemState.Delivered; i++) game.Tick(0.1f);
      Assert.AreEqual(DeliveryItem.ItemState.Delivered, b.State, "it delivers fine afterwards");
      Assert.AreEqual(1, game.Count, "counted exactly once");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // N. Exit cue + reward picture (brief §21/§22): hidden before completion,
  // lit above the exit after; staying lit on adopt.
  [Test] public void P57N_ExitCueAndReward() {
    GameObject arena;
    DeliveryBuilder builder = BuildArena(out arena);
    GameObject player = BuildPlayer(DeliveryBuilder.EntryLocal);
    try {
      Assert.IsNotNull(builder.ExitCue, "the exit cue exists");
      Assert.IsFalse(builder.ExitCue.activeSelf, "hidden before completion");
      Vector3 cue = builder.ExitCue.transform.localPosition;
      Assert.Greater(cue.y, 2.0f, "the cue floats above the exit arch");
      Assert.Less(Mathf.Abs(cue.z - DeliveryBuilder.ExitLocal.z), 0.6f, "the cue rides the exit");
      FakeAudio audio = new FakeAudio();
      DeliveryGame game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("deliver_apples", "test"), 4);
      AdvanceToDelivering(game);
      for (int i = 0; i < 4; i++) DeliverOne(game, arena, player, i);
      for (int i = 0; i < 20 && game.Current != DeliveryGame.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(DeliveryGame.Phase.Success, game.Current, "the order completes");
      Assert.IsTrue(game.ResultShown, "the result board shows the digit + tick");
      Assert.IsTrue(game.ExitCueShown, "the exit cue lights up");
      // The field stays open after success (drain the recap + the way-home line).
      for (int i = 0; i < 260; i++) game.Tick(0.1f);
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Time to go home!", "Mình ra cổng nhé!")),
        "the teacher points at the way home (no auto-return)");
      Assert.AreEqual(DeliveryGame.Phase.Success, game.Current, "success is stable, never auto-exits");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }
}
