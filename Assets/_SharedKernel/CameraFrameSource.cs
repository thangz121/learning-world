// _SharedKernel/CameraFrameSource.cs — Lead owns. Phase 2.2 latest-frame store.
// Pure C# (NO UnityEngine). The bounded/backpressure contract (§5):
//
//   NEW FRAME -> replace stale frame -> GAME consumes newest available frame.
//
// Queue depth is EXACTLY ONE: intermediate frames are dropped by design (a face
// preview shows "the latest face", never "every frame ever received"). There is
// NO growth path: no list, no queue, one byte[] slot. Memory is bounded by
// PhoneCameraConfig.MaxFrameBytes regardless of how fast the phone produces.
//
// Session isolation (§20, same pattern as NetworkMicrophoneCapture): the first
// sessionSerial posted after Reset latches; foreign serials are dropped and
// counted, never merged — an old session can never contaminate a reconnect.
// STOP clears the slot (the HUD must never show a frozen old face, §10).
// Staleness (§10): IsStale() tells the service when to leave Live; the slot
// itself is kept for stats but never presented as live past the threshold.
//
// Threading: the watcher posts from its worker thread; the game consumes on
// the main thread. All state is lock-guarded. Never throws. No disk, no
// recording, no analysis — transport + display only (§11/§12).
using System;

public struct CameraSourceStats {
  public long ReceivedFrames;      // accepted (latched session, valid JPEG)
  public long DroppedForeign;      // wrong sessionSerial (§20)
  public long DroppedInvalid;      // empty / oversize / non-JPEG
  public long ReplacedUnread;      // newer frame replaced one never displayed
  public long DisplayedFrames;     // consumed via TryTakeLatest
  public long DecodeFailures;      // service-side JPEG decode failures
  public long StopCount;
  public long ErrorCount;
}

public sealed class CameraFrameSource {
  readonly object _mutex = new object();
  readonly int _maxBytes;

  bool _latched;
  uint _serial;
  uint _lastSeq;
  byte[] _latest;          // the ONE slot (null = none)
  int _latestTick;         // Environment.TickCount at post (age base)
  bool _latestConsumed = true;
  string _lastError = string.Empty;
  CameraSourceStats _stats;

  public CameraFrameSource() : this(PhoneCameraConfig.Default.MaxFrameBytes) { }

  public CameraFrameSource(int maxBytes) {
    _maxBytes = maxBytes > 0 ? maxBytes : PhoneCameraConfig.Default.MaxFrameBytes;
  }

  // Worker-thread post. Returns true when accepted into the slot.
  public bool PostFrame(uint serial, uint seq, byte[] jpeg) {
    try {
      string reason;
      if (!PhoneCameraProtocol.IsValidFramePayload(jpeg, _maxBytes, out reason)) {
        lock (_mutex) {
          CameraSourceStats s = _stats;
          s.DroppedInvalid++;
          _stats = s;
        }
        return false;
      }
      lock (_mutex) {
        if (!_latched) {
          _latched = true;
          _serial = serial;
          _lastSeq = seq;
        } else if (serial != _serial) {
          CameraSourceStats s = _stats;
          s.DroppedForeign++;
          _stats = s;
          return false;
        } else {
          _lastSeq = seq;
        }
        if (_latest != null && !_latestConsumed) {
          CameraSourceStats s = _stats;
          s.ReplacedUnread++;
          _stats = s;
        }
        _latest = jpeg; // slot replace: caller must not mutate after post
        try { _latestTick = Environment.TickCount; } catch (Exception) { _latestTick = 0; }
        _latestConsumed = false;
        {
          CameraSourceStats s = _stats;
          s.ReceivedFrames++;
          _stats = s;
        }
        return true;
      }
    } catch (Exception) { return false; }
  }

  // Main-thread consume: copies the reference out and marks it read. The
  // service decodes immediately; the slot keeps the bytes until replaced.
  public bool TryTakeLatest(out uint serial, out uint seq, out byte[] jpeg, out int ageMs) {
    serial = 0;
    seq = 0;
    jpeg = null;
    ageMs = -1;
    try {
      lock (_mutex) {
        if (_latest == null) return false;
        serial = _serial;
        seq = _lastSeq;
        jpeg = _latest;
        int now = 0;
        try { now = Environment.TickCount; } catch (Exception) { }
        ageMs = _latestTick == 0 ? 0 : unchecked(now - _latestTick);
        if (!_latestConsumed) {
          _latestConsumed = true;
          CameraSourceStats s = _stats;
          s.DisplayedFrames++;
          _stats = s;
        }
        return true;
      }
    } catch (Exception) { return false; }
  }

  // Peek without consuming (Phase 2.3 recording tap + state machine
  // freshness checks). Unlike TryTakeLatest this never marks the slot read,
  // so the display path's DisplayedFrames accounting is untouched no matter
  // how often the recorder samples. Returns the latest game-ACCEPTED frame
  // (session-latched, JPEG-validated) — the recording boundary (§3).
  public bool TryPeekLatest(out uint serial, out uint seq, out byte[] jpeg, out int ageMs) {
    serial = 0;
    seq = 0;
    jpeg = null;
    ageMs = -1;
    try {
      lock (_mutex) {
        if (_latest == null) return false;
        serial = _serial;
        seq = _lastSeq;
        jpeg = _latest;
        int now = 0;
        try { now = Environment.TickCount; } catch (Exception) { }
        ageMs = _latestTick == 0 ? 0 : unchecked(now - _latestTick);
        return true;
      }
    } catch (Exception) { return false; }
  }

  // Peek without consuming (state machine freshness checks).
  public bool HasFreshFrame(int staleMs, out int ageMs) {
    ageMs = -1;
    try {
      lock (_mutex) {
        if (_latest == null) return false;
        int now = 0;
        try { now = Environment.TickCount; } catch (Exception) { }
        ageMs = _latestTick == 0 ? 0 : unchecked(now - _latestTick);
        return ageMs >= 0 && ageMs <= staleMs;
      }
    } catch (Exception) { return false; }
  }

  public void OnStopped(uint serial) {
    try {
      lock (_mutex) {
        CameraSourceStats s = _stats;
        s.StopCount++;
        _stats = s;
        if (_latched && serial != _serial) {
          s = _stats;
          s.DroppedForeign++;
          _stats = s;
          return;
        }
        _latest = null; // §10: never a frozen old face after STOP
        _latestConsumed = true;
      }
    } catch (Exception) { }
  }

  public void OnError(string reason) {
    try {
      lock (_mutex) {
        _lastError = reason ?? "error";
        CameraSourceStats s = _stats;
        s.ErrorCount++;
        _stats = s;
        _latest = null;
        _latestConsumed = true;
      }
    } catch (Exception) { }
  }

  public void ReportDecodeFailure() {
    try {
      lock (_mutex) {
        CameraSourceStats s = _stats;
        s.DecodeFailures++;
        _stats = s;
      }
    } catch (Exception) { }
  }

  // New session epoch (§20): latch + slot cleared so stale bytes can never
  // appear after a reconnect. Called on DOWN / transport loss / service start.
  public void ResetSession() {
    try {
      lock (_mutex) {
        _latched = false;
        _serial = 0;
        _latest = null;
        _latestConsumed = true;
      }
    } catch (Exception) { }
  }

  public void ReadStats(out CameraSourceStats stats) {
    CameraSourceStats s;
    lock (_mutex) { s = _stats; }
    stats = s;
  }

  public string LastError {
    get { try { lock (_mutex) { return _lastError; } } catch (Exception) { return string.Empty; } }
  }

  public bool IsLatched {
    get { try { lock (_mutex) { return _latched; } } catch (Exception) { return false; } }
  }
}
