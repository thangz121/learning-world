// CT-P14: Phase 2.1-local M6 phone-microphone transport — deterministic suite.
// Phone audio is ANOTHER INPUT, never another engine: every speaking test
// below runs the FROZEN pipeline (SpeechRecognizer -> LocalAcousticProvider
// -> SpeakingPassPolicy -> SpeakingExerciseRunner -> WordSpokenEvent ->
// QuestManager). SIMULATED = scripted PCM through the real code path;
// loopback = real TCP sockets on 127.0.0.1; REAL PHONE = user-run E2E only.
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class CT_P14_PhoneMicTransport {
  // ---------- scripted transport double (no hardware, no sockets) ----------

  sealed class ScriptedPhoneTransport : IPhoneAudioTransport {
    readonly Queue<PhoneAudioEvent> _queue = new Queue<PhoneAudioEvent>();
    public bool Connected = true;
    public int TakeCalls { get; private set; }
    public bool IsConnected => Connected;
    public void Enqueue(PhoneAudioEvent ev) { _queue.Enqueue(ev); }
    public void Cancel() { }
    public async Task<PhoneAudioEvent> TakeAsync(CancellationToken ct) {
      TakeCalls++;
      while (_queue.Count == 0) {
        await Task.Delay(20, ct).ConfigureAwait(false); // quiet slice path
      }
      return _queue.Dequeue();
    }
  }

  sealed class PhoneReadyMic : IMicrophoneDevice {
    public event Action<MicStatus> StatusChanged;
    public MicStatus Status => MicStatus.Ready;
    public string SelectedDevice => "phone-test";
    public string[] Devices => new[] { "phone-test" };
    public SpeechCapability Capability => new SpeechCapability {
      Status = MicStatus.Ready, DeviceName = "phone-test", DeviceCount = 1 };
    public void Refresh() { }
    public void ReportCaptureFailure() { }
  }

  static ScriptedPronunciationProvider SixWords() {
    return new ScriptedPronunciationProvider()
      .Add("ball", "B", "AO", "L")
      .Add("apple", "AE", "P", "AH", "L")
      .Add("red", "R", "EH", "D")
      .Add("one", "W", "AH", "N")
      .Add("please", "P", "L", "IY", "Z")
      .Add("teddy", "T", "EH", "D", "IY");
  }

  // SIMULATED phone wire: float fixture -> int16 PCM (page quantization) ->
  // float32 again (gateway PCM decode), chunked like network frames.
  static List<PhoneAudioEvent> PhoneFrames(float[] pcm, uint serial, int chunk = 1600) {
    var bytes = new byte[pcm.Length * 2];
    for (int i = 0; i < pcm.Length; i++) {
      float v = Math.Max(-1f, Math.Min(1f, pcm[i]));
      short s = (short)(v < 0 ? v * 32768f : v * 32767f);
      bytes[i * 2] = (byte)(s & 0xFF);
      bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
    }
    var evs = new List<PhoneAudioEvent>();
    uint seq = 0;
    for (int o = 0; o < bytes.Length; o += chunk * 2) {
      int n = Math.Min(chunk * 2, bytes.Length - o);
      var slice = new byte[n];
      Buffer.BlockCopy(bytes, o, slice, 0, n);
      evs.Add(PhoneAudioEvent.Audio(serial, seq++,
        PhoneMicProtocol.Pcm16ToFloat32(slice, 0, slice.Length)));
    }
    evs.Add(PhoneAudioEvent.Stopped(serial));
    return evs;
  }

  static float[] Const(float value, int samples) {
    var s = new float[samples];
    for (int i = 0; i < samples; i++) s[i] = value;
    return s;
  }

  static CapturedSpeech CaptureWith(IPhoneAudioTransport t,
      float maxDur = 8f, float silence = 3f) {
    var cap = new NetworkMicrophoneCapture(t);
    return cap.CaptureAsync(maxDur, silence, CancellationToken.None)
      .GetAwaiter().GetResult();
  }

  // ---------- 1-2. control protocol ----------

  [Test] public void P14A_ControlStartParses() {
    string json = "{\"type\":\"start\",\"session\":\"ph-abc\",\"sampleRate\":16000,\"channels\":1,\"format\":\"pcm16\"}";
    string type, session, rate, ch, fmt;
    Assert.IsTrue(PhoneMicProtocol.TryReadControlField(json, "type", out type));
    Assert.AreEqual("start", type);
    Assert.IsTrue(PhoneMicProtocol.TryReadControlField(json, "session", out session));
    Assert.AreEqual("ph-abc", session);
    Assert.IsTrue(PhoneMicProtocol.TryReadControlField(json, "sampleRate", out rate));
    Assert.AreEqual("16000", rate);
    Assert.IsTrue(PhoneMicProtocol.TryReadControlField(json, "channels", out ch));
    Assert.AreEqual("1", ch);
    Assert.IsTrue(PhoneMicProtocol.TryReadControlField(json, "format", out fmt));
    Assert.AreEqual("pcm16", fmt);
    Assert.IsTrue(PhoneMicProtocol.IsCanonicalFormat(int.Parse(rate), int.Parse(ch), fmt));
  }

  [Test] public void P14B_MalformedControlFails() {
    string v;
    Assert.IsFalse(PhoneMicProtocol.TryReadControlField("not json", "type", out v));
    Assert.IsFalse(PhoneMicProtocol.TryReadControlField("{\"type\":\"start\"}", "session", out v));
    Assert.IsFalse(PhoneMicProtocol.TryReadControlField(null, "type", out v));
    Assert.IsFalse(PhoneMicProtocol.TryReadControlField("{\"type\":}", "type", out v));
  }

  // ---------- 3-4. bridge envelope ----------

  [Test] public void P14C_BridgeRoundTrip() {
    byte[] payload = { 0x01, 0x02, 0x03, 0x04 };
    byte[] raw = PhoneMicProtocol.EncodeBridgeFrame(PhoneMicProtocol.KindAudio, 7, 42, payload);
    PhoneMicProtocol.BridgeFrame f;
    string reason;
    Assert.IsTrue(PhoneMicProtocol.TryDecodeBridgeFrame(raw, 0, raw.Length, out f, out reason), reason);
    Assert.AreEqual(PhoneMicProtocol.KindAudio, f.Kind);
    Assert.AreEqual(7u, f.SessionSerial);
    Assert.AreEqual(42u, f.Seq);
    CollectionAssert.AreEqual(payload, f.Payload);
  }

  [Test] public void P14D_BridgeRejectsGarbage() {
    PhoneMicProtocol.BridgeFrame f;
    string reason;
    Assert.IsFalse(PhoneMicProtocol.TryDecodeBridgeFrame(new byte[0], 0, 0, out f, out reason));
    Assert.IsNotEmpty(reason);
    Assert.IsFalse(PhoneMicProtocol.TryDecodeBridgeFrame(new byte[] { 0, 0 }, 0, 2, out f, out reason));
    byte[] truncated = PhoneMicProtocol.EncodeBridgeFrame(PhoneMicProtocol.KindAudio, 1, 1, new byte[10]);
    Assert.IsFalse(PhoneMicProtocol.TryDecodeBridgeFrame(truncated, 0, truncated.Length - 3, out f, out reason));
    Assert.AreEqual("truncated", reason);
    byte[] unknown = PhoneMicProtocol.EncodeBridgeFrame(0x7F, 1, 1, new byte[0]);
    Assert.IsFalse(PhoneMicProtocol.TryDecodeBridgeFrame(unknown, 0, unknown.Length, out f, out reason));
    StringAssert.StartsWith("unknown-kind", reason);
  }

  // ---------- 10-11. format + PCM ----------

  [Test] public void P14E_CanonicalFormatMatrix() {
    Assert.IsTrue(PhoneMicProtocol.IsCanonicalFormat(16000, 1, "pcm16"));
    Assert.IsTrue(PhoneMicProtocol.IsCanonicalFormat(16000, 1, "PCM16"));
    Assert.IsFalse(PhoneMicProtocol.IsCanonicalFormat(48000, 1, "pcm16"));
    Assert.IsFalse(PhoneMicProtocol.IsCanonicalFormat(16000, 2, "pcm16"));
    Assert.IsFalse(PhoneMicProtocol.IsCanonicalFormat(16000, 1, "float32"));
    Assert.IsFalse(PhoneMicProtocol.IsCanonicalFormat(0, 0, null));
    Assert.IsNotEmpty(PhoneMicProtocol.FormatRejection(48000, 2, "float32"));
  }

  [Test] public void P14F_Pcm16Conversion() {
    byte[] bytes = { 0x00, 0x80, 0xFF, 0xFF, 0x00, 0x00, 0xFF, 0x7F, 0x99 };
    float[] s = PhoneMicProtocol.Pcm16ToFloat32(bytes, 0, bytes.Length);
    Assert.AreEqual(4, s.Length, "odd trailing byte ignored");
    Assert.AreEqual(-1f, s[0], 1e-6f);
    Assert.AreEqual(0f, s[2], 1e-6f);
    Assert.AreEqual(32767f / 32768f, s[3], 1e-6f);
    Assert.AreEqual(0, PhoneMicProtocol.Pcm16ToFloat32(null, 0, 0).Length);
  }

  // ---------- 5-7,12. capture lifecycle ----------

  [Test] public void P14G_CaptureDeliversSession() {
    var t = new ScriptedPhoneTransport();
    var cap = new NetworkMicrophoneCapture(t);
    var task = cap.CaptureAsync(8f, 3f, CancellationToken.None);
    Assert.IsTrue(cap.IsCapturing);
    foreach (var ev in PhoneFrames(Const(0.3f, 16000), 7)) t.Enqueue(ev);
    CapturedSpeech seg = task.GetAwaiter().GetResult();
    Assert.IsFalse(cap.IsCapturing, "lifecycle closes after STOP");
    Assert.IsEmpty(seg.Error);
    Assert.AreEqual(16000, seg.Samples.Length);
    Assert.AreEqual(16000, seg.SampleRate);
    Assert.AreEqual(1, seg.Channels);
    Assert.AreEqual(0.3f, seg.MeanEnergy, 0.05f);
  }

  [Test] public void P14H_ForeignSerialDropped() {
    var t = new ScriptedPhoneTransport();
    var cap = new NetworkMicrophoneCapture(t);
    var task = cap.CaptureAsync(8f, 3f, CancellationToken.None);
    t.Enqueue(PhoneAudioEvent.Audio(7, 0, Const(0.3f, 8000)));
    t.Enqueue(PhoneAudioEvent.Audio(9, 0, Const(0.9f, 8000))); // other session: dropped
    t.Enqueue(PhoneAudioEvent.Audio(7, 1, Const(0.3f, 8000)));
    t.Enqueue(PhoneAudioEvent.Stopped(7));
    CapturedSpeech seg = task.GetAwaiter().GetResult();
    Assert.AreEqual(1, cap.LastDroppedForeignFrames);
    Assert.AreEqual(16000, seg.Samples.Length, "only the latched session merged");
    Assert.AreEqual(0.3f, seg.MeanEnergy, 0.05f);
  }

  [Test] public void P14I_SeqGapDetectedTolerated() {
    var t = new ScriptedPhoneTransport();
    var cap = new NetworkMicrophoneCapture(t);
    var task = cap.CaptureAsync(8f, 3f, CancellationToken.None);
    t.Enqueue(PhoneAudioEvent.Audio(3, 0, Const(0.3f, 4000)));
    t.Enqueue(PhoneAudioEvent.Audio(3, 1, Const(0.3f, 4000)));
    t.Enqueue(PhoneAudioEvent.Audio(3, 5, Const(0.3f, 4000))); // 2-4 missing
    t.Enqueue(PhoneAudioEvent.Stopped(3));
    CapturedSpeech seg = task.GetAwaiter().GetResult();
    Assert.AreEqual(1, cap.LastMissingSeqGaps);
    Assert.IsEmpty(seg.Error, "gap is tolerated, session still completes");
    Assert.AreEqual(12000, seg.Samples.Length);
  }

  [Test] public void P14J_NoTransportIsMicUnavailable() {
    var t = new ScriptedPhoneTransport { Connected = false };
    CapturedSpeech seg = CaptureWith(t);
    Assert.AreEqual(SpeechFailureReasons.MicUnavailable, seg.Error);
    Assert.AreEqual(0, t.TakeCalls, "no network attempt without a link");
  }

  [Test] public void P14K_LinkDownMidCaptureIsEnvError() {
    var t = new ScriptedPhoneTransport();
    var cap = new NetworkMicrophoneCapture(t);
    var recognizer = new SpeechRecognizer(new PhoneReadyMic(), cap,
      new LocalAcousticProvider(SixWords()));
    var task = recognizer.StartAttemptAsync(new WordId("ball"), CancellationToken.None);
    t.Enqueue(PhoneAudioEvent.Audio(1, 0, Const(0.3f, 4000)));
    t.Enqueue(PhoneAudioEvent.LinkDown());
    SpeakingAssessment a = task.GetAwaiter().GetResult();
    Assert.IsTrue(a.IsEnvironmentError);
    Assert.AreEqual(SpeechFailureReasons.NetworkError, a.FailureReason);
    Assert.AreNotEqual(SpeakingDecision.WrongWord, a.Decision, "link loss is never WrongWord");
  }

  [Test] public void P14L_CancelAborts() {
    var t = new ScriptedPhoneTransport();
    var cap = new NetworkMicrophoneCapture(t);
    var task = cap.CaptureAsync(8f, 3f, CancellationToken.None);
    cap.Cancel();
    CapturedSpeech seg = task.GetAwaiter().GetResult();
    Assert.IsTrue(seg.Cancelled);
    Assert.IsFalse(cap.IsCapturing);
  }

  [Test] public void P14M_SilentTransportTimesOut() {
    var t = new ScriptedPhoneTransport(); // never yields: pure silence window
    var sw = System.Diagnostics.Stopwatch.StartNew();
    CapturedSpeech seg = CaptureWith(t, 0.4f, 0.2f);
    sw.Stop();
    Assert.IsTrue(seg.TimedOut);
    Assert.AreEqual(0, seg.Samples.Length);
    Assert.Less(sw.ElapsedMilliseconds, 8000, "wall-clock exits, never hangs");
  }

  // ---------- 15-16. capability ----------

  [Test] public void P14N_PhoneCapabilityLifecycle() {
    var dev = new PhoneMicrophoneDevice();
    Assert.AreEqual(MicStatus.NoDevice, dev.Status);
    Assert.IsFalse(dev.Capability.IsAvailable());
    int fires = 0;
    dev.StatusChanged += s => fires++;
    dev.ReportLinkUp("phone@192.168.1.20");
    Assert.AreEqual(MicStatus.Ready, dev.Status);
    Assert.IsTrue(dev.Capability.IsAvailable());
    Assert.AreEqual("phone@192.168.1.20", dev.Capability.DeviceName);
    dev.ReportLinkDown();
    Assert.AreEqual(MicStatus.NoDevice, dev.Status);
    Assert.IsFalse(dev.Capability.IsAvailable());
    Assert.IsNull(dev.SelectedDevice, "reconnect starts a fresh epoch");
    dev.ReportPermissionDenied();
    Assert.AreEqual(MicStatus.PermissionDenied, dev.Status);
    Assert.GreaterOrEqual(fires, 3);
  }

  [Test] public void P14O_SkippedNoMicDefers() {
    var dev = new PhoneMicrophoneDevice(); // down: no link
    var t = new ScriptedPhoneTransport();
    var recognizer = new SpeechRecognizer(dev, new NetworkMicrophoneCapture(t),
      new LocalAcousticProvider(SixWords()));
    var runner = new SpeakingExerciseRunner(recognizer, null, new GameEventBus());
    SpeakingExerciseResult r = runner.RunAsync(
      SpeakingExerciseConfig.DefaultFor(new WordId("ball")), CancellationToken.None)
      .GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.SkippedNoMic, r.Outcome);
    Assert.AreEqual(0, t.TakeCalls, "no capture without capability");
    Assert.AreEqual("Microphone not connected.", r.ChildMessage);
  }

  // ---------- 17-18. speaking path over phone input ----------

  static void DriveToSpeakLeg(QuestManager quests, QuestId q, GameEventBus bus) {
    // Production glue mirror (MarketBootstrap): spoken levels advance the quest.
    bus.Subscribe<WordSpokenEvent>(e => quests.AdvanceOnSpoken(e.WordId, e.Result.Level));
    quests.StartQuest(q);
    quests.AdvanceOnSeen(new WordId("apple"));
    quests.ReportAction(PlayerAction.Bring, new WordId("apple"));
    Assert.AreEqual(2, quests.GetState(q).ObjectiveIndex, "speak leg reached");
  }

  [Test] public void P14P_PhonePassPublishesWordSpoken() {
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var quests = new QuestManager(bus, learning, hints);
    var q = new QuestId("market_help_mia");
    DriveToSpeakLeg(quests, q, bus);
    var spoken = new List<WordSpokenEvent>();
    bus.Subscribe<WordSpokenEvent>(e => spoken.Add(e));

    var t = new ScriptedPhoneTransport();
    // SIMULATED clear apple through the REAL phone wire (PCM quantize + chunk).
    foreach (var ev in PhoneFrames(AcousticFixtures.Word("apple", 1f), 11)) t.Enqueue(ev);
    var recognizer = new SpeechRecognizer(new PhoneReadyMic(),
      new NetworkMicrophoneCapture(t), new LocalAcousticProvider(SixWords()));
    var runner = new SpeakingExerciseRunner(recognizer, null, bus);
    SpeakingExerciseResult r = runner.RunAsync(
      SpeakingExerciseConfig.DefaultFor(new WordId("apple")), CancellationToken.None)
      .GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.Passed, r.Outcome, r.BestAssessment.Evidence);
    Assert.AreEqual(1, spoken.Count, "pass publishes exactly one WordSpokenEvent");
    Assert.IsTrue(quests.GetState(q).Completed, "frozen Speak gate advances on phone audio");
  }

  [Test] public void P14Q_EnvFailurePublishesNothing() {
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var quests = new QuestManager(bus, learning, hints);
    var q = new QuestId("market_help_mia");
    DriveToSpeakLeg(quests, q, bus);
    int spokenCount = 0;
    bus.Subscribe<WordSpokenEvent>(e => spokenCount++);
    WordMastery mBefore = learning.GetMastery(new WordId("apple"));
    int totalsBefore = mBefore.Exposure + mBefore.SpeakingTotal + mBefore.SpeakingHit;

    var t = new ScriptedPhoneTransport();
    t.Enqueue(PhoneAudioEvent.Audio(5, 0, Const(0.3f, 4000)));
    t.Enqueue(PhoneAudioEvent.LinkDown()); // Wi-Fi dies mid-speech (§11)
    var recognizer = new SpeechRecognizer(new PhoneReadyMic(),
      new NetworkMicrophoneCapture(t), new LocalAcousticProvider(SixWords()));
    var runner = new SpeakingExerciseRunner(recognizer, null, bus);
    var config = SpeakingExerciseConfig.DefaultFor(new WordId("apple"));
    config.attemptsAllowed = 1;
    SpeakingExerciseResult r = runner.RunAsync(config, CancellationToken.None)
      .GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.ErrorAborted, r.Outcome);
    Assert.AreEqual(0, spokenCount, "environment failure publishes no learning event");
    WordMastery mAfter = learning.GetMastery(new WordId("apple"));
    int totalsAfter = mAfter.Exposure + mAfter.SpeakingTotal + mAfter.SpeakingHit;
    Assert.AreEqual(totalsBefore, totalsAfter, "no mastery penalty for network failure");
    Assert.IsFalse(quests.GetState(q).Completed, "quest stays open, never fails");
  }

  // ---------- 12/19. reconnect + M5 regression ----------

  [Test] public void P14T_ReconnectStartsFreshSession() {
    // Session A dies; session B on a fresh link completes independently.
    var tA = new ScriptedPhoneTransport();
    tA.Enqueue(PhoneAudioEvent.Audio(21, 0, Const(0.3f, 4000)));
    tA.Enqueue(PhoneAudioEvent.LinkDown());
    CapturedSpeech a = CaptureWith(tA);
    Assert.AreEqual(SpeechFailureReasons.NetworkError, a.Error);

    var tB = new ScriptedPhoneTransport();
    foreach (var ev in PhoneFrames(Const(0.3f, 8000), 22)) tB.Enqueue(ev);
    CapturedSpeech b = CaptureWith(tB);
    Assert.IsEmpty(b.Error);
    Assert.AreEqual(8000, b.Samples.Length, "session B carries no audio from A");
  }

  [Test] public void P14S_M5LocalPathRegression() {
    // M5 staged beat on LOCAL capture still passes: phone work changed nothing.
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var quests = new QuestManager(bus, learning, hints);
    var q = new QuestId("market_help_mia");
    DriveToSpeakLeg(quests, q, bus);
    float[] pcm = AcousticFixtures.Word("apple", 1f);
    float mean = VoiceActivity.MeanAbsolute(pcm);
    var seg = new CapturedSpeech {
      Samples = pcm, SampleRate = 16000, Channels = 1,
      DurationSec = (float)pcm.Length / 16000, MeanEnergy = mean,
      PeakEnergy = mean * 2f, VoicedSec = 0.7f,
      TimedOut = false, Cancelled = false, Error = string.Empty
    };
    var recognizer = new SpeechRecognizer(new PhoneReadyMic(),
      new FakeSpeechCapture(() => seg), new LocalAcousticProvider(SixWords()));
    var runner = new SpeakingExerciseRunner(recognizer, null, bus);
    SpeakingExerciseResult r = runner.RunAsync(
      SpeakingExerciseConfig.DefaultFor(new WordId("apple")), CancellationToken.None)
      .GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.Passed, r.Outcome);
    Assert.IsTrue(quests.GetState(q).Completed);
  }

  // ---------- real sockets, still deterministic (loopback) ----------

  [Test] public void P14R_TcpLoopbackRealSockets() {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    byte[] helloPayload = System.Text.Encoding.UTF8.GetBytes("gw-test");
    var server = Task.Run(() => {
      using (TcpClient c = listener.AcceptTcpClient())
      using (NetworkStream s = c.GetStream()) {
        var tmp = new byte[64];
        int n = s.Read(tmp, 0, tmp.Length); // SUBSCRIBE envelope
        Assert.Greater(n, 0, "Unity subscribes first");
        byte[] hello = PhoneMicProtocol.EncodeBridgeFrame(
          PhoneMicProtocol.KindHello, 0, 0, helloPayload);
        s.Write(hello, 0, hello.Length);
        byte[] pcm = new byte[3200]; // 1600 samples const 0.25
        for (int i = 0; i < 1600; i++) {
          short v = (short)(0.25f * 32767f);
          pcm[i * 2] = (byte)(v & 0xFF);
          pcm[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        byte[] audio = PhoneMicProtocol.EncodeBridgeFrame(
          PhoneMicProtocol.KindAudio, 77, 0, pcm);
        s.Write(audio, 0, audio.Length);
        byte[] stop = PhoneMicProtocol.EncodeBridgeFrame(
          PhoneMicProtocol.KindStop, 77, 1, new byte[0]);
        s.Write(stop, 0, stop.Length);
        s.Flush();
      }
    });
    CapturedSpeech seg;
    using (var transport = new TcpPhoneAudioTransport("127.0.0.1", port)) {
      seg = CaptureWith(transport);
    }
    Assert.IsTrue(server.Wait(10000), "server finished");
    listener.Stop();
    Assert.IsEmpty(seg.Error);
    Assert.AreEqual(1600, seg.Samples.Length);
    Assert.AreEqual(0.25f, seg.MeanEnergy, 0.02f);
  }

  // ---------- presence (mid-game disconnect/STOP detection) ----------

  [Test] public void P14U_PresenceFramesDecodeAndCaptureSkipsThem() {
    // New kinds ride the same envelope (gateway<->Unity contract).
    foreach (byte kind in new[] { PhoneMicProtocol.KindPresenceUp, PhoneMicProtocol.KindPresenceDown }) {
      byte[] raw = PhoneMicProtocol.EncodeBridgeFrame(kind, 0, 0,
        System.Text.Encoding.UTF8.GetBytes(kind == PhoneMicProtocol.KindPresenceUp ? "ws-connected" : "ws-closed"));
      PhoneMicProtocol.BridgeFrame f;
      string reason;
      Assert.IsTrue(PhoneMicProtocol.TryDecodeBridgeFrame(raw, 0, raw.Length, out f, out reason), reason);
      Assert.AreEqual(kind, f.Kind);
    }
    // A capture whose stream is interleaved with presence frames (page opened
    // before START, page closed after STOP) still delivers the session: the
    // transport skips presence like HELLO instead of failing protocol_error.
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var server = Task.Run(() => {
      using (TcpClient c = listener.AcceptTcpClient())
      using (NetworkStream s = c.GetStream()) {
        var tmp = new byte[64];
        Assert.Greater(s.Read(tmp, 0, tmp.Length), 0, "Unity subscribes first");
        s.Write(PhoneMicProtocol.EncodeBridgeFrame(PhoneMicProtocol.KindHello, 0, 0, new byte[0]), 0, 13);
        s.Write(PhoneMicProtocol.EncodeBridgeFrame(PhoneMicProtocol.KindPresenceUp, 0, 0,
          System.Text.Encoding.UTF8.GetBytes("ws-connected")), 0, 13 + 12);
        byte[] pcm = new byte[3200]; // 1600 samples const 0.25
        for (int i = 0; i < 1600; i++) {
          short v = (short)(0.25f * 32767f);
          pcm[i * 2] = (byte)(v & 0xFF);
          pcm[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        byte[] audio = PhoneMicProtocol.EncodeBridgeFrame(PhoneMicProtocol.KindAudio, 77, 0, pcm);
        s.Write(audio, 0, audio.Length);
        byte[] stop = PhoneMicProtocol.EncodeBridgeFrame(PhoneMicProtocol.KindStop, 77, 1, new byte[0]);
        s.Write(stop, 0, stop.Length);
        s.Write(PhoneMicProtocol.EncodeBridgeFrame(PhoneMicProtocol.KindPresenceDown, 0, 0,
          System.Text.Encoding.UTF8.GetBytes("ws-closed")), 0, 13 + 9);
        s.Flush();
        Task.Delay(500).GetAwaiter().GetResult(); // let the capture drain
      }
    });
    CapturedSpeech seg;
    using (var transport = new TcpPhoneAudioTransport("127.0.0.1", port)) {
      seg = CaptureWith(transport);
    }
    Assert.IsTrue(server.Wait(10000), "server finished");
    listener.Stop();
    Assert.IsEmpty(seg.Error, "presence frames must not fail the capture");
    Assert.AreEqual(1600, seg.Samples.Length);
  }
}
