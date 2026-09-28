// CT-P61: S3-P2Z33 — GameJuice shared feedback layer.
// Pins the deterministic, accessibility-aware one-shot effects every activity
// uses (pop / squash / wobble / ring / flash / camera punch) and the
// ReduceMotion switch. Effects are Update-driven; tests call Step(dt) directly.
// C# 9.0 only.
using NUnit.Framework;
using UnityEngine;

public class CT_P61_GameJuice {
  bool _reduce;

  [SetUp] public void SetUp() { _reduce = GameJuice.ReduceMotion; }
  [TearDown] public void TearDown() { GameJuice.ReduceMotion = _reduce; }

  [Test] public void P61A_PopScalesUpThenRests() {
    GameObject go = new GameObject("P61Pop");
    go.transform.localScale = Vector3.one * 2f;
    try {
      GameJuice.Pop(go.transform, 0.2f, 0.3f);
      JuiceScaler s = go.GetComponent<JuiceScaler>();
      Assert.IsNotNull(s, "pop adds a scaler");
      s.Step(0.15f);
      Assert.Greater(go.transform.localScale.x, 2f, "peak grows the scale");
      s.Step(0.2f);
      Assert.AreEqual(2f, go.transform.localScale.x, 0.001f, "returns to rest");
      Assert.IsNull(go.GetComponent<JuiceScaler>(), "one-shot removes itself");
    } finally { Object.DestroyImmediate(go); }
  }

  [Test] public void P61B_SquashCompressesY() {
    GameObject go = new GameObject("P61Squash");
    go.transform.localScale = Vector3.one;
    try {
      GameJuice.Squash(go.transform, 0.2f, 0.3f);
      JuiceScaler s = go.GetComponent<JuiceScaler>();
      s.Step(0.1f);
      Assert.Less(go.transform.localScale.y, 1f, "squash compresses Y");
      s.Step(0.3f);
      Assert.AreEqual(1f, go.transform.localScale.y, 0.001f, "returns to rest");
    } finally { Object.DestroyImmediate(go); }
  }

  [Test] public void P61C_WobbleHonoursReduceMotion() {
    GameObject go = new GameObject("P61Wobble");
    try {
      GameJuice.Wobble(go.transform);
      Assert.IsNotNull(go.GetComponent<JuiceWobble>(), "wobble adds a driver");
      Object.DestroyImmediate(go.GetComponent<JuiceWobble>());

      GameJuice.ReduceMotion = true;
      GameJuice.Wobble(go.transform);
      Assert.IsNull(go.GetComponent<JuiceWobble>(), "reduce-motion skips the wobble");
    } finally { Object.DestroyImmediate(go); }
  }

  [Test] public void P61D_RingExpandsThenClears() {
    GameObject parent = new GameObject("P61RingParent");
    try {
      GameJuice.Ring(parent.transform, Vector3.zero, Color.white, 2f, 0.5f);
      Transform ring = parent.transform.Find("FxJuiceRing");
      Assert.IsNotNull(ring, "ring built under the parent");
      Assert.IsNull(ring.GetComponent<Collider>(), "ring is collider-free");
      JuiceRing r = ring.GetComponent<JuiceRing>();
      Assert.IsNotNull(r, "ring driver present");
      float r0 = ring.localScale.x;
      r.Step(0.25f);
      Assert.Greater(ring.localScale.x, r0, "ring expands");
      r.Step(0.3f);
      Assert.IsNull(parent.transform.Find("FxJuiceRing"), "ring clears itself");
    } finally { Object.DestroyImmediate(parent); }
  }

  [Test] public void P61E_FlashSwapsAndRestoresMaterial() {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    go.name = "P61Flash";
    try {
      Renderer rend = go.GetComponent<Renderer>();
      Material before = rend.sharedMaterial;
      GameJuice.Flash(rend, new Color(1f, 0.5f, 0.2f));
      Assert.AreNotSame(before, rend.sharedMaterial, "flash swaps in a bright material");
      JuiceFlash f = go.GetComponent<JuiceFlash>();
      Assert.IsNotNull(f, "flash driver present");
      f.Step(0.5f);
      Assert.AreSame(before, rend.sharedMaterial, "restores the original material");
      Assert.IsNull(go.GetComponent<JuiceFlash>(), "one-shot removes itself");
    } finally { Object.DestroyImmediate(go); }
  }

  [Test] public void P61F_CameraPunchHonoursReduceMotion() {
    GameObject camGo = new GameObject("P61Cam");
    camGo.tag = "MainCamera";
    camGo.AddComponent<Camera>();
    SmartCamera cam = camGo.AddComponent<SmartCamera>();
    try {
      GameJuice.ReduceMotion = true;
      GameJuice.CameraPunch(2f, 0.2f);
      Assert.IsFalse(cam.PunchActive, "reduce-motion never punches");

      GameJuice.ReduceMotion = false;
      GameJuice.CameraPunch();
      Assert.IsTrue(cam.PunchActive, "punch arms the camera shake");
    } finally { Object.DestroyImmediate(camGo); }
  }

  // ---- P2: ActivityFeedback overlay ------------------------------------------

  [TearDown] public void TearDownFeedback() { ActivityFeedback.ResetForTests(); }

  [Test] public void P61G_ProgressRowShowsDots() {
    ActivityFeedback.Progress(2, 5);
    ActivityFeedback f = ActivityFeedback.Ensure();
    Assert.IsTrue(f.ProgressVisibleForTests, "progress row is visible");
    Assert.AreEqual(5, f.DotCountForTests, "one dot per round");
    Assert.Greater(f.DotColorForTests(0).g, f.DotColorForTests(0).r, "done dot is green");
    Assert.Less(f.DotColorForTests(4).a, 0.6f, "pending dot is faded");
    ActivityFeedback.Progress(0, 0);
    Assert.IsFalse(f.ProgressVisibleForTests, "total 0 hides the row");
  }

  [Test] public void P61H_BannerShowsThenFades() {
    ActivityFeedback.Banner("GOOD!", ActivityFeedback.Kind.Correct, 1f);
    ActivityFeedback f = ActivityFeedback.Ensure();
    Assert.AreEqual("GOOD!", f.BannerTextForTests, "banner text set");
    Assert.Greater(f.BannerAlphaForTests, 0.9f, "banner starts opaque");
    f.Tick(1.5f);
    Assert.Less(f.BannerAlphaForTests, 0.05f, "banner fades out when done");
  }

  [Test] public void P61I_ClearResetsOverlay() {
    ActivityFeedback.Progress(3, 4);
    ActivityFeedback.Banner("HI", ActivityFeedback.Kind.Info, 2f);
    ActivityFeedback f = ActivityFeedback.Ensure();
    ActivityFeedback.Clear();
    Assert.IsFalse(f.ProgressVisibleForTests, "clear hides progress");
    Assert.Less(f.BannerAlphaForTests, 0.05f, "clear hides the banner");
  }

  // ---- P3: in-world ActivityGuide --------------------------------------------

  [TearDown] public void TearDownGuide() { ActivityGuide.ResetForTests(); }

  [Test] public void P61J_GuidePointsThenClears() {
    ActivityGuide.PointAt(new Vector3(3f, 0f, 4f));
    ActivityGuide g = ActivityGuide.Ensure();
    Assert.IsTrue(g.ActiveForTests, "guide is active after PointAt");
    Assert.AreEqual(3f, g.TargetForTests.x, 0.001f, "guide targets the point (x)");
    Assert.AreEqual(4f, g.TargetForTests.z, 0.001f, "guide targets the point (z)");
    ActivityGuide.Clear();
    Assert.IsFalse(g.ActiveForTests, "guide clears");
  }
}
