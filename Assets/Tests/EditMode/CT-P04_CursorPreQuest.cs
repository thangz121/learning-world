// CT-P04: pre-talk quest robustness (R7 softlock) + software cursor contract.
// A child can click the apple BEFORE talking to Milo (nothing blocks it):
//   - the crate must STAY visible/clickable pre-quest (else find_apple is
//     unrecoverable after Talk),
//   - the in-quest find still empties the crate (R6 lifecycle),
//   - Mia must not consume carrying / count wrongs / publish story pre-talk,
//   - the normal talk -> find -> bring path completes at unit level.
// Cursor: hover language values + an overlay that can never eat clicks.
// Pure EditMode: deterministic public-API/event calls only. C# 9.0 only.
using NUnit.Framework;
using System;
using UnityEngine;
using UnityEngine.UI;

public class CT_P04_CursorPreQuest {
  static readonly QuestId W1 = new QuestId("w1_mia_apple");
  static readonly WordId Apple = new WordId("apple");

  sealed class Ctx {
    public GameEventBus Bus;
    public QuestManager Quests;
    public HintService Hints;
    public Ctx() {
      Bus = new GameEventBus();
      var learning = new LearningService(Bus);
      Hints = new HintService(Bus);
      Quests = new QuestManager(Bus, learning, Hints); // default provider: find -> bring
    }
  }

  static void PublishSeen(GameEventBus bus) {
    bus.Publish(new WordSeenEvent(Apple, LearnSource.Object, DateTime.UtcNow));
  }

  static void DestroyCarriedDecoy() {
    GameObject decoy = GameObject.Find("CarriedApple");
    if (decoy != null) UnityEngine.Object.DestroyImmediate(decoy);
  }

  // 1. Pre-talk find keeps the crate (recovery stays possible after Talk).
  [Test] public void CT_P04A_PreQuestFindKeepsCrate() {
    var ctx = new Ctx();
    GameObject presenterGo = new GameObject("ApplePresenterPreTest");
    GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
    try {
      var presenter = presenterGo.AddComponent<ApplePresenter>();
      presenter.Bind(ctx.Bus);
      presenter.SetCrateApple(crate);
      PublishSeen(ctx.Bus); // NO StartQuest: the pre-talk tap
      Assert.IsTrue(crate.activeSelf, "pre-talk find must not hide the crate (else find is unrecoverable)");
      Assert.IsFalse(ctx.Quests.GetState(W1).Completed);
      Assert.AreEqual(0, ctx.Quests.GetState(W1).ObjectiveIndex);
    } finally {
      UnityEngine.Object.DestroyImmediate(presenterGo);
      UnityEngine.Object.DestroyImmediate(crate);
      DestroyCarriedDecoy();
    }
  }

  // 2. In-quest find still empties the crate (R6 lifecycle intact).
  [Test] public void CT_P04B_InQuestFindHidesCrate() {
    var ctx = new Ctx();
    GameObject presenterGo = new GameObject("ApplePresenterInTest");
    GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
    try {
      var presenter = presenterGo.AddComponent<ApplePresenter>();
      presenter.Bind(ctx.Bus);
      presenter.SetCrateApple(crate);
      ctx.Quests.StartQuest(W1);
      PublishSeen(ctx.Bus);
      Assert.IsFalse(crate.activeSelf, "in-quest find must empty the crate");
    } finally {
      UnityEngine.Object.DestroyImmediate(presenterGo);
      UnityEngine.Object.DestroyImmediate(crate);
      DestroyCarriedDecoy();
    }
  }

  // 3. Pre-talk Mia click is wave-only: carrying kept, no wrongs, no story.
  [Test] public void CT_P04C_MiaIgnoresBringPreQuest() {
    var ctx = new Ctx();
    GameObject miaGo = new GameObject("MiaPreTest");
    StoryMomentEvent? story = null;
    try {
      var mia = miaGo.AddComponent<MiaPresenter>();
      Assert.IsNotNull(mia, "Mia presenter must build in EditMode");
      mia.Bind(ctx.Bus, ctx.Quests, ctx.Hints);
      ctx.Bus.Subscribe<StoryMomentEvent>(e => story = e);
      PublishSeen(ctx.Bus); // pre-talk apple tap arms carrying
      Assert.IsTrue(mia.IsCarrying, "WordSeen(apple) arms carrying even pre-talk");
      mia.OnMiaClicked(); // pre-talk tap on Mia
      Assert.IsTrue(mia.IsCarrying, "pre-talk Mia click must not consume the apple");
      Assert.IsFalse(ctx.Quests.GetState(W1).Completed);
      Assert.AreEqual(0, ctx.Hints.GetState(W1).WrongCount, "pre-talk taps must not count wrongs");
      Assert.IsFalse(story.HasValue, "pre-talk Mia click must publish no story moment");
    } finally {
      UnityEngine.Object.DestroyImmediate(miaGo);
    }
  }

  // 4. Normal path still completes at unit level (talk -> find -> bring).
  [Test] public void CT_P04D_TalkFindBringCompletes() {
    var ctx = new Ctx();
    GameObject miaGo = new GameObject("MiaPathTest");
    StoryMomentEvent? story = null;
    try {
      var mia = miaGo.AddComponent<MiaPresenter>();
      mia.Bind(ctx.Bus, ctx.Quests, ctx.Hints);
      ctx.Bus.Subscribe<StoryMomentEvent>(e => story = e);
      // Production glue lives in MarketBootstrap.Build (tests drive Advance*
      // directly everywhere else): WordSeen advances the Find objective.
      ctx.Bus.Subscribe<WordSeenEvent>(e => ctx.Quests.AdvanceOnSeen(e.WordId));
      ctx.Quests.StartQuest(W1); // Talk to Milo
      Assert.IsFalse(mia.IsCarrying, "quest start resets carrying");
      PublishSeen(ctx.Bus); // find the apple
      Assert.AreEqual(1, ctx.Quests.GetState(W1).ObjectiveIndex);
      mia.OnMiaClicked(); // bring it to Mia
      Assert.IsTrue(ctx.Quests.GetState(W1).Completed, "bring while carrying must complete w1_mia_apple");
      Assert.IsTrue(story.HasValue, "bring must publish the CorrectChoice story moment");
      Assert.AreEqual(StoryMoment.CorrectChoice, story.Value.Moment);
    } finally {
      UnityEngine.Object.DestroyImmediate(miaGo);
    }
  }

  // 5. Cursor hover language values (gold + bigger on hover, plain otherwise).
  [Test] public void CT_P04E_CursorHoverLanguage() {
    CursorPresenter.ComputeCursor(false, out float idleScale, out Color idleColor);
    Assert.AreEqual(CursorPresenter.IdleScale, idleScale, 0.001f);
    Assert.AreEqual(CursorPresenter.IdleColor, idleColor);
    CursorPresenter.ComputeCursor(true, out float hoverScale, out Color hoverColor);
    Assert.AreEqual(CursorPresenter.HoverScale, hoverScale, 0.001f);
    Assert.Greater(hoverScale, idleScale, "hover must grow the cursor");
    Assert.AreEqual(CursorPresenter.HoverColor, hoverColor);
  }

  // 6. Cursor overlay is topmost yet can never eat world clicks.
  [Test] public void CT_P04F_CursorOverlayNeverEatsClicks() {
    GameObject go = new GameObject("CursorSafetyTest");
    try {
      var cursor = go.AddComponent<CursorPresenter>();
      cursor.BuildCursorImmediate();
      Canvas canvas = go.GetComponentInChildren<Canvas>(true);
      Assert.IsNotNull(canvas, "cursor must build its overlay canvas");
      Assert.AreEqual(CursorPresenter.CanvasOrder, canvas.sortingOrder, "cursor stays above the HUD");
      Assert.IsNull(go.GetComponentInChildren<GraphicRaycaster>(true), "no raycaster: overlay never intercepts");
      Image[] images = go.GetComponentsInChildren<Image>(true);
      Assert.Greater(images.Length, 0, "cursor must render its arrow");
      foreach (Image img in images)
        Assert.IsFalse(img.raycastTarget, "cursor graphics must never block clicks");
      Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
      Assert.AreEqual(0, colliders.Length, "cursor must never eat physics clicks");
      cursor.RefreshForTests(new Vector2(100f, 200f), true); // must not throw
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // 7. Hover marker contract (R9: one reusable gold DOWN-ARROW — shaft +
  // chevron head — collider-free, hidden until placed (a marker that eats
  // clicks would blind its own ray). R8 was an exclamation; the arrow says
  // WHERE to tap, not just "look here".)
  [Test] public void CT_P04G_HoverMarkerContract() {
    GameObject go = new GameObject("MarkerContractTest");
    try {
      var cursor = go.AddComponent<CursorPresenter>();
      cursor.BuildCursorImmediate();
      Assert.IsFalse(cursor.IsMarkerVisible, "marker starts hidden (idle pointer)");
      Transform marker = go.transform.Find("HoverMarker");
      Assert.IsNotNull(marker, "must own exactly one reusable marker");
      Assert.IsNotNull(marker.Find("MarkShaft"), "down-arrow needs its shaft");
      Assert.IsNotNull(marker.Find("MarkHeadL"), "down-arrow needs its left chevron arm");
      Assert.IsNotNull(marker.Find("MarkHeadR"), "down-arrow needs its right chevron arm");
      Assert.AreEqual(0, marker.GetComponentsInChildren<Collider>(true).Length,
        "marker must never intercept the hover/click raycast");
      Renderer[] rends = marker.GetComponentsInChildren<Renderer>(true);
      Assert.GreaterOrEqual(rends.Length, 3, "marker must render shaft + 2 chevron arms");
      foreach (Renderer r in rends)
        Assert.IsNotNull(r.sharedMaterial, "marker parts must carry a material (never default-white)");
      cursor.PlaceMarkerForTests(new Vector3(0f, 2f, 0f));
      Assert.IsTrue(cursor.IsMarkerVisible, "placed marker must show");
      Assert.AreEqual(2f, marker.position.y, 0.001f, "marker must sit at its anchor");
      cursor.RefreshForTests(new Vector2(0f, 0f), false);
      Assert.IsFalse(cursor.IsMarkerVisible, "leaving hover must hide the marker");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // 8. Tightened bring radius (R8): 1.5m completes, just outside stays silent.
  [Test] public void CT_P04H_ProximityBoundary() {
    var ctx = new Ctx();
    GameObject miaGo = new GameObject("MiaBoundaryTest");
    try {
      var mia = miaGo.AddComponent<MiaPresenter>();
      mia.Bind(ctx.Bus, ctx.Quests, ctx.Hints);
      ctx.Bus.Subscribe<WordSeenEvent>(e => ctx.Quests.AdvanceOnSeen(e.WordId));
      ctx.Quests.StartQuest(W1);
      PublishSeen(ctx.Bus);
      Assert.IsTrue(mia.IsCarrying, "setup: carrying");
      // Mia root sits at origin in this fixture (SpawnPosition applies at
      // Start, which EditMode never runs): measure from the live root.
      Vector3 root = miaGo.transform.position;
      mia.TryProximityBring(root + new Vector3(1.2f, 0f, 1.0f)); // ~1.56m: outside
      Assert.IsFalse(ctx.Quests.GetState(W1).Completed, "1.56m must stay silent (bring is a handover, not a shout)");
      Assert.IsTrue(mia.IsCarrying, "carrying must survive (retry preserved)");
      mia.TryProximityBring(root + new Vector3(0.9f, 0f, 0.9f)); // ~1.27m: inside
      Assert.IsTrue(ctx.Quests.GetState(W1).Completed, "1.27m must complete the handover");
    } finally {
      UnityEngine.Object.DestroyImmediate(miaGo);
    }
  }
}
