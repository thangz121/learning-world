// D_Audio/MicDeviceClassifier.cs — Phase 2.1 mic-setup gate (Agent D).
// Pure C# (NO UnityEngine): decides whether the STARTUP prompt is needed and
// which words the prompt should use ("tai nghe" vs "microphone").
//
// HONESTY NOTE (§11): Unity's Microphone.devices lists CAPTURE endpoints
// only — speakers / output-only headsets NEVER appear here. So "distinguishing
// speakers from mic-capable headsets" holds BY CONSTRUCTION: we only ever
// look at this list. Classification below only LABELS the likely mic kind
// for prompt wording; it never excludes a listed device (a listed-but-broken
// device still flips the service to Error via ReportCaptureFailure, and the
// gate treats Error as "no usable mic").
//
// Heuristics are substring-based and conservative: unknown names count as
// usable (OtherMic). Virtual routing entries (e.g. "Steam Streaming
// Microphone") are real capture endpoints — also usable.
public enum MicDeviceKind {
  None,       // no capture device at all
  BuiltInMic, // laptop/array/internal mic (usable, but kid + fan noise prone)
  HeadsetMic, // headset/earbud/hands-free mic (preferred wording: "tai nghe")
  OtherMic    // anything else listed (USB mic, webcam mic, virtual route...)
}

public static class MicDeviceClassifier {
  // True when at least one capture device is listed. Empty/null/whitespace-
  // only entries never count (some drivers report placeholder strings).
  public static bool HasUsableMic(string[] devices) {
    return BestKind(devices) != MicDeviceKind.None;
  }

  // Best (most headset-like) kind across the list; None when nothing usable.
  public static MicDeviceKind BestKind(string[] devices) {
    if (devices == null || devices.Length == 0) return MicDeviceKind.None;
    bool anyOther = false;
    bool anyBuiltIn = false;
    for (int i = 0; i < devices.Length; i++) {
      MicDeviceKind k = Classify(devices[i]);
      if (k == MicDeviceKind.HeadsetMic) return MicDeviceKind.HeadsetMic;
      if (k == MicDeviceKind.BuiltInMic) anyBuiltIn = true;
      else if (k == MicDeviceKind.OtherMic) anyOther = true;
    }
    if (anyBuiltIn) return MicDeviceKind.BuiltInMic;
    return anyOther ? MicDeviceKind.OtherMic : MicDeviceKind.None;
  }

  // Single-name classifier. Null/empty/whitespace -> None; unknown -> OtherMic.
  public static MicDeviceKind Classify(string name) {
    if (string.IsNullOrWhiteSpace(name)) return MicDeviceKind.None;
    string n = name.ToLowerInvariant();
    if (ContainsAny(n, HeadsetHints)) return MicDeviceKind.HeadsetMic;
    if (ContainsAny(n, BuiltInHints)) return MicDeviceKind.BuiltInMic;
    return MicDeviceKind.OtherMic;
  }

  static bool ContainsAny(string haystack, string[] needles) {
    for (int i = 0; i < needles.Length; i++) {
      if (haystack.Contains(needles[i])) return true;
    }
    return false;
  }

  // Ordered so the strongest signals match first; kept lowercase (input is
  // lowercased before compare). Deliberately broad: missing a headset label
  // only changes prompt WORDING, never eligibility.
  static readonly string[] HeadsetHints = {
    "headset", "hands-free", "handsfree", "headphone", "earphone", "earbud",
    "airpod", "buds", "head set", "hsp", "hfp", "sco",
    "bluetooth", "bt ", " bt", "wireless", "usb headset", "gaming headset"
  };

  static readonly string[] BuiltInHints = {
    "microphone array", "mic array", "array mic", "built-in", "builtin",
    "internal mic", "internal microphone", "integrated mic", "laptop mic",
    "macbook", "imac", "webcam" // webcam mics behave like room mics, not headsets
  };
}
