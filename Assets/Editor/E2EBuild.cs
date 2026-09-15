// Assets/Editor/E2EBuild.cs — TEMPORARY milestone build script (DELETED after the
// final fresh build). Builds the real game (Bootstrap + Market scenes) to a
// Windows dev build outside the repo. Not a test harness: the artifact IS the
// game under test.
using UnityEditor;
using UnityEngine;

public static class E2EBuild {
  public static void BuildWindows() {
    string outDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "opencode", "LWE-E2E");
    System.IO.Directory.CreateDirectory(outDir);
    string exe = System.IO.Path.Combine(outDir, "LWE.exe");
    var opts = new BuildPlayerOptions {
      scenes = new[] {
        "Assets/_Bootstrap/BootstrapScene.unity",
        "Assets/A_World/Supermarket/MarketScene.unity",
      },
      locationPathName = exe,
      target = BuildTarget.StandaloneWindows64,
      options = BuildOptions.Development | BuildOptions.AllowDebugging,
    };
    var report = BuildPipeline.BuildPlayer(opts);
    Debug.Log("[E2EBuild] result=" + report.summary.result
      + " errors=" + report.summary.totalErrors
      + " warnings=" + report.summary.totalWarnings
      + " size=" + report.summary.totalSize + " out=" + exe);
    if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
      EditorApplication.Exit(1);
  }
}
