// D_Audio/PhoneMicProtocol.cs — Phase 2.1-local M6 phone-mic transport contract.
// Additive only. This file defines the WIRE CONTRACT between the phone page,
// the PC gateway (tools/phone_mic_gateway.py), and Unity. It performs NO
// recognition, NO DSP, NO policy, NO audio I/O: pure framing + validation.
//
// Topology (§1/§6/§8):
//   phone browser --WSS--> gateway --TCP loopback--> Unity (this code)
// TLS terminates at the gateway (LAN); loopback needs none (same machine).
//
// Canonical audio (§7): mono PCM16LE @ 16 kHz. The phone page resamples at
// the capture boundary (AudioContext 16 kHz); the gateway REJECTS mismatched
// rates (no silent adaptation); this side REJECTS non-canonical segments.
// The speech engine always receives the same format as the PC microphone.
//
// Frame envelope on the TCP bridge (all integers big-endian):
//   [u32 totalLen][u8 kind][u32 sessionSerial][u32 seq][payload]
// totalLen covers kind+sessionSerial+seq+payload (not itself).
// Kinds gateway->Unity: AUDIO=1 (payload PCM16LE), STOP=2 (clean end),
// ERROR=3 (payload utf8 reason), HELLO=4 (payload utf8 gateway id),
// PRESENCE_UP=5 (phone page opened, payload utf8 "ws-connected"),
// PRESENCE_DOWN=6 (phone page gone, payload utf8 "ws-closed").
// Presence carries NO audio: captures skip it (TakeAsync loops like HELLO);
// the monitor's watcher uses it to tell "STOP pressed, page still open"
// (STOP seen, no DOWN) apart from "phone gone mid-game" (DOWN) in seconds.
// Kinds Unity->gateway: SUBSCRIBE=0x10, CANCEL=0x11 (payload empty).
// Session isolation (§12): the capture latches the first sessionSerial seen
// after SUBSCRIBE; frames from any other serial are dropped, never merged.
using System;
using System.Text;

public enum SpeechInputSource {
  LocalMicrophone, // default production behavior (PC microphone)
  Phone            // explicitly selected phone-over-LAN input
}

public static class PhoneMicProtocol {
  public const int CanonicalSampleRate = 16000;
  public const int CanonicalChannels = 1;
  public const string CanonicalFormat = "pcm16";

  public const int DefaultHttpsPort = 8443;   // phone page + WSS (TLS, LAN)
  public const int DefaultBridgePort = 8451;  // gateway<->Unity TCP loopback
  public const string LoopbackHost = "127.0.0.1";

  public const byte KindAudio = 1;
  public const byte KindStop = 2;
  public const byte KindError = 3;
  public const byte KindHello = 4;
  public const byte KindPresenceUp = 5;
  public const byte KindPresenceDown = 6;
  public const byte KindSubscribe = 0x10;
  public const byte KindCancel = 0x11;

  public const int HeaderBytes = 1 + 4 + 4;      // kind + serial + seq
  public const int MaxFrameBytes = 4 + HeaderBytes + 65535; // len + envelope cap
  public const int MaxSessionSec = 8;            // mirrors capture window (§9)
  public const int MaxBufferedSec = 10;          // bounded queue ceiling (§18)
  public static int MaxBufferedSamples {
    get { return CanonicalSampleRate * MaxBufferedSec; }
  }

  // True when a phone-declared stream matches the canonical contract.
  // Mismatches are REJECTED at the boundary (§7), never adapted silently.
  public static bool IsCanonicalFormat(int sampleRate, int channels, string format) {
    return sampleRate == CanonicalSampleRate
      && channels == CanonicalChannels
      && string.Equals(format, CanonicalFormat, StringComparison.OrdinalIgnoreCase);
  }

  public static string FormatRejection(int sampleRate, int channels, string format) {
    return "need mono pcm16@16000, got rate=" + sampleRate
      + " ch=" + channels + " fmt=" + (format ?? "<null>");
  }

  // PCM16LE bytes -> float32 mono samples in [-1, 1]. Never throws: short
  // trailing bytes are ignored, null/empty yields an empty array.
  public static float[] Pcm16ToFloat32(byte[] bytes, int offset, int count) {
    if (bytes == null || count <= 1 || offset < 0) return new float[0];
    int available = Math.Min(count, bytes.Length - offset);
    int samples = available / 2;
    var out_ = new float[samples];
    for (int i = 0; i < samples; i++) {
      short s = (short)(bytes[offset + i * 2] | (bytes[offset + i * 2 + 1] << 8));
      out_[i] = s / 32768f;
    }
    return out_;
  }

  // Minimal control-message field reader for phone JSON
  // ({"type":"start","session":"...","sampleRate":16000,...}). Avoids a JSON
  // dependency on the gateway boundary; strict: missing/odd values -> false.
  public static bool TryReadControlField(string json, string field, out string value) {
    value = null;
    if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(field)) return false;
    string key = "\"" + field + "\"";
    int ki = json.IndexOf(key, StringComparison.Ordinal);
    if (ki < 0) return false;
    int ci = json.IndexOf(':', ki + key.Length);
    if (ci < 0) return false;
    int i = ci + 1;
    while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
    if (i >= json.Length) return false;
    if (json[i] == '"') {
      int j = json.IndexOf('"', i + 1);
      if (j < 0) return false;
      value = json.Substring(i + 1, j - i - 1);
      return true;
    }
    int k = i;
    while (k < json.Length && (char.IsDigit(json[k]) || json[k] == '-' || json[k] == '.')) k++;
    if (k == i) return false;
    value = json.Substring(i, k - i);
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

  // Strict decoder: malformed envelopes -> false + reason, never an exception
  // to the caller (§23 case 3). Callers treat false as PROTOCOL_ERROR (env).
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
      case KindAudio:
      case KindStop:
      case KindError:
      case KindHello:
      case KindPresenceUp:
      case KindPresenceDown:
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
