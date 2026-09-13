// CT-P01: apple interaction contract (Phase-1 closure). Guards the real manual
// playtest failure "walked into the apple area, nothing happened":
//   A. Interactable.ParseIds after code-built assignment (MarketBuilder sets
//      fields AFTER AddComponent/Awake; without re-parse HasWord stays false
//      and Interact() silently drops every event — the actual blocker).
//   B-E. ProximityDiscovery: fires on enter while armed, rising-edge only,
//      silent while the router holds the same target, disarmed on completion.
// Pure EditMode: deterministic public-API calls only (PollImmediate, bus
// events), no timing, no screenshots. C# 9.0 only.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class CT_P01_Proximity {
  static readonly QuestId W1Quest = new QuestId("w1_mia_apple");
  static readonly WordId Apple = new WordId("apple");

  sealed class Trap {
    public readonly List<WordSeenEvent> Seen = new List<WordSeenEvent>();
    public Trap(GameEventBus bus) { bus.Subscribe<WordSeenEvent>(e => Seen.Add(e)); }
  }

  static GameObject BuildApple(out Interactable interactable, out ProximityDiscovery discovery) {
    GameObject go = new GameObject("AppleContractTest");
    go.AddComponent<SphereCollider>();
    interactable = go.AddComponent<Interactable>(); // Awake runs on empty fields
    discovery = go.AddComponent<ProximityDiscovery>();
    return go;
  }

  // A. Code-built assignment MUST re-parse or events silently drop.
  [Test] public void CT_P01A_ParseIdsAfterAssignment() {
    GameEventBus bus = new GameEventBus();
    Trap trap = new Trap(bus);
    GameObject go = new GameObject("ParseIdsTest");
    try {
      Interactable apple = go.AddComponent<Interactable>();
      Assert.IsFalse(apple.HasWord, "fresh code-built Interactable has no word yet");
      apple.wordId = "apple";
      apple.interactionId = "take_apple";
      apple.npcId = "mia";
      apple.interactionDistance = 2.5f;
      apple.ParseIds();
      Assert.IsTrue(apple.HasWord, "ParseIds must pick up post-Awake assignment");
      Assert.AreEqual("apple", apple.Word.Value);
      apple.Bind(bus);
      apple.Interact();
      Assert.AreEqual(1, trap.Seen.Count, "Interact must publish after ParseIds");
      Assert.AreEqual("apple", trap.Seen[0].WordId.Value);
      Assert.AreEqual(LearnSource.Object, trap.Seen[0].Source);
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // B. Proximity fires once on enter while the quest is active.
  [Test] public void CT_P01B_FiresOnEnterWhileArmed() {
    GameEventBus bus = new GameEventBus();
    Trap trap = new Trap(bus);
    GameObject go = BuildApple(out Interactable apple, out ProximityDiscovery disc);
    try {
      apple.wordId = "apple"; apple.interactionDistance = 2.5f; apple.ParseIds(); apple.Bind(bus);
      disc.Bind(bus, null, null, null);
      Vector3 far = new Vector3(0f, 0f, 4.5f);
      Vector3 inside = new Vector3(0f, 0f, 0.5f);
      go.transform.position = Vector3.zero;
      disc.PollImmediate(far);
      Assert.AreEqual(0, trap.Seen.Count, "silent before quest start");
      bus.Publish(new QuestStartedEvent(W1Quest, DateTime.UtcNow));
      disc.PollImmediate(far);
      Assert.AreEqual(0, trap.Seen.Count, "silent outside range");
      disc.PollImmediate(inside);
      Assert.AreEqual(1, trap.Seen.Count, "enter must fire once");
      disc.PollImmediate(inside);
      Assert.AreEqual(1, trap.Seen.Count, "standing inside must not refire");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // C. Exit + re-enter fires again (rising edge, per quest arming).
  [Test] public void CT_P01C_ExitReenterRefires() {
    GameEventBus bus = new GameEventBus();
    Trap trap = new Trap(bus);
    GameObject go = BuildApple(out Interactable apple, out ProximityDiscovery disc);
    try {
      apple.wordId = "apple"; apple.interactionDistance = 2.5f; apple.ParseIds(); apple.Bind(bus);
      disc.Bind(bus, null, null, null);
      go.transform.position = Vector3.zero;
      bus.Publish(new QuestStartedEvent(W1Quest, DateTime.UtcNow));
      disc.PollImmediate(new Vector3(0f, 0f, 0.5f));
      Assert.AreEqual(1, trap.Seen.Count);
      disc.PollImmediate(new Vector3(0f, 0f, 4.5f));
      disc.PollImmediate(new Vector3(0f, 0f, 0.5f));
      Assert.AreEqual(2, trap.Seen.Count, "re-enter must fire again");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // D. Silent while the router holds a pending arrival for the same target
  // (the click path fires on arrival instead); resumes after reroute+reenter.
  [Test] public void CT_P01D_SuppressedWhileRouterPending() {
    GameEventBus bus = new GameEventBus();
    Trap trap = new Trap(bus);
    GameObject go = BuildApple(out Interactable apple, out ProximityDiscovery disc);
    GameObject routerGo = new GameObject("RouterContractTest");
    GameObject groundGo = new GameObject("GroundContractTest");
    try {
      apple.wordId = "apple"; apple.interactionDistance = 2.5f; apple.ParseIds(); apple.Bind(bus);
      go.transform.position = Vector3.zero;
      ClickRouter router = routerGo.AddComponent<ClickRouter>();
      ClickToMove player = routerGo.AddComponent<ClickToMove>();
      router.AttachPlayer(player);
      disc.Bind(bus, null, null, router);
      bus.Publish(new QuestStartedEvent(W1Quest, DateTime.UtcNow));
      // Player clicked the apple: router holds the arrival.
      router.RouteHitForTests(go.GetComponent<Collider>(), new Vector3(0f, 0f, 1f));
      Assert.AreEqual(apple, router.PendingInteractTarget, "router must hold the apple arrival");
      disc.PollImmediate(new Vector3(0f, 0f, 0.5f));
      Assert.AreEqual(0, trap.Seen.Count, "proximity must stay silent while router will fire");
      // Player clicked elsewhere instead: pending cleared, walk back in fires.
      groundGo.AddComponent<BoxCollider>();
      router.RouteHitForTests(groundGo.GetComponent<Collider>(), new Vector3(0f, 0f, 4.5f));
      Assert.IsNull(router.PendingInteractTarget, "ground click must clear pending");
      disc.PollImmediate(new Vector3(0f, 0f, 4.5f));
      disc.PollImmediate(new Vector3(0f, 0f, 0.5f));
      Assert.AreEqual(1, trap.Seen.Count, "re-enter after reroute must fire");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
      UnityEngine.Object.DestroyImmediate(routerGo);
      UnityEngine.Object.DestroyImmediate(groundGo);
    }
  }

  // E. Quest completion disarms; a new quest start re-arms.
  [Test] public void CT_P01E_DisarmOnCompleteRearmOnStart() {
    GameEventBus bus = new GameEventBus();
    Trap trap = new Trap(bus);
    GameObject go = BuildApple(out Interactable apple, out ProximityDiscovery disc);
    try {
      apple.wordId = "apple"; apple.interactionDistance = 2.5f; apple.ParseIds(); apple.Bind(bus);
      disc.Bind(bus, null, null, null);
      go.transform.position = Vector3.zero;
      bus.Publish(new QuestStartedEvent(W1Quest, DateTime.UtcNow));
      bus.Publish(new QuestCompletedEvent(W1Quest, DateTime.UtcNow));
      disc.PollImmediate(new Vector3(0f, 0f, 0.5f));
      Assert.AreEqual(0, trap.Seen.Count, "silent after completion");
      bus.Publish(new QuestStartedEvent(W1Quest, DateTime.UtcNow));
      disc.PollImmediate(new Vector3(0f, 0f, 0.5f));
      Assert.AreEqual(1, trap.Seen.Count, "new quest start must re-arm");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }
}
