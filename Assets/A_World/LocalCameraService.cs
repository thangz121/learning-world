// A_World/LocalCameraService.cs — Agent A (World & Visual).
// Local PC webcam capture: the laptop/integrated camera (or a plugged-in USB
// webcam, which outranks it) streams DIRECTLY into the game — no LAN, no
// gateway, no phone. Runs BESIDE the phone path (GameCameraStreamService):
// the HUD shows local while it is Live and falls back to phone otherwise;
// the gateway prefer report (cam:local while ANY local camera is listed)
// keeps the phone START button stood down so the two never fight.
//
//   Unity WebCamTexture(dev, 320x240@30) --RAM--> CurrentTexture (the live
//   WebCamTexture itself: zero copies, zero per-frame allocation, no disk)
//
// States reuse PhoneCameraState so the HUD speaks one language:
//   Disabled (forced off via -e2e-nocam / feature off) · WaitingForPhone is
//   unused locally (no phone involved) · Connecting (device opening) · Live
//   (frames flowing) · TempDisconnected (device lost mid-run / stale) ·
//   Error (denied/unavailable) · Stopped. Hot-plug: the device list is
//   re-polled every 5 s; plugging a USB webcam switches to it, unplugging
//   the active one falls back (or to the phone when none remains).
// Privacy (§11, same as the phone path): RAM-only preview, no recording,
// no file, no upload, no analysis — Stop() destroys the texture handle.
// Never throws out of public methods; never touches the network.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class LocalCameraService : MonoBehaviour {
  public const int CaptureWidth = 320;
  public const int CaptureHeight = 240;
  public const int CaptureFps = 30;

  Func<string[]> _deviceLister;
  WebCamTexture _cam;
  string _activeDevice;
  PhoneCameraState _state = PhoneCameraState.Disabled;
  PhoneCameraState _lastLoggedState = (PhoneCameraState)(-1);
  bool _running;
  bool _forcePhone;
  float _pollTimer;
  const float PollSec = 5f;
  int _lastFrameCount;

  public PhoneCameraState State => _state;
  public bool IsRunning => _running;
  public string ActiveDevice => _activeDevice ?? string.Empty;
  // The live texture, or null unless actually Live (same gate contract as
  // GameCameraStreamService.CurrentTexture: stale faces are hidden by
  // construction, never frozen on screen).
  public Texture CurrentTexture => _state == PhoneCameraState.Live && _cam != null ? _cam : null;
  public bool HasLiveTexture => CurrentTexture != null;

  // Test seam: inject a scripted device list (hot-plug simulation without
  // hardware). Null restores the default (WebCamTexture.devices names).
  public void SetDeviceListerForTests(Func<string[]> lister) {
    _deviceLister = lister;
  }

  // E2E hook: -e2e-nocam forces the phone path (mirrors the phone service).
  public void SetForcePhoneForTests(bool force) {
    _forcePhone = force;
  }

  public void StartService() {
    try {
      if (_running) return;
      _running = true;
      _pollTimer = 0f; // open immediately on the first Update
      _state = PhoneCameraState.Connecting;
    } catch (Exception) { _state = PhoneCameraState.Error; }
  }

  public void StopService() {
    try {
      _running = false;
      CloseCamera();
      _state = PhoneCameraState.Stopped;
    } catch (Exception) { _state = PhoneCameraState.Stopped; }
  }

  void OnDestroy() {
    try { StopService(); } catch (Exception) { }
  }

  void Update() {
    if (!_running) return;
    try {
      if (IsForcePhone()) {
        // Forced phone path: stay out of the way, keep whatever the HUD had.
        if (_state != PhoneCameraState.Disabled && _state != PhoneCameraState.Stopped) {
          CloseCamera();
          _state = PhoneCameraState.Disabled;
        }
        return;
      }
      _pollTimer -= Time.deltaTime;
      if (_pollTimer <= 0f) {
        _pollTimer = PollSec;
        RepickIfNeeded();
      }
      RefreshLiveness();
      if (_state != _lastLoggedState) {
        _lastLoggedState = _state;
        try {
          UnityEngine.Debug.Log("[LocalCamera] " + StatusLine());
        } catch (Exception) { }
      }
    } catch (Exception) { _state = PhoneCameraState.Error; }
  }

  bool IsForcePhone() {
    try {
      if (_forcePhone) return true;
      foreach (string a in System.Environment.GetCommandLineArgs())
        if (string.Equals(a, "-e2e-nocam", StringComparison.OrdinalIgnoreCase)) return true;
    } catch (Exception) { }
    return false;
  }

  string[] ListDevices() {
    try {
      if (_deviceLister != null) return _deviceLister() ?? new string[0];
      var devs = WebCamTexture.devices;
      if (devs == null) return new string[0];
      var names = new string[devs.Length];
      for (int i = 0; i < devs.Length; i++) names[i] = devs[i].name;
      return names;
    } catch (Exception) { return new string[0]; }
  }

  // Re-resolves the picked device on the poll tick: USB-webcam-plugged
  // switches up, active-unplugged falls back, all-gone idles (phone takes
  // over via the HUD fallback + cam:phone report from the phone service).
  void RepickIfNeeded() {
    string picked;
    try { picked = LocalCameraClassifier.PickDevice(ListDevices()); }
    catch (Exception) { picked = null; }
    if (string.IsNullOrEmpty(picked)) {
      // No local camera at all: close, idle honestly (never a frozen frame).
      if (_cam != null || _state == PhoneCameraState.Live) {
        CloseCamera();
        _state = PhoneCameraState.ConnectedWaitingFrames;
      } else if (_state != PhoneCameraState.Error) {
        _state = PhoneCameraState.ConnectedWaitingFrames;
      }
      return;
    }
    if (!string.Equals(picked, _activeDevice, StringComparison.Ordinal)) {
      OpenCamera(picked);
    } else if (_cam == null && _state != PhoneCameraState.Error) {
      OpenCamera(picked);
    }
  }

  void OpenCamera(string deviceName) {
    CloseCamera();
    _activeDevice = deviceName;
    _state = PhoneCameraState.Connecting;
    try {
      _cam = new WebCamTexture(deviceName, CaptureWidth, CaptureHeight, CaptureFps);
      _cam.Play();
      _lastFrameCount = 0;
    } catch (Exception) {
      // Denied/unavailable (privacy off, exclusive use): error honestly so
      // the HUD falls back to the phone instead of spinning forever.
      CloseCamera();
      _state = PhoneCameraState.Error;
    }
  }

  void CloseCamera() {
    try {
      if (_cam != null) {
        try { if (_cam.isPlaying) _cam.Stop(); } catch (Exception) { }
        try { Destroy(_cam); } catch (Exception) { }
      }
    } catch (Exception) { }
    _cam = null;
  }

  void RefreshLiveness() {
    try {
      if (_cam == null) {
        if (_state == PhoneCameraState.Live) _state = PhoneCameraState.TempDisconnected;
        return;
      }
      bool playing = false;
      bool fresh = false;
      try {
        playing = _cam.isPlaying;
        if (playing && _cam.didUpdateThisFrame) {
          _lastFrameCount++;
          fresh = true;
        } else if (playing && _cam.width > 16) {
          // didUpdateThisFrame is per-frame; between frames the last image
          // is still current (same contract as a 30 fps preview).
          fresh = true;
        }
      } catch (Exception) { playing = false; }
      if (!playing) {
        _state = PhoneCameraState.TempDisconnected;
        return;
      }
      _state = fresh ? PhoneCameraState.Live : PhoneCameraState.Connecting;
    } catch (Exception) { _state = PhoneCameraState.Error; }
  }

  public string StatusLine() {
    try {
      int w = 0, h = 0, fps = 0;
      bool playing = false;
      try {
        if (_cam != null) {
          w = _cam.width; h = _cam.height;
          try { fps = (int)_cam.requestedFPS; } catch (Exception) { fps = 0; }
          playing = _cam.isPlaying;
        }
      } catch (Exception) { }
      return string.Format("localcam {0} dev=\"{1}\" playing={2} {3}x{4}@{5} frames={6}",
        _state, _activeDevice ?? "", playing, w, h, fps, _lastFrameCount);
    } catch (Exception) { return "localcam " + _state; }
  }
}
