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
using System.IO.Compression; // ZipFileExtensions.ExtractToFile (portable fallback)
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
  //
  // Runtime reality (probed 2026-09-16 on this Unity profile): CertificateRequest
  // does NOT exist (TypeLoad), ExportPkcs8/SPKI throw PlatformNotSupported,
  // and RSA.Create caps at 1024 (browsers/phones reject 1024-bit today).
  // What DOES work: RSACryptoServiceProvider(2048) + ExportParameters +
  // SHA256 + X509 parse. So this builder hand-rolls the DER (TBSCertificate
  // + PKCS#1/SPKI/PKCS#8, standard OIDs) and signs with the platform RSA.
  // Every byte it emits is re-validated below by an independent parse
  // (subject/expiry/key-size) plus the python-ssl load in compat.

  public static bool GenerateLanCert(string primaryIp, List<string> extraIps,
      string crtPath, string keyPath, out string error) {
    error = null;
    try {
      if (string.IsNullOrEmpty(primaryIp)) {
        error = "no-lan-ip";
        return false;
      }
      System.Security.Cryptography.RSA rsa = MintRsa2048();
      if (rsa == null) {
        error = "weak-key-unavailable (refusing 1024-bit cert)";
        return false;
      }
      using (rsa) {
        System.Security.Cryptography.RSAParameters p;
        try {
          p = rsa.ExportParameters(true);
        } catch (Exception e) {
          error = "export-parameters:" + e.GetType().Name;
          return false;
        }
        if (p.Modulus == null || p.Modulus.Length != 256 || p.Exponent == null) {
          error = "bad-key-material";
          return false;
        }
        DateTime notBefore, notAfter;
        try {
          notBefore = DateTime.UtcNow.AddDays(-1);
          notAfter = DateTime.UtcNow.AddDays(825);
        } catch (Exception e) {
          error = "validity:" + e.GetType().Name;
          return false;
        }
        byte[] serial;
        try {
          serial = new byte[8];
          using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            rng.GetBytes(serial);
          serial[0] &= 0x7F;
        } catch (Exception e) {
          error = "serial:" + e.GetType().Name;
          return false;
        }
        byte[] tbs;
        try {
          tbs = BuildTbs(primaryIp, extraIps, notBefore, notAfter, serial, p.Modulus, p.Exponent);
        } catch (Exception e) {
          error = "tbs:" + e.GetType().Name;
          return false;
        }
        byte[] sig;
        string signStep;
        if (!TrySignTbs(rsa, tbs, p.Modulus.Length, out sig, out signStep)) {
          error = "sign:" + signStep;
          return false;
        }
        byte[] certDer, keyDer;
        try {
          certDer = BuildCert(tbs, sig);
          keyDer = BuildPkcs8(p);
        } catch (Exception e) {
          error = "assemble:" + e.GetType().Name;
          return false;
        }
        // Independent self-verify (parse works on this runtime — probed):
        // never write a cert we cannot read back as 2048-bit with our dates.
        try {
          var chk = new X509Certificate2(certDer);
          if (chk.PublicKey == null || chk.PublicKey.Key == null
              || chk.PublicKey.Key.KeySize != 2048) {
            error = "self-verify:keysize";
            return false;
          }
          double days = (chk.NotAfter.ToUniversalTime() - DateTime.UtcNow).TotalDays;
          if (days < 800 || days > 830) {
            error = "self-verify:validity";
            return false;
          }
          if (chk.Subject == null || chk.Subject.IndexOf(primaryIp, StringComparison.Ordinal) < 0) {
            error = "self-verify:subject";
            return false;
          }
        } catch (Exception e) {
          error = "self-verify:" + e.GetType().Name;
          return false;
        }
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

  // RSA-2048 or nothing (1024-bit is refused, never silently shipped).
  static System.Security.Cryptography.RSA MintRsa2048() {
    try {
      var csp = new System.Security.Cryptography.RSACryptoServiceProvider(2048);
      try {
        if (csp.KeySize == 2048) return csp;
      } catch (Exception) { }
      try { csp.Dispose(); } catch (Exception) { }
    } catch (Exception) { }
    try {
      var r = System.Security.Cryptography.RSA.Create();
      try {
        r.KeySize = 2048;
        if (r.KeySize == 2048) return r;
      } catch (Exception) { }
      try { r.Dispose(); } catch (Exception) { }
    } catch (Exception) { }
    return null;
  }

  // Signer ladder: SignData (hashes internally) -> manual PKCS#1 v1.5 pad +
  // raw private op -> base SignHash. First success wins; failures name steps.
  static bool TrySignTbs(System.Security.Cryptography.RSA rsa, byte[] tbs,
      int modLen, out byte[] sig, out string step) {
    sig = null;
    step = "none";
    var csp = rsa as System.Security.Cryptography.RSACryptoServiceProvider;
    if (csp != null) {
      try {
        byte[] s = csp.SignData(tbs, "SHA256");
        sig = NormalizeSig(s, modLen);
        if (sig != null) return true;
        step = "signdata-length";
      } catch (Exception e) { step = "signdata:" + e.GetType().Name; }
      try {
        byte[] hash;
        using (var sha = System.Security.Cryptography.SHA256.Create())
          hash = sha.ComputeHash(tbs);
        byte[] pad = Pkcs1V15Encode(hash, modLen);
        if (pad != null) {
          byte[] s = csp.EncryptValue(pad);
          sig = NormalizeSig(s, modLen);
          if (sig != null) return true;
          step = "manual-length";
        } else step = "manual-pad";
      } catch (Exception e) { step = "manual:" + e.GetType().Name; }
    }
    try {
      byte[] hash;
      using (var sha = System.Security.Cryptography.SHA256.Create())
        hash = sha.ComputeHash(tbs);
      byte[] s = rsa.SignHash(hash,
        System.Security.Cryptography.HashAlgorithmName.SHA256,
        System.Security.Cryptography.RSASignaturePadding.Pkcs1);
      sig = NormalizeSig(s, modLen);
      if (sig != null) return true;
      step = "signhash-length";
    } catch (Exception e) { step = "signhash:" + e.GetType().Name; }
    sig = null;
    return false;
  }

  static byte[] NormalizeSig(byte[] s, int modLen) {
    try {
      if (s == null || s.Length > modLen) return null;
      if (s.Length == modLen) return s;
      var out_ = new byte[modLen];
      Buffer.BlockCopy(s, 0, out_, modLen - s.Length, s.Length);
      return out_;
    } catch (Exception) { return null; }
  }

  // PKCS#1 v1.5 signature padding for SHA-256 (RFC 8017, DigestInfo prefix).
  static byte[] Pkcs1V15Encode(byte[] hash32, int modLen) {
    try {
      if (hash32 == null || hash32.Length != 32 || modLen < 64) return null;
      byte[] prefix = {
        0x30, 0x31, 0x30, 0x0D, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01,
        0x65, 0x03, 0x04, 0x02, 0x01, 0x05, 0x00, 0x04, 0x20
      };
      int tLen = prefix.Length + 32;
      if (modLen < tLen + 11) return null;
      var em = new byte[modLen];
      em[0] = 0x00;
      em[1] = 0x01;
      for (int i = 2; i < modLen - tLen - 1; i++) em[i] = 0xFF;
      em[modLen - tLen - 1] = 0x00;
      Buffer.BlockCopy(prefix, 0, em, modLen - tLen, prefix.Length);
      Buffer.BlockCopy(hash32, 0, em, modLen - 32, 32);
      return em;
    } catch (Exception) { return null; }
  }

  // ---------------- minimal DER builder (standard OIDs only) ------------------

  static byte[] D_Len(int n) {
    if (n < 0) return new byte[] { 0x00 };
    if (n < 128) return new byte[] { (byte)n };
    var tmp = new System.Collections.Generic.List<byte>();
    int v = n;
    while (v > 0) { tmp.Insert(0, (byte)(v & 0xFF)); v >>= 8; }
    var out_ = new byte[tmp.Count + 1];
    out_[0] = (byte)(0x80 | tmp.Count);
    for (int i = 0; i < tmp.Count; i++) out_[i + 1] = tmp[i];
    return out_;
  }

  static byte[] D_Tlv(byte tag, byte[] content) {
    byte[] len = D_Len(content != null ? content.Length : 0);
    var out_ = new byte[1 + len.Length + (content != null ? content.Length : 0)];
    out_[0] = tag;
    Buffer.BlockCopy(len, 0, out_, 1, len.Length);
    if (content != null && content.Length > 0)
      Buffer.BlockCopy(content, 0, out_, 1 + len.Length, content.Length);
    return out_;
  }

  static byte[] D_Concat(params byte[][] parts) {
    int n = 0;
    foreach (byte[] p in parts) n += p != null ? p.Length : 0;
    var out_ = new byte[n];
    int o = 0;
    foreach (byte[] p in parts) {
      if (p == null || p.Length == 0) continue;
      Buffer.BlockCopy(p, 0, out_, o, p.Length);
      o += p.Length;
    }
    return out_;
  }

  static byte[] D_Seq(params byte[][] parts) {
    return D_Tlv(0x30, D_Concat(parts));
  }

  static byte[] D_Set(byte[] part) {
    return D_Tlv(0x31, part);
  }

  static byte[] D_Int(byte[] be) {
    int i = 0;
    while (i + 1 < be.Length && be[i] == 0x00) i++;
    int n = be.Length - i;
    bool hi = (be[i] & 0x80) != 0;
    var out_ = new byte[n + (hi ? 1 : 0)];
    if (hi) out_[0] = 0x00;
    Buffer.BlockCopy(be, i, out_, hi ? 1 : 0, n);
    return D_Tlv(0x02, out_);
  }

  static byte[] D_IntSmall(int v) {
    if (v == 0) return D_Tlv(0x02, new byte[] { 0x00 });
    var tmp = new System.Collections.Generic.List<byte>();
    int x = v;
    while (x > 0) { tmp.Insert(0, (byte)(x & 0xFF)); x >>= 8; }
    return D_Int(tmp.ToArray());
  }

  static byte[] D_Oid(string dotted) {
    string[] parts = dotted.Split('.');
    var body = new System.Collections.Generic.List<byte>();
    int a = int.Parse(parts[0]), b = int.Parse(parts[1]);
    body.Add((byte)(a * 40 + b));
    for (int i = 2; i < parts.Length; i++) {
      long v = long.Parse(parts[i]);
      var st = new System.Collections.Generic.List<byte>();
      st.Insert(0, (byte)(v & 0x7F));
      v >>= 7;
      while (v > 0) { st.Insert(0, (byte)((v & 0x7F) | 0x80)); v >>= 7; }
      foreach (byte bb in st) body.Add(bb);
    }
    return D_Tlv(0x06, body.ToArray());
  }

  static byte[] D_Null() {
    return new byte[] { 0x05, 0x00 };
  }

  static byte[] D_Utf8(string s) {
    return D_Tlv(0x0C, Encoding.UTF8.GetBytes(s));
  }

  static byte[] D_UtcTime(DateTime utc) {
    string t = utc.ToUniversalTime().ToString("yyMMddHHmmss") + "Z";
    return D_Tlv(0x17, Encoding.ASCII.GetBytes(t));
  }

  static byte[] D_BitStr(byte[] content) {
    return D_Tlv(0x03, D_Concat(new byte[] { 0x00 }, content));
  }

  static byte[] D_Octet(byte[] content) {
    return D_Tlv(0x04, content);
  }

  static byte[] D_Explicit(int tag, byte[] content) {
    return D_Tlv((byte)(0xA0 | tag), content);
  }

  static byte[] SigAlg() {
    return D_Seq(D_Oid("1.2.840.113549.1.1.11"), D_Null()); // sha256WithRSAEncryption
  }

  static byte[] Pkcs1Pub(byte[] n, byte[] e) {
    return D_Seq(D_Int(n), D_Int(e));
  }

  static byte[] Spki(byte[] n, byte[] e) {
    return D_Seq(
      D_Seq(D_Oid("1.2.840.113549.1.1.1"), D_Null()), // rsaEncryption
      D_BitStr(Pkcs1Pub(n, e)));
  }

  static byte[] NameOf(string cn) {
    return D_Seq(D_Set(D_Seq(D_Oid("2.5.4.3"), D_Utf8(cn))));
  }

  static byte[] SanOf(List<string> ips) {
    var parts = new System.Collections.Generic.List<byte[]>();
    foreach (string ip in ips) {
      IPAddress addr;
      if (IPAddress.TryParse(ip, out addr)) {
        byte[] raw = addr.GetAddressBytes();
        if (raw != null && raw.Length == 4)
          parts.Add(D_Concat(new byte[] { 0x87, 0x04 }, raw));
      } else if (!string.IsNullOrEmpty(ip)) {
        byte[] dns = Encoding.ASCII.GetBytes(ip);
        parts.Add(D_Concat(new byte[] { 0x82 }, D_Len(dns.Length), dns));
      }
    }
    return D_Seq(parts.ToArray());
  }

  static byte[] BuildTbs(string primaryIp, List<string> extraIps,
      DateTime notBefore, DateTime notAfter, byte[] serial,
      byte[] modulus, byte[] exponent) {
    var ips = new List<string> { primaryIp };
    if (extraIps != null) {
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { primaryIp };
      foreach (string ip in extraIps) {
        if (!string.IsNullOrEmpty(ip) && seen.Add(ip)) ips.Add(ip);
      }
    }
    byte[] exts = D_Seq(
      D_Seq(D_Oid("2.5.29.19"), D_Octet(D_Seq())),                       // basicConstraints CA:false
      D_Seq(D_Oid("2.5.29.15"), D_Octet(D_Tlv(0x03, new byte[] { 0x05, 0xA0 }))), // keyUsage digSig+keyEnc
      D_Seq(D_Oid("2.5.29.17"), D_Octet(SanOf(ips))));                    // subjectAltName
    return D_Seq(
      D_Explicit(0, D_Tlv(0x02, new byte[] { 0x02 })),                    // version v3
      D_Int(serial),
      SigAlg(),
      NameOf(primaryIp),
      D_Seq(D_UtcTime(notBefore), D_UtcTime(notAfter)),
      NameOf(primaryIp),
      Spki(modulus, exponent),
      D_Explicit(3, exts));
  }

  static byte[] BuildCert(byte[] tbs, byte[] sig) {
    return D_Seq(tbs, SigAlg(), D_BitStr(sig));
  }

  static byte[] BuildPkcs8(System.Security.Cryptography.RSAParameters p) {
    byte[] pkcs1 = D_Seq(
      D_IntSmall(0), D_Int(p.Modulus), D_Int(p.Exponent), D_Int(p.D),
      D_Int(p.P), D_Int(p.Q), D_Int(p.DP), D_Int(p.DQ), D_Int(p.InverseQ));
    return D_Seq(
      D_IntSmall(0),
      D_Seq(D_Oid("1.2.840.113549.1.1.1"), D_Null()),
      D_Octet(pkcs1));
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
