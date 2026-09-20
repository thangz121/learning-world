// CT-P32: wheel-zoom contract (user round: free mouse-wheel zoom in Follow).
// Pins: 1.0 reproduces the frozen default framing exactly (all beat/camera
// behavior unchanged); clamp bounds hold; sign mapping (up = closer).
// C# 9.0 only.
using NUnit.Framework;
using UnityEngine;

public class CT_P32_CameraZoom {
  static readonly Vector3 Base = new Vector3(0f, 3.2f, 4.6f);

  [Test] public void P32A_DefaultZoomPreservesFraming() {
    Vector3 got = SmartCamera.ZoomedOffset(Base, 1f);
    Assert.AreEqual(Base.x, got.x, 1e-6f);
    Assert.AreEqual(Base.y, got.y, 1e-6f);
    Assert.AreEqual(Base.z, got.z, 1e-6f);
  }

  [Test] public void P32B_ZoomScalesOffset() {
    Vector3 half = SmartCamera.ZoomedOffset(Base, 0.5f);
    Assert.AreEqual(Base.magnitude * 0.5f, half.magnitude, 1e-4f);
    Vector3 far = SmartCamera.ZoomedOffset(Base, 2f);
    Assert.AreEqual(Base.magnitude * 2f, far.magnitude, 1e-4f);
  }

  [Test] public void P32C_ClampBoundsHold() {
    Assert.AreEqual(0.45f, SmartCamera.ClampZoomFactor(0.1f, 0.45f, 3.2f), 1e-6f);
    Assert.AreEqual(3.2f, SmartCamera.ClampZoomFactor(9f, 0.45f, 3.2f), 1e-6f);
    Assert.AreEqual(1.2f, SmartCamera.ClampZoomFactor(1.2f, 0.45f, 3.2f), 1e-6f);
  }

  [Test] public void P32D_DefaultsMatchFrozenFraming() {
    var go = new GameObject("P32Cam");
    try {
      SmartCamera cam = go.AddComponent<SmartCamera>();
      Assert.AreEqual(1f, cam.zoomFactor, "default zoom must be neutral (frozen beats untouched)");
      Assert.AreEqual(new Vector3(0f, 3.2f, 4.6f), cam.defaultOffset);
    } finally { Object.DestroyImmediate(go); }
  }
}
