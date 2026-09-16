// Assets/Editor/E2EBuild.cs — TEMPORARY milestone build script (DELETED after the
// final fresh build). Builds the real game (Bootstrap + Market scenes) to a
// Windows dev build outside the repo. Not a test harness: the artifact IS the
// game under test.
using UnityEditor;
using UnityEngine;

public static class E2EBuild {
  public static void BuildWindows() {
    BuildTo("LWE-E2E", BuildOptions.Development | BuildOptions.AllowDebugging);
  }

  // Release cho máy nhà (user ask): KHÔNG Development/AllowDebugging (hết
  // console overlay + debug socket), còn lại giống hệt E2E (scenes, tools).
  public static void BuildRelease() {
    BuildTo("LWE-Release", BuildOptions.None);
  }

  static void BuildTo(string outName, BuildOptions options) {
    string outDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "opencode", outName);
    System.IO.Directory.CreateDirectory(outDir);
    string exe = System.IO.Path.Combine(outDir, "LWE.exe");
    var opts = new BuildPlayerOptions {
      scenes = new[] {
        "Assets/_Bootstrap/BootstrapScene.unity",
        "Assets/A_World/Supermarket/MarketScene.unity",
      },
      locationPathName = exe,
      target = BuildTarget.StandaloneWindows64,
      options = options,
    };
    var report = BuildPipeline.BuildPlayer(opts);
    Debug.Log("[E2EBuild] result=" + report.summary.result
      + " errors=" + report.summary.totalErrors
      + " warnings=" + report.summary.totalWarnings
      + " size=" + report.summary.totalSize + " out=" + exe);
    if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
      EditorApplication.Exit(1);
    BundlePhoneTools(outDir);
  }

  // The in-game phone flow auto-starts tools/phone_mic_gateway.py (see
  // MicSetupMonitor.TryStartGateway, which resolves <exeDir>/tools). Ship a
  // copy next to the build so the feature works outside the repo checkout.
  // Dev-E2E only: lan.crt/key are machine certs, never ship them in a real
  // installer (production needs per-machine certs + DHCP reservation).
  static void BundlePhoneTools(string outDir) {
    try {
      string root = System.IO.Path.GetFullPath(
        System.IO.Path.Combine(UnityEngine.Application.dataPath, ".."));
      string src = System.IO.Path.Combine(root, "tools");
      string dst = System.IO.Path.Combine(outDir, "tools");
      System.IO.Directory.CreateDirectory(dst);
      foreach (string f in new[] {
          "phone_mic_gateway.py", "phone_mic_page.html",
          "phone_camera_page.html", "phone_page.html",
          "lwe_qr.py", "qrcodegen.py", "lan.crt", "lan.key" }) {
        string s = System.IO.Path.Combine(src, f);
        if (System.IO.File.Exists(s))
          System.IO.File.Copy(s, System.IO.Path.Combine(dst, f), true);
        else
          Debug.LogWarning("[E2EBuild] phone-tools bundle missing: " + f);
      }
      Debug.Log("[E2EBuild] phone tools bundled at " + dst);
    } catch (System.Exception e) {
      Debug.LogWarning("[E2EBuild] phone-tools bundle failed: " + e.Message);
    }
  }
}
