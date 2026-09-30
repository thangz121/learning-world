// CT-S18: comparison_market (Khu Chợ Của Bé). IMPLEMENTED, not HUMAN_ACCEPTED.
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CT_S18_ComparisonMarket {
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

  static ComparisonMarketBuilder BuildArena(out GameObject arena) {
    arena = new GameObject("S18MarketWorld");
    ComparisonMarketBuilder builder = arena.AddComponent<ComparisonMarketBuilder>();
    builder.BuildContent(arena.transform);
    return builder;
  }

  static ComparisonMarket BuildGame(ComparisonMarketBuilder builder, GameObject arena,
      GameObject player, FakeAudio audio, ActivityLifecycle life) {
    ComparisonMarket game = arena.GetComponent<ComparisonMarket>();
    if (game == null) game = arena.AddComponent<ComparisonMarket>();
    game.Build(builder, player != null ? player.transform : null, null, audio, life);
    return game;
  }

  static GameObject PlayerAt(Vector3 local) {
    GameObject p = new GameObject("S18Player");
    p.transform.position = local;
    return p;
  }

  static void Drain(ComparisonMarket game, int n) {
    for (int i = 0; i < n; i++) game.Tick(0.1f);
  }

  static ComparisonChoice AnswerOf(ComparisonMarketBuilder b) {
    for (int i = 0; i < b.Choices.Count; i++) {
      ComparisonChoice c = b.Choices[i];
      if (c != null && c.IsAnswer) return c;
    }
    return null;
  }

  static ComparisonChoice WrongOf(ComparisonMarketBuilder b) {
    for (int i = 0; i < b.Choices.Count; i++) {
      ComparisonChoice c = b.Choices[i];
      if (c != null && !c.IsAnswer) return c;
    }
    return null;
  }

  [Test] public void S18A_Registration() {
    GameEntry g = LearningMap.Game("comparison_market");
    Assert.IsNotNull(g, "registered");
    Assert.AreEqual("math_comparison", g.SkillId, "correct skill");
    Assert.AreEqual("math", LearningMap.SubjectOfSkill(g.SkillId).Id, "correct subject");
    Assert.AreEqual("ComparisonMarketPlayScene", g.SceneName, "correct scene");
    Assert.AreEqual(GameStatus.Implemented, g.Status, "IMPLEMENTED");
    Assert.IsFalse(g.HumanAccepted, "not HUMAN_ACCEPTED");
    Assert.IsFalse(LearningMap.IsPlayable("comparison_market"), "acceptance firewall holds");
    Assert.IsTrue(LearningMap.CanLaunch("comparison_market"), "may enter for play");
  }

  [Test] public void S18B_ComparisonModel() {
    Assert.AreEqual(1, ComparisonLogic.CompareCounts(5, 3), "MORE logic");
    Assert.AreEqual(-1, ComparisonLogic.CompareCounts(2, 4), "LESS logic");
    Assert.AreEqual(0, ComparisonLogic.CompareCounts(3, 3), "EQUAL logic");
    Assert.AreEqual(0, ComparisonLogic.AnswerMoreLess(5, 3, true), "more of 5v3 is left");
    Assert.AreEqual(1, ComparisonLogic.AnswerMoreLess(2, 4, true), "more of 2v4 is right");
    Assert.AreEqual(0, ComparisonLogic.AnswerMoreLess(2, 4, false), "less of 2v4 is left");
    Assert.AreEqual(1, ComparisonLogic.LargestIndex(new int[] { 2, 5, 3 }), "largest");
    Assert.AreEqual(0, ComparisonLogic.SmallestIndex(new int[] { 2, 5, 3 }), "smallest");
    Assert.AreEqual(0, ComparisonLogic.LongestIndex(new float[] { 2.2f, 1.3f, 1.7f }), "longest");
    Assert.AreEqual(1, ComparisonLogic.ShortestIndex(new float[] { 2.2f, 1.3f, 1.7f }), "shortest");
    Assert.IsTrue(ComparisonLogic.AllEqual(new int[] { 3, 3 }), "all equal");
    Assert.IsFalse(ComparisonLogic.AllEqual(new int[] { 3, 4 }), "not equal");
  }

  [Test] public void S18C_YardDoorAndReturn() {
    GameObject r = new GameObject("S18Yard");
    try {
      SelectionYardBuilder b = r.AddComponent<SelectionYardBuilder>();
      b.Level = "game";
      b.SkillId = "math_comparison";
      b.BuildContent(r.transform);
      Assert.AreEqual(1, b.GatePortals.Count, "one market door");
      Assert.AreEqual("comparison_market", b.GateTargetIds[0]);
      Assert.IsNotNull(FindDeep(r.transform, "SYPreview_comparison_market"), "wordless preview");
    } finally { Object.DestroyImmediate(r); }
    GameObject go = new GameObject("S18Area");
    try {
      SelectionYardArea area = go.AddComponent<SelectionYardArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      area.SetCurrentForTests(SelectionYardArea.YardLevel.Game, "", "math_comparison");
      area.PlayGame("comparison_market");
      Assert.AreEqual(1, area.PlayRequests, "comparison yard launches the market");
      Assert.AreEqual("skill:math", area.BackTargetForTests(), "return is the skill yard");
      area.PlayGame("rabbit_feeding");
      Assert.AreEqual(1, area.PlayRequests, "counting game refused here");
    } finally { Object.DestroyImmediate(go); }
  }

  [Test] public void S18D_MoreLessFlow() {
    GameObject arena;
    ComparisonMarketBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(ComparisonMarketBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ComparisonMarket game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("comparison_market", "test"));
      Drain(game, 5);
      Assert.AreEqual(ComparisonMarket.Phase.Choose, game.Current, "starts at play");
      Assert.AreEqual(3, game.Level, "LV3 first");
      Assert.AreEqual(ComparisonType.Quantity, game.CType, "quantity type");
      Assert.AreEqual(2, builder.Choices.Count, "two baskets");
      ComparisonChoice ans = AnswerOf(builder);
      ComparisonChoice wrong = WrongOf(builder);
      Assert.IsNotNull(ans, "an answer exists");
      Assert.IsNotNull(wrong, "a distractor exists");
      game.TryTapChoice(wrong);
      Assert.AreEqual(1, game.WrongCount, "incorrect rejected");
      Assert.AreEqual(3, game.Level, "still LV3 round 0");
      Assert.IsFalse(game.LastCorrect, "no false win");
      game.TryTapChoice(ans);
      Assert.IsTrue(game.LastCorrect, "correct wins the round");
      Assert.AreEqual(1, game.RoundIndex, "round advances");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S18E_InvariancePairingAndMismatch() {
    GameObject arena;
    ComparisonMarketBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(ComparisonMarketBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ComparisonMarket game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("comparison_market", "test"));
      // LV4: equal counts, both baskets answer.
      game.SetLevelForTests(4, 0);
      Assert.AreEqual(TargetRelation.Equal, game.Relation, "equality relation");
      int answers = 0;
      for (int i = 0; i < builder.Choices.Count; i++)
        if (builder.Choices[i].IsAnswer) answers++;
      Assert.AreEqual(2, answers, "spread does not decide: both equal");
      game.TryTapChoice(builder.Choices[0]);
      Assert.IsTrue(game.LastCorrect, "either basket wins on equal");
      // LV5: pair three apples with four oranges, then confirm.
      game.SetLevelForTests(5, 0);
      Assert.AreEqual(ComparisonMarket.Phase.Pair, game.Current, "pairing phase");
      Assert.AreEqual(3, game.PairsNeed, "three pairs");
      List<ComparisonFruit> apples = new List<ComparisonFruit>();
      foreach (ComparisonFruit f in builder.Fruits)
        if (f != null && f.Group == 0) apples.Add(f);
      Assert.AreEqual(3, apples.Count, "three apples");
      for (int i = 0; i < apples.Count; i++) game.TryTapFruit(apples[i]);
      Assert.AreEqual(ComparisonMarket.Phase.Confirm, game.Current, "confirm after pairing");
      ComparisonChoice leftover = AnswerOf(builder);
      Assert.IsNotNull(leftover, "leftover group");
      game.TryTapChoice(leftover);
      Assert.IsTrue(game.LastCorrect, "leftover group wins");
      // LV6: mismatched item sizes.
      game.SetLevelForTests(6, 0);
      Assert.AreEqual(TargetRelation.More, game.Relation, "more despite sizes");
      Assert.AreEqual(2, builder.Choices.Count, "two baskets");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S18F_SizeLengthThreeWayDelivery() {
    GameObject arena;
    ComparisonMarketBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(ComparisonMarketBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("comparison_market", "test");
      ComparisonMarket game = BuildGame(builder, arena, player, audio, life);
      game.SetLevelForTests(7, 0);
      Assert.AreEqual(ComparisonType.Size, game.CType, "size type");
      Assert.AreEqual(TargetRelation.Larger, game.Relation, "larger relation");
      game.TryTapChoice(AnswerOf(builder));
      Assert.IsTrue(game.LastCorrect, "bigger wins");
      game.SetLevelForTests(8, 0);
      Assert.AreEqual(TargetRelation.Longer, game.Relation, "longer relation");
      game.TryTapChoice(AnswerOf(builder));
      Assert.IsTrue(game.LastCorrect, "longer wins");
      game.SetLevelForTests(9, 0);
      Assert.AreEqual(3, builder.Choices.Count, "three contenders");
      game.TryTapChoice(AnswerOf(builder));
      Assert.IsTrue(game.LastCorrect, "biggest of three wins");
      // LV10: choose then deliver to the cart.
      game.SetLevelForTests(10, 1);
      Drain(game, 2);
      game.TryTapChoice(AnswerOf(builder));
      Assert.AreEqual(ComparisonMarket.Phase.Deliver, game.Current, "carry to cart");
      game.TryTapChoice(builder.CartChoice);
      Drain(game, 8);
      Assert.IsTrue(game.Completed, "visit completes");
      Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle completed");
      Assert.AreEqual(ComparisonMarket.Phase.Done, game.Current);
      foreach (string line in audio.Lines) {
        bool ok = SafetyFilter.ValidateLine(line, false, out string why);
        Assert.IsTrue(ok, "line '" + line + "' " + why);
      }
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S18G_ReentryNoDuplicate() {
    GameObject arena;
    ComparisonMarketBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(ComparisonMarketBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("comparison_market", "test");
      ComparisonMarket a = BuildGame(builder, arena, player, audio, life);
      ComparisonMarket b = BuildGame(builder, arena, player, audio, life);
      Assert.AreSame(a, b, "same component");
      Assert.AreEqual(1, arena.GetComponents<ComparisonMarket>().Length, "no duplicate listeners");
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
