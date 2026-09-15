// CT-P17: Phase 2.2 phone-camera stream — deterministic suite.
// Camera is ANOTHER MEDIUM over the PROVEN mic transport SHAPE, never another
// engine and never a second copy of the game: every test below pins reuse
// (same envelope, same session/presence/lifecycle patterns) AND independence
// (own port 8452, own thread, own depth-ONE slot — camera can never starve
// speech). SIMULATED = scripted JPEG bytes through the real code path;
// loopback = real TCP sockets on 127.0.0.1; REAL PHONE = user-run E2E only.
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine.UI;

public class CT_P17_PhoneCameraStream {
  // Minimal JPEG-shaped payload (magic-gated only at the source level; the
  // service decode test uses a REAL EncodeToJPG below).
  static byte[] FakeJpeg(int len, byte fill = 0x41) {
    var b = new byte[Math.Max(8, len)];
    b[0] = 0xFF; b[1] = 0xD8; b[2] = 0xFF; b[3] = 0xE0;
    for (int i = 4; i < b.Length; i++) b[i] = fill;
    return b;
  }

  static byte[] RealJpeg() {
    var tex = new UnityEngine.Texture2D(4, 4, UnityEngine.TextureFormat.RGBA32, false);
    try {
      for (int y = 0; y < 4; y++)
        for (int x = 0; x < 4; x++)
          tex.SetPixel(x, y, new UnityEngine.Color(0.8f, 0.2f, 0.2f, 1f));
      tex.Apply();
      return UnityEngine.ImageConversion.EncodeToJPG(tex, 60);
    } finally {
      UnityEngine.Object.DestroyImmediate(tex);
    }
  }

  // ---------- config (§4: small face preview, configurable, validated) ----------

  [Test] public void P17A_ConfigDefaultAndValidation() {
    var d = PhoneCameraConfig.Default;
    Assert.AreEqual(320, d.Width);
    Assert.AreEqual(240, d.Height);
    Assert.AreEqual(60, d.TargetFps);
    Assert.AreEqual(60, d.JpegQuality);
    string reason;
    Assert.IsTrue(d.Validate(out reason), reason);
    var bad = d;
    bad.Width = 1920;
    Assert.IsFalse(bad.Validate(out reason));
    Assert.IsNotEmpty(reason);
    bad = d; bad.Height = 1080;
    Assert.IsFalse(bad.Validate(out reason));
    bad = d; bad.TargetFps = 200;
    Assert.IsFalse(bad.Validate(out reason));
    bad = d; bad.JpegQuality = 95;
    Assert.IsFalse(bad.Validate(out reason));
    bad = d; bad.StaleMs = 30000;
    Assert.IsFalse(bad.Validate(out reason));
  }

  // ---------- protocol (same envelope shape as the mic path) ----------

  [Test] public void P17B_BridgeRoundTripAllKinds() {
    byte[] payload = FakeJpeg(64);
    foreach (byte kind in new[] {
        PhoneCameraProtocol.KindFrame, PhoneCameraProtocol.KindStop,
        PhoneCameraProtocol.KindError, PhoneCameraProtocol.KindHello,
        PhoneCameraProtocol.KindUp, PhoneCameraProtocol.KindDown }) {
      byte[] raw = PhoneCameraProtocol.EncodeBridgeFrame(kind, 9, 77, payload);
      PhoneCameraProtocol.BridgeFrame f;
      string reason;
      Assert.IsTrue(PhoneCameraProtocol.TryDecodeBridgeFrame(raw, 0, raw.Length, out f, out reason), reason);
      Assert.AreEqual(kind, f.Kind);
      Assert.AreEqual(9u, f.SessionSerial);
      Assert.AreEqual(77u, f.Seq);
      CollectionAssert.AreEqual(payload, f.Payload);
    }
  }

  [Test] public void P17C_BridgeRejectsGarbage() {
    PhoneCameraProtocol.BridgeFrame f;
    string reason;
    Assert.IsFalse(PhoneCameraProtocol.TryDecodeBridgeFrame(new byte[0], 0, 0, out f, out reason));
    Assert.IsNotEmpty(reason);
    byte[] truncated = PhoneCameraProtocol.EncodeBridgeFrame(PhoneCameraProtocol.KindFrame, 1, 1, FakeJpeg(32));
    Assert.IsFalse(PhoneCameraProtocol.TryDecodeBridgeFrame(truncated, 0, truncated.Length - 3, out f, out reason));
    Assert.AreEqual("truncated", reason);
    byte[] unknown = PhoneCameraProtocol.EncodeBridgeFrame(0x7F, 1, 1, new byte[0]);
    Assert.IsFalse(PhoneCameraProtocol.TryDecodeBridgeFrame(unknown, 0, unknown.Length, out f, out reason));
    StringAssert.StartsWith("unknown-kind", reason);
  }

  [Test] public void P17D_FramePayloadValidation() {
    string reason;
    Assert.IsFalse(PhoneCameraProtocol.IsValidFramePayload(null, 200 * 1024, out reason));
    Assert.IsNotEmpty(reason);
    Assert.IsFalse(PhoneCameraProtocol.IsValidFramePayload(new byte[0], 200 * 1024, out reason));
    Assert.IsFalse(PhoneCameraProtocol.IsValidFramePayload(
      new byte[] { 0x89, 0x50, 0x4E, 0x47 }, 200 * 1024, out reason), "PNG is not JPEG");
    Assert.AreEqual("not-jpeg", reason);
    Assert.IsFalse(PhoneCameraProtocol.IsValidFramePayload(FakeJpeg(8), 4, out reason));
    Assert.AreEqual("oversize-frame", reason);
    Assert.IsTrue(PhoneCameraProtocol.IsValidFramePayload(FakeJpeg(64), 200 * 1024, out reason), reason);
  }

  // ---------- source (§5: depth ONE, latest wins, never grows) ----------

  [Test] public void P17E_LatestFrameReplacesStale() {
    var src = new CameraFrameSource();
    Assert.IsTrue(src.PostFrame(1, 0, FakeJpeg(64, 0x41)));
    Assert.IsTrue(src.PostFrame(1, 1, FakeJpeg(64, 0x42)), "new frame replaces the stale one");
    uint serial, seq;
    byte[] jpeg;
    int age;
    Assert.IsTrue(src.TryTakeLatest(out serial, out seq, out jpeg, out age));
    Assert.AreEqual(1u, serial);
    Assert.AreEqual(0x42, jpeg[4], "the slot holds the NEWEST frame, not the first");
    CameraSourceStats s;
    src.ReadStats(out s);
    Assert.AreEqual(2, s.ReceivedFrames);
    Assert.AreEqual(1, s.ReplacedUnread, "intermediate drop is counted, never queued");
    Assert.AreEqual(1, s.DisplayedFrames);
  }

  [Test] public void P17F_ForeignSerialDropped() {
    var src = new CameraFrameSource();
    Assert.IsTrue(src.PostFrame(7, 0, FakeJpeg(32, 0x41)));
    Assert.IsFalse(src.PostFrame(9, 0, FakeJpeg(32, 0x99)), "old/new session bytes never merge");
    uint serial, seq;
    byte[] jpeg;
    int age;
    Assert.IsTrue(src.TryTakeLatest(out serial, out seq, out jpeg, out age));
    Assert.AreEqual(7u, serial);
    Assert.AreEqual(0x41, jpeg[4]);
    CameraSourceStats s;
    src.ReadStats(out s);
    Assert.AreEqual(1, s.DroppedForeign);
    Assert.AreEqual(1, s.ReceivedFrames);
  }

  [Test] public void P17G_InvalidFramesDroppedAndCounted() {
    var src = new CameraFrameSource();
    Assert.IsFalse(src.PostFrame(1, 0, new byte[0]));
    Assert.IsFalse(src.PostFrame(1, 1, new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
    Assert.IsFalse(src.PostFrame(1, 2, null));
    CameraSourceStats s;
    src.ReadStats(out s);
    Assert.AreEqual(3, s.DroppedInvalid);
    Assert.AreEqual(0, s.ReceivedFrames);
    uint serial, seq;
    byte[] jpeg;
    int age;
    Assert.IsFalse(src.TryTakeLatest(out serial, out seq, out jpeg, out age), "nothing valid ever landed");
  }

  [Test] public void P17H_StopClearsSlotNoFrozenFace() {
    var src = new CameraFrameSource();
    Assert.IsTrue(src.PostFrame(3, 0, FakeJpeg(32)));
    src.OnStopped(3);
    uint serial, seq;
    byte[] jpeg;
    int age;
    Assert.IsFalse(src.TryTakeLatest(out serial, out seq, out jpeg, out age),
      "STOP must clear the slot — the HUD never shows a frozen old face");
    CameraSourceStats s;
    src.ReadStats(out s);
    Assert.AreEqual(1, s.StopCount);
    // Foreign STOP is dropped, never clears the live session.
    Assert.IsTrue(src.PostFrame(3, 1, FakeJpeg(32)));
    src.OnStopped(99);
    Assert.IsTrue(src.TryTakeLatest(out serial, out seq, out jpeg, out age));
    src.ReadStats(out s);
    Assert.AreEqual(1, s.DroppedForeign);
  }

  [Test] public void P17I_ResetStartsFreshEpoch() {
    var src = new CameraFrameSource();
    Assert.IsTrue(src.PostFrame(5, 0, FakeJpeg(32, 0x41)));
    src.ResetSession(); // DOWN / transport loss / reconnect (§20)
    Assert.IsFalse(src.IsLatched);
    Assert.IsTrue(src.PostFrame(5, 0, FakeJpeg(32, 0x42)),
      "same serial after a reset is a NEW epoch, not foreign");
    uint serial, seq;
    byte[] jpeg;
    int age;
    Assert.IsTrue(src.TryTakeLatest(out serial, out seq, out jpeg, out age));
    Assert.AreEqual(0x42, jpeg[4], "stale bytes never survive a reconnect");
  }

  [Test] public void P17J_FreshnessHonesty() {
    var src = new CameraFrameSource();
    int age;
    Assert.IsFalse(src.HasFreshFrame(3000, out age), "no frame = never fresh");
    Assert.AreEqual(-1, age);
    Assert.IsTrue(src.PostFrame(1, 0, FakeJpeg(32)));
    Assert.IsTrue(src.HasFreshFrame(30000, out age), "just-posted frame is fresh in-window");
    Assert.GreaterOrEqual(age, 0);
    Assert.IsFalse(src.HasFreshFrame(0, out age) && age > 0, "zero window expires immediately");
    src.OnError("denied");
    Assert.IsFalse(src.HasFreshFrame(30000, out age), "error clears the slot");
    CameraSourceStats s;
    src.ReadStats(out s);
    Assert.AreEqual(1, s.ErrorCount);
    Assert.IsNotEmpty(src.LastError);
  }

  // ---------- state machine (§10: never pretend live) ----------

  [Test] public void P17K_NextStateHonestyTable() {
    Assert.AreEqual(PhoneCameraState.Stopped,
      GameCameraStreamService.NextStateAfterFreshness(false, true, true, true, PhoneCameraState.Live));
    Assert.AreEqual(PhoneCameraState.Live,
      GameCameraStreamService.NextStateAfterFreshness(true, true, true, true, PhoneCameraState.ConnectedWaitingFrames));
    Assert.AreEqual(PhoneCameraState.ConnectedWaitingFrames,
      GameCameraStreamService.NextStateAfterFreshness(true, true, true, false, PhoneCameraState.Live),
      "stale Live downgrades, texture gate hides the old face");
    Assert.AreEqual(PhoneCameraState.TempDisconnected,
      GameCameraStreamService.NextStateAfterFreshness(true, false, false, false, PhoneCameraState.Live),
      "transport loss is TempDisconnected, never frozen Live");
    Assert.AreEqual(PhoneCameraState.Error,
      GameCameraStreamService.NextStateAfterFreshness(true, true, true, false, PhoneCameraState.Error),
      "error sticks until a good edge recovers it");
  }

  [Test] public void P17L_ServiceEdgeTransitions() {
    var go = new UnityEngine.GameObject("CamSvcL");
    var svc = go.AddComponent<GameCameraStreamService>();
    try {
      svc.Bind("127.0.0.1", 1, PhoneCameraConfig.Default);
      svc.StartService();
      Assert.AreEqual(PhoneCameraState.WaitingForPhone, svc.State);
      svc.ApplyEdgeForTests(CameraWatcherEvent.TransportOk, 0, string.Empty);
      Assert.AreEqual(PhoneCameraState.Connecting, svc.State);
      svc.ApplyEdgeForTests(CameraWatcherEvent.CameraUp, 0, "ws-connected");
      Assert.AreEqual(PhoneCameraState.ConnectedWaitingFrames, svc.State);
      Assert.IsNull(svc.CurrentTexture, "no frame yet = no texture, never a stale image");
      svc.ApplyEdgeForTests(CameraWatcherEvent.CameraDown, 0, "ws-closed");
      Assert.AreEqual(PhoneCameraState.TempDisconnected, svc.State);
      svc.ApplyEdgeForTests(CameraWatcherEvent.CameraError, 0, "denied");
      Assert.AreEqual(PhoneCameraState.Error, svc.State);
      StringAssert.Contains("denied", svc.StatusLine());
      svc.StopService();
      Assert.AreEqual(PhoneCameraState.Stopped, svc.State);
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  [Test] public void P17L2_ServiceDecodesRealJpeg() {
    var go = new UnityEngine.GameObject("CamSvcJpg");
    var svc = go.AddComponent<GameCameraStreamService>();
    try {
      svc.Bind("127.0.0.1", 1, PhoneCameraConfig.Default);
      svc.StartService();
      byte[] jpg = RealJpeg();
      Assert.Greater(jpg.Length, 100, "real encoder output");
      Assert.IsTrue(svc.DecodeForTests(jpg), "valid JPEG decodes to Live");
      Assert.AreEqual(PhoneCameraState.Live, svc.State);
      Assert.IsNotNull(svc.CurrentTexture, "Live exposes the reused texture");
      var first = svc.CurrentTexture;
      // Second frame reuses the SAME texture object (no per-frame allocation).
      Assert.IsTrue(svc.DecodeForTests(jpg));
      Assert.AreSame(first, svc.CurrentTexture, "texture object reused across frames");
      long rx, shown, fails;
      float ms, fps;
      svc.ReadServiceStats(out rx, out shown, out fails, out ms, out fps);
      Assert.AreEqual(2, rx);
      Assert.AreEqual(2, shown);
      Assert.AreEqual(0, fails);
      // Corrupt-but-magic-shaped bytes fail honestly, never crash: the decode
      // counter moves, Live is NOT re-announced by the bad frame (the stale
      // gate owns downgrade, so one corrupt frame never kills the stream).
      var bad = FakeJpeg(64);
      long failsBefore;
      svc.ReadServiceStats(out rx, out shown, out failsBefore, out ms, out fps);
      svc.DecodeForTests(bad);
      svc.ReadServiceStats(out rx, out shown, out fails, out ms, out fps);
      Assert.AreEqual(failsBefore + 1, fails, "corrupt JPEG counted as a decode failure");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // ---------- HUD (§9: small top-right box, secondary, never eats clicks) ----------

  [Test] public void P17M_HudLayoutBottomLeft() {
    var hud = new UnityEngine.GameObject("CamHudM").AddComponent<PhoneCameraHud>();
    try {
      hud.BuildHudImmediate();
      var box = hud.transform.Find("PhoneCameraRoot/Box");
      Assert.IsNotNull(box, "Box owns all children (root stays fullscreen)");
      var boxRt = box.GetComponent<UnityEngine.RectTransform>();
      Assert.AreEqual(new UnityEngine.Vector2(0f, 0f), boxRt.anchorMin, "pinned bottom-left");
      Assert.AreEqual(new UnityEngine.Vector2(0f, 0f), boxRt.anchorMax, "pinned bottom-left");
      Assert.AreEqual(PhoneCameraHud.BoxSize, boxRt.sizeDelta, "small secondary box, never dominant");
      Assert.AreEqual(PhoneCameraHud.BoxPosition, boxRt.anchoredPosition, "bottom-left corner (20,20)");
      Assert.LessOrEqual(boxRt.sizeDelta.x, 240f, "never the dominant visual");
      Assert.LessOrEqual(boxRt.sizeDelta.y, 220f);
      foreach (UnityEngine.Transform t in hud.transform.Find("PhoneCameraRoot").GetComponentsInChildren<UnityEngine.Transform>(true)) {
        Assert.IsInstanceOf<UnityEngine.RectTransform>(t,
          t.gameObject.name + " must be a RectTransform (plain middlemen misplace children)");
      }
      var canvas = hud.transform.Find("PhoneCameraRoot").GetComponent<UnityEngine.Canvas>();
      Assert.AreEqual(PhoneCameraHud.CanvasOrder, canvas.sortingOrder);
      var raycasters = hud.GetComponentsInChildren<GraphicRaycaster>(true);
      Assert.AreEqual(0, raycasters.Length, "no GraphicRaycaster in the widget");
      foreach (var g in hud.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) {
        Assert.IsFalse(g.raycastTarget, g.gameObject.name + " must not intercept clicks");
      }
    } finally {
      UnityEngine.Object.DestroyImmediate(hud.gameObject);
    }
  }

  [Test] public void P17M2_HudStateRendering() {
    var hud = new UnityEngine.GameObject("CamHudS").AddComponent<PhoneCameraHud>();
    try {
      hud.RefreshForTests(PhoneCameraState.WaitingForPhone, null);
      Assert.IsFalse(hud.IsVideoVisible, "no frame = video hidden, placeholder instead");
      StringAssert.Contains("WAITING", hud.StatusText);
      hud.RefreshForTests(PhoneCameraState.TempDisconnected, null);
      Assert.IsFalse(hud.IsVideoVisible, "lost link never shows a frozen face");
      StringAssert.Contains("LOST", hud.StatusText);
      var tex = new UnityEngine.Texture2D(2, 2);
      try {
        hud.RefreshForTests(PhoneCameraState.Live, tex);
        Assert.IsTrue(hud.IsVideoVisible, "Live + texture = video shown");
        StringAssert.Contains("LIVE", hud.StatusText);
        // A stale texture passed with a non-live state stays hidden.
        hud.RefreshForTests(PhoneCameraState.TempDisconnected, tex);
        Assert.IsFalse(hud.IsVideoVisible, "non-live state hides even a valid texture");
      } finally {
        UnityEngine.Object.DestroyImmediate(tex);
      }
    } finally {
      UnityEngine.Object.DestroyImmediate(hud.gameObject);
    }
  }

  // ---------- loopback (real sockets, deterministic JPEG delivery) ----------

  static void WriteCamFrame(NetworkStream s, byte kind, uint serial, uint seq, byte[] payload) {
    int bodyLen = 9 + (payload != null ? payload.Length : 0);
    var frame = new byte[4 + bodyLen];
    frame[0] = (byte)((bodyLen >> 24) & 0xFF);
    frame[1] = (byte)((bodyLen >> 16) & 0xFF);
    frame[2] = (byte)((bodyLen >> 8) & 0xFF);
    frame[3] = (byte)(bodyLen & 0xFF);
    frame[4] = kind;
    frame[5] = (byte)((serial >> 24) & 0xFF);
    frame[6] = (byte)((serial >> 16) & 0xFF);
    frame[7] = (byte)((serial >> 8) & 0xFF);
    frame[8] = (byte)(serial & 0xFF);
    frame[9] = (byte)((seq >> 24) & 0xFF);
    frame[10] = (byte)((seq >> 16) & 0xFF);
    frame[11] = (byte)((seq >> 8) & 0xFF);
    frame[12] = (byte)(seq & 0xFF);
    if (payload != null && payload.Length > 0)
      Buffer.BlockCopy(payload, 0, frame, 13, payload.Length);
    s.Write(frame, 0, frame.Length);
    s.Flush();
  }

  [Test] public void P17N_WatcherDeliversJpegOverLoopback() {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    byte[] jpeg = FakeJpeg(128);
    var server = Task.Run(() => {
      using (TcpClient c = listener.AcceptTcpClient())
      using (NetworkStream s = c.GetStream()) {
        var tmp = new byte[64];
        Assert.Greater(s.Read(tmp, 0, tmp.Length), 0, "watcher subscribes first");
        WriteCamFrame(s, 4, 0, 0, new byte[0]); // HELLO greeting
        WriteCamFrame(s, 5, 0, 0,
          System.Text.Encoding.UTF8.GetBytes("ws-connected")); // UP
        WriteCamFrame(s, 1, 11, 0, jpeg); // FRAME serial 11
        WriteCamFrame(s, 1, 11, 1, new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // non-JPEG: dropped
        WriteCamFrame(s, 1, 99, 2, FakeJpeg(64)); // foreign serial: dropped
        WriteCamFrame(s, 2, 11, 3, new byte[0]); // STOP clears the slot
        Task.Delay(2500).GetAwaiter().GetResult(); // hold open for the drain
      }
    });
    var source = new CameraFrameSource();
    var watcher = new PhoneCameraWatcher("127.0.0.1", port, source);
    try {
      watcher.Start();
      // Edges are latest-wins (a FRAME burst collapses into the trailing STOP
      // in microseconds), so delivery is proven by COUNTERS, not by catching
      // the transient CameraFrame edge mid-flight. STOP is last and stable.
      DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(12);
      long up = 0, frames = 0, stop = 0, down = 0;
      while (DateTime.UtcNow < deadline) {
        watcher.ReadCounters(out up, out frames, out stop, out down);
        if (frames >= 1) break;
        Task.Delay(50).GetAwaiter().GetResult();
      }
      Assert.GreaterOrEqual(frames, 1, "JPEG frame delivered over real sockets");
      deadline = DateTime.UtcNow + TimeSpan.FromSeconds(12);
      int seq = 0;
      CameraWatcherEvent ev = CameraWatcherEvent.None;
      uint serial = 0;
      string reason = string.Empty;
      bool sawStop = false;
      while (DateTime.UtcNow < deadline && !sawStop) {
        watcher.ReadState(out seq, out ev, out serial, out reason);
        if (ev == CameraWatcherEvent.CameraStopped) sawStop = true;
        Task.Delay(50).GetAwaiter().GetResult();
      }
      Assert.IsTrue(sawStop, "STOP edge delivered over real sockets");
      CameraSourceStats st;
      source.ReadStats(out st);
      Assert.AreEqual(1, st.ReceivedFrames, "only the valid latched frame accepted");
      Assert.AreEqual(1, st.DroppedInvalid, "non-JPEG dropped");
      Assert.AreEqual(1, st.DroppedForeign, "foreign serial dropped");
      watcher.ReadCounters(out up, out frames, out stop, out down);
      Assert.AreEqual(1, up, "UP edge counted");
      Assert.GreaterOrEqual(stop, 1, "STOP edge counted");
      long fcount, fbytes;
      int age;
      watcher.ReadFrameStats(out fcount, out fbytes, out age);
      Assert.AreEqual(1, fcount);
      Assert.AreEqual(jpeg.Length, fbytes);
    } finally {
      watcher.Dispose();
    }
    Assert.IsTrue(server.Wait(10000), "server finished");
    listener.Stop();
  }

  // ---------- audio regression (§8/§28: mic path untouched by camera) ----------

  [Test] public void P17O_MicPathUntouchedByCameraKinds() {
    // Same numeric envelope shape, separate sockets: a camera FRAME and a mic
    // AUDIO frame are byte-identical at kind=1 — the PORT decides the meaning.
    // This pins that neither decoder rejects the other's valid frame shape.
    byte[] payload = { 0x01, 0x02, 0x03, 0x04 };
    byte[] micRaw = PhoneMicProtocol.EncodeBridgeFrame(PhoneMicProtocol.KindAudio, 7, 42, payload);
    byte[] camRaw = PhoneCameraProtocol.EncodeBridgeFrame(PhoneCameraProtocol.KindFrame, 7, 42, payload);
    CollectionAssert.AreEqual(micRaw, camRaw, "shared envelope shape is intentional");
    PhoneMicProtocol.BridgeFrame mf;
    string mreason;
    Assert.IsTrue(PhoneMicProtocol.TryDecodeBridgeFrame(micRaw, 0, micRaw.Length, out mf, out mreason));
    Assert.AreEqual(PhoneMicProtocol.KindAudio, mf.Kind);
    PhoneCameraProtocol.BridgeFrame cf;
    string creason;
    Assert.IsTrue(PhoneCameraProtocol.TryDecodeBridgeFrame(camRaw, 0, camRaw.Length, out cf, out creason));
    Assert.AreEqual(PhoneCameraProtocol.KindFrame, cf.Kind);
    // Canonical mic contract still holds (camera added no audio behavior).
    Assert.IsTrue(PhoneMicProtocol.IsCanonicalFormat(16000, 1, "pcm16"));
    Assert.AreNotEqual(PhoneMicProtocol.DefaultBridgePort, PhoneCameraProtocol.DefaultBridgePort,
      "separate ports: JPEGs can never head-of-line-block speech");
  }
}
