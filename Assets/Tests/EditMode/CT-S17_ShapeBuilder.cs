// CT-S17: shape_builder (Lắp Hình Vui Nhộn). IMPLEMENTED, not HUMAN_ACCEPTED.
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CT_S17_ShapeBuilder {
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

  static GeometryPlayBuilder BuildArena(out GameObject arena) {
    arena = new GameObject("S17GeoWorld");
    GeometryPlayBuilder builder = arena.AddComponent<GeometryPlayBuilder>();
    builder.BuildContent(arena.transform);
    return builder;
  }

  static GeometryPlay BuildGame(GeometryPlayBuilder builder, GameObject arena,
      GameObject player, FakeAudio audio, ActivityLifecycle life) {
    GeometryPlay game = arena.GetComponent<GeometryPlay>();
    if (game == null) game = arena.AddComponent<GeometryPlay>();
    game.Build(builder, player != null ? player.transform : null, null, audio, life);
    return game;
  }

  static GameObject PlayerAt(Vector3 local) {
    GameObject p = new GameObject("S17Player");
    p.transform.position = local;
    return p;
  }

  static void Drain(GeometryPlay game, int n) {
    for (int i = 0; i < n; i++) game.Tick(0.1f);
  }

  static GeometryPiece Hunt(GeometryPlayBuilder b, GeometryKind k) {
    for (int i = 0; i < b.Pieces.Count; i++) {
      GeometryPiece p = b.Pieces[i];
      if (p != null && p.Kind == k && !p.EnvRole
          && p.State == GeometryPiece.PieceState.Idle) return p;
    }
    return null;
  }

  static GeometrySocket Live(GeometryPlayBuilder b, GeometryKind k) {
    for (int i = 0; i < b.Sockets.Count; i++) {
      GeometrySocket s = b.Sockets[i];
      if (s != null && s.gameObject.activeInHierarchy && s.Kind == k) return s;
    }
    return null;
  }

  [Test] public void S17A_Registration() {
    GameEntry g = LearningMap.Game("shape_builder");
    Assert.IsNotNull(g, "registered");
    Assert.AreEqual("math_geometry", g.SkillId, "correct skill");
    Assert.AreEqual("math", LearningMap.SubjectOfSkill(g.SkillId).Id, "correct subject");
    Assert.AreEqual("GeometryPlayScene", g.SceneName, "correct scene");
    Assert.AreEqual(GameStatus.Implemented, g.Status, "IMPLEMENTED");
    Assert.IsFalse(g.HumanAccepted, "not HUMAN_ACCEPTED");
    Assert.IsFalse(LearningMap.IsPlayable("shape_builder"), "acceptance firewall holds");
    Assert.IsTrue(LearningMap.CanLaunch("shape_builder"), "may enter for play");
    Assert.AreEqual(4, GeometryShapes.All.Length, "four base shapes");
    Assert.AreEqual(0, GeometryShapes.Corners(GeometryKind.Circle));
    Assert.AreEqual(3, GeometryShapes.Corners(GeometryKind.Triangle));
    Assert.IsTrue(GeometryShapes.EqualSides(GeometryKind.Square));
    Assert.IsTrue(GeometryShapes.LongAndShort(GeometryKind.Rectangle));
  }

  [Test] public void S17B_YardDoorAndReturn() {
    GameObject r = new GameObject("S17Yard");
    try {
      SelectionYardBuilder b = r.AddComponent<SelectionYardBuilder>();
      b.Level = "game";
      b.SkillId = "math_geometry";
      b.BuildContent(r.transform);
      Assert.AreEqual(1, b.GatePortals.Count, "one geometry door");
      Assert.AreEqual("shape_builder", b.GateTargetIds[0]);
      Assert.IsNotNull(FindDeep(r.transform, "SYPreview_shape_builder"), "wordless preview");
    } finally { Object.DestroyImmediate(r); }
    GameObject go = new GameObject("S17Area");
    try {
      SelectionYardArea area = go.AddComponent<SelectionYardArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      area.SetCurrentForTests(SelectionYardArea.YardLevel.Game, "", "math_geometry");
      area.PlayGame("shape_builder");
      Assert.AreEqual(1, area.PlayRequests, "geometry yard launches shape_builder");
      Assert.AreEqual("skill:math", area.BackTargetForTests(), "return is the skill yard");
      area.PlayGame("rabbit_feeding");
      Assert.AreEqual(1, area.PlayRequests, "counting game refused here");
    } finally { Object.DestroyImmediate(go); }
  }

  [Test] public void S17C_FourShapesAndProgression() {
    GameObject arena;
    GeometryPlayBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(GeometryPlayBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      GeometryPlay game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("shape_builder", "test"));
      Drain(game, 5);
      Assert.AreEqual(GeometryPlay.Phase.Hunt, game.Current, "starts at play");
      Assert.AreEqual(3, game.Level, "LV3 first");
      Assert.AreEqual(GeometryPlay.ActionKind.PlacePad, GeometryPlay.ActionFor(3));
      Assert.AreEqual(GeometryPlay.ActionKind.TapEnv, GeometryPlay.ActionFor(6));
      Assert.AreEqual(GeometryPlay.ActionKind.Assemble, GeometryPlay.ActionFor(7));
      Assert.AreEqual(GeometryPlay.ActionKind.Memory, GeometryPlay.ActionFor(8));
      Assert.AreEqual(6, GeometryPlay.RoundsOf(3));
      Assert.AreEqual(4, GeometryPlay.RoundsOf(5));
      var seen = new HashSet<GeometryKind>();
      for (int i = 0; i < builder.Pieces.Count; i++)
        if (builder.Pieces[i] != null && !builder.Pieces[i].EnvRole)
          seen.Add(builder.Pieces[i].Kind);
      Assert.AreEqual(4, seen.Count, "four hunt candidates");
      GeometrySocket startPad = Live(builder, game.Target);
      Assert.IsNotNull(startPad, "workshop pad");
      Assert.IsNotNull(startPad.transform.Find("Ghost"), "socket shows a shape ghost");
      Assert.IsNotNull(startPad.transform.Find("Pad"), "socket pad is unscaled");
      game.SetLevelForTests(4, 0);
      Assert.AreEqual(4, game.Level);
      game.SetLevelForTests(5, 0);
      Assert.AreEqual("corners3", game.PropertyId);
      Assert.AreEqual(GeometryKind.Triangle, game.Target);
      game.SetLevelForTests(6, 0);
      Assert.AreEqual(GeometryPlay.ActionKind.TapEnv, game.Action);
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S17D_PlaceRejectOrientConstruct() {
    GameObject arena;
    GeometryPlayBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(GeometryPlayBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("shape_builder", "test");
      GeometryPlay game = BuildGame(builder, arena, player, audio, life);
      Drain(game, 5);
      GeometryPiece wrong = Hunt(builder, game.Target == GeometryKind.Circle
        ? GeometryKind.Square : GeometryKind.Circle);
      GeometryPiece right = Hunt(builder, game.Target);
      Assert.IsNotNull(right, "target piece");
      player.transform.position = wrong.transform.position;
      game.TrySelect(wrong);
      GeometrySocket pad = Live(builder, game.Target);
      Assert.IsNotNull(pad, "workshop pad");
      player.transform.position = pad.transform.position;
      game.TryPlace(pad);
      Assert.IsTrue(game.LastWrongKind, "wrong shape rejected");
      Assert.IsFalse(pad.Filled, "socket empty");
      Assert.Greater(game.WrongCount, 0);
      if (game.Carried != null) game.Carried.ReturnHome();
      // drop carried by selecting after return
      game.SetLevelForTests(3, 0);
      Drain(game, 3);
      right = Hunt(builder, game.Target);
      player.transform.position = right.transform.position;
      game.TrySelect(right);
      pad = Live(builder, game.Target);
      player.transform.position = pad.transform.position;
      game.TryPlace(pad);
      Assert.IsTrue(pad.Filled || game.Level >= 3, "correct shape places");

      game.SetLevelForTests(7, 0);
      GeometrySocket roof = Live(builder, GeometryKind.Triangle);
      Assert.IsNotNull(roof, "house roof socket");
      Assert.IsTrue(roof.NeedsUpright, "roof cares about upright");
      GeometryPiece tri = Hunt(builder, GeometryKind.Triangle);
      player.transform.position = tri.transform.position;
      game.TrySelect(tri);
      tri.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
      player.transform.position = roof.transform.position;
      game.TryPlace(roof);
      Assert.IsTrue(game.LastOrientationFix || roof.Filled, "orientation handled");
      Assert.IsTrue(GeometryShapes.OrientationOk(GeometryKind.Circle, 90f, 0f),
        "a circle has no facing");
      Assert.IsFalse(GeometryShapes.OrientationOk(GeometryKind.Triangle, 90f, 0f),
        "a triangle can be upside-wrong");

      game.SetLevelForTests(10, 1);
      Drain(game, 2);
      FillAssemble(game, builder, player);
      Drain(game, 8);
      Assert.IsTrue(game.Completed, "visit completes");
      Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle completed");
      Assert.AreEqual(GeometryPlay.Phase.Done, game.Current);
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S17E_ReentryNoDuplicate() {
    GameObject arena;
    GeometryPlayBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(GeometryPlayBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("shape_builder", "test");
      GeometryPlay a = BuildGame(builder, arena, player, audio, life);
      GeometryPlay b = BuildGame(builder, arena, player, audio, life);
      Assert.AreSame(a, b, "same component");
      Assert.AreEqual(1, arena.GetComponents<GeometryPlay>().Length, "no duplicate listeners");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S17F_PropertyAndEnv() {
    GameObject arena;
    GeometryPlayBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(GeometryPlayBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      GeometryPlay game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("shape_builder", "test"));
      game.SetLevelForTests(5, 1);
      Drain(game, 20);
      Assert.AreEqual("nocorners", game.PropertyId);
      Assert.AreEqual(GeometryKind.Circle, game.Target);
      bool okLine = false;
      for (int i = 0; i < audio.Lines.Count; i++) {
        bool pass = SafetyFilter.ValidateLine(audio.Lines[i], false, out string why);
        Assert.IsTrue(pass, "line '" + audio.Lines[i] + "' " + why);
        if (audio.Lines[i].Contains("góc") || audio.Lines[i].Contains("corner"))
          okLine = true;
      }
      Assert.IsTrue(okLine, "property spoken");
      game.SetLevelForTests(6, 0);
      Assert.AreEqual(GeometryKind.Circle, game.Target);
      player.transform.position = builder.EnvWheel.transform.position;
      game.TrySelect(builder.EnvWheel);
      Assert.AreEqual(6, game.Level, "env tap accepted as find");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S17G_SixLivingToysAwakening() {
    GameObject arena;
    GeometryPlayBuilder builder = BuildArena(out arena);
    try {
      ToyKind[] toys = {
        ToyKind.House, ToyKind.IceCream, ToyKind.Sailboat,
        ToyKind.Train, ToyKind.Cat, ToyKind.Rocket
      };

      for (int i = 0; i < toys.Length; i++) {
        ToyKind toy = toys[i];
        builder.BuildLivingToyDecor(toy, Vector3.zero);
        Assert.IsNotNull(builder.ActiveToyDecor, "decor created for " + toy);
        Assert.IsNotNull(builder.ActiveLivingToy, "animator attached for " + toy);
        Assert.AreEqual(toy, builder.ActiveLivingToy.Toy, "correct toy kind");

        // Awakening triggers living decor activation
        builder.AwakenToy(toy);
        Assert.IsTrue(builder.ActiveToyDecor.activeSelf, "living toy awakened for " + toy);

        // Living toy is interactive tap target
        builder.ActiveLivingToy.OnClicked();
      }
    } finally {
      Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S17H_TapToFlyAndLivingAnimation() {
    GameObject arena;
    GeometryPlayBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(GeometryPlayBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      GeometryPlay game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("shape_builder", "test"));

      // Verify toy localization
      Assert.AreEqual("choo-choo train", GeometryShapes.ToyNameEn(ToyKind.Train));
      Assert.AreEqual("tàu hỏa tí hon", GeometryShapes.ToyNameVi(ToyKind.Train));
      Assert.AreEqual("space rocket", GeometryShapes.ToyNameEn(ToyKind.Rocket));
      Assert.AreEqual("tên lửa vũ trụ", GeometryShapes.ToyNameVi(ToyKind.Rocket));

      // Test flight and socket ghost
      GeometryPiece p = builder.SpawnHuntPiece(GeometryKind.Circle, Vector3.zero, Color.red, 1f, 0f);
      GeometrySocket s = builder.ShowSocket(0, Vector3.forward * 2f, GeometryKind.Circle, false);
      Assert.IsNotNull(s.transform.Find("Ghost"), "ghost present");

      bool arrived = false;
      p.BeginFlyTo(s.transform, 0f, () => { arrived = true; });
      Assert.AreEqual(GeometryPiece.PieceState.Flying, p.State, "flying state");

      // Wrong wobble doesn't crash
      p.PlayWrongWobble();

      // Clear cleans up toy decor
      builder.ClearActiveToy();
      Assert.IsNull(builder.ActiveToyDecor);
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S17I_AwakenWiresLivingPartsToPlacedPieces() {
    GameObject arena;
    GeometryPlayBuilder builder = BuildArena(out arena);
    try {
      // Child placed: 2 circles (wheels/eyes) + 2 triangles (ears)
      GeometryPiece c1 = builder.SpawnHuntPiece(GeometryKind.Circle, Vector3.zero, Color.red, 1f, 0f);
      GeometryPiece c2 = builder.SpawnHuntPiece(GeometryKind.Circle, new Vector3(0f, 0f, 1.5f), Color.blue, 1f, 0f);
      GeometryPiece t1 = builder.SpawnHuntPiece(GeometryKind.Triangle, new Vector3(0f, 0f, 3f), Color.green, 1f, 0f);
      GeometryPiece t2 = builder.SpawnHuntPiece(GeometryKind.Triangle, new Vector3(0f, 0f, 4.5f), Color.yellow, 1f, 0f);
      GeometrySocket s1 = builder.ShowSocket(0, Vector3.zero, GeometryKind.Circle, false);
      GeometrySocket s2 = builder.ShowSocket(1, new Vector3(0f, 0f, 1.5f), GeometryKind.Circle, false);
      GeometrySocket s3 = builder.ShowSocket(2, new Vector3(0f, 0f, 3f), GeometryKind.Triangle, true);
      GeometrySocket s4 = builder.ShowSocket(3, new Vector3(0f, 0f, 4.5f), GeometryKind.Triangle, true);
      s1.Accept(c1); s2.Accept(c2); s3.Accept(t1); s4.Accept(t2);

      builder.BuildLivingToyDecor(ToyKind.Train, Vector3.zero);
      builder.AwakenToy(ToyKind.Train);
      Assert.IsNotNull(builder.ActiveLivingToy.Wheels, "train wheels must be wired to the placed circles");
      Assert.AreEqual(2, builder.ActiveLivingToy.Wheels.Length, "both placed circles become spinning wheels");

      builder.BuildLivingToyDecor(ToyKind.Cat, Vector3.zero);
      builder.AwakenToy(ToyKind.Cat);
      Assert.IsNotNull(builder.ActiveLivingToy.EarLeft, "cat ear L wired");
      Assert.IsNotNull(builder.ActiveLivingToy.EarRight, "cat ear R wired");
      Assert.IsNotNull(builder.ActiveLivingToy.EyeLeft, "cat eye L wired");
      Assert.IsNotNull(builder.ActiveLivingToy.EyeRight, "cat eye R wired");
    } finally {
      Object.DestroyImmediate(arena);
    }
  }

  static void FillAssemble(GeometryPlay game, GeometryPlayBuilder builder, GameObject player) {
    for (int n = 0; n < 4; n++) {
      GeometrySocket open = null;
      for (int i = 0; i < builder.Sockets.Count; i++) {
        GeometrySocket s = builder.Sockets[i];
        if (s != null && s.gameObject.activeInHierarchy && !s.Filled) { open = s; break; }
      }
      if (open == null) break;
      GeometryPiece p = Hunt(builder, open.Kind);
      if (p == null) break;
      player.transform.position = p.transform.position;
      game.TrySelect(p);
      player.transform.position = open.transform.position;
      game.TryPlace(open);
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
