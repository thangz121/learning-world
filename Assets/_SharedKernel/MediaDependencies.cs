// _SharedKernel/MediaDependencies.cs — Lead owns. Phase 2.3c startup
// dependency specs: what the game needs beyond itself, where it comes
// from, and how to verify it. Pure C# (NO UnityEngine). Security rules:
// HTTPS-only sources, installer identity pinned (winget IDs), portable
// archives verified by SHA-256 fetched alongside the bytes (never trusted
// on URL alone), user consent BEFORE any network fetch (the dialog owns
// that), user-scope/app-local installs only (never admin escalation).
using System;
using System.IO;
using System.Text;

public enum DependencyKind {
  Ffmpeg,   // MP4 (H.264) + MP3 (LAME) deliverables
  Python,   // phone gateway auto-start (child process)
  LanCert,  // LAN HTTPS certificate for the phone QR flow
}

public enum DependencyAction {
  WingetThenPortable, // winget silent first, portable zip fallback
  WingetOnly,         // winget silent, manual text on failure
  GenerateLocal,      // created in-process, no download
}

public struct DependencySpec {
  public DependencyKind Kind;
  public string TitleVi;
  public string WhyVi;
  public DependencyAction Action;
  public string WingetId;      // null when N/A
  public string DownloadUrl;   // null when N/A (portable fallback)
  public string ShaUrl;        // null when N/A (hash fetched with bytes)
  public string VerUrl;        // null when N/A (version label for UI)
  public string ApproxSizeVi;  // user-facing size note
  public string[] ExtractNames; // zip members to keep (by file name)

  public static DependencySpec Ffmpeg() {
    return new DependencySpec {
      Kind = DependencyKind.Ffmpeg,
      TitleVi = "FFmpeg (xuất video MP4 + audio MP3)",
      WhyVi = "Thiếu FFmpeg thì bản thu chỉ còn file WAV/AVI thô, không có MP4/MP3.",
      Action = DependencyAction.WingetThenPortable,
      WingetId = MediaDependencySpecs.FfmpegWingetId,
      DownloadUrl = MediaDependencySpecs.FfmpegZipUrl,
      ShaUrl = MediaDependencySpecs.FfmpegZipUrl + ".sha256",
      VerUrl = MediaDependencySpecs.FfmpegZipUrl + ".ver",
      ApproxSizeVi = "≈110 MB (tải 1 lần)",
      ExtractNames = new[] { "ffmpeg.exe", "ffprobe.exe" },
    };
  }

  public static DependencySpec Python() {
    return new DependencySpec {
      Kind = DependencyKind.Python,
      TitleVi = "Python (kết nối điện thoại qua QR)",
      WhyVi = "Thiếu Python thì game không tự mở cổng kết nối điện thoại, phải chạy tay.",
      Action = DependencyAction.WingetOnly,
      WingetId = MediaDependencySpecs.PythonWingetId,
      DownloadUrl = null,
      ShaUrl = null,
      VerUrl = null,
      ApproxSizeVi = "≈30 MB (tải 1 lần)",
      ExtractNames = null,
    };
  }

  public static DependencySpec LanCert() {
    return new DependencySpec {
      Kind = DependencyKind.LanCert,
      TitleVi = "Chứng chỉ LAN (trang điện thoại https)",
      WhyVi = "Thiếu chứng chỉ thì điện thoại không mở được trang kết nối an toàn.",
      Action = DependencyAction.GenerateLocal,
      WingetId = null,
      DownloadUrl = null,
      ShaUrl = null,
      VerUrl = null,
      ApproxSizeVi = "tạo tại chỗ, không tải",
      ExtractNames = null,
    };
  }
}

public static class MediaDependencySpecs {
  public const string FfmpegWingetId = "Gyan.FFmpeg";
  public const string PythonWingetId = "Python.Python.3";
  public const string FfmpegZipUrl =
    "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
  public const int WingetTimeoutMs = 600000;      // 10'
  public const int DownloadStallMs = 90000;       // 90 s without bytes = abort
  public const int ProbeTimeoutMs = 8000;         // --version probes

  // Exact silent-install command that is executed (logged before run).
  // User-scope/portable packages: no admin escalation attempted, ever.
  public static string WingetCommand(string packageId) {
    try {
      if (string.IsNullOrEmpty(packageId)) return string.Empty;
      return "install -e --id " + packageId
        + " --silent --accept-package-agreements --accept-source-agreements";
    } catch (Exception) { return string.Empty; }
  }

  public static bool IsHttps(string url) {
    try {
      if (string.IsNullOrEmpty(url)) return false;
      Uri u;
      if (!Uri.TryCreate(url, UriKind.Absolute, out u)) return false;
      return string.Equals(u.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    } catch (Exception) { return false; }
  }

  // Gyan publishes "<64 hex>" (+ optional filename) — accept both, normalize.
  public static bool TryParseSha256(string text, out string hash) {
    hash = null;
    try {
      if (string.IsNullOrEmpty(text)) return false;
      string tok = text.Trim().Split(
        new[] { ' ', '\t', '\r', '\n', '*' }, StringSplitOptions.RemoveEmptyEntries)[0];
      if (tok.Length != 64) return false;
      foreach (char c in tok) {
        bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        if (!hex) return false;
      }
      hash = tok.ToLowerInvariant();
      return true;
    } catch (Exception) { return false; }
  }

  public static string Sha256OfFile(string path) {
    try {
      using (var sha = System.Security.Cryptography.SHA256.Create())
      using (var fs = File.OpenRead(path)) {
        byte[] h = sha.ComputeHash(fs);
        var sb = new StringBuilder(64);
        foreach (byte b in h) sb.Append(b.ToString("x2"));
        return sb.ToString();
      }
    } catch (Exception) { return null; }
  }

  public static bool LooksLikeVersion(string v) {
    try {
      if (string.IsNullOrEmpty(v)) return false;
      v = v.Trim();
      string[] parts = v.Split('.');
      if (parts.Length < 2 || parts.Length > 4) return false;
      foreach (string p in parts) {
        int n;
        if (!int.TryParse(p, out n) || n < 0 || n > 9999) return false;
      }
      return true;
    } catch (Exception) { return false; }
  }
}
