// CT-P35: S2 travel seams (Phase 3.0.x Math pilot).
// P35A pins the scene catalog (Math scene-backed, siblings spatial until
// phased). P35B pins the click-bounds default (world-centred zero = legacy
// Main behaviour; Bootstrap recentres on travel). C# 9.0 only.
using NUnit.Framework;
using UnityEngine;

public class CT_P35_MathTravel {
  [Test] public void P35A_MathSceneResolvedThroughCatalog() {
    Assert.AreEqual("MathScene", SubjectCatalog.Math.SceneName,
      "Math must resolve its scene through the catalog (no scattered literals)");
    Assert.IsTrue(string.IsNullOrEmpty(SubjectCatalog.Thinking.SceneName),
      "Thinking stays spatial until P3.0.2");
    Assert.IsTrue(string.IsNullOrEmpty(SubjectCatalog.English.SceneName),
      "English stays spatial until P3.0.3");
    Assert.IsTrue(string.IsNullOrEmpty(SubjectCatalog.Vietnamese.SceneName),
      "Vietnamese stays spatial until P3.0.4");
  }

  [Test] public void P35B_ClickBoundsDefaultToMain() {
    var go = new GameObject("P35Router");
    try {
      ClickRouter router = go.AddComponent<ClickRouter>();
      Assert.AreEqual(Vector3.zero, router.boundCenter,
        "default centre zero preserves legacy Main bounds (regression guard)");
      Assert.AreEqual(16f, router.boundX);
      Assert.AreEqual(14f, router.boundZ);
    } finally { Object.DestroyImmediate(go); }
  }
}
