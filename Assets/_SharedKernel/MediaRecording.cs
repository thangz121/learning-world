// _SharedKernel/MediaRecording.cs — Lead owns. Phase 2.3 recording kernel.
// Pure C# (NO UnityEngine, NO I/O beyond path strings): lifecycle, config,
// file naming, bounded queues, telemetry. The actual file bytes live in
// WavWriter / AviMjpegWriter; the threaded file pumps live in the
// game-side MediaRecordingService (A_World). This file never touches the
// network, the disk, or raw-media logs — telemetry carries SCALARS only
// (counts, durations, paths, sizes, encoder status), never media bytes.
//
// Recording boundary (§3): the recorder consumes media ONLY after the game
// accepted it — audio payloads the PhonePresenceWatcher already observed
// (game link state) and video frames the GameCameraStreamService already
// decoded to Live (slot-accepted). Serial latch + foreign-drop below reuse
// the Phase 2.1/2.2 session-isolation pattern so a reconnect can never
// merge stale bytes into a new session.
using System;
using System.Text;

public enum RecordingState {
  Idle,       // no session (or last session fully reported)
  Starting,   // validating config + sources + opening files
  Recording,  // accepting bounded samples on the game thread(s)
  Stopping,   // stop requested: no new samples accepted
  Finalizing, // workers draining queues + closing containers
  Completed,  // files finalized + verified (per-media flags say how well)
  Error       // init/write/finalize failure (telemetry carries the reason)
}

public enum RecordingMode {
  MicOnly,
  CameraOnly,
  MicAndCamera
}

// Output quality preset (user rule): one choice drives CRF + x264 preset +
// capture resolution together (never scattered knobs). MAX keeps a 1080p
// source at 1080p (never downscales a capable source, never upscales a
// small one); lower tiers cap the capture box. Default = High (current
// proven behavior: 720p / CRF 19 / medium).
public enum VideoQuality {
  Preview = 0,  // fast + light: 480p cap, CRF 24, superfast
  Standard = 1, // moderate: 720p cap, CRF 21, medium
  High = 2,     // full HD: 1080p cap, CRF 19, slow
  Max = 3,      // best: 1080p cap, CRF 12 (near-raw), slow (transcode is post-session)
}

public struct QualityTierParams {
  public int Crf;
  public string Preset;
  public int CapWidth;
  public int CapHeight;
}

public static class QualityTier {
  public static QualityTierParams For(VideoQuality q) {
    switch (q) {
      case VideoQuality.Preview:
        return new QualityTierParams { Crf = 24, Preset = "superfast", CapWidth = 854, CapHeight = 480 };
      case VideoQuality.Standard:
        return new QualityTierParams { Crf = 21, Preset = "medium", CapWidth = 1280, CapHeight = 720 };
      case VideoQuality.Max:
        return new QualityTierParams { Crf = 12, Preset = "slow", CapWidth = 1920, CapHeight = 1080 };
      default: // High + any garbage value fail safe to the proven tier
        return new QualityTierParams { Crf = 19, Preset = "slow", CapWidth = 1920, CapHeight = 1080 };
    }
  }

  // Resolve the capture size for a source frame: fit srcW x srcH inside the
  // tier cap, NEVER upscale (scale <= 1), aspect preserved, even dims
  // (x264/yuv420p requirement), floor 16. Unknown/degenerate source falls
  // back to the tier cap itself. Pure + never throws (unit-pinned).
  public static void ResolveGameSize(VideoQuality q, int srcW, int srcH, out int w, out int h) {
    QualityTierParams t = For(q);
    w = t.CapWidth;
    h = t.CapHeight;
    try {
      if (srcW < 16 || srcH < 16) return;
      double scale = Math.Min(1.0, Math.Min(t.CapWidth / (double)srcW, t.CapHeight / (double)srcH));
      int rw = (int)(srcW * scale);
      int rh = (int)(srcH * scale);
      rw &= ~1;
      rh &= ~1;
      if (rw < 16 || rh < 16) return;
      w = rw;
      h = rh;
    } catch (Exception) {
      w = t.CapWidth;
      h = t.CapHeight;
    }
  }
}

public static class MediaRecording {
  public const int DefaultVideoFps = 10;
  public const int MinVideoFps = 1;
  public const int MaxVideoFps = 30;
  public const int DefaultMaxAudioQueueSec = 30;
  public const int DefaultMaxVideoFrames = 300;
  public const int MinVideoWidth = 160;
  public const int MaxVideoWidth = 640;
  public const int MinVideoHeight = 120;
  public const int MaxVideoHeight = 480;
  public const string AudioExtension = ".wav";
  public const string VideoExtension = ".avi";
  public const string SidecarExtension = ".json";
  public const string AudioBackendName = "wav-pcm16-mono-16k";
  public const string VideoBackendName = "avi-mjpeg";
  public const int AudioSampleRate = 16000;
  public const int AudioChannels = 1;

  // Full-session deliverables (Phase 2.3b): gameplay + camera PiP + audio,
  // transcoded by the isolated FFmpeg backend when available.
  public const string GameExtension = ".avi"; // intermediate (MJPEG, C# JPEG)
  public const string Mp4Extension = ".mp4";  // deliverable (H.264 + MP3)
  public const string Mp3Extension = ".mp3";  // deliverable (LAME VBR)
  public const string GameBackendName = "avi-mjpeg(csharp-jpeg)";
  public const string DeliverBackendName = "mp4-h264+mp3(ffmpeg)";
  public const int DefaultGameWidth = 1280; // match the typical window: no
  public const int DefaultGameHeight = 720; // downscale softening (user rule)
  public const int MinGameWidth = 320;
  public const int MaxGameWidth = 1920;
  public const int MinGameHeight = 180;
  public const int MaxGameHeight = 1080;
  public const int DefaultGameFps = 20; // worker JPEG sustains ~19 fps here;
                                  // cap 20 keeps header rate ≈ wall rate
  public const int DefaultGameJpegQuality = 90; // intermediate is deleted
                                   // after transcode: stay near-lossless here
  public const int MaxGameRawFrames = 6; // RGBA handoff cap (bounded memcpy)
  public const int DefaultVideoCrf = 19; // x264: lower = better (10..32);
                                   // 19 ~= transparent (user rule: squeeze
                                   // size, keep near-original quality)
  public const string DefaultVideoPreset = "medium"; // x264 preset: motion
                                   // quality first (user rule); transcode runs
                                   // post-STOP, never during gameplay
  public const int DefaultMp3Quality = 4; // LAME VBR -q:a (0..9, lower = better)
  public const int DefaultPipWidth = 240; // 4:3 camera overlay
  public const int DefaultPipMargin = 16;

  public static readonly string[] AllowedVideoPresets = {
    "ultrafast", "superfast", "veryfast", "faster", "medium", "slow"
  };

  public static bool IsAllowedPreset(string p) {
    try {
      if (string.IsNullOrEmpty(p)) return false;
      foreach (string a in AllowedVideoPresets)
        if (string.Equals(a, p, StringComparison.OrdinalIgnoreCase)) return true;
      return false;
    } catch (Exception) { return false; }
  }

  public static bool IsTerminal(RecordingState s) {
    return s == RecordingState.Completed || s == RecordingState.Error;
  }
}

// One controlled configuration object (§8: width/height/FPS/bitrate-ish,
// codec, container, output dir come from HERE, never scattered literals).
public struct MediaRecordingConfig {
  public bool RecordAudio;
  public bool RecordVideo;
  public int VideoFps;            // sampled on the main thread (default 10)
  public int MaxAudioQueueSec;    // bounded audio backlog (default 30 s)
  public int MaxVideoFrames;      // bounded video backlog (default 300)
  public int VideoWidth;          // must match the game camera feed (320)
  public int VideoHeight;         // (240)
  public int VideoJpegQuality;    // local-camera JPEG encode only (phone
                                  // bytes are stored verbatim, never
                                  // re-encoded)
  public string OutputDirOverride; // null/empty = runtime default
                                   // (persistentDataPath/MediaRecordings)
  // Full-session deliverables (display/input quality stays max — see the
  // Phase 2.2 camera config and the untouched render pipeline; these ONLY
  // shape the recorded OUTPUT, which prioritizes fps + compression).
  public int GameWidth;           // gameplay capture width (default 1280)
  public int GameHeight;          // gameplay capture height (default 720)
  public int GameFps;             // gameplay sample rate (default 24)
  public int GameJpegQuality;     // worker-side C# JPEG for game frames
  public int VideoCrf;            // x264 CRF 10..32 (default 19: transparent,
                                  // bigger file by user rule)
  public string VideoPreset;      // x264 preset allowlist (default medium:
                                  // best motion handling; transcode is
                                  // post-session so speed is secondary)
  public int AudioMp3Quality;     // LAME VBR -q:a 0..9 (default 4 ≈ voice-
                                  // transparent, roughly half of 128k CBR)
  public int PipWidth;            // camera overlay width (4:3, default 240)
  public int PipMargin;           // overlay margin px (default 16)
  public string FfmpegPathOverride; // explicit ffmpeg binary (null = detect)
  public bool KeepIntermediates;  // keep wav/avis after a successful
                                  // transcode (default false: save disk)
  public VideoQuality Quality;    // output quality preset (default High):
                                  // drives CRF + x264 preset + capture
                                  // resolution together at session start

  public static MediaRecordingConfig Default {
    get {
      return new MediaRecordingConfig {
        RecordAudio = true,
        RecordVideo = true,
        VideoFps = MediaRecording.DefaultVideoFps,
        MaxAudioQueueSec = MediaRecording.DefaultMaxAudioQueueSec,
        MaxVideoFrames = MediaRecording.DefaultMaxVideoFrames,
        VideoWidth = 320,
        VideoHeight = 240,
        VideoJpegQuality = 60,
        OutputDirOverride = null,
        GameWidth = MediaRecording.DefaultGameWidth,
        GameHeight = MediaRecording.DefaultGameHeight,
        GameFps = MediaRecording.DefaultGameFps,
        GameJpegQuality = MediaRecording.DefaultGameJpegQuality,
        VideoCrf = MediaRecording.DefaultVideoCrf,
        VideoPreset = MediaRecording.DefaultVideoPreset,
        AudioMp3Quality = MediaRecording.DefaultMp3Quality,
        PipWidth = MediaRecording.DefaultPipWidth,
        PipMargin = MediaRecording.DefaultPipMargin,
        FfmpegPathOverride = null,
        KeepIntermediates = false,
        Quality = VideoQuality.High,
      };
    }
  }

  public static MediaRecordingConfig ForMode(RecordingMode mode) {
    MediaRecordingConfig c = Default;
    c.RecordAudio = mode == RecordingMode.MicOnly || mode == RecordingMode.MicAndCamera;
    c.RecordVideo = mode == RecordingMode.CameraOnly || mode == RecordingMode.MicAndCamera;
    return c;
  }

  public bool Validate(out string reason) {
    if (!RecordAudio && !RecordVideo) { reason = "nothing to record (audio+video off)"; return false; }
    if (VideoFps < MediaRecording.MinVideoFps || VideoFps > MediaRecording.MaxVideoFps) {
      reason = "videoFps 1..30"; return false;
    }
    if (MaxAudioQueueSec < 5 || MaxAudioQueueSec > 120) {
      reason = "maxAudioQueueSec 5..120"; return false;
    }
    if (MaxVideoFrames < 10 || MaxVideoFrames > 1800) {
      reason = "maxVideoFrames 10..1800"; return false;
    }
    if (VideoWidth < MediaRecording.MinVideoWidth || VideoWidth > MediaRecording.MaxVideoWidth) {
      reason = "videoWidth 160..640"; return false;
    }
    if (VideoHeight < MediaRecording.MinVideoHeight || VideoHeight > MediaRecording.MaxVideoHeight) {
      reason = "videoHeight 120..480"; return false;
    }
    if (VideoJpegQuality < 30 || VideoJpegQuality > 85) {
      reason = "videoJpegQuality 30..85"; return false;
    }
    if (GameWidth < MediaRecording.MinGameWidth || GameWidth > MediaRecording.MaxGameWidth) {
      reason = "gameWidth 320..1920"; return false;
    }
    if (GameHeight < MediaRecording.MinGameHeight || GameHeight > MediaRecording.MaxGameHeight) {
      reason = "gameHeight 180..1080"; return false;
    }
    if (GameFps < MediaRecording.MinVideoFps || GameFps > MediaRecording.MaxVideoFps) {
      reason = "gameFps 1..30"; return false;
    }
    if (GameJpegQuality < 30 || GameJpegQuality > 100) {
      reason = "gameJpegQuality 30..100"; return false;
    }
    if (VideoCrf < 10 || VideoCrf > 32) {
      reason = "videoCrf 10..32"; return false;
    }
    if (!MediaRecording.IsAllowedPreset(VideoPreset)) {
      reason = "videoPreset ultrafast..slow"; return false;
    }
    if (AudioMp3Quality < 0 || AudioMp3Quality > 9) {
      reason = "audioMp3Quality 0..9"; return false;
    }
    if (PipWidth < 120 || PipWidth > 320) {
      reason = "pipWidth 120..320"; return false;
    }
    if (PipMargin < 0 || PipMargin > 64) {
      reason = "pipMargin 0..64"; return false;
    }
    if (!System.Enum.IsDefined(typeof(VideoQuality), Quality)) {
      reason = "quality Preview..Max"; return false;
    }
    reason = null;
    return true;
  }
}

// Deterministic + collision-safe file naming (§17): one session ID shared
// by the audio file, the video file, and the sidecar, so a session stays
// traceable and no recording ever overwrites another.
public static class MediaRecordingNaming {
  public static string NewSessionId(DateTime utc) {
    string stamp;
    try { stamp = utc.ToString("yyyyMMdd-HHmmss"); }
    catch (Exception) { stamp = "unknowntime"; }
    string suffix;
    try { suffix = Guid.NewGuid().ToString("N").Substring(0, 8); }
    catch (Exception) { suffix = "00000000"; }
    return "rec-" + stamp + "-" + suffix;
  }

  public static string AudioFileName(string sessionId) {
    return sessionId + "_audio" + MediaRecording.AudioExtension;
  }
  public static string VideoFileName(string sessionId) {
    return sessionId + "_video" + MediaRecording.VideoExtension;
  }

  public static string GameFileName(string sessionId) {
    return sessionId + "_game" + MediaRecording.GameExtension;
  }

  public static string Mp4FileName(string sessionId) {
    return sessionId + MediaRecording.Mp4Extension;
  }

  public static string Mp3FileName(string sessionId) {
    return sessionId + MediaRecording.Mp3Extension;
  }

  public static string SidecarFileName(string sessionId) {
    return sessionId + MediaRecording.SidecarExtension;
  }

  // Join helper that never throws (service maps false to ERROR telemetry).
  public static bool TryJoin(string dir, string name, out string path) {
    path = null;
    try {
      if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name)) return false;
      path = System.IO.Path.Combine(dir, name);
      return true;
    } catch (Exception) { return false; }
  }
}

// Bounded backlog (§11: never unbounded growth, never a gameplay freeze).
// Enqueue copies the payload (callers keep their buffers), drops the OLDEST
// while over cap, and counts everything. Dequeue drains in order. Close()
// stops acceptance so Stop can flush deterministically. Thread-safe, never
// throws out of public methods.
public sealed class BoundedByteQueue {
  readonly object _mutex = new object();
  readonly System.Collections.Generic.Queue<byte[]> _q =
    new System.Collections.Generic.Queue<byte[]>();
  readonly int _maxItems;
  readonly long _maxBytes;
  long _bytes;
  bool _closed;
  long _enqueued;
  long _dropped;
  long _dequeued;

  public BoundedByteQueue(int maxItems, long maxBytes) {
    _maxItems = maxItems > 0 ? maxItems : 100;
    _maxBytes = maxBytes > 0 ? maxBytes : 4L * 1024 * 1024;
  }

  public static BoundedByteQueue ForAudio(int maxSec) {
    // 16 kHz mono PCM16 = 32000 B/s; chunks arrive ~4-10/s.
    long cap = Math.Max(160000L, (long)maxSec * 32000L);
    return new BoundedByteQueue(Math.Max(64, maxSec * 16), cap);
  }

  public static BoundedByteQueue ForVideo(int maxFrames) {
    // 320x240 JPEG q60 ≈ 5-20 KB/frame; cap bytes generously above items.
    return new BoundedByteQueue(maxFrames, (long)maxFrames * 64L * 1024L);
  }

  public bool IsClosed {
    get { try { lock (_mutex) { return _closed; } } catch (Exception) { return true; } }
  }

  public int Count {
    get { try { lock (_mutex) { return _q.Count; } } catch (Exception) { return 0; } }
  }

  // False = closed (not accepted) — never blocks, never grows past the cap.
  public bool TryEnqueue(byte[] data) {
    if (data == null || data.Length == 0) return false;
    try {
      var copy = new byte[data.Length];
      Buffer.BlockCopy(data, 0, copy, 0, data.Length);
      lock (_mutex) {
        if (_closed) return false;
        _q.Enqueue(copy);
        _bytes += copy.Length;
        _enqueued++;
        while ((_q.Count > _maxItems || _bytes > _maxBytes) && _q.Count > 1) {
          byte[] old = _q.Dequeue();
          _bytes -= old != null ? old.Length : 0;
          _dropped++;
        }
        if (_q.Count > _maxItems || _bytes > _maxBytes) {
          // Single huge payload over the whole budget: keep it (one item)
          // rather than wedging; the cap still bounds every later enqueue.
        }
        return true;
      }
    } catch (Exception) { return false; }
  }

  public bool TryDequeue(out byte[] data) {
    data = null;
    try {
      lock (_mutex) {
        if (_q.Count == 0) return false;
        data = _q.Dequeue();
        _bytes -= data != null ? data.Length : 0;
        _dequeued++;
        return true;
      }
    } catch (Exception) { return false; }
  }

  public void Close() {
    try { lock (_mutex) { _closed = true; } } catch (Exception) { }
  }

  public void ReadCounters(out long enqueued, out long dropped, out long dequeued, out int count) {
    try {
      lock (_mutex) {
        enqueued = _enqueued; dropped = _dropped; dequeued = _dequeued; count = _q.Count;
        return;
      }
    } catch (Exception) { }
    enqueued = 0; dropped = 0; dequeued = 0; count = 0;
  }
}

// Session serial latch (§36: stale-frame/stale-audio protection). The first
// serial enqueued after Reset wins; foreign serials are rejected (the caller
// counts them). Same pattern as NetworkMicrophoneCapture / CameraFrameSource.
public sealed class RecordingSessionLatch {
  readonly object _mutex = new object();
  bool _latched;
  uint _serial;
  long _droppedForeign;

  public void Reset() {
    try { lock (_mutex) { _latched = false; _serial = 0; } } catch (Exception) { }
  }

  // True = accepted (caller's payload belongs to the live session).
  public bool Accept(uint serial) {
    try {
      lock (_mutex) {
        if (!_latched) { _latched = true; _serial = serial; return true; }
        if (serial != _serial) { _droppedForeign++; return false; }
        return true;
      }
    } catch (Exception) { return false; }
  }

  public long DroppedForeign {
    get { try { lock (_mutex) { return _droppedForeign; } } catch (Exception) { return 0; } }
  }

  public bool IsLatched {
    get { try { lock (_mutex) { return _latched; } } catch (Exception) { return false; } }
  }
}

// Monotonic session clock (§16): wall UTC for file names/sidecar, tick base
// for first-sample/first-frame offsets and durations. No perfect-A/V-sync
// claim: audio and video offsets share this ONE clock so drift is measurable.
public sealed class RecordingClock {
  DateTime _startUtc = DateTime.UtcNow;
  int _startTick;
  bool _started;

  public DateTime StartUtc {
    get { return _startUtc; }
  }

  public void Start() {
    try {
      _startUtc = DateTime.UtcNow;
      _startTick = Environment.TickCount;
      _started = true;
    } catch (Exception) { _started = false; }
  }

  public int OffsetMs() {
    try {
      if (!_started) return -1;
      return unchecked(Environment.TickCount - _startTick);
    } catch (Exception) { return -1; }
  }

  public static string ToIso(DateTime utc) {
    try { return utc.ToUniversalTime().ToString("o"); }
    catch (Exception) { return string.Empty; }
  }
}

// Telemetry (§19: scalars only — session, state, duration, counts, errors,
// paths, sizes, encoder status. NEVER raw media, never media bytes in logs).
public sealed class RecordingTelemetry {
  public string SessionId = string.Empty;
  public RecordingMode Mode;
  public RecordingState State;
  public string StartUtcIso = string.Empty;
  public string StopUtcIso = string.Empty;
  public int FirstAudioOffsetMs = -1;
  public int FirstVideoOffsetMs = -1;
  public int FirstGameOffsetMs = -1;
  public long AudioChunks;
  public long AudioSamples;
  public long AudioDroppedQueue;
  public long AudioDroppedForeign;
  public long LocalMicChunks;      // chunks fed from the laptop mic (Phase 2.3e)
  public long AudioSourceSwitches; // phone<->local transitions mid-session
  public string LocalMicDevice = string.Empty;
  public string AudioOrigin = string.Empty; // phone | local | switched | (empty=none)
  public long VideoFrames;
  public long VideoDroppedQueue;
  public long VideoDroppedForeign;
  public long VideoGapSamples; // sample ticks with no Live frame while recording
  public long GameFrames;
  public long GameDroppedRaw;   // RGBA handoff over cap (worker lagging)
  public long GameDroppedQueue; // JPEG backlog over cap
  public long GameDroppedForeign;
  public long GameGapSamples;   // sample ticks with no readback while recording
  public long GameDarkFrames;   // readbacks with near-zero brightness (black-path tripwire)
  public int GameEncodeThreads; // JPEG farm width for the session (0 = single-thread legacy)
  public int CpuCount;          // machine logical processors seen at session start
  public bool Transcoded;       // ffmpeg deliverables produced
  public string Mp4Path = string.Empty;
  public string Mp3Path = string.Empty;
  public long Mp4Bytes;
  public long Mp3Bytes;
  public bool Mp4Complete;
  public bool Mp3Complete;
  public string FfmpegVersion = string.Empty;
  public string TranscodeError = string.Empty;
  public string GamePath = string.Empty;
  public long GameBytes;
  public bool GameComplete;
  public int GameWidth;
  public int GameHeight;
  public int GameFps;
  public string Quality = string.Empty; // VideoQuality tier that ran (traceability)
  public int VideoCrf;                  // resolved x264 CRF for the session
  public string VideoPreset = string.Empty; // resolved x264 preset
  public string AudioPath = string.Empty;
  public string VideoPath = string.Empty;
  public long AudioBytes;
  public long VideoBytes;
  public bool AudioComplete;
  public bool VideoComplete;
  public bool Interrupted; // phone/camera drop or reconnect mid-session
  public string Error = string.Empty;
  public string AudioBackend = MediaRecording.AudioBackendName;
  public string VideoBackend = MediaRecording.VideoBackendName;
  public int VideoWidth;
  public int VideoHeight;
  public int VideoFps;

  static string Esc(string s) {
    if (string.IsNullOrEmpty(s)) return string.Empty;
    try {
      return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\r", "\\r").Replace("\n", "\\n");
    } catch (Exception) { return string.Empty; }
  }

  public string ToJson() {
    try {
      var b = new StringBuilder(1024);
      b.Append("{");
      P(b, "sessionId", SessionId, true);
      P(b, "mode", Mode.ToString(), false);
      P(b, "state", State.ToString(), false);
      P(b, "startUtc", StartUtcIso, false);
      P(b, "stopUtc", StopUtcIso, false);
      N(b, "firstAudioOffsetMs", FirstAudioOffsetMs, false);
      N(b, "firstVideoOffsetMs", FirstVideoOffsetMs, false);
      N(b, "firstGameOffsetMs", FirstGameOffsetMs, false);
      N(b, "audioChunks", AudioChunks, false);
      N(b, "audioSamples", AudioSamples, false);
      N(b, "audioDroppedQueue", AudioDroppedQueue, false);
      N(b, "audioDroppedForeign", AudioDroppedForeign, false);
      N(b, "localMicChunks", LocalMicChunks, false);
      N(b, "audioSourceSwitches", AudioSourceSwitches, false);
      P(b, "localMicDevice", LocalMicDevice, false);
      P(b, "audioOrigin", AudioOrigin, false);
      N(b, "videoFrames", VideoFrames, false);
      N(b, "videoDroppedQueue", VideoDroppedQueue, false);
      N(b, "videoDroppedForeign", VideoDroppedForeign, false);
      N(b, "videoGapSamples", VideoGapSamples, false);
      N(b, "gameFrames", GameFrames, false);
      N(b, "gameDroppedRaw", GameDroppedRaw, false);
      N(b, "gameDroppedQueue", GameDroppedQueue, false);
      N(b, "gameDroppedForeign", GameDroppedForeign, false);
      N(b, "gameGapSamples", GameGapSamples, false);
      N(b, "gameDarkFrames", GameDarkFrames, false);
      N(b, "gameEncodeThreads", GameEncodeThreads, false);
      N(b, "cpuCount", CpuCount, false);
      B(b, "transcoded", Transcoded, false);
      P(b, "mp4Path", Mp4Path, false);
      P(b, "mp3Path", Mp3Path, false);
      N(b, "mp4Bytes", Mp4Bytes, false);
      N(b, "mp3Bytes", Mp3Bytes, false);
      B(b, "mp4Complete", Mp4Complete, false);
      B(b, "mp3Complete", Mp3Complete, false);
      P(b, "ffmpegVersion", FfmpegVersion, false);
      P(b, "transcodeError", TranscodeError, false);
      P(b, "gamePath", GamePath, false);
      N(b, "gameBytes", GameBytes, false);
      B(b, "gameComplete", GameComplete, false);
      N(b, "gameWidth", GameWidth, false);
      N(b, "gameHeight", GameHeight, false);
      N(b, "gameFps", GameFps, false);
      P(b, "quality", Quality, false);
      N(b, "videoCrf", VideoCrf, false);
      P(b, "videoPreset", VideoPreset, false);
      P(b, "audioPath", AudioPath, false);
      P(b, "videoPath", VideoPath, false);
      N(b, "audioBytes", AudioBytes, false);
      N(b, "videoBytes", VideoBytes, false);
      B(b, "audioComplete", AudioComplete, false);
      B(b, "videoComplete", VideoComplete, false);
      B(b, "interrupted", Interrupted, false);
      P(b, "error", Error, false);
      P(b, "audioBackend", AudioBackend, false);
      P(b, "videoBackend", VideoBackend, false);
      N(b, "videoWidth", VideoWidth, false);
      N(b, "videoHeight", VideoHeight, false);
      N(b, "videoFps", VideoFps, false);
      b.Append("}");
      return b.ToString();
    } catch (Exception) { return "{}"; }
  }

  static void P(StringBuilder b, string k, string v, bool first) {
    if (!first) b.Append(",");
    b.Append("\"").Append(k).Append("\":\"").Append(Esc(v)).Append("\"");
  }

  static void N(StringBuilder b, string k, long v, bool first) {
    if (!first) b.Append(",");
    b.Append("\"").Append(k).Append("\":").Append(v);
  }

  static void B(StringBuilder b, string k, bool v, bool first) {
    if (!first) b.Append(",");
    b.Append("\"").Append(k).Append("\":").Append(v ? "true" : "false");
  }
}

// F2 double-press tracker (pure, unit-tested): first F2 with no remembered
// choice opens the chooser immediately; otherwise single F2 arms a short
// disambiguation hold (PollStart fires the start) and a second F2 inside
// the window opens the chooser instead (change location, no F4 needed).
public enum F2PressResult {
  None,        // armed: wait to disambiguate (caller polls PollStart)
  OpenChooser, // open the save-location chooser now
}

public sealed class F2DoubleTracker {
  public const float WindowSec = 0.45f;
  float _armedUntil = -1f;

  public F2PressResult Press(float now, bool hasChoice) {
    if (!hasChoice) return F2PressResult.OpenChooser;
    if (_armedUntil > 0f && now < _armedUntil) {
      _armedUntil = -1f;
      return F2PressResult.OpenChooser;
    }
    _armedUntil = now + WindowSec;
    return F2PressResult.None;
  }

  public bool PollStart(float now) {
    if (_armedUntil > 0f && now >= _armedUntil) {
      _armedUntil = -1f;
      return true;
    }
    return false;
  }

  public void Cancel() {
    _armedUntil = -1f;
  }
}

// Detected audio origins (game-side truth, never assumed):
// PhoneReady = phone audio flowing in-game (recordable);
// LocalOnly  = a PC mic is listed/ready but no phone audio (detected, NOT
//   recordable by this phase: the recorder taps the phone watcher only);
// None      = no audio source anywhere.
public enum AudioSourceState {
  None,
  LocalOnly,
  PhoneReady,
}

// F2 start decision (pure, unit-tested): full session when everything is
// there; an explicit video-only PROPOSAL when audio is missing but video
// is present (never a silent fallback, never a bare fail); refusal only
// when nothing usable exists (the Start gates then voice the exact reason).
public enum StartDecision {
  StartAsRequested,
  ProposeVideoOnly,
  RefuseNoMedia,
}

public static class RecordingStartDecider {
  public static StartDecision DecideStart(bool audioReady, bool videoReady, RecordingMode mode) {
    try {
      if (mode == RecordingMode.MicOnly)
        return audioReady ? StartDecision.StartAsRequested : StartDecision.RefuseNoMedia;
      if (mode == RecordingMode.CameraOnly)
        return videoReady ? StartDecision.StartAsRequested : StartDecision.RefuseNoMedia;
      if (audioReady && videoReady) return StartDecision.StartAsRequested;
      if (!audioReady && videoReady) return StartDecision.ProposeVideoOnly;
      return StartDecision.RefuseNoMedia;
    } catch (System.Exception) { return StartDecision.RefuseNoMedia; }
  }
}
