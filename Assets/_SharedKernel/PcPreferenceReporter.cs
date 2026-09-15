// _SharedKernel/PcPreferenceReporter.cs — Lead owns. Source-precedence signal.
// Pure C# (NO UnityEngine; System.Net.Sockets only). The game decides local vs
// phone per medium (local plugged-in hardware ALWAYS wins — generic rule, not
// per-machine); the gateway cannot decide (it never sees PC hardware), and the
// phone page cannot decide (it never sees the game). This one-shot reporter
// bridges them over the EXISTING bridge sockets:
//
//   game --TCP loopback--> gateway --WSS--> phone page ("pc-prefer" control)
//
// Payload is utf8 "<media>:<origin>": "mic:local" / "mic:phone" / "cam:local"
// / "cam:phone". Each medium reports on its OWN bridge port (mic 8451, cam
// 8452) so the gateway can route to the matching phone sessions. One-shot:
// connect, SUBSCRIBE (the gateway requires it first), send PREFER, close.
// Called on TRANSITIONS only (never per frame). Never throws; failure just
// means the phone keeps its current buttons (game-side precedence still
// applies — the signal only stands the phone down, it never grants access).
//
// LAYERING: LWE.World refs ONLY LWE.SharedKernel, so this file (kernel) must
// NOT reference LWE.Audio (PhoneMicProtocol). The mic envelope bytes needed
// here are DUPLICATED below (kind + SUBSCRIBE), pinned byte-equal by CT-P18
// against the real PhoneMicProtocol framing (same pattern as PhoneLinkProbe).
// The full mic contract stays in D_Audio/PhoneMicProtocol.cs (source of truth).
using System;
using System.Net.Sockets;
using System.Text;

public static class PcPreferenceReporter {
  public const string MicLocal = "mic:local";
  public const string MicPhone = "mic:phone";
  public const string CamLocal = "cam:local";
  public const string CamPhone = "cam:phone";

  // True when the payload names a medium this reporter may send.
  public static bool IsPreferPayload(string payload) {
    return string.Equals(payload, MicLocal, StringComparison.Ordinal)
      || string.Equals(payload, MicPhone, StringComparison.Ordinal)
      || string.Equals(payload, CamLocal, StringComparison.Ordinal)
      || string.Equals(payload, CamPhone, StringComparison.Ordinal);
  }

  // Fire-and-forget report. Returns true when the bytes left the machine.
  public static bool Report(string host, int bridgePort, string payload, bool micBridge) {
    try {
      if (!IsPreferPayload(payload)) return false;
      if (string.IsNullOrEmpty(host)) host = "127.0.0.1";
      if (bridgePort <= 0) return false;
      byte[] prefer;
      byte[] sub;
      if (micBridge) {
        prefer = EncodeMicFrame(KindMicPreferLocal, 0, 0, Encoding.UTF8.GetBytes(payload));
        sub = EncodeMicFrame(KindMicSubscribe, 0, 0, new byte[0]);
      } else {
        prefer = PhoneCameraProtocol.EncodeBridgeFrame(
          PhoneCameraProtocol.KindPreferLocal, 0, 0, Encoding.UTF8.GetBytes(payload));
        sub = PhoneCameraProtocol.EncodeBridgeFrame(PhoneCameraProtocol.KindSubscribe, 0, 0, new byte[0]);
      }
      using (var c = new TcpClient()) {
        IAsyncResult ar = c.BeginConnect(host, bridgePort, null, null);
        if (!ar.AsyncWaitHandle.WaitOne(1500)) return false;
        try { c.EndConnect(ar); } catch (Exception) { return false; }
        if (!c.Connected) return false;
        NetworkStream s = c.GetStream();
        s.WriteTimeout = 1500;
        s.Write(sub, 0, sub.Length);
        s.Write(prefer, 0, prefer.Length);
        s.Flush();
        return true;
      }
    } catch (Exception) { return false; }
  }

  // --- minimal mic bridge framing (mirror of PhoneMicProtocol; see note above) ---
  // Pinned byte-equal by CT-P18 (reporter vs real protocol, same payload).
  const byte KindMicPreferLocal = 0x12;
  const byte KindMicSubscribe = 0x10;

  static byte[] EncodeMicFrame(byte kind, uint serial, uint seq, byte[] payload) {
    int bodyLen = 9 + (payload != null ? payload.Length : 0);
    var frame = new byte[4 + bodyLen];
    frame[0] = (byte)((bodyLen >> 24) & 0xFF);
    frame[1] = (byte)((bodyLen >> 16) & 0xFF);
    frame[2] = (byte)((bodyLen >> 8) & 0xFF);
    frame[3] = (byte)(bodyLen & 0xFF);
    frame[4] = kind;
    frame[5] = (byte)((serial >> 24) & 0xFF);
    frame[6] = (byte)((serial >> 16) & 0xFF);
    frame[7] = (byte)((serial >> 8) & 0xFF);
    frame[8] = (byte)(serial & 0xFF);
    frame[9] = (byte)((seq >> 24) & 0xFF);
    frame[10] = (byte)((seq >> 16) & 0xFF);
    frame[11] = (byte)((seq >> 8) & 0xFF);
    frame[12] = (byte)(seq & 0xFF);
    if (payload != null && payload.Length > 0)
      Buffer.BlockCopy(payload, 0, frame, 13, payload.Length);
    return frame;
  }

  // Test seam: lets CT-P18 pin byte-equality without sockets.
  public static byte[] EncodeMicPreferForTests(string payload) {
    return EncodeMicFrame(KindMicPreferLocal, 0, 0, Encoding.UTF8.GetBytes(payload ?? ""));
  }
}
