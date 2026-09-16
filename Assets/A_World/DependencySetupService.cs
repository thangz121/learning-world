// A_World/DependencySetupService.cs — Agent A (World & Visual).
// Startup dependency driver: shortly after boot, checks (on a worker
// thread) whether the game is missing anything it needs beyond itself
// (FFmpeg for MP4/MP3, Python for the phone gateway, LAN certificate for
// the phone HTTPS page). If all present: total silence, no prompt, ever.
// If something is missing: ONE modal prompt offering one-click install
// (explicit consent BEFORE any download). Decline/close = session skip:
// the game stays fully playable on its fallback paths (verified WAV/AVI,
// manual gateway, manual cert steps), and the prompt returns next launch
// while anything is still missing.
//
// Threading: worker owns checks + installs; the main thread only applies
// the mailbox to the dialog and polls nothing else. No Unity API on the
// worker, no pause (downloads take minutes — freezing the game would look
// like a hang), no re-prompt inside one session.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

[DisallowMultipleComponent]
public class DependencySetupService : MonoBehaviour {
  string _toolsDir;
  string _appToolsDir;
  DependencySetupDialog _dialog;
  Thread _worker;
  volatile bool _sessionDone;
  volatile bool _cancel;

  readonly object _box = new object();
  string _showList;     // non-null = show LIST with this items text
  string _progressText; // latest progress line (PROGRESS mode)
  string _showResult;   // non-null = show RESULT with this summary
  string _logResult;    // non-null = main thread logs it once
  bool _progressMode;

  List<DependencySpec> _missing = new List<DependencySpec>();

  public void Bind(string toolsDir, string appToolsDir, DependencySetupDialog dialog) {
    try {
      _toolsDir = toolsDir;
      _appToolsDir = appToolsDir;
      _dialog = dialog;
    } catch (Exception) { }
  }

  void Start() {
    try {
      if (_sessionDone) return;
      _worker = new Thread(CheckWorker) { IsBackground = true, Name = "DependencySetup" };
      _worker.Start();
    } catch (Exception) { }
  }

  void Update() {
    try {
      string list = null, prog = null, res = null, log = null;
      bool pm = false;
      lock (_box) {
        list = _showList; _showList = null;
        prog = _progressText;
        res = _showResult; _showResult = null;
        log = _logResult; _logResult = null;
        pm = _progressMode;
      }
      if (!string.IsNullOrEmpty(log)) {
        try { Debug.Log("[DepSetup] install finished:\n" + log); } catch (Exception) { }
      }
      if (_dialog == null) return;
      if (list != null) {
        try { _dialog.ShowList(list, OnInstall, OnLater); } catch (Exception) { }
      } else if (res != null) {
        try { _dialog.ShowResult(res, OnClose); } catch (Exception) { }
      } else if (pm) {
        try {
          if (!_dialog.IsShowing) _dialog.ShowProgress(OnStop);
          _dialog.SetProgressText(prog ?? string.Empty);
        } catch (Exception) { }
      }
    } catch (Exception) { }
  }

  void OnInstall() {
    try {
      _cancel = false;
      lock (_box) { _progressMode = true; _progressText = "Bắt đầu cài đặt..."; }
      try { _dialog.ShowProgress(OnStop); } catch (Exception) { }
      var t = new Thread(InstallWorker) { IsBackground = true, Name = "DependencyInstall" };
      t.Start();
    } catch (Exception) { }
  }

  void OnLater() {
    try {
      _sessionDone = true;
      if (_dialog != null) _dialog.Hide();
      lock (_box) { _logResult = "postponed by user (fallbacks active this session)"; }
    } catch (Exception) { }
  }

  void OnClose() {
    try {
      _sessionDone = true;
      if (_dialog != null) _dialog.Hide();
    } catch (Exception) { }
  }

  void OnStop() {
    try {
      _cancel = true;
      lock (_box) { _progressText = "Đang dừng..."; }
    } catch (Exception) { }
  }

  void OnDestroy() {
    try { _cancel = true; } catch (Exception) { }
  }

  // ---------------- worker: check --------------------------------------------

  void CheckWorker() {
    try {
      Thread.Sleep(1500); // let boot visuals settle before any prompt
      if (_sessionDone) return;
      var missing = new List<DependencySpec>();
      var notes = new List<string>();
      try {
        DependencyCheck f = DependencyInstaller.CheckFfmpeg(ExeDir(), _toolsDir, _appToolsDir);
        if (!f.Ready) {
          missing.Add(DependencySpec.Ffmpeg());
          notes.Add("• " + DependencySpec.Ffmpeg().TitleVi + "\n  " +
            DependencySpec.Ffmpeg().WhyVi + " (" + DependencySpec.Ffmpeg().ApproxSizeVi + ")");
        }
      } catch (Exception) { }
      try {
        DependencyCheck p = DependencyInstaller.CheckPython();
        if (!p.Ready) {
          missing.Add(DependencySpec.Python());
          notes.Add("• " + DependencySpec.Python().TitleVi + "\n  " +
            DependencySpec.Python().WhyVi + " (" + DependencySpec.Python().ApproxSizeVi + ")");
        }
      } catch (Exception) { }
      try {
        DependencyCheck c = DependencyInstaller.CheckLanCert(_toolsDir);
        if (!c.Ready) {
          missing.Add(DependencySpec.LanCert());
          notes.Add("• " + DependencySpec.LanCert().TitleVi + "\n  " +
            DependencySpec.LanCert().WhyVi + " (" + DependencySpec.LanCert().ApproxSizeVi + ")");
        }
      } catch (Exception) { }
      if (_sessionDone || missing.Count == 0) return; // all present: total silence
      lock (_box) {
        _missing = missing;
        _showList = string.Join("\n\n", notes.ToArray());
        _logResult = "missing: " + missing.Count + " item(s) — prompt shown";
      }
    } catch (Exception) { }
  }

  // ---------------- worker: install ------------------------------------------

  void InstallWorker() {
    var lines = new List<string>();
    try {
      List<DependencySpec> jobs;
      lock (_box) { jobs = new List<DependencySpec>(_missing); }
      foreach (DependencySpec spec in jobs) {
        if (WasCancelled()) {
          lines.Add("✗ " + spec.TitleVi + ": đã dừng theo yêu cầu.");
          break;
        }
        bool ok = false;
        string note = string.Empty;
        try {
          if (spec.Kind == DependencyKind.Ffmpeg) ok = InstallFfmpeg(spec, out note);
          else if (spec.Kind == DependencyKind.Python) ok = InstallPython(spec, out note);
          else if (spec.Kind == DependencyKind.LanCert) ok = InstallCert(spec, out note);
        } catch (Exception e) { ok = false; note = "lỗi: " + e.GetType().Name; }
        lines.Add((ok ? "✓ " : "✗ ") + spec.TitleVi + (string.IsNullOrEmpty(note) ? "" : " — " + note));
      }
      if (WasCancelled() && lines.Count == 0) lines.Add("Đã dừng, chưa cài gì thêm.");
      lines.Add("");
      lines.Add("Game vẫn chơi bình thường. Mở lại game để kiểm tra tiếp.");
    } catch (Exception) {
      lines.Add("Cài đặt dở dang do lỗi không ngờ. Game vẫn chơi bình thường.");
    }
    string summary = string.Join("\n", lines.ToArray());
    lock (_box) {
      _progressMode = false;
      _progressText = null;
      _showResult = summary;
      _logResult = summary; // main thread logs it (no Unity API on workers)
    }
  }

  bool WasCancelled() {
    try { return _cancel; } catch (Exception) { return false; }
  }

  void Progress(DependencyKind kind, string phase, int percent, string detail) {
    try {
      string line;
      string name = ShortName(kind);
      if (phase == "download" && percent >= 0) line = "Đang tải " + name + "... " + percent + "%";
      else if (phase == "winget") line = "Đang cài " + name + "... " + (detail ?? string.Empty);
      else if (phase == "verify") line = "Đang kiểm tra file " + name + "...";
      else if (phase == "extract") line = "Đang giải nén " + name + "...";
      else if (phase == "generate") line = "Đang tạo " + name + "...";
      else if (phase == "done") line = "Xong " + name + ".";
      else if (phase == "failed") line = "Lỗi " + name + ": " + (detail ?? string.Empty);
      else line = name + ": " + phase;
      lock (_box) { _progressText = line; }
    } catch (Exception) { }
  }

  static string ShortName(DependencyKind kind) {
    return kind == DependencyKind.Ffmpeg ? "FFmpeg"
      : kind == DependencyKind.Python ? "Python" : "chứng chỉ LAN";
  }

  // ---------------- per-item installers --------------------------------------

  bool InstallFfmpeg(DependencySpec spec, out string note) {
    note = string.Empty;
    try {
      // Path 1: winget silent (trusted source, handles PATH + updates).
      Progress(spec.Kind, "winget", -1, "FFmpeg");
      string tail;
      if (DependencyInstaller.InstallViaWinget(spec.WingetId, spec.Kind,
          (p) => Progress(p.Kind, p.Phase, p.Percent, p.Detail),
          () => WasCancelled(), MediaDependencySpecs.WingetTimeoutMs, out tail)) {
        if (RecheckFfmpeg()) {
          note = "cài qua winget";
          Progress(spec.Kind, "done", 100, note);
          return true;
        }
      }
      if (WasCancelled()) {
        note = "đã dừng";
        return false;
      }
      // Path 2: portable zip beside the game (no admin, self-contained).
      string appFfmpeg = null;
      try {
        appFfmpeg = string.IsNullOrEmpty(_appToolsDir) ? null
          : Path.Combine(_appToolsDir, "ffmpeg");
      } catch (Exception) { }
      if (string.IsNullOrEmpty(appFfmpeg)) {
        note = "không cài được qua winget, cũng không có chỗ chứa bản portable. "
          + "Cài tay: winget install -e --id Gyan.FFmpeg";
        return false;
      }
      string zip = null, shaTxt = null;
      try {
        zip = Path.Combine(Path.GetTempPath(), "lwe-ffmpeg.zip");
        shaTxt = Path.Combine(Path.GetTempPath(), "lwe-ffmpeg.sha256");
      } catch (Exception) { }
      string err;
      string ver = "?";
      try {
        string verContent;
        if (DependencyInstaller.DownloadText(spec.VerUrl, out verContent)) {
          string v = (verContent ?? string.Empty).Trim();
          if (MediaDependencySpecs.LooksLikeVersion(v)) ver = v;
        }
      } catch (Exception) { }
      if (!DependencyInstaller.DownloadFile(spec.DownloadUrl, zip,
          (got, total) => {
            int pct = total > 0 ? (int)(got * 100 / Math.Max(1, total)) : -1;
            Progress(spec.Kind, "download", pct, FormatMB(got, total));
          },
          () => WasCancelled(), out err)) {
        note = err == "cancelled" ? "đã dừng khi đang tải"
          : "tải thất bại (" + err + "). Cài tay: winget install -e --id Gyan.FFmpeg";
        return false;
      }
      Progress(spec.Kind, "verify", -1, null);
      if (!DependencyInstaller.DownloadText(spec.ShaUrl, out shaTxt) || !File.Exists(shaTxt)) {
        note = "không lấy được mã kiểm tra, bỏ file tải về cho an toàn. "
          + "Cài tay: winget install -e --id Gyan.FFmpeg";
        SafeDelete(zip);
        return false;
      }
      string expected, actual;
      try { expected = null; MediaDependencySpecs.TryParseSha256(File.ReadAllText(shaTxt), out expected); }
      catch (Exception) { expected = null; }
      actual = MediaDependencySpecs.Sha256OfFile(zip);
      if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(actual)
          || !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)) {
        note = "file tải về không đúng mã kiểm tra, đã xóa. Thử lại hoặc cài tay.";
        SafeDelete(zip);
        return false;
      }
      Progress(spec.Kind, "extract", -1, null);
      if (!DependencyInstaller.ExtractNames(zip, appFfmpeg, spec.ExtractNames, out err)) {
        note = "giải nén thất bại (" + err + ")";
        SafeDelete(zip);
        return false;
      }
      SafeDelete(zip);
      SafeDelete(shaTxt);
      if (RecheckFfmpeg()) {
        note = "bản portable " + ver + " (không cần quyền admin)";
        Progress(spec.Kind, "done", 100, note);
        return true;
      }
      note = "giải nén xong nhưng vẫn chưa chạy được. Cài tay: winget install -e --id Gyan.FFmpeg";
      return false;
    } catch (Exception e) {
      note = "lỗi: " + e.GetType().Name;
      return false;
    }
  }

  bool RecheckFfmpeg() {
    try {
      DependencyCheck c = DependencyInstaller.CheckFfmpeg(ExeDir(), _toolsDir, _appToolsDir);
      return c.Ready;
    } catch (Exception) { return false; }
  }

  bool InstallPython(DependencySpec spec, out string note) {
    note = string.Empty;
    try {
      Progress(spec.Kind, "winget", -1, "Python");
      string tail;
      if (DependencyInstaller.InstallViaWinget(spec.WingetId, spec.Kind,
          (p) => Progress(p.Kind, p.Phase, p.Percent, p.Detail),
          () => WasCancelled(), MediaDependencySpecs.WingetTimeoutMs, out tail)) {
        // Fresh PATH entries need a moment + our birth env is stale: probe
        // the usual install roots as well as PATH.
        Thread.Sleep(3000);
        DependencyCheck c = DependencyInstaller.CheckPython();
        if (c.Ready) {
          note = c.Detail;
          Progress(spec.Kind, "done", 100, note);
          return true;
        }
      }
      if (WasCancelled()) {
        note = "đã dừng";
        return false;
      }
      note = "chưa tự cài được. Cài tay: mở https://www.python.org/downloads/ "
        + "(tick Add to PATH), rồi mở lại game.";
      return false;
    } catch (Exception e) {
      note = "lỗi: " + e.GetType().Name;
      return false;
    }
  }

  bool InstallCert(DependencySpec spec, out string note) {
    note = string.Empty;
    try {
      if (string.IsNullOrEmpty(_toolsDir) || !Directory.Exists(_toolsDir)) {
        note = "không thấy thư mục tools của game. Hỏi kỹ thuật để tạo tay (mkcert/openssl).";
        return false;
      }
      Progress(spec.Kind, "generate", -1, null);
      string ip = DependencyInstaller.DetectLanIp();
      if (string.IsNullOrEmpty(ip)) {
        note = "không thấy mạng LAN (cắm dây/bật Wi-Fi rồi bấm cài lại).";
        return false;
      }
      string crt = Path.Combine(_toolsDir, "lan.crt");
      string key = Path.Combine(_toolsDir, "lan.key");
      string err;
      if (!DependencyInstaller.GenerateLanCert(ip, DependencyInstaller.LocalIPv4s(), crt, key, out err)) {
        note = "tạo thất bại (" + err + "). Cài tay bằng mkcert theo hướng dẫn.";
        return false;
      }
      DependencyCheck c = DependencyInstaller.CheckLanCert(_toolsDir);
      if (c.Ready) {
        note = "cho IP " + ip + " (" + c.Detail + "). Trên điện thoại mở trang sẽ báo "
          + "chứng chỉ — bấm chấp nhận 1 lần.";
        Progress(spec.Kind, "done", 100, note);
        return true;
      }
      note = "tạo xong nhưng kiểm tra lại chưa đạt (" + c.Detail + ")";
      return false;
    } catch (Exception e) {
      note = "lỗi: " + e.GetType().Name;
      return false;
    }
  }

  // ---------------- small plumbing --------------------------------------------

  static string FormatMB(long got, long total) {
    try {
      string g = (got / 1048576.0).ToString("0") + " MB";
      if (total > 0) g += " / " + (total / 1048576.0).ToString("0") + " MB";
      return g;
    } catch (Exception) { return string.Empty; }
  }

  static void SafeDelete(string path) {
    try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); }
    catch (Exception) { }
  }

  string ExeDir() {
    try {
      using (var p = System.Diagnostics.Process.GetCurrentProcess()) {
        string exe = p.MainModule.FileName;
        return Path.GetDirectoryName(exe);
      }
    } catch (Exception) { return null; }
  }
}
