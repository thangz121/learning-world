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
      Assert.AreEqual(0f, cam.orbitYaw, "default orbit must be neutral");
      Assert.AreEqual(0f, cam.orbitPitch, "default orbit must be neutral");
    } finally { Object.DestroyImmediate(go); }
  }

  // ---- S4 mouse-drag orbit (user round: xoay hướng nhìn map) ------------------

  [Test] public void P32E_OrbitNeutralPreservesFraming() {
    Vector3 got = SmartCamera.OrbitOffset(Base, 0f, 0f);
    Assert.AreEqual(Base.x, got.x, 1e-5f);
    Assert.AreEqual(Base.y, got.y, 1e-5f);
    Assert.AreEqual(Base.z, got.z, 1e-5f);
  }

  [Test] public void P32F_OrbitKeepsDistanceAndRaisesPitch() {
    Vector3 o = SmartCamera.OrbitOffset(Base, 137f, 25f);
    Assert.AreEqual(Base.magnitude, o.magnitude, 1e-4f, "orbit is a rotation, not a move");
    Vector3 flat = SmartCamera.OrbitOffset(Base, 0f, 0f);
    Vector3 high = SmartCamera.OrbitOffset(Base, 0f, 30f);
    Assert.Greater(high.y, flat.y, "positive pitch raises the eye (look down)");
    Assert.AreEqual(flat.magnitude, high.magnitude, 1e-4f);
  }

  [Test] public void P32G_OrbitDragClampsPitchAndWrapsYaw() {
    Vector2 a = SmartCamera.ApplyOrbitDrag(new Vector2(0f, 0f), new Vector2(10f, 10f), 1f, -20f, 40f);
    Assert.AreEqual(10f, a.x, 1e-4f);
    Assert.AreEqual(10f, a.y, 1e-4f);
    Vector2 up = SmartCamera.ApplyOrbitDrag(new Vector2(0f, 30f), new Vector2(0f, 100f), 1f, -20f, 40f);
    Assert.AreEqual(40f, up.y, 1e-4f, "pitch clamps high");
    Vector2 down = SmartCamera.ApplyOrbitDrag(new Vector2(0f, -10f), new Vector2(0f, -100f), 1f, -20f, 40f);
    Assert.AreEqual(-20f, down.y, 1e-4f, "pitch clamps low");
    Vector2 wrapped = SmartCamera.ApplyOrbitDrag(new Vector2(170f, 0f), new Vector2(20f, 0f), 1f, -20f, 40f);
    Assert.AreEqual(-170f, wrapped.x, 1e-3f, "yaw wraps into (-180, 180]");
    Vector2 zero = SmartCamera.ApplyOrbitDrag(new Vector2(0f, 0f), Vector2.zero, 0.28f, -22f, 42f);
    Assert.AreEqual(0f, zero.x, 1e-6f);
    Assert.AreEqual(0f, zero.y, 1e-6f);
  }
}
