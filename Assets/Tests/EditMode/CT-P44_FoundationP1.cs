// CT-P44: P3.0.1 foundation P1 blockers (P1-1..P1-7). Owner: Lead.
// Targeted, headless, no player/scene/NavMesh: lifecycle machine, gate
// re-arm + transition lock, generic adopt, save replay + persist, answer
// validator, anchor registry, dialog click-through (J1/O3 rule).
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CT_P44_FoundationP1 {
  // ---- P1-1 lifecycle --------------------------------------------------------

  [Test] public void P44A_LifecycleHappyPathAndOwner() {
    var lc = new ActivityLifecycle("math_counting", "MathQuestDirector");
    Assert.AreEqual("math_counting", lc.ActivityId);
    Assert.AreEqual("MathQuestDirector", lc.Owner);
    Assert.AreEqual(ActivityState.Unavailable, lc.State);
    Assert.IsFalse(lc.CanAcceptInput);
    Assert.IsTrue(lc.MarkAvailable("built"));
    Assert.IsFalse(lc.CanAcceptInput);
    Assert.IsTrue(lc.Begin("first talk")); // talk-gated shortcut Available->Active
    Assert.IsTrue(lc.CanAcceptInput);
    Assert.IsTrue(lc.BeginCompleting("bring done"));
    Assert.IsFalse(lc.CanAcceptInput);
    Assert.IsTrue(lc.MarkCompleted("celebrated"));
    Assert.AreEqual(ActivityState.Completed, lc.State);
    Assert.IsFalse(string.IsNullOrEmpty(lc.LastReason));
  }

  [Test] public void P44B_LifecycleRejectsInvalid() {
    var lc = new ActivityLifecycle("x", "o");
    Assert.IsFalse(lc.Begin("no"), "Unavailable->Active refused");
    Assert.IsFalse(lc.MarkCompleted("no"), "Unavailable->Completed refused");
    Assert.IsFalse(lc.Resume("no"), "Resume without Pause refused");
    Assert.IsFalse(lc.Pause("no"), "Pause outside Active refused");
    Assert.IsTrue(lc.MarkAvailable("ok"));
    Assert.IsTrue(lc.BeginEnter("walk in"));
    Assert.IsFalse(lc.BeginEnter("again"), "double enter refused");
    Assert.IsTrue(lc.MarkReady("staged"));
    Assert.IsTrue(lc.Begin("go"));
    Assert.IsFalse(lc.Begin("again"), "double begin refused");
    Assert.IsFalse(lc.MarkAvailable("no"), "Active->Available refused");
  }

  [Test] public void P44C_LifecycleAdoptAndRecovery() {
    var lc = new ActivityLifecycle("m", "d");
    Assert.IsTrue(lc.AdoptCompleted("re-entry"), "fresh re-entry adopts directly");
    Assert.AreEqual(ActivityState.Completed, lc.State);
    Assert.IsTrue(lc.MarkUnavailable("audio interrupted"), "failure from ANY state");
    Assert.AreEqual(ActivityState.Unavailable, lc.State);
    var lc2 = new ActivityLifecycle("m", "d");
    lc2.MarkAvailable("b"); lc2.Begin("t");
    Assert.IsTrue(lc2.Pause("interrupted"));
    Assert.IsFalse(lc2.CanAcceptInput, "paused blocks input");
    Assert.IsTrue(lc2.Resume("back"));
    Assert.IsTrue(lc2.CanAcceptInput);
    Assert.IsTrue(lc2.BeginExit("leave"));
    Assert.AreEqual(ActivityState.Exiting, lc2.State);
    Assert.IsTrue(lc2.MarkExitedAvailable("re-offer"));
  }

  // ---- P1-5 gate re-arm + P1-6 transition lock --------------------------------

  static SubjectGate NewGate(GameObject go, IWorldNavService nav, bool isReturn) {
    go.transform.position = SubjectCatalog.Math.GatePos;
    SubjectGate gate = go.AddComponent<SubjectGate>();
    gate.Bind(nav, SubjectIds.Math, isReturn, null);
    return gate;
  }

  [Test] public void P44D_GateRearmsByDistance() {
    GameObject go = new GameObject("P44Gate");
    try {
      var bus = new GameEventBus();
      var nav = new WorldNavService(bus);
      SubjectGate gate = NewGate(go, nav, false);
      Vector3 at = SubjectCatalog.Math.GatePos;
      Assert.IsTrue(gate.TryFireForTests(at, SubjectIds.Main), "first entry fires");
      Assert.AreEqual(SubjectIds.Math, nav.Current);
      // Landing back inside (return warp onto the mouth) must NOT refire.
      nav.ReturnToMain();
      Assert.IsFalse(gate.TryFireForTests(at, SubjectIds.Main),
        "inside after fire: walk clear first (J4 case)");
      Vector3 clear = at + new Vector3(10f, 0f, 0f);
      Assert.IsFalse(gate.TryFireForTests(clear, SubjectIds.Main),
        "outside re-arms without firing");
      Assert.IsTrue(gate.TryFireForTests(at, SubjectIds.Main), "re-entry fires again");
    } finally { Object.DestroyImmediate(go); }
  }

  [Test] public void P44E_GateFrozenDuringTransition() {
    GameObject go = new GameObject("P44GateLock");
    try {
      var bus = new GameEventBus();
      var nav = new WorldNavService(bus);
      SubjectGate gate = NewGate(go, nav, false);
      var gateLock = new InteractionGate();
      gate.BindGate(gateLock);
      gateLock.BeginTransition("travel:math");
      Assert.IsFalse(gate.TryFireForTests(SubjectCatalog.Math.GatePos, SubjectIds.Main),
        "transition beat freezes firing");
      gateLock.EndTransition("travel:math");
      Assert.IsTrue(gate.TryFireForTests(SubjectCatalog.Math.GatePos, SubjectIds.Main),
        "release resumes firing");
    } finally { Object.DestroyImmediate(go); }
  }

  // ---- P1-6 gate unit ----------------------------------------------------------

  [Test] public void P44J_InteractionGateBlocksWorldOnly() {
    var g = new InteractionGate();
    Assert.IsTrue(g.CanRouteWorld, "fresh gate routes");
    Assert.AreEqual("", g.Blocker());
    g.BeginTransition("travel");
    Assert.IsFalse(g.CanRouteWorld);
    Assert.IsTrue(g.TransitionBusy);
    Assert.IsTrue(g.Blocker().StartsWith("transition:"));
    g.EndTransition("travel");
    Assert.IsTrue(g.CanRouteWorld);
    g.BeginActivity("completing");
    Assert.IsFalse(g.CanRouteWorld, "completion beat freezes world taps");
    Assert.IsTrue(g.ActivityBusy);
    g.EndActivity("completing");
    Assert.IsTrue(g.CanRouteWorld);
    // Dialogs have NO lock side by design (J1 click-through): nothing to assert
    // except that the gate exposes no dialog API — open prompts never block.
  }

  // ---- P1-3 generic adopt -------------------------------------------------------

  sealed class P44FakeAdoptable : IQuestAdoptable {
    public QuestState Seen;
    public void AdoptQuestState(QuestState state) { Seen = state; }
  }

  [Test] public void P44F_AdoptAllPushesLiveState() {
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var quests = new QuestManager(bus, learning, hints);
    bus.Subscribe<WordSeenEvent>(e => quests.AdvanceOnSeen(e.WordId));
    var fake = new P44FakeAdoptable();
    QuestAdoption.AdoptAll(new IQuestAdoptable[] { fake, null }, quests, new QuestId("math_counting"));
    Assert.IsNotNull(fake.Seen, "fresh quest adopts default state");
    Assert.IsFalse(fake.Seen.Completed);
    quests.StartQuest(new QuestId("math_counting"));
    bus.Publish(new WordSeenEvent(new WordId("one"), LearnSource.Object, System.DateTime.UtcNow));
    var fake2 = new P44FakeAdoptable();
    QuestAdoption.AdoptAll(new IQuestAdoptable[] { fake2 }, quests, new QuestId("math_counting"));
    Assert.AreEqual(1, fake2.Seen.ObjectiveIndex, "mid-quest re-entry adopts progress");
    Assert.IsFalse(fake2.Seen.Completed);
  }

  // ---- P1-4 replay + P1-7 persist -------------------------------------------------

  sealed class P44MemSave : ISaveService {
    public PlayerProgress Stored = new PlayerProgress();
    public void Save(PlayerProgress p) { Stored = p; }
    public PlayerProgress Load() { return Stored; }
  }

  [Test] public void P44G_RestoreIsSilentAndComplete() {
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var quests = new QuestManager(bus, learning, hints);
    var rewards = new QuestRewardService(bus);
    int events = 0;
    bus.Subscribe<QuestCompletedEvent>(_ => events++);
    quests.RestoreCompleted(new List<string> { "math_counting", "nope_unknown", null, "" });
    rewards.RestoreCompleted(new List<string> { "math_counting" });
    QuestState s = quests.GetState(new QuestId("math_counting"));
    Assert.IsTrue(s.Completed, "save-banked quest replays completed");
    Assert.AreEqual(2, s.ObjectiveIndex, "index at objective count");
    Assert.AreEqual(0, events, "replay publishes NOTHING (no re-celebration)");
    Assert.IsTrue(rewards.HasWorldChange("math_bloom"), "ledger restores silently");
    // Live completion of another quest still works after a restore.
    quests.StartQuest(new QuestId("w1_mia_apple"));
    Assert.AreEqual(0, quests.GetState(new QuestId("w1_mia_apple")).ObjectiveIndex);
  }

  [Test] public void P44H_PersistQuestDoneIsIdempotent() {
    var save = new P44MemSave();
    save.Stored.QuestsDone = new List<string>();
    Assert.IsTrue(ActivityCompletion.PersistQuestDone(save, new QuestId("math_counting")));
    Assert.AreEqual(1, save.Stored.QuestsDone.Count);
    Assert.IsTrue(ActivityCompletion.PersistQuestDone(save, new QuestId("MATH_COUNTING")),
      "case-insensitive id match");
    Assert.AreEqual(1, save.Stored.QuestsDone.Count, "no duplicate banking");
    Assert.IsFalse(ActivityCompletion.PersistQuestDone(null, new QuestId("x")), "null save fails soft");
  }

  // ---- P1-7 validator --------------------------------------------------------------

  [Test] public void P44I_ValidatorVerdicts() {
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var quests = new QuestManager(bus, learning, hints);
    bus.Subscribe<WordSeenEvent>(e => quests.AdvanceOnSeen(e.WordId));
    var q = new QuestId("math_counting");
    Assert.AreEqual(AnswerVerdict.Ignored, AnswerValidator.ValidateBring(null, q, true));
    Assert.AreEqual(AnswerVerdict.Ignored, AnswerValidator.ValidateBring(quests, q, false),
      "pre-talk: silent, nothing to be wrong about");
    quests.StartQuest(q);
    Assert.AreEqual(AnswerVerdict.GentleRetry, AnswerValidator.ValidateBring(quests, q, true),
      "find not done: gentle re-prompt, never punishment");
    Assert.IsFalse(AnswerValidator.CanBring(quests, q));
    bus.Publish(new WordSeenEvent(new WordId("one"), LearnSource.Object, System.DateTime.UtcNow));
    Assert.AreEqual(AnswerVerdict.Correct, AnswerValidator.ValidateBring(quests, q, true));
    Assert.IsTrue(AnswerValidator.CanBring(quests, q));
    Assert.IsTrue(AnswerValidator.IsQuestOpen(quests, q));
    quests.ReportAction(PlayerAction.Bring, new WordId("one"));
    Assert.IsTrue(quests.GetState(q).Completed);
    Assert.AreEqual(AnswerVerdict.Ignored, AnswerValidator.ValidateBring(quests, q, true),
      "post-completion: inert");
    Assert.IsFalse(AnswerValidator.IsQuestOpen(quests, q));
  }

  // ---- P1-2 anchors ------------------------------------------------------------------

  [Test] public void P44K_AnchorRegistryIsIdempotent() {
    GameObject root = new GameObject("P44World");
    try {
      ActivityAnchors a = ActivityAnchors.Ensure(root.transform, "PresentationRoot");
      Assert.IsNotNull(a);
      a.Entry = a.EnsureSlot("EntryAnchor", new Vector3(0f, 0f, 0f));
      a.Reward = a.EnsureSlot("RewardAnchor", new Vector3(1f, 0f, 2f));
      ActivityAnchors again = ActivityAnchors.Ensure(root.transform, "PresentationRoot");
      Assert.AreSame(a, again, "re-build reuses the registry");
      Assert.IsNotNull(again.Entry);
      Assert.AreEqual(new Vector3(1f, 0f, 2f), again.Reward.localPosition);
      GameObject camGo = new GameObject("P44Cam");
      SmartCamera cam = camGo.AddComponent<SmartCamera>();
      try {
        cam.FrameAnchor(null, null, 1f); // graceful no-op, never strands
        GameObject lookGo = new GameObject("P44Look");
        try {
          cam.FrameAnchor(a.Reward, lookGo.transform, 1f);
          Assert.AreEqual(CameraMode.Interaction, cam.Mode, "anchor beat frames");
        } finally { Object.DestroyImmediate(lookGo); }
      } finally { Object.DestroyImmediate(camGo); }
    } finally { Object.DestroyImmediate(root); }
  }

  // ---- P1-6/J1-O3 dialog click-through --------------------------------------------------

  static void AssertClickThrough(MonoBehaviour dlg, string id) {
    Assert.IsNotNull(dlg, id + " exists");
    Graphic[] graphics = dlg.GetComponentsInChildren<Graphic>(true);
    Assert.Greater(graphics.Length, 0, id + " has graphics");
    foreach (Graphic g in graphics) {
      if (g == null) continue;
      Button btn = g.GetComponent<Button>();
      if (btn != null) continue; // buttons are the ONLY click targets
      // Button labels ride on child Text: click-through like every other text.
      bool isButtonLabel = g is Text && g.transform.parent != null
        && g.transform.parent.GetComponent<Button>() != null;
      if (isButtonLabel) {
        Assert.IsFalse(g.raycastTarget, id + " button label click-through: " + g.name);
        continue;
      }
      // Button images stay interactive; everything else passes taps through.
      Assert.IsFalse(g.raycastTarget, id + " graphic click-through: " + g.name);
    }
  }

  [Test] public void P44L_AllSetupDialogsAreClickThrough() {
    GameObject go1 = new GameObject("P44Mic");
    MicSetupDialog mic = go1.AddComponent<MicSetupDialog>();
    mic.BuildUiImmediate();
    GameObject go2 = new GameObject("P44Dep");
    DependencySetupDialog dep = go2.AddComponent<DependencySetupDialog>();
    dep.BuildUiImmediate();
    GameObject go3 = new GameObject("P44RecLoc");
    RecordingLocationDialog loc = go3.AddComponent<RecordingLocationDialog>();
    loc.BuildUiImmediate();
    GameObject go4 = new GameObject("P44RecConfirm");
    RecordingConfirmDialog confirm = go4.AddComponent<RecordingConfirmDialog>();
    confirm.BuildUiImmediate();
    try {
      AssertClickThrough(mic, "MicSetup");
      AssertClickThrough(dep, "DependencySetup");
      AssertClickThrough(loc, "RecordingLocation");
      AssertClickThrough(confirm, "RecordingConfirm");
    } finally {
      Object.DestroyImmediate(go1);
      Object.DestroyImmediate(go2);
      Object.DestroyImmediate(go3);
      Object.DestroyImmediate(go4);
    }
  }
}
