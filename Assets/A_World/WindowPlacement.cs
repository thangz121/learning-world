// A_World/WindowPlacement.cs — S3-P2Z12 operator convenience (user order):
// "mở game nhỏ nhất có thể, góc phải-dưới màn hình" on the demo/test machine.
// Launch flag driven, inert by default: `-window-bottom-right` parks the main
// window at the bottom-right of the primary display after the first scene load.
// Uses Screen.MoveMainWindowTo (standalone players; a no-op risk is caught and
// logged — the game never fails because a window could not be moved).
// C# 9.0 only.
using System;
using UnityEngine;

public static class WindowPlacement {
  public const string Flag = "-window-bottom-right";

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Apply() {
    try {
      bool want = false;
      foreach (string a in Environment.GetCommandLineArgs()) {
        if (string.Equals(a, Flag, StringComparison.OrdinalIgnoreCase)) { want = true; break; }
      }
      if (!want) return;
      DisplayInfo display = Screen.mainWindowDisplayInfo;
      int w = Screen.width > 0 ? Screen.width : 640;
      int h = Screen.height > 0 ? Screen.height : 360;
      int x = Mathf.Max(0, display.width - w);
      int y = Mathf.Max(0, display.height - h);
      Screen.MoveMainWindowTo(display, new Vector2Int(x, y));
      Debug.Log("[WindowPlacement] parked bottom-right x=" + x + " y=" + y
        + " size=" + w + "x" + h + " display=" + display.width + "x" + display.height);
    } catch (Exception e) {
      Debug.LogWarning("[WindowPlacement] could not park the window: " + e.Message);
    }
  }
}
