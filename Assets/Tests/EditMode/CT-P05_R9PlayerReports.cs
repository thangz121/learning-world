// CT-P05: R9 player-report contracts (pickable distractor, bigger hint
// bubble + placement rule, heading cursor + down-arrow marker).
// Rule under test — pickup is neutral, the BRING decides: an in-quest ball
// tap picks the ball up (quest open, 0 wrongs); carrying it to Mia (click or
// proximity) is wrong + the ball hops home; seeing the apple swaps back.
// Pure EditMode: deterministic public-API/event calls only. C# 9.0 only.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class CT_P05_R9PlayerReports {
  static readonly QuestId W1 = new QuestId("w1_mia_apple");
  static readonly WordId Apple = new WordId("apple");
  static readonly WordId Ball = new WordId("ball");

  sealed class Ctx {
    public GameEventBus Bus;
    public HintService Hints;
    public QuestManager Quests;
    public List<StoryMomentEvent> Moments;
    public List<WordSeenEvent> Seen;
    public Ctx() {
      Bus = new GameEventBus();
      var learning = new LearningService(Bus);
      Hints = new HintService(Bus);
      Quests = new QuestManager(Bus, learning, Hints); // default provider: find -> bring
      Moments = new List<StoryMomentEvent>();
      Seen = new List<WordSeenEvent>();
      Bus.Subscribe<StoryMomentEvent>(e => Moments.Add(e));
      Bus.Subscribe<WordSeenEvent>(e => Seen.Add(e));
    }
  }

  static DistractorChoice NewBall(Ctx ctx) {
    GameObject go = new GameObject("BallR9Test");
    DistractorChoice ball = go.AddComponent<DistractorChoice>();
    ball.Bind(ctx.Bus, ctx.Hints, ctx.Quests);
    return ball;
  }

  static MiaPresenter NewMia(Ctx ctx) {
    GameObject go = new GameObject("MiaR9Test");
    MiaPresenter mia = go.AddComponent<MiaPresenter>();
    mia.Bind(ctx.Bus, ctx.Quests, ctx.Hints);
    // Production glue lives in MarketBootstrap.Build: seen advances find.
    ctx.Bus.Subscribe<WordSeenEvent>(e => ctx.Quests.AdvanceOnSeen(e.WordId));
    return mia;
  }

  static void DestroyMinis() {
    // The carried mini outlives its ball (separate root object, hidden on
    // restore — GameObject.Find only sees ACTIVE objects, so sweep hidden
    // ones too; otherwise minis leak across tests).
    foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>()) {
      if (go != null && go.name == "CarriedBall") UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // 1. Pickup is neutral: quest open, 0 wrongs, 0 story, ball in hand.
  [Test] public void CT_P05A_PickupIsNeutral() {
    var ctx = new Ctx();
    DistractorChoice ball = NewBall(ctx);
    try {
      ctx.Quests.StartQuest(W1);
      ball.OnClicked();
      Assert.IsTrue(ball.IsCarrying, "in-quest tap with empty hands must pick the ball up");
      Assert.IsFalse(ball.IsBallHome, "picked ball must leave the pedestal");
      Assert.AreEqual(1, ctx.Seen.Count, "pickup must publish exactly one seen event");
      Assert.AreEqual(Ball.Value, ctx.Seen[0].WordId.Value, "pickup must announce the ball");
      Assert.AreEqual(0, ctx.Hints.GetState(W1).WrongCount, "pickup must not count a wrong");
      Assert.AreEqual(0, ctx.Moments.Count, "pickup must publish no story moment");
      Assert.IsFalse(ctx.Quests.GetState(W1).Completed, "pickup must never complete the quest");
    } finally {
      UnityEngine.Object.DestroyImmediate(ball.gameObject);
      DestroyMinis();
    }
  }

  // 2. Bringing the ball to Mia is wrong: ladder + moment, quest open, ball home.
  [Test] public void CT_P05B_BallBringIsWrongAndRestores() {
    var ctx = new Ctx();
    DistractorChoice ball = NewBall(ctx);
    MiaPresenter mia = NewMia(ctx);
    try {
      ctx.Quests.StartQuest(W1);
      ball.OnClicked(); // pick up
      Assert.IsTrue(mia.IsCarryingBall, "Mia must hold the wrong-item context after the pickup");
      mia.OnMiaClicked(); // bring the wrong item to the counter
      Assert.AreEqual(1, ctx.Hints.GetState(W1).WrongCount, "wrong bring must feed the hint ladder");
      Assert.AreEqual(1, ctx.Moments.Count, "wrong bring must emit exactly one moment");
      Assert.AreEqual(StoryMoment.WrongChoice, ctx.Moments[0].Moment, "wrong bring must emit WrongChoice");
      Assert.IsFalse(ctx.Quests.GetState(W1).Completed, "wrong bring must never complete the quest");
      Assert.IsFalse(mia.IsCarryingBall, "wrong bring must clear Mia's hands");
      Assert.IsFalse(ball.IsCarrying, "wrong bring must clear the ball carry");
      Assert.IsTrue(ball.IsBallHome, "the ball must hop home (retry preserved)");
    } finally {
      UnityEngine.Object.DestroyImmediate(ball.gameObject);
      UnityEngine.Object.DestroyImmediate(mia.gameObject);
      DestroyMinis();
    }
  }

  // 3. Seeing the apple swaps: ball home, apple context wins the hands.
  [Test] public void CT_P05C_AppleSeenSwapsBallHome() {
    var ctx = new Ctx();
    DistractorChoice ball = NewBall(ctx);
    MiaPresenter mia = NewMia(ctx);
    try {
      ctx.Quests.StartQuest(W1);
      ball.OnClicked();
      Assert.IsTrue(ball.IsCarrying, "setup: ball in hand");
      ctx.Bus.Publish(new WordSeenEvent(Apple, LearnSource.Object, DateTime.UtcNow));
      Assert.IsTrue(ball.IsBallHome, "apple seen must send the ball home");
      Assert.IsFalse(ball.IsCarrying, "swap must clear the ball carry");
      Assert.IsTrue(mia.IsCarrying, "apple must arm the bring context");
      Assert.IsFalse(mia.IsCarryingBall, "apple must win the hands over the ball");
    } finally {
      UnityEngine.Object.DestroyImmediate(ball.gameObject);
      UnityEngine.Object.DestroyImmediate(mia.gameObject);
      DestroyMinis();
    }
  }

  // 4. Hands full of the apple: ball taps stay legacy instant-wrong, no pickup.
  [Test] public void CT_P05D_HandsFullStaysLegacyWrong() {
    var ctx = new Ctx();
    DistractorChoice ball = NewBall(ctx);
    MiaPresenter mia = NewMia(ctx);
    try {
      ctx.Quests.StartQuest(W1);
      ctx.Bus.Publish(new WordSeenEvent(Apple, LearnSource.Object, DateTime.UtcNow));
      Assert.IsTrue(mia.IsCarrying, "setup: apple in hand");
      ball.OnClicked();
      Assert.IsFalse(ball.IsCarrying, "hands-full tap must not pick the ball up");
      Assert.IsTrue(ball.IsBallHome, "ball must stay on the pedestal");
      Assert.AreEqual(1, ctx.Hints.GetState(W1).WrongCount, "hands-full tap is a genuine mistake");
      Assert.AreEqual(1, ctx.Moments.Count, "exactly one WrongChoice moment");
    } finally {
      UnityEngine.Object.DestroyImmediate(ball.gameObject);
      UnityEngine.Object.DestroyImmediate(mia.gameObject);
      DestroyMinis();
    }
  }

  // 5. Walking the ball up to Mia is a bring attempt too (same 1.5m radius);
  // empty-handed wandering stays silent (P1 closure contract intact).
  [Test] public void CT_P05E_ProximityBallBringIsWrong() {
    var ctx = new Ctx();
    DistractorChoice ball = NewBall(ctx);
    MiaPresenter mia = NewMia(ctx);
    try {
      ctx.Quests.StartQuest(W1);
      Vector3 root = mia.gameObject.transform.position;
      mia.TryProximityBring(root + new Vector3(0.9f, 0f, 0.9f)); // ~1.27m, empty hands
      Assert.AreEqual(0, ctx.Hints.GetState(W1).WrongCount, "empty-handed proximity must stay silent");
      ball.OnClicked(); // pick up
      mia.TryProximityBring(root + new Vector3(0.9f, 0f, 0.9f)); // ~1.27m, ball in hand
      Assert.AreEqual(1, ctx.Hints.GetState(W1).WrongCount, "ball proximity bring must count a wrong");
      Assert.IsFalse(ctx.Quests.GetState(W1).Completed, "wrong bring must never complete");
      Assert.IsTrue(ball.IsBallHome, "the ball must hop home");
    } finally {
      UnityEngine.Object.DestroyImmediate(ball.gameObject);
      UnityEngine.Object.DestroyImmediate(mia.gameObject);
      DestroyMinis();
    }
  }

  // 6. Hint-placement contract: every NPC parks its bubble east-south at head
  // height via ONE rule; the R9 enlargement is pinned (instant readability).
  [Test] public void CT_P05F_BubbleAnchorContract() {
    Vector3 anchor = WorldQuestionBubble.AnchorFor(MarketBuilder.MiaAnchorPos);
    Assert.AreEqual(MarketBuilder.MiaAnchorPos.x + 1.45f, anchor.x, 0.001f, "bubble parks east of the asker");
    Assert.AreEqual(1.78f, anchor.y, 0.001f, "bubble rides at head height");
    Assert.AreEqual(MarketBuilder.MiaAnchorPos.z + 0.55f, anchor.z, 0.001f, "bubble sits south of the asker");
    GameObject go = new GameObject("BubbleR9Test");
    try {
      WorldQuestionBubble bubble = go.AddComponent<WorldQuestionBubble>();
      bubble.BuildBubbleImmediate();
      Transform outline = go.transform.Find("BubbleShell/ShellOutline");
      Assert.IsNotNull(outline, "must keep the outline");
      Assert.AreEqual(0.95f, outline.localScale.x, 0.001f, "R9 outline width pins the enlargement");
      Assert.AreEqual(0.77f, outline.localScale.y, 0.001f, "R9 outline height pins the enlargement");
      Transform appleIcon = go.transform.Find("AskIcon/AskApple");
      Assert.IsNotNull(appleIcon, "must keep the apple icon");
      Assert.AreEqual(0.30f, appleIcon.localScale.x, 0.001f, "R9 icon size pins the enlargement");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // 7. Heading cue: pure screen-space lean (clockwise from straight-up).
  [Test] public void CT_P05G_CursorHeadingAngle() {
    Vector2 o = Vector2.zero;
    Assert.AreEqual(0f, CursorPresenter.ComputeArrowAngle(o, new Vector2(0f, 1f)), 0.001f, "facing up reads straight");
    Assert.AreEqual(90f, CursorPresenter.ComputeArrowAngle(o, new Vector2(1f, 0f)), 0.001f, "facing right leans 90");
    Assert.AreEqual(-90f, CursorPresenter.ComputeArrowAngle(o, new Vector2(-1f, 0f)), 0.001f, "facing left leans -90");
    float down = CursorPresenter.ComputeArrowAngle(o, new Vector2(0f, -1f));
    Assert.AreEqual(180f, Math.Abs(down), 0.001f, "facing down flips the arrow");
    Assert.AreEqual(0f, CursorPresenter.ComputeArrowAngle(o, o), 0.001f, "degenerate facing reads straight, never NaN");
    // Smoothing keeps the render contract: every result stays inside
    // [-180,180] (unnormalized LerpAngle output photographed mirrored).
    Assert.AreEqual(94f, CursorPresenter.SmoothAngle(389.1f, 94f, 1f), 0.001f, "full step lands exactly");
    Assert.AreEqual(-94f, CursorPresenter.SmoothAngle(266f, -94f, 1f), 0.001f, "full step lands exactly (negative)");
    float partial = CursorPresenter.SmoothAngle(389.1f, 94f, 0.18f);
    Assert.GreaterOrEqual(partial, -180f, "partial steps never escape the render range");
    Assert.LessOrEqual(partial, 180f, "partial steps never escape the render range");
  }

  // 8. Full R9 loop at unit level: pick ball -> wrong bring -> swap apple ->
  // correct bring completes (difficulty up, path intact).
  [Test] public void CT_P05H_FullLoopWithWrongItem() {
    var ctx = new Ctx();
    DistractorChoice ball = NewBall(ctx);
    MiaPresenter mia = NewMia(ctx);
    try {
      ctx.Quests.StartQuest(W1);
      ball.OnClicked(); // explorative pickup (neutral)
      Assert.AreEqual(0, ctx.Hints.GetState(W1).WrongCount, "pickup stays neutral");
      mia.OnMiaClicked(); // wrong item at the counter
      Assert.AreEqual(1, ctx.Hints.GetState(W1).WrongCount, "wrong bring counts");
      Assert.IsTrue(ball.IsBallHome, "ball hops home for the retry");
      ctx.Bus.Publish(new WordSeenEvent(Apple, LearnSource.Object, DateTime.UtcNow)); // real find
      Assert.AreEqual(1, ctx.Quests.GetState(W1).ObjectiveIndex, "find must advance first");
      mia.OnMiaClicked(); // correct bring
      Assert.IsTrue(ctx.Quests.GetState(W1).Completed, "correct bring still completes the quest");
    } finally {
      UnityEngine.Object.DestroyImmediate(ball.gameObject);
      UnityEngine.Object.DestroyImmediate(mia.gameObject);
      DestroyMinis();
    }
  }

  // 9. Echo guard: the trailing tap after a resolved bring is silent (one
  // bring = one wrong, even when click arrival AND proximity both resolve).
  [Test] public void CT_P05I_TrailingTapAfterWrongBringIsSilent() {
    var ctx = new Ctx();
    DistractorChoice ball = NewBall(ctx);
    MiaPresenter mia = NewMia(ctx);
    try {
      ctx.Quests.StartQuest(W1);
      ball.OnClicked(); // pick up
      mia.OnMiaClicked(); // wrong bring (proximity-equivalent resolution)
      Assert.AreEqual(1, ctx.Hints.GetState(W1).WrongCount, "setup: one wrong");
      mia.OnMiaClicked(); // trailing arrival tap, hands empty now: echo
      Assert.AreEqual(1, ctx.Hints.GetState(W1).WrongCount, "echo tap must stay silent");
      Assert.AreEqual(1, ctx.Moments.Count, "echo tap must publish no moment");
      mia.OnMiaClicked(); // a genuinely NEW empty-handed tap still counts
      Assert.AreEqual(2, ctx.Hints.GetState(W1).WrongCount, "new intent must count again");
      Assert.IsFalse(ctx.Quests.GetState(W1).Completed, "echoes must never complete");
    } finally {
      UnityEngine.Object.DestroyImmediate(ball.gameObject);
      UnityEngine.Object.DestroyImmediate(mia.gameObject);
      DestroyMinis();
    }
  }
}
