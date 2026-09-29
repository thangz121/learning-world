// TempBuildP62.cs — TEMP build driver for gameplay #7 (discovery, standing
// order: committed). Drop-in path Assets/Editor/TempBuildP62.cs.
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TempBuildP62 {
  public static void Build() { BuildTo("D:/Vscode/P62Build"); }

  public static void BuildJourney() { BuildTo("D:/Vscode/P62JBuild"); }

  static void BuildTo(string outDir) {
    Directory.CreateDirectory(outDir);
    var scenes = new System.Collections.Generic.List<string>();
    foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes) {
      if (s.enabled) scenes.Add(s.path);
    }
    var opts = new BuildPlayerOptions();
    opts.scenes = scenes.ToArray();
    opts.locationPathName = outDir + "/LWE.exe";
    opts.target = BuildTarget.StandaloneWindows64;
    opts.options = BuildOptions.None;
    var report = BuildPipeline.BuildPlayer(opts);
    long size = 0;
    try { size = new FileInfo(outDir + "/LWE.exe").Length; } catch (System.Exception) { }
    Debug.Log("[TempBuildP62] result=" + report.summary.result
      + " errors=" + report.summary.totalErrors
      + " warnings=" + report.summary.totalWarnings
      + " size=" + size);
  }
}
