// _SharedKernel/PhonePresenceWatcher.cs — Lead owns. Mid-game link monitor.
// Pure C# (NO UnityEngine; System.Net.Sockets only). While the one-shot
// PhoneLinkProbe answers "is a phone streaming RIGHT NOW", this class holds a
// PERSISTENT bridge subscription and streams link LIFECYCLE edges to the
// driver, so a drop or an explicit STOP mid-game is noticed in seconds:
//
//   PhoneUp      phone page opened (WS up, before any START)
//   PhoneAudio   live session audio flowing (same proof as the probe)
//   PhoneStopped clean session end (user pressed STOP, page may stay open)
//   PhoneDown    phone page GONE (WS closed/dropped) — authoritative
//   TransportLost our bridge socket died (gateway died, or our own blip)
//   GatewayDown  bridge unreachable (gateway not running?)
//   TransportOk  (re)connected + SUBSCRIBE accepted
//
// Threading: a background thread owns the TcpClient. The driver (main thread)
// polls ReadState() in Update and applies the LATEST edge only — rapid
// AUDIO bursts collapse harmlessly; a DOWN following them still wins.
// Never throws out of public methods; never touches Unity APIs.
using System;
using System.Net.Sockets;
using System.Threading;

public enum WatcherEvent {
  None,
  TransportOk,
  GatewayDown,
  TransportLost,
  PhoneUp,
  PhoneAudio,
  PhoneStopped,
  PhoneDown
}

public sealed class PhonePresenceWatcher : IDisposable {
  const byte KindAudio = 1;
  const byte KindStop = 2;
  const byte KindError = 3;
  const byte KindHello = 4;
  const byte KindPresenceUp = 5;
  const byte KindPresenceDown = 6;
  const byte KindSubscribe = 0x10;

  readonly string _host;
  readonly int _port;
  Thread _thread;
  volatile bool _stop;
  TcpClient _client; // worker-thread owned; Stop() closes it to unblock reads
  bool _disposed;

  readonly object _mutex = new object();
  WatcherEvent _lastEvent = WatcherEvent.None;
  int _eventSeq;
  uint _lastSerial;
  string _lastReason = string.Empty;
  long _upCount;
  long _audioCount;
  long _stopCount;
  long _downCount;

  public PhonePresenceWatcher(string host, int port) {
    _host = string.IsNullOrEmpty(host) ? "127.0.0.1" : host;
    _port = port > 0 ? port : 8451;
  }

  public bool IsRunning {
    get {
      try { return _thread != null && _thread.IsAlive; } catch (Exception) { return false; }
    }
  }

  public void Start() {
    try {
      if (IsRunning) return;
      _stop = false;
      _thread = new Thread(Loop) { IsBackground = true, Name = "PhonePresence" };
      _thread.Start();
    } catch (Exception) { }
  }

  public void Stop() {
    try { _stop = true; } catch (Exception) { }
    try {
      TcpClient c = _client;
      if (c != null) { try { c.Close(); } catch (Exception) { } }
    } catch (Exception) { }
    try {
      Thread t = _thread;
      if (t != null && t.IsAlive) t.Join(3000);
    } catch (Exception) { }
    _thread = null;
  }

  // Main-thread snapshot. seq changes on every edge; the driver applies the
  // latest edge when seq moves and ignores the rest (burst collapse).
  public void ReadState(out int seq, out WatcherEvent ev, out uint serial, out string reason) {
    lock (_mutex) {
      seq = _eventSeq;
      ev = _lastEvent;
      serial = _lastSerial;
      reason = _lastReason;
    }
  }

  public void ReadCounters(out long up, out long audio, out long stop, out long down) {
    lock (_mutex) {
      up = _upCount;
      audio = _audioCount;
      stop = _stopCount;
      down = _downCount;
    }
  }

  void Post(WatcherEvent ev, uint serial, string reason) {
    lock (_mutex) {
      _lastEvent = ev;
      _eventSeq++;
      _lastSerial = serial;
      _lastReason = reason ?? string.Empty;
      switch (ev) {
        case WatcherEvent.PhoneUp: _upCount++; break;
        case WatcherEvent.PhoneAudio: _audioCount++; break;
        case WatcherEvent.PhoneStopped: _stopCount++; break;
        case WatcherEvent.PhoneDown: _downCount++; break;
      }
    }
  }

  void Loop() {
    while (!_stop) {
      TcpClient c = TryConnect();
      if (c == null || _stop) {
        if (!_stop) {
          Post(WatcherEvent.GatewayDown, 0, string.Empty);
          SleepOrStop(2000);
        }
        continue;
      }
      _client = c;
      try {
        NetworkStream s = c.GetStream();
        s.WriteTimeout = 1500;
        byte[] sub = EncodeFrame(KindSubscribe, 0, 0, null);
        s.Write(sub, 0, sub.Length);
        c.ReceiveTimeout = 2000;
        Post(WatcherEvent.TransportOk, 0, string.Empty);
        while (!_stop) {
          Decoded? f = ReadFrame(s);
          if (!f.HasValue) continue; // quiet slice (timeout) or malformed: normal
          switch (f.Value.Kind) {
            case KindAudio:
              Post(WatcherEvent.PhoneAudio, f.Value.Serial, string.Empty);
              break;
            case KindStop:
              Post(WatcherEvent.PhoneStopped, f.Value.Serial, string.Empty);
              break;
            case KindPresenceUp:
              Post(WatcherEvent.PhoneUp, 0, Utf8(f.Value.Payload));
              break;
            case KindPresenceDown:
              Post(WatcherEvent.PhoneDown, 0, Utf8(f.Value.Payload));
              break;
            default:
              break; // HELLO/ERROR/SUBSCRIBE-echo: no edge (DOWN follows real losses)
          }
        }
      } catch (Exception) {
        if (!_stop) Post(WatcherEvent.TransportLost, 0, string.Empty);
      } finally {
        try { c.Close(); } catch (Exception) { }
        _client = null;
      }
      if (!_stop) SleepOrStop(1500);
    }
  }

  TcpClient TryConnect() {
    TcpClient c = null;
    try {
      c = new TcpClient();
      IAsyncResult ar = c.BeginConnect(_host, _port, null, null);
      if (!ar.AsyncWaitHandle.WaitOne(1500)) {
        try { c.Close(); } catch (Exception) { }
        return null;
      }
      try { c.EndConnect(ar); } catch (Exception) {
        try { c.Close(); } catch (Exception) { }
        return null;
      }
      if (!c.Connected) {
        try { c.Close(); } catch (Exception) { }
        return null;
      }
      return c;
    } catch (Exception) {
      try { if (c != null) c.Close(); } catch (Exception) { }
      return null;
    }
  }

  void SleepOrStop(int ms) {
    try {
      int waited = 0;
      while (!_stop && waited < ms) {
        Thread.Sleep(100);
        waited += 100;
      }
    } catch (Exception) { }
  }

  struct Decoded {
    public byte Kind;
    public uint Serial;
    public byte[] Payload;
  }

  // Null = quiet slice (read timeout) or malformed (ignored). Throws only on
  // orderly shutdown / hard socket failure (caller maps to TransportLost).
  static Decoded? ReadFrame(NetworkStream s) {
    byte[] len;
    try {
      len = ReadExact(s, 4);
    } catch (IOExceptionWithTimeout) {
      return null;
    }
    if (len == null) throw new Exception("closed");
    int bodyLen = (len[0] << 24) | (len[1] << 16) | (len[2] << 8) | len[3];
    if (bodyLen < 9 || bodyLen > 4 + 9 + 65535) {
      // Misaligned stream (should not happen after the HELLO-first fix):
      // do NOT kill the watcher; treat the window as quiet.
      return null;
    }
    byte[] body;
    try {
      body = ReadExact(s, bodyLen);
    } catch (IOExceptionWithTimeout) {
      return null;
    }
    if (body == null) throw new Exception("closed");
    byte kind = body[0];
    switch (kind) {
      case KindAudio:
      case KindStop:
      case KindError:
      case KindHello:
      case KindPresenceUp:
      case KindPresenceDown:
        break;
      default:
        return null;
    }
    uint serial = ((uint)body[1] << 24) | ((uint)body[2] << 16)
      | ((uint)body[3] << 8) | body[4];
    var payload = new byte[Math.Max(0, bodyLen - 9)];
    if (payload.Length > 0) Buffer.BlockCopy(body, 9, payload, 0, payload.Length);
    return new Decoded { Kind = kind, Serial = serial, Payload = payload };
  }

  // Distinguishes read TIMEOUT (quiet: null) from real failure (null + flag).
  sealed class IOExceptionWithTimeout : Exception { }

  static byte[] ReadExact(NetworkStream s, int count) {
    var buf = new byte[count];
    int got = 0;
    while (got < count) {
      int n;
      try {
        n = s.Read(buf, got, count - got);
      } catch (System.IO.IOException e) {
        // ReceiveTimeout surfaces as IOException(TimedOut). Quiet slice.
        try {
          var se = e.InnerException as SocketException;
          if (se != null && se.SocketErrorCode == SocketError.TimedOut)
            throw new IOExceptionWithTimeout();
        } catch (IOExceptionWithTimeout) { throw; } catch (Exception) { }
        return null;
      } catch (Exception) {
        return null;
      }
      if (n <= 0) return null; // orderly shutdown
      got += n;
    }
    return buf;
  }

  static string Utf8(byte[] payload) {
    if (payload == null || payload.Length == 0) return string.Empty;
    try { return System.Text.Encoding.UTF8.GetString(payload); } catch (Exception) { return string.Empty; }
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

  public void Dispose() {
    if (_disposed) return;
    _disposed = true;
    try { Stop(); } catch (Exception) { }
  }
}
