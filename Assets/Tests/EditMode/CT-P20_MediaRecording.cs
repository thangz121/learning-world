// CT-P20: Phase 2.3 audio + video recording — deterministic suite.
// The recorder consumes media the GAME already accepted (watcher-observed
// audio, slot-accepted video) and stores it via PC-side encoder backends
// (WAV PCM16 + AVI/MJPEG, zero native deps). SIMULATED = scripted PCM/JPEG
// bytes through the real queues/writers/files; loopback = real TCP sockets
// for the watcher tap; REAL PHONE = user-run E2E only (handoff runbook).
using NUnit.Framework;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class CT_P20_MediaRecording {
  // ---------- helpers ----------

  static string TempDir() {
    string d = Path.Combine(Path.GetTempPath(),
      "LWE-P20-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(d);
    return d;
  }

  static void WipeDir(string d) {
    try { if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) Directory.Delete(d, true); }
    catch (Exception) { }
  }

  static byte[] TonePcm16(int samples, float level) {
    var b = new byte[samples * 2];
    short v = (short)(level * 32767f);
    for (int i = 0; i < samples; i++) {
      b[i * 2] = (byte)(v & 0xFF);
      b[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
    }
    return b;
  }

  static byte[] FakeJpeg(int len, byte fill) {
    var b = new byte[Math.Max(16, len)];
    b[0] = 0xFF; b[1] = 0xD8; b[2] = 0xFF; b[3] = 0xE0;
    for (int i = 4; i < b.Length; i++) b[i] = fill;
    return b;
  }

  static MediaRecordingService NewService(string dir) {
    var go = new UnityEngine.GameObject("MediaRecTest");
    var rec = go.AddComponent<MediaRecordingService>();
    rec.Bind(MediaRecordingConfig.Default, null, null,
      (Func<PhonePresenceWatcher>)(() => (PhonePresenceWatcher)null), dir);
    rec.SetTestForceSourcesAvailable(true);
    rec.SetTestDisableTranscode(true); // unit env: intermediates ARE the verdict
    rec.SetPrefsKeyForTests("LWE.Test.P20." + Guid.NewGuid().ToString("N"));
    return rec;
  }

  // Synthetic gameplay frame (RGBA gradient, default config size).
  static byte[] TestRgba(int step) {
    int w = MediaRecording.DefaultGameWidth, h = MediaRecording.DefaultGameHeight;
    var b = new byte[w * h * 4];
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int o = (y * w + x) * 4;
        b[o] = (byte)((x + step) & 0xFF);
        b[o + 1] = (byte)((y + step) & 0xFF);
        b[o + 2] = (byte)(((x + y) / 2 + step) & 0xFF);
        b[o + 3] = 255;
      }
    }
    return b;
  }

  static void KillService(MediaRecordingService rec) {
    try {
      if (rec != null) UnityEngine.Object.DestroyImmediate(rec.gameObject);
    } catch (Exception) { }
  }

  static bool WaitTerminal(MediaRecordingService rec, int timeoutMs) {
    int waited = 0;
    while (rec.CurrentState == RecordingState.Stopping && waited < timeoutMs) {
      try { rec.PumpForTests(); } catch (Exception) { }
      Thread.Sleep(50);
      waited += 50;
    }
    var s = rec.CurrentState;
    return s == RecordingState.Completed || s == RecordingState.Error;
  }

  // ---------- config (§8: one controlled object, validated) ----------

  [Test] public void P20A_ConfigDefaultAndModes() {
    var d = MediaRecordingConfig.Default;
    string reason;
    Assert.IsTrue(d.Validate(out reason), reason);
    Assert.AreEqual(320, d.VideoWidth);
    Assert.AreEqual(240, d.VideoHeight);
    Assert.AreEqual(10, d.VideoFps);
    var mic = MediaRecordingConfig.ForMode(RecordingMode.MicOnly);
    Assert.IsTrue(mic.RecordAudio);
    Assert.IsFalse(mic.RecordVideo);
    var cam = MediaRecordingConfig.ForMode(RecordingMode.CameraOnly);
    Assert.IsFalse(cam.RecordAudio);
    Assert.IsTrue(cam.RecordVideo);
    var both = MediaRecordingConfig.ForMode(RecordingMode.MicAndCamera);
    Assert.IsTrue(both.RecordAudio && both.RecordVideo);
  }

  [Test] public void P20B_ConfigValidationMatrix() {
    string reason;
    var c = MediaRecordingConfig.Default;
    c.VideoFps = 60;
    Assert.IsFalse(c.Validate(out reason));
    c = MediaRecordingConfig.Default;
    c.VideoWidth = 1920;
    Assert.IsFalse(c.Validate(out reason));
    c = MediaRecordingConfig.Default;
    c.VideoHeight = 1080;
    Assert.IsFalse(c.Validate(out reason));
    c = MediaRecordingConfig.Default;
    c.MaxAudioQueueSec = 500;
    Assert.IsFalse(c.Validate(out reason));
    c = MediaRecordingConfig.Default;
    c.RecordAudio = false;
    c.RecordVideo = false;
    Assert.IsFalse(c.Validate(out reason));
  }

  // ---------- naming (§17: session-traceable, collision-safe) ----------

  [Test] public void P20C_NamingSessionTraceableAndUnique() {
    string a = MediaRecordingNaming.NewSessionId(new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc));
    string b = MediaRecordingNaming.NewSessionId(new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc));
    Assert.AreNotEqual(a, b, "collision-safe suffix required");
    string fa = MediaRecordingNaming.AudioFileName(a);
    string va = MediaRecordingNaming.VideoFileName(a);
    string sa = MediaRecordingNaming.SidecarFileName(a);
    Assert.IsTrue(fa.StartsWith(a) && va.StartsWith(a) && sa.StartsWith(a), "one session, three files");
    Assert.IsTrue(fa.EndsWith(".wav") && va.EndsWith(".avi") && sa.EndsWith(".json"));
    string p;
    Assert.IsTrue(MediaRecordingNaming.TryJoin("/tmp", fa, out p));
    Assert.IsFalse(MediaRecordingNaming.TryJoin(null, fa, out p));
  }

  // ---------- bounded queues (§11) ----------

  [Test] public void P20D_BoundedQueueDropsOldestAndCloses() {
    var q = new BoundedByteQueue(3, 1L << 30);
    for (int i = 1; i <= 5; i++) Assert.IsTrue(q.TryEnqueue(new byte[] { (byte)i }));
    long enq, drop, deq;
    int cnt;
    q.ReadCounters(out enq, out drop, out deq, out cnt);
    Assert.AreEqual(5, enq);
    Assert.AreEqual(2, drop);
    Assert.AreEqual(3, cnt);
    byte[] first;
    Assert.IsTrue(q.TryDequeue(out first));
    Assert.AreEqual(3, first[0], "oldest (1,2) dropped, 3 first out");
    q.Close();
    Assert.IsTrue(q.IsClosed);
    Assert.IsFalse(q.TryEnqueue(new byte[] { 9 }), "closed: no new samples");
    Assert.IsTrue(q.TryDequeue(out first), "closed: drain still allowed");
  }

  [Test] public void P20E_SessionLatchRejectsForeign() {
    var l = new RecordingSessionLatch();
    Assert.IsTrue(l.Accept(7));
    Assert.IsTrue(l.Accept(7));
    Assert.IsFalse(l.Accept(9), "foreign serial never merges");
    Assert.AreEqual(1, l.DroppedForeign);
    l.Reset();
    Assert.IsFalse(l.IsLatched);
    Assert.IsTrue(l.Accept(9), "new epoch after reset");
  }

  [Test] public void P20F_TelemetryJsonCarriesScalarsOnly() {
    var t = new RecordingTelemetry {
      SessionId = "rec-test", Mode = RecordingMode.MicAndCamera,
      State = RecordingState.Completed, AudioChunks = 10, VideoFrames = 5,
      AudioPath = "C:\\r\\a.wav", VideoPath = "C:\\r\\v.avi",
    };
    string j = t.ToJson();
    Assert.IsTrue(j.Contains("rec-test") && j.Contains("Completed"));
    Assert.IsTrue(j.Contains("audioChunks") && j.Contains("videoFrames"));
    Assert.IsFalse(j.Contains("FFD8"), "never raw media bytes");
  }

  // ---------- WAV backend ----------

  [Test] public void P20G_WavRoundTripBitExact() {
    string dir = TempDir();
    try {
      string path = Path.Combine(dir, "a.wav");
      byte[] pcm = TonePcm16(1600, 0.25f);
      using (var w = new WavWriter()) {
        Assert.IsTrue(w.Begin(path));
        int n;
        Assert.IsTrue(w.AppendPcm16(pcm, 0, pcm.Length, out n));
        Assert.AreEqual(pcm.Length, n);
        long dataBytes;
        Assert.IsTrue(w.Finalize(out dataBytes));
        Assert.AreEqual(pcm.Length, dataBytes);
      }
      WavWriter.WavInfo info;
      Assert.IsTrue(WavWriter.TryReadInfo(path, out info) && info.Valid, info.Reason);
      Assert.AreEqual(16000, info.SampleRate);
      Assert.AreEqual(1, info.Channels);
      Assert.AreEqual(16, info.BitsPerSample);
      Assert.AreEqual(1600, info.SampleCount);
      Assert.AreEqual(0.1, info.DurationSec, 0.001);
      byte[] all = File.ReadAllBytes(path);
      int dataAt = FindTag(all, "data");
      Assert.Greater(dataAt, 0);
      var back = new byte[pcm.Length];
      Buffer.BlockCopy(all, dataAt + 8, back, 0, pcm.Length);
      CollectionAssert.AreEqual(pcm, back, "canonical PCM stored bit-exact");
    } finally { WipeDir(dir); }
  }

  static int FindTag(byte[] all, string tag) {
    byte[] t = System.Text.Encoding.ASCII.GetBytes(tag);
    for (int i = 0; i + 4 <= all.Length; i++)
      if (all[i] == t[0] && all[i + 1] == t[1] && all[i + 2] == t[2] && all[i + 3] == t[3]) return i;
    return -1;
  }

  [Test] public void P20H_WavEmptyIsInvalid() {
    string dir = TempDir();
    try {
      string path = Path.Combine(dir, "empty.wav");
      using (var w = new WavWriter()) {
        Assert.IsTrue(w.Begin(path));
        long n;
        Assert.IsTrue(w.Finalize(out n));
        Assert.AreEqual(0, n);
      }
      WavWriter.WavInfo info;
      Assert.IsFalse(WavWriter.TryReadInfo(path, out info) && info.Valid, "empty file is not valid media");
    } finally { WipeDir(dir); }
  }

  [Test] public void P20I_WavBadBeginNeverThrows() {
    using (var w = new WavWriter()) {
      Assert.IsFalse(w.Begin(null));
      Assert.IsFalse(w.Begin(""));
      Assert.IsFalse(w.Begin(Path.Combine("Z:\\no-such-dir-xyz", "a.wav")));
    }
  }

  // ---------- AVI/MJPEG backend ----------

  [Test] public void P20J_AviRoundTripFramesVerbatim() {
    string dir = TempDir();
    try {
      string path = Path.Combine(dir, "v.avi");
      var frames = new byte[5][];
      for (int i = 0; i < 5; i++) frames[i] = FakeJpeg(5000 + i * 100, (byte)(0x30 + i));
      using (var w = new AviMjpegWriter()) {
        Assert.IsTrue(w.Begin(path, 320, 240, 10));
        foreach (var f in frames) Assert.IsTrue(w.AppendJpeg(f));
        long n;
        Assert.IsTrue(w.Finalize(out n));
        Assert.AreEqual(5, n);
      }
      AviMjpegWriter.AviInfo info;
      Assert.IsTrue(AviMjpegWriter.TryReadInfo(path, out info) && info.Valid, info.Reason);
      Assert.AreEqual(320, info.Width);
      Assert.AreEqual(240, info.Height);
      Assert.AreEqual(5, info.FrameCount);
      Assert.AreEqual("MJPG", info.Handler, "strh inside LIST strl must parse");
      Assert.AreEqual(10.0, info.Fps, 0.001);
      Assert.AreEqual(0.5, info.DurationSec, 0.001);
      for (int i = 0; i < 5; i++) {
        byte[] back;
        string why;
        Assert.IsTrue(AviMjpegWriter.TryExtractFrame(path, i, out back, out why), why);
        CollectionAssert.AreEqual(frames[i], back, "frame " + i + " stored verbatim");
      }
      byte[] oob;
      string r2;
      Assert.IsFalse(AviMjpegWriter.TryExtractFrame(path, 5, out oob, out r2));
    } finally { WipeDir(dir); }
  }

  [Test] public void P20K_AviRejectsNonJpeg() {
    string dir = TempDir();
    try {
      string path = Path.Combine(dir, "v.avi");
      using (var w = new AviMjpegWriter()) {
        Assert.IsTrue(w.Begin(path, 320, 240, 10));
        Assert.IsFalse(w.AppendJpeg(null));
        Assert.IsFalse(w.AppendJpeg(new byte[0]));
        Assert.IsFalse(w.AppendJpeg(new byte[] { 1, 2, 3, 4 }), "non-JPEG rejected");
        Assert.AreEqual(0, w.FrameCount);
        Assert.IsTrue(w.AppendJpeg(FakeJpeg(100, 0x41)));
        Assert.AreEqual(1, w.FrameCount);
        long n;
        Assert.IsTrue(w.Finalize(out n));
      }
      AviMjpegWriter.AviInfo info;
      Assert.IsTrue(AviMjpegWriter.TryReadInfo(path, out info) && info.Valid);
      Assert.AreEqual(1, info.FrameCount);
    } finally { WipeDir(dir); }
  }

  [Test] public void P20L_AviEmptyAndBadBegin() {
    string dir = TempDir();
    try {
      string path = Path.Combine(dir, "empty.avi");
      using (var w = new AviMjpegWriter()) {
        Assert.IsTrue(w.Begin(path, 320, 240, 10));
        long n;
        Assert.IsTrue(w.Finalize(out n));
      }
      AviMjpegWriter.AviInfo info;
      Assert.IsFalse(AviMjpegWriter.TryReadInfo(path, out info) && info.Valid, "empty video is not valid media");
    } finally { WipeDir(dir); }
    using (var w2 = new AviMjpegWriter()) {
      Assert.IsFalse(w2.Begin(null, 320, 240, 10));
      Assert.IsFalse(w2.Begin("x.avi", 9999, 240, 10));
      Assert.IsFalse(w2.Begin("x.avi", 320, 240, 0));
    }
  }

  // ---------- service lifecycle (§14) ----------

  [Test] public void P20M_FullSessionMicAndCamera() {
    string dir = TempDir();
    MediaRecordingService rec = null;
    try {
      rec = NewService(dir);
      Assert.AreEqual(RecordingState.Idle, rec.CurrentState);
      Assert.IsTrue(rec.StartRecording(RecordingMode.MicAndCamera), rec.LastError);
      Assert.AreEqual(RecordingState.Recording, rec.CurrentState);
      byte[] chunk = TonePcm16(1600, 0.25f);
      for (int i = 0; i < 10; i++) Assert.IsTrue(rec.TestEnqueueAudio(7, chunk));
      for (int i = 0; i < 5; i++) Assert.IsTrue(rec.TestEnqueueVideo(3, FakeJpeg(4000, (byte)(0x40 + i))));
      for (int i = 0; i < 4; i++) Assert.IsTrue(rec.TestEnqueueGameRaw(3, TestRgba(i * 20)));
      Assert.IsTrue(rec.StopRecording());
      Assert.AreEqual(RecordingState.Stopping, rec.CurrentState);
      Assert.IsTrue(WaitTerminal(rec, 15000), "must finalize, state=" + rec.CurrentState);
      Assert.AreEqual(RecordingState.Completed, rec.CurrentState);
      var t = rec.ReadTelemetrySnapshot();
      Assert.AreEqual(10, t.AudioChunks);
      Assert.AreEqual(16000, t.AudioSamples);
      Assert.AreEqual(5, t.VideoFrames);
      Assert.AreEqual(4, t.GameFrames);
      Assert.IsFalse(t.Interrupted);
      Assert.IsTrue(t.AudioComplete && t.VideoComplete && t.GameComplete);
      Assert.IsFalse(t.Transcoded, "unit env disables transcode; intermediates are the verdict");
      Assert.IsTrue(File.Exists(rec.AudioPath) && new FileInfo(rec.AudioPath).Length > 0);
      Assert.IsTrue(File.Exists(rec.VideoPath) && new FileInfo(rec.VideoPath).Length > 0);
      string gamePath = Path.Combine(dir, rec.SessionId + "_game.avi");
      Assert.IsTrue(File.Exists(gamePath) && new FileInfo(gamePath).Length > 0);
      string sidecar = Path.Combine(dir, rec.SessionId + ".json");
      Assert.IsTrue(File.Exists(sidecar), "traceable sidecar per session");
      Assert.IsTrue(File.ReadAllText(sidecar).Contains(rec.SessionId));
    } finally {
      KillService(rec);
      WipeDir(dir);
    }
  }

  [Test] public void P20N_StartRefusesWhenGameHasNoMedia() {
    string dir = TempDir();
    MediaRecordingService rec = null;
    try {
      var go = new UnityEngine.GameObject("MediaRecTest");
      rec = go.AddComponent<MediaRecordingService>();
      rec.Bind(MediaRecordingConfig.Default, null, null,
        (Func<PhonePresenceWatcher>)(() => (PhonePresenceWatcher)null), dir);
      // No force flag: no watcher, no cameras -> explicit refusal.
      Assert.IsFalse(rec.StartRecording(RecordingMode.MicAndCamera));
      Assert.AreEqual(RecordingState.Error, rec.CurrentState);
      Assert.IsTrue(rec.LastError.Contains("no-audio-in-game"), rec.LastError);
      // Error is recoverable by the next explicit Start (no wedged encoder).
      Assert.IsFalse(rec.StartRecording(RecordingMode.CameraOnly));
      Assert.AreEqual(RecordingState.Error, rec.CurrentState);
    } finally {
      KillService(rec);
      WipeDir(dir);
    }
  }

  [Test] public void P20O_SerialChangeMarksInterruptedKeepsMedia() {
    string dir = TempDir();
    MediaRecordingService rec = null;
    try {
      rec = NewService(dir);
      Assert.IsTrue(rec.StartRecording(RecordingMode.MicAndCamera));
      byte[] chunk = TonePcm16(800, 0.2f);
      Assert.IsTrue(rec.TestEnqueueAudio(7, chunk));
      Assert.IsTrue(rec.TestEnqueueAudio(7, chunk));
      Assert.IsTrue(rec.TestEnqueueAudio(9, chunk), "reconnect serial continues, flagged");
      Assert.IsTrue(rec.TestEnqueueVideo(4, FakeJpeg(3000, 0x41)));
      Assert.IsTrue(rec.TestEnqueueVideo(5, FakeJpeg(3000, 0x42)));
      Assert.IsTrue(rec.StopRecording());
      Assert.IsTrue(WaitTerminal(rec, 15000));
      Assert.AreEqual(RecordingState.Completed, rec.CurrentState);
      var t = rec.ReadTelemetrySnapshot();
      Assert.AreEqual(3, t.AudioChunks);
      Assert.AreEqual(2, t.VideoFrames);
      Assert.IsTrue(t.Interrupted, "serial change must be observable, never silent");
      Assert.IsTrue(t.AudioComplete && t.VideoComplete);
    } finally {
      KillService(rec);
      WipeDir(dir);
    }
  }

  [Test] public void P20P_RepeatedSessionsStayIndependent() {
    string dir = TempDir();
    MediaRecordingService rec = null;
    try {
      rec = NewService(dir);
      Assert.IsTrue(rec.StartRecording(RecordingMode.MicAndCamera));
      Assert.IsTrue(rec.TestEnqueueAudio(1, TonePcm16(1600, 0.25f)));
      Assert.IsTrue(rec.TestEnqueueVideo(1, FakeJpeg(3000, 0x41)));
      Assert.IsTrue(rec.StopRecording());
      Assert.IsTrue(WaitTerminal(rec, 15000));
      Assert.AreEqual(RecordingState.Completed, rec.CurrentState);
      string s1 = rec.SessionId, a1 = rec.AudioPath, v1 = rec.VideoPath;
      // Session 2 from Completed: fresh files, no stale samples.
      Assert.IsTrue(rec.StartRecording(RecordingMode.MicAndCamera));
      Assert.AreNotEqual(s1, rec.SessionId, "no accidental overwrite");
      Assert.IsTrue(rec.TestEnqueueAudio(2, TonePcm16(800, 0.2f)));
      Assert.IsTrue(rec.TestEnqueueVideo(2, FakeJpeg(3000, 0x42)));
      Assert.IsTrue(rec.StopRecording());
      Assert.IsTrue(WaitTerminal(rec, 15000));
      Assert.AreEqual(RecordingState.Completed, rec.CurrentState);
      var t = rec.ReadTelemetrySnapshot();
      Assert.AreEqual(800, t.AudioSamples, "session 2 has no session-1 samples");
      Assert.AreEqual(1, t.VideoFrames);
      Assert.IsTrue(File.Exists(a1) && File.Exists(v1), "session 1 files retained");
      Assert.IsTrue(File.Exists(rec.AudioPath) && File.Exists(rec.VideoPath));
    } finally {
      KillService(rec);
      WipeDir(dir);
    }
  }

  [Test] public void P20Q_OutputDirFailureIsExplicitError() {
    MediaRecordingService rec = null;
    string probe = Path.GetTempFileName(); // a FILE, not a dir
    try {
      var go = new UnityEngine.GameObject("MediaRecTest");
      rec = go.AddComponent<MediaRecordingService>();
      var cfg = MediaRecordingConfig.Default;
      cfg.OutputDirOverride = probe;
      rec.Bind(cfg, null, null, (Func<PhonePresenceWatcher>)(() => (PhonePresenceWatcher)null), null);
      rec.SetTestForceSourcesAvailable(true);
      Assert.IsFalse(rec.StartRecording(RecordingMode.MicOnly));
      Assert.AreEqual(RecordingState.Error, rec.CurrentState);
      Assert.IsTrue(rec.LastError.Contains("output-dir"), rec.LastError);
    } finally {
      KillService(rec);
      try { File.Delete(probe); } catch (Exception) { }
    }
  }

  [Test] public void P20R_SingleMediumModes() {
    string dir = TempDir();
    MediaRecordingService rec = null;
    try {
      rec = NewService(dir);
      Assert.IsTrue(rec.StartRecording(RecordingMode.MicOnly));
      Assert.IsTrue(rec.TestEnqueueAudio(1, TonePcm16(1600, 0.3f)));
      Assert.IsFalse(rec.TestEnqueueVideo(1, FakeJpeg(2000, 0x41)), "video off: not accepted");
      Assert.IsTrue(rec.StopRecording());
      Assert.IsTrue(WaitTerminal(rec, 15000));
      Assert.AreEqual(RecordingState.Completed, rec.CurrentState);
      var t = rec.ReadTelemetrySnapshot();
      Assert.IsTrue(t.AudioComplete);
      Assert.IsFalse(t.VideoComplete, "video was not requested");
      Assert.AreEqual(0, Directory.GetFiles(dir, "*.avi").Length, "no stray video file");
      Assert.AreEqual(1, Directory.GetFiles(dir, "*.wav").Length);

      Assert.IsTrue(rec.StartRecording(RecordingMode.CameraOnly));
      Assert.IsFalse(rec.TestEnqueueAudio(1, TonePcm16(1600, 0.3f)), "audio off: not accepted");
      Assert.IsTrue(rec.TestEnqueueVideo(1, FakeJpeg(2000, 0x41)));
      Assert.IsTrue(rec.TestEnqueueGameRaw(1, TestRgba(7)));
      Assert.IsTrue(rec.StopRecording());
      Assert.IsTrue(WaitTerminal(rec, 15000));
      Assert.AreEqual(RecordingState.Completed, rec.CurrentState);
      t = rec.ReadTelemetrySnapshot();
      Assert.IsTrue(t.VideoComplete);
      Assert.IsTrue(t.GameComplete);
      Assert.IsFalse(t.AudioComplete);
      Assert.AreEqual(1, Directory.GetFiles(dir, "*.wav").Length, "session-1 wav retained, none new");
      Assert.AreEqual(2, Directory.GetFiles(dir, "*.avi").Length, "cam.avi + game.avi");
    } finally {
      KillService(rec);
      WipeDir(dir);
    }
  }

  [Test] public void P20S_NoDuplicateEncoderOrDoubleStop() {
    string dir = TempDir();
    MediaRecordingService rec = null;
    try {
      rec = NewService(dir);
      Assert.IsTrue(rec.StartRecording(RecordingMode.MicOnly));
      Assert.IsFalse(rec.StartRecording(RecordingMode.MicOnly), "no duplicate encoder mid-session");
      Assert.AreEqual(RecordingState.Recording, rec.CurrentState);
      Assert.IsTrue(rec.TestEnqueueAudio(1, TonePcm16(1600, 0.2f)));
      Assert.IsTrue(rec.StopRecording());
      Assert.IsFalse(rec.StopRecording(), "no double stop");
      Assert.IsTrue(WaitTerminal(rec, 15000));
      Assert.AreEqual(RecordingState.Completed, rec.CurrentState);
    } finally {
      KillService(rec);
      WipeDir(dir);
    }
  }

  // ---------- real-socket watcher tap (game-observed bytes -> recorder) ----------

  [Test] public void P20T_WatcherTapForwardsGameObservedAudio() {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    byte[] pcm = TonePcm16(1600, 0.25f);
    var server = Task.Run(() => {
      using (TcpClient c = listener.AcceptTcpClient())
      using (NetworkStream s = c.GetStream()) {
        var tmp = new byte[64];
        Assert.Greater(s.Read(tmp, 0, tmp.Length), 0, "watcher subscribes first");
        byte[] hello = PhoneMicProtocol.EncodeBridgeFrame(PhoneMicProtocol.KindHello, 0, 0, new byte[0]);
        s.Write(hello, 0, hello.Length);
        byte[] audio = PhoneMicProtocol.EncodeBridgeFrame(PhoneMicProtocol.KindAudio, 77, 5, pcm);
        s.Write(audio, 0, audio.Length);
        s.Flush();
        Task.Delay(1500).GetAwaiter().GetResult(); // let the watcher drain
      }
    });
    PhonePresenceWatcher watcher = null;
    try {
      watcher = new PhonePresenceWatcher("127.0.0.1", port);
      uint gotSerial = 0, gotSeq = 0;
      byte[] gotPayload = null;
      object m = new object();
      watcher.AudioPayloadAccepted += (serial, seq, payload) => {
        lock (m) {
          if (gotPayload == null && payload != null) {
            gotSerial = serial;
            gotSeq = seq;
            gotPayload = (byte[])payload.Clone();
          }
        }
      };
      watcher.Start();
      int waited = 0;
      while (waited < 10000) {
        lock (m) { if (gotPayload != null) break; }
        Thread.Sleep(100);
        waited += 100;
      }
      Assert.IsTrue(server.Wait(10000), "server finished");
      lock (m) {
        Assert.IsNotNull(gotPayload, "tap must forward the game-observed payload");
        Assert.AreEqual(77u, gotSerial);
        Assert.AreEqual(5u, gotSeq);
        CollectionAssert.AreEqual(pcm, gotPayload, "recorder receives the SAME bytes the game saw");
      }
    } finally {
      try { if (watcher != null) watcher.Dispose(); } catch (Exception) { }
      try { listener.Stop(); } catch (Exception) { }
    }
  }

  // ---------- game-accepted video peek (slot -> recorder, no consume) ----------

  [Test] public void P20U_CameraPeekExposesAcceptedFrameWithoutConsuming() {
    var go = new UnityEngine.GameObject("CamSvcTest");
    GameCameraStreamService svc = null;
    try {
      svc = go.AddComponent<GameCameraStreamService>();
      svc.Bind("127.0.0.1", 1, PhoneCameraConfig.Default);
      svc.StartService();
      var tex = new UnityEngine.Texture2D(8, 8, UnityEngine.TextureFormat.RGBA32, false);
      byte[] jpeg;
      try {
        for (int y = 0; y < 8; y++)
          for (int x = 0; x < 8; x++)
            tex.SetPixel(x, y, new UnityEngine.Color(0.2f, 0.5f, 0.8f, 1f));
        tex.Apply();
        jpeg = UnityEngine.ImageConversion.EncodeToJPG(tex, 60);
      } finally {
        UnityEngine.Object.DestroyImmediate(tex);
      }
      Assert.IsTrue(svc.DecodeForTests(jpeg), "game accepts + decodes the frame");
      uint serial, seq;
      byte[] peek;
      int age;
      Assert.IsTrue(svc.TryPeekAcceptedJpegForRecording(out serial, out seq, out peek, out age));
      Assert.AreEqual(1u, serial);
      CollectionAssert.AreEqual(jpeg, peek);
      // Peek again: still there (display path untouched, no consume-mark).
      Assert.IsTrue(svc.TryPeekAcceptedJpegForRecording(out serial, out seq, out peek, out age));
      CollectionAssert.AreEqual(jpeg, peek);
    } finally {
      try { UnityEngine.Object.DestroyImmediate(go); } catch (Exception) { }
    }
  }

  [Test] public void P20V_RecordingHidesCameraBoxRestoresOnStop() {
    // One face in the file (user rule): the service hides the bound camera
    // box on entering Recording and shows it back on Stop.
    string dir = TempDir();
    MediaRecordingService rec = null;
    PhoneCameraHud hud = null;
    try {
      rec = NewService(dir);
      var hudGo = new UnityEngine.GameObject("HudP20V");
      hud = hudGo.AddComponent<PhoneCameraHud>();
      hud.BuildHudImmediate();
      rec.BindCameraHud(hud);
      Assert.IsFalse(hud.IsRecordingHidden);
      Assert.IsTrue(rec.StartRecording(RecordingMode.MicAndCamera), rec.LastError);
      Assert.IsTrue(hud.IsRecordingHidden, "box steps aside while recording");
      Assert.IsFalse(hud.IsShowing, "hidden box stays hidden");
      Assert.IsTrue(rec.StopRecording());
      Assert.IsFalse(hud.IsRecordingHidden, "box returns on stop");
      Assert.IsTrue(WaitTerminal(rec, 15000));
      Assert.AreEqual(RecordingState.Completed, rec.CurrentState);
    } finally {
      KillService(rec);
      try { if (hud != null) UnityEngine.Object.DestroyImmediate(hud.gameObject); } catch (Exception) { }
      WipeDir(dir);
    }
  }

  [Test] public void P20W_AviHeaderRestampsToMeasuredRate() {
    // Wall-clock honesty: a short-count file re-stamps its header so duration
    // == wall (else -shortest truncates the sibling streams in transcode).
    string dir = TempDir();
    try {
      string path = Path.Combine(dir, "restamp.avi");
      var w = new AviMjpegWriter();
      Assert.IsTrue(w.Begin(path, 64, 48, 20));
      byte[] jpeg = FakeJpeg(2000, 0x50);
      for (int i = 0; i < 10; i++) Assert.IsTrue(w.AppendJpeg(jpeg));
      long frames;
      Assert.IsTrue(w.Finalize(out frames, 10.0));
      Assert.AreEqual(10, frames);
      try { w.Close(); } catch (Exception) { }
      AviMjpegWriter.AviInfo info;
      Assert.IsTrue(AviMjpegWriter.TryReadInfo(path, out info), info.Reason);
      Assert.AreEqual(10, info.FrameCount);
      Assert.AreEqual(10.0, info.Fps, 0.01);
      Assert.AreEqual(1.0, info.DurationSec, 0.01, "10 frames @ measured 10fps == 1 s wall");
      // Declared-rate path untouched (old behavior for unit-speed sessions).
      string path2 = Path.Combine(dir, "declared.avi");
      var w2 = new AviMjpegWriter();
      Assert.IsTrue(w2.Begin(path2, 64, 48, 20));
      for (int i = 0; i < 10; i++) Assert.IsTrue(w2.AppendJpeg(jpeg));
      Assert.IsTrue(w2.Finalize(out frames));
      try { w2.Close(); } catch (Exception) { }
      Assert.IsTrue(AviMjpegWriter.TryReadInfo(path2, out info), info.Reason);
      Assert.AreEqual(20.0, info.Fps, 0.01);
    } finally { WipeDir(dir); }
  }

  [Test] public void P20X_AmbientScreenIgnoredWhileNotPlaying() {
    // Batch scenes may carry cameras/screens: capture-size resolve applies
    // ONLY while playing, so default-sized injections always match in unit
    // env (regression: an ungated resolve broke P20M/P20R/P21L/P21M).
    string dir = TempDir();
    MediaRecordingService rec = null;
    try {
      rec = NewService(dir);
      Assert.IsTrue(rec.StartRecording(RecordingMode.MicAndCamera), rec.LastError);
      Assert.IsTrue(rec.TestEnqueueGameRaw(1, TestRgba(7)), "default-size buffer must fit");
      var t = rec.ReadTelemetrySnapshot();
      Assert.AreEqual(MediaRecording.DefaultGameWidth, t.GameWidth);
      Assert.AreEqual(MediaRecording.DefaultGameHeight, t.GameHeight);
      Assert.IsTrue(rec.StopRecording());
      Assert.IsTrue(WaitTerminal(rec, 20000));
      Assert.AreEqual(RecordingState.Completed, rec.CurrentState);
    } finally {
      KillService(rec);
      WipeDir(dir);
    }
  }
}
