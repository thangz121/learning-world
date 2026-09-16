// CT-P16: mic status-HUD — measured bars / headphone / data dot / cross.
// Pins that the corner widget renders MEASUREMENTS, never decoration:
//   bars  <- MicSignal.ComputeLevel over real payload energy (thresholds
//            derived from TooWeak 0.005 + ambient 0.0259 + speech >= 0.05)
//   dot   <- AUDIO payload bytes fresh within DataFreshMs (content flowing)
//            vs control-only/idle (red); local path = presence sampling
//   cross <- link down only; idle-but-linked shows grey, never crossed.
// Loopback socket tests mirror P15R (real TCP, fake bridge server).
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine.UI;

public class CT_P16_MicStatusHud {
  // ---------- pure signal language ----------

  [Test] public void P16A_LevelBoundaries() {
    Assert.AreEqual(SignalLevel.None, MicSignal.ComputeLevel(0f), "zero = no measurement");
    Assert.AreEqual(SignalLevel.Weak, MicSignal.ComputeLevel(0.004f), "VAD floor reads weak");
    Assert.AreEqual(SignalLevel.Weak, MicSignal.ComputeLevel(0.0099f));
    Assert.AreEqual(SignalLevel.Medium, MicSignal.ComputeLevel(0.01f), "band edge inclusive");
    Assert.AreEqual(SignalLevel.Medium, MicSignal.ComputeLevel(0.0259f), "ambient room = medium");
    Assert.AreEqual(SignalLevel.Medium, MicSignal.ComputeLevel(0.0499f));
    Assert.AreEqual(SignalLevel.Strong, MicSignal.ComputeLevel(0.05f), "speech band");
    Assert.AreEqual(SignalLevel.Strong, MicSignal.ComputeLevel(0.3f));
    Assert.AreEqual(SignalLevel.None, MicSignal.ComputeLevel(-1f));
    Assert.AreEqual(SignalLevel.None, MicSignal.ComputeLevel(float.NaN));
  }

  [Test] public void P16B_DataFreshness() {
    Assert.IsFalse(MicSignal.IsDataFlowing(-1), "never = red dot");
    Assert.IsTrue(MicSignal.IsDataFlowing(0));
    Assert.IsTrue(MicSignal.IsDataFlowing(MicSignal.DataFreshMs), "window edge inclusive");
    Assert.IsFalse(MicSignal.IsDataFlowing(MicSignal.DataFreshMs + 1), "stale = red dot");
  }

  [Test] public void P16C_PeakHoldDecay() {
    Assert.AreEqual(0.2f, MicSignal.ApplyDecay(0.05f, 0.2f, 1f), 0.0001f, "rises instantly");
    Assert.AreEqual(0.2f, MicSignal.ApplyDecay(0.2f, 0.2f, 1f), 0.0001f, "holds at target");
    float after1s = MicSignal.ApplyDecay(0.2f, 0f, 1f);
    Assert.AreEqual(0.2f - MicSignal.DecayPerSec, after1s, 0.0001f, "falls linearly");
    Assert.AreEqual(0f, MicSignal.ApplyDecay(0.01f, 0f, 10f), 0.0001f, "clamps at target");
    Assert.AreEqual(0.2f, MicSignal.ApplyDecay(0.2f, 0f, 0f), 0.0001f, "no time = hold");
    Assert.AreEqual(0f, MicSignal.ApplyDecay(float.NaN, float.NaN, 1f), 0.0001f, "NaN-safe");
  }

  [Test] public void P16D_BarMapping() {
    int lit; MicBarColor color; bool crossed;
    MicSignal.ComputeBars(SignalLevel.Weak, true, out lit, out color, out crossed);
    Assert.AreEqual(1, lit); Assert.AreEqual(MicBarColor.Red, color); Assert.IsFalse(crossed);
    MicSignal.ComputeBars(SignalLevel.Medium, true, out lit, out color, out crossed);
    Assert.AreEqual(2, lit); Assert.AreEqual(MicBarColor.Yellow, color); Assert.IsFalse(crossed);
    MicSignal.ComputeBars(SignalLevel.Strong, true, out lit, out color, out crossed);
    Assert.AreEqual(3, lit); Assert.AreEqual(MicBarColor.Green, color); Assert.IsFalse(crossed);
    MicSignal.ComputeBars(SignalLevel.None, true, out lit, out color, out crossed);
    Assert.AreEqual(0, lit); Assert.AreEqual(MicBarColor.Grey, color); Assert.IsFalse(crossed, "idle linked = grey, never crossed");
    MicSignal.ComputeBars(SignalLevel.Strong, false, out lit, out color, out crossed);
    Assert.AreEqual(0, lit); Assert.IsTrue(crossed, "down always crosses, even mid-speech");
    MicSignal.ComputeBars(SignalLevel.None, false, out lit, out color, out crossed);
    Assert.IsTrue(crossed);
  }

  // ---------- laptop built-in detection (req-3) ----------

  [Test] public void P16E_LaptopBuiltInMicDetected() {
    string[] laptopNames = {
      "Microphone Array (Realtek(R) Audio)",
      "Microphone (Realtek High Definition Audio)",
      "Conexant SmartAudio HD",
      "Built-in Microphone"
    };
    foreach (string name in laptopNames) {
      Assert.AreEqual(MicDeviceKind.BuiltInMic, MicDeviceClassifier.Classify(name), name);
      Assert.IsTrue(MicDeviceClassifier.HasUsableMic(new[] { name }), name + " counts as usable");
    }
    // Gate consequence: built-in listed => ReadyLocal, never the phone offer.
    var local = new MicrophoneDeviceService(() => new[] { "Microphone Array (Realtek(R) Audio)" }, null);
    var gate = new MicSetupGate(local, new PhoneMicrophoneDevice());
    Assert.AreEqual(MicSetupState.ReadyLocal, gate.EvaluateAtStartup());
  }

  // ---------- HUD rendering ----------

  static MicSignalSnapshot Snap(MicSignalSource src, SignalLevel lvl, bool flowing, bool up) {
    return new MicSignalSnapshot {
      Source = src, Level = lvl, DataFlowing = flowing, LinkUp = up, Energy = 0f
    };
  }

  [Test] public void P16F_PhoneStrongFlowing() {
    var hud = new UnityEngine.GameObject("HudF").AddComponent<MicStatusHud>();
    try {
      hud.RefreshForTests(Snap(MicSignalSource.Phone, SignalLevel.Strong, true, true));
      Assert.IsTrue(hud.IsShowing);
      Assert.IsTrue(hud.IsBarsVisible);
      Assert.IsFalse(hud.IsHeadphoneVisible, "phone mode shows bars, not headphone");
      Assert.AreEqual(3, hud.LitBars);
      Assert.AreEqual(MicStatusHud.DotGreen, hud.DotColor, "data flowing = green dot");
      Assert.IsFalse(hud.IsCrossVisible);
      Assert.IsFalse(hud.IsDotBeside, "phone dot sits UNDER the bars");
    } finally {
      UnityEngine.Object.DestroyImmediate(hud.gameObject);
    }
  }

  [Test] public void P16G_PhoneIdleLinked() {
    var hud = new UnityEngine.GameObject("HudG").AddComponent<MicStatusHud>();
    try {
      hud.RefreshForTests(Snap(MicSignalSource.Phone, SignalLevel.None, false, true));
      Assert.AreEqual(0, hud.LitBars, "no measurement = unlit bars");
      Assert.AreEqual(MicStatusHud.DotRed, hud.DotColor, "control-only = red dot");
      Assert.IsFalse(hud.IsCrossVisible, "linked-but-idle is grey, NOT crossed");
    } finally {
      UnityEngine.Object.DestroyImmediate(hud.gameObject);
    }
  }

  [Test] public void P16H_PhoneLinkDown() {
    var hud = new UnityEngine.GameObject("HudH").AddComponent<MicStatusHud>();
    try {
      hud.RefreshForTests(Snap(MicSignalSource.Phone, SignalLevel.Weak, false, false));
      Assert.IsTrue(hud.IsCrossVisible, "down = red cross slash");
      Assert.AreEqual(MicStatusHud.DotRed, hud.DotColor);
      Assert.AreEqual(0, hud.LitBars);
    } finally {
      UnityEngine.Object.DestroyImmediate(hud.gameObject);
    }
  }

  [Test] public void P16I_LocalHeadphone() {
    var hud = new UnityEngine.GameObject("HudI").AddComponent<MicStatusHud>();
    try {
      hud.RefreshForTests(Snap(MicSignalSource.Local, SignalLevel.None, true, true));
      Assert.IsTrue(hud.IsHeadphoneVisible, "local mode = headphone, not bars");
      Assert.IsFalse(hud.IsBarsVisible);
      Assert.IsFalse(hud.IsCrossVisible);
      Assert.IsTrue(hud.IsDotBeside, "local dot sits BESIDE the icon");
      Assert.AreEqual(MicStatusHud.DotGreen, hud.DotColor);
      hud.RefreshForTests(Snap(MicSignalSource.Local, SignalLevel.None, false, true));
      Assert.AreEqual(MicStatusHud.DotRed, hud.DotColor, "presence lost = red dot");
      Assert.IsTrue(hud.IsHeadphoneVisible, "icon stays while local path exists");
    } finally {
      UnityEngine.Object.DestroyImmediate(hud.gameObject);
    }
  }

  [Test] public void P16J_HudNeverEatsClicks() {
    var hud = new UnityEngine.GameObject("HudJ").AddComponent<MicStatusHud>();
    try {
      hud.RefreshForTests(Snap(MicSignalSource.Phone, SignalLevel.Strong, true, true));
      var raycasters = hud.GetComponentsInChildren<GraphicRaycaster>(true);
      Assert.AreEqual(0, raycasters.Length, "no GraphicRaycaster in the widget");
      foreach (Image img in hud.GetComponentsInChildren<Image>(true)) {
        Assert.IsFalse(img.raycastTarget, img.gameObject.name + " must not intercept clicks");
      }
    } finally {
      UnityEngine.Object.DestroyImmediate(hud.gameObject);
    }
  }

  [Test] public void P16N_HudBoxLayoutTopRight() {
    var hud = new UnityEngine.GameObject("HudN").AddComponent<MicStatusHud>();
    try {
      hud.BuildHudImmediate();
      var box = hud.transform.Find("MicStatusRoot/Box");
      Assert.IsNotNull(box, "Box owns all children (root stays fullscreen)");
      var boxRt = box.GetComponent<UnityEngine.RectTransform>();
      Assert.IsNotNull(boxRt);
      Assert.AreEqual(new UnityEngine.Vector2(1f, 1f), boxRt.anchorMax, "pinned top-right");
      Assert.AreEqual(new UnityEngine.Vector2(140f, 170f), boxRt.sizeDelta);
      foreach (UnityEngine.Transform t in hud.transform.Find("MicStatusRoot").GetComponentsInChildren<UnityEngine.Transform>(true)) {
        Assert.IsInstanceOf<UnityEngine.RectTransform>(t,
          t.gameObject.name + " must be a RectTransform (plain middlemen misplace children)");
      }
      var slash0 = hud.transform.Find("MicStatusRoot/Box/Bars/Cross/Slash0");
      Assert.IsNotNull(slash0, "cross resolves inside the Box");
      var slashRt = slash0.GetComponent<UnityEngine.RectTransform>();
      Assert.GreaterOrEqual(slashRt.anchoredPosition.x, 0f);
      Assert.LessOrEqual(slashRt.anchoredPosition.x, 140f);
      Assert.GreaterOrEqual(slashRt.anchoredPosition.y, 0f);
      Assert.LessOrEqual(slashRt.anchoredPosition.y, 170f);
    } finally {
      UnityEngine.Object.DestroyImmediate(hud.gameObject);
    }
  }

  // ---------- measured watcher stats (req-1/2, real loopback) ----------

  static void WriteFrame(NetworkStream s, byte kind, uint serial, uint seq, byte[] payload) {
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

  // 800 samples of constant 0.5 (int16 16384 LE) => mean-abs energy exactly 0.5.
  static byte[] TonePayload() {
    var pcm = new byte[1600];
    for (int i = 0; i < 800; i++) {
      pcm[i * 2] = 0x00;
      pcm[i * 2 + 1] = 0x40;
    }
    return pcm;
  }

  [Test] public void P16K_WatcherMeasuresAudioContent() {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var server = Task.Run(() => {
      using (TcpClient c = listener.AcceptTcpClient())
      using (NetworkStream s = c.GetStream()) {
        var tmp = new byte[64];
        Assert.Greater(s.Read(tmp, 0, tmp.Length), 0, "watcher subscribes first");
        WriteFrame(s, 1, 7, 0, TonePayload()); // AUDIO serial 7, real content
        WriteFrame(s, 1, 7, 1, new byte[0]);   // AUDIO serial 7, empty payload
        Task.Delay(2500).GetAwaiter().GetResult(); // hold open for the drain
      }
    });
    var watcher = new PhonePresenceWatcher("127.0.0.1", port);
    try {
      watcher.Start();
      DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
      long frames = 0, bytes = 0;
      float energy = 0f;
      int lastBytes = -1, age = -1;
      while (DateTime.UtcNow < deadline) {
        watcher.ReadAudioStats(out frames, out bytes, out energy, out lastBytes, out age);
        if (frames >= 2 && bytes >= 1600) break;
        Task.Delay(50).GetAwaiter().GetResult();
      }
      watcher.ReadAudioStats(out frames, out bytes, out energy, out lastBytes, out age);
      Assert.GreaterOrEqual(frames, 2, "both AUDIO frames counted");
      Assert.GreaterOrEqual(bytes, 1600, "payload bytes counted (content, not control)");
      Assert.AreEqual(0, lastBytes, "latest payload was empty => dot evidence is red");
      Assert.GreaterOrEqual(age, 0, "age measured");
      Assert.LessOrEqual(age, MicSignal.DataFreshMs + 2500, "age within the session window");
    } finally {
      watcher.Dispose();
    }
    Assert.IsTrue(server.Wait(10000), "server finished");
    listener.Stop();
  }

  [Test] public void P16K2_WatcherEnergyMatchesPayload() {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var server = Task.Run(() => {
      using (TcpClient c = listener.AcceptTcpClient())
      using (NetworkStream s = c.GetStream()) {
        var tmp = new byte[64];
        s.Read(tmp, 0, tmp.Length);
        WriteFrame(s, 1, 3, 0, TonePayload()); // single tone frame, then hold
        Task.Delay(2500).GetAwaiter().GetResult();
      }
    });
    var watcher = new PhonePresenceWatcher("127.0.0.1", port);
    try {
      watcher.Start();
      DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
      long frames = 0, bytes = 0;
      float energy = 0f;
      int lastBytes = 0, age = -1;
      while (DateTime.UtcNow < deadline) {
        watcher.ReadAudioStats(out frames, out bytes, out energy, out lastBytes, out age);
        if (frames >= 1 && lastBytes >= 1600) break;
        Task.Delay(50).GetAwaiter().GetResult();
      }
      watcher.ReadAudioStats(out frames, out bytes, out energy, out lastBytes, out age);
      Assert.AreEqual(0.5f, energy, 0.001f, "measured energy equals the injected tone");
      Assert.AreEqual(SignalLevel.Strong, MicSignal.ComputeLevel(energy), "tone lights Strong");
      Assert.IsTrue(MicSignal.IsDataFlowing(age) && lastBytes > 0, "green-dot evidence");
    } finally {
      watcher.Dispose();
    }
    Assert.IsTrue(server.Wait(10000), "server finished");
    listener.Stop();
  }

  // ---------- monitor snapshot wiring ----------

  static MicrophoneDeviceService LocalWith(params string[] names) {
    string[] snapshot = (string[])names.Clone();
    return new MicrophoneDeviceService(() => (string[])snapshot.Clone(), null);
  }

  [Test] public void P16L_SnapshotLocalHeadphone() {
    var local = LocalWith("Microphone Array (Realtek(R) Audio)");
    var phone = new PhoneMicrophoneDevice();
    var gate = new MicSetupGate(local, phone);
    gate.EvaluateAtStartup();
    Assert.AreEqual(MicSetupState.ReadyLocal, gate.State);
    var monitor = new UnityEngine.GameObject("MonL").AddComponent<MicSetupMonitor>();
    try {
      monitor.Bind(gate, local, phone, null, "127.0.0.1", 8451);
      MicSignalSnapshot snap = monitor.CurrentSignal;
      Assert.AreEqual(MicSignalSource.Local, snap.Source, "local wins for the icon");
      Assert.IsTrue(snap.LinkUp);
      Assert.IsTrue(snap.DataFlowing, "presence sampling fresh at bind");
    } finally {
      UnityEngine.Object.DestroyImmediate(monitor.gameObject);
    }
  }

  [Test] public void P16M_SnapshotPhoneNoMeasurementYet() {
    var local = LocalWith();
    var phone = new PhoneMicrophoneDevice();
    var gate = new MicSetupGate(local, phone);
    gate.EvaluateAtStartup();
    Assert.AreEqual(MicSetupState.OfferPhone, gate.State);
    gate.AcceptPhoneOffer();
    phone.ReportLinkUp("phone");
    gate.OnPhoneLink(PhoneLinkState.PhoneLinked);
    Assert.AreEqual(MicSetupState.ReadyPhone, gate.State);
    var monitor = new UnityEngine.GameObject("MonM").AddComponent<MicSetupMonitor>();
    try {
      monitor.Bind(gate, local, phone, null, "127.0.0.1", 8451);
      MicSignalSnapshot snap = monitor.CurrentSignal;
      Assert.AreEqual(MicSignalSource.Phone, snap.Source);
      Assert.IsTrue(snap.LinkUp);
      Assert.IsFalse(snap.DataFlowing, "no AUDIO payload observed yet => red dot (honest)");
      Assert.AreEqual(SignalLevel.None, snap.Level, "no measurement => unlit bars");
    } finally {
      UnityEngine.Object.DestroyImmediate(monitor.gameObject);
    }
  }

  [Test] public void P16O_HeadphoneUpright() {
    // Regression: the band arc was drawn in the lower half (upside-down).
    // Upright = white arc pixels up top, empty rows at the very bottom.
    UnityEngine.Sprite sprite = MicStatusHud.MakeHeadphoneSprite();
    try {
      Assert.IsNotNull(sprite);
      UnityEngine.Texture2D tex = sprite.texture;
      Assert.IsNotNull(tex);
      UnityEngine.Color[] px = tex.GetPixels();
      Assert.AreEqual(64 * 64, px.Length);
      int top = 0, bottom = 0;
      for (int y = 0; y < 64; y++) {
        for (int x = 0; x < 64; x++) {
          if (px[y * 64 + x].a > 0.5f) {
            if (y >= 40) top++;
            if (y < 8) bottom++;
          }
        }
      }
      Assert.Greater(top, 0, "arc lives in the upper half");
      Assert.AreEqual(0, bottom, "nothing below the earcups");
    } finally {
      try { UnityEngine.Object.DestroyImmediate(sprite.texture); } catch (System.Exception) { }
    }
  }
}
