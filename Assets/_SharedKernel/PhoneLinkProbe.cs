// _SharedKernel/PhoneLinkProbe.cs — mic-setup gate link check (Lead owns).
// Pure C# (NO UnityEngine; System.Net.Sockets only). Lives in SharedKernel
// (not D_Audio) because the A_World driver (LWE.World) must call it and
// LWE.World references ONLY LWE.SharedKernel. Answers the one question the
// startup dialog and exercise entry need: is a PHONE actually streaming
// through the gateway right now (phone↔gateway↔PC end to end)?
//
// Method: open a THROWAWAY TCP bridge subscriber (the gateway holds many;
// this never disturbs the real capture transport), SUBSCRIBE, then watch up
// to waitSec for a live-session frame:
//   AUDIO (or clean STOP)  -> PhoneLinked   (a phone session is/was live)
//   only greeting/idle     -> GatewayUpNoPhone (gateway chạy, điện thoại chưa START)
//   TCP connect fails      -> GatewayDown   (gateway chưa chạy?)
// Malformed traffic -> GatewayUpNoPhone (gateway answers, just not usefully).
// Never throws: every failure maps to a state (caller shows status lines).
//
// Threading: fully async, no main-thread requirement. The driver probes from
// a worker context and marshals the result back (TCP dials block).
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public static class PhoneLinkProbe {
  // One-shot probe. waitSec bounds the AUDIO watch (each read is also
  // slice-bounded so cancellation is prompt). host/port default to the
  // loopback bridge (PhoneMicProtocol.LoopbackHost/DefaultBridgePort).
  // NOTE: PhoneMicProtocol lives in LWE.Audio, which SharedKernel can NOT
  // reference — so the envelope constants needed here are duplicated below
  // (kind bytes + SUBSCRIBE), pinned equal by CT-P15 (probe against the real
  // framing incl. a gateway-behavior loopback). The full contract stays in
  // D_Audio/PhoneMicProtocol.cs (source of truth).
  const byte KindAudio = 1;
  const byte KindStop = 2;
  const byte KindError = 3;
  const byte KindHello = 4;
  const byte KindPresenceUp = 5;
  const byte KindPresenceDown = 6;
  const byte KindSubscribe = 0x10;
  const string DefaultHost = "127.0.0.1";
  const int DefaultPort = 8451;

  public static async Task<PhoneLinkState> ProbeAsync(
      string host, int port, float waitSec, CancellationToken ct) {
    string h = string.IsNullOrEmpty(host) ? DefaultHost : host;
    int p = port > 0 ? port : DefaultPort;
    if (waitSec <= 0f) waitSec = 3f;
    if (waitSec > 15f) waitSec = 15f; // bounded: a prompt must never hang

    TcpClient client = null;
    try {
      client = await ConnectAsync(h, p, ct).ConfigureAwait(false);
      if (client == null) return PhoneLinkState.GatewayDown;
      using (NetworkStream stream = client.GetStream()) {
        byte[] sub = EncodeFrame(KindSubscribe, 0, 0, new byte[0]);
        await stream.WriteAsync(sub, 0, sub.Length, ct).ConfigureAwait(false);
        // Watch for a live phone session until the window expires. (TCP
        // connect + accepted SUBSCRIBE + well-formed frames already prove the
        // gateway; only a session frame proves the PHONE.)
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(waitSec);
        while (DateTime.UtcNow < deadline) {
          if (ct.IsCancellationRequested) return PhoneLinkState.Unknown;
          float slice = (float)Math.Min(0.5, (deadline - DateTime.UtcNow).TotalSeconds);
          if (slice <= 0f) break;
          DecodedFrame? frame = await TryReadFrameAsync(stream, slice, ct).ConfigureAwait(false);
          if (!frame.HasValue) continue; // quiet slice: keep watching
          switch (frame.Value.Kind) {
            case KindAudio:
            case KindStop:
              return PhoneLinkState.PhoneLinked;
            case KindError: {
              // A session-scoped error ("cancelled") still proves a phone was
              // attached; a transport-level protocol error does not.
              string reason = Utf8(frame.Value.Payload);
              if (!string.Equals(reason, "protocol_error", StringComparison.Ordinal))
                return PhoneLinkState.PhoneLinked;
              break;
            }
            case KindHello:
              break; // greeting: keep watching
            default:
              break;
          }
        }
        return PhoneLinkState.GatewayUpNoPhone;
      }
    } catch (OperationCanceledException) {
      return PhoneLinkState.Unknown;
    } catch (Exception) {
      return PhoneLinkState.GatewayDown;
    } finally {
      try { if (client != null) client.Close(); } catch (Exception) { }
    }
  }

  // --- minimal bridge framing (mirror of PhoneMicProtocol; see note above) ---

  struct DecodedFrame {
    public byte Kind;
    public uint SessionSerial;
    public uint Seq;
    public byte[] Payload;
  }

  static byte[] EncodeFrame(byte kind, uint serial, uint seq, byte[] payload) {
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

  static string Utf8(byte[] payload) {
    if (payload == null || payload.Length == 0) return string.Empty;
    try { return System.Text.Encoding.UTF8.GetString(payload); } catch (Exception) { return string.Empty; }
  }

  // Null = connect failed (GatewayDown). Never throws.
  static async Task<TcpClient> ConnectAsync(string host, int port, CancellationToken ct) {
    TcpClient client = null;
    try {
      client = new TcpClient();
      Task connect = client.ConnectAsync(host, port);
      Task timeout = Task.Delay(TimeSpan.FromSeconds(1.5), ct);
      Task first = await Task.WhenAny(connect, timeout).ConfigureAwait(false);
      if (first != connect || ct.IsCancellationRequested) {
        try { client.Close(); } catch (Exception) { }
        return null;
      }
      await connect.ConfigureAwait(false); // surfaces refusal as exception
      if (!client.Connected) {
        try { client.Close(); } catch (Exception) { }
        return null;
      }
      return client;
    } catch (Exception) {
      try { if (client != null) client.Close(); } catch (Exception) { }
      return null;
    }
  }

  // Null = quiet slice (timeout) — NOT a failure. Throws only on cancel.
  static async Task<DecodedFrame?> TryReadFrameAsync(
      NetworkStream stream, float timeoutSec, CancellationToken ct) {
    byte[] len = await ReadExactAsync(stream, 4, timeoutSec, ct).ConfigureAwait(false);
    if (len == null) return null;
    int bodyLen = (len[0] << 24) | (len[1] << 16) | (len[2] << 8) | len[3];
    if (bodyLen < 9 || bodyLen > 4 + 9 + 65535)
      return null; // malformed: treat window as quiet, caller decides
    byte[] body = await ReadExactAsync(stream, bodyLen, timeoutSec, ct).ConfigureAwait(false);
    if (body == null) return null;
    byte kind = body[0];
    switch (kind) {
      case KindAudio:
      case KindStop:
      case KindError:
      case KindHello:
      case KindSubscribe:
      case KindPresenceUp:   // page opened: NOT a session (probe keeps watching
      case KindPresenceDown: // for AUDIO/STOP); the watcher consumes these.
        break;
      default:
        return null;
    }
    uint serial = ((uint)body[1] << 24) | ((uint)body[2] << 16)
      | ((uint)body[3] << 8) | body[4];
    uint seq = ((uint)body[5] << 24) | ((uint)body[6] << 16)
      | ((uint)body[7] << 8) | body[8];
    var payload = new byte[Math.Max(0, bodyLen - 9)];
    if (payload.Length > 0) Buffer.BlockCopy(body, 9, payload, 0, payload.Length);
    return new DecodedFrame { Kind = kind, SessionSerial = serial, Seq = seq, Payload = payload };
  }

  // Null = slice expired without bytes. Throws only on caller cancellation.
  static async Task<byte[]> ReadExactAsync(
      NetworkStream stream, int count, float timeoutSec, CancellationToken ct) {
    var buf = new byte[count];
    int got = 0;
    DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(0.1, timeoutSec));
    while (got < count) {
      ct.ThrowIfCancellationRequested();
      float left = (float)(deadline - DateTime.UtcNow).TotalSeconds;
      if (left <= 0f) return null;
      Task<int> read = stream.ReadAsync(buf, got, count - got, ct);
      Task quiet = Task.Delay(TimeSpan.FromSeconds(left), ct);
      Task first = await Task.WhenAny(read, quiet).ConfigureAwait(false);
      if (first != read) return null; // slice expired (or cancelled -> throws below)
      ct.ThrowIfCancellationRequested();
      int n = await read.ConfigureAwait(false);
      if (n <= 0) return null; // orderly shutdown: no more frames coming
      got += n;
    }
    return buf;
  }
}
