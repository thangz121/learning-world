// CT-S20: ordering_station (Ga Thứ Tự). IMPLEMENTED, not HUMAN_ACCEPTED.
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CT_S20_OrderingStation {
  sealed class FakeAudio : IAudioDirector {
    public readonly List<string> Lines = new List<string>();
    public Task PlayVocabularyAsync(WordId wordId, VocabularyAudioMode mode) {
      return Task.CompletedTask;
    }
    public Task SpeakAsync(DialogueRequest request) {
      Lines.Add(request.Text);
      return Task.CompletedTask;
    }
    public void PlaySfx(SfxId id) { }
    public void PlayMusic(MusicId id) { }
    public void SetMusicEnabled(bool on) { }
    public void SetAudioFocus(AudioFocusMode mode) { }
  }

  static OrderingStationBuilder BuildArena(out GameObject arena) {
    arena = new GameObject("S20StationWorld");
    OrderingStationBuilder builder = arena.AddComponent<OrderingStationBuilder>();
    builder.BuildContent(arena.transform);
    return builder;
  }

  static OrderingStation BuildGame(OrderingStationBuilder builder, GameObject arena,
      GameObject player, FakeAudio audio, ActivityLifecycle life) {
    OrderingStation game = arena.GetComponent<OrderingStation>();
    if (game == null) game = arena.AddComponent<OrderingStation>();
    game.Build(builder, player != null ? player.transform : null, null, audio, life);
    return game;
  }

  static GameObject PlayerAt(Vector3 local) {
    GameObject p = new GameObject("S20Player");
    p.transform.position = local;
    return p;
  }

  static void Drain(OrderingStation game, int n) {
    for (int i = 0; i < n; i++) game.Tick(0.1f);
  }

  static List<OrderingPiece> IdlePieces(OrderingStationBuilder b) {
    List<OrderingPiece> list = new List<OrderingPiece>();
    for (int i = 0; i < b.Pieces.Count; i++) {
      OrderingPiece p = b.Pieces[i];
      if (p != null && !p.Locked && p.State == OrderingPiece.PieceState.Idle) list.Add(p);
    }
    return list;
  }

  // Fill every empty slot with its correct rank (whole-sequence thinking).
  static void FillTrack(OrderingStation game, OrderingStationBuilder b) {
    List<float> all = new List<float>();
    for (int i = 0; i < b.Pieces.Count; i++)
      if (b.Pieces[i] != null) all.Add(b.Pieces[i].Rank);
    all.Sort();
    if (game.Descending) all.Reverse();
    int target = 0;
    for (int s = 0; s < b.Slots.Count; s++)
      if (!b.Slots[s].Filled) target++;
    Assert.Greater(target, 0, "track has empty slots");
    int startRound = game.RoundIndex;
    int startLevel = game.Level;
    for (int s = 0; s < b.Slots.Count; s++) {
      // A placement can complete the round and rebuild the track: stop there.
      if (game.Completed || game.RoundIndex != startRound || game.Level != startLevel) break;
      if (b.Slots[s].Filled) continue;
      float want = all[s];
      OrderingPiece found = null;
      foreach (OrderingPiece p in IdlePieces(b)) {
        if (Mathf.Abs(p.Rank - want) < 0.01f) { found = p; break; }
      }
      Assert.IsNotNull(found, "piece of rank " + want);
      game.TrySelect(found);
      game.TryPlace(b.Slots[s]);
    }
    Assert.IsTrue(game.Completed || game.RoundIndex != startRound || game.Level != startLevel,
      "round advances");
  }

  // Fill every empty slot so the full track is WRONG: remaining idle ranks
  // go onto the empty slots in descending slot order (an ascending track can
  // never end up ordered that way). Never completes the round.
  static void FillWrong(OrderingStation game, OrderingStationBuilder b) {
    List<OrderingSlot> empty = new List<OrderingSlot>();
    for (int s = 0; s < b.Slots.Count; s++)
      if (!b.Slots[s].Filled) empty.Add(b.Slots[s]);
    List<OrderingPiece> idle = IdlePieces(b);
    Assert.AreEqual(empty.Count, idle.Count, "one piece per empty slot");
    idle.Sort((a, c) => a.Rank.CompareTo(c.Rank));
    for (int i = 0; i < empty.Count; i++) {
      OrderingPiece p = idle[empty.Count - 1 - i];
      game.TrySelect(p);
      game.TryPlace(empty[i]);
    }
  }

  [Test] public void S20A_Registration() {
    GameEntry g = LearningMap.Game("ordering_station");
    Assert.IsNotNull(g, "registered");
    Assert.AreEqual("math_order", g.SkillId, "correct skill");
    Assert.AreEqual("math", LearningMap.SubjectOfSkill(g.SkillId).Id, "correct subject");
    Assert.AreEqual("OrderingStationPlayScene", g.SceneName, "correct scene");
    Assert.AreEqual(GameStatus.Implemented, g.Status, "IMPLEMENTED");
    Assert.IsFalse(g.HumanAccepted, "not HUMAN_ACCEPTED");
    Assert.IsFalse(LearningMap.IsPlayable("ordering_station"), "acceptance firewall holds");
    Assert.IsTrue(LearningMap.CanLaunch("ordering_station"), "may enter for play");
  }

  [Test] public void S20B_OrderingModel() {
    Assert.AreEqual(1, OrderLogic.SortIndices(new float[] { 3f, 1f, 2f }, false)[0], "ascending first");
    int[] desc = OrderLogic.SortIndices(new float[] { 1f, 3f, 2f }, true);
    Assert.AreEqual(1, desc[0], "descending first");
    Assert.IsTrue(OrderLogic.IsOrdered(new float[] { 1f, 2f, 3f }, false), "ascending ordered");
    Assert.IsFalse(OrderLogic.IsOrdered(new float[] { 1f, 3f, 2f }, false), "broken order");
    Assert.IsTrue(OrderLogic.IsOrdered(new float[] { 3f, 2f, 1f }, true), "descending ordered");
    Assert.AreEqual(1, OrderLogic.InsertPosition(new float[] { 1f, 3f }, 2f, false), "missing slot");
    Assert.AreEqual(0, OrderLogic.FirstSlot(5), "first");
    Assert.AreEqual(4, OrderLogic.LastSlot(5), "last");
    Assert.AreEqual(2, OrderLogic.MiddleSlot(5), "middle");
  }

  [Test] public void S20C_YardDoorAndReturn() {
    GameObject r = new GameObject("S20Yard");
    try {
      SelectionYardBuilder b = r.AddComponent<SelectionYardBuilder>();
      b.Level = "game";
      b.SkillId = "math_order";
      b.BuildContent(r.transform);
      Assert.AreEqual(1, b.GatePortals.Count, "one station door");
      Assert.AreEqual("ordering_station", b.GateTargetIds[0]);
      Assert.IsNotNull(FindDeep(r.transform, "SYPreview_ordering_station"), "wordless preview");
    } finally { Object.DestroyImmediate(r); }
    GameObject go = new GameObject("S20Area");
    try {
      SelectionYardArea area = go.AddComponent<SelectionYardArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      area.SetCurrentForTests(SelectionYardArea.YardLevel.Game, "", "math_order");
      area.PlayGame("ordering_station");
      Assert.AreEqual(1, area.PlayRequests, "order yard launches the station");
      Assert.AreEqual("skill:math", area.BackTargetForTests(), "return is the skill yard");
      area.PlayGame("rabbit_feeding");
      Assert.AreEqual(1, area.PlayRequests, "counting game refused here");
    } finally { Object.DestroyImmediate(go); }
  }

  [Test] public void S20D_SizeAscAndCorrection() {
    GameObject arena;
    OrderingStationBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(OrderingStationBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      OrderingStation game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("ordering_station", "test"));
      Drain(game, 5);
      Assert.AreEqual(OrderingStation.Phase.Arrange, game.Current, "starts at play");
      Assert.AreEqual(3, game.Level, "LV3 first");
      Assert.AreEqual(OrderDim.Size, game.Dim, "size dimension");
      Assert.IsFalse(game.Descending, "ascending first");
      Assert.AreEqual(3, builder.Slots.Count, "three track slots");
      // A single piece never wins: only the WHOLE track validates.
      List<OrderingPiece> idle = IdlePieces(builder);
      game.TrySelect(idle[0]);
      game.TryPlace(builder.Slots[0]);
      Assert.AreEqual(OrderingStation.Phase.Arrange, game.Current, "one piece is not a win");
      // Fill the track backwards: full but wrong → gentle reject, keep layout.
      FillWrong(game, builder);
      Assert.AreEqual(1, game.WrongCount, "incorrect sequence rejected");
      Assert.AreEqual(3, game.Level, "still the same round");
      Assert.IsFalse(game.LastCorrect, "no false win");
      // Correct it by repositioning through the game: pick a misplaced
      // piece back up and put it where it belongs (no reset needed).
      float[] asc = { 1f, 2f, 3f };
      for (int n = 0; n < 6; n++) {
        if (game.RoundIndex != 0 || game.Level != 3) break; // a fix already won it
        int bad = -1;
        for (int s = 0; s < builder.Slots.Count; s++) {
          OrderingPiece occ = builder.Slots[s].Occupant;
          if (occ != null && Mathf.Abs(occ.Rank - asc[s]) > 0.01f) { bad = s; break; }
        }
        if (bad < 0) break;
        OrderingPiece badOcc = builder.Slots[bad].Occupant;
        game.TrySelect(badOcc);
        Assert.AreSame(badOcc, game.Carried, "placed pieces can be picked back up");
        int home = -1;
        for (int s = 0; s < builder.Slots.Count; s++)
          if (Mathf.Abs(asc[s] - badOcc.Rank) < 0.01f) { home = s; break; }
        game.TryPlace(builder.Slots[home]);
      }
      FillTrack(game, builder);
      Assert.IsTrue(game.LastCorrect, "correction completes a win");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S20E_DirectionHeightInsertTap() {
    GameObject arena;
    OrderingStationBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(OrderingStationBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      OrderingStation game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("ordering_station", "test"));
      // LV4 descending length.
      game.SetLevelForTests(4, 1);
      Assert.AreEqual(OrderDim.Length, game.Dim, "length dimension");
      Assert.IsTrue(game.Descending, "reversed direction");
      Assert.AreEqual(4, builder.Slots.Count, "four slots");
      FillTrack(game, builder);
      // LV5 height ascending.
      game.SetLevelForTests(5, 0);
      Assert.AreEqual(OrderDim.Height, game.Dim, "height dimension");
      Assert.IsFalse(game.Descending, "back to ascending");
      FillTrack(game, builder);
      // LV6 insert: one gap, locked neighbours.
      game.SetLevelForTests(6, 0);
      Assert.AreEqual(OrderingStation.TaskKind.Insert, game.Task, "insert task");
      int locked = 0;
      foreach (OrderingPiece p in builder.Pieces) if (p.Locked) locked++;
      Assert.AreEqual(3, locked, "three locked neighbours");
      Assert.AreEqual(1, IdlePieces(builder).Count, "one missing piece");
      FillTrack(game, builder);
      // LV7 tap positions: first / last / second / third.
      for (int q = 0; q < 4; q++) {
        game.SetLevelForTests(7, q);
        Assert.AreEqual(OrderingStation.Phase.TapAnswer, game.Current, "tap mode");
        OrderingPiece target = builder.Slots[game.TapTargetSlot].Occupant;
        Assert.IsNotNull(target, "target piece on track");
        game.TrySelect(target);
        Assert.IsTrue(game.LastCorrect, "position question " + q + " wins");
      }
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S20F_MissionCompletionSpeech() {
    GameObject arena;
    OrderingStationBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(OrderingStationBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("ordering_station", "test");
      OrderingStation game = BuildGame(builder, arena, player, audio, life);
      // LV8 five-piece order.
      game.SetLevelForTests(8, 0);
      Assert.AreEqual(5, builder.Slots.Count, "five slots");
      FillTrack(game, builder);
      // LV9 five-slot insert.
      game.SetLevelForTests(9, 0);
      FillTrack(game, builder);
      // LV10 mission: full five-train order.
      game.SetLevelForTests(10, 0);
      FillTrack(game, builder);
      Drain(game, 8);
      Assert.IsTrue(game.Completed, "visit completes");
      Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle completed");
      Assert.AreEqual(OrderingStation.Phase.Done, game.Current);
      foreach (string line in audio.Lines) {
        bool ok = SafetyFilter.ValidateLine(line, false, out string why);
        Assert.IsTrue(ok, "line '" + line + "' " + why);
      }
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S20G_ReentryNoDuplicate() {
    GameObject arena;
    OrderingStationBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(OrderingStationBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("ordering_station", "test");
      OrderingStation a = BuildGame(builder, arena, player, audio, life);
      OrderingStation b = BuildGame(builder, arena, player, audio, life);
      Assert.AreSame(a, b, "same component");
      Assert.AreEqual(1, arena.GetComponents<OrderingStation>().Length, "no duplicate listeners");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
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
}
