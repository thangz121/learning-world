// _SharedKernel/PhoneCameraWatcher.cs — Lead owns. Phase 2.2 camera link monitor.
// Pure C# (NO UnityEngine; System.Net.Sockets only). Structural mirror of the
// PROVEN PhonePresenceWatcher (Phase 2.1), pointed at the SEPARATE camera
// bridge port (default 8452). Same threading contract: a background thread owns
// the TcpClient; the driver (main thread) polls ReadState() and applies the
// LATEST edge only. Rapid FRAME bursts collapse harmlessly; a DOWN after them
// still wins. Frames are posted into the shared CameraFrameSource (depth ONE);
// the watcher itself holds no frame bytes after posting.
//
// Media independence (§8): this thread/socket/queue is fully disjoint from the
// mic watcher. A camera flood can delay camera edges only — mic capture runs
// on its own socket and is never touched here.
//
// Never throws out of public methods; never touches Unity APIs; never writes
// disk (privacy §11: bytes live in the bounded RAM slot only).
using System;
using System.Net.Sockets;
using System.Threading;

public enum CameraWatcherEvent {
  None,
  TransportOk,
  GatewayDown,
  TransportLost,
  CameraUp,      // phone camera page opened (WS up, before any START)
  CameraFrame,   // a JPEG frame was accepted into the source slot
  CameraStopped, // user pressed STOP (page may stay open; NOT a drop)
  CameraDown,    // phone page GONE (WS closed/dropped) — authoritative
  CameraError    // gateway rejected the session (denied/unavailable/...)
}

public sealed class PhoneCameraWatcher : IDisposable {
  readonly string _host;
  readonly int _port;
  readonly CameraFrameSource _source;
  Thread _thread;
  volatile bool _stop;
  TcpClient _client; // worker-thread owned; Stop() closes it to unblock reads
  bool _disposed;

  readonly object _mutex = new object();
  CameraWatcherEvent _lastEvent = CameraWatcherEvent.None;
  int _eventSeq;
  uint _lastSerial;
  string _lastReason = string.Empty;
  long _upCount;
  long _frameCount;
  long _stopCount;
  long _downCount;
  long _frameBytes;
  int _lastFrameTick = -1;

  public PhoneCameraWatcher(string host, int port, CameraFrameSource source) {
    _host = string.IsNullOrEmpty(host) ? PhoneCameraProtocol.LoopbackHost : host;
    _port = port > 0 ? port : PhoneCameraProtocol.DefaultBridgePort;
    _source = source ?? new CameraFrameSource();
  }

  public CameraFrameSource Source => _source;

  public bool IsRunning {
    get {
      try { return _thread != null && _thread.IsAlive; } catch (Exception) { return false; }
    }
  }

  public void Start() {
    try {
      if (IsRunning) return;
      _stop = false;
      _thread = new Thread(Loop) { IsBackground = true, Name = "PhoneCamera" };
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

  public void ReadState(out int seq, out CameraWatcherEvent ev, out uint serial, out string reason) {
    lock (_mutex) {
      seq = _eventSeq;
      ev = _lastEvent;
      serial = _lastSerial;
      reason = _lastReason;
    }
  }

  public void ReadCounters(out long up, out long frames, out long stop, out long down) {
    lock (_mutex) {
      up = _upCount;
      frames = _frameCount;
      stop = _stopCount;
      down = _downCount;
    }
  }

  // Freshness of the last accepted frame (green LIVE evidence vs stale red).
  public void ReadFrameStats(out long frames, out long bytes, out int ageMs) {
    lock (_mutex) {
      frames = _frameCount;
      bytes = _frameBytes;
      ageMs = _lastFrameTick < 0 ? -1
        : unchecked(Environment.TickCount - _lastFrameTick);
    }
  }

  void Post(CameraWatcherEvent ev, uint serial, string reason) {
    lock (_mutex) {
      _lastEvent = ev;
      _eventSeq++;
      _lastSerial = serial;
      _lastReason = reason ?? string.Empty;
      switch (ev) {
        case CameraWatcherEvent.CameraUp: _upCount++; break;
        case CameraWatcherEvent.CameraFrame: _frameCount++; break;
        case CameraWatcherEvent.CameraStopped: _stopCount++; break;
        case CameraWatcherEvent.CameraDown: _downCount++; break;
      }
    }
  }

  void RecordFrameBytes(int n) {
    lock (_mutex) {
      _frameBytes += Math.Max(0, n);
      try { _lastFrameTick = Environment.TickCount; } catch (Exception) { }
    }
  }

  void Loop() {
    while (!_stop) {
      TcpClient c = TryConnect();
      if (c == null || _stop) {
        if (!_stop) {
          Post(CameraWatcherEvent.GatewayDown, 0, string.Empty);
          SleepOrStop(2000);
        }
        continue;
      }
      _client = c;
      try {
        NetworkStream s = c.GetStream();
        s.WriteTimeout = 1500;
        byte[] sub = PhoneCameraProtocol.EncodeBridgeFrame(
          PhoneCameraProtocol.KindSubscribe, 0, 0, null);
        s.Write(sub, 0, sub.Length);
        c.ReceiveTimeout = 2000;
        Post(CameraWatcherEvent.TransportOk, 0, string.Empty);
        while (!_stop) {
          Decoded? f = ReadFrame(s);
          if (!f.HasValue) continue; // quiet slice or malformed: normal
          switch (f.Value.Kind) {
            case PhoneCameraProtocol.KindFrame:
              // Session + validity enforced inside the source (foreign/invalid
              // counted there); the edge fires only on ACCEPT so the service
              // never wakes for bytes it cannot show.
              if (_source.PostFrame(f.Value.Serial, f.Value.Seq, f.Value.Payload)) {
                RecordFrameBytes(f.Value.Payload != null ? f.Value.Payload.Length : 0);
                Post(CameraWatcherEvent.CameraFrame, f.Value.Serial, string.Empty);
              }
              break;
            case PhoneCameraProtocol.KindStop:
              _source.OnStopped(f.Value.Serial);
              Post(CameraWatcherEvent.CameraStopped, f.Value.Serial, string.Empty);
              break;
            case PhoneCameraProtocol.KindError:
              _source.OnError(PhoneCameraProtocol.Utf8(f.Value.Payload));
              Post(CameraWatcherEvent.CameraError, f.Value.Serial,
                PhoneCameraProtocol.Utf8(f.Value.Payload));
              break;
            case PhoneCameraProtocol.KindUp:
              Post(CameraWatcherEvent.CameraUp, 0, PhoneCameraProtocol.Utf8(f.Value.Payload));
              break;
            case PhoneCameraProtocol.KindDown:
              _source.ResetSession(); // §20: old bytes never survive a reconnect
              Post(CameraWatcherEvent.CameraDown, 0, PhoneCameraProtocol.Utf8(f.Value.Payload));
              break;
            default:
              break; // HELLO/SUBSCRIBE-echo: no edge
          }
        }
      } catch (Exception) {
        if (!_stop) Post(CameraWatcherEvent.TransportLost, 0, string.Empty);
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
    public uint Seq;
    public byte[] Payload;
  }

  static Decoded? ReadFrame(NetworkStream s) {
    byte[] len;
    try {
      len = ReadExact(s, 4);
    } catch (IOExceptionWithTimeout) {
      return null;
    }
    if (len == null) throw new Exception("closed");
    int bodyLen = (len[0] << 24) | (len[1] << 16) | (len[2] << 8) | len[3];
    if (bodyLen < PhoneCameraProtocol.HeaderBytes
        || bodyLen > PhoneCameraProtocol.MaxFrameBytes - 4) {
      return null; // misaligned window: stay alive, treat as quiet
    }
    byte[] body;
    try {
      body = ReadExact(s, bodyLen);
    } catch (IOExceptionWithTimeout) {
      return null;
    }
    if (body == null) throw new Exception("closed");
    PhoneCameraProtocol.BridgeFrame f;
    string reason;
    var raw = new byte[4 + bodyLen];
    Buffer.BlockCopy(len, 0, raw, 0, 4);
    Buffer.BlockCopy(body, 0, raw, 4, bodyLen);
    if (!PhoneCameraProtocol.TryDecodeBridgeFrame(raw, 0, raw.Length, out f, out reason))
      return null;
    return new Decoded { Kind = f.Kind, Serial = f.SessionSerial, Seq = f.Seq, Payload = f.Payload };
  }

  sealed class IOExceptionWithTimeout : Exception { }

  static byte[] ReadExact(NetworkStream s, int count) {
    var buf = new byte[count];
    int got = 0;
    while (got < count) {
      int n;
      try {
        n = s.Read(buf, got, count - got);
      } catch (System.IO.IOException e) {
        try {
          var se = e.InnerException as SocketException;
          if (se != null && se.SocketErrorCode == SocketError.TimedOut)
            throw new IOExceptionWithTimeout();
        } catch (IOExceptionWithTimeout) { throw; } catch (Exception) { }
        return null;
      } catch (Exception) {
        return null;
      }
      if (n <= 0) return null;
      got += n;
    }
    return buf;
  }

  public void Dispose() {
    if (_disposed) return;
    _disposed = true;
    try { Stop(); } catch (Exception) { }
  }
}
