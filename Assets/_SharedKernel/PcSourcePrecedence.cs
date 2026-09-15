// _SharedKernel/PcSourcePrecedence.cs — Lead owns. Source-precedence DECISION.
// Pure C# (NO UnityEngine, NO sockets). The game prefers plugged-in PC
// hardware over the phone PER MEDIUM (mic, cam), generically: ANY listed
// device counts — names are labels only, never eligibility (MicDeviceClassifier
// honesty rule extended to cameras). The gateway cannot decide (it never sees
// PC hardware); the phone page cannot decide (it never sees the game). This
// file owns the DECISION; PcPreferenceReporter owns the TRANSPORT (bridge
// PREFER frame); the gateway owns the RELAY (pc-prefer push); the phone pages
// own the UI (START stands down). Game-side precedence applies regardless —
// the wire signal only moves the phone UI, it never grants access.
//
// Payloads: "mic:local" / "mic:phone" / "cam:local" / "cam:phone" (exact,
// ordinal). forcePhone (E2E flags -e2e-nomic / -e2e-nocam) simulates no local
// hardware so the phone path stays reachable in-build. Never throws.
using System;

public static class PcSourcePrecedence {
  public const string MicLocal = "mic:local";
  public const string MicPhone = "mic:phone";
  public const string CamLocal = "cam:local";
  public const string CamPhone = "cam:phone";

  // Mic: localReady = gate/composite says the PC mic is usable NOW.
  public static string PreferMic(bool localReady, bool forcePhone = false) {
    if (forcePhone) return MicPhone;
    return localReady ? MicLocal : MicPhone;
  }

  // Cam: localCamPresent = ANY PC webcam listed NOW (generic count, no names).
  public static string PreferCam(bool localCamPresent, bool forcePhone = false) {
    if (forcePhone) return CamPhone;
    return localCamPresent ? CamLocal : CamPhone;
  }

  public static bool IsPreferPayload(string payload) {
    return string.Equals(payload, MicLocal, StringComparison.Ordinal)
      || string.Equals(payload, MicPhone, StringComparison.Ordinal)
      || string.Equals(payload, CamLocal, StringComparison.Ordinal)
      || string.Equals(payload, CamPhone, StringComparison.Ordinal);
  }

  // "mic" / "cam" / "" (never throws, "" = not a prefer payload).
  public static string MediaOf(string payload) {
    try {
      if (!IsPreferPayload(payload)) return string.Empty;
      int i = payload.IndexOf(':');
      return i > 0 ? payload.Substring(0, i) : string.Empty;
    } catch (Exception) { return string.Empty; }
  }

  // "local" / "phone" / "" (never throws).
  public static string OriginOf(string payload) {
    try {
      if (!IsPreferPayload(payload)) return string.Empty;
      int i = payload.IndexOf(':');
      return i >= 0 && i + 1 < payload.Length ? payload.Substring(i + 1) : string.Empty;
    } catch (Exception) { return string.Empty; }
  }

  public static bool IsLocalOrigin(string payload) {
    return string.Equals(OriginOf(payload), "local", StringComparison.Ordinal);
  }
}
