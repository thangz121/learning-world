// CT-P33: hub-selection mode (user round: gate-selection hall). Covers the
// parts that run in batch EditMode: the flag contract (default off = full
// world for the whole quest suite) + the gate-plaza predicate via the same
// reflection seam P28E uses. Full world assembly is verified in player
// builds (P25H pattern: Ignore when the domain doesn't instantiate it).
// C# 9.0 only.
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public class CT_P33_HubSelection {
  static bool InGatePlaza(Vector3 pos) {
    MethodInfo m = typeof(MarketBuilder).GetMethod("IsInGatePlaza",
      BindingFlags.NonPublic | BindingFlags.Static);
    Assert.IsNotNull(m, "gate-plaza predicate must exist (contract seam)");
    return (bool)m.Invoke(null, new object[] { pos });
  }

  [Test] public void P33A_HubFlagDefaultsOff() {
    Assert.IsFalse(MarketBuilder.HubSelectionOnly, "flag defaults off (full world for tests)");
  }

  [Test] public void P33B_HubFlagRoundTrip() {
    bool prev = MarketBuilder.HubSelectionOnly;
    try {
      MarketBuilder.HubSelectionOnly = true;
      Assert.IsTrue(MarketBuilder.HubSelectionOnly);
      MarketBuilder.HubSelectionOnly = false;
      Assert.IsFalse(MarketBuilder.HubSelectionOnly);
    } finally { MarketBuilder.HubSelectionOnly = prev; }
  }

  [Test] public void P33C_GatePlazaCoversGates() {
    foreach (SubjectDefinition def in SubjectCatalog.All) {
      Assert.IsTrue(InGatePlaza(def.GatePos), def.DisplayName + " gate sits inside its plaza");
      Assert.IsTrue(InGatePlaza(def.GatePos + new Vector3(1.5f, 0f, 1.5f)),
        def.DisplayName + " plaza margin covers the arch footprint");
    }
  }

  [Test] public void P33D_GatePlazaLeavesPlayOpen() {
    Assert.IsFalse(InGatePlaza(MarketBuilder.PlayerSpawn), "spawn stays plantable-free of plaza veto");
    Assert.IsFalse(InGatePlaza(new Vector3(0f, 0f, -8.5f)), "mid-arc gap stays open");
    Assert.IsFalse(InGatePlaza(new Vector3(0f, 0f, 4.5f)), "south lawn stays open");
  }

  [Test] public void P33E_HubBuildSkipsQuestObjects() {
    bool prev = MarketBuilder.HubSelectionOnly;
    MarketBuilder.HubSelectionOnly = true;
    var go = new GameObject("P33Hub");
    try {
      var builder = go.AddComponent<MarketBuilder>();
      if (builder.Player == null) {
        Assert.Ignore("World not instantiated in this EditMode domain — hub assembly verified in player build");
      }
      foreach (string n in new[] {
        "MarketStall", "AppleCrate", "BallCrate", "FlowerBed", "BallStand",
        "Pedestal", "QuestionBubble", "MiloMat", "Barrel",
      }) {
        int found = 0;
        foreach (Transform t in go.transform.GetComponentsInChildren<Transform>(true)) {
          if (t.name == n) found++;
        }
        Assert.AreEqual(0, found, "hub must not contain " + n);
      }
      Assert.IsNull(builder.Bubble, "no bubble service object in hub mode");
    } finally {
      Object.DestroyImmediate(go);
      MarketBuilder.HubSelectionOnly = prev;
    }
  }

  [Test] public void P33F_SetupLockedPaintsBoard() {
    // Gate name boards ride ON the arch (locked mode): position painted once,
    // fixed yaw toward the hub via the proven billboard math — no follow
    // anchor, no per-frame rotation (the floating pill read as a black box).
    var go = new GameObject("P33Locked");
    try {
      WorldNameLabel label = go.AddComponent<WorldNameLabel>();
      Vector3 board = new Vector3(10.5f, 1.95f, -4f);
      label.SetupLocked("Toán", board, SubjectCatalog.HubCenter);
      label.Show();
      Assert.AreEqual("Toán", label.CurrentName, "locked setup must name the board");
      Assert.AreEqual(board.x, go.transform.position.x, 1e-6f);
      Assert.AreEqual(board.y, go.transform.position.y, 1e-6f);
      Assert.AreEqual(board.z, go.transform.position.z, 1e-6f);
      Quaternion expect = WorldNameLabel.BillboardRotation(board, SubjectCatalog.HubCenter);
      Assert.AreEqual(expect.eulerAngles.y, go.transform.rotation.eulerAngles.y, 1e-3f,
        "locked yaw must face the hub exactly like the board it sits on");
      // Reusing the label in follow mode must unlock it (no frozen tags).
      var follow = new GameObject("P33LockedFollow");
      try {
        label.Setup("Milo", follow.transform, 2f);
        Assert.AreEqual("Milo", label.CurrentName);
      } finally { Object.DestroyImmediate(follow); }
    } finally {
      Object.DestroyImmediate(go);
    }
  }

  [Test] public void P33G_HubFollowOffsetHigherThanDefault() {
    // Hub convention: the spawn camera sits higher so the whole gate arc
    // reads in one frame; the frozen default framing is never touched.
    bool prev = MarketBuilder.HubSelectionOnly;
    try {
      Vector3 def = new Vector3(0f, 3.2f, 4.6f);
      MarketBuilder.HubSelectionOnly = true;
      Assert.AreEqual(MarketBuilder.HubFollowOffset, MarketBuilder.FollowOffset(def),
        "hub must frame higher than default");
      Assert.Greater(MarketBuilder.HubFollowOffset.y, def.y, "hub camera higher");
      Assert.Greater(MarketBuilder.HubFollowOffset.magnitude, def.magnitude, "hub camera wider");
      MarketBuilder.HubSelectionOnly = false;
      Assert.AreEqual(def, MarketBuilder.FollowOffset(def),
        "full world keeps the frozen default framing (CT-P32)");
    } finally { MarketBuilder.HubSelectionOnly = prev; }
  }

  [Test] public void P33H_GateClicksSnapToMouth() {
    // Clicking the arch/pillars must walk the corridor centre (reachable), not
    // the raw collider point (unreachable stall outside the poll radius).
    SubjectDefinition th = SubjectCatalog.Thinking;
    Vector3 mouth;
    Vector3 pillarClick = th.GatePos + new Vector3(1.6f, 1f, 0f);
    Assert.IsTrue(ClickRouter.TrySnapToGateMouth(pillarClick, out mouth), "pillar click must snap");
    float mouthDist = Vector3.Distance(new Vector3(mouth.x, 0f, mouth.z), new Vector3(th.GatePos.x, 0f, th.GatePos.z));
    Assert.AreEqual(0.6f, mouthDist, 1e-4f, "mouth sits 0.6m hub-side of the gate (walkway end)");
    float gateHub = Vector3.Distance(new Vector3(th.GatePos.x, 0f, th.GatePos.z), SubjectCatalog.HubCenter);
    float mouthHub = Vector3.Distance(new Vector3(mouth.x, 0f, mouth.z), SubjectCatalog.HubCenter);
    Assert.Less(mouthHub, gateHub, "mouth must be hub-side so the walk reads up the bricks");
    Vector3 retClick = th.ReturnPoint + new Vector3(0.5f, 0f, 0.5f);
    Assert.IsTrue(ClickRouter.TrySnapToGateMouth(retClick, out mouth), "return-arch click must snap");
    Assert.AreEqual(th.ReturnPoint.x, mouth.x, 1e-6f);
    Assert.AreEqual(th.ReturnPoint.z, mouth.z, 1e-6f);
    Assert.IsFalse(ClickRouter.TrySnapToGateMouth(MarketBuilder.PlayerSpawn, out mouth),
      "spawn clicks must walk raw (no force-enter)");
    Vector3 sign = th.GatePos + new Vector3(2.38f, 0f, 0f);
    Assert.IsFalse(ClickRouter.TrySnapToGateMouth(sign, out mouth),
      "signpost clicks (2.38m out) must not force-enter");
  }

  [Test] public void P33I_ThroughOpeningClicksSnapToMouth() {
    // Clicking the gate MIDDLE hits district ground behind — the ray still
    // threads the gate, so it must enter (not overshoot out the back).
    SubjectDefinition th2 = SubjectCatalog.Thinking;
    Vector3 org = new Vector3(0f, 3.2f, 9.1f);
    Vector3 toHub = SubjectCatalog.HubCenter - th2.GatePos;
    toHub.y = 0f;
    toHub.Normalize();
    Vector3 farHit = th2.GatePos - toHub * 4f; // district ground past the gate
    farHit.y = 0f;
    Ray through = new Ray(org, th2.GatePos - org);
    Vector3 mouth2;
    Assert.IsTrue(ClickRouter.TrySnapGateOnRay(through, farHit, out mouth2),
      "through-opening click must snap");
    float md = Vector3.Distance(new Vector3(mouth2.x, 0f, mouth2.z),
      new Vector3(th2.GatePos.x, 0f, th2.GatePos.z));
    Assert.AreEqual(0.6f, md, 1e-4f, "through click lands at the mouth, not behind");
    // Lawn click whose ray only passes the gate far beyond the hit: no snap.
    Vector3 lawnHit = new Vector3(2f, 0f, 2f);
    Ray lawn = new Ray(org, lawnHit - org);
    Assert.IsFalse(ClickRouter.TrySnapGateOnRay(lawn, lawnHit, out mouth2),
      "gate beyond the clicked lawn must not hijack the walk");
  }
}
