// CT-P40: math quest runtime-loop integration (Phase 3.0.x S3B).
// Drives the REAL components headlessly through the full pilot loop —
// presenter + director + HUD + token-carrier + bloom consumer + real
// QuestManager/Learning/Hints/Rewards — proving events cross runtime (not
// just compile/test-fakes). No player movement, no scenes, no screenshots:
//   - subscription order MIRRORS production (Bootstrap's AdvanceOnSeen lambda
//     first at boot, Math subscribers later at scene load — order matters
//     because carrier/director read live quest state);
//   - proximity uses the deterministic TryProximityBring seam at the host's
//     own position (= zero-distance arrival, no travel faked).
// S3B §3/§4 evidence: token World->Carried->Consumed, bloom applied,
// HUD Start->Active->Complete->Post states. C# 9.0 only.
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public class CT_P40_QuestLoopIntegration {
  GameEventBus _bus;
  LearningService _learning;
  HintService _hints;
  QuestManager _quests;
  QuestRewardService _rewards;
  MarketHUD _hud;
  MathHostPresenter _host;
  MathQuestDirector _director;
  MathTokenCarry _carry;
  MathBloomDisplay _bloom;
  Interactable _cube;
  readonly List<GameObject> _gos = new List<GameObject>();

  GameObject NewGo(string name) {
    GameObject go = new GameObject(name);
    _gos.Add(go);
    return go;
  }

  void SetUp() {
    _bus = new GameEventBus();
    _learning = new LearningService(_bus);
    _hints = new HintService(_bus);
    // Production order: Bootstrap's seen->advance lambda subscribes at BOOT,
    // long before any Math wiring exists.
    _quests = new QuestManager(_bus, _learning, _hints);
    _bus.Subscribe<WordSeenEvent>(e => _quests.AdvanceOnSeen(e.WordId));
    _rewards = new QuestRewardService(_bus);

    GameObject hudGo = NewGo("P40Hud");
    _hud = hudGo.AddComponent<MarketHUD>();
    _hud.BuildUiImmediate();
    _hud.Bind(_bus, _quests);

    GameObject hostGo = NewGo("P40Tess");
    _host = hostGo.AddComponent<MathHostPresenter>();
    _host.Bind(_bus, _quests, _hints);
    Tess.Bind(null); // voice no-ops headless; wiring (IsBound) still true

    GameObject dirGo = NewGo("P40Director");
    _director = dirGo.AddComponent<MathQuestDirector>();
    _director.Build(_bus, _quests, _host, _hud);

    GameObject cubeGo = NewGo("P40OneCube");
    _cube = cubeGo.AddComponent<Interactable>();
    _cube.wordId = "one";
    _cube.interactionDistance = 2.5f;
    _cube.ParseIds();
    _cube.Bind(_bus);

    GameObject carryGo = NewGo("P40Carry");
    _carry = carryGo.AddComponent<MathTokenCarry>();
    GameObject handGo = NewGo("P40Hand");
    _carry.Build(_bus, _quests, _cube, handGo.transform);

    GameObject bloomRoot = NewGo("P40BloomRoot");
    for (int i = 0; i < 3; i++) {
      GameObject b = new GameObject("P40Bloom" + i);
      b.transform.SetParent(bloomRoot.transform, false);
      b.SetActive(false);
    }
    _bloom = bloomRoot.AddComponent<MathBloomDisplay>();
    _bloom.Build(_bus, _quests); // journey fix: quest service for re-entry adoption
  }

  void TearDown() {
    foreach (GameObject go in _gos) {
      if (go != null) UnityEngine.Object.DestroyImmediate(go);
    }
    _gos.Clear();
  }

  [Test] public void P40A_FirstTalkStartsQuest() {
    SetUp();
    try {
      Assert.AreEqual(MathTokenState.InWorld, _carry.State, "token starts in world");
      Assert.IsFalse(_carry.IsTokenShown);
      Assert.IsFalse(_bloom.BloomShown);
      Assert.IsNotNull(_host.OnFirstTalk, "director wires first-talk");
      _host.OnFirstTalk();
      Assert.AreEqual(0, _quests.GetState(new QuestId("math_counting")).ObjectiveIndex);
      Assert.IsFalse(_quests.GetState(new QuestId("math_counting")).Completed);
      Assert.AreEqual("Find the one", _hud.CurrentObjective, "HUD reflects started state");
    } finally { TearDown(); }
  }

  [Test] public void P40B_FindStashesTokenAndUpdatesHud() {
    SetUp();
    try {
      _host.OnFirstTalk();
      _cube.Interact(); // real Interactable path (ParseIds + bus publish)
      Assert.AreEqual(1, _quests.GetState(new QuestId("math_counting")).ObjectiveIndex,
        "seen 'one' advances the find");
      Assert.AreEqual("Bring it to Tess", _hud.CurrentObjective, "HUD reflects active-bring state");
      Assert.AreEqual(MathTokenState.Carried, _carry.State, "cube leaves the world");
      Assert.IsFalse(_cube.gameObject.activeSelf, "world cube hidden");
      Assert.IsTrue(_carry.IsTokenShown, "hand token shown");
    } finally { TearDown(); }
  }

  [Test] public void P40C_BringCompletesQuestAndBlooms() {
    SetUp();
    try {
      _host.OnFirstTalk();
      _cube.Interact();
      _host.TryProximityBring(_host.transform.position); // zero-distance arrival seam
      Assert.IsTrue(_quests.GetState(new QuestId("math_counting")).Completed, "bring completes");
      Assert.AreEqual("Math World", _hud.CurrentObjective,
        "director post-quest line wins over the global Great-job handler (subscription order)");
      Assert.AreEqual(MathTokenState.Consumed, _carry.State, "token consumed");
      Assert.IsFalse(_carry.IsTokenShown, "hand token hidden");
      Assert.IsTrue(_bloom.BloomShown, "bloom consumer applied");
      Assert.IsTrue(_rewards.HasWorldChange("math_bloom"), "ledger banks the change id");
      // Post-quest talk stays silent (no restart, no HUD churn).
      string before = _hud.CurrentObjective;
      _host.OnTalk();
      Assert.AreEqual(before, _hud.CurrentObjective, "post-quest talk is inert");
    } finally { TearDown(); }
  }

  // P3.0.1 journey fix pin: a FRESH host after re-entry (never saw
  // QuestStartedEvent) must still complete the bring — find-done is quest
  // state, not presenter event history.
  [Test] public void P40E_ReentryHostCompletesBringStateDriven() {
    SetUp();
    try {
      _host.OnFirstTalk();          // quest starts on the first-visit host
      _cube.Interact();             // find advances to index 1
      GameObject reentryGo = NewGo("P40ReentryTess");
      MathHostPresenter host2 = reentryGo.AddComponent<MathHostPresenter>();
      host2.Bind(_bus, _quests, _hints); // fresh subscriber: no QuestStarted seen
      host2.TryProximityBring(host2.transform.position);
      Assert.IsTrue(_quests.GetState(new QuestId("math_counting")).Completed,
        "bring completes on a fresh mid-quest re-entry host");
    } finally { TearDown(); }
  }

  // P3.0.1 journey fix pin: a fresh bloom consumer on re-entry adopts an
  // already-completed quest (the reward visual survives the second visit).
  [Test] public void P40F_ReentryBloomAdoptsCompletedQuest() {
    SetUp();
    try {
      _host.OnFirstTalk();
      _cube.Interact();
      _host.TryProximityBring(_host.transform.position);
      Assert.IsTrue(_quests.GetState(new QuestId("math_counting")).Completed);
      GameObject bloomRoot = NewGo("P40ReentryBloomRoot");
      for (int i = 0; i < 3; i++) {
        GameObject b = new GameObject("P40ReentryBloom" + i);
        b.transform.SetParent(bloomRoot.transform, false);
        b.SetActive(false);
      }
      MathBloomDisplay bloom2 = bloomRoot.AddComponent<MathBloomDisplay>();
      bloom2.Build(_bus, _quests); // fresh scene load, event long gone
      Assert.IsTrue(bloom2.BloomShown, "bloom adopts the completed quest");
      for (int i = 0; i < bloomRoot.transform.childCount; i++) {
        Assert.IsTrue(bloomRoot.transform.GetChild(i).gameObject.activeSelf,
          "blooms visible on re-entry");
      }
    } finally { TearDown(); }
  }

  // P3.0.1 journey fix pin: a fresh carry on re-entry after completion must
  // adopt Consumed (world cube + hand token stay hidden — the reward state
  // must not regress).
  [Test] public void P40G_ReentryCarryAdoptsConsumed() {
    SetUp();
    try {
      _host.OnFirstTalk();
      _cube.Interact();
      _host.TryProximityBring(_host.transform.position);
      Assert.IsTrue(_quests.GetState(new QuestId("math_counting")).Completed);
      GameObject carryGo2 = NewGo("P40ReentryCarry");
      MathTokenCarry carry2 = carryGo2.AddComponent<MathTokenCarry>();
      GameObject cube2 = NewGo("P40ReentryCube");
      Interactable inter2 = cube2.AddComponent<Interactable>();
      inter2.wordId = "one";
      inter2.ParseIds();
      inter2.Bind(_bus);
      GameObject hand2 = NewGo("P40ReentryHand");
      carry2.Build(_bus, _quests, inter2, hand2.transform);
      Assert.AreEqual(MathTokenState.Consumed, carry2.State, "re-entry adopts Consumed");
      Assert.IsFalse(cube2.activeSelf, "world cube stays hidden after completion");
      Assert.IsFalse(carry2.IsTokenShown, "hand token stays hidden");
    } finally { TearDown(); }
  }

  [Test] public void P40D_PreTalkTapArmsNothing() {
    SetUp();
    try {
      _cube.Interact(); // tap before any talk: learning only, no quest, no stash
      Assert.AreEqual(0, _quests.GetState(new QuestId("math_counting")).ObjectiveIndex);
      Assert.AreEqual(MathTokenState.InWorld, _carry.State, "no pickup before Talk (Mia rule)");
      Assert.IsTrue(_cube.gameObject.activeSelf, "cube stays");
    } finally { TearDown(); }
  }
}
