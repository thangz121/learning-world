// CT-P22: Phase 2.3c startup dependency setup — deterministic suite.
// Specs, winget command, SHA helpers, locator, LAN-cert round trip (real
// crypto in EditMode), zip extraction, dialog states. No network, no
// installs, no hardware: downloads/installs are proven by the user E2E
// runbook (handoff), guarded here by construction (https-only, consent
// owned by the dialog, user-scope/app-local only).
using NUnit.Framework;
using System;
using System.IO;
using System.Net;

public class CT_P22_DependencySetup {
  static string TempDir(string tag) {
    string d = Path.Combine(Path.GetTempPath(), tag + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(d);
    return d;
  }

  static void WipeDir(string d) {
    try { if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) Directory.Delete(d, true); }
    catch (Exception) { }
  }

  // ---------- specs (what/why/where, single source) ----------

  [Test] public void P22A_SpecsValid() {
    var f = DependencySpec.Ffmpeg();
    Assert.IsNotEmpty(f.TitleVi);
    Assert.IsNotEmpty(f.WhyVi);
    Assert.AreEqual(DependencyAction.WingetThenPortable, f.Action);
    Assert.AreEqual("Gyan.FFmpeg", f.WingetId);
    Assert.IsTrue(MediaDependencySpecs.IsHttps(f.DownloadUrl));
    Assert.IsTrue(MediaDependencySpecs.IsHttps(f.ShaUrl));
    Assert.IsTrue(MediaDependencySpecs.IsHttps(f.VerUrl));
    Assert.IsNotNull(f.ExtractNames);
    var p = DependencySpec.Python();
    Assert.IsNotEmpty(p.TitleVi);
    Assert.AreEqual(DependencyAction.WingetOnly, p.Action);
    Assert.AreEqual("Python.Python.3", p.WingetId);
    var c = DependencySpec.LanCert();
    Assert.IsNotEmpty(c.TitleVi);
    Assert.AreEqual(DependencyAction.GenerateLocal, c.Action);
    Assert.IsNull(c.WingetId);
    Assert.IsNull(c.DownloadUrl);
  }

  [Test] public void P22B_WingetCommandSilentAndConsented() {
    string cmd = MediaDependencySpecs.WingetCommand("Gyan.FFmpeg");
    StringAssert.Contains("Gyan.FFmpeg", cmd);
    StringAssert.Contains("--silent", cmd);
    StringAssert.Contains("--accept-package-agreements", cmd);
    StringAssert.Contains("--accept-source-agreements", cmd);
    StringAssert.Contains("-e", cmd);
    Assert.IsEmpty(MediaDependencySpecs.WingetCommand(null));
    Assert.IsEmpty(MediaDependencySpecs.WingetCommand(string.Empty));
    Assert.IsTrue(MediaDependencySpecs.IsHttps("https://example.com/x.zip"));
    Assert.IsFalse(MediaDependencySpecs.IsHttps("http://example.com/x.zip"));
    Assert.IsFalse(MediaDependencySpecs.IsHttps("ftp://example.com/x.zip"));
    Assert.IsFalse(MediaDependencySpecs.IsHttps(null));
  }

  // ---------- SHA helpers (portable archives verified, never trusted) ----------

  [Test] public void P22C_ShaParseRealSample() {
    // Gyan publishes a bare lowercase hex line (fetched 2026-09-16).
    string hash;
    Assert.IsTrue(MediaDependencySpecs.TryParseSha256(
      "fec81ae03971d9dd4be3ebe02e263bd2ec1d789483f931bdba5f5715e65da2e9\n", out hash));
    Assert.AreEqual("fec81ae03971d9dd4be3ebe02e263bd2ec1d789483f931bdba5f5715e65da2e9", hash);
    Assert.IsTrue(MediaDependencySpecs.TryParseSha256(
      "FEC81AE03971D9DD4BE3EBE02E263BD2EC1D789483F931BDBA5F5715E65DA2E9 *ffmpeg.zip", out hash));
    Assert.IsFalse(MediaDependencySpecs.TryParseSha256("garbage", out hash));
    Assert.IsFalse(MediaDependencySpecs.TryParseSha256("abc123", out hash));
    Assert.IsFalse(MediaDependencySpecs.TryParseSha256(null, out hash));
    Assert.IsFalse(MediaDependencySpecs.TryParseSha256(string.Empty, out hash));
  }

  [Test] public void P22D_Sha256KnownVector() {
    string dir = TempDir("LWE-P22-");
    try {
      string f = Path.Combine(dir, "v.txt");
      File.WriteAllBytes(f, new byte[] { (byte)'a', (byte)'b', (byte)'c' });
      Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
        MediaDependencySpecs.Sha256OfFile(f));
      Assert.IsNull(MediaDependencySpecs.Sha256OfFile(Path.Combine(dir, "nope.bin")));
    } finally { WipeDir(dir); }
  }

  // ---------- locator (explicit paths; PATH untouched by tests) ----------

  [Test] public void P22E_LocatorExplicit() {
    string dir = TempDir("LWE-P22-");
    try {
      string exe = Path.Combine(dir, "ffmpeg.exe");
      File.WriteAllBytes(exe, new byte[] { 1, 2, 3 });
      Assert.AreEqual(exe,
        FfmpegTranscodeBackend.FindExecutable(exe, null, null, null));
      Assert.IsNull(FfmpegTranscodeBackend.FindExecutable(
        Path.Combine(dir, "missing.exe"), null, null, null),
        "explicit-but-missing substitutes nothing");
    } finally { WipeDir(dir); }
  }

  // ---------- LAN cert round trip (real crypto, local files) ----------

  [Test] public void P22F_CertGenerateAndCheck() {
    string dir = TempDir("LWE-P22-");
    try {
      string crt = Path.Combine(dir, "lan.crt");
      string key = Path.Combine(dir, "lan.key");
      string err;
      Assert.IsTrue(DependencyInstaller.GenerateLanCert(
        "127.0.0.1", new System.Collections.Generic.List<string> { "192.168.1.50" },
        crt, key, out err), err);
      Assert.IsTrue(File.Exists(crt) && File.Exists(key));
      string pem = File.ReadAllText(key);
      Assert.IsTrue(pem.Contains("-----BEGIN PRIVATE KEY-----"), "unencrypted PKCS#8 for gateway ssl");
      Assert.IsTrue(File.ReadAllText(crt).Contains("-----BEGIN CERTIFICATE-----"));
      // Independent re-parse teeth: real 2048-bit key, our CN, our validity.
      var parsed = new System.Security.Cryptography.X509Certificates.X509Certificate2(
        File.ReadAllBytes(crt));
      Assert.AreEqual(2048, parsed.PublicKey.Key.KeySize, "never ship 1024-bit certs");
      Assert.IsTrue(parsed.Subject.Contains("127.0.0.1"), parsed.Subject);
      double days = (parsed.NotAfter.ToUniversalTime() - DateTime.UtcNow).TotalDays;
      Assert.Greater(days, 800, "825-day validity");
      Assert.Less(days, 830, "825-day validity");
      // SAN IPs land in the DER (87 04 <4 bytes>): 127.0.0.1 + 192.168.1.50.
      // NOTE: scan the base64-DECODED body — the .crt file itself is PEM text.
      byte[] der = PemBody(File.ReadAllText(crt));
      Assert.IsNotNull(der, "pem decodes");
      Assert.IsTrue(ContainsSeq(der, new byte[] { 0x87, 0x04, 0x7F, 0x00, 0x00, 0x01 }), "SAN 127.0.0.1");
      Assert.IsTrue(ContainsSeq(der, new byte[] { 0x87, 0x04, 192, 168, 1, 50 }), "SAN 192.168.1.50");
      DependencyCheck c = DependencyInstaller.CheckLanCert(dir);
      Assert.IsTrue(c.Ready, c.Detail);
      // Missing dir / missing files never throw, never claim ready.
      DependencyCheck m = DependencyInstaller.CheckLanCert(Path.Combine(dir, "nope"));
      Assert.IsFalse(m.Ready);
      DependencyCheck n = DependencyInstaller.CheckLanCert(null);
      Assert.IsFalse(n.Ready);
      string err2;
      Assert.IsFalse(DependencyInstaller.GenerateLanCert(null, null, crt, key, out err2));
      Assert.IsNotEmpty(err2);
    } finally { WipeDir(dir); }
  }

  static byte[] PemBody(string pem) {
    try {
      var sb = new System.Text.StringBuilder();
      foreach (string line in pem.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
        string t = line.Trim();
        if (t.StartsWith("-----")) continue;
        sb.Append(t);
      }
      return Convert.FromBase64String(sb.ToString());
    } catch (Exception) { return null; }
  }

  static bool ContainsSeq(byte[] hay, byte[] needle) {    try {
      for (int i = 0; i + needle.Length <= hay.Length; i++) {
        bool ok = true;
        for (int k = 0; k < needle.Length; k++) {
          if (hay[i + k] != needle[k]) { ok = false; break; }
        }
        if (ok) return true;
      }
    } catch (Exception) { }
    return false;
  }

  [Test] public void P22G_LanIpNeverThrows() {
    string ip = null;
    Assert.DoesNotThrow(() => { ip = DependencyInstaller.DetectLanIp(); });
    if (!string.IsNullOrEmpty(ip)) {
      IPAddress addr;
      Assert.IsTrue(IPAddress.TryParse(ip, out addr), "valid IPv4 or null, never garbage");
    }
  }

  // ---------- zip extraction (portable fallback needs only these) ----------

  [Test] public void P22H_ZipExtractByFileName() {
    string dir = TempDir("LWE-P22-");
    try {
      string zip = Path.Combine(dir, "pkg.zip");
      using (var a = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create)) {
        WriteEntry(a, "ffmpeg-9.0.1-essentials_build/bin/ffmpeg.exe", new byte[] { 1 });
        WriteEntry(a, "ffmpeg-9.0.1-essentials_build/bin/ffprobe.exe", new byte[] { 2 });
        WriteEntry(a, "ffmpeg-9.0.1-essentials_build/README.txt", new byte[] { 3 });
      }
      string dest = Path.Combine(dir, "out");
      string err;
      Assert.IsTrue(DependencyInstaller.ExtractNames(
        zip, dest, new[] { "ffmpeg.exe", "ffprobe.exe" }, out err), err);
      Assert.IsTrue(File.Exists(Path.Combine(dest, "ffmpeg.exe")));
      Assert.IsTrue(File.Exists(Path.Combine(dest, "ffprobe.exe")));
      Assert.IsFalse(File.Exists(Path.Combine(dest, "README.txt")), "docs not extracted");
      Assert.IsFalse(DependencyInstaller.ExtractNames(
        zip, dest, new[] { "nope.exe" }, out err), "nothing matched must fail loudly");
      Assert.IsNotEmpty(err);
    } finally { WipeDir(dir); }
  }

  static void WriteEntry(System.IO.Compression.ZipArchive a, string name, byte[] data) {
    var e = a.CreateEntry(name);
    using (var s = e.Open()) s.Write(data, 0, data.Length);
  }

  // ---------- offline checks never throw, never lie ----------

  [Test] public void P22I_ChecksGraceful() {
    DependencyCheck m = DependencyInstaller.CheckLanCert(
      Path.Combine(Path.GetTempPath(), "lwe-no-such-dir-xyz"));
    Assert.AreEqual(DependencyKind.LanCert, m.Kind);
    Assert.IsFalse(m.Ready);
    DependencyCheck p = DependencyInstaller.CheckPython();
    Assert.AreEqual(DependencyKind.Python, p.Kind);
    Assert.IsNotEmpty(p.Detail); // version when present, "not-found" otherwise
  }

  // ---------- dialog states (hardware-free UI contract) ----------

  [Test] public void P22J_DialogStates() {
    var go = new UnityEngine.GameObject("DepDlgTest");
    DependencySetupDialog dlg = null;
    try {
      dlg = go.AddComponent<DependencySetupDialog>();
      dlg.BuildUiImmediate();
      Assert.IsFalse(dlg.IsShowing);
      bool installed = false, later = false, closed = false;
      dlg.ShowList("• FFmpeg (thử)\n  vì thử", () => { installed = true; }, () => { later = true; });
      Assert.IsTrue(dlg.IsShowing);
      dlg.SetProgressText("Đang tải... 10%");
      dlg.ShowProgress(() => { });
      Assert.IsTrue(dlg.IsShowing);
      dlg.ShowResult("✓ FFmpeg — xong", () => { closed = true; });
      Assert.IsTrue(dlg.IsShowing);
      Assert.IsFalse(installed);
      Assert.IsFalse(later);
      Assert.IsFalse(closed);
      dlg.Hide();
      Assert.IsFalse(dlg.IsShowing);
    } finally {
      try { UnityEngine.Object.DestroyImmediate(go); } catch (Exception) { }
    }
  }

  [Test] public void P22K_VersionLooks() {    Assert.IsTrue(MediaDependencySpecs.LooksLikeVersion("9.0.1"));
    Assert.IsTrue(MediaDependencySpecs.LooksLikeVersion(" 8.1 "));
    Assert.IsFalse(MediaDependencySpecs.LooksLikeVersion("abc"));
    Assert.IsFalse(MediaDependencySpecs.LooksLikeVersion("1"));
    Assert.IsFalse(MediaDependencySpecs.LooksLikeVersion("1.2.3.4.5"));
    Assert.IsFalse(MediaDependencySpecs.LooksLikeVersion(null));
  }

  // P23 loopback-E2E finding: Update() pumped Stopping only, so sessions
  // stranded in Finalizing after the transcode finished (mp4+mp3 on disk,
  // state never COMPLETED). Both states must pump.
  [Test] public void P22L_FinalizingKeepsPumping() {
    Assert.IsTrue(MediaRecordingService.ShouldPumpStopping(RecordingState.Stopping));
    Assert.IsTrue(MediaRecordingService.ShouldPumpStopping(RecordingState.Finalizing));
    Assert.IsFalse(MediaRecordingService.ShouldPumpStopping(RecordingState.Idle));
    Assert.IsFalse(MediaRecordingService.ShouldPumpStopping(RecordingState.Recording));
    Assert.IsFalse(MediaRecordingService.ShouldPumpStopping(RecordingState.Completed));
    Assert.IsFalse(MediaRecordingService.ShouldPumpStopping(RecordingState.Error));
  }
}
