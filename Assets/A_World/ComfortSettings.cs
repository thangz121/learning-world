// A_World/ComfortSettings.cs — S3-P2Z37 (user: "UX/UI ở mức cao nhất", P5).
// Accessibility + a touch of comfort, shared by every activity:
//   * ReduceMotion — disables camera shake / wobble / bob (vestibular safety).
//   * BigText      — larger banner + objective type (low-vision / small screens).
// Flags on the command line (-reduce-motion, -big-text) or the in-game keys
// (M = motion on/off, B = big text on/off). Pure parser for EditMode cover.
// C# 9.0 only.
using System;
using UnityEngine;

public static class ComfortSettings {
  public static bool ReduceMotion { get; private set; }
  public static bool BigText { get; private set; }

  static bool _booted;

  // Pure command-line parse (EditMode-testable).
  public static void ParseInto(string[] args, out bool reduce, out bool big) {
    reduce = false;
    big = false;
    if (args == null) return;
    foreach (string a in args) {
      if (string.Equals(a, "-reduce-motion", StringComparison.OrdinalIgnoreCase)) reduce = true;
      if (string.Equals(a, "-big-text", StringComparison.OrdinalIgnoreCase)) big = true;
    }
  }

  public static void Apply(bool reduce, bool big) {
    ReduceMotion = reduce;
    BigText = big;
    GameJuice.ReduceMotion = reduce;
    ActivityFeedback.SetBigText(big);
  }

  public static void ToggleReduceMotion() { Apply(!ReduceMotion, BigText); }
  public static void ToggleBigText() { Apply(ReduceMotion, !BigText); }

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void Boot() {
    if (_booted) return;
    _booted = true;
    ParseInto(Environment.GetCommandLineArgs(), out bool reduce, out bool big);
    Apply(reduce, big);
  }

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void SpawnHost() {
    GameObject go = new GameObject("ComfortSettings");
    UnityEngine.Object.DontDestroyOnLoad(go);
    go.AddComponent<ComfortSettingsHost>();
  }
}

// Reads the toggle keys. Null-safe in batch/EditMode (no Keyboard device).
public class ComfortSettingsHost : MonoBehaviour {
  void Update() {
    try {
      var kb = UnityEngine.InputSystem.Keyboard.current;
      if (kb == null) return;
      if (kb.mKey.wasPressedThisFrame) {
        ComfortSettings.ToggleReduceMotion();
        ActivityFeedback.Banner(
          ComfortSettings.ReduceMotion
            ? DialogueLang.T("LESS MOTION", "GIẢM CHUYỂN ĐỘNG")
            : DialogueLang.T("MORE MOTION", "BẬT CHUYỂN ĐỘNG"),
          ActivityFeedback.Kind.Info, 1.2f);
      }
      if (kb.bKey.wasPressedThisFrame) {
        ComfortSettings.ToggleBigText();
        ActivityFeedback.Banner(
          ComfortSettings.BigText
            ? DialogueLang.T("BIG TEXT ON", "CHỮ TO: BẬT")
            : DialogueLang.T("BIG TEXT OFF", "CHỮ TO: TẮT"),
          ActivityFeedback.Kind.Info, 1.2f);
      }
    } catch (Exception) { }
  }
}
