// CT-P15: Phase 2.1 mic-setup gate — startup offer + phone fallback + skip.
// Covers the parent-facing flow: game start with no mic/headset-mic prompts
// "dùng điện thoại không?"; YES shows instructions + probes the
// phone↔gateway↔PC link; background polls stay SILENT (never nag mid-play);
// re-prompt happens ONLY at listening-exercise entry; decline/unavailable
// => the FROZEN runner policy SkippedNoMic ("tạm thời bỏ qua bài nghe",
// mastery untouched). Sockets tests use real loopback TCP like P14R.
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class CT_P15_MicSetupGate {
  // ---------- fakes (scripted, no hardware) ----------

  static MicrophoneDeviceService LocalWith(params string[] names) {
    string[] snapshot = (string[])names.Clone();
    return new MicrophoneDeviceService(() => (string[])snapshot.Clone(), null);
  }

  sealed class DeadCapture : ISpeechAudioCapture {
    public bool IsCapturing => false;
    public void Cancel() { }
    public Task<CapturedSpeech> CaptureAsync(float maxDur, float silence, CancellationToken ct) {
      throw new NotImplementedException("must short-circuit before capture");
    }
  }

  sealed class DeadProvider : ISpeechAssessmentProvider {
    public string ProviderId => "test-dead";
    public bool ProvidesTranscript => false;
    public bool ProvidesPhonemeEvidence => false;
    public bool RequiresNetwork => false;
    public Task<SpeechRecognitionResult> RecognizeAsync(CapturedSpeech audio, WordId target, CancellationToken ct) {
      throw new NotImplementedException("must short-circuit before provider");
    }
  }

  static SpeakingExerciseConfig ExerciseFor(string word) {
    return new SpeakingExerciseConfig {
      id = "say_" + word, targetWord = word, questId = "q-mic",
      attemptsAllowed = 1, minDecisionForPass = "pass"
    };
  }

  // ---------- classifier ----------

  [Test] public void P15A_EmptyListNoMic() {
    Assert.AreEqual(MicDeviceKind.None, MicDeviceClassifier.BestKind(null));
    Assert.AreEqual(MicDeviceKind.None, MicDeviceClassifier.BestKind(new string[0]));
    Assert.AreEqual(MicDeviceKind.None,
      MicDeviceClassifier.BestKind(new[] { "", "   ", null }));
    Assert.IsFalse(MicDeviceClassifier.HasUsableMic(null));
    Assert.IsFalse(MicDeviceClassifier.HasUsableMic(new string[0]));
  }

  [Test] public void P15B_HeadsetNamesPreferred() {
    Assert.AreEqual(MicDeviceKind.HeadsetMic,
      MicDeviceClassifier.Classify("WH-1000XM4 Hands-Free AG Audio"));
    Assert.AreEqual(MicDeviceKind.HeadsetMic,
      MicDeviceClassifier.Classify("USB Headset Microphone"));
    Assert.AreEqual(MicDeviceKind.HeadsetMic, MicDeviceClassifier.Classify("AirPods"));
    // Headset wins over built-in in a mixed list (prompt says "tai nghe").
    Assert.AreEqual(MicDeviceKind.HeadsetMic, MicDeviceClassifier.BestKind(
      new[] { "Microphone Array", "Headset Microphone" }));
  }

  [Test] public void P15C_BuiltInNames() {
    Assert.AreEqual(MicDeviceKind.BuiltInMic,
      MicDeviceClassifier.Classify("Microphone Array (Realtek Audio)"));
    Assert.AreEqual(MicDeviceKind.BuiltInMic,
      MicDeviceClassifier.Classify("Built-in Microphone"));
    Assert.AreEqual(MicDeviceKind.BuiltInMic,
      MicDeviceClassifier.BestKind(new[] { "Microphone Array" }));
  }

  [Test] public void P15D_UnknownCountsUsable() {
    // Unknown names NEVER exclude: classification only changes WORDING.
    Assert.AreEqual(MicDeviceKind.OtherMic, MicDeviceClassifier.Classify("Studio USB Mic"));
    Assert.IsTrue(MicDeviceClassifier.HasUsableMic(new[] { "Studio USB Mic" }));
    Assert.IsTrue(MicDeviceClassifier.HasUsableMic(new[] { "Steam Streaming Microphone" }));
  }

  // ---------- gate ----------

  [Test] public void P15E_StartupWithLocalMicNeedsNoPrompt() {
    var gate = new MicSetupGate(LocalWith("Microphone Array"), new PhoneMicrophoneDevice());
    Assert.AreEqual(MicSetupState.ReadyLocal, gate.EvaluateAtStartup());
    Assert.IsFalse(gate.ShouldSkipListening());
    Assert.IsFalse(gate.ShouldPromptAtExercise("say_ball"));
  }

  [Test] public void P15F_StartupNoMicOffersPhone() {
    var gate = new MicSetupGate(LocalWith(), new PhoneMicrophoneDevice());
    Assert.AreEqual(MicSetupState.OfferPhone, gate.EvaluateAtStartup());
    Assert.IsTrue(gate.ShouldSkipListening());
    Assert.IsTrue(gate.ShouldPromptAtExercise("say_ball"));
  }

  [Test] public void P15G_DeclineSkipsAndRepromptsNextExerciseOnly() {
    var gate = new MicSetupGate(LocalWith(), new PhoneMicrophoneDevice());
    gate.EvaluateAtStartup();
    gate.DeclinePhoneOffer();
    Assert.AreEqual(MicSetupState.Skipped, gate.State);
    Assert.IsTrue(gate.ShouldSkipListening());
    // Same exercise retries never re-prompt (no nag inside one exercise).
    gate.MarkPromptShown("say_ball");
    Assert.IsFalse(gate.ShouldPromptAtExercise("say_ball"));
    // ...but the NEXT listening exercise re-offers (user's reprompt policy).
    Assert.IsTrue(gate.ShouldPromptAtExercise("say_apple"));
  }

  [Test] public void P15H_AcceptThenLinkBeatsSkip() {
    var gate = new MicSetupGate(LocalWith(), new PhoneMicrophoneDevice());
    gate.EvaluateAtStartup();
    gate.AcceptPhoneOffer();
    Assert.AreEqual(MicSetupState.WaitPhoneLink, gate.State);
    Assert.IsTrue(gate.IsWaitingForPhone);
    Assert.IsTrue(gate.ShouldSkipListening());
    gate.OnPhoneLink(PhoneLinkState.GatewayUpNoPhone); // idle gateway: panel stays
    Assert.AreEqual(MicSetupState.WaitPhoneLink, gate.State);
    gate.OnPhoneLink(PhoneLinkState.GatewayDown);      // dead gateway: panel stays
    Assert.AreEqual(MicSetupState.WaitPhoneLink, gate.State);
    gate.OnPhoneLink(PhoneLinkState.PhoneLinked);
    Assert.AreEqual(MicSetupState.ReadyPhone, gate.State);
    Assert.IsFalse(gate.ShouldSkipListening());
  }

  [Test] public void P15I_SilentLossAndRecovery() {
    var phone = new PhoneMicrophoneDevice();
    // Background rule on a gate that LOSES its source mid-play: Ready ->
    // loss -> Skipped SILENTLY (never back to Offer on its own).
    var flip = new FlipMic(MicStatus.Ready);
    var gate = new MicSetupGate(flip, phone);
    Assert.AreEqual(MicSetupState.ReadyLocal, gate.EvaluateAtStartup());
    flip.Set(MicStatus.NoDevice);
    gate.NotifySourcesChanged();
    Assert.AreEqual(MicSetupState.Skipped, gate.State);
    // Recovery is silent too: Skipped -> Ready without any prompt state.
    flip.Set(MicStatus.Ready);
    gate.NotifySourcesChanged();
    Assert.AreEqual(MicSetupState.ReadyLocal, gate.State);
    // Phone link while local down -> ReadyPhone (fallback path).
    flip.Set(MicStatus.NoDevice);
    phone.ReportLinkUp("phone");
    gate.NotifySourcesChanged();
    Assert.AreEqual(MicSetupState.ReadyPhone, gate.State);
    // Phone link lost again -> silent Skipped (next exercise re-offers).
    phone.ReportLinkDown();
    gate.NotifySourcesChanged();
    Assert.AreEqual(MicSetupState.Skipped, gate.State);
  }

  sealed class FlipMic : IMicrophoneDevice {
    MicStatus _status;
    public FlipMic(MicStatus start) { _status = start; }
    public event Action<MicStatus> StatusChanged;
    public MicStatus Status => _status;
    public string SelectedDevice => "flip";
    public string[] Devices => _status == MicStatus.NoDevice ? new string[0] : new[] { "flip" };
    public SpeechCapability Capability => new SpeechCapability {
      Status = _status, DeviceName = "flip",
      DeviceCount = _status == MicStatus.NoDevice ? 0 : 1 };
    public void Refresh() { }
    public void ReportCaptureFailure() { _status = MicStatus.Error; }
    public void Set(MicStatus s) {
      _status = s;
      try { StatusChanged?.Invoke(s); } catch (Exception) { }
    }
  }

  [Test] public void P15J_CompositePrefersLocalFallsBackToPhone() {
    var local = LocalWith("Microphone Array");
    var phone = new PhoneMicrophoneDevice();
    var both = new CompositeMicrophoneDevice(local, phone);
    Assert.IsTrue(both.Capability.IsAvailable());
    Assert.AreEqual("Microphone Array", both.SelectedDevice);
    phone.ReportLinkUp("phone"); // both Ready: local still wins
    Assert.AreEqual("Microphone Array", both.SelectedDevice);
    var down = new CompositeMicrophoneDevice(LocalWith(), phone);
    Assert.IsTrue(down.Capability.IsAvailable()); // phone fallback
    Assert.AreEqual("phone", down.SelectedDevice);
    var none = new CompositeMicrophoneDevice(LocalWith(), new PhoneMicrophoneDevice());
    Assert.IsFalse(none.Capability.IsAvailable()); // actionable local reason kept
    Assert.AreEqual(MicStatus.NoDevice, none.Status);
  }

  // ---------- probe (real loopback) ----------

  static int FreePort() {
    var l = new TcpListener(IPAddress.Loopback, 0);
    l.Start();
    int p = ((IPEndPoint)l.LocalEndpoint).Port;
    l.Stop();
    return p;
  }

  [Test] public void P15K_ProbeFindsLivePhoneSession() {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var server = Task.Run(() => {
      using (TcpClient c = listener.AcceptTcpClient())
      using (NetworkStream s = c.GetStream()) {
        var tmp = new byte[64];
        Assert.Greater(s.Read(tmp, 0, tmp.Length), 0, "probe subscribes first");
        byte[] hello = PhoneMicProtocol.EncodeBridgeFrame(
          PhoneMicProtocol.KindHello, 0, 0, new byte[0]);
        s.Write(hello, 0, hello.Length);
        byte[] audio = PhoneMicProtocol.EncodeBridgeFrame(
          PhoneMicProtocol.KindAudio, 9, 0, new byte[] { 1, 2, 3, 4 });
        s.Write(audio, 0, audio.Length);
        s.Flush();
      }
    });
    PhoneLinkState link = PhoneLinkProbe.ProbeAsync(
      "127.0.0.1", port, 5f, CancellationToken.None).GetAwaiter().GetResult();
    Assert.IsTrue(server.Wait(10000), "server finished");
    listener.Stop();
    Assert.AreEqual(PhoneLinkState.PhoneLinked, link);
  }

  [Test] public void P15L_ProbeGatewayUpNoPhoneOnHelloOnly() {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var server = Task.Run(() => {
      using (TcpClient c = listener.AcceptTcpClient())
      using (NetworkStream s = c.GetStream()) {
        var tmp = new byte[64];
        s.Read(tmp, 0, tmp.Length);
        byte[] hello = PhoneMicProtocol.EncodeBridgeFrame(
          PhoneMicProtocol.KindHello, 0, 0, new byte[0]);
        s.Write(hello, 0, hello.Length);
        s.Flush();
        Task.Delay(3000).GetAwaiter().GetResult(); // idle gateway: no session
      }
    });
    PhoneLinkState link = PhoneLinkProbe.ProbeAsync(
      "127.0.0.1", port, 1f, CancellationToken.None).GetAwaiter().GetResult();
    Assert.IsTrue(server.Wait(10000), "server finished");
    listener.Stop();
    Assert.AreEqual(PhoneLinkState.GatewayUpNoPhone, link);
  }

  [Test] public void P15M_ProbeGatewayDownOnRefusedPort() {
    PhoneLinkState link = PhoneLinkProbe.ProbeAsync(
      "127.0.0.1", FreePort(), 2f, CancellationToken.None).GetAwaiter().GetResult();
    Assert.AreEqual(PhoneLinkState.GatewayDown, link);
  }

  // ---------- dialog + monitor (code-built UI, EditMode-safe) ----------

  [Test] public void P15O_DialogBuildsHiddenThenTogglesPanels() {
    var dialog = new UnityEngine.GameObject("MicDlg").AddComponent<MicSetupDialog>();
    try {
      dialog.BuildUiImmediate();
      Assert.IsFalse(dialog.IsShowing, "hidden until needed (never eats clicks)");
      bool accepted = false, declined = false;
      dialog.ShowOffer(() => accepted = true, () => declined = true);
      Assert.IsTrue(dialog.IsShowing);
      Assert.IsTrue(dialog.IsOfferShowing);
      Assert.IsFalse(dialog.IsWaitShowing);
      dialog.Hide();
      Assert.IsFalse(dialog.IsShowing);
      bool recheck = false, skip = false;
      dialog.ShowWait("hello", () => recheck = true, () => skip = true);
      Assert.IsTrue(dialog.IsWaitShowing);
      Assert.IsFalse(dialog.IsOfferShowing);
      dialog.SetWaitStatus("waiting...");
      dialog.Hide();
      Assert.IsFalse(accepted || declined || recheck || skip, "show/hide never fires callbacks");
    } finally {
      UnityEngine.Object.DestroyImmediate(dialog.gameObject);
    }
  }

  [Test] public void P15Q_DialogQrAndLateSkip() {
    var dialog = new UnityEngine.GameObject("MicDlgQ").AddComponent<MicSetupDialog>();
    try {
      dialog.BuildUiImmediate();
      bool recheck = false, skip = false;
      dialog.ShowWait("hello", () => recheck = true, () => skip = true);
      Assert.IsTrue(dialog.IsWaitShowing);
      Assert.IsFalse(dialog.IsSkipVisible, "skip hidden at show (30 s rule)");
      Assert.IsFalse(dialog.IsQrShowing, "no QR until gateway PNG arrives");
      Assert.IsFalse(dialog.SetQrImage(null), "null png rejected");
      Assert.IsFalse(dialog.SetQrImage(new byte[0]), "empty png rejected");
      Assert.IsFalse(dialog.IsQrShowing);
      dialog.SetSkipVisible(true);
      Assert.IsTrue(dialog.IsSkipVisible, "monitor reveals skip after 30 s");
      dialog.SetSkipVisible(false);
      Assert.IsFalse(dialog.IsSkipVisible);
      // Layout regression pin (P15 milestone E2E: bottom-anchored rects with
      // inverted offsets collapse to negative height and silently vanish in
      // the build — buttons + status were invisible). Heights resolve from
      // offsets alone, so this holds without a layout pass.
      var waitPanel = dialog.transform.Find("MicSetupRoot/WaitPanel");
      Assert.IsNotNull(waitPanel, "wait panel built");
      int buttons = 0;
      foreach (UnityEngine.RectTransform rt in waitPanel.GetComponentsInChildren<UnityEngine.RectTransform>(true)) {
        if (rt.gameObject.name == "Button") {
          buttons++;
          Assert.Greater(rt.rect.height, 0f, "dialog button must have positive height");
        }
        if (rt.gameObject.name == "Status")
          Assert.Greater(rt.rect.height, 0f, "dialog status must have positive height");
      }
      Assert.AreEqual(2, buttons, "recheck + skip buttons built");
      dialog.Hide();
      Assert.IsFalse(dialog.IsShowing);
      Assert.IsFalse(recheck || skip, "show/hide never fires callbacks");
    } finally {
      UnityEngine.Object.DestroyImmediate(dialog.gameObject);
    }
  }

  [Test] public void P15T_DialogQrShowsRealPngBytes() {
    // The live in-game QR path (gateway writes PNG -> monitor SetQrImage)
    // was never proven with real bytes (P15Q only pins rejections). Read the
    // repo's own QR artifact (also shipped next to player builds) and render it.
    string qrPath = null;
    try {
      string root = System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName;
      qrPath = System.IO.Path.Combine(root, "tools", "phone-mic-qr.png");
    } catch (Exception) { }
    Assert.IsNotNull(qrPath);
    Assert.IsTrue(System.IO.File.Exists(qrPath), "repo QR artifact must exist: " + qrPath);
    byte[] png = System.IO.File.ReadAllBytes(qrPath);
    Assert.Greater(png.Length, 100, "QR png non-trivial");
    var dialog = new UnityEngine.GameObject("MicDlgT").AddComponent<MicSetupDialog>();
    try {
      dialog.BuildUiImmediate();
      bool shown = false;
      dialog.ShowWait("hello", () => { }, () => { });
      try { shown = dialog.SetQrImage(png); } catch (Exception e) {
        Assert.Fail("SetQrImage threw on real bytes: " + e.Message);
      }
      Assert.IsTrue(shown, "valid QR bytes must render");
      Assert.IsTrue(dialog.IsQrShowing);
      // Layout pin (R10 photo: 180px QR covered body line 3 + status text).
      var qr = dialog.transform.Find("MicSetupRoot/WaitPanel/QrImage");
      Assert.IsNotNull(qr, "QR image built");
      var qrRt = qr.GetComponent<UnityEngine.RectTransform>();
      Assert.AreEqual(140f, qrRt.rect.height, 0.5f, "QR fits the body/status band");
      Assert.AreEqual(140f, qrRt.rect.width, 0.5f);
      dialog.Hide();
      Assert.IsFalse(dialog.IsQrShowing, "hide clears the QR");
    } finally {
      UnityEngine.Object.DestroyImmediate(dialog.gameObject);
    }
  }

  [Test] public void P15P_MonitorDefersExerciseOncePerToken() {
    var gate = new MicSetupGate(LocalWith(), new PhoneMicrophoneDevice());
    gate.EvaluateAtStartup();
    var monitor = new UnityEngine.GameObject("MicMon").AddComponent<MicSetupMonitor>();
    try {
      monitor.Bind(gate, null, null, null, "127.0.0.1", 8451);
      Assert.IsFalse(monitor.IsListeningAvailable);
      Assert.IsFalse(monitor.CheckBeforeListening("say_ball"), "first entry: offer + defer");
      Assert.AreEqual(MicSetupState.OfferPhone, gate.State);
      Assert.IsFalse(monitor.CheckBeforeListening("say_ball"), "same exercise: defer silently");
      gate.AcceptPhoneOffer();
      gate.OnPhoneLink(PhoneLinkState.PhoneLinked);
      Assert.IsTrue(monitor.IsListeningAvailable);
      Assert.IsTrue(monitor.CheckBeforeListening("say_ball"), "linked: proceed");
    } finally {
      UnityEngine.Object.DestroyImmediate(monitor.gameObject);
    }
  }

  // ---------- frozen-policy integration ----------

  [Test] public void P15N_BothDownSkipsExerciseWithoutMasteryTouch() {
    var composite = new CompositeMicrophoneDevice(LocalWith(), new PhoneMicrophoneDevice());
    var bus = new GameEventBus();
    int spoken = 0;
    bus.Subscribe<WordSpokenEvent>(e => spoken++);
    var recognizer = new SpeechRecognizer(composite, new DeadCapture(), new DeadProvider());
    var runner = new SpeakingExerciseRunner(recognizer, null, bus);
    SpeakingExerciseResult r = runner.RunAsync(
      ExerciseFor("ball"), CancellationToken.None).GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.SkippedNoMic, r.Outcome);
    Assert.AreEqual(0, spoken, "silence publishes NOTHING (§71)");
    Assert.IsTrue(runner.Deferral.IsDeferred(new QuestId("q-mic")));
  }

  // ---------- presence watcher (mid-game drops + explicit STOP) ----------

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

  [Test] public void P15R_WatcherSeesUpAudioStopDown() {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var server = Task.Run(() => {
      using (TcpClient c = listener.AcceptTcpClient())
      using (NetworkStream s = c.GetStream()) {
        var tmp = new byte[64];
        Assert.Greater(s.Read(tmp, 0, tmp.Length), 0, "watcher subscribes first");
        WriteFrame(s, 4, 0, 0, new byte[0]); // HELLO
        WriteFrame(s, 5, 0, 0, System.Text.Encoding.UTF8.GetBytes("ws-connected")); // UP
        WriteFrame(s, 1, 9, 0, new byte[] { 1, 2, 3, 4 }); // AUDIO serial 9
        WriteFrame(s, 2, 9, 1, new byte[0]); // STOP = explicit user stop
        WriteFrame(s, 6, 0, 0, System.Text.Encoding.UTF8.GetBytes("ws-closed")); // DOWN
        Task.Delay(2000).GetAwaiter().GetResult(); // let the watcher drain, hold open
      }
    });
    var watcher = new PhonePresenceWatcher("127.0.0.1", port);
    try {
      watcher.Start();
      Assert.IsTrue(watcher.IsRunning);
      DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
      long up = 0, audio = 0, stop = 0, down = 0;
      while (DateTime.UtcNow < deadline) {
        watcher.ReadCounters(out up, out audio, out stop, out down);
        if (up >= 1 && audio >= 1 && stop >= 1 && down >= 1) break;
        Task.Delay(50).GetAwaiter().GetResult();
      }
      watcher.ReadCounters(out up, out audio, out stop, out down);
      Assert.GreaterOrEqual(up, 1, "page-open edge");
      Assert.GreaterOrEqual(audio, 1, "streaming edge");
      Assert.GreaterOrEqual(stop, 1, "explicit-STOP edge");
      Assert.GreaterOrEqual(down, 1, "page-gone edge");
      int seq;
      WatcherEvent ev;
      uint serial;
      string reason;
      watcher.ReadState(out seq, out ev, out serial, out reason);
      Assert.AreEqual(WatcherEvent.PhoneDown, ev, "latest edge wins (DOWN after STOP)");
      Assert.Greater(seq, 0);
    } finally {
      watcher.Dispose();
    }
    Assert.IsFalse(watcher.IsRunning, "stop joins the thread");
    Assert.IsTrue(server.Wait(10000), "server finished");
    listener.Stop();
  }

  [Test] public void P15S_WatcherReportsGatewayDownOnRefusedPort() {
    var watcher = new PhonePresenceWatcher("127.0.0.1", FreePort());
    try {
      watcher.Start();
      DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
      WatcherEvent ev = WatcherEvent.None;
      while (DateTime.UtcNow < deadline) {
        int seq;
        uint serial;
        string reason;
        watcher.ReadState(out seq, out ev, out serial, out reason);
        if (ev == WatcherEvent.GatewayDown) break;
        Task.Delay(100).GetAwaiter().GetResult();
      }
      Assert.AreEqual(WatcherEvent.GatewayDown, ev);
    } finally {
      watcher.Dispose();
    }
    Assert.IsFalse(watcher.IsRunning);
  }
}
