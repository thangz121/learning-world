// CT-P10: Phase 2F ball-staging contracts (second staged quest).
// Proves, through REAL services (default BuiltIn providers = the production
// path GameInstaller uses) + the REAL MiaPresenter:
//   A. Ball quest runs the full loop on production content (find -> bring ->
//      completed, friendship +10, flower_pot) — no test provider involved.
//   B. Receiver quest-awareness matrix (the 2F Mia fix):
//      appleQ + apple -> COMPLETE / appleQ + ball -> WRONG (R9 preserved) /
//      ballQ + ball -> COMPLETE / ballQ + apple -> WRONG.
//   C. Ball narration mirrors pin: manifest inst_07/ok_05 texts are EXACTLY
//      what MarketBootstrap speaks (two-way contract; code mirrors content,
//      this test locks the content side — validators lock the rest).
//   D. Ball staging config: crate anchor in bounds, separated from the
//      distractor pedestal and the apple crate (click/raycast unambiguous).
// Pure EditMode. C# 9.0 only.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class CT_P10_BallStaging {
  static readonly QuestId AppleQ = new QuestId("w1_mia_apple");
  static readonly QuestId BallQ = new QuestId("w1_mia_ball");
  static readonly WordId Apple = new WordId("apple");
  static readonly WordId Ball = new WordId("ball");

  sealed class Ctx {
    public GameEventBus Bus;
    public HintService Hints;
    public QuestManager Quests;
    public QuestRewardService Rewards;
    public List<StoryMomentEvent> Moments;
    public Ctx() {
      Bus = new GameEventBus();
      var learning = new LearningService(Bus);
      Hints = new HintService(Bus);
      Quests = new QuestManager(Bus, learning, Hints); // production BuiltIn path
      Rewards = new QuestRewardService(Bus); // production BuiltIn path
      Moments = new List<StoryMomentEvent>();
      Bus.Subscribe<StoryMomentEvent>(e => Moments.Add(e));
      Bus.Subscribe<WordSeenEvent>(e => Quests.AdvanceOnSeen(e.WordId)); // MarketBootstrap glue
    }
  }

  static MiaPresenter NewMia(Ctx ctx) {
    GameObject go = new GameObject("MiaP10Test");
    MiaPresenter mia = go.AddComponent<MiaPresenter>();
    mia.Bind(ctx.Bus, ctx.Quests, ctx.Hints);
    return mia;
  }

  // A. Ball quest completes on the PRODUCTION content path (no test doubles).
  [Test] public void CT_P10A_BallQuestProductionPath() {
    var ctx = new Ctx();
    ctx.Quests.StartQuest(BallQ);
    ctx.Quests.AdvanceOnSeen(Ball);
    Assert.AreEqual(1, ctx.Quests.GetState(BallQ).ObjectiveIndex, "ball find must advance");
    ctx.Quests.ReportAction(PlayerAction.Bring, Ball);
    Assert.IsTrue(ctx.Quests.GetState(BallQ).Completed, "ball bring must complete");
    Assert.AreEqual(10, ctx.Rewards.GetFriendship(new NpcId("mia")), "ball friendship recorded");
    Assert.IsTrue(ctx.Rewards.HasWorldChange("flower_pot"), "ball completion effect recorded");
  }

  // B. Receiver matrix: correct word completes per active quest, wrong wrongs.
  [Test] public void CT_P10B_ReceiverQuestMatrix() {
    // apple quest + apple -> COMPLETE
    {
      var ctx = new Ctx();
      MiaPresenter mia = NewMia(ctx);
      try {
        ctx.Quests.StartQuest(AppleQ);
        ctx.Bus.Publish(new WordSeenEvent(Apple, LearnSource.Object, DateTime.UtcNow));
        mia.OnMiaClicked();
        Assert.IsTrue(ctx.Quests.GetState(AppleQ).Completed, "appleQ+apple must complete");
        Assert.AreEqual(0, ctx.Hints.GetState(AppleQ).WrongCount, "no wrong on correct bring");
      } finally {
        UnityEngine.Object.DestroyImmediate(mia.gameObject);
      }
    }
    // apple quest + ball -> WRONG (R9 behavior preserved by the 2F change)
    {
      var ctx = new Ctx();
      MiaPresenter mia = NewMia(ctx);
      try {
        ctx.Quests.StartQuest(AppleQ);
        ctx.Bus.Publish(new WordSeenEvent(Ball, LearnSource.Object, DateTime.UtcNow));
        mia.OnMiaClicked();
        Assert.AreEqual(1, ctx.Hints.GetState(AppleQ).WrongCount, "appleQ+ball must wrong");
        Assert.IsFalse(ctx.Quests.GetState(AppleQ).Completed, "wrong bring never completes");
      } finally {
        UnityEngine.Object.DestroyImmediate(mia.gameObject);
      }
    }
    // ball quest + ball -> COMPLETE (the 2F fix)
    {
      var ctx = new Ctx();
      MiaPresenter mia = NewMia(ctx);
      try {
        ctx.Quests.StartQuest(BallQ);
        ctx.Bus.Publish(new WordSeenEvent(Ball, LearnSource.Object, DateTime.UtcNow));
        Assert.IsTrue(mia.IsCarryingBall, "setup: ball must arm carry");
        mia.OnMiaClicked();
        Assert.IsTrue(ctx.Quests.GetState(BallQ).Completed, "ballQ+ball must complete");
        Assert.AreEqual(0, ctx.Hints.GetState(BallQ).WrongCount, "no wrong on correct bring");
        Assert.AreEqual(10, ctx.Rewards.GetFriendship(new NpcId("mia")), "ball reward fires");
      } finally {
        UnityEngine.Object.DestroyImmediate(mia.gameObject);
      }
    }
    // ball quest + apple -> WRONG (apple is the distractor now)
    {
      var ctx = new Ctx();
      MiaPresenter mia = NewMia(ctx);
      try {
        ctx.Quests.StartQuest(BallQ);
        ctx.Bus.Publish(new WordSeenEvent(Apple, LearnSource.Object, DateTime.UtcNow));
        mia.OnMiaClicked();
        Assert.AreEqual(1, ctx.Hints.GetState(BallQ).WrongCount, "ballQ+apple must wrong");
        Assert.IsFalse(ctx.Quests.GetState(BallQ).Completed, "wrong bring never completes");
      } finally {
        UnityEngine.Object.DestroyImmediate(mia.gameObject);
      }
    }
  }

  // C. Narration mirror pin: manifest texts equal what Bootstrap speaks.
  // MarketBootstrap mirrors inst_07/ok_05 as consts (runtime catalog loading
  // is stager scope, NOT 2F); this test locks the CONTENT side of that
  // two-way contract — if either text changes, the mirror breaks loudly.
  [Test] public void CT_P10C_BallNarrationMirror() {
    string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Content");
    DialoguePack pack = ContentDatabase.ParseDialoguePack(
      File.ReadAllText(Path.Combine(root, "dialogues", "manifest.json")));
    DialogueLine inst = pack.lines.Find(l => l.id == "inst_07");
    DialogueLine ok = pack.lines.Find(l => l.id == "ok_05");
    Assert.IsNotNull(inst, "inst_07 must exist");
    Assert.IsNotNull(ok, "ok_05 must exist");
    Assert.AreEqual("Ball please!", inst.text, "inst_07 text is the bootstrap mirror source");
    Assert.AreEqual("Great! Ball!", ok.text, "ok_05 text is the bootstrap mirror source");
    Assert.AreEqual("mia_v1", inst.voice, "ask line is Mia-voiced");
    Assert.AreEqual("mia_v1", ok.voice, "praise line is Mia-voiced");
  }

  // D. Staging config: crate in bounds, unambiguous vs pedestal + apple crate.
  [Test] public void CT_P10D_BallStagingConfig() {
    Vector3 crate = MarketBuilder.BallCrateAnchorPos;
    Assert.Less(Mathf.Abs(crate.x), MarketBuilder.BoundX, "crate inside X bounds");
    Assert.Less(Mathf.Abs(crate.z), MarketBuilder.BoundZ, "crate inside Z bounds");
    Vector3 pedestal = new Vector3(5.4f, 0f, 0.6f); // distractor (MarketBuilder.BuildDistractor)
    float dPedestal = Vector3.Distance(crate, pedestal);
    Assert.Greater(dPedestal, 2.0f, "crate vs pedestal separated (was 2.6m authored, floor 2.0)");
    float dApple = Vector3.Distance(crate, MarketBuilder.CrateAnchorPos);
    Assert.Greater(dApple, 3.0f, "crate vs apple crate separated (distinct quest zones)");
    Vector3 mia = MarketBuilder.MiaAnchorPos;
    float dMia = Vector3.Distance(crate, mia);
    Assert.Greater(dMia, 2.5f, "find requires a real walk (not spawn-adjacent)");
  }
}
