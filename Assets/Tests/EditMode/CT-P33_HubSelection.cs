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
}
