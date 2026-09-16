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
      string gamePath = Path.Combine(dir, MediaRecordingNaming.GameFileName(rec.SessionId));
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
      Assert.AreEqual(1, Directory.GetFiles(dir, "*.avi").Length, "cam.avi only");
      Assert.AreEqual(1, Directory.GetFiles(dir, "*.rawvid").Length, "game.rawvid");
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

  [Test] public void P20Z_EncoderThreadCountStaysSane() {
    // Raw path runs single-threaded (memcpy-speed writes need no farm):
    // a full session must report exactly 1 writer thread + live CPU count.
    string dir = TempDir();
    MediaRecordingService rec = null;
    try {
      rec = NewService(dir);
      Assert.IsTrue(rec.StartRecording(RecordingMode.MicAndCamera), rec.LastError);
      Assert.IsTrue(rec.TestEnqueueGameRaw(1, TestRgba(3)));
      Assert.IsTrue(rec.StopRecording());
      Assert.IsTrue(WaitTerminal(rec, 20000));
      Assert.AreEqual(RecordingState.Completed, rec.CurrentState);
      var t = rec.ReadTelemetrySnapshot();
      Assert.AreEqual(1, t.GameEncodeThreads);
      Assert.GreaterOrEqual(t.CpuCount, 1);
    } finally {
      KillService(rec);
      WipeDir(dir);
    }
  }

  [Test] public void P20AA_RawWriterRoundTripRestamp() {
    // Lossless gameplay container: exact-size RGBA in/out, header restamps
    // to the measured rate, handler reads back DIB (not MJPG).
    string dir = TempDir();
    try {
      string path = Path.Combine(dir, "raw.avi");
      var w = new AviMjpegWriter();
      Assert.IsTrue(w.BeginRaw(path, 64, 48, 20));
      byte[] rgba = new byte[64 * 48 * 4];
      for (int i = 0; i < rgba.Length; i++) rgba[i] = (byte)(i & 0xFF);
      Assert.IsTrue(w.AppendRawFrame(rgba));
      Assert.IsTrue(w.AppendRawFrame(rgba));
      // Wrong-size payloads are refused, never adapted.
      Assert.IsFalse(w.AppendRawFrame(new byte[10]));
      // MJPEG appends are refused on a raw track (never mix).
      Assert.IsFalse(w.AppendJpeg(FakeJpeg(500, 0x60)));
      long frames;
      Assert.IsTrue(w.Finalize(out frames, 10.0));
      Assert.AreEqual(2, frames);
      try { w.Close(); } catch (Exception) { }
      AviMjpegWriter.AviInfo info;
      Assert.IsTrue(AviMjpegWriter.TryReadInfo(path, out info), info.Reason);
      Assert.AreEqual(64, info.Width);
      Assert.AreEqual(48, info.Height);
      Assert.AreEqual(2, info.FrameCount);
      Assert.AreEqual(10.0, info.Fps, 0.01);
      Assert.AreEqual("DIB ", info.Handler);
      // Default (MJPEG) extract refuses raw payloads; allowRaw returns them.
      byte[] out_;
      string why;
      Assert.IsFalse(AviMjpegWriter.TryExtractFrame(path, 0, out out_, out why));
      Assert.IsTrue(AviMjpegWriter.TryExtractFrame(path, 1, out out_, out why, true), why);
      Assert.IsNotNull(out_);
      Assert.AreEqual(64 * 48 * 4, out_.Length);
      CollectionAssert.AreEqual(rgba, out_, "lossless: bit-exact round trip");
      // Header layout pin (P2X exit-22 root cause): strf chunk size 40 +
      // biSize 40 (double-40, not a duplication) + negative biHeight for
      // top-down raw. Read the bytes directly, independent of the writer.
      byte[] blob = File.ReadAllBytes(path);
      int at = FindTag(blob, "strf");
      Assert.GreaterOrEqual(at, 0, "strf present");
      Assert.AreEqual(40u, RdU32(blob, at + 4), "strf chunk size");
      Assert.AreEqual(40u, RdU32(blob, at + 8), "biSize");
      Assert.AreEqual(64, RdI32(blob, at + 12), "biWidth");
      Assert.AreEqual(-48, RdI32(blob, at + 16), "raw biHeight negative (top-down)");
    } finally { WipeDir(dir); }
  }

  static uint RdU32(byte[] b, int o) {
    return (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
  }

  static int RdI32(byte[] b, int o) {
    return unchecked((int)RdU32(b, o));
  }

  [Test] public void P20AC_RawHeadersStayFfmpegReadable() {
    // MJPEG keeps the legacy positive height; raw declares top-down.
    // Both keep the double-40 strf layout (chunk + biSize).
    string dir = TempDir();
    try {
      string mj = Path.Combine(dir, "mj.avi");
      var w = new AviMjpegWriter();
      Assert.IsTrue(w.Begin(mj, 64, 48, 20));
      Assert.IsTrue(w.AppendJpeg(FakeJpeg(500, 0x60)));
      long n;
      Assert.IsTrue(w.Finalize(out n));
      try { w.Close(); } catch (Exception) { }
      byte[] blob = File.ReadAllBytes(mj);
      int at = FindTag(blob, "strf");
      Assert.GreaterOrEqual(at, 0);
      Assert.AreEqual(40u, RdU32(blob, at + 4));
      Assert.AreEqual(40u, RdU32(blob, at + 8));
      Assert.AreEqual(48, RdI32(blob, at + 16), "mjpeg keeps positive height");
      // Transcode graph normalizes the main to yuv420p so BI_RGB BGRA and
      // MJPEG yuvj both overlay cleanly (raw exit-22 pin).
      var spec = new TranscodeSpec {
        GameAvi = "g.avi", CamAvi = "c.avi", MicWav = "m.wav",
        OutMp4 = "s.mp4", OutMp3 = "s.mp3",
        GameFpsActual = 20, CamFpsActual = 10,
        PipWidth = 240, PipMargin = 16, Crf = 12,
        Preset = "slow", Mp3Quality = 4,
      };
      string args = FfmpegTranscodeBackend.BuildArguments(spec);
      StringAssert.Contains("[0:v]format=yuv420p[main]", args);
      StringAssert.Contains("[main][pip]overlay=", args);
    } finally { WipeDir(dir); }
  }

  [Test] public void P20AB_DriveSpaceGuardHonest() {
    string dir = TempDir();
    try {
      Assert.IsTrue(MediaRecording.DriveSpaceOk(dir, 1), "temp dir has bytes free");
      Assert.IsFalse(MediaRecording.DriveSpaceOk(null, 1));
      Assert.IsFalse(MediaRecording.DriveSpaceOk(string.Empty, 1));
      Assert.IsFalse(MediaRecording.DriveSpaceOk("::bogus-drive::", 1));
    } finally { WipeDir(dir); }
  }

  [Test] public void P20AD_PacingStatsHonest() {
    // 30 Hz steady: mean ~33.3, max ~33.3, zero holes. One hitch: max + over.
    var p = new RecordingPacing();
    for (int i = 0; i < 90; i++) p.AddSample(1000.0 / 30.0);
    Assert.AreEqual(90, p.N);
    Assert.AreEqual(1000.0 / 30.0, p.MeanMs(), 0.001);
    Assert.AreEqual(1000.0 / 30.0, p.MaxMs, 0.001);
    Assert.AreEqual(0, p.Over50);
    Assert.AreEqual(0.0, p.StdMs(), 0.5);
    p.AddSample(120.0);
    Assert.AreEqual(1, p.Over50);
    Assert.AreEqual(120.0, p.MaxMs, 0.001);
    Assert.Greater(p.StdMs(), 0.0);
    // Garbage in, no-throw out (never let a probe break a session).
    p.AddSample(-5.0);
    p.AddSample(double.NaN);
    p.AddSample(double.PositiveInfinity);
    Assert.AreEqual(91, p.N);
    var q = new RecordingPacing();
    Assert.AreEqual(0, q.N);
    Assert.AreEqual(0.0, q.MeanMs());
    Assert.AreEqual(0.0, q.StdMs());
  }

  [Test] public void P20AE_FrameSampleHashDetectsDuplicates() {
    byte[] a = new byte[64 * 48 * 4];
    for (int i = 0; i < a.Length; i++) a[i] = (byte)(i & 0xFF);
    byte[] same = (byte[])a.Clone();
    byte[] diff = (byte[])a.Clone();
    diff[4096] ^= 0xFF; // one sampled byte flips -> different content
    Assert.AreEqual(FrameSampleHash.Hash(a, 4096), FrameSampleHash.Hash(same, 4096));
    Assert.AreNotEqual(FrameSampleHash.Hash(a, 4096), FrameSampleHash.Hash(diff, 4096));
    Assert.AreEqual(0u, FrameSampleHash.Hash(null, 4096));
    Assert.AreEqual(0u, FrameSampleHash.Hash(new byte[0], 4096));
    Assert.AreNotEqual(0u, FrameSampleHash.Hash(a, 4096));
  }

  [Test] public void P20AG_RawvidRoundTripRefusals() {
    // Stream container: exact-size BGRA in/out, count from length math,
    // torn tails and wrong sizes refused (the >4GB AVI failure mode cannot
    // exist here — there are no 32-bit size fields to wrap).
    string dir = TempDir();
    try {
      string path = Path.Combine(dir, "g.rawvid");
      var w = new RawVideoWriter();
      Assert.IsTrue(w.Begin(path, 64, 48));
      // Refusal probes use a dedicated instance (a refused Begin must never
      // disturb a live session).
      var badBegin = new RawVideoWriter();
      Assert.IsFalse(badBegin.Begin(null, 64, 48));
      Assert.IsFalse(badBegin.Begin("", 64, 48));
      Assert.IsFalse(badBegin.Begin(Path.Combine(dir, "x.rawvid"), 4, 4), "dims floor");
      byte[] bgra = new byte[64 * 48 * 4];
      for (int i = 0; i < bgra.Length; i++) bgra[i] = (byte)((i * 7) & 0xFF);
      Assert.IsTrue(w.AppendFrame(bgra));
      Assert.IsTrue(w.AppendFrame(bgra));
      Assert.IsTrue(w.AppendFrame(bgra));
      Assert.IsFalse(w.AppendFrame(new byte[10]), "wrong size refused, never adapted");
      Assert.IsFalse(w.AppendFrame(null));
      long frames;
      Assert.IsTrue(w.Finalize(out frames));
      Assert.AreEqual(3, frames);
      Assert.AreEqual(3, w.FrameCount);
      try { w.Close(); } catch (Exception) { }
      RawVideoReader.RawInfo info;
      Assert.IsTrue(RawVideoReader.TryReadInfo(path, out info), info.Reason);
      Assert.IsTrue(info.Valid);
      Assert.AreEqual(64, info.Width);
      Assert.AreEqual(48, info.Height);
      Assert.AreEqual(3, info.FrameCount);
      Assert.AreEqual(64 * 48 * 4, info.FrameBytes);
      // Footer contract: byte 0 = frame 0 (no header shift, P30 forensic);
      // last 24 B carry magic + dims + exact count.
      byte[] blob = File.ReadAllBytes(path);
      Assert.AreEqual(3 * 64 * 48 * 4 + 24, blob.Length);
      int f0 = blob.Length - 24;
      Assert.AreEqual("LWRV", System.Text.Encoding.ASCII.GetString(blob, f0, 4));
      Assert.AreEqual(1u, (uint)(blob[f0 + 4] | (blob[f0 + 5] << 8)
        | (blob[f0 + 6] << 16) | (blob[f0 + 7] << 24)));
      Assert.AreEqual(3u, (uint)(blob[f0 + 16] | (blob[f0 + 17] << 8)
        | (blob[f0 + 18] << 16) | (blob[f0 + 19] << 24)), "footer count");
      for (int i = 0; i < 3; i++) {
        byte[] out_;
        string why;
        Assert.IsTrue(RawVideoReader.TryExtractFrame(path, i, 64, 48, out out_, out why), why);
        CollectionAssert.AreEqual(bgra, out_, "lossless frame " + i);
      }
      byte[] miss;
      string whyMiss;
      Assert.IsFalse(RawVideoReader.TryExtractFrame(path, 3, 64, 48, out miss, out whyMiss));
      Assert.IsFalse(RawVideoReader.TryExtractFrame(path, 0, 32, 48, out miss, out whyMiss),
        "dims mismatch refused");
      // Torn tail: one byte short of a full frame fails info, loudly.
      string torn = Path.Combine(dir, "torn.rawvid");
      var w2 = new RawVideoWriter();
      Assert.IsTrue(w2.Begin(torn, 64, 48));
      Assert.IsTrue(w2.AppendFrame(bgra));
      long n2;
      Assert.IsTrue(w2.Finalize(out n2));
      try { w2.Close(); } catch (Exception) { }
      using (var fs = new FileStream(torn, FileMode.Append, FileAccess.Write)) {
        fs.Write(new byte[100], 0, 100);
      }
      RawVideoReader.RawInfo ti;
      Assert.IsFalse(RawVideoReader.TryReadInfo(torn, out ti) && ti.Valid, ti.Reason);
      // Non-rawvid bytes are refused with an explicit reason, never_valid.
      string fake = Path.Combine(dir, "fake.rawvid");
      File.WriteAllBytes(fake, new byte[64]);
      RawVideoReader.RawInfo fi2;
      Assert.IsFalse(RawVideoReader.TryReadInfo(fake, out fi2) && fi2.Valid);
      Assert.IsNotEmpty(fi2.Reason);
    } finally { WipeDir(dir); }
  }

  [Test] public void P20AH_ActiveSpanRateHonest() {
    // 529 frames over an 18.01 s start-anchored wall with a 0.36 s warmup =
    // 29.97 active, not 29.37: the pacing ground truth, reported as a rate.
    Assert.AreEqual(29.97, RecordingRates.ActiveStreamFps(529, 18.01, 360), 0.01);
    Assert.AreEqual(0.0, RecordingRates.ActiveStreamFps(0, 18.0, 360), "no frames = no rate");
    Assert.AreEqual(0.0, RecordingRates.ActiveStreamFps(10, 0.4, 0), "short wall means nothing");
    Assert.AreEqual(0.0, RecordingRates.ActiveStreamFps(10, 18.0, 18000), "warmup eating the wall guards");
    // Oversample tick: nominal 30.5 so measured nets >= 30 after losses.
    Assert.AreEqual(0.5f, MediaRecording.GameSampleOverHz);
    Assert.Greater(1f / (30 + MediaRecording.GameSampleOverHz), 0f);
    Assert.Less(1f / (30 + MediaRecording.GameSampleOverHz), 1f / 30f);
  }

  [Test] public void P20AI_RawvidTranscodeArgsDeclared() {
    // Headerless input: pix_fmt + size + measured rate declared per input.
    var s = new TranscodeSpec {
      GameAvi = Path.Combine("D:", "r", "g.rawvid"),
      CamAvi = Path.Combine("D:", "r", "c.avi"),
      MicWav = Path.Combine("D:", "r", "m.wav"),
      OutMp4 = Path.Combine("D:", "r", "s.mp4"),
      OutMp3 = Path.Combine("D:", "r", "s.mp3"),
      GameFpsActual = 30.5, CamFpsActual = 10,
      GameFrames = 540,
      GameWidth = 1920, GameHeight = 1080,
      PipWidth = 240, PipMargin = 16, Crf = 12,
      Preset = "slow", Mp3Quality = 4,
    };
    Assert.IsTrue(FfmpegTranscodeBackend.IsRawVideoInput(s.GameAvi));
    Assert.IsFalse(FfmpegTranscodeBackend.IsRawVideoInput(s.CamAvi));
    Assert.IsFalse(FfmpegTranscodeBackend.IsRawVideoInput(null));
    string args = FfmpegTranscodeBackend.BuildArguments(s);
    StringAssert.Contains("-f rawvideo", args);
    StringAssert.Contains("-pix_fmt bgra", args);
    StringAssert.Contains("-s 1920x1080", args);
    StringAssert.Contains("-framerate 30.5", args);
    StringAssert.Contains("-frames:v 540", args);
    StringAssert.Contains("[0:v]format=yuv420p[main]", args);
    StringAssert.Contains("[main][pip]overlay=", args);
    // Legacy AVI game input keeps the plain -i form (old evidence still builds).
    s.GameAvi = Path.Combine("D:", "r", "g.avi");
    string args2 = FfmpegTranscodeBackend.BuildArguments(s);
    Assert.IsFalse(args2.Contains("-f rawvideo"), "avi keeps header-probed input");
    Assert.IsFalse(args2.Contains("-frames:v"), "frame cap is rawvideo-only");
    StringAssert.Contains("-i", args2);
  }

  [Test] public void P20AJ_DiskGuardScalesWithTake() {
    // Real 1080p30 takes need the full 8 GB ceiling; 100 KB unit sessions
    // need only the floor — a flat floor blocked healthy small sessions
    // (P30-final: 10 session tests red on disk-space-low with GBs free).
    Assert.AreEqual(8L * 1024 * 1024 * 1024,
      MediaRecording.RequiredFreeBytes(1920, 1080, 30));
    Assert.AreEqual(256L * 1024 * 1024,
      MediaRecording.RequiredFreeBytes(64, 48, 20));
    Assert.AreEqual(256L * 1024 * 1024,
      MediaRecording.RequiredFreeBytes(0, 0, 0), "degenerate clamps to floor");
    Assert.AreEqual(8L * 1024 * 1024 * 1024,
      MediaRecording.RequiredFreeBytes(4096, 4096, 120), "ceiling holds");
    Assert.AreEqual(2L * 1024 * 1024 * 1024,
      MediaRecording.RequiredFreeBytesMid(1920, 1080, 30));
    Assert.AreEqual(64L * 1024 * 1024,
      MediaRecording.RequiredFreeBytesMid(64, 48, 20));
  }

  [Test] public void P20AF_CpuMathAndSidecarKeys() {
    // 12-thread box, process burned 36 cpu-sec over 18 s wall = 16.7%.
    Assert.AreEqual(16.7, CpuUtil.Pct(36.0, 18.0, 12), 0.05);
    Assert.AreEqual(0.0, CpuUtil.Pct(1.0, 0.0, 8), "zero wall guards div0");
    Assert.AreEqual(0.0, CpuUtil.Pct(-1.0, 10.0, 8), "negative cpu clamps");
    Assert.AreEqual(0.0, CpuUtil.Pct(1.0, 10.0, 0), "zero cores guards div0");
    double read = 0;
    Assert.DoesNotThrow(() => { read = CpuUtil.ProcessCpuSec(); });
    Assert.GreaterOrEqual(read, 0.0);
    // The fps gate reads these sidecar keys: fail the build if a rename drops one.
    string json = new RecordingTelemetry().ToJson();
    foreach (string k in new[] {
        "renderFrames", "renderMsMean", "renderMsMax",
        "gameCaptureRequests", "gameDuplicateFrames",
        "gamePacingN", "gamePacingMeanMs", "gamePacingMaxMs", "gamePacingOver50",
        "transcodeElapsedMs", "transcodeFps", "processCpuPct" }) {
      StringAssert.Contains("\"" + k + "\"", json);
    }
  }
}
