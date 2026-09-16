// CT-P21: Phase 2.3b full-session deliverables — deterministic suite.
// Gameplay (C# JPEG worker path) + FFmpeg transcode backend (locator,
// argument builder, runner) + MP4/MP3 session semantics. Real ffmpeg is
// NEVER required here (builder/locator/runner-missing are pure); the real
// binary proof lives in the compat run (Unity fixture -> real ffmpeg ->
// tools/verify_recording.py). REAL PHONE = user-run E2E only.
using NUnit.Framework;
using System;
using System.IO;
using System.Threading;

public class CT_P21_SessionDeliverables {
  static string TempDir() {
    string d = Path.Combine(Path.GetTempPath(),
      "LWE-P21-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(d);
    return d;
  }

  static void WipeDir(string d) {
    try { if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) Directory.Delete(d, true); }
    catch (Exception) { }
  }

  static byte[] RgbaGrad(int w, int h, int step) {
    var b = new byte[w * h * 4];
    for (int y = 0; y < h; y++)
      for (int x = 0; x < w; x++) {
        int o = (y * w + x) * 4;
        b[o] = (byte)((x + step) & 0xFF);
        b[o + 1] = (byte)((y + step) & 0xFF);
        b[o + 2] = (byte)(((x + y) / 2 + step) & 0xFF);
        b[o + 3] = 255;
      }
    return b;
  }

  static bool JpegDims(byte[] jpeg, out int w, out int h) {
    w = 0; h = 0;
    try {
      int i = 2; // skip SOI
      while (i + 4 < jpeg.Length) {
        if (jpeg[i] != 0xFF) { i++; continue; }
        byte m = jpeg[i + 1];
        if (m == 0xD8 || m == 0xD9 || (m >= 0xD0 && m <= 0xD7) || m == 0x01) { i += 2; continue; }
        int len = (jpeg[i + 2] << 8) | jpeg[i + 3];
        if (len < 2) return false;
        if (m == 0xC0) {
          h = (jpeg[i + 5] << 8) | jpeg[i + 6];
          w = (jpeg[i + 7] << 8) | jpeg[i + 8];
          return true;
        }
        i += 2 + len;
      }
    } catch (Exception) { }
    return false;
  }

  static MediaRecordingService NewService(string dir) {
    var go = new UnityEngine.GameObject("MediaRecTest21");
    var rec = go.AddComponent<MediaRecordingService>();
    rec.Bind(MediaRecordingConfig.Default, null, null,
      (Func<PhonePresenceWatcher>)(() => (PhonePresenceWatcher)null), dir);
    rec.SetTestForceSourcesAvailable(true);
    rec.SetTestDisableTranscode(true);
    rec.SetPrefsKeyForTests("LWE.Test.P21." + Guid.NewGuid().ToString("N"));
    return rec;
  }

  static void KillService(MediaRecordingService rec) {
    try { if (rec != null) UnityEngine.Object.DestroyImmediate(rec.gameObject); }
    catch (Exception) { }
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

  static byte[] TonePcm16(int samples, float level) {
    var b = new byte[samples * 2];
    short v = (short)(level * 32767f);
    for (int i = 0; i < samples; i++) {
      b[i * 2] = (byte)(v & 0xFF);
      b[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
    }
    return b;
  }

  // ---------- config (output shaping only; display stays max) ----------

  [Test] public void P21A_ConfigGameAndDeliverableDefaults() {
    var d = MediaRecordingConfig.Default;
    string reason;
    Assert.IsTrue(d.Validate(out reason), reason);
    Assert.AreEqual(1280, d.GameWidth);
    Assert.AreEqual(720, d.GameHeight);
    Assert.AreEqual(20, d.GameFps);
    Assert.AreEqual(90, d.GameJpegQuality);
    Assert.AreEqual(19, d.VideoCrf);
    Assert.AreEqual("veryfast", d.VideoPreset);
    Assert.AreEqual(4, d.AudioMp3Quality);
    Assert.AreEqual(240, d.PipWidth);
    Assert.AreEqual(16, d.PipMargin);
    Assert.IsFalse(d.KeepIntermediates);
    var c = d;
    c.GameWidth = 4096;
    Assert.IsFalse(c.Validate(out reason));
    c = d; c.VideoCrf = 40;
    Assert.IsFalse(c.Validate(out reason));
    c = d; c.VideoPreset = "placebo";
    Assert.IsFalse(c.Validate(out reason));
    c = d; c.AudioMp3Quality = 12;
    Assert.IsFalse(c.Validate(out reason));
    c = d; c.GameFps = 60;
    Assert.IsFalse(c.Validate(out reason));
    c = d; c.PipWidth = 64;
    Assert.IsFalse(c.Validate(out reason));
  }

  [Test] public void P21B_NamingCoversDeliverables() {
    string s = MediaRecordingNaming.NewSessionId(new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc));
    Assert.IsTrue(MediaRecordingNaming.GameFileName(s).StartsWith(s));
    Assert.IsTrue(MediaRecordingNaming.GameFileName(s).EndsWith(".avi"));
    Assert.IsTrue(MediaRecordingNaming.Mp4FileName(s).StartsWith(s));
    Assert.IsTrue(MediaRecordingNaming.Mp4FileName(s).EndsWith(".mp4"));
    Assert.IsTrue(MediaRecordingNaming.Mp3FileName(s).StartsWith(s));
    Assert.IsTrue(MediaRecordingNaming.Mp3FileName(s).EndsWith(".mp3"));
  }

  // ---------- C# JPEG encoder (worker path; real decoders judge it) ----------

  [Test] public void P21C_JpegEncoderEmitsValidContainer() {
    byte[] rgba = RgbaGrad(64, 48, 5);
    byte[] jpeg;
    Assert.IsTrue(JpegEncoder.TryEncode(rgba, 64, 48, 65, out jpeg));
    Assert.Greater(jpeg.Length, 512, "real content compresses to real bytes");
    Assert.Less(jpeg.Length, 64 * 48 * 4);
    Assert.AreEqual(0xFF, jpeg[0]);
    Assert.AreEqual(0xD8, jpeg[1]);
    Assert.AreEqual(0xFF, jpeg[2]);
    Assert.AreEqual(0xFF, jpeg[jpeg.Length - 2]);
    Assert.AreEqual(0xD9, jpeg[jpeg.Length - 1]);
    int w, h;
    Assert.IsTrue(JpegDims(jpeg, out w, out h), "SOF0 parses");
    Assert.AreEqual(64, w);
    Assert.AreEqual(48, h);
  }

  [Test] public void P21D_JpegEncoderRejectsBadInputNeverThrows() {
    byte[] jpeg;
    Assert.IsFalse(JpegEncoder.TryEncode(null, 64, 48, 65, out jpeg));
    Assert.IsFalse(JpegEncoder.TryEncode(new byte[10], 64, 48, 65, out jpeg));
    Assert.IsFalse(JpegEncoder.TryEncode(RgbaGrad(64, 48, 0), 4, 4, 65, out jpeg));
    Assert.IsFalse(JpegEncoder.TryEncode(RgbaGrad(64, 48, 0), 5000, 48, 65, out jpeg));
  }

  [Test] public void P21E_JpegQualityMovesCompression() {
    byte[] rgba = RgbaGrad(128, 96, 9);
    byte[] lo, hi;
    Assert.IsTrue(JpegEncoder.TryEncode(rgba, 128, 96, 30, out lo));
    Assert.IsTrue(JpegEncoder.TryEncode(rgba, 128, 96, 80, out hi));
    Assert.Greater(hi.Length, lo.Length, "higher quality keeps more bytes");
    int w, h;
    Assert.IsTrue(JpegDims(lo, out w, out h) && w == 128 && h == 96);
    Assert.IsTrue(JpegDims(hi, out w, out h) && w == 128 && h == 96);
  }

  // ---------- transcode argument builder (pure; ffmpeg judges it live) ----------

  static TranscodeSpec FullSpec(string dir) {
    return new TranscodeSpec {
      GameAvi = Path.Combine(dir, "g.avi"),
      CamAvi = Path.Combine(dir, "c.avi"),
      MicWav = Path.Combine(dir, "m.wav"),
      OutMp4 = Path.Combine(dir, "s.mp4"),
      OutMp3 = Path.Combine(dir, "s.mp3"),
      GameFpsActual = 24,
      CamFpsActual = 10,
      GameWidth = 960,
      GameHeight = 540,
      PipWidth = 240,
      PipMargin = 16,
      Crf = 24,
      Preset = "veryfast",
      Mp3Quality = 4,
    };
  }

  [Test] public void P21F_ArgsFullSession() {
    string args = FfmpegTranscodeBackend.BuildArguments(FullSpec("D:\\r"));
    StringAssert.Contains("-filter_complex", args);
    StringAssert.Contains("scale=240:180", args);
    StringAssert.Contains("overlay=W-w-16:H-h-16", args);
    StringAssert.Contains("libx264", args);
    StringAssert.Contains("-preset veryfast", args);
    StringAssert.Contains("-crf 24", args);
    StringAssert.Contains("libmp3lame", args);
    StringAssert.Contains("-q:a 4", args);
    StringAssert.Contains("-shortest", args);
    StringAssert.Contains("+faststart", args);
    StringAssert.Contains("-r 24", args); // wall-clock: measured game rate
    StringAssert.Contains("s.mp4", args);
    StringAssert.Contains("s.mp3", args);
  }

  [Test] public void P21G_ArgsAudioOnly() {
    var s = FullSpec("D:\\r");
    s.GameAvi = null;
    s.CamAvi = null;
    s.OutMp4 = null;
    string args = FfmpegTranscodeBackend.BuildArguments(s);
    Assert.IsFalse(args.Contains("libx264"), "no video encode for mic-only");
    Assert.IsFalse(args.Contains("filter_complex"));
    StringAssert.Contains("libmp3lame", args);
    StringAssert.Contains("s.mp3", args);
  }

  [Test] public void P21H_ArgsVideoOnly() {
    var s = FullSpec("D:\\r");
    s.MicWav = null;
    s.OutMp3 = null;
    string args = FfmpegTranscodeBackend.BuildArguments(s);
    StringAssert.Contains("-an", args);
    StringAssert.Contains("s.mp4", args);
    Assert.IsFalse(args.Contains("libmp3lame"));
    Assert.IsFalse(args.Contains("s.mp3"));
  }

  [Test] public void P21I_ArgsSanitizeOutOfRange() {
    var s = FullSpec("D:\\r");
    s.Crf = 99;
    s.Preset = "placebo";
    s.Mp3Quality = -3;
    string args = FfmpegTranscodeBackend.BuildArguments(s);
    StringAssert.Contains("-crf 19", args);
    StringAssert.Contains("-preset veryfast", args);
    StringAssert.Contains("-q:a 4", args);
  }

  // ---------- locator + runner (no ffmpeg required; never throws) ----------

  [Test] public void P21J_LocatorExplicitMissingIsNull() {
    string got = FfmpegTranscodeBackend.FindExecutable(
      Path.Combine(Path.GetTempPath(), "no-such-ffmpeg-xyz.exe"), null, null);
    Assert.IsNull(got, "explicit-but-missing substitutes nothing");
  }

  [Test] public void P21K_RunnerMissingBinaryFailsExplicitly() {
    var s = FullSpec(Path.GetTempPath());
    TranscodeResult r = FfmpegTranscodeBackend.Run(
      "definitely-not-a-binary-xyz-123", s, 15000);
    Assert.IsFalse(r.Ok);
    Assert.IsNotEmpty(r.Error);
  }

  // ---------- game session semantics (intermediates; transcode off in unit) ----------

  [Test] public void P21L_GameSessionFallbackKeepsIntermediates() {
    string dir = TempDir();
    MediaRecordingService rec = null;
    try {
      rec = NewService(dir);
      Assert.IsTrue(rec.StartRecording(RecordingMode.MicAndCamera), rec.LastError);
      Assert.IsTrue(rec.TestEnqueueAudio(1, TonePcm16(1600, 0.25f)));
      Assert.IsTrue(rec.TestEnqueueGameRaw(1, RgbaGrad(
        MediaRecording.DefaultGameWidth, MediaRecording.DefaultGameHeight, 3)));
      Assert.IsTrue(rec.TestEnqueueGameRaw(1, RgbaGrad(
        MediaRecording.DefaultGameWidth, MediaRecording.DefaultGameHeight, 40)));
      Assert.IsTrue(rec.StopRecording());
      Assert.IsTrue(WaitTerminal(rec, 20000), "worker JPEG must drain, state=" + rec.CurrentState);
      Assert.AreEqual(RecordingState.Completed, rec.CurrentState);
      var t = rec.ReadTelemetrySnapshot();
      Assert.AreEqual(2, t.GameFrames);
      Assert.IsTrue(t.GameComplete);
      Assert.IsFalse(t.Transcoded, "unit env disables transcode");
      Assert.IsFalse(string.IsNullOrEmpty(t.GamePath));
      Assert.IsTrue(File.Exists(t.GamePath) && new FileInfo(t.GamePath).Length > 0);
      Assert.GreaterOrEqual(t.FirstGameOffsetMs, 0);
    } finally {
      KillService(rec);
      WipeDir(dir);
    }
  }

  [Test] public void P21M_GameSerialChangeFlagged() {
    string dir = TempDir();
    MediaRecordingService rec = null;
    try {
      rec = NewService(dir);
      Assert.IsTrue(rec.StartRecording(RecordingMode.CameraOnly));
      int w = MediaRecording.DefaultGameWidth, h = MediaRecording.DefaultGameHeight;
      Assert.IsTrue(rec.TestEnqueueGameRaw(1, RgbaGrad(w, h, 1)));
      Assert.IsTrue(rec.TestEnqueueGameRaw(2, RgbaGrad(w, h, 2)));
      Assert.IsTrue(rec.StopRecording());
      Assert.IsTrue(WaitTerminal(rec, 20000));
      Assert.AreEqual(RecordingState.Completed, rec.CurrentState);
      var t = rec.ReadTelemetrySnapshot();
      Assert.AreEqual(2, t.GameFrames, "media kept across serial change");
      Assert.IsTrue(t.Interrupted, "change observable, never silent");
    } finally {
      KillService(rec);
      WipeDir(dir);
    }
  }
}
