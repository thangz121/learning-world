// CT-P30: player-report round two (small ranges, pickable wrong answers,
// Mia sad-voice retry, distinct quest ball, always-on Mia label data,
// speak-on-click). Pure EditMode: deterministic public-API/event calls only.
// C# 9.0 only.
using NUnit.Framework;
using System;
using System.IO;
using UnityEngine;

public class CT_P30_RoundTwoReports {
  static readonly QuestId AppleQ = new QuestId("w1_mia_apple");
  static readonly QuestId BallQ = new QuestId("w1_mia_ball");
  static readonly WordId Apple = new WordId("apple");
  static readonly WordId Ball = new WordId("ball");

  sealed class Ctx {
    public GameEventBus Bus;
    public QuestManager Quests;
    public HintService Hints;
    public Ctx() {
      Bus = new GameEventBus();
      var learning = new LearningService(Bus);
      Hints = new HintService(Bus);
      Quests = new QuestManager(Bus, learning, Hints);
    }
  }

  // 1. Tightened ranges (player report: small pickup radius, halved NPC auto).
  [Test] public void P30A_RangesTightened() {
    GameObject go = new GameObject("RangePinTest");
    try {
      var router = go.AddComponent<ClickRouter>();
      Assert.AreEqual(0.75f, router.arrivalRange, 0.001f, "click arrival halved to cheek-to-cheek");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // 2. Wrong apple pickable during the BALL quest (answers differ by bring,
  // never by pickup refusal); still nothing pre-quest.
  [Test] public void P30B_WrongApplePickableInBallQuest() {
    var ctx = new Ctx();
    GameObject presenterGo = new GameObject("AppleBallQuestTest");
    GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
    try {
      var presenter = presenterGo.AddComponent<ApplePresenter>();
      presenter.Bind(ctx.Bus);
      presenter.SetCrateApple(crate);
      ctx.Bus.Publish(new WordSeenEvent(Apple, LearnSource.Object, DateTime.UtcNow));
      Assert.IsTrue(crate.activeSelf, "pre-quest tap picks nothing (rule kept)");
      Assert.IsNull(GameObject.Find("CarriedApple"), "pre-quest tap rides nothing");
      ctx.Quests.StartQuest(BallQ); // ball quest: apple is the WRONG answer
      ctx.Bus.Publish(new WordSeenEvent(Apple, LearnSource.Object, DateTime.UtcNow));
      Assert.IsFalse(crate.activeSelf, "in-quest find empties the crate even for the wrong item");
      Assert.IsNotNull(GameObject.Find("CarriedApple"), "wrong item rides the hand (bring decides)");
    } finally {
      UnityEngine.Object.DestroyImmediate(presenterGo);
      UnityEngine.Object.DestroyImmediate(crate);
      GameObject decoy = GameObject.Find("CarriedApple");
      if (decoy != null) UnityEngine.Object.DestroyImmediate(decoy);
    }
  }

  // 3. Wrong bring: Mia speaks retry, quest stays open, exactly one wrong.
  [Test] public void P30C_MiaWrongBringRetry() {
    var ctx = new Ctx();
    GameObject miaGo = new GameObject("MiaRetryTest");
    StoryMomentEvent? story = null;
    try {
      var mia = miaGo.AddComponent<MiaPresenter>();
      mia.Bind(ctx.Bus, ctx.Quests, ctx.Hints);
      ctx.Bus.Subscribe<StoryMomentEvent>(e => story = e);
      ctx.Bus.Subscribe<WordSeenEvent>(e => ctx.Quests.AdvanceOnSeen(e.WordId));
      ctx.Quests.StartQuest(AppleQ);
      ctx.Bus.Publish(new WordSeenEvent(Ball, LearnSource.Object, DateTime.UtcNow)); // wrong item in hand
      Assert.IsTrue(mia.IsCarryingBall, "setup: wrong item armed");
      mia.OnMiaClicked(); // bring the wrong thing to Mia
      Assert.IsFalse(ctx.Quests.GetState(AppleQ).Completed, "wrong bring must never complete");
      Assert.AreEqual(1, ctx.Hints.GetState(AppleQ).WrongCount, "exactly one wrong per bring");
      Assert.IsTrue(story.HasValue, "wrong bring must publish a moment");
      Assert.AreEqual(StoryMoment.WrongChoice, story.Value.Moment, "sad moment (attitude) fires");
      Assert.DoesNotThrow(() => Mia.SayRetry(), "retry voice never throws (unbound-safe)");
      Assert.DoesNotThrow(() => Mia.SayName(), "name readout never throws (unbound-safe)");
    } finally {
      UnityEngine.Object.DestroyImmediate(miaGo);
    }
  }

  // 4. Mia voice mirrors pin: code speaks exact manifest texts (CT-P10 pattern).
  [Test] public void P30D_MiaVoiceManifestMirrors() {
    string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Content");
    DialoguePack pack = ContentDatabase.ParseDialoguePack(
      File.ReadAllText(Path.Combine(root, "dialogues", "manifest.json")));
    DialogueLine name = pack.lines.Find(l => l.id == "mia_01");
    DialogueLine retry = pack.lines.Find(l => l.id == "retry_03");
    Assert.IsNotNull(name, "mia_01 must exist");
    Assert.IsNotNull(retry, "retry_03 must exist");
    Assert.AreEqual("I am Mia!", name.text, "mia_01 text is the SayName mirror source");
    Assert.AreEqual("Try again please!", retry.text, "retry_03 text is the SayRetry mirror source");
    Assert.AreEqual("mia_v1", name.voice, "name readout is Mia-voiced");
    Assert.AreEqual("mia_v1", retry.voice, "retry is Mia-voiced");
  }

  // 5. Quest ball carried visual: orange + band (never distractor blue).
  [Test] public void P30E_CarriedQuestBallDistinct() {
    var ctx = new Ctx();
    GameObject presenterGo = new GameObject("QuestBallMiniTest");
    GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
    try {
      var presenter = presenterGo.AddComponent<BallPresenter>();
      presenter.Bind(ctx.Bus);
      presenter.SetCrateBall(crate);
      ctx.Quests.StartQuest(BallQ);
      ctx.Bus.Publish(new WordSeenEvent(Ball, LearnSource.Object, DateTime.UtcNow));
      GameObject mini = GameObject.Find("CarriedQuestBall");
      Assert.IsNotNull(mini, "wrong-item pickup rides the hand");
      Color c = mini.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
      Assert.Greater(c.r, 0.9f, "carried quest ball is orange");
      Assert.Less(c.b, 0.3f, "carried quest ball is orange, never distractor blue");
      Transform band = mini.transform.Find("CarriedQuestBallBand");
      Assert.IsNotNull(band, "carried quest ball keeps its white band");
      Assert.AreEqual(0, mini.GetComponentsInChildren<Collider>(true).Length,
        "carried decoy must never eat clicks");
    } finally {
      UnityEngine.Object.DestroyImmediate(presenterGo);
      UnityEngine.Object.DestroyImmediate(crate);
      GameObject mini = GameObject.Find("CarriedQuestBall");
      if (mini != null) UnityEngine.Object.DestroyImmediate(mini);
      GameObject band = GameObject.Find("CarriedQuestBallBand");
      if (band != null) UnityEngine.Object.DestroyImmediate(band);
    }
  }

  // 6. Distractor speaks its word (player rule: every clickable reads out).
  [Test] public void P30F_DistractorSpeaksBall() {
    GameObject go = new GameObject("DistractorSpeakTest");
    try {
      var ball = go.AddComponent<DistractorChoice>();
      Assert.AreEqual("ball", ball.SpeakWord.Value, "distractor reads its own word");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // 7. Arrival is horizontal-only (tight ranges must still trip when the click
  // point rides ~1m up the body while feet stay on the grass).
  [Test] public void P30G_ArrivalIgnoresHeight() {
    Vector3 feet = new Vector3(0.3f, 0.1f, 0.2f);
    Vector3 highClick = new Vector3(0f, 1.2f, 0f); // 3D gap ~1.3m, XZ gap ~0.36m
    Assert.IsTrue(ClickRouter.InArrivalRange(feet, highClick, 0.75f),
      "cheek-to-cheek arrival must fire despite the body-height gap");
    Assert.IsFalse(ClickRouter.InArrivalRange(new Vector3(2f, 0.1f, 0f), highClick, 0.75f),
      "far XZ must stay silent");
    Assert.IsFalse(ClickRouter.InArrivalRange(feet, new Vector3(0f, 1.2f, 2f), 0.75f),
      "far XZ must stay silent even with matching height");
  }
}
