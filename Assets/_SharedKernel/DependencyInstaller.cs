// _SharedKernel/DependencyInstaller.cs — Lead owns. Phase 2.3c startup
// dependency worker: CHECK what the game needs, INSTALL what's missing on
// explicit user consent. Pure C# (NO UnityEngine): every method is callable
// from a background thread, reports via callbacks/return values, and never
// throws out. The UI (dialog/service) only marshals these results.
//
// Consent boundary: NOTHING here touches the network unless the caller
// already holds user confirmation (the startup prompt). Downloads are
// HTTPS-only, SHA-256-verified, and installs are user-scope/app-local
// (winget silent, or a portable zip extracted beside the game) — no admin
// escalation is ever attempted.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

public struct DependencyCheck {
  public DependencyKind Kind;
  public bool Ready;
  public string Detail;
}

public struct InstallProgress {
  public DependencyKind Kind;
  public string Phase;   // winget | download | verify | extract | generate | done | failed
  public int Percent;    // 0..100, -1 = indeterminate
  public string Detail;
}

public static class DependencyInstaller {
  // ---------------- checks (fast, offline, never throws) ---------------------

  public static DependencyCheck CheckFfmpeg(string exeDir, string repoToolsDir, string appToolsDir) {
    var c = new DependencyCheck { Kind = DependencyKind.Ffmpeg };
    try {
      string found = FfmpegTranscodeBackend.FindExecutable(null, exeDir, repoToolsDir, appToolsDir);
      if (string.IsNullOrEmpty(found)) {
        c.Ready = false;
        c.Detail = "not-found";
        return c;
      }
      string ver = FfmpegTranscodeBackend.QueryVersion(found);
      c.Ready = true;
      c.Detail = string.IsNullOrEmpty(ver) ? found : ver.Split('\n')[0].Trim();
      return c;
    } catch (Exception) {
      c.Ready = false;
      c.Detail = "check-failed";
      return c;
    }
  }

  public static DependencyCheck CheckPython() {
    var c = new DependencyCheck { Kind = DependencyKind.Python };
    try {
      string[][] probes = {
        new[] { "python", "--version" },
        new[] { "python3", "--version" },
        new[] { "py", "-3", "--version" },
      };
      foreach (string[] pr in probes) {
        string ver = ProbePython(pr);
        if (!string.IsNullOrEmpty(ver)) {
          c.Ready = true;
          c.Detail = ver;
          return c;
        }
      }
      c.Ready = false;
      c.Detail = "not-found";
      return c;
    } catch (Exception) {
      c.Ready = false;
      c.Detail = "check-failed";
      return c;
    }
  }

  static string ProbePython(string[] cmd) {
    try {
      var psi = new ProcessStartInfo {
        FileName = cmd[0],
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      };
      if (cmd.Length > 1) psi.Arguments = string.Join(" ", cmd, 1, cmd.Length - 1);
      using (var p = Process.Start(psi)) {
        if (p == null) return null;
        string all = string.Empty;
        try {
          all = (p.StandardOutput.ReadToEnd() + "\n" + p.StandardError.ReadToEnd()).Trim();
        } catch (Exception) { }
        try { if (!p.WaitForExit(MediaDependencySpecs.ProbeTimeoutMs)) { try { p.Kill(); } catch (Exception) { } } }
        catch (Exception) { }
        if (p.ExitCode != 0) return null;
        foreach (string line in all.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
          string t = line.Trim();
          if (t.StartsWith("Python 3.", StringComparison.OrdinalIgnoreCase)) return t;
        }
        return null;
      }
    } catch (Exception) { return null; }
  }

  public static DependencyCheck CheckLanCert(string toolsDir) {
    var c = new DependencyCheck { Kind = DependencyKind.LanCert };
    try {
      if (string.IsNullOrEmpty(toolsDir)) {
        c.Ready = false;
        c.Detail = "no-tools-dir";
        return c;
      }
      string crt = Path.Combine(toolsDir, "lan.crt");
      string key = Path.Combine(toolsDir, "lan.key");
      if (!File.Exists(crt) || !File.Exists(key)) {
        c.Ready = false;
        c.Detail = "missing";
        return c;
      }
      try {
        string pem = File.ReadAllText(crt);
        var cert = new X509Certificate2(Encoding.ASCII.GetBytes(pem));
        if (DateTime.UtcNow > cert.NotAfter.ToUniversalTime().AddDays(-30)) {
          c.Ready = false;
          c.Detail = "expiring:" + cert.NotAfter.ToString("yyyy-MM-dd");
          return c;
        }
        c.Ready = true;
        c.Detail = "until " + cert.NotAfter.ToString("yyyy-MM-dd");
        return c;
      } catch (Exception) {
        c.Ready = false;
        c.Detail = "unreadable";
        return c;
      }
    } catch (Exception) {
      c.Ready = false;
      c.Detail = "check-failed";
      return c;
    }
  }

  public static string DetectLanIp() {
    try {
      var host = Dns.GetHostEntry(Dns.GetHostName());
      string fallback = null;
      foreach (IPAddress a in host.AddressList) {
        try {
          if (a.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(a)) continue;
          string s = a.ToString();
          if (s.StartsWith("192.168.", StringComparison.Ordinal) || s.StartsWith("10.", StringComparison.Ordinal))
            return s;
          if (fallback == null) fallback = s;
        } catch (Exception) { }
      }
      return fallback;
    } catch (Exception) { return null; }
  }

  public static List<string> LocalIPv4s() {
    var out_ = new List<string>();
    try {
      var host = Dns.GetHostEntry(Dns.GetHostName());
      foreach (IPAddress a in host.AddressList) {
        try {
          if (a.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(a)) continue;
          string s = a.ToString();
          if (!out_.Contains(s)) out_.Add(s);
        } catch (Exception) { }
      }
    } catch (Exception) { }
    return out_;
  }

  // ---------------- winget (silent, trusted source) --------------------------

  public static bool InstallViaWinget(string packageId, DependencyKind kind,
      Action<InstallProgress> progress, Func<bool> cancel, int timeoutMs, out string logTail) {
    logTail = string.Empty;
    try {
      Report(progress, kind, "winget", -1, packageId);
      string args = MediaDependencySpecs.WingetCommand(packageId);
      if (string.IsNullOrEmpty(args)) return false;
      var psi = new ProcessStartInfo {
        FileName = "winget",
        Arguments = args,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      };
      using (var p = Process.Start(psi)) {
        if (p == null) {
          Report(progress, kind, "failed", -1, "winget not launchable");
          return false;
        }
        var sw = Stopwatch.StartNew();
        string err = string.Empty;
        try {
          // Drain stderr on a threadpool read so a chatty installer can
          // never wedge the wait below.
          var readErr = p.StandardError.ReadToEndAsync();
          while (!p.WaitForExit(2000)) {
            if (cancel != null) {
              bool stop = false;
              try { stop = cancel(); } catch (Exception) { }
              if (stop) {
                try { p.Kill(); } catch (Exception) { }
                Report(progress, kind, "failed", -1, "cancelled");
                return false;
              }
            }
            if (sw.ElapsedMilliseconds > Math.Max(60000, timeoutMs)) {
              try { p.Kill(); } catch (Exception) { }
              Report(progress, kind, "failed", -1, "timeout");
              return false;
            }
            Report(progress, kind, "winget", -1,
              "installing " + packageId + " (" + (sw.ElapsedMilliseconds / 1000) + "s)");
          }
          try { err = readErr.Result ?? string.Empty; } catch (Exception) { }
        } catch (Exception) { }
        logTail = Tail(p.StandardOutput.ReadToEnd() + "\n" + err);
        bool ok = p.ExitCode == 0;
        Report(progress, kind, ok ? "done" : "failed", ok ? 100 : -1,
          ok ? packageId : ("exit " + p.ExitCode));
        return ok;
      }
    } catch (Exception e) {
      logTail = e.GetType().Name;
      Report(progress, kind, "failed", -1, logTail);
      return false;
    }
  }

  // ---------------- download + verify + extract ------------------------------

  public static bool DownloadFile(string url, string destPath,
      Action<long, long> bytes, Func<bool> cancel, out string error) {
    error = null;
    try {
      if (!MediaDependencySpecs.IsHttps(url)) {
        error = "refused-non-https";
        return false;
      }
      string dir = null;
      try { dir = Path.GetDirectoryName(destPath); } catch (Exception) { }
      if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
      string tmp = destPath + ".part";
      try {
        using (var http = new HttpClient()) {
          http.Timeout = Timeout.InfiniteTimeSpan;
          using (var resp = http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
              .GetAwaiter().GetResult()) {
            if (!resp.IsSuccessStatusCode) {
              error = "http-" + ((int)resp.StatusCode);
              return false;
            }
            long total = -1;
            try { total = resp.Content.Headers.ContentLength ?? -1; } catch (Exception) { }
            using (var src = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
            using (var dst = File.Open(tmp, FileMode.Create, FileAccess.Write, FileShare.None)) {
              var buf = new byte[128 * 1024];
              long got = 0;
              var watch = Stopwatch.StartNew();
              const long absoluteCapMs = 30L * 60 * 1000; // 30' for ~110 MB worst case
              for (;;) {
                if (cancel != null) {
                  bool stop = false;
                  try { stop = cancel(); } catch (Exception) { }
                  if (stop) {
                    error = "cancelled";
                    try { dst.Dispose(); } catch (Exception) { }
                    try { File.Delete(tmp); } catch (Exception) { }
                    return false;
                  }
                }
                if (watch.ElapsedMilliseconds > absoluteCapMs) {
                  error = "download-too-slow";
                  try { dst.Dispose(); } catch (Exception) { }
                  try { File.Delete(tmp); } catch (Exception) { }
                  return false;
                }
                // Per-read budget = the stall guard: a hung connection yields
                // no bytes within DownloadStallMs and aborts here. Slow but
                // moving links survive (every delivered chunk resets it).
                int n;
                try {
                  var readTask = src.ReadAsync(buf, 0, buf.Length);
                  if (!readTask.Wait(MediaDependencySpecs.DownloadStallMs)) {
                    error = "download-stall";
                    try { dst.Dispose(); } catch (Exception) { }
                    try { File.Delete(tmp); } catch (Exception) { }
                    return false;
                  }
                  n = readTask.Result;
                } catch (Exception e) {
                  error = "download:" + e.GetType().Name;
                  try { dst.Dispose(); } catch (Exception) { }
                  try { File.Delete(tmp); } catch (Exception) { }
                  return false;
                }
                if (n <= 0) break;
                dst.Write(buf, 0, n);
                got += n;
                try { if (bytes != null) bytes(got, total); } catch (Exception) { }
              }
            }
          }
        }
      } catch (Exception e) {
        error = "download:" + e.GetType().Name;
        try { File.Delete(tmp); } catch (Exception) { }
        return false;
      }
      try {
        if (File.Exists(destPath)) File.Delete(destPath);
        File.Move(tmp, destPath);
      } catch (Exception e) {
        error = "save:" + e.GetType().Name;
        return false;
      }
      return true;
    } catch (Exception e) {
      error = "error:" + e.GetType().Name;
      return false;
    }
  }

  public static bool DownloadText(string url, out string text) {
    text = null;
    try {
      if (!MediaDependencySpecs.IsHttps(url)) return false;
      using (var http = new HttpClient()) {
        http.Timeout = TimeSpan.FromSeconds(30);
        text = http.GetStringAsync(url).GetAwaiter().GetResult();
        return !string.IsNullOrEmpty(text);
      }
    } catch (Exception) { return false; }
  }

  // Zip members matched by FILE NAME (gyan zips nest under bin/ + docs).
  public static bool ExtractNames(string zipPath, string destDir, string[] names, out string error) {
    error = null;
    try {
      if (names == null || names.Length == 0) {
        error = "no-names";
        return false;
      }
      Directory.CreateDirectory(destDir);
      int kept = 0;
      using (var zip = System.IO.Compression.ZipFile.OpenRead(zipPath)) {
        foreach (var entry in zip.Entries) {
          string fn = null;
          try { fn = Path.GetFileName(entry.FullName); } catch (Exception) { continue; }
          if (string.IsNullOrEmpty(fn)) continue;
          bool want = false;
          foreach (string w in names) {
            if (string.Equals(fn, w, StringComparison.OrdinalIgnoreCase)) { want = true; break; }
          }
          if (!want) continue;
          string dst = Path.Combine(destDir, fn);
          try {
            if (File.Exists(dst)) File.Delete(dst);
            entry.ExtractToFile(dst);
            kept++;
          } catch (Exception e) {
            error = "extract:" + e.GetType().Name;
            return false;
          }
        }
      }
      if (kept == 0) {
        error = "nothing-matched";
        return false;
      }
      return true;
    } catch (Exception e) {
      error = "zip:" + e.GetType().Name;
      return false;
    }
  }

  // ---------------- LAN certificate (generated locally, no download) ----------

  public static bool GenerateLanCert(string primaryIp, List<string> extraIps,
      string crtPath, string keyPath, out string error) {
    error = null;
    try {
      if (string.IsNullOrEmpty(primaryIp)) {
        error = "no-lan-ip";
        return false;
      }
      using (var rsa = System.Security.Cryptography.RSA.Create(2048)) {
        var req = new CertificateRequest(
          "CN=" + primaryIp,
          rsa,
          System.Security.Cryptography.HashAlgorithmName.SHA256,
          System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(
          new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(
          new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        var san = new SubjectAlternativeNameBuilder();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var all = new List<string> { primaryIp };
        if (extraIps != null) all.AddRange(extraIps);
        foreach (string ip in all) {
          if (string.IsNullOrEmpty(ip) || !seen.Add(ip)) continue;
          IPAddress addr;
          if (IPAddress.TryParse(ip, out addr)) san.AddIpAddress(addr);
          else san.AddDnsName(ip);
        }
        req.CertificateExtensions.Add(san.Build());
        var cert = req.CreateSelfSigned(
          DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(825));
        byte[] certDer = cert.Export(X509ContentType.Cert);
        byte[] keyDer = rsa.ExportPkcs8PrivateKey();
        string dir = null;
        try { dir = Path.GetDirectoryName(crtPath); } catch (Exception) { }
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(crtPath, ToPem("CERTIFICATE", certDer));
        File.WriteAllText(keyPath, ToPem("PRIVATE KEY", keyDer));
        return true;
      }
    } catch (Exception e) {
      error = "generate:" + e.GetType().Name;
      return false;
    }
  }

  public static string ToPem(string header, byte[] der) {
    try {
      var sb = new StringBuilder();
      sb.Append("-----BEGIN ").Append(header).Append("-----\n");
      string b64 = Convert.ToBase64String(der);
      for (int i = 0; i < b64.Length; i += 64)
        sb.Append(b64.Substring(i, Math.Min(64, b64.Length - i))).Append("\n");
      sb.Append("-----END ").Append(header).Append("-----\n");
      return sb.ToString();
    } catch (Exception) { return string.Empty; }
  }

  // ---------------- small plumbing -------------------------------------------

  static void Report(Action<InstallProgress> progress, DependencyKind kind,
      string phase, int percent, string detail) {
    try {
      if (progress != null)
        progress(new InstallProgress { Kind = kind, Phase = phase, Percent = percent, Detail = detail });
    } catch (Exception) { }
  }

  static string Tail(string s) {
    try {
      if (string.IsNullOrEmpty(s)) return string.Empty;
      s = s.Trim();
      return s.Length > 2000 ? s.Substring(s.Length - 2000) : s;
    } catch (Exception) { return string.Empty; }
  }
}
