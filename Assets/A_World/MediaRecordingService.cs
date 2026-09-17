// A_World/MediaRecordingService.cs — Agent A (World & Visual).
// Phase 2.3 PC-side recording layer: full play-session capture. Records
// GAMEPLAY video (GPU copy + async readback at OUTPUT size/fps — the render
// pipeline and max display quality are untouched) + CAMERA stream (phone/
// local, same box the HUD shows) + PHONE-MIC audio, then delivers MP4
// (H.264 + camera PiP overlay) + MP3 (LAME VBR) via the isolated FFmpeg
// backend when available, with verified WAV/AVI intermediates as the honest
// fallback when it is not. Architecture (§1 hard rule):
//
//   PHONE (capture + realtime transport only, no encoding)
//     -> PC gateway bridges (8451 audio PCM16 / 8452 camera JPEG)
//     -> GAME MEDIA INPUT (PhonePresenceWatcher link/audio stats,
//        GameCameraStreamService decoded Live frames)
//     -> THIS SERVICE (bounded tap copies, worker-thread file pumps)
//     -> STORAGE (WAV audio + AVI video [cam MJPEG verbatim + game raw BGRA]
//        + JSON sidecar)
//
// Recording boundary (§3): audio is accepted ONLY from the game-owned
// watcher tap (bytes the game already observed for link/HUD state);
// video is accepted ONLY from slot-accepted, freshly-decoded game frames
// (phone) or the live local texture (fallback, same box the HUD shows).
// The recorder NEVER dials its own bridge socket and NEVER re-encodes:
// phone JPEGs are stored verbatim, phone PCM16 is stored bit-exact.
//
// Threading (§11): the game thread only copies bounded samples into
// bounded queues; two background pumps own the file writers. Expensive
// work NEVER runs on the Unity main thread. Queues are bounded
// (drop-oldest + counted); audio drops are telemetered, video drops are
// observable via counters + gaps. Stop flushes deterministically:
// no "complete" is reported before finalization + verification succeed.
//
// Lifecycle (§14): Idle -> Starting -> Recording -> Stopping ->
// Finalizing -> Completed, with Error from Starting/Recording/Stopping/
// Finalizing. No boolean soup: CurrentState is the single owner.
//
// Privacy (§19): local files only, no upload, no cloud, no face/AI code,
// no raw media in logs (telemetry scalars + paths only).
// Control (§20): explicit StartRecording/StopRecording + F2 toggle for
// dev/E2E. Nothing auto-records on connect or game start.
using System;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

[DisallowMultipleComponent]
public class MediaRecordingService : MonoBehaviour {
  const int PhoneFreshnessMs = 3000; // mirrors PhoneCameraConfig.StaleMs default
  const int FinalizeTimeoutSec = 30;
  const int MaxSampleIterationsPerFrame = 3;

  // Remembered save location (PlayerPrefs). Missing = never chosen: F2 opens
  // the in-game chooser instead of hardcoding a far-away path. Either an
  // explicit dir or the DEFAULT sentinel is remembered — choosing (either
  // way) asks exactly once. Double-F2 re-opens the chooser (change).
  // Tests isolate via SetPrefsKeyForTests + cleanup.
  public const string PrefsKey = "LWE.MediaRec.OutputDir.v1";
  public const string DefaultSentinel = "__DEFAULT__";
  string _prefsKey = PrefsKey;
  RecordingLocationDialog _locationDialog;
  RecordingConfirmDialog _confirmDialog;
  RecordingToast _toast;
  RecordingIndicator _indicator;
  PhoneCameraHud _cameraHud;
  bool _chooserStartAfter;
  readonly F2DoubleTracker _f2 = new F2DoubleTracker();

  MediaRecordingConfig _baseConfig = MediaRecordingConfig.Default;
  MediaRecordingConfig _effective = MediaRecordingConfig.Default;
  GameCameraStreamService _phoneCam;
  LocalCameraService _localCam;
  Func<PhonePresenceWatcher> _audioSource;
  Func<bool> _localMicPresent;
  Func<string> _localMicDevice;
  bool? _testLocalMic;
  string _outputBaseDir;
  string _repoToolsDir;
  string _appToolsDir;
  bool _keyControl = true;
  RecordingMode _toggleMode = RecordingMode.MicAndCamera;

  // Full-session gameplay capture (output side only — the render pipeline
  // and display quality are untouched; this is a GPU copy + async readback
  // at the configured OUTPUT size/fps, never a re-render).
  Camera _boundGameCam;
  Camera _gameCam;
  RenderTexture _recordRT;
  bool _shotArmed; // asked Unity for a screen capture this tick: read it back next Update
  bool _gameCaptureReady;
  bool _savedRunInBackground = true;
  bool _touchedRunInBackground;

  volatile RecordingState _state = RecordingState.Idle;
  string _lastError = string.Empty;

  readonly object _telLock = new object();
  RecordingTelemetry _telemetry = new RecordingTelemetry();
  readonly RecordingClock _clock = new RecordingClock();
  readonly RecordingSessionLatch _audioLatch = new RecordingSessionLatch();
  readonly RecordingSessionLatch _videoLatch = new RecordingSessionLatch();
  readonly RecordingSessionLatch _gameLatch = new RecordingSessionLatch();
  readonly RecordingSessionLatch _localLatch = new RecordingSessionLatch();
  BoundedByteQueue _audioQueue;
  BoundedByteQueue _videoQueue;
  BoundedByteQueue _gameRawQueue; // RGBA handoff: main thread -> game worker

  // Laptop-mic capture (user ask, additive): the recorder opens its OWN loop
  // handle on the GAME-VALIDATED local device (bundle LocalMic, never a raw
  // pick) and feeds the SAME bounded audio queue/pump/wav as phone audio.
  // Phone keeps precedence (no mixing ever); switches flag interrupted.
  // Known risk, stated plainly: holding the mic open across speaking
  // exercises overlaps the speech engine's transient opens. The engine's
  // device-error paths already treat that as environment failure (never a
  // WrongWord); if the user run shows interference, the design revisits to
  // a shared handle. No speech file is touched here.
  AudioClip _localMicClip;
  string _localMicActiveDevice;
  int _localMicPos;
  int _localMicLoopFrames;
  int _localMicChannels = 1;
  int _localMicRate = 16000;
  bool _localMicActive;
  bool _localMicFed;
  float _localMicRepickTimer;

  WavWriter _audioWriter;
  AviMjpegWriter _videoWriter;
  RawVideoWriter _gameWriter;
  Thread _audioThread;
  Thread _videoThread;
  Thread _gameThread;
  Thread _transcodeThread;
  volatile bool _audioPumpDone = true;
  volatile bool _videoPumpDone = true;
  volatile bool _gamePumpDone = true;
  volatile bool _phoneFed; // phone fed >=1 chunk: watcher loss below is a real drop
  volatile bool _audioWriteFailed;
  volatile bool _videoWriteFailed;
  volatile bool _gameWriteFailed;

  string _sessionId = string.Empty;
  string _audioPath = string.Empty;
  string _videoPath = string.Empty;
  string _gamePath = string.Empty;
  string _mp4Path = string.Empty;
  string _mp3Path = string.Empty;
  string _sidecarPath = string.Empty;
  string _outputDir = string.Empty;
  DateTime _stopUtc;
  float _videoSampleTimer;
  float _gameSampleTimer;
  // Real-fps instrumentation state (all reset per session in ResetSessionState;
  // every accumulation is lock-guarded + never-throw — probes, not logic).
  float _lastCaptureTickTime = -1f; // unscaled time of last enqueued readback
  uint _prevGameHash;
  bool _hasPrevGameHash;
  double _cpuStartSec;
  double _cpuWallStartSec; // SessionDurationSec at START (wall anchor for CPU %)
  bool _interruptedLogged;
  bool _testForceSources;
  bool _testDisableTranscode;

  // Transcode stage bookkeeping (Finalizing sub-phases, main-thread owned).
  bool _intermediatesVerified;
  bool _transcodeStarted;
  volatile bool _transcodeDone;
  TranscodeResult _transcodeResult;
  DateTime _transcodeStartUtc;
  int _transcodeTimeoutMs;

  public RecordingState CurrentState => _state;
  public string LastError {
    get { try { lock (_telLock) { return _lastError; } } catch (Exception) { return string.Empty; } }
  }
  public string SessionId => _sessionId;
  public string AudioPath => _audioPath;
  public string VideoPath => _videoPath;

  // Injection boundary (wired by MarketBootstrap; all optional except the
  // services actually needed for the requested mode — missing pieces fail
  // StartRecording explicitly, never silently). gameCam: gameplay camera to
  // copy for session capture (null = Camera.main fallback at Start).
  // repoToolsDir: <repo>/tools for dev-time ffmpeg detection (null = skip).
  public void Bind(MediaRecordingConfig config, GameCameraStreamService phoneCam,
      LocalCameraService localCam, Func<PhonePresenceWatcher> audioSource, string outputBaseDir,
      Camera gameCam = null, string repoToolsDir = null, Func<bool> localMicPresent = null,
      Func<string> localMicDevice = null) {
    try {
      string reason;
      if (config.Validate(out reason)) _baseConfig = config;
      _phoneCam = phoneCam;
      _localCam = localCam;
      _audioSource = audioSource;
      if (!string.IsNullOrEmpty(outputBaseDir)) _outputBaseDir = outputBaseDir;
      _boundGameCam = gameCam;
      if (!string.IsNullOrEmpty(repoToolsDir)) _repoToolsDir = repoToolsDir;
      _localMicPresent = localMicPresent;
      _localMicDevice = localMicDevice;
    } catch (Exception) { }
  }

  public void SetToggleModeForTests(RecordingMode mode) {
    _toggleMode = mode;
  }

  public void SetTestForceSourcesAvailable(bool force) {
    _testForceSources = force;
  }

  public void SetTestDisableTranscode(bool disable) {
    _testDisableTranscode = disable;
  }

  public void SetTestOutputBaseDir(string dir) {
    try { if (!string.IsNullOrEmpty(dir)) _outputBaseDir = dir; } catch (Exception) { }
  }

  // Forensic seam: keep wav/avi intermediates after a successful transcode
  // (default deletes them). Used to compare pre-encoder frames against the
  // decoded mp4; never enabled in normal play.
  public void SetKeepIntermediatesForTests(bool keep) {
    try { _baseConfig.KeepIntermediates = keep; } catch (Exception) { }
  }

  // Output quality preset (Preview/Standard/High/Max): drives CRF + x264
  // preset + capture resolution together at the next StartRecording. Invalid
  // values are refused (the running default is kept), never applied half-way.
  public void SetQuality(VideoQuality q) {
    try {
      if (!System.Enum.IsDefined(typeof(VideoQuality), q)) return;
      _baseConfig.Quality = q;
    } catch (Exception) { }
  }

  // App-local tools dir (dependency auto-install target for portable
  // FFmpeg). The transcoder finds it without PATH changes.
  public void SetAppToolsDir(string dir) {
    try { _appToolsDir = dir; } catch (Exception) { }
  }

  // Save-location dialog (optional; without it F2 keeps the legacy direct
  // behavior). Wired by MarketBootstrap like the other setup dialogs.
  public void BindLocationDialog(RecordingLocationDialog dlg) {
    try { _locationDialog = dlg; } catch (Exception) { }
  }

  public void BindConfirmDialog(RecordingConfirmDialog dlg) {
    try { _confirmDialog = dlg; } catch (Exception) { }
  }

  public void BindIndicator(RecordingIndicator ind) {
    try { _indicator = ind; } catch (Exception) { }
  }

  // Camera box hide (user rule: exactly ONE face in the file — the PiP).
  // Wired by MarketBootstrap like the other dialogs; null-safe (camera path
  // may be absent while recording still runs on gameplay + audio).
  public void BindCameraHud(PhoneCameraHud hud) {
    try { _cameraHud = hud; } catch (Exception) { }
  }

  public void SetLocalMicForTests(bool present) {
    _testLocalMic = present;
  }

  public void SetPrefsKeyForTests(string key) {
    try { if (!string.IsNullOrEmpty(key)) _prefsKey = key; } catch (Exception) { }
  }

  // --- save location (asked in-game once, remembered, double-F2 to change) --
  // First F2 with no remembered choice opens the chooser (animated panel)
  // instead of silently writing to a hardcoded path. Choosing EITHER the
  // default or a folder remembers it (sentinel for default) — later F2s
  // start immediately. Double-F2 re-opens the chooser. Cancel = no recording.

  public bool HasChosenOutput() {
    try {
      string v = PlayerPrefs.GetString(_prefsKey, string.Empty);
      return !string.IsNullOrEmpty(v);
    } catch (Exception) { return false; }
  }

  public string SavedOutputDir() {
    try {
      string v = PlayerPrefs.GetString(_prefsKey, string.Empty);
      if (string.IsNullOrEmpty(v) || v == DefaultSentinel) return null;
      return v;
    } catch (Exception) { return null; }
  }

  public string DefaultOutputDir() {
    try {
      if (!string.IsNullOrEmpty(_outputBaseDir)) return _outputBaseDir;
      string base_;
      try { base_ = Application.persistentDataPath; } catch (Exception) { return null; }
      if (string.IsNullOrEmpty(base_)) return null;
      return Path.Combine(base_, "MediaRecordings");
    } catch (Exception) { return null; }
  }

  public string CurrentOutputDisplay() {
    try {
      if (HasChosenOutput()) {
        string saved = SavedOutputDir();
        if (!string.IsNullOrEmpty(saved)) return saved;
      }
      return "Mặc định: " + (DefaultOutputDir() ?? "?");
    } catch (Exception) { return string.Empty; }
  }

  public bool IsDirWritable(string dir, out string why) {
    why = null;
    try {
      if (string.IsNullOrEmpty(dir)) {
        why = "Thư mục trống.";
        return false;
      }
      if (!Directory.Exists(dir)) {
        why = "Thư mục không còn tồn tại. Hãy chọn chỗ khác.";
        return false;
      }
      string probe = Path.Combine(dir, ".lwe-write-test");
      try {
        File.WriteAllText(probe, "ok");
        try { File.Delete(probe); } catch (Exception) { }
      } catch (Exception) {
        why = "Không ghi được vào thư mục này (quyền đĩa). Hãy chọn chỗ khác.";
        return false;
      }
      return true;
    } catch (Exception) {
      why = "Không kiểm tra được thư mục.";
      return false;
    }
  }

  public bool TrySetOutputDir(string dir, out string error) {
    error = null;
    try {
      string why;
      if (!IsDirWritable(dir, out why)) {
        error = why;
        return false;
      }
      try { PlayerPrefs.SetString(_prefsKey, dir); } catch (Exception e) {
        error = "Không lưu được lựa chọn: " + e.GetType().Name;
        return false;
      }
      try { PlayerPrefs.Save(); } catch (Exception) { }
      try { Debug.Log("[MediaRec] save location set: " + dir); } catch (Exception) { }
      return true;
    } catch (Exception e) {
      error = "Lỗi: " + e.GetType().Name;
      return false;
    }
  }

  // Remember the DEFAULT (sentinel) so choosing it also asks exactly once.
  public void UseDefaultLocation() {
    try { PlayerPrefs.SetString(_prefsKey, DefaultSentinel); } catch (Exception) { }
    try { PlayerPrefs.Save(); } catch (Exception) { }
    try { Debug.Log("[MediaRec] save location set: default"); } catch (Exception) { }
  }

  // False = open the chooser instead of recording (first F2, stale dir).
  public bool EnsureOutputReady(out string error) {
    error = null;
    try {
      if (!HasChosenOutput()) {
        error = "no-saved-location";
        return false;
      }
      string dir = SavedOutputDir() ?? DefaultOutputDir();
      string why;
      if (!IsDirWritable(dir, out why)) {
        error = why;
        return false;
      }
      return true;
    } catch (Exception) {
      error = "check-failed";
      return false;
    }
  }

  void OpenLocationChooser(string errorOrNull, bool startAfterChoice) {
    try {
      _chooserStartAfter = startAfterChoice;
      _locationDialog.ShowChooser(CurrentOutputDisplay(), errorOrNull,
        OnChooserDefault, OnChooserBrowse, OnChooserCancel);
    } catch (Exception) { _chooserStartAfter = false; }
  }

  void OnChooserDefault() {
    try {
      UseDefaultLocation();
      AfterLocationChoice();
    } catch (Exception) { }
  }

  void OnChooserBrowse() {
    try {
      string picked = null;
      bool ok = false;
      try { ok = NativeFolderDialog.TryPickFolder("Chon thu muc luu ban thu", out picked); }
      catch (Exception) { ok = false; }
      if (!ok || string.IsNullOrEmpty(picked)) {
        try { _locationDialog.SetError("Chưa chọn. Hãy chọn một thư mục, hoặc bấm Hủy."); }
        catch (Exception) { }
        return;
      }
      string err;
      if (!TrySetOutputDir(picked, out err)) {
        try { _locationDialog.SetError(err); } catch (Exception) { }
        return;
      }
      AfterLocationChoice();
    } catch (Exception) { }
  }

  void OnChooserCancel() {
    try {
      _chooserStartAfter = false;
      _locationDialog.Hide();
      try { Debug.Log("[MediaRec] location cancelled — not recording"); } catch (Exception) { }
    } catch (Exception) { }
  }

  void AfterLocationChoice() {
    try { if (_locationDialog != null) _locationDialog.Hide(); } catch (Exception) { }
    bool start = _chooserStartAfter;
    _chooserStartAfter = false;
    try {
      ShowToast("Đã nhớ chỗ lưu: " + CurrentOutputDisplay()
        + "\nNhấn F2 2 lần liên tiếp để đổi.", 5f);
    } catch (Exception) { }
    if (start) {
      try {
        Debug.Log("[MediaRec] saving to " + CurrentOutputDisplay());
      } catch (Exception) { }
      StartRecordingSmart();
    } else {
      try { Debug.Log("[MediaRec] save location: " + CurrentOutputDisplay()); }
      catch (Exception) { }
    }
  }

  // --- result toast (in-game notice: what happened + where files are) ---------
  // Non-modal by construction (no raycaster, clicks pass through): recording
  // just ended, the game keeps running underneath.

  public void BindToast(RecordingToast toast) {
    try { _toast = toast; } catch (Exception) { }
  }

  public void ShowToast(string msg, float seconds) {
    try {
      if (_toast == null) return;
      _toast.Show(msg, seconds);
    } catch (Exception) { }
  }

  // Technical reason -> parent-facing hint (toast + log share it).
  // disk-space-low carries the required GB ("disk-space-low:8GB"); parse it
  // defensively, fall back to the full-size wording on any shape mismatch.
  static string DiskNeedText(string reason) {
    try {
      int i = reason.IndexOf("disk-space-low:", StringComparison.Ordinal);
      if (i >= 0) {
        int s = i + "disk-space-low:".Length;
        int e = reason.IndexOf("GB", s, StringComparison.OrdinalIgnoreCase);
        if (e > s && e - s <= 4) {
          string num = reason.Substring(s, e - s).Trim();
          int gb;
          if (int.TryParse(num, out gb) && gb > 0 && gb <= 1024)
            return "cần trống ít nhất " + gb + " GB";
        }
      }
    } catch (Exception) { }
    return "cần trống ít nhất 8 GB";
  }

  static string FriendlyReason(string reason) {
    try {
      if (string.IsNullOrEmpty(reason)) return "lỗi không rõ.";
      if (reason.Contains("no-audio-in-game"))
        return "Không thấy nguồn tiếng nào (mic + phone đều không có).";
      if (reason.Contains("no-phone-audio-in-game"))
        return "Chưa có tiếng từ điện thoại. Hãy kết nối phone (QR) rồi bấm F2 lại.";
      if (reason.Contains("no-camera-in-game"))
        return "Chưa thấy camera trong game.";
      if (reason.Contains("output-dir"))
        return "Không ghi được vào chỗ lưu. Nhấn F2 2 lần liên tiếp để chọn chỗ khác.";
      if (reason.Contains("disk-space-low"))
        return "Ổ đĩa sắp đầy (" + DiskNeedText(reason)
          + " cho bản thu). Dọn bớt rồi thử lại.";
      if (reason.Contains("encoder-init-failed"))
        return "Không khởi động được bộ mã hóa.";
      if (reason.Contains("transcode-timeout"))
        return "Xuất MP4 quá lâu (file thô vẫn giữ nguyên).";
      if (reason.Contains("transcode-failed"))
        return "Xuất MP4 thất bại (file WAV/AVI thô vẫn giữ nguyên).";
      if (reason.Contains("write-failed"))
        return "Ghi file thất bại (kiểm tra dung lượng đĩa).";
      if (reason.Contains("finalize-timeout"))
        return "Kết thúc bản thu quá lâu.";
      return reason;
    } catch (Exception) { return "lỗi không rõ."; }
  }

  static string BaseName(string path) {
    try {
      if (string.IsNullOrEmpty(path)) return string.Empty;
      return Path.GetFileName(path);
    } catch (Exception) { return string.Empty; }
  }

  // --- source detection (what CAN be recorded, before promising) --------------
  // Phone audio flowing in-game = recordable. A listed PC mic with no phone
  // audio = DETECTED but not recordable by this phase (the recorder taps the
  // phone watcher only — stated plainly in the proposal, never silently).

  public bool IsLocalMicAvailable() {
    try {
      if (_testLocalMic.HasValue) return _testLocalMic.Value;
      if (_localMicPresent == null) return false;
      return _localMicPresent();
    } catch (Exception) { return false; }
  }

  public AudioSourceState GetAudioSourceState() {
    try {
      if (IsPhoneAudioInGame()) return AudioSourceState.PhoneReady;
      if (IsLocalMicAvailable()) return AudioSourceState.LocalOnly;
      return AudioSourceState.None;
    } catch (Exception) { return AudioSourceState.None; }
  }

  // F2 smart start: full session when everything is there; an explicit
  // video-only PROPOSAL when audio is missing but video is present; the
  // existing gates voice the exact reason when nothing usable exists.
  public void StartRecordingSmart() {
    try {
      bool audioReady = false, videoReady = false;
      try { audioReady = IsSessionAudioReady(); } catch (Exception) { }
      try { videoReady = IsCameraInGame(); } catch (Exception) { }
      StartDecision d = RecordingStartDecider.DecideStart(audioReady, videoReady, _toggleMode);
      if (d == StartDecision.ProposeVideoOnly && _confirmDialog != null) {
        ShowVideoOnlyProposal();
        return;
      }
      StartRecording(_toggleMode);
    } catch (Exception) { }
  }

  void ShowVideoOnlyProposal() {
    try {
      _confirmDialog.ShowConfirm("Không có tiếng để thu",
        "Không thấy nguồn tiếng nào (mic + phone đều không có).\n\nQuay video không tiếng?",
        "Quay video", "Hủy", OnVideoOnlyConfirm, OnVideoOnlyCancel);
      try { Debug.Log("[MediaRec] proposing video-only (no audio source)"); }
      catch (Exception) { }
    } catch (Exception) { }
  }

  void OnVideoOnlyConfirm() {
    try {
      if (_confirmDialog != null) _confirmDialog.Hide();
      StartRecording(RecordingMode.CameraOnly);
    } catch (Exception) { }
  }

  void OnVideoOnlyCancel() {
    try {
      if (_confirmDialog != null) _confirmDialog.Hide();
      try { Debug.Log("[MediaRec] video-only declined — not recording"); } catch (Exception) { }
    } catch (Exception) { }
  }

  // Session audio is recordable when the phone flows OR a game-validated
  // local mic is present (test seams force each side independently).
  public bool IsSessionAudioReady() {
    try {
      if (IsPhoneAudioInGame()) return true;
      return IsLocalMicAvailable();
    } catch (Exception) { return false; }
  }

  // --- explicit control (§20) ------------------------------------------------
  public bool StartRecording(RecordingMode mode) {
    try {
      RecordingState s = _state;
      if (s == RecordingState.Recording || s == RecordingState.Starting
          || s == RecordingState.Stopping || s == RecordingState.Finalizing) {
        SetError("already-running");
        return false;
      }
      SetState(RecordingState.Starting);
      _effective = _baseConfig;
      _effective.RecordAudio = mode == RecordingMode.MicOnly || mode == RecordingMode.MicAndCamera;
      _effective.RecordVideo = mode == RecordingMode.CameraOnly || mode == RecordingMode.MicAndCamera;
      // Quality preset fans out here (CRF + x264 preset now; capture
      // resolution follows in PrepareGameCapture from the live screen size,
      // so unit/test envs without a screen keep deterministic behavior).
      try {
        QualityTierParams tier = QualityTier.For(_effective.Quality);
        _effective.VideoCrf = tier.Crf;
        _effective.VideoPreset = tier.Preset;
      } catch (Exception) { }
      string reason;
      if (!_effective.Validate(out reason)) return FailStart("bad-config:" + reason);

      string dir = ResolveOutputDir();
      if (string.IsNullOrEmpty(dir)) return FailStart("output-dir-unavailable");
      try {
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
      } catch (Exception) { return FailStart("output-dir-unavailable"); }
      _outputDir = dir;

      _clock.Start();
      _sessionId = MediaRecordingNaming.NewSessionId(_clock.StartUtc);
      if (!MediaRecordingNaming.TryJoin(dir, MediaRecordingNaming.AudioFileName(_sessionId), out _audioPath))
        return FailStart("naming-failed");
      if (!MediaRecordingNaming.TryJoin(dir, MediaRecordingNaming.VideoFileName(_sessionId), out _videoPath))
        return FailStart("naming-failed");
      if (!MediaRecordingNaming.TryJoin(dir, MediaRecordingNaming.SidecarFileName(_sessionId), out _sidecarPath))
        return FailStart("naming-failed");
      if (!MediaRecordingNaming.TryJoin(dir, MediaRecordingNaming.GameFileName(_sessionId), out _gamePath))
        return FailStart("naming-failed");
      if (!MediaRecordingNaming.TryJoin(dir, MediaRecordingNaming.Mp4FileName(_sessionId), out _mp4Path))
        return FailStart("naming-failed");
      if (!MediaRecordingNaming.TryJoin(dir, MediaRecordingNaming.Mp3FileName(_sessionId), out _mp3Path))
        return FailStart("naming-failed");
      if (!_effective.RecordAudio) _audioPath = string.Empty;
      if (!_effective.RecordVideo) {
        _videoPath = string.Empty;
        _gamePath = string.Empty;
        _mp4Path = string.Empty;
      }
      if (!_effective.RecordAudio) _mp3Path = string.Empty;

      // Capture resolution (user rule): fit the LIVE screen inside the
      // quality-tier cap — a 1080p source stays 1080p on Max, a small window
      // is never upscaled, aspect preserved, even dims. Playing-gated:
      // EditMode batch scenes may contain cameras/screens, but ambient
      // hardware reads apply ONLY while playing, so unit behavior stays
      // deterministic. Runs BEFORE ResetSessionState + writers Begin so
      // telemetry, queues, writers, readback and transcode all see the SAME
      // resolved size.
      try {
        bool playing = false;
        try { playing = Application.isPlaying; } catch (Exception) { }
        bool hasCam = false;
        try { hasCam = (_boundGameCam != null || Camera.main != null); } catch (Exception) { }
        int sw = 0, sh = 0;
        try { sw = Screen.width; sh = Screen.height; } catch (Exception) { }
        if (_effective.RecordVideo && playing && hasCam && sw >= 16 && sh >= 16) {
          int rw, rh;
          QualityTier.ResolveGameSize(_effective.Quality, sw, sh, out rw, out rh);
          _effective.GameWidth = rw;
          _effective.GameHeight = rh;
        }
      } catch (Exception) { }

      ResetSessionState(mode);

      // Availability gates: media must be IN THE GAME before it can be
      // recorded (§3). Session audio = phone flowing OR a game-validated
      // local mic (user rule: USB rời > laptop > phone; the recorder feeds
      // the same order). Refuse explicitly rather than writing empty files.
      if (_effective.RecordAudio && !IsSessionAudioReady())
        return FailStart("no-audio-in-game");
      if (_effective.RecordVideo && !IsCameraInGame())
        return FailStart("no-camera-in-game");

      if (_effective.RecordAudio) {
        _audioQueue = BoundedByteQueue.ForAudio(_effective.MaxAudioQueueSec);
        _audioWriter = new WavWriter();
        if (!_audioWriter.Begin(_audioPath)) return FailStart("encoder-init-failed:audio");
      }
      if (_effective.RecordVideo) {
        // Raw gameplay intermediates are big: refuse early with a clear
        // message rather than corrupting a take when the disk fills mid-way.
        // Requirement scales with the configured take (unit sessions need
        // only the floor, real 1080p30 needs the full 8 GB).
        long needStart = MediaRecording.RequiredFreeBytes(
          _effective.GameWidth, _effective.GameHeight, _effective.GameFps);
        if (!MediaRecording.DriveSpaceOk(dir, needStart))
          return FailStart("disk-space-low:" + (needStart / (1024L * 1024L * 1024L)) + "GB");
        _videoQueue = BoundedByteQueue.ForVideo(_effective.MaxVideoFrames);
        _videoWriter = new AviMjpegWriter();
        if (!_videoWriter.Begin(_videoPath, _effective.VideoWidth, _effective.VideoHeight, _effective.VideoFps))
          return FailStart("encoder-init-failed:video");
        // Gameplay intermediate (same session, own queue/pump/thread):
        // RGBA handoff cap bounds the main-thread memcpy cost.
        long rawCap = (long)MediaRecording.MaxGameRawFrames
          * _effective.GameWidth * _effective.GameHeight * 4;
        _gameRawQueue = new BoundedByteQueue(MediaRecording.MaxGameRawFrames, rawCap);
        _gameWriter = new RawVideoWriter();
        // Lossless gameplay path (forensic finding): raw BGRA chunks, no
        // JPEG stage anywhere between screen and x264.
        if (!_gameWriter.Begin(_gamePath, _effective.GameWidth, _effective.GameHeight))
          return FailStart("encoder-init-failed:game");
        if (!PrepareGameCapture()) {
          // Gameplay copy unavailable (no camera/RT): the session continues
          // with the camera stream alone — transcode bases on cam.avi.
          _gameCaptureReady = false;
          try { Debug.Log("[MediaRec] gameplay capture unavailable; camera-only video"); }
          catch (Exception) { }
        }
      }

      if (_effective.RecordAudio) {
        var w = CurrentWatcher();
        // Watcher absent is fine when a local mic feeds (user rule); fail
        // only when NO audio source exists at all (mirrors the gate above).
        if (w == null && !IsLocalMicAvailable() && !_testForceSources)
          return FailStart("no-audio-in-game");
        try { if (w != null) w.AudioPayloadAccepted += OnAudioTap; }
        catch (Exception) { return FailStart("audio-tap-failed"); }
      }

      _audioPumpDone = !_effective.RecordAudio;
      _videoPumpDone = !_effective.RecordVideo;
      _gamePumpDone = !_effective.RecordVideo;
      _audioWriteFailed = false;
      _videoWriteFailed = false;
      _gameWriteFailed = false;
      _transcodeDone = false;
      try {
        if (_effective.RecordAudio) {
          _audioThread = new Thread(AudioPump) { IsBackground = true, Name = "MediaRecAudio" };
          _audioThread.Start();
        }
        if (_effective.RecordVideo) {
          _videoThread = new Thread(VideoPump) { IsBackground = true, Name = "MediaRecVideo" };
          _videoThread.Start();
          _gameThread = new Thread(GamePump) { IsBackground = true, Name = "MediaRecGame" };
          _gameThread.Start();
        }
      } catch (Exception) { return FailStart("worker-start-failed"); }

      SetState(RecordingState.Recording);
      // Real-fps anchors: process CPU seconds at START (FinishSession diffs
      // it over the wall); pacing/dup/render probes were zeroed in reset.
      try {
        _cpuStartSec = CpuUtil.ProcessCpuSec();
        _cpuWallStartSec = SessionDurationSec();
      } catch (Exception) { }
      // One face in the file (user rule): the in-game camera box steps aside
      // while recording (the PiP carries the face); it returns on Stop.
      // Placed AFTER all FailStart gates: a refused start never hides the box.
      try { if (_cameraHud != null) _cameraHud.SetRecordingHide(true); } catch (Exception) { }
      try {
        Debug.Log("[MediaRec] START session=" + _sessionId + " mode=" + mode
          + " audio=" + (_effective.RecordAudio ? _audioPath : "<off>")
          + " video=" + (_effective.RecordVideo ? _videoPath + "+" + _gamePath : "<off>")
          + " key=F2 toggles mic+camera");
      } catch (Exception) { }
      return true;
    } catch (Exception) { return FailStart("start-exception"); }
  }

  public bool StopRecording() {
    try {
      if (_state != RecordingState.Recording) return false;
      _stopUtc = DateTime.UtcNow;
      try {
        var w = CurrentWatcher();
        if (w != null) {
          try { w.AudioPayloadAccepted -= OnAudioTap; } catch (Exception) { }
        }
      } catch (Exception) { }
      try { if (_audioQueue != null) _audioQueue.Close(); } catch (Exception) { }
      try { if (_videoQueue != null) _videoQueue.Close(); } catch (Exception) { }
      try { if (_gameRawQueue != null) _gameRawQueue.Close(); } catch (Exception) { }
      try { StopLocalMic(); } catch (Exception) { }
      lock (_telLock) { _telemetry.StopUtcIso = RecordingClock.ToIso(_stopUtc); }
      SetState(RecordingState.Stopping);
      // Recording ended (STOP): the camera box returns immediately while the
      // session finalizes in the background (see toast "finishing…").
      try { if (_cameraHud != null) _cameraHud.SetRecordingHide(false); } catch (Exception) { }
      try { Debug.Log("[MediaRec] STOP session=" + _sessionId + " (flushing)"); }
      catch (Exception) { }
      return true;
    } catch (Exception) { return false; }
  }

  void Update() {
    try {
      PollRecordKey();
      // Armed single-F2: the disambiguation hold expired with no second
      // press, so this really is a single press -> start now.
      if (IsStartableState()) {
        bool fire = false;
        try { fire = _f2.PollStart(Time.unscaledTime); } catch (Exception) { }
        if (fire) StartRecordingSmart();
      }
      if (_state == RecordingState.Recording) {
        // Source-rate probe (2 float ops/frame, lock-guarded): how fast the
        // game itself renders while recording. Capture can never outrun this.
        try {
          float ms = 0f;
          try { ms = Time.unscaledDeltaTime * 1000f; } catch (Exception) { }
          if (ms < 0) ms = 0;
          lock (_telLock) {
            _telemetry.RenderFrames++;
            _telemetry.RenderMsSum += ms;
            if (ms > _telemetry.RenderMsMax) _telemetry.RenderMsMax = ms;
          }
        } catch (Exception) { }
        if (_effective.RecordVideo) {
          SampleVideo();
          SampleGame();
        }
        if (_effective.RecordAudio) PollLocalMic();
        WatchAudioPresence();
        PollDiskSpace(); // throttled inside: graceful early stop before the disk fills
      } else if (ShouldPumpStopping(_state)) {
        // Finalizing must keep pumping: the transcode thread finishes there
        // and FinishSession only runs from this pump (a session stuck in
        // Finalizing with no pump is the P23 loopback-E2E finding).
        PumpStopping();
      }
      DriveIndicator();
    } catch (Exception) { }
  }

  // "Am I recording?" badge (user ask): live REC+timer while recording,
  // finishing line while stopping/finalizing, hidden otherwise. State-only;
  // the indicator owns blink/text details.
  void DriveIndicator() {
    try {
      if (_indicator == null) return;
      if (_state == RecordingState.Recording) {
        float sec = 0f;
        try {
          int ms = _clock.OffsetMs();
          sec = ms >= 0 ? ms / 1000f : 0f;
        } catch (Exception) { }
        _indicator.SetRecording(true, sec);
      } else if (_state == RecordingState.Stopping || _state == RecordingState.Finalizing) {
        _indicator.SetFinishing();
      } else if (_indicator.IsShowing) {
        _indicator.Hide();
      }
    } catch (Exception) { }
  }

  // Mid-session disk guard (raw intermediates are big): stop gracefully
  // while there is still room to finalize + transcode the partial take.
  // Throttled to one cheap probe per 10 s. Trips once per session: sets the
  // flag and stops normally; FinishSession converts it to an explicit
  // disk-space-low Error (with the partial files kept), never a silent
  // truncation and never a corrupt tail.
  float _lastDiskCheck = -1000f;
  bool _diskLowStop;

  void PollDiskSpace() {
    try {
      if (_diskLowStop) return;
      float now = 0f;
      try { now = Time.unscaledTime; } catch (Exception) { return; }
      if (now - _lastDiskCheck < 10f) return;
      _lastDiskCheck = now;
      string dir = _outputDir;
      if (string.IsNullOrEmpty(dir)) return;
      long needMid = MediaRecording.RequiredFreeBytesMid(
        _effective.GameWidth, _effective.GameHeight, _effective.GameFps);
      if (MediaRecording.DriveSpaceOk(dir, needMid)) return;
      _diskLowStop = true;
      try { Debug.Log("[MediaRec] disk low mid-session: stopping gracefully"); }
      catch (Exception) { }
      try { StopRecording(); } catch (Exception) { }
    } catch (Exception) { }
  }

  // Update pump predicate (pinned by P22: Stopping AND Finalizing both pump;
  // dropping Finalizing strands sessions after the transcode finishes).
  public static bool ShouldPumpStopping(RecordingState s) {
    return s == RecordingState.Stopping || s == RecordingState.Finalizing;
  }

  // --- game-thread sampling ---------------------------------------------------
  void SampleVideo() {
    try {
      _videoSampleTimer += Time.deltaTime;
      float interval = 1f / Math.Max(1, _effective.VideoFps);
      int iters = 0;
      while (_videoSampleTimer >= interval && iters < MaxSampleIterationsPerFrame) {
        _videoSampleTimer -= interval;
        iters++;
        SampleVideoOnce();
      }
      if (_videoSampleTimer > interval * 2) _videoSampleTimer = 0; // don't spiral after hitches
    } catch (Exception) { }
  }

  void SampleVideoOnce() {
    try {
      if (_videoQueue == null || _videoQueue.IsClosed) return;
      uint serial = 0, seq = 0;
      byte[] jpeg = null;
      // Precedence mirrors the HUD: live local wins, phone is the fallback.
      bool fromLocal = false;
      try {
        if (_localCam != null && _localCam.HasLiveTexture) {
          jpeg = EncodeLocalFrame(_localCam.CurrentTexture);
          fromLocal = jpeg != null;
          if (fromLocal) serial = 0; // local path: single implicit session
        }
      } catch (Exception) { fromLocal = false; }
      if (!fromLocal) {
        try {
          if (_phoneCam != null) {
            int age;
            if (_phoneCam.TryPeekAcceptedJpegForRecording(out serial, out seq, out jpeg, out age)) {
              if (!(age >= 0 && age <= PhoneFreshnessMs)) jpeg = null;
            }
          }
        } catch (Exception) { jpeg = null; }
        if (jpeg == null) {
          lock (_telLock) { _telemetry.VideoGapSamples++; }
          return;
        }
      }
      if (!AcceptVideoSerial(serial)) return;
      if (_videoQueue.TryEnqueue(jpeg)) {
        lock (_telLock) {
          _telemetry.VideoFrames++;
          if (_telemetry.FirstVideoOffsetMs < 0)
            _telemetry.FirstVideoOffsetMs = _clock.OffsetMs();
        }
      }
    } catch (Exception) { }
  }

  bool AcceptVideoSerial(uint serial) {
    try {
      if (_videoLatch.Accept(serial)) return true;
      _videoLatch.Reset();
      _videoLatch.Accept(serial);
      MarkInterrupted("video-serial-change");
      return true;
    } catch (Exception) { return false; }
  }

  // --- full-session gameplay capture (output side only) -----------------------
  // Display rendering is untouched (max quality, zero extra scene renders).
  // On sample ticks SampleGame asks Unity for a screen capture straight into
  // the record RT (GPU-side, free downscale to output size); the NEXT Update
  // consumes it via AsyncGPUReadback. Screen capture is the only scope in
  // which the finished frame is reliably readable: Update-scope blits read an
  // unbound target, a runtime-cloned camera broke URP's render-pass
  // bookkeeping (EndRenderPass errors + black frames), and an
  // endCameraRendering blit of CameraTarget stayed black (P2X findings).
  // Main-thread cost per sample is one 2 MB memcpy; encode NEVER touches the
  // main thread. runInBackground is forced during capture so clicking out of
  // the window never pauses the session (restored afterwards).
  void SampleGame() {
    try {
      if (!_gameCaptureReady) return;
      // Backpressure: if worker is behind (raw queue near cap), skip this tick instead of allocating 8MB that will be dropped
      try {
        if (_gameRawQueue != null && _gameRawQueue.Count >= MediaRecording.MaxGameRawFrames - 1) {
          // Count as gap (honest) and don't request another capture this tick
          return;
        }
      } catch (Exception) { }
      // Consume last tick's capture (Unity finished it at end of that
      // frame): read back, then stand down until the next tick.
      if (_shotArmed) {
        _shotArmed = false;
        try {
          lock (_telLock) { _telemetry.GameCaptureRequests++; }
          AsyncGPUReadback.Request(_recordRT, 0, TextureFormat.RGBA32, OnGameReadback);
        } catch (Exception) {
          lock (_telLock) { _telemetry.GameGapSamples++; }
        }
      }
      _gameSampleTimer += Time.deltaTime;
      // Nominal tick runs GameFps+OverHz (P30: 30.5): warmup, stop-boundary
      // partial ticks and hitch forgiveness all cost frames against a bare
      // 30.0 nominal, netting ~29.4 measured. The +0.5 nets >= 30 measured;
      // the measured rate (never nominal) stamps outputs, and the duplicate
      // detector proves every counted frame is a distinct capture.
      float tickHz = _effective.GameFps + MediaRecording.GameSampleOverHz;
      if (tickHz < 1f) tickHz = 1f;
      float interval = 1f / tickHz;
      if (_gameSampleTimer >= interval) {
        _gameSampleTimer = interval > 0 ? _gameSampleTimer - interval : 0;
        try {
          ScreenCapture.CaptureScreenshotIntoRenderTexture(_recordRT);
          _shotArmed = true;
        } catch (Exception) {
          lock (_telLock) { _telemetry.GameGapSamples++; }
        }
      }
      if (_gameSampleTimer > interval * 2) _gameSampleTimer = 0;
    } catch (Exception) { }
  }

  bool PrepareGameCapture() {
    _gameCaptureReady = false;
    try {
      ReleaseGameCapture();
      Camera cam = _boundGameCam;
      if (cam == null) {
        try { cam = Camera.main; } catch (Exception) { }
      }
      if (cam == null) return false;
      _gameCam = cam;
      _recordRT = new RenderTexture(
        _effective.GameWidth, _effective.GameHeight, 0, RenderTextureFormat.ARGB32);
      try { _recordRT.Create(); } catch (Exception) { ReleaseGameCapture(); return false; }
      if (!_recordRT.IsCreated()) { ReleaseGameCapture(); return false; }
      _shotArmed = false;
      // Capture must continue when the player clicks away from the game
      // window (user requirement): keep the player loop alive unfocused.
      try {
        _savedRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        _touchedRunInBackground = true;
      } catch (Exception) { }
      _gameCaptureReady = true;
      return true;
    } catch (Exception) {
      try { ReleaseGameCapture(); } catch (Exception) { }
      return false;
    }
  }

  void ReleaseGameCapture() {
    try {
      _gameCaptureReady = false;
      _shotArmed = false;
      _gameCam = null;
      if (_recordRT != null) {
        try {
          if (_recordRT.IsCreated()) _recordRT.Release();
        } catch (Exception) { }
        try {
          if (Application.isPlaying) Destroy(_recordRT);
          else DestroyImmediate(_recordRT);
        } catch (Exception) { }
      }
      try {
        if (_touchedRunInBackground) {
          _touchedRunInBackground = false;
          Application.runInBackground = _savedRunInBackground;
        }
      } catch (Exception) { }
    } catch (Exception) { }
    finally {
      _recordRT = null;
    }
  }

  void OnGameReadback(AsyncGPUReadbackRequest req) {
    try {
      if (_state != RecordingState.Recording || !_effective.RecordVideo) return;
      if (req.hasError) {
        lock (_telLock) { _telemetry.GameGapSamples++; }
        return;
      }
      if (_gameRawQueue == null || _gameRawQueue.IsClosed || !_gameCaptureReady) return;
      int expect = _effective.GameWidth * _effective.GameHeight * 4;
      NativeArray<byte> data;
      try { data = req.GetData<byte>(); } catch (Exception) { return; }
      if (data.Length != expect) {
        lock (_telLock) { _telemetry.GameGapSamples++; }
        return;
      }
      var copy = new byte[data.Length];
      try { data.CopyTo(copy); } catch (Exception) { return; }
      CountDarkFrame(copy);
      // Gameplay is one implicit session (screen has no serial); the latch
      // stays uniform so any foreign concept never applies here.
      _gameLatch.Accept(1);
      if (_gameRawQueue.TryEnqueue(copy)) {
        lock (_telLock) {
          if (_telemetry.FirstGameOffsetMs < 0)
            _telemetry.FirstGameOffsetMs = _clock.OffsetMs();
          // Pacing probe: interval between capture completions. Online
          // aggregates only (no per-frame lists in the sidecar).
          try {
            float now = Time.unscaledTime;
            if (_lastCaptureTickTime >= 0f) {
              double dt = (now - _lastCaptureTickTime) * 1000.0;
              if (dt >= 0 && dt <= 60000) {
                _telemetry.GamePacingN++;
                _telemetry.GamePacingSumMs += dt;
                _telemetry.GamePacingSumSqMs += dt * dt;
                if (dt > _telemetry.GamePacingMaxMs) _telemetry.GamePacingMaxMs = dt;
                if (dt > 50) _telemetry.GamePacingOver50++;
              }
            }
            _lastCaptureTickTime = now;
          } catch (Exception) { }
        }
      }
    } catch (Exception) { }
  }

  // Cheap black-frame tripwire (main thread, ~8k sampled pixels): a capture
  // path silently rendering black looks identical to success until someone
  // watches the file (P23 user-run finding). Counted, never thrown.
  void CountDarkFrame(byte[] rgba) {
    try {
      if (rgba == null || rgba.Length < 16) return;
      long sum = 0;
      int n = 0;
      for (int i = 0; i < rgba.Length; i += 1024) {
        sum += rgba[i];
        n++;
      }
      if (n > 0 && sum / n < 4) {
        lock (_telLock) { _telemetry.GameDarkFrames++; }
      }
    } catch (Exception) { }
  }

  // Game worker (background): raw RGBA -> lossless game.rawvid (direct file
  // writes, memcpy-speed — no encode stage, so one thread sustains any
  // sample rate; the threaded JPEG farm became obsolete with the raw path).
  // Owns _gameWriter. Counts and failure semantics unchanged (every dequeued
  // frame appended once in order; any refusal fails the track).
  //
  // Color contract: capture hands us RGBA32 (R at [0]); the rawvid stream is
  // BGRA on the wire (B at [0], ffmpeg rawvideo native). The old JPEG path
  // converted correctly inside JpegEncoder; raw must swizzle R<->B explicitly
  // or ffmpeg decodes red/blue swapped. In-place on the worker-owned dequeue
  // (never the shared queue slot), so the main thread pays nothing.
  static void SwizzleRgbaToBgra(byte[] px) {
    try {
      if (px == null) return;
      for (int i = 0; i + 3 < px.Length; i += 4) {
        byte r = px[i];
        px[i] = px[i + 2];
        px[i + 2] = r;
      }
    } catch (Exception) { }
  }

  void GamePump() {
    try {
      byte[] raw;
      int expect = _effective.GameWidth * _effective.GameHeight * 4;
      while (!_gameRawQueue.IsClosed || _gameRawQueue.Count > 0) {
        try {
          if (_gameRawQueue.TryDequeue(out raw)) {
            if (raw == null || raw.Length != expect) { _gameWriteFailed = true; break; }
            SwizzleRgbaToBgra(raw);
            // Duplicate-frame tripwire (anti-fake-fps): adjacent-identical
            // FILE bytes mean the pipeline re-emitted one capture twice.
            // Sample-hashed (2K samples, noise-cheap); a walking scene must
            // never trip it. Counted under the same lock as GameFrames.
            try {
              uint h = FrameSampleHash.Hash(raw, 4096);
              lock (_telLock) {
                if (_hasPrevGameHash && h != 0 && h == _prevGameHash)
                  _telemetry.GameDuplicateFrames++;
                if (h != 0) { _prevGameHash = h; _hasPrevGameHash = true; }
              }
            } catch (Exception) { }
            if (!_gameWriter.AppendFrame(raw)) {
              _gameWriteFailed = true;
              break;
            }
            lock (_telLock) { _telemetry.GameFrames++; }
          } else {
            Thread.Sleep(5);
          }
        } catch (Exception) { _gameWriteFailed = true; break; }
      }
      try {
        long frames;
        // .rawvid carries no header rate (stream, no size/rate fields by
        // design) — the measured active-span rate travels as the ffmpeg
        // -framerate input flag instead (see BuildTranscodeSpec).
        if (!_gameWriter.Finalize(out frames)) _gameWriteFailed = true;
      } catch (Exception) { _gameWriteFailed = true; }
    } catch (Exception) { _gameWriteFailed = true; }
    finally {
      try { _gameWriter.Close(); } catch (Exception) { }
      _gamePumpDone = true;
    }
  }

  // GPU-blit path: GetPixels() alloc + CPU blit caused main-thread spikes (10fps * 640x480) when moving+voice.
  // Phase 2.4: prefer Graphics.CopyTexture (no GetPixels alloc); throttle disabled to keep PIP 10fps (was 3Hz cap in 2026-09-17 fix, now honest 10fps with GPU fast path).
  // (Throttle field kept for compat but unused)
  float _localEncodeThrottle;
  byte[] EncodeLocalFrame(Texture tex) {
    WebCamTexture wct = null;
    try { wct = tex as WebCamTexture; } catch (Exception) { return null; }
    if (wct == null) return null;
    Texture2D tmp = null;
    try {
      int w = wct.width, h = wct.height;
      if (w < 16 || h < 16 || w > 1280 || h > 960) return null;
      // Prefer GPU path: blit without GetPixels alloc when possible
      try {
        if (wct.isPlaying) {
          tmp = new Texture2D(w, h, TextureFormat.RGB24, false);
          // Fast path: CopyTexture if same format, else Graphics.Blit fallback still cheaper than GetPixels
          try { Graphics.CopyTexture(wct, tmp); } catch (Exception) {
            var px32 = wct.GetPixels32();
            var px = new Color[px32.Length];
            for (int i = 0; i < px32.Length; i++) px[i] = (Color)px32[i];
            tmp.SetPixels(px);
          }
          tmp.Apply(false, false);
          return ImageConversion.EncodeToJPG(tmp, Math.Min(_effective.VideoJpegQuality, 50));
        }
      } catch (Exception) { }
      tmp = new Texture2D(w, h, TextureFormat.RGB24, false);
      tmp.SetPixels(wct.GetPixels());
      tmp.Apply();
      return ImageConversion.EncodeToJPG(tmp, _effective.VideoJpegQuality);
    } catch (Exception) { return null; }
    finally {
      try {
        if (tmp != null) {
          if (Application.isPlaying) Destroy(tmp);
          else DestroyImmediate(tmp);
        }
      } catch (Exception) { }
    }
  }

  // Worker-thread audio tap (game-observed bytes only, §3). Copies into the
  // bounded queue synchronously and returns — never blocks the watcher.
  void OnAudioTap(uint serial, uint seq, byte[] pcm16) {    try {
      if (_state != RecordingState.Recording) return;
      if (!_effective.RecordAudio || _audioQueue == null || _audioQueue.IsClosed) return;
      if (pcm16 == null || pcm16.Length < 2) return;
      if (pcm16.Length > 1024 * 1024) return; // sanity: ~32 s in one chunk is not real
      lock (_telLock) {
        if (!_audioLatch.Accept(serial)) {
          _audioLatch.Reset();
          _audioLatch.Accept(serial);
          _telemetry.Interrupted = true;
        }
      }
      if (_audioQueue.TryEnqueue(pcm16)) {
        _phoneFed = true;
        NoteAudioOrigin("phone");
        lock (_telLock) {
          _telemetry.AudioChunks++;
          _telemetry.AudioSamples += pcm16.Length / 2;
          if (_telemetry.FirstAudioOffsetMs < 0)
            _telemetry.FirstAudioOffsetMs = _clock.OffsetMs();
        }
      }
    } catch (Exception) { }
  }

  // --- laptop-mic capture (main thread polls, worker-free path) ----------------
  // Polled every Update while recording audio: cheap position check, copies
  // only when >=10 ms of new speech exist. Phone flowing => park local.

  void PollLocalMic() {
    try {
      if (!_effective.RecordAudio) return;
      // Local is a first-class source (user rule): feed whenever the
      // game-validated device is available, phone or not. The phone tap
      // parks itself while local feeds (OnAudioTap), so no mixing.
      if (!_localMicActive) {
        _localMicRepickTimer -= Time.deltaTime;
        if (_localMicRepickTimer <= 0f) {
          _localMicRepickTimer = 2f;
          TryStartLocalMic();
        }
        return;
      }
      if (!LocalDeviceStillThere()) {
        OnLocalMicLost();
        return;
      }
      DrainLocalMic();
    } catch (Exception) { }
  }

  void TryStartLocalMic() {
    try {
      if (_audioQueue == null || _audioQueue.IsClosed) return;
      string dev = null;
      try { dev = _localMicDevice != null ? _localMicDevice() : null; } catch (Exception) { }
      if (string.IsNullOrEmpty(dev)) return; // game-validated device only (no raw pick)
      AudioClip clip = null;
      try { clip = Microphone.Start(dev, true, 10, 16000); } catch (Exception) { return; }
      if (clip == null) return;
      int ch = 1, rate = 16000;
      try { ch = Math.Max(1, clip.channels); } catch (Exception) { }
      try { rate = clip.frequency > 0 ? clip.frequency : 16000; } catch (Exception) { }
      _localMicClip = clip;
      _localMicActiveDevice = dev;
      _localMicPos = 0;
      _localMicLoopFrames = Math.Max(1600, clip.samples);
      _localMicChannels = Math.Min(8, ch);
      _localMicRate = rate;
      _localMicActive = true;
      _localMicFed = false;
      _localMicRepickTimer = 2f;
      _localLatch.Reset();
      _localLatch.Accept(1);
      lock (_telLock) { _telemetry.LocalMicDevice = dev; }
      try {
        Debug.Log("[MediaRec] local mic feeding: " + dev + " @" + rate + "Hz ch=" + ch);
      } catch (Exception) { }
    } catch (Exception) { }
  }

  bool LocalDeviceStillThere() {
    try {
      if (!_localMicActive || _localMicClip == null || string.IsNullOrEmpty(_localMicActiveDevice))
        return false;
      bool rec = false;
      try { rec = Microphone.IsRecording(_localMicActiveDevice); } catch (Exception) { return false; }
      return rec;
    } catch (Exception) { return false; }
  }

  void OnLocalMicLost() {
    try {
      bool fed = _localMicFed;
      StopLocalMic();
      if (fed) {
        MarkInterrupted("local-mic-lost");
        try { Debug.LogWarning("[MediaRec] local mic lost mid-session — audio truncated"); }
        catch (Exception) { }
      }
    } catch (Exception) { }
  }

  void StopLocalMic() {
    try {
      string dev = _localMicActiveDevice;
      _localMicActive = false;
      _localMicClip = null;
      _localMicActiveDevice = null;
      if (!string.IsNullOrEmpty(dev)) {
        try { Microphone.End(dev); } catch (Exception) { }
      }
    } catch (Exception) { }
  }

  void DrainLocalMic() {
    try {
      if (!_localMicActive || _localMicClip == null || _audioQueue.IsClosed) return;
      int pos;
      try { pos = Microphone.GetPosition(_localMicActiveDevice); } catch (Exception) {
        OnLocalMicLost();
        return;
      }
      if (pos < 0) {
        OnLocalMicLost();
        return;
      }
      int loop = Math.Max(1600, _localMicLoopFrames);
      int avail = (pos - _localMicPos + loop) % loop;
      if (avail < 160) return; // <10 ms @16k: wait for more
      int frames = Math.Min(avail, 3200); // <=200 ms per poll
      int ch = Math.Max(1, _localMicChannels);
      var buf = new float[frames * ch];
      int start = _localMicPos % loop;
      try {
        if (start + frames <= loop) {
          _localMicClip.GetData(buf, start);
        } else {
          int first = loop - start;
          var a = new float[first * ch];
          var b = new float[(frames - first) * ch];
          _localMicClip.GetData(a, start);
          _localMicClip.GetData(b, 0);
          Buffer.BlockCopy(a, 0, buf, 0, a.Length * 4);
          Buffer.BlockCopy(b, 0, buf, a.Length * 4, b.Length * 4);
        }
      } catch (Exception) {
        OnLocalMicLost();
        return;
      }
      _localMicPos = (start + frames) % loop;
      byte[] pcm;
      try { pcm = AudioChunkConverter.ToMono16(buf, ch, _localMicRate, 16000); }
      catch (Exception) { return; }
      if (pcm == null || pcm.Length == 0) return;
      _localLatch.Accept(1);
      if (_audioQueue.TryEnqueue(pcm)) {
        _localMicFed = true;
        NoteAudioOrigin("local");
        lock (_telLock) {
          _telemetry.AudioChunks++;
          _telemetry.AudioSamples += pcm.Length / 2;
          _telemetry.LocalMicChunks++;
          if (_telemetry.FirstAudioOffsetMs < 0)
            _telemetry.FirstAudioOffsetMs = _clock.OffsetMs();
        }
      }
    } catch (Exception) { }
  }

  void NoteAudioOrigin(string which) {
    try {
      lock (_telLock) {
        string cur = _telemetry.AudioOrigin;
        if (string.IsNullOrEmpty(cur)) {
          _telemetry.AudioOrigin = which;
        } else if (!string.Equals(cur, which, StringComparison.Ordinal)
            && !string.Equals(cur, "switched", StringComparison.Ordinal)) {
          _telemetry.AudioOrigin = "switched";
          _telemetry.AudioSourceSwitches++;
          _telemetry.Interrupted = true;
        }
      }
    } catch (Exception) { }
  }

  void WatchAudioPresence() {
    try {
      if (!_effective.RecordAudio) return;
      // A missing watcher is normal when the phone never fed this session
      // (local-mic rigs): flag ONLY a mid-session phone drop, never the
      // mere absence (P23 user-run finding: every local session was wrongly
      // marked interrupted).
      if (CurrentWatcher() == null && _phoneFed) MarkInterrupted("audio-watcher-lost");
    } catch (Exception) { }
  }

  void MarkInterrupted(string why) {
    try {
      lock (_telLock) {
        if (!_telemetry.Interrupted) {
          _telemetry.Interrupted = true;
          if (!_interruptedLogged) {
            _interruptedLogged = true;
            try { Debug.Log("[MediaRec] session=" + _sessionId + " interrupted (" + why + ") — partial flags apply"); }
            catch (Exception) { }
          }
        }
      }
    } catch (Exception) { }
  }

  // --- stopping pump (main thread; workers finalize their own writers) -------
  void PumpStopping() {
    try {
      bool audioDone = !_effective.RecordAudio || _audioPumpDone;
      bool videoDone = !_effective.RecordVideo || _videoPumpDone;
      bool gameDone = !_effective.RecordVideo || _gamePumpDone;
      if (!(audioDone && videoDone && gameDone)) {
        // Bounded wait: never hang the game on a stuck pump (§31).
        double waitSec = 0;
        try { waitSec = (DateTime.UtcNow - _stopUtc).TotalSeconds; } catch (Exception) { }
        if (waitSec > FinalizeTimeoutSec) {
          FailFinalize("finalize-timeout");
        }
        return;
      }
      if (!_intermediatesVerified) {
        _intermediatesVerified = true;
        SetState(RecordingState.Finalizing);
        VerifyIntermediates();
        MaybeStartTranscode();
        if (_transcodeStarted) return; // transcode thread owns the wait now
      }
      if (_transcodeStarted && !_transcodeDone) {
        double tsec = 0;
        try { tsec = (DateTime.UtcNow - _transcodeStartUtc).TotalSeconds; } catch (Exception) { }
        if (tsec > (_transcodeTimeoutMs / 1000 + 60)) FailFinalize("transcode-stall");
        return;
      }
      FinishSession();
    } catch (Exception) { FailFinalize("finalize-exception"); }
  }

  // Intermediates: queue counters + latch foreigns + header verifies.
  // No sidecar yet — the transcode stage may still add deliverables.
  void VerifyIntermediates() {
    try {
      long enq, drop, deq;
      int cnt;
      if (_effective.RecordAudio && _audioQueue != null) {
        try {
          _audioQueue.ReadCounters(out enq, out drop, out deq, out cnt);
          lock (_telLock) { _telemetry.AudioDroppedQueue = drop; }
        } catch (Exception) { }
      }
      if (_effective.RecordVideo && _videoQueue != null) {
        try {
          _videoQueue.ReadCounters(out enq, out drop, out deq, out cnt);
          lock (_telLock) { _telemetry.VideoDroppedQueue = drop; }
        } catch (Exception) { }
      }
      if (_effective.RecordVideo && _gameRawQueue != null) {
        try {
          _gameRawQueue.ReadCounters(out enq, out drop, out deq, out cnt);
          lock (_telLock) { _telemetry.GameDroppedRaw = drop; }
        } catch (Exception) { }
      }
      lock (_telLock) {
        _telemetry.AudioDroppedForeign = _audioLatch.DroppedForeign;
        _telemetry.VideoDroppedForeign = _videoLatch.DroppedForeign;
        _telemetry.GameDroppedForeign = _gameLatch.DroppedForeign;
      }

      bool audioOk = true, videoOk = true, gameOk = true;
      if (_effective.RecordAudio) audioOk = VerifyAudioFile();
      if (_effective.RecordVideo) {
        videoOk = VerifyVideoFile();
        gameOk = VerifyGameFile();
      }
      lock (_telLock) {
        _telemetry.AudioComplete = _effective.RecordAudio && audioOk;
        _telemetry.VideoComplete = _effective.RecordVideo && videoOk;
        _telemetry.GameComplete = _effective.RecordVideo && gameOk;
      }
    } catch (Exception) { }
  }

  // Transcode stage: intermediates -> MP4 (+MP3) on a worker thread.
  // Missing ffmpeg is NOT an error (verified intermediates ARE the honest
  // fallback, §10); a FAILED transcode run IS (clear error, §32).
  void MaybeStartTranscode() {
    _transcodeStarted = false;
    try {
      if (_testDisableTranscode) return;
      TranscodeSpec spec = BuildTranscodeSpec();
      if (string.IsNullOrEmpty(spec.OutMp4) && string.IsNullOrEmpty(spec.OutMp3)) return;
      string ffmpeg = FfmpegTranscodeBackend.FindExecutable(
        _effective.FfmpegPathOverride, ExeDir(), _repoToolsDir, _appToolsDir);
      if (string.IsNullOrEmpty(ffmpeg)) {
        lock (_telLock) {
          _telemetry.Transcoded = false;
          _telemetry.TranscodeError = "ffmpeg-not-found (intermediates kept)";
        }
        try { Debug.Log("[MediaRec] ffmpeg not found — session keeps verified wav/avi intermediates"); }
        catch (Exception) { }
        return;
      }
      double dur = SessionDurationSec();
      _transcodeTimeoutMs = Math.Max(120000, (int)(Math.Max(1, dur) * 4000));
      _transcodeResult = new TranscodeResult();
      _transcodeStartUtc = DateTime.UtcNow;
      _transcodeStarted = true;
      _transcodeDone = false;
      var t = new Thread(() => TranscodePump(ffmpeg, spec)) {
        IsBackground = true, Name = "MediaRecTranscode"
      };
      _transcodeThread = t;
      try {
        Debug.Log("[MediaRec] transcode start (" + ffmpeg + ") session=" + _sessionId);
      } catch (Exception) { }
      // Forensics (no behavior change): the exact encoder invocation, so any
      // output file can be traced back to its arguments from the log alone.
      try {
        Debug.Log("[MediaRec] ffmpeg args: " + FfmpegTranscodeBackend.BuildArguments(spec));
      } catch (Exception) { }
      t.Start();
    } catch (Exception) { _transcodeStarted = false; }
  }

  void TranscodePump(string ffmpeg, TranscodeSpec spec) {
    DateTime t0 = DateTime.UtcNow;
    try {
      _transcodeResult = FfmpegTranscodeBackend.Run(ffmpeg, spec, _transcodeTimeoutMs);
    } catch (Exception e) {
      _transcodeResult = new TranscodeResult { Error = "pump:" + e.GetType().Name };
    } finally {
      // Encoder throughput (offline x264: informational, never gating):
      // game frames per transcode second.
      try {
        double ms = (DateTime.UtcNow - t0).TotalMilliseconds;
        if (ms < 0) ms = 0;
        lock (_telLock) {
          _telemetry.TranscodeElapsedMs = (long)ms;
          long gf = _telemetry.GameFrames;
          _telemetry.TranscodeFps = (gf > 0 && ms > 0) ? gf / (ms / 1000.0) : 0;
        }
      } catch (Exception) { }
      _transcodeDone = true;
    }
  }

  TranscodeSpec BuildTranscodeSpec() {
    var s = new TranscodeSpec();
    try {
      bool haveGame, haveCam, haveAudio;
      lock (_telLock) {
        haveGame = _telemetry.GameComplete;
        haveCam = _telemetry.VideoComplete;
        haveAudio = _telemetry.AudioComplete;
      }
      if (haveGame) s.GameAvi = _gamePath;
      if (haveCam) s.CamAvi = _videoPath;
      if (haveAudio) s.MicWav = _audioPath;
      if (haveGame || haveCam) s.OutMp4 = _mp4Path;
      if (haveAudio) s.OutMp3 = _mp3Path;
      double dur = SessionDurationSec();
      lock (_telLock) {
        long gf = _telemetry.GameFrames, vf = _telemetry.VideoFrames;
        // Game rate anchors on the ACTIVE span (first game frame -> stop):
        // start-anchored walls include ~0.3 s of capture warmup with zero
        // frames and understate a steady 30 Hz cadence as ~29.4 (P30: pacing
        // mean 33.5 ms proved the cadence while the wall said 29.37).
        s.GameFpsActual = RecordingRates.ActiveStreamFps(
          gf, dur, _telemetry.FirstGameOffsetMs);
        if (s.GameFpsActual <= 0) s.GameFpsActual = _effective.GameFps;
        s.CamFpsActual = dur > 0.5 ? vf / dur : _effective.VideoFps;
        s.GameFrames = gf;
      }
      s.GameWidth = _effective.GameWidth;
      s.GameHeight = _effective.GameHeight;
      s.PipWidth = _effective.PipWidth;
      s.PipMargin = _effective.PipMargin;
      s.Crf = _effective.VideoCrf;
      s.Preset = _effective.VideoPreset;
      s.Mp3Quality = _effective.AudioMp3Quality;
    } catch (Exception) { }
    return s;
  }

  double SessionDurationSec() {
    try {
      DateTime stop = _stopUtc == DateTime.MinValue ? DateTime.UtcNow : _stopUtc;
      return Math.Max(0, (stop - _clock.StartUtc).TotalSeconds);
    } catch (Exception) { return 0; }
  }

  string ExeDir() {
    try {
      using (var p = System.Diagnostics.Process.GetCurrentProcess()) {
        string exe = p.MainModule.FileName;
        return Path.GetDirectoryName(exe);
      }
    } catch (Exception) { return null; }
  }

  void FinishSession() {
    try {
      bool ioOk = !_audioWriteFailed && !_videoWriteFailed && !_gameWriteFailed;
      bool transcodeFailed = false;
      if (_transcodeStarted && _transcodeDone) {
        var r = _transcodeResult;
        lock (_telLock) {
          _telemetry.FfmpegVersion = r.FfmpegVersion ?? string.Empty;
          _telemetry.TranscodeError = r.Ok ? string.Empty : (r.Error ?? "transcode-failed");
          _telemetry.Transcoded = r.Ok;
        }
        if (r.Ok) {
          CheckDeliverable(_mp4Path, true);
          CheckDeliverable(_mp3Path, false);
          MaybeDeleteIntermediates();
        } else {
          transcodeFailed = true;
        }
      }
      lock (_telLock) {
        if ((!_telemetry.AudioComplete && _effective.RecordAudio)
            || (!_telemetry.VideoComplete && _effective.RecordVideo)
            || (!_telemetry.GameComplete && _effective.RecordVideo))
          _telemetry.Interrupted = true;
        _telemetry.State = RecordingState.Completed;
        // Process CPU % over the session wall (probe-grade: TotalProcessorTime
        // deltas; GPU has no vendor API in-player, render-ms is the proxy).
        try {
          double cpuNow = CpuUtil.ProcessCpuSec();
          double wall = SessionDurationSec() - _cpuWallStartSec;
          int cores = 1;
          try { cores = System.Environment.ProcessorCount; } catch (Exception) { }
          _telemetry.ProcessCpuPct = CpuUtil.Pct(cpuNow - _cpuStartSec, wall, cores);
        } catch (Exception) { }
      }
      WriteSidecar();

      if (!ioOk) {
        FailFinalize(_audioWriteFailed && _videoWriteFailed ? "write-failed:av"
          : _audioWriteFailed ? "write-failed:audio"
          : _videoWriteFailed ? "write-failed:video" : "write-failed:game");
        return;
      }
      if (transcodeFailed) {
        FailFinalize("transcode-failed");
        return;
      }
      SetState(RecordingState.Completed);
      try {
        Debug.Log("[MediaRec] COMPLETE session=" + _sessionId
          + " audio=" + (_effective.RecordAudio ? _telemetry.AudioSamples + "samples/" + _telemetry.AudioBytes + "B" : "<off>")
          + " cam=" + (_effective.RecordVideo ? _telemetry.VideoFrames + "frames" : "<off>")
          + " game=" + (_effective.RecordVideo ? _telemetry.GameFrames + "frames" : "<off>")
          + " mp4=" + (_telemetry.Transcoded ? _telemetry.Mp4Bytes + "B" : "<" + _telemetry.TranscodeError + ">")
          + " mp3=" + (_telemetry.Mp3Complete ? _telemetry.Mp3Bytes + "B" : "<off>")
          + " interrupted=" + _telemetry.Interrupted);
      } catch (Exception) { }
      // Real-fps stage line (log evidence for the fps gate; same numbers as
      // the sidecar, one line for grep): source/render, capture requests,
      // measured game rate, pacing, duplicates, CPU, encoder throughput.
      try {
        double wall = SessionDurationSec();
        if (wall < 0.5) wall = 0.5;
        double renderFps = _telemetry.RenderFrames / wall;
        double reqFps = _telemetry.GameCaptureRequests / wall;
        double gameFps = _telemetry.GameFrames / wall;
        double paceMean = _telemetry.GamePacingN > 0
          ? _telemetry.GamePacingSumMs / _telemetry.GamePacingN : 0;
        Debug.Log("[MediaRec] FPS session=" + _sessionId
          + " render=" + renderFps.ToString("0.0") + "fps"
          + " capReq=" + reqFps.ToString("0.0") + "fps"
          + " game=" + gameFps.ToString("0.0") + "fps"
          + " paceMean=" + paceMean.ToString("0.0") + "ms"
          + " paceMax=" + _telemetry.GamePacingMaxMs.ToString("0.0") + "ms"
          + " over50=" + _telemetry.GamePacingOver50
          + " gaps=" + _telemetry.GameGapSamples
          + " drops=" + (_telemetry.GameDroppedRaw + _telemetry.GameDroppedQueue)
          + " dup=" + _telemetry.GameDuplicateFrames
          + " cpu=" + _telemetry.ProcessCpuPct.ToString("0.0") + "%"
          + " x264=" + _telemetry.TranscodeFps.ToString("0.0") + "fps");
      } catch (Exception) { }
      // Plain log (never a warning/error: those pop the dev-player console
      // overlay on screen): every captured gameplay frame was near-black, so
      // the file's main video is blind even though the session completed.
      try {
        if (_effective.RecordVideo && _telemetry.GameFrames > 0
            && _telemetry.GameDarkFrames >= _telemetry.GameFrames)
          Debug.Log("[MediaRec] NOTICE session=" + _sessionId
            + " all " + _telemetry.GameFrames + " gameplay frames dark — check capture path");
      } catch (Exception) { }
      ShowCompletedToast();
    } catch (Exception) { FailFinalize("verify-exception"); }
  }

  // "Đã lưu file ở đâu + tên file" (§user-3): filenames + dir, never full
  // paths on screen (the log + sidecar keep those).
  void ShowCompletedToast() {
    try {
      var names = new System.Collections.Generic.List<string>();
      string dir = null;
      try {
        lock (_telLock) {
          if (_telemetry.Transcoded) {
            if (_telemetry.Mp4Complete) names.Add(BaseName(_telemetry.Mp4Path));
            if (_telemetry.Mp3Complete) names.Add(BaseName(_telemetry.Mp3Path));
          } else {
            if (_telemetry.AudioComplete) names.Add(BaseName(_telemetry.AudioPath));
            if (_telemetry.VideoComplete) names.Add(BaseName(_telemetry.VideoPath));
            if (_telemetry.GameComplete) names.Add(BaseName(_telemetry.GamePath));
          }
        }
        dir = _outputDir;
      } catch (Exception) { }
      if (names.Count == 0) names.Add("(không có file hoàn chỉnh)");
      string head = "Đã lưu xong:";
      bool partial = false;
      try {
        lock (_telLock) {
          partial = !_telemetry.Transcoded && _effective.RecordVideo;
        }
      } catch (Exception) { }
      if (partial) head = "Đã lưu file thô (thiếu FFmpeg):";
      ShowToast(head + "\n" + string.Join("\n", names.ToArray()) + "\ntại " + (dir ?? "?"), 8f);
    } catch (Exception) { }
  }

  void CheckDeliverable(string path, bool isMp4) {
    try {
      long size = 0;
      bool ok = false;
      if (!string.IsNullOrEmpty(path)) {
        var fi = new FileInfo(path);
        if (fi.Exists && fi.Length > 0) {
          size = fi.Length;
          ok = true;
        }
      }
      lock (_telLock) {
        if (isMp4) { _telemetry.Mp4Complete = ok; _telemetry.Mp4Bytes = size; }
        else { _telemetry.Mp3Complete = ok; _telemetry.Mp3Bytes = size; }
      }
    } catch (Exception) { }
  }

  void MaybeDeleteIntermediates() {
    try {
      if (!_telemetry.Transcoded || _effective.KeepIntermediates) return;
      foreach (string p in new[] { _audioPath, _videoPath, _gamePath }) {
        try { if (!string.IsNullOrEmpty(p) && File.Exists(p)) File.Delete(p); }
        catch (Exception) { }
      }
      try { Debug.Log("[MediaRec] intermediates deleted after transcode session=" + _sessionId); }
      catch (Exception) { }
    } catch (Exception) { }
  }

  bool VerifyAudioFile() {
    try {
      if (string.IsNullOrEmpty(_audioPath)) return false;
      var fi = new FileInfo(_audioPath);
      lock (_telLock) { _telemetry.AudioBytes = fi.Exists ? fi.Length : 0; }
      if (!fi.Exists || fi.Length <= 0) return false;
      WavWriter.WavInfo info;
      if (!WavWriter.TryReadInfo(_audioPath, out info) || !info.Valid) return false;
      if (info.SampleRate != MediaRecording.AudioSampleRate || info.Channels != 1) return false;
      return info.SampleCount > 0;
    } catch (Exception) { return false; }
  }

  bool VerifyVideoFile() {
    try {
      if (string.IsNullOrEmpty(_videoPath)) return false;
      var fi = new FileInfo(_videoPath);
      lock (_telLock) { _telemetry.VideoBytes = fi.Exists ? fi.Length : 0; }
      if (!fi.Exists || fi.Length <= 0) return false;
      AviMjpegWriter.AviInfo info;
      if (!AviMjpegWriter.TryReadInfo(_videoPath, out info) || !info.Valid) return false;
      if (info.Width != _effective.VideoWidth || info.Height != _effective.VideoHeight) return false;
      if (info.FrameCount <= 0) return false;
      // Real decode spot-checks: first, middle, last stills must resolve to
      // JPEG SOI payloads (not just index rows).
      int n = (int)Math.Min(info.FrameCount, int.MaxValue);
      int[] picks = { 0, n / 2, n - 1 };
      foreach (int p in picks) {
        byte[] jpeg;
        string why;
        if (!AviMjpegWriter.TryExtractFrame(_videoPath, p, out jpeg, out why)) return false;
        if (jpeg == null || jpeg.Length == 0) return false;
      }
      return true;
    } catch (Exception) { return false; }
  }

  bool VerifyGameFile() {
    try {
      if (string.IsNullOrEmpty(_gamePath)) return false;
      var fi = new FileInfo(_gamePath);
      lock (_telLock) { _telemetry.GameBytes = fi.Exists ? fi.Length : 0; }
      if (!fi.Exists || fi.Length <= 0) return false;
      RawVideoReader.RawInfo info;
      if (!RawVideoReader.TryReadInfo(_gamePath, out info) || !info.Valid) return false;
      if (info.Width != _effective.GameWidth || info.Height != _effective.GameHeight) return false;
      if (info.FrameCount <= 0) return false;
      int n = info.FrameCount > int.MaxValue ? int.MaxValue : (int)info.FrameCount;
      // Lossless path: exact-size BGRA payloads at computed offsets.
      foreach (int p in new[] { 0, n / 2, n - 1 }) {
        byte[] raw;
        string why;
        if (!RawVideoReader.TryExtractFrame(_gamePath, p,
            _effective.GameWidth, _effective.GameHeight, out raw, out why)) return false;
        if (raw == null || raw.Length != info.FrameBytes) return false;
      }
      return true;
    } catch (Exception) { return false; }
  }

  void WriteSidecar() {
    try {
      if (string.IsNullOrEmpty(_sidecarPath)) return;
      string json;
      lock (_telLock) { json = _telemetry.ToJson(); }
      File.WriteAllText(_sidecarPath, json);
    } catch (Exception) {
      try { Debug.LogWarning("[MediaRec] sidecar write failed for session=" + _sessionId); }
      catch (Exception) { }
    }
  }

  // --- worker pumps (background threads; file I/O only) ----------------------
  void AudioPump() {
    try {
      byte[] chunk;
      while (!_audioQueue.IsClosed || _audioQueue.Count > 0) {
        try {
          if (_audioQueue.TryDequeue(out chunk)) {
            int n;
            if (!_audioWriter.AppendPcm16(chunk)) { _audioWriteFailed = true; break; }
          } else {
            Thread.Sleep(5);
          }
        } catch (Exception) { _audioWriteFailed = true; break; }
      }
      try {
        long dataBytes;
        if (!_audioWriter.Finalize(out dataBytes)) _audioWriteFailed = true;
      } catch (Exception) { _audioWriteFailed = true; }
    } catch (Exception) { _audioWriteFailed = true; }
    finally {
      try { _audioWriter.Close(); } catch (Exception) { }
      _audioPumpDone = true;
    }
  }

  void VideoPump() {
    try {
      byte[] frame;
      while (!_videoQueue.IsClosed || _videoQueue.Count > 0) {
        try {
          if (_videoQueue.TryDequeue(out frame)) {
            if (!_videoWriter.AppendJpeg(frame)) { _videoWriteFailed = true; break; }
          } else {
            Thread.Sleep(5);
          }
        } catch (Exception) { _videoWriteFailed = true; break; }
      }
      try {
        long frames;
        double vFps = MeasuredStreamFps(_videoWriter != null ? _videoWriter.FrameCount : 0);
        if (!_videoWriter.Finalize(out frames, vFps)) _videoWriteFailed = true;
      } catch (Exception) { _videoWriteFailed = true; }
    } catch (Exception) { _videoWriteFailed = true; }
    finally {
      try { _videoWriter.Close(); } catch (Exception) { }
      _videoPumpDone = true;
    }
  }

  // Measured stream rate (frames/wall) for the AVI header re-stamp: without
  // it a short-count file plays SHORTER than the wall session (duration =
  // frames/header-rate) and -shortest truncates the sibling streams. Returns
  // 0 (= keep the declared rate) when the wall is too short to mean anything
  // (unit-speed sessions) or unset. Never throws, never negative.
  double MeasuredStreamFps(long frames) {
    try {
      if (frames <= 0) return 0;
      DateTime stop = _stopUtc == DateTime.MinValue ? DateTime.UtcNow : _stopUtc;
      double wall = 0;
      try { wall = (stop - _clock.StartUtc).TotalSeconds; } catch (Exception) { }
      if (wall < 1.0) return 0;
      double fps = frames / wall;
      if (fps < 1 || fps > 120) return 0;
      return fps;
    } catch (Exception) { return 0; }
  }

  // --- availability gates (§3: game-first, explicit refusal) ------------------
  bool IsPhoneAudioInGame() {
    try {
      if (_testForceSources) return true;
      var w = CurrentWatcher();
      if (w == null) return false;
      long frames, bytes;
      float energy;
      int lastBytes, ageMs;
      w.ReadAudioStats(out frames, out bytes, out energy, out lastBytes, out ageMs);
      return lastBytes > 0 && ageMs >= 0 && ageMs <= 3000;
    } catch (Exception) { return false; }
  }

  bool IsCameraInGame() {
    try {
      if (_testForceSources) return true;
      try {
        if (_localCam != null && _localCam.HasLiveTexture) return true;
      } catch (Exception) { }
      try {
        if (_phoneCam != null) {
          uint serial, seq;
          byte[] jpeg;
          int age;
          if (_phoneCam.TryPeekAcceptedJpegForRecording(out serial, out seq, out jpeg, out age))
            return jpeg != null && age >= 0 && age <= PhoneFreshnessMs;
        }
      } catch (Exception) { }
      return false;
    } catch (Exception) { return false; }
  }

  PhonePresenceWatcher CurrentWatcher() {
    try {
      if (_audioSource == null) return null;
      return _audioSource();
    } catch (Exception) { return null; }
  }

  // Machine evidence for the sidecar (per-machine sizing traceability).
  // Never throws; 0 = unknown.
  static int CpuCountNow() {
    try {
      int c = System.Environment.ProcessorCount;
      return c > 0 ? c : 0;
    } catch (Exception) { return 0; }
  }

  string ResolveOutputDir() {
    try {
      if (!string.IsNullOrEmpty(_effective.OutputDirOverride)) return _effective.OutputDirOverride;      // Remembered choice wins over the hardcoded default (F2/F4 flow).
      try {
        string saved = SavedOutputDir();
        if (!string.IsNullOrEmpty(saved)) return saved;
      } catch (Exception) { }
      if (!string.IsNullOrEmpty(_outputBaseDir)) return _outputBaseDir;
      string base_;
      try { base_ = Application.persistentDataPath; } catch (Exception) { return null; }
      if (string.IsNullOrEmpty(base_)) return null;
      return Path.Combine(base_, "MediaRecordings");
    } catch (Exception) { return null; }
  }

  // --- state/error plumbing ----------------------------------------------------
  void ResetSessionState(RecordingMode mode) {
    try {
      lock (_telLock) {
        _telemetry = new RecordingTelemetry {
          SessionId = _sessionId,
          Mode = mode,
          State = RecordingState.Starting,
          StartUtcIso = RecordingClock.ToIso(_clock.StartUtc),
          AudioPath = _audioPath,
          VideoPath = _videoPath,
          GamePath = _gamePath,
          Mp4Path = _mp4Path,
          Mp3Path = _mp3Path,
          VideoWidth = _effective.VideoWidth,
          VideoHeight = _effective.VideoHeight,
          VideoFps = _effective.VideoFps,
          GameWidth = _effective.GameWidth,
          GameHeight = _effective.GameHeight,
          GameFps = _effective.GameFps,
          Quality = _effective.Quality.ToString(),
          VideoCrf = _effective.VideoCrf,
          VideoPreset = _effective.VideoPreset,
          GameEncodeThreads = 1, // raw path: single writer thread, memcpy-speed
          CpuCount = CpuCountNow(),
        };
        _lastError = string.Empty;
      }
      _audioLatch.Reset();
      _videoLatch.Reset();
      _gameLatch.Reset();
      _localLatch.Reset();
      _phoneFed = false;
      try { StopLocalMic(); } catch (Exception) { }
      _localMicFed = false;
      _localMicRepickTimer = 0f;
      _audioQueue = null;
      _videoQueue = null;
      _gameRawQueue = null;
      _videoSampleTimer = 0;
      _gameSampleTimer = 0;
      _lastCaptureTickTime = -1f;
      _prevGameHash = 0;
      _hasPrevGameHash = false;
      _cpuStartSec = 0;
      _cpuWallStartSec = 0;
      _interruptedLogged = false;
      _intermediatesVerified = false;
      _transcodeStarted = false;
      _transcodeDone = false;
      _transcodeResult = new TranscodeResult();
      _stopUtc = DateTime.MinValue;
    } catch (Exception) { }
  }

  bool FailStart(string reason) {
    try {
      try {
        var w = CurrentWatcher();
        if (w != null) {
          try { w.AudioPayloadAccepted -= OnAudioTap; } catch (Exception) { }
        }
      } catch (Exception) { }
      try { if (_audioWriter != null) _audioWriter.Close(); } catch (Exception) { }
      try { if (_videoWriter != null) _videoWriter.Close(); } catch (Exception) { }
      try { if (_gameWriter != null) _gameWriter.Close(); } catch (Exception) { }
      try { ReleaseGameCapture(); } catch (Exception) { }
      _audioWriter = null;
      _videoWriter = null;
      _gameWriter = null;
      lock (_telLock) {
        _lastError = reason ?? "start-failed";
        _telemetry.Error = _lastError;
        _telemetry.State = RecordingState.Error;
      }
      SetState(RecordingState.Error);
      try { Debug.LogWarning("[MediaRec] START FAILED (" + reason + ")"); }
      catch (Exception) { }
      ShowToast("Thu thất bại: " + FriendlyReason(reason), 7f);
    } catch (Exception) { }
    return false;
  }

  void FailFinalize(string reason) {
    try {
      lock (_telLock) {
        _lastError = reason ?? "finalize-failed";
        _telemetry.Error = _lastError;
        _telemetry.State = RecordingState.Error;
      }
      WriteSidecar();
      SetState(RecordingState.Error);
      try { Debug.LogWarning("[MediaRec] session=" + _sessionId + " ERROR (" + reason + ")"); }
      catch (Exception) { }
      ShowToast("Thu thất bại: " + FriendlyReason(reason), 7f);
    } catch (Exception) { }
  }

  void SetState(RecordingState s) {
    try {
      _state = s;
      lock (_telLock) { _telemetry.State = s; }
    } catch (Exception) { }
  }

  void SetError(string reason) {
    try { lock (_telLock) { _lastError = reason ?? string.Empty; } } catch (Exception) { }
  }

  // --- dev/E2E key control (§20: simple explicit start/stop) ------------------
  // F2 toggles recording. First F2 with no remembered save location opens
  // the chooser; afterwards single F2 starts immediately and DOUBLE F2
  // (two presses within the tracker window) re-opens the chooser instead.
  // Stopping is always instant — the hold applies to starts only.
  void PollRecordKey() {
    try {
      if (!_keyControl) return;
      bool f2 = false;
      try {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null) f2 = kb.f2Key.wasPressedThisFrame;
      } catch (Exception) { }
      if (f2) HandleRecordKey();
    } catch (Exception) { }
  }

  bool IsStartableState() {
    try {
      return _state == RecordingState.Idle || _state == RecordingState.Completed
        || _state == RecordingState.Error;
    } catch (Exception) { return false; }
  }

  void HandleRecordKey() {
    try {
      if (_locationDialog != null && _locationDialog.IsShowing) return;
      if (_state == RecordingState.Recording) {
        try { _f2.Cancel(); } catch (Exception) { }
        StopRecording();
        return;
      }
      if (!IsStartableState()) return;
      if (_locationDialog != null) {
        F2PressResult r;
        try { r = _f2.Press(Time.unscaledTime, HasChosenOutput()); }
        catch (Exception) { r = F2PressResult.OpenChooser; }
        if (r == F2PressResult.OpenChooser) {
          string err = null;
          if (HasChosenOutput()) {
            // Double-F2 with a remembered choice: change location.
            try { Debug.Log("[MediaRec] F2 double: changing save location"); }
            catch (Exception) { }
          } else {
            string why;
            if (!EnsureOutputReady(out why) && why != "no-saved-location") err = why;
          }
          OpenLocationChooser(err, true);
          return;
        }
        // Armed (None): Update fires the start when the hold expires with
        // no second press. Nothing to do on this frame.
        return;
      }
      StartRecording(_toggleMode);
    } catch (Exception) { }
  }

  public string StatusLine() {
    try {
      long aq = 0, ad = 0, vq = 0, vd = 0, gq = 0, gd = 0;
      lock (_telLock) {
        aq = _telemetry.AudioChunks; ad = _telemetry.AudioDroppedQueue;
        vq = _telemetry.VideoFrames; vd = _telemetry.VideoDroppedQueue;
        gq = _telemetry.GameFrames; gd = _telemetry.GameDroppedRaw;
      }
      int aqn = _audioQueue != null ? _audioQueue.Count : 0;
      int vqn = _videoQueue != null ? _videoQueue.Count : 0;
      int gqn = _gameRawQueue != null ? _gameRawQueue.Count : 0;
      return string.Format("rec {0} session={1} achunks={2}(q{3}/d{4}) vframes={5}(q{6}/d{7}) gframes={8}(q{9}/d{10}) tc={11} int={12} err={13}",
        _state, _sessionId, aq, aqn, ad, vq, vqn, vd, gq, gqn, gd,
        _telemetry.Transcoded, _telemetry.Interrupted, _lastError);
    } catch (Exception) { return "rec " + _state; }
  }

  public RecordingTelemetry ReadTelemetrySnapshot() {
    try {
      lock (_telLock) {
        return new RecordingTelemetry {
          SessionId = _telemetry.SessionId,
          Mode = _telemetry.Mode,
          State = _telemetry.State,
          StartUtcIso = _telemetry.StartUtcIso,
          StopUtcIso = _telemetry.StopUtcIso,
          FirstAudioOffsetMs = _telemetry.FirstAudioOffsetMs,
          FirstVideoOffsetMs = _telemetry.FirstVideoOffsetMs,
          FirstGameOffsetMs = _telemetry.FirstGameOffsetMs,
          AudioChunks = _telemetry.AudioChunks,
          AudioSamples = _telemetry.AudioSamples,
          AudioDroppedQueue = _telemetry.AudioDroppedQueue,
          AudioDroppedForeign = _telemetry.AudioDroppedForeign,
          LocalMicChunks = _telemetry.LocalMicChunks,
          AudioSourceSwitches = _telemetry.AudioSourceSwitches,
          LocalMicDevice = _telemetry.LocalMicDevice,
          AudioOrigin = _telemetry.AudioOrigin,
          VideoFrames = _telemetry.VideoFrames,
          VideoDroppedQueue = _telemetry.VideoDroppedQueue,
          VideoDroppedForeign = _telemetry.VideoDroppedForeign,
          VideoGapSamples = _telemetry.VideoGapSamples,
          GameFrames = _telemetry.GameFrames,
          GameDroppedRaw = _telemetry.GameDroppedRaw,
          GameDroppedQueue = _telemetry.GameDroppedQueue,
          GameDroppedForeign = _telemetry.GameDroppedForeign,
          GameGapSamples = _telemetry.GameGapSamples,
          GameDarkFrames = _telemetry.GameDarkFrames,
          RenderFrames = _telemetry.RenderFrames,
          RenderMsSum = _telemetry.RenderMsSum,
          RenderMsMax = _telemetry.RenderMsMax,
          GameCaptureRequests = _telemetry.GameCaptureRequests,
          GameDuplicateFrames = _telemetry.GameDuplicateFrames,
          GamePacingN = _telemetry.GamePacingN,
          GamePacingSumMs = _telemetry.GamePacingSumMs,
          GamePacingSumSqMs = _telemetry.GamePacingSumSqMs,
          GamePacingMaxMs = _telemetry.GamePacingMaxMs,
          GamePacingOver50 = _telemetry.GamePacingOver50,
          TranscodeElapsedMs = _telemetry.TranscodeElapsedMs,
          TranscodeFps = _telemetry.TranscodeFps,
          ProcessCpuPct = _telemetry.ProcessCpuPct,
          Transcoded = _telemetry.Transcoded,
          Mp4Path = _telemetry.Mp4Path,
          Mp3Path = _telemetry.Mp3Path,
          Mp4Bytes = _telemetry.Mp4Bytes,
          Mp3Bytes = _telemetry.Mp3Bytes,
          Mp4Complete = _telemetry.Mp4Complete,
          Mp3Complete = _telemetry.Mp3Complete,
          FfmpegVersion = _telemetry.FfmpegVersion,
          TranscodeError = _telemetry.TranscodeError,
          GamePath = _telemetry.GamePath,
          GameBytes = _telemetry.GameBytes,
          GameComplete = _telemetry.GameComplete,
          GameWidth = _telemetry.GameWidth,
          GameHeight = _telemetry.GameHeight,
          GameFps = _telemetry.GameFps,
          Quality = _telemetry.Quality,
          VideoCrf = _telemetry.VideoCrf,
          VideoPreset = _telemetry.VideoPreset,
          GameEncodeThreads = _telemetry.GameEncodeThreads,
          CpuCount = _telemetry.CpuCount,
          AudioPath = _telemetry.AudioPath,
          VideoPath = _telemetry.VideoPath,
          AudioBytes = _telemetry.AudioBytes,
          VideoBytes = _telemetry.VideoBytes,
          AudioComplete = _telemetry.AudioComplete,
          VideoComplete = _telemetry.VideoComplete,
          Interrupted = _telemetry.Interrupted,
          Error = _telemetry.Error,
          AudioBackend = _telemetry.AudioBackend,
          VideoBackend = _telemetry.VideoBackend,
          VideoWidth = _telemetry.VideoWidth,
          VideoHeight = _telemetry.VideoHeight,
          VideoFps = _telemetry.VideoFps,
        };
      }
    } catch (Exception) { return new RecordingTelemetry(); }
  }

  // --- test hooks (hardware-free; drive the real queues/writers/files) -------
  // EditMode has no Update pump: tests drive the stopping pipeline by
  // calling PumpForTests() until the state leaves Stopping (bounded by the
  // test's own timeout). Recording-state sampling is driven via
  // TestEnqueue* below (no cameras/watcher needed).
  public void PumpForTests() {
    try {
      if (_state == RecordingState.Stopping) PumpStopping();
    } catch (Exception) { }
  }
  public bool TestEnqueueAudio(uint serial, byte[] pcm16) {
    try {
      if (_state != RecordingState.Recording || !_effective.RecordAudio) return false;
      OnAudioTap(serial, 0, pcm16);
      return true;
    } catch (Exception) { return false; }
  }

  public bool TestEnqueueVideo(uint serial, byte[] jpeg) {
    try {
      if (_state != RecordingState.Recording || !_effective.RecordVideo) return false;
      if (jpeg == null || jpeg.Length < 4) return false;
      if (!(jpeg[0] == 0xFF && jpeg[1] == 0xD8 && jpeg[2] == 0xFF)) return false;
      if (_videoQueue == null || _videoQueue.IsClosed) return false;
      if (!AcceptVideoSerial(serial)) return false;
      if (_videoQueue.TryEnqueue(jpeg)) {
        lock (_telLock) {
          _telemetry.VideoFrames++;
          if (_telemetry.FirstVideoOffsetMs < 0)
            _telemetry.FirstVideoOffsetMs = _clock.OffsetMs();
        }
        return true;
      }
      return false;
    } catch (Exception) { return false; }
  }

  // Gameplay test hook: raw RGBA frame through the REAL worker path
  // (swizzle + raw BGRA -> game.rawvid). Bytes must be W*H*4 (config game size).
  public bool TestEnqueueGameRaw(uint serial, byte[] rgba) {
    try {
      if (_state != RecordingState.Recording || !_effective.RecordVideo) return false;
      if (rgba == null || rgba.Length != _effective.GameWidth * _effective.GameHeight * 4) return false;
      if (_gameRawQueue == null || _gameRawQueue.IsClosed) return false;
      lock (_telLock) {
        if (!_gameLatch.Accept(serial)) {
          _gameLatch.Reset();
          _gameLatch.Accept(serial);
          _telemetry.Interrupted = true;
        }
      }
      if (_gameRawQueue.TryEnqueue(rgba)) {
        lock (_telLock) {
          if (_telemetry.FirstGameOffsetMs < 0)
            _telemetry.FirstGameOffsetMs = _clock.OffsetMs();
        }
        return true;
      }
      return false;
    } catch (Exception) { return false; }
  }

  // Best-effort graceful close on exit (§30): bounded join, never throws.
  // No crash-safe recovery is claimed: an uncontrolled kill may leave an
  // unfinalized file, documented as a limitation in the handoff.
  void OnApplicationQuit() {
    TryGracefulShutdown();
  }

  void OnDestroy() {
    TryGracefulShutdown();
    try { ReleaseGameCapture(); } catch (Exception) { }
  }

  void TryGracefulShutdown() {
    try {
      if (_state != RecordingState.Recording && _state != RecordingState.Stopping) return;
      try {
        var w = CurrentWatcher();
        if (w != null) {
          try { w.AudioPayloadAccepted -= OnAudioTap; } catch (Exception) { }
        }
      } catch (Exception) { }
      try { if (_audioQueue != null) _audioQueue.Close(); } catch (Exception) { }
      try { if (_videoQueue != null) _videoQueue.Close(); } catch (Exception) { }
      try { if (_gameRawQueue != null) _gameRawQueue.Close(); } catch (Exception) { }
      try { StopLocalMic(); } catch (Exception) { }
      try { if (_audioThread != null && _audioThread.IsAlive) _audioThread.Join(2000); } catch (Exception) { }
      try { if (_videoThread != null && _videoThread.IsAlive) _videoThread.Join(2000); } catch (Exception) { }
      try { if (_gameThread != null && _gameThread.IsAlive) _gameThread.Join(2000); } catch (Exception) { }
      try { if (_audioWriter != null) _audioWriter.Close(); } catch (Exception) { }
      try { if (_videoWriter != null) _videoWriter.Close(); } catch (Exception) { }
      try { if (_gameWriter != null) _gameWriter.Close(); } catch (Exception) { }
      try { ReleaseGameCapture(); } catch (Exception) { }
    } catch (Exception) { }
  }
}
