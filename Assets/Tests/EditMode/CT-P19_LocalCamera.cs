// CT-P19: Local PC camera — laptop cam streams into the game, USB webcam
// outranks it, phone is the fallback. Ranking is name-HEURISTIC (labels
// only); presence stays generic (any listed device counts). No hardware is
// touched: the picker is pure, services run on injected lists, the HUD is
// driven through its test seams. The phone path (CT-P17) is frozen — every
// phone string/layout assert below re-pins the old contract unchanged.
using NUnit.Framework;
using System;

public class CT_P19_LocalCamera {
  // ---------- classifier: integrated vs external ----------

  [Test] public void P19A_IntegratedNamesDetected() {
    Assert.IsTrue(LocalCameraClassifier.IsIntegrated("Integrated Camera"));
    Assert.IsTrue(LocalCameraClassifier.IsIntegrated("Built-in Camera"));
    Assert.IsTrue(LocalCameraClassifier.IsIntegrated("USB2.0 HD UVC WebCam"),
      "stock ASUS integrated module (internally USB-attached UVC)");
    Assert.IsFalse(LocalCameraClassifier.IsIntegrated("Logitech HD Pro Webcam C920"));
    Assert.IsFalse(LocalCameraClassifier.IsIntegrated("USB Camera"));
    Assert.IsFalse(LocalCameraClassifier.IsIntegrated(null));
    Assert.IsFalse(LocalCameraClassifier.IsIntegrated(""));
    Assert.IsFalse(LocalCameraClassifier.IsIntegrated("   "));
  }

  [Test] public void P19B_NoCameraPicksNull() {
    Assert.IsNull(LocalCameraClassifier.PickDevice(null));
    Assert.IsNull(LocalCameraClassifier.PickDevice(new string[0]));
    Assert.IsNull(LocalCameraClassifier.PickDevice(new[] { "", "   ", null }));
    Assert.IsFalse(LocalCameraClassifier.HasUsableCamera(null));
    Assert.IsFalse(LocalCameraClassifier.HasUsableCamera(new string[0]));
  }

  [Test] public void P19C_SingleLaptopCamPicked() {
    // The user's machine: one integrated UVC cam still beats the phone.
    Assert.AreEqual("USB2.0 HD UVC WebCam",
      LocalCameraClassifier.PickDevice(new[] { "USB2.0 HD UVC WebCam" }));
    Assert.IsTrue(LocalCameraClassifier.HasUsableCamera(new[] { "USB2.0 HD UVC WebCam" }));
  }

  [Test] public void P19D_UsbWebcamOutranksLaptopCam() {
    // Either order: the external camera wins, the laptop cam is skipped.
    Assert.AreEqual("Logitech HD Pro Webcam C920", LocalCameraClassifier.PickDevice(
      new[] { "USB2.0 HD UVC WebCam", "Logitech HD Pro Webcam C920" }));
    Assert.AreEqual("Logitech HD Pro Webcam C920", LocalCameraClassifier.PickDevice(
      new[] { "Logitech HD Pro Webcam C920", "USB2.0 HD UVC WebCam" }));
  }

  [Test] public void P19E_SameKindKeepsOrderDeterministic() {
    Assert.AreEqual("A", LocalCameraClassifier.PickDevice(new[] { "A", "B" }));
    Assert.AreEqual("Integrated Camera",
      LocalCameraClassifier.PickDevice(new[] { "Integrated Camera", "Built-in Camera" }));
    // Twice, same answer (no randomness anywhere in the picker).
    string[] two = { "USB2.0 HD UVC WebCam", "Logitech C920" };
    Assert.AreEqual(LocalCameraClassifier.PickDevice(two), LocalCameraClassifier.PickDevice(two));
  }

  [Test] public void P19F_UnknownNamesStillUsable() {
    // Odd names never exclude: ranking only matters at 2+ cameras.
    Assert.AreEqual("WeirdCam 4K", LocalCameraClassifier.PickDevice(new[] { "WeirdCam 4K" }));
    Assert.IsTrue(LocalCameraClassifier.HasUsableCamera(new[] { "WeirdCam 4K" }));
  }

  // ---------- HUD: phone contract frozen, local wins only when Live ----------

  static UnityEngine.Texture2D TestTexture() {
    var tex = new UnityEngine.Texture2D(4, 4, UnityEngine.TextureFormat.RGBA32, false);
    for (int y = 0; y < 4; y++)
      for (int x = 0; x < 4; x++)
        tex.SetPixel(x, y, new UnityEngine.Color(0.2f, 0.6f, 0.2f, 1f));
    tex.Apply();
    return tex;
  }

  static PhoneCameraHud FreshHud() {
    var go = new UnityEngine.GameObject("HudP19");
    var hud = go.AddComponent<PhoneCameraHud>();
    hud.BuildHudImmediate();
    return hud;
  }

  static void DropHud(PhoneCameraHud hud) {
    try { UnityEngine.Object.DestroyImmediate(hud.gameObject); } catch (Exception) { }
  }

  [Test] public void P19G_PhoneStringsUnchanged() {
    // Frozen phone contract (mirrors CT-P17 pins): local code must not move these.
    var hud = FreshHud();
    try {
      var tex = TestTexture();
      try {
        hud.RefreshForTests(PhoneCameraState.Live, tex);
        Assert.AreEqual("CAMERA ● LIVE", hud.StatusText);
        Assert.IsTrue(hud.IsVideoVisible);
        hud.RefreshForTests(PhoneCameraState.ConnectedWaitingFrames, null);
        Assert.AreEqual("CAMERA ○ READY", hud.StatusText);
        Assert.IsFalse(hud.IsVideoVisible);
      } finally {
        try { UnityEngine.Object.DestroyImmediate(tex); } catch (Exception) { }
      }
    } finally { DropHud(hud); }
  }

  [Test] public void P19H_LocalLiveRendersPcLabel() {
    var hud = FreshHud();
    try {
      var tex = TestTexture();
      try {
        hud.RefreshLocalForTests(PhoneCameraState.Live, tex);
        Assert.AreEqual("PC CAM ● LIVE", hud.StatusText);
        Assert.IsTrue(hud.IsVideoVisible);
      } finally {
        try { UnityEngine.Object.DestroyImmediate(tex); } catch (Exception) { }
      }
    } finally { DropHud(hud); }
  }

  [Test] public void P19I_LocalNotLiveFallsBackToPhoneStrings() {
    var hud = FreshHud();
    try {
      // Local idle/error/empty-texture never hijacks the box: phone strings show.
      hud.RefreshLocalForTests(PhoneCameraState.Connecting, null);
      Assert.AreEqual("CAMERA ○ CONNECTING", hud.StatusText);
      hud.RefreshLocalForTests(PhoneCameraState.Live, null);
      Assert.AreEqual("CAMERA ● LIVE", hud.StatusText);
      Assert.IsFalse(hud.IsVideoVisible, "Live without texture shows no frozen frame");
    } finally { DropHud(hud); }
  }

  [Test] public void P19J_HudNeverEatsClicks() {
    // No-click-eat contract holds with the local binding present too.
    var go = new UnityEngine.GameObject("HudP19Click");
    var hud = go.AddComponent<PhoneCameraHud>();
    var localGo = new UnityEngine.GameObject("LocalP19");
    var local = localGo.AddComponent<LocalCameraService>();
    try {
      hud.BuildHudImmediate();
      hud.BindLocal(local);
      foreach (var graphic in go.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
        Assert.IsFalse(graphic.raycastTarget, graphic.name);
      Assert.IsNull(go.GetComponentInChildren<UnityEngine.UI.GraphicRaycaster>());
    } finally {
      try { UnityEngine.Object.DestroyImmediate(go); } catch (Exception) { }
      try { UnityEngine.Object.DestroyImmediate(localGo); } catch (Exception) { }
    }
  }

  // ---------- service: safe without hardware ----------

  [Test] public void P19K_ServiceIdleWithoutCameraNeverThrows() {
    var go = new UnityEngine.GameObject("LocalP19Idle");
    var svc = go.AddComponent<LocalCameraService>();
    try {
      svc.SetDeviceListerForTests(() => new string[0]);
      svc.StartService();
      Assert.IsTrue(svc.IsRunning);
      Assert.IsNull(svc.CurrentTexture, "no camera => no texture, never a frozen frame");
      Assert.AreEqual(string.Empty, svc.ActiveDevice);
      Assert.IsNotEmpty(svc.StatusLine());
      svc.StopService();
      Assert.AreEqual(PhoneCameraState.Stopped, svc.State);
    } finally {
      try { UnityEngine.Object.DestroyImmediate(go); } catch (Exception) { }
    }
  }

  [Test] public void P19L_ForcePhoneDisablesLocal() {
    var go = new UnityEngine.GameObject("LocalP19Force");
    var svc = go.AddComponent<LocalCameraService>();
    try {
      svc.SetDeviceListerForTests(() => new[] { "USB2.0 HD UVC WebCam" });
      svc.SetForcePhoneForTests(true); // -e2e-nocam path without the flag
      svc.StartService();
      Assert.IsNull(svc.CurrentTexture);
    } finally {
      try { UnityEngine.Object.DestroyImmediate(go); } catch (Exception) { }
    }
  }

  // ---------- precedence: any local camera keeps the phone stood down ----------

  [Test] public void P19M_AnyLocalCameraMeansPhoneStandsDown() {
    // Single laptop cam, USB webcam, or both: the phone path stays down.
    Assert.AreEqual("cam:local", PcSourcePrecedence.PreferCam(
      LocalCameraClassifier.HasUsableCamera(new[] { "USB2.0 HD UVC WebCam" })));
    Assert.AreEqual("cam:local", PcSourcePrecedence.PreferCam(
      LocalCameraClassifier.HasUsableCamera(new[] { "Logitech C920" })));
    Assert.AreEqual("cam:phone", PcSourcePrecedence.PreferCam(
      LocalCameraClassifier.HasUsableCamera(new string[0])));
    Assert.AreEqual("cam:phone", PcSourcePrecedence.PreferCam(false, true),
      "-e2e-nocam still forces the phone path end to end");
  }

  // ---------- recording hide: one face in the file (user rule) ----------

  [Test] public void P19N_RecordingHideHoldsBoxHiddenUntilReleased() {
    var hud = FreshHud();
    try {
      var tex = TestTexture();
      try {
        hud.RefreshLocalForTests(PhoneCameraState.Live, tex);
        Assert.IsTrue(hud.IsShowing, "live box shows normally");
        Assert.IsFalse(hud.IsRecordingHidden);
        hud.SetRecordingHide(true);
        Assert.IsTrue(hud.IsRecordingHidden);
        Assert.IsFalse(hud.IsShowing, "box steps aside while recording");
        hud.RefreshLocalForTests(PhoneCameraState.Live, tex);
        Assert.IsFalse(hud.IsShowing, "state machine must not re-show mid-record");
        hud.SetRecordingHide(false);
        Assert.IsFalse(hud.IsRecordingHidden);
        hud.RefreshLocalForTests(PhoneCameraState.Live, tex);
        Assert.IsTrue(hud.IsShowing, "release resumes normal display");
      } finally {
        try { UnityEngine.Object.DestroyImmediate(tex); } catch (Exception) { }
      }
    } finally { DropHud(hud); }
  }
}
