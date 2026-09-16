// A_World/GameCameraStreamService.cs — Agent A (World & Visual).
// Phase 2.2 game-side camera stream: PC receiver -> frame source -> decode ->
// UI-consumable texture. The UI (PhoneCameraHud) knows ONLY "latest frame":
// it never sees sockets, sessions, reconnects, or JPEG bytes.
//
//   PhoneCameraWatcher (worker thread, port 8452)
//       -> CameraFrameSource (depth-ONE slot, session latched)
//       -> this service (main thread: edge poll + JPEG decode + state)
//       -> CurrentTexture (single reused Texture2D, null unless Live)
//
// Reuse (§0): lifecycle/session/presence/disconnect patterns mirror the mic
// path (MicSetupMonitor + PhonePresenceWatcher); the SOCKET, THREAD, QUEUE and
// SESSION SPACE are fully independent (§8) so camera can never starve speech.
// Decode reuses ONE Texture2D via LoadImage (§18: no per-frame allocation,
// no readbacks, no conversions beyond the unavoidable JPEG decode).
// Privacy (§11): RAM only — no recording, no file, no upload, no analysis.
using System;
using System.Diagnostics;
using UnityEngine;

[DisallowMultipleComponent]
public class GameCameraStreamService : MonoBehaviour {
  string _host = PhoneCameraProtocol.LoopbackHost;
  int _port = PhoneCameraProtocol.DefaultBridgePort;
  PhoneCameraConfig _config = PhoneCameraConfig.Default;

  CameraFrameSource _source;
  PhoneCameraWatcher _watcher;
  bool _running;
  bool _transportOk;
  bool _sawUp;
  string _lastError = string.Empty;
  PhoneCameraState _state = PhoneCameraState.Disabled;
  int _appliedSeq;

  Texture2D _texture; // the ONE reused texture (§18)
  long _displayedCount;
  long _decodeFailures;
  float _lastDecodeMs;
  float _fpsEstimate;
  int _lastDisplayTick;
  readonly int[] _displayTicks = new int[10];
  int _displayTickCount;
  PhoneCameraState _lastLoggedState = PhoneCameraState.Disabled;
  bool _logArmed = true;

  // Source-precedence (game -> gateway -> phone START UI, per-medium): the
  // game prefers ANY plugged-in PC webcam over the phone (generic count, no
  // names — names are labels only). Reported on TRANSITIONS only via the cam
  // bridge (8452), fire-and-forget (TCP dials block). Game-side precedence
  // applies regardless — this only stands the phone START button down.
  // -e2e-nocam forces phone (E2E hook, mirrors -e2e-nomic for mics).
  System.Func<bool> _localCamPresentFn;
  bool _forcePhoneCam;
  string _lastReportedCamPrefer;
  float _camPreferPollSec = 5f;
  float _camPreferTimer;

  public PhoneCameraState State => _state;
  public bool IsRunning => _running;
  public Texture2D CurrentTexture => _state == PhoneCameraState.Live ? _texture : null;
  public bool HasLiveTexture => _state == PhoneCameraState.Live && _texture != null;

  // Injection boundary (wired by MarketBootstrap; all optional).
  public void Bind(string host, int port, PhoneCameraConfig config) {
    if (!string.IsNullOrEmpty(host)) _host = host;
    if (port > 0) _port = port;
    string reason;
    if (config.Validate(out reason)) _config = config;
  }

  // Test seam: inject scripted local-webcam presence (generic bool, no names).
  // Null restores the default (WebCamTexture.devices count).
  public void SetLocalCamProbeForTests(System.Func<bool> fn) {
    _localCamPresentFn = fn;
  }

  // E2E hook: -e2e-nocam forces phone even with a local webcam present.
  public void SetForcePhoneForTests(bool force) {
    _forcePhoneCam = force;
  }

  bool HasLocalCam() {
    try {
      if (_forcePhoneCam) return false;
      try {
        foreach (string a in System.Environment.GetCommandLineArgs())
          if (string.Equals(a, "-e2e-nocam", System.StringComparison.OrdinalIgnoreCase)) return false;
      } catch (Exception) { }
      if (_localCamPresentFn != null) {
        try { return _localCamPresentFn(); } catch (Exception) { return false; }
      }
      // Generic presence (names are labels only): ANY usable local camera —
      // integrated laptop cam or USB webcam — keeps the phone stood down.
      // Ranking (which one the game SHOWS) lives in LocalCameraClassifier.
      try {
        var devs = UnityEngine.WebCamTexture.devices;
        if (devs == null) return false;
        var names = new string[devs.Length];
        for (int i = 0; i < devs.Length; i++) names[i] = devs[i].name;
        return LocalCameraClassifier.HasUsableCamera(names);
      } catch (Exception) { return false; }
    } catch (Exception) { return false; }
  }

  void ReportCamPreferIfChanged() {
    try {
      string prefer = PcSourcePrecedence.PreferCam(HasLocalCam(), false);
      // forcePhone already folded into HasLocalCam (false when forced), so
      // the payload stays honest ("cam:phone" when forced).
      if (string.Equals(prefer, _lastReportedCamPrefer, StringComparison.Ordinal)) return;
      _lastReportedCamPrefer = prefer;
      string host = _host;
      int port = _port;
      try {
        System.Threading.Tasks.Task.Run(() => {
          try { PcPreferenceReporter.Report(host, port, prefer, false); } catch (Exception) { }
        });
      } catch (Exception) { }
    } catch (Exception) { }
  }

  public void StartService() {
    try {
      if (_running) return;
      _source = new CameraFrameSource(_config.MaxFrameBytes);
      _source.ResetSession();
      _watcher = new PhoneCameraWatcher(_host, _port, _source);
      _watcher.Start();
      _running = true;
      _transportOk = false;
      _sawUp = false;
      _lastError = string.Empty;
      _appliedSeq = 0;
      _state = PhoneCameraState.WaitingForPhone;
      _camPreferTimer = _camPreferPollSec;
      try { ReportCamPreferIfChanged(); } catch (Exception) { }
    } catch (Exception) { _state = PhoneCameraState.Error; }
  }

  public void StopService() {
    try {
      _running = false;
      try { if (_watcher != null) _watcher.Dispose(); } catch (Exception) { }
      _watcher = null;
      try { if (_source != null) _source.ResetSession(); } catch (Exception) { }
      _transportOk = false;
      _sawUp = false;
      _state = PhoneCameraState.Stopped;
    } catch (Exception) { _state = PhoneCameraState.Stopped; }
  }

  void OnDestroy() {
    try { StopService(); } catch (Exception) { }
    try { if (_texture != null) Destroy(_texture); } catch (Exception) { }
    _texture = null;
  }

  void Update() {
    if (!_running) return;
    try {
      DrainWatcherEdges();
      RefreshLiveness();
      // Source-precedence poll (cheap bool, 5 s): USB webcam plugged/unplugged
      // flips the phone START UI via the gateway. Transitions only.
      try {
        _camPreferTimer -= UnityEngine.Time.deltaTime;
        if (_camPreferTimer <= 0f) {
          _camPreferTimer = _camPreferPollSec;
          ReportCamPreferIfChanged();
        }
      } catch (Exception) { }
      // One line per STATE TRANSITION only (R10: never per-frame spam).
      // Lets Player.log prove Live vs waiting vs lost without a debugger.
      if (_logArmed && _state != _lastLoggedState) {
        _lastLoggedState = _state;
        UnityEngine.Debug.Log("[PhoneCamera] " + StatusLine());
      }
    } catch (Exception) { _state = PhoneCameraState.Error; }
  }

  void DrainWatcherEdges() {
    if (_watcher == null) return;
    int seq;
    CameraWatcherEvent ev;
    uint serial;
    string reason;
    try { _watcher.ReadState(out seq, out ev, out serial, out reason); }
    catch (Exception) { return; }
    if (seq == _appliedSeq) return;
    _appliedSeq = seq;
    ApplyEdge(ev, serial, reason);
  }

  // Single-edge application (latest-wins collapse happens in the watcher).
  // Public for tests (no sockets needed).
  public void ApplyEdgeForTests(CameraWatcherEvent ev, uint serial, string reason) {
    ApplyEdge(ev, serial, reason);
    RefreshLiveness();
  }

  void ApplyEdge(CameraWatcherEvent ev, uint serial, string reason) {
    switch (ev) {
      case CameraWatcherEvent.TransportOk:
        _transportOk = true;
        if (_state == PhoneCameraState.WaitingForPhone)
          _state = PhoneCameraState.Connecting;
        break;
      case CameraWatcherEvent.GatewayDown:
      case CameraWatcherEvent.TransportLost:
        _transportOk = false;
        _sawUp = false;
        try { if (_source != null) _source.ResetSession(); } catch (Exception) { }
        _state = PhoneCameraState.TempDisconnected;
        break;
      case CameraWatcherEvent.CameraUp:
        _transportOk = true;
        _sawUp = true;
        if (_state != PhoneCameraState.Live)
          _state = PhoneCameraState.ConnectedWaitingFrames;
        break;
      case CameraWatcherEvent.CameraFrame:
        _transportOk = true;
        _sawUp = true;
        DecodeLatest();
        break;
      case CameraWatcherEvent.CameraStopped:
        // Explicit STOP with the page possibly still open: idle, NOT a drop.
        // The source already cleared the slot (no frozen face, §10).
        if (_state == PhoneCameraState.Live || _state == PhoneCameraState.ConnectedWaitingFrames)
          _state = PhoneCameraState.ConnectedWaitingFrames;
        break;
      case CameraWatcherEvent.CameraDown:
        _sawUp = false;
        try { if (_source != null) _source.ResetSession(); } catch (Exception) { }
        _state = PhoneCameraState.TempDisconnected;
        break;
      case CameraWatcherEvent.CameraError:
        _lastError = reason ?? "camera_error";
        _state = PhoneCameraState.Error;
        break;
      default:
        break; // None: nothing to apply
    }
  }

  // Freshness gate (§10): Live claims require a frame within the stale window.
  // Anything else falls back to the best honest non-live state — never a frozen
  // old face presented as live.
  void RefreshLiveness() {
    if (!_running) { _state = PhoneCameraState.Stopped; return; }
    if (_state == PhoneCameraState.Error || _state == PhoneCameraState.TempDisconnected) {
      // Recovery check: a fresh frame below still promotes to Live (the next
      // watcher edge decodes and sets Live directly; this only re-honests a
      // stale Live that lost its edge race).
      if (_state == PhoneCameraState.TempDisconnected && _transportOk && _sawUp
          && HasFresh()) {
        _state = PhoneCameraState.Live;
      }
      return;
    }
    if (HasFresh()) { _state = PhoneCameraState.Live; return; }
    if (_state == PhoneCameraState.Live) {
      // The frame we showed went stale: honest downgrade, texture hidden by
      // the CurrentTexture gate (object kept for reuse, never shown stale).
      _state = _transportOk ? PhoneCameraState.ConnectedWaitingFrames
        : PhoneCameraState.TempDisconnected;
      return;
    }
    if (_state == PhoneCameraState.Disabled || _state == PhoneCameraState.Stopped) return;
    if (_sawUp || _transportOk) {
      if (_state == PhoneCameraState.WaitingForPhone) _state = PhoneCameraState.Connecting;
      else if (_state != PhoneCameraState.ConnectedWaitingFrames
          && _state != PhoneCameraState.Connecting) _state = PhoneCameraState.ConnectedWaitingFrames;
    }
  }

  bool HasFresh() {
    try {
      if (_source == null) return false;
      int age;
      return _source.HasFreshFrame(_config.StaleMs, out age);
    } catch (Exception) { return false; }
  }

  // Decode-on-edge only (§18): one JPEG -> the ONE texture. Failure keeps the
  // previous texture hidden (state falls out of Live via staleness) and counts.
  // PC overload is impossible by construction: Update drains at most ONE edge
  // per rendered frame, so decode rate <= display rate no matter how fast the
  // phone produces (60fps-capped phone grabs only ever replace the depth-ONE
  // slot).
  void DecodeLatest() {
    if (_source == null) return;
    uint serial, seq;
    byte[] jpeg;
    int age;
    try {
      if (!_source.TryTakeLatest(out serial, out seq, out jpeg, out age)) return;
    } catch (Exception) { return; }
    if (jpeg == null || jpeg.Length == 0) return;
    Stopwatch sw = null;
    try { sw = Stopwatch.StartNew(); } catch (Exception) { }
    bool ok = false;
    try {
      if (_texture == null) {
        _texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        _texture.wrapMode = TextureWrapMode.Clamp;
      }
      ok = _texture.LoadImage(jpeg);
    } catch (Exception) { ok = false; }
    try { if (sw != null) { sw.Stop(); _lastDecodeMs = (float)sw.Elapsed.TotalMilliseconds; } }
    catch (Exception) { }
    if (ok) {
      _displayedCount++;
      StampDisplayTick();
      _state = PhoneCameraState.Live;
    } else {
      _decodeFailures++;
      try { if (_source != null) _source.ReportDecodeFailure(); } catch (Exception) { }
    }
  }

  void StampDisplayTick() {
    try {
      int now = Environment.TickCount;
      _lastDisplayTick = now;
      _displayTicks[_displayTickCount % _displayTicks.Length] = now;
      _displayTickCount++;
      if (_displayTickCount >= 3) {
        int oldest = _displayTicks[(_displayTickCount - Math.Min(_displayTickCount, 10)) % _displayTicks.Length];
        int span = unchecked(now - oldest);
        int n = Math.Min(_displayTickCount, 10) - 1;
        _fpsEstimate = span > 0 && n > 0 ? (n * 1000f) / span : 0f;
      }
    } catch (Exception) { }
  }

  // Test seam: drive decode deterministically with caller-made JPEG bytes.
  public bool DecodeForTests(byte[] jpeg) {
    if (_source == null) _source = new CameraFrameSource(_config.MaxFrameBytes);
    if (!_source.PostFrame(1, 0, jpeg)) return false;
    DecodeLatest();
    return _state == PhoneCameraState.Live;
  }

  public string StatusLine() {
    try {
      CameraSourceStats s;
      if (_source != null) _source.ReadStats(out s);
      else s = new CameraSourceStats();
      return string.Format("cam {0} rx={1} shown={2} dropF={3} dropI={4} decFail={5} decMs={6:F1} fps~{7:F1}{8}",
        _state, s.ReceivedFrames, _displayedCount, s.DroppedForeign, s.DroppedInvalid,
        _decodeFailures, _lastDecodeMs, _fpsEstimate,
        string.IsNullOrEmpty(_lastError) ? "" : " err=" + _lastError);
    } catch (Exception) { return "cam " + _state; }
  }

  public void ReadServiceStats(out long received, out long displayed, out long decodeFailures,
      out float decodeMs, out float fps) {
    received = 0;
    displayed = _displayedCount;
    decodeFailures = _decodeFailures;
    decodeMs = _lastDecodeMs;
    fps = _fpsEstimate;
    try {
      if (_source != null) {
        CameraSourceStats s;
        _source.ReadStats(out s);
        received = s.ReceivedFrames;
      }
    } catch (Exception) { }
  }

  // Pure state helper (no Unity): pins the §10 honesty table for tests.
  public static PhoneCameraState NextStateAfterFreshness(bool running, bool transportOk,
      bool sawUp, bool hasFresh, PhoneCameraState current) {
    if (!running) return PhoneCameraState.Stopped;
    if (hasFresh) return PhoneCameraState.Live;
    if (current == PhoneCameraState.Error) return PhoneCameraState.Error;
    if (!transportOk) return sawUp
      ? PhoneCameraState.ConnectedWaitingFrames : PhoneCameraState.TempDisconnected;
    return PhoneCameraState.ConnectedWaitingFrames;
  }
}
