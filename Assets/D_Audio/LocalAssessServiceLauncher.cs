// D_Audio/LocalAssessServiceLauncher.cs — starts the local speak-assess helper
// (tools/speak_assess_server.py) when it is not already healthy. Foundation/dev
// helper, mirroring the phone-mic gateway autostart pattern: best-effort, never
// throws, and the game still works acoustic-only when the service is absent.
// C# 9.0 only.
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

public static class LocalAssessServiceLauncher {
  public const int DefaultPort = LocalAssessServiceClient.DefaultPort;
  // How long EnsureRunning may wait for the service to answer /health.
  public const int WaitMs = 15000;

  public static bool IsHealthy(int port) {
    try {
      var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/health");
      req.Timeout = 1200;
      req.ReadWriteTimeout = 1200;
      using (var resp = (HttpWebResponse)req.GetResponse()) {
        using (var sr = new StreamReader(resp.GetResponseStream())) {
          string body = sr.ReadToEnd();
          return body != null && body.IndexOf("\"ok\"", StringComparison.Ordinal) >= 0;
        }
      }
    } catch (Exception) {
      return false;
    }
  }

  // Best-effort: healthy already / start + poll. Returns true when healthy.
  public static bool EnsureRunning(int port, string toolsDir) {
    if (IsHealthy(port)) return true;
    if (string.IsNullOrEmpty(toolsDir)) return false;
    string script;
    try {
      script = Path.Combine(toolsDir, "speak_assess_server.py");
    } catch (Exception) { return false; }
    if (!File.Exists(script)) return false;
    string[] candidates = { "python", "python3", "py" };
    for (int i = 0; i < candidates.Length; i++) {
      try {
        var psi = new ProcessStartInfo();
        psi.FileName = candidates[i];
        psi.Arguments = "\"" + script + "\" --port " + port;
        psi.WorkingDirectory = toolsDir;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        Process p = Process.Start(psi);
        if (p != null) break;
      } catch (Exception) {
        // try the next interpreter name
      }
    }
    var sw = Stopwatch.StartNew();
    while (sw.ElapsedMilliseconds < WaitMs) {
      if (IsHealthy(port)) return true;
      try { Thread.Sleep(300); } catch (Exception) { }
    }
    return false;
  }
}
