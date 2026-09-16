// A_World/LocalCameraClassifier.cs — Agent A (World & Visual).
// Pure C# (NO UnityEngine): picks WHICH local PC camera to show when several
// exist. User rule: an external USB webcam beats the integrated laptop camera;
// with a single camera (or several of the same kind) the first one wins.
// Names are RANKING labels only, never eligibility: every listed device is
// usable, and an all-integrated list still picks one (generic presence stays
// in PcSourcePrecedence + GameCameraStreamService). Heuristics are
// substring-based and conservative on purpose: a missed external label only
// changes WHICH local feed shows when 2+ cameras exist, never whether the
// phone stands down. Never throws (null/empty safe).
public static class LocalCameraClassifier {
  // Integrated-laptop hints (kept tight + lowercase; input is lowercased
  // before compare). Deliberately NOT matching bare "usb"/"uvc"/"webcam":
  // cheap external cameras also enumerate as USB/UVC, and misranking one of
  // them only matters with 2+ cameras plugged in.
  static readonly string[] IntegratedHints = {
    "integrated", "built-in", "builtin", "built in", "internal",
    "easycamera", "easy camera", "truevision", "true vision",
    "crystal eye", "crystaleye", "hp wide vision", "turevision",
    // Stock integrated laptop modules enumerate as USB Video Class devices
    // (e.g. ASUS "USB2.0 HD UVC WebCam" — internally USB-attached). Trade-off,
    // stated plainly: a cheap EXTERNAL camera that also reports a bare-UVC
    // name ranks integrated too, so at 2+ bare-UVC cameras list order wins.
    // Single-camera and brand-named cases are unaffected either way.
    "uvc"
  };

  // True when the name looks like an integrated laptop camera.
  // Null/empty/whitespace -> false (unknown counts as external: with a single
  // oddly-named camera it still gets picked; ranking only matters at 2+).
  public static bool IsIntegrated(string name) {
    if (string.IsNullOrWhiteSpace(name)) return false;
    string n = name.ToLowerInvariant();
    for (int i = 0; i < IntegratedHints.Length; i++) {
      if (n.Contains(IntegratedHints[i])) return true;
    }
    return false;
  }

  // Best device name, or null when none listed (null/empty/whitespace-only
  // entries never count — some drivers report placeholder strings).
  // External outranks integrated; ties keep list order (deterministic).
  public static string PickDevice(string[] devices) {
    if (devices == null || devices.Length == 0) return null;
    string firstUsable = null;
    string firstIntegrated = null;
    for (int i = 0; i < devices.Length; i++) {
      string name = devices[i];
      if (string.IsNullOrWhiteSpace(name)) continue;
      if (firstUsable == null) firstUsable = name;
      if (IsIntegrated(name)) {
        if (firstIntegrated == null) firstIntegrated = name;
      } else {
        return name; // first external wins immediately (list order = stable)
      }
    }
    // No external found: fall back to the first integrated (laptop cam still
    // beats the phone), or null when nothing usable was listed.
    return firstIntegrated ?? firstUsable;
  }

  // True when at least one usable local camera is listed (generic count).
  public static bool HasUsableCamera(string[] devices) {
    return PickDevice(devices) != null;
  }
}
