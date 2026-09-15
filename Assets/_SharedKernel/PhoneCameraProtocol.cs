// _SharedKernel/PhoneCameraProtocol.cs — Phase 2.2 phone camera transport contract.
// Additive only. Mirrors the PROVEN Phase 2.1 phone-mic wire architecture
// (PhoneMicProtocol + tools/phone_mic_gateway.py) for a SECOND, INDEPENDENT
// media path: JPEG camera frames instead of PCM16 audio.
//
// Reused from the mic path (same patterns, same envelope shape):
//   phone browser --WSS--> gateway --TCP loopback--> Unity (this code)
//   TLS terminates at the gateway (LAN); loopback needs none (same machine).
//   [u32 totalLen][u8 kind][u32 sessionSerial][u32 seq][payload]
//   totalLen covers kind+sessionSerial+seq+payload (not itself).
//   Session latch: first sessionSerial after SUBSCRIBE wins; foreign dropped.
//   HELLO-first: gateway greets BEFORE joining broadcast (no AUDIO-before-HELLO).
//   Presence UP/DOWN (no media): page opened / page gone.
//   Bounded latest-frame (CameraFrameSource): drop, never queue.
//   Never throws out of decode/validate (reason strings, not exceptions).
//
// Deliberately DIFFERENT from the mic path (media independence, §8):
//   separate TCP bridge port (8452 vs 8451) so a large JPEG can NEVER
//   head-of-line-block speech audio. Separate sessionSerial space, separate
//   watcher thread, separate frame store. Audio and video share NO queue,
//   NO thread, NO socket — only the envelope SHAPE and lifecycle PATTERNS.
//   A camera stall/drop can never fail, delay, or contaminate a mic capture.
//
// Payload (Phase 2.2): JPEG bytes only (JFIF SOI FF D8 FF). No raw frames,
// no video codec (no H.264/VP9/AV1 — Phase 2.3 owns recording/encoding).
// Default 320x240 @ 60fps q60: a face box needs ~10-20 KB/frame, NOT
// megabytes. Validation REJECTS (never adapts): empty, oversize, non-JPEG.
using System;
using System.Text;

public enum PhoneCameraState {
  Disabled,               // service not running / feature off
  WaitingForPhone,        // running, no transport to the gateway yet
  Connecting,             // transport up, phone page not seen yet
  ConnectedWaitingFrames, // page open (UP) but no frame decoded yet (or STOPped idle)
  Live,                   // fresh frame displayed within the stale window
  TempDisconnected,       // stale beyond threshold / DOWN / bridge lost (no frozen face)
  Error,                  // protocol/permission error (recovers on next good edge)
  Stopped                 // service stopped by game (terminal until restarted)
}

public struct PhoneCameraConfig {
  public int Width;
  public int Height;
  public int TargetFps;
  public int JpegQuality;
  public int MaxFrameBytes;
  public int StaleMs;

  public static PhoneCameraConfig Default {
    get {
      return new PhoneCameraConfig {
        Width = 320,
        Height = 240,
        TargetFps = 60,
        JpegQuality = 60,
        MaxFrameBytes = 200 * 1024,
        StaleMs = 3000,
      };
    }
  }

  // Bounds are the CONFIG contract (§4): small face preview, never full-res.
  // TargetFps is the DECLARED OPERATING POINT (default 60, the screen
  // average); the phone grabs at most 60/s (rAF-throttled) and the PC decodes
  // at most once per rendered frame off the depth-ONE slot — so the ceiling
  // is the hardware, not a number here. 120 is a sanity bound (fastest phone
  // displays), never a stream cap. False + reason (never throws) so bad
  // configs fail at Bind.
  public bool Validate(out string reason) {
    if (Width < 160 || Width > 640) { reason = "width 160..640"; return false; }
    if (Height < 120 || Height > 480) { reason = "height 120..480"; return false; }
    if (TargetFps < 1 || TargetFps > 120) { reason = "fps 1..120"; return false; }
    if (JpegQuality < 30 || JpegQuality > 85) { reason = "quality 30..85"; return false; }
    if (MaxFrameBytes < 32 * 1024 || MaxFrameBytes > PhoneCameraProtocol.MaxPayloadBytes) {
      reason = "maxFrameBytes 32KB..cap"; return false;
    }
    if (StaleMs < 1000 || StaleMs > 10000) { reason = "staleMs 1000..10000"; return false; }
    reason = null;
    return true;
  }
}

public static class PhoneCameraProtocol {
  public const string LoopbackHost = "127.0.0.1";
  public const int DefaultBridgePort = 8452; // video-only; mic stays on 8451 (§8)
  public const int DefaultHttpsPort = 8443;  // shared with the mic gateway (same TLS)
  public const string CameraWsPath = "/cam";
  public const string CameraPagePath = "/camera";

  public const byte KindFrame = 1;  // payload: JPEG bytes (one still per frame)
  public const byte KindStop = 2;   // payload empty (user pressed STOP, page may stay open)
  public const byte KindError = 3;  // payload utf8 reason (denied/unavailable/replaced/...)
  public const byte KindHello = 4;  // payload utf8 gateway id (greeting, skipped by watcher)
  public const byte KindUp = 5;     // payload utf8 "ws-connected" (page opened, no media yet)
  public const byte KindDown = 6;   // payload utf8 "ws-closed" (page gone mid-game)
  public const byte KindSubscribe = 0x10; // Unity -> gateway (payload empty)
  public const byte KindCancel = 0x11;    // Unity -> gateway (payload empty)

  public const int HeaderBytes = 1 + 4 + 4; // kind + serial + seq
  public const int MaxPayloadBytes = 300 * 1024; // envelope cap (JPEG headroom)
  public const int MaxFrameBytes = 4 + HeaderBytes + MaxPayloadBytes;

  // JPEG SOI marker: FF D8 FF. Phase 2.2 accepts JPEG ONLY (simplest realtime
  // frame the existing transport carries efficiently; WebP/future formats are
  // a decoder + magic change here, not a transport change).
  public static bool LooksLikeJpeg(byte[] bytes) {
    if (bytes == null || bytes.Length < 4) return false;
    return bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
  }

  public static bool IsValidFramePayload(byte[] payload, int maxBytes, out string reason) {
    reason = null;
    if (payload == null || payload.Length == 0) { reason = "empty-frame"; return false; }
    if (payload.Length > maxBytes) { reason = "oversize-frame"; return false; }
    if (!LooksLikeJpeg(payload)) { reason = "not-jpeg"; return false; }
    return true;
  }

  public static byte[] EncodeBridgeFrame(byte kind, uint sessionSerial, uint seq, byte[] payload) {
    int bodyLen = HeaderBytes + (payload != null ? payload.Length : 0);
    var frame = new byte[4 + bodyLen];
    frame[0] = (byte)((bodyLen >> 24) & 0xFF);
    frame[1] = (byte)((bodyLen >> 16) & 0xFF);
    frame[2] = (byte)((bodyLen >> 8) & 0xFF);
    frame[3] = (byte)(bodyLen & 0xFF);
    frame[4] = kind;
    frame[5] = (byte)((sessionSerial >> 24) & 0xFF);
    frame[6] = (byte)((sessionSerial >> 16) & 0xFF);
    frame[7] = (byte)((sessionSerial >> 8) & 0xFF);
    frame[8] = (byte)(sessionSerial & 0xFF);
    frame[9] = (byte)((seq >> 24) & 0xFF);
    frame[10] = (byte)((seq >> 16) & 0xFF);
    frame[11] = (byte)((seq >> 8) & 0xFF);
    frame[12] = (byte)(seq & 0xFF);
    if (payload != null && payload.Length > 0)
      Buffer.BlockCopy(payload, 0, frame, 13, payload.Length);
    return frame;
  }

  public struct BridgeFrame {
    public byte Kind;
    public uint SessionSerial;
    public uint Seq;
    public byte[] Payload;
  }

  // Strict decoder: malformed -> false + reason, never an exception.
  public static bool TryDecodeBridgeFrame(byte[] bytes, int offset, int count,
      out BridgeFrame frame, out string reason) {
    frame = new BridgeFrame();
    reason = null;
    if (bytes == null || count < 4 + HeaderBytes || offset < 0
        || offset + count > bytes.Length) {
      reason = "short-envelope";
      return false;
    }
    int bodyLen = (bytes[offset] << 24) | (bytes[offset + 1] << 16)
      | (bytes[offset + 2] << 8) | bytes[offset + 3];
    if (bodyLen < HeaderBytes || bodyLen > MaxFrameBytes - 4) {
      reason = "bad-length";
      return false;
    }
    if (count < 4 + bodyLen) {
      reason = "truncated";
      return false;
    }
    frame.Kind = bytes[offset + 4];
    frame.SessionSerial = ((uint)bytes[offset + 5] << 24) | ((uint)bytes[offset + 6] << 16)
      | ((uint)bytes[offset + 7] << 8) | bytes[offset + 8];
    frame.Seq = ((uint)bytes[offset + 9] << 24) | ((uint)bytes[offset + 10] << 16)
      | ((uint)bytes[offset + 11] << 8) | bytes[offset + 12];
    int payloadLen = bodyLen - HeaderBytes;
    frame.Payload = new byte[Math.Max(0, payloadLen)];
    if (payloadLen > 0)
      Buffer.BlockCopy(bytes, offset + 13, frame.Payload, 0, payloadLen);
    switch (frame.Kind) {
      case KindFrame:
      case KindStop:
      case KindError:
      case KindHello:
      case KindUp:
      case KindDown:
      case KindSubscribe:
      case KindCancel:
        return true;
      default:
        reason = "unknown-kind:" + frame.Kind;
        return false;
    }
  }

  public static string Utf8(byte[] payload) {
    if (payload == null || payload.Length == 0) return string.Empty;
    try { return Encoding.UTF8.GetString(payload); } catch (Exception) { return string.Empty; }
  }
}
