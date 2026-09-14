// CT-P02: bring-to-Mia proximity contract (Phase-1 closure). Guards the real
// manual playtest failure "walked the apple to Mia, nothing completed":
// the click ray can be eaten by the awning/counter, so arrival-by-proximity
// while carrying must complete the bring through the SAME path as a click
// (idempotent: first of click/proximity wins, the other goes inert).
// Empty-handed proximity stays silent (never a wrong).
// Pure EditMode: deterministic public-API calls only, no timing. C# 9.0 only.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class CT_P02_ProximityBring {
  static readonly QuestId W1Quest = new QuestId("w1_mia_apple");
  static readonly WordId Apple = new WordId("apple");

  sealed class Fixture {
    public GameEventBus Bus;
    public LearningService Learning;
    public HintService Hints;
    public QuestManager Quests;
    public List<StoryMomentEvent> Moments;
    public Fixture() {
      Bus = new GameEventBus();
      Learning = new LearningService(Bus);
      Hints = new HintService(Bus);
      Quests = new QuestManager(Bus, Learning, Hints); // built-in w1_mia_apple: find -> bring
      Moments = new List<StoryMomentEvent>();
      Bus.Subscribe<StoryMomentEvent>(e => Moments.Add(e));
    }
    // Live-slice arming: seen (as MarketBootstrap routes it) advances find.
    public void ArmCarrying(MiaPresenter mia) {
      Quests.StartQuest(W1Quest);
      Bus.Publish(new WordSeenEvent(Apple, LearnSource.Object, DateTime.UtcNow));
      Quests.AdvanceOnSeen(Apple);
    }
  }

  static MiaPresenter BuildMia(Fixture f, out GameObject go) {
    go = new GameObject("MiaProximityTest");
    go.transform.position = new Vector3(-3.5f, 0f, -2.5f);
    MiaPresenter mia = go.AddComponent<MiaPresenter>();
    mia.Bind(f.Bus, f.Quests, f.Hints);
    return mia;
  }

  // A. Carrying + in range completes the bring with a Correct moment.
  [Test] public void CT_P02A_ProximityWhileCarryingCompletes() {
    var f = new Fixture();
    GameObject go;
    MiaPresenter mia = BuildMia(f, out go);
    try {
      f.ArmCarrying(mia);
      Assert.AreEqual(1, f.Quests.GetState(W1Quest).ObjectiveIndex, "setup: find must advance first");
      Assert.IsTrue(mia.IsCarrying, "setup: Mia must hold the apple context");
      mia.TryProximityBring(new Vector3(-2.6f, 0f, -1.6f)); // ~1.27m: inside 1.5m (R8 tightened from 1.8)
      Assert.IsTrue(f.Quests.GetState(W1Quest).Completed, "proximity while carrying must complete the bring");
      Assert.AreEqual(1, f.Moments.Count, "must emit exactly one moment");
      Assert.AreEqual(StoryMoment.CorrectChoice, f.Moments[0].Moment);
      Assert.IsFalse(mia.IsCarrying, "carrying must clear so the path cannot double-fire");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // B. Empty-handed proximity is silent (never a wrong, never a moment).
  [Test] public void CT_P02B_EmptyHandedProximitySilent() {
    var f = new Fixture();
    GameObject go;
    MiaPresenter mia = BuildMia(f, out go);
    try {
      f.Quests.StartQuest(W1Quest);
      Assert.IsFalse(mia.IsCarrying, "setup: nothing seen yet");
      mia.TryProximityBring(new Vector3(-3.4f, 0f, -2.4f)); // touching distance
      Assert.IsFalse(f.Quests.GetState(W1Quest).Completed, "must not complete empty-handed");
      Assert.AreEqual(0, f.Moments.Count, "must emit no moment");
      Assert.AreEqual(0, f.Hints.GetState(W1Quest).WrongCount, "must not count a wrong");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // C. Carrying but out of range stays silent (the quest waits for arrival).
  [Test] public void CT_P02C_OutOfRangeSilent() {
    var f = new Fixture();
    GameObject go;
    MiaPresenter mia = BuildMia(f, out go);
    try {
      f.ArmCarrying(mia);
      mia.TryProximityBring(new Vector3(0f, 0f, 4.5f)); // spawn: ~8m away
      Assert.IsFalse(f.Quests.GetState(W1Quest).Completed, "far proximity must not complete");
      Assert.AreEqual(0, f.Moments.Count, "must emit no moment");
      Assert.IsTrue(mia.IsCarrying, "carrying must survive (retry preserved)");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // D. Click + proximity are idempotent: whichever lands first wins.
  [Test] public void CT_P02D_ClickProximityIdempotent() {
    var f = new Fixture();
    GameObject go;
    MiaPresenter mia = BuildMia(f, out go);
    try {
      f.ArmCarrying(mia);
      mia.OnMiaClicked(); // click lands first
      Assert.IsTrue(f.Quests.GetState(W1Quest).Completed, "setup: click completes");
      f.Moments.Clear();
      mia.TryProximityBring(new Vector3(-3.4f, 0f, -2.4f)); // proximity after
      Assert.AreEqual(0, f.Moments.Count, "post-quest proximity must be inert");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // E. Post-quest proximity is inert (no extra moments after completion).
  [Test] public void CT_P02E_PostQuestProximityInert() {
    var f = new Fixture();
    GameObject go;
    MiaPresenter mia = BuildMia(f, out go);
    try {
      f.ArmCarrying(mia);
      mia.TryProximityBring(new Vector3(-3.4f, 0f, -2.4f)); // proximity lands first
      Assert.IsTrue(f.Quests.GetState(W1Quest).Completed, "setup: proximity completes");
      f.Moments.Clear();
      mia.OnMiaClicked(); // click after
      Assert.AreEqual(0, f.Moments.Count, "post-quest click must stay inert");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }
}
