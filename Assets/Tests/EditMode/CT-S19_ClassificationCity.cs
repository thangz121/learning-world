// CT-S19: classification_city (Thành Phố Phân Loại). IMPLEMENTED, not HUMAN_ACCEPTED.
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CT_S19_ClassificationCity {
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

  static ClassificationCityBuilder BuildArena(out GameObject arena) {
    arena = new GameObject("S19CityWorld");
    ClassificationCityBuilder builder = arena.AddComponent<ClassificationCityBuilder>();
    builder.BuildContent(arena.transform);
    return builder;
  }

  static ClassificationCity BuildGame(ClassificationCityBuilder builder, GameObject arena,
      GameObject player, FakeAudio audio, ActivityLifecycle life) {
    ClassificationCity game = arena.GetComponent<ClassificationCity>();
    if (game == null) game = arena.AddComponent<ClassificationCity>();
    game.Build(builder, player != null ? player.transform : null, null, audio, life);
    return game;
  }

  static GameObject PlayerAt(Vector3 local) {
    GameObject p = new GameObject("S19Player");
    p.transform.position = local;
    return p;
  }

  static void Drain(ClassificationCity game, int n) {
    for (int i = 0; i < n; i++) game.Tick(0.1f);
  }

  static ClassificationBin BinFor(ClassificationCityBuilder b, string key) {
    for (int i = 0; i < b.Bins.Count; i++) {
      ClassificationBin bin = b.Bins[i];
      if (bin != null && bin.GroupKey == key) return bin;
    }
    return null;
  }

  static List<ClassificationItem> IdleItems(ClassificationCityBuilder b) {
    List<ClassificationItem> list = new List<ClassificationItem>();
    for (int i = 0; i < b.Items.Count; i++) {
      ClassificationItem it = b.Items[i];
      if (it != null && it.State == ClassificationItem.ItemState.Idle) list.Add(it);
    }
    return list;
  }

  // Classify every idle item of the CURRENT round into its correct bin.
  // Stops at the round boundary (the last placement starts a fresh round).
  static void PlaceAll(ClassificationCity game, ClassificationCityBuilder b) {
    int target = IdleItems(b).Count;
    Assert.Greater(target, 0, "round has items");
    int startRound = game.RoundIndex;
    int startLevel = game.Level;
    for (int n = 0; n < target; n++) {
      List<ClassificationItem> idle = IdleItems(b);
      Assert.Greater(idle.Count, 0, "item left to sort");
      ClassificationItem it = idle[0];
      game.TrySelect(it);
      int g = ClassLogic.GroupFor(it.Props, game.Criterion, game.Groups);
      string want = g >= 0 ? game.Groups[g] : "";
      ClassificationBin bin = BinFor(b, want);
      Assert.IsNotNull(bin, "bin for " + want);
      game.TryPlace(bin);
    }
    Assert.IsTrue(game.Completed || game.RoundIndex != startRound || game.Level != startLevel,
      "round advances");
  }

  [Test] public void S19A_Registration() {
    GameEntry g = LearningMap.Game("classification_city");
    Assert.IsNotNull(g, "registered");
    Assert.AreEqual("math_classification", g.SkillId, "correct skill");
    Assert.AreEqual("math", LearningMap.SubjectOfSkill(g.SkillId).Id, "correct subject");
    Assert.AreEqual("ClassificationCityPlayScene", g.SceneName, "correct scene");
    Assert.AreEqual(GameStatus.Implemented, g.Status, "IMPLEMENTED");
    Assert.IsFalse(g.HumanAccepted, "not HUMAN_ACCEPTED");
    Assert.IsFalse(LearningMap.IsPlayable("classification_city"), "acceptance firewall holds");
    Assert.IsTrue(LearningMap.CanLaunch("classification_city"), "may enter for play");
  }

  [Test] public void S19B_CriteriaModel() {
    ClassItem cat = new ClassItem("animal", false, 1, false, false, ClassFunction.Play);
    ClassItem car = new ClassItem("vehicle", true, 0, true, true, ClassFunction.Move);
    ClassItem apple = new ClassItem("food", false, 2, false, false, ClassFunction.Eat);
    Assert.AreEqual(1, ClassLogic.GroupFor(car, ClassCriterion.ObjectType,
      new string[] { "animal", "vehicle" }), "two-group type");
    Assert.AreEqual(1, ClassLogic.GroupFor(cat, ClassCriterion.Size,
      new string[] { "big", "small" }), "small by size");
    Assert.AreEqual(0, ClassLogic.GroupFor(car, ClassCriterion.Wheels,
      new string[] { "wheels", "nowheels" }), "wheels property");
    Assert.AreEqual(0, ClassLogic.GroupFor(apple, ClassCriterion.Function,
      new string[] { "food", "transport", "toy" }), "food function");
    ClassItem[] trio = { cat, new ClassItem("animal", true, 0, false, false, ClassFunction.Play), car };
    Assert.AreEqual(2, ClassLogic.OddIndex(trio, ClassCriterion.ObjectType), "odd one out");
    Assert.AreEqual(-1, ClassLogic.OddIndex(
      new ClassItem[] { cat, cat, cat }, ClassCriterion.ObjectType), "no single odd");
    ClassItem bigBall = new ClassItem("toy", true, 0, false, false, ClassFunction.Play);
    ClassItem bigBox = new ClassItem("toy", true, 1, false, true, ClassFunction.Play);
    ClassItem smallBall = new ClassItem("toy", false, 2, false, false, ClassFunction.Play);
    Assert.AreEqual(ClassCriterion.Size, ClassLogic.InferCriterion(
      new ClassItem[] { bigBall, bigBox }, new ClassItem[] { smallBall, cat }),
      "discover size rule");
  }

  [Test] public void S19C_YardDoorAndReturn() {
    GameObject r = new GameObject("S19Yard");
    try {
      SelectionYardBuilder b = r.AddComponent<SelectionYardBuilder>();
      b.Level = "game";
      b.SkillId = "math_classification";
      b.BuildContent(r.transform);
      Assert.AreEqual(1, b.GatePortals.Count, "one city door");
      Assert.AreEqual("classification_city", b.GateTargetIds[0]);
      Assert.IsNotNull(FindDeep(r.transform, "SYPreview_classification_city"), "wordless preview");
    } finally { Object.DestroyImmediate(r); }
    GameObject go = new GameObject("S19Area");
    try {
      SelectionYardArea area = go.AddComponent<SelectionYardArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      area.SetCurrentForTests(SelectionYardArea.YardLevel.Game, "", "math_classification");
      area.PlayGame("classification_city");
      Assert.AreEqual(1, area.PlayRequests, "classification yard launches the city");
      Assert.AreEqual("skill:math", area.BackTargetForTests(), "return is the skill yard");
      area.PlayGame("rabbit_feeding");
      Assert.AreEqual(1, area.PlayRequests, "counting game refused here");
    } finally { Object.DestroyImmediate(go); }
  }

  [Test] public void S19D_TwoGroupsAndRejection() {
    GameObject arena;
    ClassificationCityBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(ClassificationCityBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ClassificationCity game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("classification_city", "test"));
      Drain(game, 5);
      Assert.AreEqual(ClassificationCity.Phase.Sort, game.Current, "starts at play");
      Assert.AreEqual(3, game.Level, "LV3 first");
      Assert.AreEqual(ClassCriterion.ObjectType, game.Criterion, "type criterion");
      Assert.AreEqual(6, IdleItems(builder).Count, "six town objects");
      // Wrong destination is gently rejected, item stays in hand.
      List<ClassificationItem> idle = IdleItems(builder);
      ClassificationItem first = idle[0];
      game.TrySelect(first);
      int g = ClassLogic.GroupFor(first.Props, game.Criterion, game.Groups);
      string want = game.Groups[g];
      ClassificationBin wrong = BinFor(builder, want == "animal" ? "vehicle" : "animal");
      Assert.IsNotNull(wrong, "a wrong bin exists");
      game.TryPlace(wrong);
      Assert.AreEqual(1, game.WrongCount, "wrong destination rejected");
      Assert.AreEqual(0, game.PlacedCount, "nothing placed");
      // Correct destination accepts.
      ClassificationBin right = BinFor(builder, want);
      game.TryPlace(right);
      Assert.AreEqual(1, game.PlacedCount, "correct destination accepts");
      Assert.IsTrue(game.LastCorrect || game.PlacedCount == 1, "progress recorded");
      PlaceAll(game, builder);
      Assert.AreEqual(1, game.RoundIndex, "round advances");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S19E_SizeAttributeThreeGroups() {
    GameObject arena;
    ClassificationCityBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(ClassificationCityBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ClassificationCity game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("classification_city", "test"));
      game.SetLevelForTests(4, 0);
      Assert.AreEqual(ClassCriterion.Size, game.Criterion, "size criterion");
      PlaceAll(game, builder);
      game.SetLevelForTests(5, 0);
      Assert.AreEqual(ClassCriterion.Wheels, game.Criterion, "wheels property");
      PlaceAll(game, builder);
      game.SetLevelForTests(6, 0);
      Assert.AreEqual(3, builder.Bins.Count, "three destinations");
      PlaceAll(game, builder);
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S19F_RuleChangeFunctionOddDiscover() {
    GameObject arena;
    ClassificationCityBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(ClassificationCityBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("classification_city", "test");
      ClassificationCity game = BuildGame(builder, arena, player, audio, life);
      // LV7: same pool, three different rules.
      game.SetLevelForTests(7, 0);
      Assert.AreEqual(ClassCriterion.ObjectType, game.Criterion, "rule 1: type");
      PlaceAll(game, builder);
      game.SetLevelForTests(7, 1);
      Assert.AreEqual(ClassCriterion.Size, game.Criterion, "rule 2: size");
      PlaceAll(game, builder);
      game.SetLevelForTests(7, 2);
      Assert.AreEqual(ClassCriterion.Color, game.Criterion, "rule 3: color");
      bool okLine = false;
      Drain(game, 30);
      for (int i = 0; i < audio.Lines.Count; i++)
        if (audio.Lines[i].Contains("Đổi cách xếp") || audio.Lines[i].Contains("New sorting rule"))
          okLine = true;
      Assert.IsTrue(okLine, "rule change is announced");
      PlaceAll(game, builder);
      // LV8: function.
      game.SetLevelForTests(8, 0);
      Assert.AreEqual(ClassCriterion.Function, game.Criterion, "function criterion");
      PlaceAll(game, builder);
      // LV9: odd one out — a wrong first tap is rejected.
      game.SetLevelForTests(9, 0);
      List<ClassificationItem> idle = IdleItems(builder);
      Assert.AreEqual(4, idle.Count, "four suspects");
      ClassItem[] props = new ClassItem[idle.Count];
      for (int i = 0; i < idle.Count; i++) props[i] = idle[i].Props;
      int odd = ClassLogic.OddIndex(props, game.Criterion);
      Assert.GreaterOrEqual(odd, 0, "a true odd item exists");
      ClassificationItem notOdd = idle[(odd + 1) % idle.Count];
      game.TrySelect(notOdd);
      Assert.AreEqual(1, game.WrongCount, "non-odd tap rejected");
      Assert.IsFalse(game.OddPicked, "odd not picked");
      game.TrySelect(idle[odd]);
      Assert.IsTrue(game.OddPicked, "odd picked up");
      game.TryPlace(BinFor(builder, "other"));
      // LV10: infer + apply, then complete.
      game.SetLevelForTests(10, 1);
      Drain(game, 2);
      PlaceAll(game, builder);
      Drain(game, 8);
      Assert.IsTrue(game.Completed, "visit completes");
      Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle completed");
      Assert.AreEqual(ClassificationCity.Phase.Done, game.Current);
      foreach (string line in audio.Lines) {
        bool ok = SafetyFilter.ValidateLine(line, false, out string why);
        Assert.IsTrue(ok, "line '" + line + "' " + why);
      }
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S19G_ReentryNoDuplicate() {
    GameObject arena;
    ClassificationCityBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(ClassificationCityBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("classification_city", "test");
      ClassificationCity a = BuildGame(builder, arena, player, audio, life);
      ClassificationCity b = BuildGame(builder, arena, player, audio, life);
      Assert.AreSame(a, b, "same component");
      Assert.AreEqual(1, arena.GetComponents<ClassificationCity>().Length, "no duplicate listeners");
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
