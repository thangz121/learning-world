// _SharedKernel/FfmpegTranscodeBackend.cs — Lead owns. Phase 2.3b deliverable
// backend: session intermediates (game.avi + cam.avi + mic.wav) -> MP4
// (H.264 + MP3, gameplay with camera PiP) + MP3 (LAME VBR). Pure C#
// (NO UnityEngine): locator + argument builder + process runner. The ONLY
// place in the codebase that knows ffmpeg exists — gameplay code never
// scatters process calls (§5 backend rule).
//
// Why two stages (record raw-ish intermediates, transcode AFTER Stop):
// the game thread never touches ffmpeg, there are no realtime pipes to
// deadlock, and a missing ffmpeg degrades to verified WAV/AVI intermediates
// instead of a failed session (§10: detect, graceful fallback). The PiP
// composite itself runs inside ffmpeg (overlay filter), so the game pays
// zero blend cost during play.
//
// Encoder choice (user rule: quality first, size second):
// libx264 (open-source, GPL) preset medium + CRF 19: near-transparent
// stills AND clean motion handling; preset steps (ultrafast..medium) trade
// transcode time (post-session, never gameplay); CRF steps trade size.
// libmp3lame VBR -q:a 4: voice-transparent at roughly half of 128k CBR.
// Both ship in the same open-source binary (Gyan full build verified).
// Requires ffmpeg on the machine once (winget: Gyan.FFmpeg); after setup
// everything works offline on LAN.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

public struct TranscodeSpec {
  public string GameAvi;   // intermediate (null when MicOnly)
  public string CamAvi;    // intermediate (null when no camera frames)
  public string MicWav;    // intermediate (null when CameraOnly)
  public string OutMp4;    // null when audio-only session
  public string OutMp3;    // null when video-only session
  public double GameFpsActual; // measured frames/duration (fallback: config)
  public double CamFpsActual;
  public int GameWidth;
  public int GameHeight;
  public int PipWidth;     // 4:3 overlay width (height derived)
  public int PipMargin;
  public int Crf;          // 10..32
  public string Preset;    // allowlist (validated by config)
  public int Mp3Quality;   // 0..9
}

public struct TranscodeResult {
  public bool Ok;
  public bool SkippedNoFfmpeg;
  public int ExitCode;
  public string FfmpegPath;
  public string FfmpegVersion;
  public string Error;
  public string LogTail;
}

public static class FfmpegTranscodeBackend {
  const int VersionTimeoutMs = 10000;

  // --- locator (detect capability, never throws) -----------------------------
  // Order: explicit override -> <exeDir>/tools + <exeDir> (player builds)
  // -> repo tools (dev) -> app-local tools (dependency auto-install target)
  // -> winget-installed Gyan package -> PATH. Null = transcode unavailable
  // (caller falls back to verified intermediates, honestly flagged).
  public static string FindExecutable(string explicitPath, string exeDir, string repoToolsDir) {
    return FindExecutable(explicitPath, exeDir, repoToolsDir, null);
  }

  public static string FindExecutable(string explicitPath, string exeDir,
      string repoToolsDir, string appToolsDir) {
    try {
      if (!string.IsNullOrEmpty(explicitPath)) {
        try { if (File.Exists(explicitPath)) return explicitPath; }
        catch (Exception) { }
        return null; // explicit but missing: do NOT silently substitute
      }
      var cands = new List<string>();
      try {
        if (!string.IsNullOrEmpty(exeDir)) {
          cands.Add(Path.Combine(exeDir, "tools", "ffmpeg.exe"));
          cands.Add(Path.Combine(exeDir, "ffmpeg.exe"));
        }
        if (!string.IsNullOrEmpty(repoToolsDir)) {
          cands.Add(Path.Combine(repoToolsDir, "ffmpeg.exe"));
          cands.Add(Path.Combine(repoToolsDir, "ffmpeg"));
        }
        if (!string.IsNullOrEmpty(appToolsDir)) {
          cands.Add(Path.Combine(appToolsDir, "ffmpeg", "ffmpeg.exe"));
          cands.Add(Path.Combine(appToolsDir, "ffmpeg.exe"));
        }
      } catch (Exception) { }
      foreach (string c in cands) {
        try { if (File.Exists(c)) return c; } catch (Exception) { }
      }
      try {
        string wingetHit = FindWingetGyan();
        if (!string.IsNullOrEmpty(wingetHit)) return wingetHit;
      } catch (Exception) { }
      string onPath = WhereIs("ffmpeg.exe") ?? WhereIs("ffmpeg");
      if (!string.IsNullOrEmpty(onPath)) return onPath;
      return null;
    } catch (Exception) { return null; }
  }

  // Winget installs Gyan.FFmpeg under %LOCALAPPDATA%\Microsoft\WinGet\Packages
  // (user scope, no admin). New shells get PATH, but a running game keeps its
  // birth environment — so glob the well-known layout directly (narrow,
  // documented, no PATH dependency).
  public static string FindWingetGyan() {
    try {
      string local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
      if (string.IsNullOrEmpty(local)) return null;
      string pkgs = Path.Combine(local, "Microsoft", "WinGet", "Packages");
      if (!Directory.Exists(pkgs)) return null;
      string[] owners;
      try { owners = Directory.GetDirectories(pkgs, "Gyan.FFmpeg_*"); }
      catch (Exception) { return null; }
      foreach (string owner in owners) {
        string[] builds;
        try { builds = Directory.GetDirectories(owner, "ffmpeg-*"); }
        catch (Exception) { continue; }
        foreach (string build in builds) {
          string bin = Path.Combine(build, "bin", "ffmpeg.exe");
          try { if (File.Exists(bin)) return bin; } catch (Exception) { }
        }
      }
      return null;
    } catch (Exception) { return null; }
  }

  static string WhereIs(string name) {
    try {
      string probe = Environment.OSVersion.Platform == PlatformID.Win32NT ? "where" : "which";
      var psi = new ProcessStartInfo {
        FileName = probe,
        Arguments = name,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      };
      using (var p = Process.Start(psi)) {
        if (p == null) return null;
        string first = null;
        try {
          string all = p.StandardOutput.ReadToEnd();
          if (!string.IsNullOrEmpty(all)) {
            string[] lines = all.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0) first = lines[0].Trim();
          }
        } catch (Exception) { }
        try { if (!p.WaitForExit(5000)) { try { p.Kill(); } catch (Exception) { } } } catch (Exception) { }
        if (!string.IsNullOrEmpty(first)) {
          try { if (File.Exists(first)) return first; } catch (Exception) { }
        }
      }
    } catch (Exception) { }
    return null;
  }

  public static string QueryVersion(string ffmpeg) {
    try {
      var psi = new ProcessStartInfo {
        FileName = ffmpeg,
        Arguments = "-hide_banner -version",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      };
      using (var p = Process.Start(psi)) {
        if (p == null) return string.Empty;
        string all = string.Empty;
        try { all = p.StandardOutput.ReadToEnd(); } catch (Exception) { }
        try { if (!p.WaitForExit(VersionTimeoutMs)) { try { p.Kill(); } catch (Exception) { } } } catch (Exception) { }
        if (string.IsNullOrEmpty(all)) return string.Empty;
        int nl = all.IndexOf('\n');
        return (nl > 0 ? all.Substring(0, nl) : all).Trim();
      }
    } catch (Exception) { return string.Empty; }
  }

  // --- argument builder (pure, unit-tested; no process, no files) ------------
  static string Q(string path) {
    try { return "\"" + path.Replace("\"", "") + "\""; }
    catch (Exception) { return "\"\""; }
  }

  public static string BuildArguments(TranscodeSpec s) {
    try {
      bool haveGame = !string.IsNullOrEmpty(s.GameAvi);
      bool haveCam = !string.IsNullOrEmpty(s.CamAvi);
      bool haveAudio = !string.IsNullOrEmpty(s.MicWav);
      bool wantMp4 = !string.IsNullOrEmpty(s.OutMp4);
      bool wantMp3 = !string.IsNullOrEmpty(s.OutMp3);
      var b = new StringBuilder(1024);
      b.Append("-y -hide_banner -v error ");
      // Inputs: game is ALWAYS index 0 when present (filter assumes it).
      // NOTE: no -framerate prefixes — AVI carries its declared rate in the
      // header (written by our writers from the record config); -framerate
      // belongs to image demuxers and ffmpeg refuses it here. Measured
      // actuals (GameFpsActual/CamFpsActual) go to the sidecar for honesty.
      int gi = -1, ci = -1, ai = -1, n = 0;
      if (haveGame) {
        b.Append("-i ").Append(Q(s.GameAvi)).Append(" ");
        gi = n++;
      }
      if (haveCam) {
        b.Append("-i ").Append(Q(s.CamAvi)).Append(" ");
        ci = n++;
      }
      if (haveAudio) {
        b.Append("-i ").Append(Q(s.MicWav)).Append(" ");
        ai = n++;
      }
      string vlabel;
      if (haveGame && haveCam) {
        int pipH = Math.Max(90, s.PipWidth * 3 / 4);
        b.Append("-filter_complex \"[")
          .Append(ci).Append(":v]scale=").Append(s.PipWidth).Append(":").Append(pipH)
          .Append(":flags=bilinear,format=yuv420p[pip];[")
          .Append(gi).Append(":v][pip]overlay=W-w-").Append(s.PipMargin)
          .Append(":H-h-").Append(s.PipMargin)
          .Append(":format=yuv420:eof_action=pass[v]\" ");
        vlabel = "[v]";
      } else if (haveGame) {
        vlabel = gi + ":v";
      } else if (haveCam) {
        vlabel = ci + ":v";
      } else {
        vlabel = null;
      }
      string preset = MediaRecording.IsAllowedPreset(s.Preset) ? s.Preset : MediaRecording.DefaultVideoPreset;
      int crf = s.Crf >= 10 && s.Crf <= 32 ? s.Crf : MediaRecording.DefaultVideoCrf;
      int mq = s.Mp3Quality >= 0 && s.Mp3Quality <= 9 ? s.Mp3Quality : MediaRecording.DefaultMp3Quality;
      if (wantMp4 && vlabel != null) {
        b.Append("-map ").Append(vlabel).Append(" ");
        if (haveAudio) b.Append("-map ").Append(ai).Append(":a ");
        else b.Append("-an ");
        b.Append("-c:v libx264 -preset ").Append(preset)
          .Append(" -crf ").Append(crf).Append(" -pix_fmt yuv420p ");
        // Wall-clock honesty: the game header declares the RECORD rate, but
        // the worker may sustain slightly less (drops counted in telemetry).
        // Re-stamping the output to the MEASURED rate keeps mp4 duration ==
        // wall duration (P23 loopback finding: 14.2 s vs 18 s wall).
        double actual = s.GameFpsActual > 0 ? s.GameFpsActual
          : (s.CamFpsActual > 0 ? s.CamFpsActual : 0);
        if (haveGame && actual >= 1 && actual <= 60)
          b.Append("-r ").Append(actual.ToString("0.###",
            System.Globalization.CultureInfo.InvariantCulture)).Append(" ");
        if (haveAudio) b.Append("-c:a libmp3lame -q:a ").Append(mq).Append(" -ar 16000 -ac 1 ");
        b.Append("-shortest -movflags +faststart ").Append(Q(s.OutMp4)).Append(" ");
      }
      if (wantMp3 && haveAudio) {
        b.Append("-map ").Append(ai).Append(":a ")
          .Append("-c:a libmp3lame -q:a ").Append(mq).Append(" -ar 16000 -ac 1 ")
          .Append(Q(s.OutMp3));
      }
      return b.ToString().Trim();
    } catch (Exception) { return string.Empty; }
  }

  // --- runner (one bounded process, never throws) -----------------------------
  public static TranscodeResult Run(string ffmpeg, TranscodeSpec spec, int timeoutMs) {
    var r = new TranscodeResult();
    try {
      r.FfmpegPath = ffmpeg ?? string.Empty;
      r.FfmpegVersion = QueryVersion(ffmpeg);
      string args = BuildArguments(spec);
      if (string.IsNullOrEmpty(args) ||
          (string.IsNullOrEmpty(spec.OutMp4) && string.IsNullOrEmpty(spec.OutMp3))) {
        r.Error = "nothing-to-transcode";
        return r;
      }
      var psi = new ProcessStartInfo {
        FileName = ffmpeg,
        Arguments = args,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      };
      try {
        string wd = null;
        try {
          string anchor = !string.IsNullOrEmpty(spec.OutMp4) ? spec.OutMp4 : spec.OutMp3;
          wd = Path.GetDirectoryName(anchor);
        } catch (Exception) { }
        if (!string.IsNullOrEmpty(wd)) psi.WorkingDirectory = wd;
      } catch (Exception) { }
      using (var p = Process.Start(psi)) {
        if (p == null) {
          r.Error = "process-start-failed";
          return r;
        }
        string err = string.Empty;
        try { err = p.StandardError.ReadToEnd(); } catch (Exception) { }
        bool exited = false;
        try { exited = p.WaitForExit(Math.Max(10000, timeoutMs)); } catch (Exception) { }
        if (!exited) {
          try { p.Kill(); } catch (Exception) { }
          r.Error = "transcode-timeout";
          r.LogTail = Tail(err);
          return r;
        }
        r.ExitCode = p.ExitCode;
        r.LogTail = Tail(err);
        if (p.ExitCode != 0) {
          r.Error = "transcode-failed:exit" + p.ExitCode;
          return r;
        }
        // Exit 0 is NOT enough (§38 rule): outputs must exist + be non-zero.
        if (!string.IsNullOrEmpty(spec.OutMp4) && !IsNonEmptyFile(spec.OutMp4)) {
          r.Error = "mp4-missing-after-exit0";
          return r;
        }
        if (!string.IsNullOrEmpty(spec.OutMp3) && !IsNonEmptyFile(spec.OutMp3)) {
          r.Error = "mp3-missing-after-exit0";
          return r;
        }
        r.Ok = true;
        return r;
      }
    } catch (Exception e) {
      r.Error = "runner-exception:" + e.GetType().Name;
      return r;
    }
  }

  static bool IsNonEmptyFile(string path) {
    try {
      var fi = new FileInfo(path);
      return fi.Exists && fi.Length > 0;
    } catch (Exception) { return false; }
  }

  static string Tail(string s) {
    try {
      if (string.IsNullOrEmpty(s)) return string.Empty;
      s = s.Trim();
      return s.Length > 2000 ? s.Substring(s.Length - 2000) : s;
    } catch (Exception) { return string.Empty; }
  }
}
