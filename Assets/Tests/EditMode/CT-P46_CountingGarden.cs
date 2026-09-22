// CT-P46: S2 PIONEER MICRO-WORLD — Counting Garden (v2: own LAZY scene).
// Pins the reusable micro-world contract proved by the first real destination:
// hub-gate walk-in portal -> LAZY scene load (never at boot) -> nested scene
// while the subject stays loaded -> exit unload -> hub landing, plus the new
// garden scene content (5 fenced zones in an arc) and the area state machine.
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CT_P46_CountingGarden {
  // ---- fake scene ops (same seam the WorldTransition tests use) ---------------

  sealed class FakeOps : ISceneOps {
    public readonly HashSet<string> Loaded = new HashSet<string>();
    public int LoadCalls;
    public int UnloadCalls;
    public bool FailLoads;
    public bool IsLoaded(string sceneName) { return Loaded.Contains(sceneName); }
    public Task LoadAdditiveAsync(string sceneName) {
      LoadCalls++;
      if (FailLoads) throw new System.InvalidOperationException("fake load failure");
      Loaded.Add(sceneName);
      return Task.CompletedTask;
    }
    public Task UnloadAsync(string sceneName) {
      UnloadCalls++;
      Loaded.Remove(sceneName);
      return Task.CompletedTask;
    }
    public Task<bool> LoadTask(string sceneName) { return Task.FromResult(true); }
  }

  GameObject _root;
  MathWorldBuilder _builder;

  void SetUp() {
    _root = new GameObject("P46MathWorld");
    _builder = _root.AddComponent<MathWorldBuilder>();
    _builder.BuildContent(_root.transform);
    try { Physics.SyncTransforms(); } catch (System.Exception) { }
  }

  void TearDown() {
    if (_root != null) Object.DestroyImmediate(_root);
    _root = null;
    _builder = null;
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

  // A. Hub door: one walk-in enter portal at the counting gate mouth.
  [Test] public void P46A_HubPortalAtGate() {
    SetUp();
    try {
      MicroWorldPortal enter = _builder.CountingGardenPortal;
      Assert.IsNotNull(enter, "hub-gate portal built");
      Assert.IsFalse(enter.ExitMode, "hub portal is an enter portal");
      Assert.AreEqual(CountingGardenArea.AreaId, enter.areaId, "portal targets the counting_garden area");
      Assert.Greater(enter.fireRadius, 0.5f, "portal has a walk-in radius");
      MicroWorldGate counting = _builder.MicroGates[0];
      Assert.AreEqual("counting_garden", counting.gateId, "gate 0 is the counting garden");
      float mouthDist = Vector3.Distance(
        new Vector3(enter.transform.position.x, 0f, enter.transform.position.z),
        new Vector3(counting.EntryAnchor.position.x, 0f, counting.EntryAnchor.position.z));
      Assert.Less(mouthDist, 0.05f, "portal rides the gate mouth anchor");
    } finally { TearDown(); }
  }

  // B. LAZY scene contract: the garden scene is NOT loaded while the subject
  // world is active; EnterMicroAsync loads it on demand; the subject stays
  // loaded underneath; exit unloads; subject return is refused while inside.
  [Test] public void P46B_LazyMicroSceneLoad() {
    var ops = new FakeOps();
    var t = new WorldTransition(SubjectIds.Main);
    var math = new SubjectId("math");
    Assert.IsTrue(t.EnterAsync(ops, math, "MathScene").GetAwaiter().GetResult(), "subject loads");
    Assert.IsTrue(ops.Loaded.Contains("MathScene"));
    Assert.IsFalse(ops.Loaded.Contains(CountingGardenBuilder.SceneName),
      "micro scene must NOT be loaded at subject entry (lazy)");
    Assert.IsTrue(t.EnterMicroAsync(ops, CountingGardenBuilder.SceneName).GetAwaiter().GetResult(),
      "micro scene loads on demand");
    Assert.IsTrue(ops.Loaded.Contains(CountingGardenBuilder.SceneName));
    Assert.IsTrue(ops.Loaded.Contains("MathScene"), "subject scene stays loaded underneath");
    Assert.IsFalse(t.EnterMicroAsync(ops, CountingGardenBuilder.SceneName).GetAwaiter().GetResult(),
      "double micro enter is spam-safe");
    Assert.IsFalse(t.ReturnAsync(ops, "MathScene").GetAwaiter().GetResult(),
      "subject return is refused while the micro scene is loaded");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "micro scene unloads");
    Assert.IsFalse(ops.Loaded.Contains(CountingGardenBuilder.SceneName));
    Assert.IsTrue(t.ReturnAsync(ops, "MathScene").GetAwaiter().GetResult(),
      "subject return works again after the micro exit");
  }

  // C. Honest failure: a failed micro load keeps the subject world and does not
  // latch the micro slot (the child can try the gate again).
  [Test] public void P46C_MicroLoadFailureStaysClean() {
    var ops = new FakeOps();
    var t = new WorldTransition(SubjectIds.Main);
    var math = new SubjectId("math");
    Assert.IsTrue(t.EnterAsync(ops, math, "MathScene").GetAwaiter().GetResult(), "subject loads");
    ops.FailLoads = true;
    Assert.IsFalse(t.EnterMicroAsync(ops, CountingGardenBuilder.SceneName).GetAwaiter().GetResult(),
      "failed load reports false");
    Assert.IsNull(t.MicroScene, "micro slot stays empty after a failed load");
    Assert.AreEqual(WorldTransitionState.InSubject, t.State, "subject world stays active");
    ops.FailLoads = false;
    Assert.IsTrue(t.EnterMicroAsync(ops, CountingGardenBuilder.SceneName).GetAwaiter().GetResult(),
      "retry succeeds after the failure clears");
  }

  // D. Garden scene content: FIVE fenced zones in an arc + entry/exit + anchors
  // (v2 scope: enclosures only — no activities yet).
  [Test] public void P46D_GardenZonesAndAnchors() {
    GameObject garden = new GameObject("P46GardenWorld");
    try {
      CountingGardenBuilder builder = garden.AddComponent<CountingGardenBuilder>();
      builder.BuildContent(garden.transform);
      Assert.AreEqual(5, CountingGardenBuilder.ZoneCount, "five zones");
      Assert.AreEqual(5, builder.ZoneCenters.Count, "five zone centres exposed");
      Assert.Greater(CountingGardenBuilder.WorldOffset.magnitude, 60f,
        "separate island (no overlap with Main/Math)");
      Vector3 c = CountingGardenBuilder.ArcCenter;
      float minAng = 999f, maxAng = -999f;
      foreach (Vector3 z in builder.ZoneCenters) {
        float d = Vector3.Distance(new Vector3(z.x, 0f, z.z), c);
        Assert.Greater(d, 8f, "zone sits out on the arc");
        Assert.Less(d, 14f, "zone stays on the arc");
        float ang = Mathf.Atan2(z.x - c.x, z.z - c.z) * Mathf.Rad2Deg;
        if (ang < minAng) minAng = ang;
        if (ang > maxAng) maxAng = ang;
        int zi = builder.ZoneCenters.IndexOf(z);
        // S3-P2V: zone 2 is the demo theatre (its floor is CG DemoStagePad).
        Transform zonePad = FindDeep(garden.transform, "CGZone" + zi + "Pad");
        if (zonePad == null) zonePad = FindDeep(garden.transform, "CGDemoStagePad");
        Assert.IsNotNull(zonePad, "zone pad built");
      }
      Assert.Greater(maxAng - minAng, 60f, "zones fan out in an arc");
      for (int z = 0; z < CountingGardenBuilder.ZoneCount; z++) {
        Assert.IsNotNull(FindDeep(garden.transform, "CGZone" + z + "Anchor"), "zone anchor " + z);
        Assert.IsNotNull(FindDeep(garden.transform, "CGZone" + z + "Fence0"), "zone fence ring " + z);
      }
      ActivityAnchors a = builder.Anchors;
      Assert.IsNotNull(a, "garden anchor registry");
      Assert.IsNotNull(a.Entry, "entry anchor");
      Assert.IsNotNull(a.GameplayFocus, "focus anchor");
      Assert.IsNotNull(a.Npc, "npc anchor");
      Assert.IsNotNull(a.Camera, "camera anchor");
      Assert.IsNotNull(a.CameraLook, "camera look anchor");
      Assert.IsNotNull(a.Prompt, "prompt anchor");
      Assert.IsNotNull(a.Feedback, "feedback anchor");
      Assert.IsNotNull(a.Reward, "reward anchor");
      Assert.IsNotNull(a.Exit, "exit anchor");
      Assert.IsNotNull(builder.EntryPoint, "EntryPoint marker");
      Assert.IsNotNull(builder.ExitPortal, "exit portal");
      Assert.IsTrue(builder.ExitPortal.ExitMode, "exit portal is in exit mode");
      Assert.AreEqual(CountingGardenArea.AreaId, builder.ExitPortal.areaId, "exit targets the area");
      // J4 lesson: the entry spawn must sit CLEAR of the exit portal fire
      // radius, or the arrival would instantly trigger the way home (user
      // report: "sang được sân vườn đếm nhưng lại bị quay lại math world").
      float spawnClear = Vector2.Distance(
        new Vector2(builder.EntryPoint.localPosition.x, builder.EntryPoint.localPosition.z),
        new Vector2(builder.ExitPortal.transform.localPosition.x, builder.ExitPortal.transform.localPosition.z));
      Assert.GreaterOrEqual(spawnClear, builder.ExitPortal.fireRadius + builder.ExitPortal.rearmMargin,
        "entry spawn clears the exit portal re-arm radius");
    } finally { Object.DestroyImmediate(garden); }
  }

  // E. Area state machine (pure seams): enter/exit idempotent, no double enter,
  // no exit while outside — the anti-spam contract for the travel beats.
  [Test] public void P46E_AreaStateMachine() {
    GameObject go = new GameObject("P46Area");
    try {
      CountingGardenArea area = go.AddComponent<CountingGardenArea>();
      Assert.IsFalse(area.IsInside, "starts outside");
      Assert.IsTrue(area.CanEnter, "can enter from outside");
      Assert.IsFalse(area.CanExit, "cannot exit while outside");
      Assert.IsTrue(area.TryEnterForTests(), "first enter succeeds");
      Assert.IsTrue(area.IsInside, "inside after enter");
      Assert.IsFalse(area.CanEnter, "cannot re-enter while inside");
      Assert.IsFalse(area.TryEnterForTests(), "double enter blocked");
      Assert.IsTrue(area.TryExitForTests(), "exit succeeds");
      Assert.IsFalse(area.IsInside, "outside after exit");
      Assert.IsFalse(area.TryExitForTests(), "double exit blocked");
      Assert.IsTrue(area.CanEnter, "can enter again after exit");
    } finally { Object.DestroyImmediate(go); }
  }

  // F. Hub return landing: outside the portal fire radius so leaving the garden
  // can never instantly re-enter (same lesson as the journey J4 fix).
  [Test] public void P46F_HubReturnClearsPortalRadius() {
    SetUp();
    try {
      MicroWorldPortal enter = _builder.CountingGardenPortal;
      Assert.IsNotNull(enter, "hub portal built");
      Vector3 ret = MathWorldBuilder.GardenHubReturnLocal;
      float d = Vector2.Distance(
        new Vector2(ret.x, ret.z),
        new Vector2(enter.transform.localPosition.x, enter.transform.localPosition.z));
      Assert.GreaterOrEqual(d, enter.fireRadius + enter.rearmMargin,
        "return landing sits outside the portal re-arm radius");
    } finally { TearDown(); }
  }
}
