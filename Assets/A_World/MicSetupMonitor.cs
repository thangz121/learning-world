// A_World/MicSetupMonitor.cs — Agent A (World & Visual).
// Driver for the mic-setup gate (thin glue; decisions live in D_Audio).
//
// STARTUP ("hỏi ngay khi bắt đầu game"): evaluates the gate once; when no
// microphone/headset is listed it shows the OFFER dialog ("Hiện đang không
// có microphone kết nối, bạn có muốn kết nối bằng điện thoại không?").
//
// BACKGROUND ("thỉnh thoảng check lại"): every LocalPollSec it Refresh()es
// the PC list (headset plugged/unplugged — cheap, main thread). While in
// phone mode it re-probes the phone↔gateway↔PC link every PhoneRecheckSec
// on a worker thread (TCP dials block; NO Unity API off the main thread —
// results are marshalled through volatile fields and applied in Update).
// Background transitions are SILENT (no mid-play popups, ever): a lost link
// just flips the gate to Skipped; recovery flips it back to Ready.
//
// EXERCISE ENTRY ("chỉ hỏi lại khi vào bài nghe"): future listening
// exercises call CheckBeforeListening(token) first. It returns true when a
// source is Ready NOW; otherwise it shows the offer (once per token) and
// returns false so the exercise defers — the runner's SkippedNoMic policy
// ("tạm thời bỏ qua bài nghe") then applies without touching mastery.
//
// NOTE (phone idleness): between utterances the phone holds NO live gateway
// session, so GatewayUpNoPhone at rest is NORMAL and never downgrades the
// phone state — only GatewayDown (gateway chết) does. Session proof happens
// per-probe (AUDIO/STOP frame) and per-capture (existing env mapping).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class MicSetupMonitor : MonoBehaviour {
  MicSetupGate _gate;
  IMicrophoneDevice _local;
  IPhoneLinkDevice _phone;
  MicSetupDialog _dialog;
  // Loopback bridge defaults (mirror PhoneMicProtocol.LoopbackHost=127.0.0.1
  // /DefaultBridgePort=8451 — duplicated because LWE.World must not reference
  // LWE.Audio; Bootstrap always passes the real values via Bind anyway).
  string _host = "127.0.0.1";
  int _port = 8451;

  float _localPollSec = 5f;
  float _phoneRecheckSec = 30f;
  float _localTimer;
  float _phoneTimer;

  // Worker-thread probe results (plain fields, applied on the main thread).
  volatile bool _probePending;
  volatile PhoneLinkState _probeResult = PhoneLinkState.Unknown;
  volatile int _probeResultSerial;
  volatile bool _probeForWait;
  int _probeSerial;
  bool _startupDone;

  // In-game gateway lifecycle (auto-start on Accept, QR in dialog, pause).
  // All process/file work is try/caught: failure == manual mode (old text).
  string _toolsDir;
  Process _gw;
  readonly object _gwLogLock = new object();
  readonly Queue<string> _gwLog = new Queue<string>();
  string _gwPageUrl = "";
  string _gwQrPath = "";
  bool _waitActive;        // inside a paused QR wait session
  bool _qrLoaded;
  bool _skipShown;
  float _waitElapsed;      // unscaled seconds (game is paused)
  float _waitProbeTimer;
  float _qrPollTimer;
  const float SkipShowAfterSec = 30f;   // QR 30 s chưa nối -> hiện nút Bỏ qua
  const float AutoSkipAfterSec = 120f;  // QR 2 ph chưa nối -> tự bỏ qua
  const float WaitReprobeSec = 3f;

  // Mid-game presence (drops + explicit STOP while the game is on): a
  // persistent bridge subscriber. Started on entering phone states, stopped
  // on leaving them. Complements the one-shot probes (which stay as backup).
  PhonePresenceWatcher _watcher;
  int _watcherAppliedSeq;
  // Edge dedup: the watcher re-posts GatewayDown/TransportLost on every
  // reconnect tick (~2 s). Without this the monitor would ReportLinkDown +
  // LogWarning forever (log spam with full stacks). Applied once per
  // down-episode; any sign of life (UP/AUDIO) re-arms.
  bool _linkDownApplied;
  // Audio-log dedup: the watcher posts PhoneAudio per AUDIO chunk (~4/s), so
  // one session would Debug.Log (full stack in player builds) dozens of times
  // (observed 39x serial 1). Log once per serial; state application stays
  // unconditional (idempotent). Reset on down-edges so a new session with a
  // reused serial still logs.
  bool _audioLoggedOnce;
  uint _lastAudioLoggedSerial;
  // Status-HUD signal state (measured, never faked — see MicSignal):
  // phone bars ride the watcher's payload energy with peak-hold decay;
  // the local dot rides device presence + poll freshness (the local mic is
  // never held open by the HUD — captures own the device).
  float _smoothEnergy;
  int _lastLocalPollTick = -1;

  // Injection boundary (wired by MarketBootstrap; all optional except gate).
  // Phone is IPhoneLinkDevice (kernel interface) because LWE.World must not
  // reference LWE.Audio; Bootstrap passes the D_Audio PhoneMicrophoneDevice.
  // toolsDir (optional): <repo>/tools holding phone_mic_gateway.py + lan
  // certs. Null/empty = manual mode (old instructions text, no auto-start).
  public void Bind(MicSetupGate gate, IMicrophoneDevice local,
      IPhoneLinkDevice phone, MicSetupDialog dialog,
      string bridgeHost, int bridgePort, string toolsDir = null) {
    _gate = gate;
    _local = local;
    _phone = phone;
    _dialog = dialog;
    if (!string.IsNullOrEmpty(bridgeHost)) _host = bridgeHost;
    if (bridgePort > 0) _port = bridgePort;
    if (!string.IsNullOrEmpty(toolsDir)) _toolsDir = toolsDir;
    _lastLocalPollTick = TickMs(); // sampling starts at bind (Start re-stamps)
  }

  public MicSetupGate Gate => _gate;

  // Listening may proceed right now (false = defer/skip the exercise).
  public bool IsListeningAvailable => _gate != null && !_gate.ShouldSkipListening();

  void Start() {
    if (_gate == null || _startupDone) return;
    _startupDone = true;
    _localTimer = _localPollSec;
    _phoneTimer = _phoneRecheckSec;
    _lastLocalPollTick = TickMs(); // startup refresh inside the gate counts
    try {
      MicSetupState state = _gate.EvaluateAtStartup();
      if (state == MicSetupState.OfferPhone) ShowOffer();
    } catch (Exception) { }
  }

  void Update() {
    if (_gate == null) return;
    // Background PC-mic poll (cheap; main thread).
    _localTimer -= Time.deltaTime;
    if (_localTimer <= 0f) {
      _localTimer = _localPollSec;
      PollLocal();
    }
    // Background phone-link recheck (phone mode only; worker thread).
    _phoneTimer -= Time.deltaTime;
    if (_phoneTimer <= 0f) {
      _phoneTimer = _phoneRecheckSec;
      if (_gate.State == MicSetupState.ReadyPhone
          || _gate.State == MicSetupState.WaitPhoneLink)
        ProbeNow(2.5f, forWait: false);
    }
    ApplyProbeResult();
    UpdateWaitSession();
    UpdatePresenceWatch();
    UpdateSignalSmoothing();
  }

  // --- status-HUD signal snapshot (measured, read-only for the HUD) -------
  // Source follows gate precedence (local wins, like CompositeMicDevice).
  // Phone energy/age come from the watcher's payload stats; local "data" is
  // device-presence sampling (listed + Ready + poll fresh) because the HUD
  // never opens the mic (captures own it). Never throws; never touches audio.
  public MicSignalSnapshot CurrentSignal {
    get {
      var none = new MicSignalSnapshot {
        Source = MicSignalSource.None, Level = SignalLevel.None,
        DataFlowing = false, LinkUp = false, Energy = 0f
      };
      try {
        if (_gate == null) return none;
        bool localReady = SafeAvailable(_local);
        if (localReady) {
          return new MicSignalSnapshot {
            Source = MicSignalSource.Local, Level = SignalLevel.None,
            DataFlowing = IsLocalPollFresh(), LinkUp = true, Energy = 0f
          };
        }
        bool phoneReady = SafeAvailable(_phone);
        if (_gate.State == MicSetupState.ReadyPhone
            || _gate.State == MicSetupState.WaitPhoneLink
            || phoneReady) {
          float energy = 0f;
          bool flowing = false;
          try {
            if (_watcher != null) {
              long frames, bytes;
              float lastEnergy;
              int lastBytes, ageMs;
              _watcher.ReadAudioStats(out frames, out bytes,
                out lastEnergy, out lastBytes, out ageMs);
              flowing = phoneReady && lastBytes > 0
                && MicSignal.IsDataFlowing(ageMs);
              if (MicSignal.IsDataFlowing(ageMs)) energy = _smoothEnergy;
            }
          } catch (Exception) { }
          return new MicSignalSnapshot {
            Source = MicSignalSource.Phone,
            Level = phoneReady ? MicSignal.ComputeLevel(energy) : SignalLevel.None,
            DataFlowing = flowing, LinkUp = phoneReady, Energy = energy
          };
        }
        return none;
      } catch (Exception) { return none; }
    }
  }

  // Peak-hold decay for the phone bars: jumps to fresh payload energy,
  // falls to 0 when the wire goes quiet (bars go grey = idle, not crossed).
  void UpdateSignalSmoothing() {
    try {
      float dt = 0f;
      try { dt = Time.deltaTime; } catch (Exception) { }
      if (dt < 0f || dt > 5f) dt = 0.03f;
      float target = 0f;
      try {
        if (_watcher != null) {
          long frames, bytes;
          float lastEnergy;
          int lastBytes, ageMs;
          _watcher.ReadAudioStats(out frames, out bytes,
            out lastEnergy, out lastBytes, out ageMs);
          if (MicSignal.IsDataFlowing(ageMs) && lastBytes > 0) target = lastEnergy;
        }
      } catch (Exception) { }
      _smoothEnergy = MicSignal.ApplyDecay(_smoothEnergy, target, dt);
    } catch (Exception) { }
  }

  static bool SafeAvailable(IMicrophoneDevice d) {
    try { return d != null && d.Capability.IsAvailable(); }
    catch (Exception) { return false; }
  }

  static int TickMs() {
    try { return Environment.TickCount; } catch (Exception) { return 0; }
  }

  bool IsLocalPollFresh() {
    try {
      if (_lastLocalPollTick < 0) return false;
      return unchecked(TickMs() - _lastLocalPollTick) <= 15000;
    } catch (Exception) { return false; }
  }

  // --- exercise entry ----------------------------------------------------------
  // Returns true when listening may proceed NOW. False = show-offer (once per
  // token) and defer: the caller skips the listening exercise this time
  // ("tạm thời bỏ qua bài nghe"); the decline/skip choice never touches
  // mastery (runner SkippedNoMic path). Null gate (feature off) = proceed.
  public bool CheckBeforeListening(string exerciseToken) {
    if (_gate == null) return true;
    if (!_gate.ShouldPromptAtExercise(exerciseToken))
      return !_gate.ShouldSkipListening();
    try {
      _gate.MarkPromptShown(exerciseToken);
      ShowOffer();
    } catch (Exception) { }
    return false;
  }

  // --- dialog callbacks ----------------------------------------------------------
  void OnAccept() {
    if (_gate == null) return;
    try {
      _gate.AcceptPhoneOffer();
      BeginPhoneWait();
      ProbeNow(6f, forWait: true);
    } catch (Exception) { }
  }

  void OnDecline() {
    try {
      if (_gate != null) _gate.DeclinePhoneOffer();
      if (_dialog != null) _dialog.Hide();
    } catch (Exception) { }
  }

  void OnRecheck() {
    try {
      if (_dialog != null) _dialog.SetWaitStatus(MicSetupDialog.StatusWaiting);
      ProbeNow(6f, forWait: true);
    } catch (Exception) { }
  }

  void OnSkipWaiting() {
    try {
      if (_gate != null) _gate.SkipWaiting();
      EndPhoneWait(linked: false);
    } catch (Exception) { }
  }

  // --- paused QR wait session (auto gateway + timeouts) -------------------------
  // Accept -> pause game + auto-start gateway + QR in dialog. Linked ->
  // hide QR + resume. 30 s without link -> reveal Skip under the QR.
  // 120 s without link -> auto-skip. All timers use UNSCALED time: the game
  // clock is stopped while the QR is up (Time.deltaTime == 0 in there).
  void BeginPhoneWait() {
    _waitActive = true;
    _qrLoaded = false;
    _linkDownApplied = false; // fresh wait episode: down edges may log again
    _audioLoggedOnce = false; // fresh wait episode: audio may log again
    _skipShown = false;
    _gwPageUrl = "";
    _waitElapsed = 0f;
    _waitProbeTimer = WaitReprobeSec;
    _qrPollTimer = 0f;
    try { Time.timeScale = 0f; } catch (Exception) { }
    try {
      if (_dialog != null)
        _dialog.ShowWait(MicSetupDialog.StatusQrStarting, OnRecheck, OnSkipWaiting);
    } catch (Exception) { }
    // Optimistic launch: if a gateway is ALREADY up (previous session, dev
    // running start_phone_mic.ps1 by hand) the new process exits on the busy
    // bridge port and we simply reuse the existing one + its QR file.
    TryStartGateway();
  }

  // Linked path: QR off, game resumes, gateway KEEPS running for captures.
  void FinishLinked() {
    LogQrOutcome("linked");
    _waitActive = false;
    try { Time.timeScale = 1f; } catch (Exception) { }
    try { if (_dialog != null) _dialog.Hide(); } catch (Exception) { }
  }

  // Skip path (manual late-skip or 120 s auto-skip): QR off, resume, and stop
  // the gateway we started (nothing needs it while listening is skipped; a
  // later Accept starts it again).
  void EndPhoneWait(bool linked) {
    LogQrOutcome(linked ? "linked" : "skipped");
    _waitActive = false;
    try { Time.timeScale = 1f; } catch (Exception) { }
    try { if (_dialog != null) _dialog.Hide(); } catch (Exception) { }
    if (!linked) StopGateway();
  }

  // Parent-facing diagnosis for "QR blank" reports: one line saying whether
  // the QR ever rendered and what the file state was. Warning only when the
  // panel ends with no QR (success is audible in the log once, at load).
  void LogQrOutcome(string how) {
    try {
      if (_qrLoaded) return;
      bool exists = false;
      try { exists = !string.IsNullOrEmpty(_gwQrPath) && File.Exists(_gwQrPath); } catch (Exception) { }
      Warn("[MicSetup] QR never rendered (" + how + " after " + _waitElapsed.ToString("F0")
        + "s; path=" + _gwQrPath + " exists=" + exists + ")");
    } catch (Exception) { }
  }

  void UpdateWaitSession() {
    if (!_waitActive || _gate == null) return;
    if (!_gate.IsWaitingForPhone) { // linked (or headset took over): done
      FinishLinked();
      return;
    }
    float dt;
    try { dt = Time.unscaledDeltaTime; } catch (Exception) { dt = 0.03f; }
    if (dt < 0f || dt > 5f) dt = 0.03f;
    _waitElapsed += dt;
    // Our gateway died at once (busy bridge port = a gateway is ALREADY up
    // outside the game): reuse it and its QR file instead of wedging on ours.
    if (!_qrLoaded && _gw != null && !IsGatewayProcessAlive()) {
      try { _gw.Dispose(); } catch (Exception) { }
      _gw = null;
      try {
        if (!string.IsNullOrEmpty(_toolsDir)) {
          string fallback = Path.Combine(_toolsDir, "phone-mic-qr.png");
          if (File.Exists(fallback)) _gwQrPath = fallback;
        }
      } catch (Exception) { }
    }
    // QR file poll (gateway writes it at STEP 3, ~1 s after launch).
    if (!_qrLoaded) {
      _qrPollTimer -= dt;
      if (_qrPollTimer <= 0f) {
        _qrPollTimer = 0.5f;
        if (TryLoadQr()) {
          try {
            if (_dialog != null) _dialog.SetWaitStatus(MicSetupDialog.StatusQrScan);
          } catch (Exception) { }
        } else if (_waitElapsed > 10f) {
          try {
            if (_dialog != null) _dialog.SetWaitStatus(MicSetupDialog.StatusQrManual);
          } catch (Exception) { }
        }
      }
    }
    // Continuous re-probe while the QR is up (single 6 s probe is not
    // enough for a 120 s wait).
    _waitProbeTimer -= dt;
    if (_waitProbeTimer <= 0f) {
      _waitProbeTimer = WaitReprobeSec;
      ProbeNow(2.5f, forWait: true);
    }
    if (!_skipShown && _waitElapsed >= SkipShowAfterSec) {
      _skipShown = true;
      try { if (_dialog != null) _dialog.SetSkipVisible(true); } catch (Exception) { }
    }
    if (_waitElapsed >= AutoSkipAfterSec) {
      try {
        UnityEngine.Debug.Log("[MicSetup] QR wait timed out after 120 s — auto-skipping listening.");
      } catch (Exception) { }
      OnSkipWaiting();
    }
  }

  bool TryLoadQr() {
    try {
      if (_dialog == null || string.IsNullOrEmpty(_gwQrPath)) return false;
      if (!File.Exists(_gwQrPath)) return false;
      byte[] png = File.ReadAllBytes(_gwQrPath);
      if (_dialog.SetQrImage(png)) {
        _qrLoaded = true;
        try {
          UnityEngine.Debug.Log("[MicSetup] QR shown (" + png.Length + "B) from " + _gwQrPath);
        } catch (Exception) { }
        return true;
      }
      return false;
    } catch (Exception) { return false; }
  }

  // --- embedded gateway (auto-start) -------------------------------------------
  // Starts tools/phone_mic_gateway.py as a child process. Never throws: any
  // failure (no python, no certs, tools dir missing in a player build) just
  // means manual mode — the status line tells the parent what to run.
  void TryStartGateway() {
    try {
      if (IsGatewayProcessAlive()) return;
      if (string.IsNullOrEmpty(_toolsDir) || !Directory.Exists(_toolsDir)) {
        Warn("[MicSetup] Gateway auto-start skipped: tools dir missing (" + _toolsDir + ")");
        return;
      }
      string script = Path.Combine(_toolsDir, "phone_mic_gateway.py");
      string cert = Path.Combine(_toolsDir, "lan.crt");
      string key = Path.Combine(_toolsDir, "lan.key");
      if (!File.Exists(script) || !File.Exists(cert) || !File.Exists(key)) {
        Warn("[MicSetup] Gateway auto-start skipped: missing "
          + (!File.Exists(script) ? "gateway.py " : "")
          + (!File.Exists(cert) ? "lan.crt " : "")
          + (!File.Exists(key) ? "lan.key" : ""));
        return;
      }
      string repoRoot = Directory.GetParent(_toolsDir).FullName;
      _gwQrPath = Path.Combine(_toolsDir, "phone-mic-qr-game.png");
      try { if (File.Exists(_gwQrPath)) File.Delete(_gwQrPath); } catch (Exception) { }
      string python = FindPython();
      if (string.IsNullOrEmpty(python)) {
        Warn("[MicSetup] Gateway auto-start skipped: no python on PATH (tried python/python3/py)");
        return;
      }
      string args = "\"" + script + "\" --cert \"" + cert + "\" --key \"" + key + "\""
        + " --https-port 8443 --bridge-port " + _port
        + " --qr-png \"" + _gwQrPath + "\" --no-ascii-qr";
      var psi = new ProcessStartInfo {
        FileName = python,
        Arguments = args,
        WorkingDirectory = repoRoot,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      };
      var proc = new Process { StartInfo = psi, EnableRaisingEvents = false };
      proc.OutputDataReceived += OnGatewayOutput;
      proc.ErrorDataReceived += OnGatewayOutput;
      if (!proc.Start()) return;
      try { proc.BeginOutputReadLine(); } catch (Exception) { }
      try { proc.BeginErrorReadLine(); } catch (Exception) { }
      _gw = proc;
      try {
        UnityEngine.Debug.Log("[MicSetup] Gateway auto-started (pid "
          + proc.Id + "). Scan the in-game QR with the phone camera.");
      } catch (Exception) { }
    } catch (Exception) { _gw = null; }
  }

  void OnGatewayOutput(object sender, DataReceivedEventArgs e) {
    try {
      string line = e != null ? e.Data : null;
      if (string.IsNullOrEmpty(line)) return;
      lock (_gwLogLock) {
        _gwLog.Enqueue(line);
        while (_gwLog.Count > 30) _gwLog.Dequeue();
      }
      if (line.IndexOf("PAGE URL", StringComparison.OrdinalIgnoreCase) >= 0)
        _gwPageUrl = line.Trim();
      // Surface progress into the waiting status line (main thread picks it
      // up — this callback runs on a worker thread, never touch Unity here).
    } catch (Exception) { }
  }

  // Production telemetry: auto-start failures must be VISIBLE (a silent
  // fallback to manual instructions leaves parents stuck). Warning, not
  // error: manual mode still works.
  static void Warn(string msg) {
    try { UnityEngine.Debug.LogWarning(msg); } catch (Exception) { }
  }

  bool IsGatewayProcessAlive() {
    try {
      if (_gw == null) return false;
      return !_gw.HasExited;
    } catch (Exception) { return false; }
  }

  void StopGateway() {
    try {
      if (_gw != null) {
        try {
          if (!_gw.HasExited) {
            try { _gw.Kill(); } catch (Exception) { }
          }
        } catch (Exception) { }
        try { _gw.Dispose(); } catch (Exception) { }
      }
    } catch (Exception) { }
    _gw = null;
  }

  static string FindPython() {
    // PATH lookup only (no registry): "python" first, then the "py" launcher.
    string[] candidates = { "python", "python3", "py" };
    foreach (string c in candidates) {
      try {
        var psi = new ProcessStartInfo {
          FileName = c,
          Arguments = "--version",
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true,
        };
        using (var p = Process.Start(psi)) {
          if (p == null) continue;
          if (p.WaitForExit(3000) && p.ExitCode == 0) return c;
          try { if (!p.HasExited) p.Kill(); } catch (Exception) { }
        }
      } catch (Exception) { }
    }
    return null;
  }

  void OnDestroy() {
    try { Time.timeScale = 1f; } catch (Exception) { }
    try { StopWatcher(); } catch (Exception) { }
    try { StopGateway(); } catch (Exception) { }
  }

  void ShowOffer() {
    if (_dialog == null) return;
    try { _dialog.ShowOffer(OnAccept, OnDecline); } catch (Exception) { }
  }

  // --- background ----------------------------------------------------------
  void PollLocal() {
    try {
      if (_local != null) _local.Refresh();
      _lastLocalPollTick = TickMs();
      if (_gate != null) _gate.NotifySourcesChanged();
      // Headset plugged while a dialog is open: recovery wins silently.
      if (_dialog != null && _dialog.IsShowing
          && (_gate.State == MicSetupState.ReadyLocal
            || _gate.State == MicSetupState.ReadyPhone)) {
        if (_waitActive) FinishLinked();
        else { try { _dialog.Hide(); } catch (Exception) { } }
      }
    } catch (Exception) { }
  }

  void ProbeNow(float waitSec, bool forWait) {
    int serial = ++_probeSerial;
    _probeForWait = forWait;
    string host = _host;
    int port = _port;
    try {
      PhoneLinkProbe.ProbeAsync(host, port, waitSec, CancellationToken.None)
        .ContinueWith(t => {
          PhoneLinkState r = PhoneLinkState.Unknown;
          try {
            if (t.Status == TaskStatus.RanToCompletion) r = t.Result;
          } catch (Exception) { r = PhoneLinkState.Unknown; }
          _probeResult = r;
          _probeResultSerial = serial;
          _probePending = true;
        }, TaskScheduler.Default);
    } catch (Exception) { }
  }

  void ApplyProbeResult() {
    if (!_probePending) return;
    _probePending = false;
    if (_probeResultSerial != _probeSerial) return; // stale: a newer probe won
    HandleLinkObservation(_probeResult, _probeForWait);
  }

  // Shared link-state application (one-shot probes AND the persistent
  // watcher). Silent by policy: no mid-play popups; loss -> quiet skip,
  // recovery -> quiet ready; the next exercise entry re-offers if skipped.
  void HandleLinkObservation(PhoneLinkState link, bool forWait) {
    PhoneLinkState observed = link;
    try {
      if (observed == PhoneLinkState.PhoneLinked && _phone != null) {
        try { _phone.ReportLinkUp("phone"); } catch (Exception) { }
      } else if (observed == PhoneLinkState.GatewayDown && _phone != null
          && (_gate.State == MicSetupState.ReadyPhone
            || _gate.State == MicSetupState.WaitPhoneLink)) {
        // Gateway died while we depended on it: drop the phone side. Idle
        // phone (GatewayUpNoPhone) at rest is NORMAL — never downgrades.
        try { _phone.ReportLinkDown(); } catch (Exception) { }
      }
      _gate.OnPhoneLink(observed);
      _gate.NotifySourcesChanged();
      if (_dialog == null) return;
      if (_gate.State == MicSetupState.ReadyPhone
          || _gate.State == MicSetupState.ReadyLocal) {
        // Linked (or headset took over): QR off, game resumes. The gateway
        // keeps running for speech captures; skip only stops it.
        if (_waitActive) FinishLinked();
        else { try { _dialog.Hide(); } catch (Exception) { } }
        return;
      }
      if (forWait && _dialog.IsWaitShowing) {
        if (observed == PhoneLinkState.GatewayDown)
          _dialog.SetWaitStatus(MicSetupDialog.StatusGatewayDown);
        else if (observed == PhoneLinkState.GatewayUpNoPhone)
          _dialog.SetWaitStatus(MicSetupDialog.StatusNoPhone);
        else
          _dialog.SetWaitStatus(MicSetupDialog.StatusWaiting);
      }
    } catch (Exception) { }
  }

  // --- persistent presence (mid-game drops + explicit STOP) --------------------
  // Runs while a phone state is active (WAIT or ReadyPhone). Edges from the
  // watcher collapse to the latest: a DOWN after an AUDIO burst still wins.
  void UpdatePresenceWatch() {
    try {
      bool wantWatch = _gate.State == MicSetupState.WaitPhoneLink
        || _gate.State == MicSetupState.ReadyPhone;
      if (wantWatch) EnsureWatcher();
      else StopWatcher();
      if (_watcher == null) return;
      int seq;
      WatcherEvent ev;
      uint serial;
      string reason;
      _watcher.ReadState(out seq, out ev, out serial, out reason);
      if (seq == _watcherAppliedSeq) return;
      _watcherAppliedSeq = seq;
      ApplyWatcherEdge(ev, serial, reason);
    } catch (Exception) { }
  }

  void EnsureWatcher() {
    try {
      if (_watcher == null) {
        _watcher = new PhonePresenceWatcher(_host, _port);
        _watcherAppliedSeq = 0;
      }
      if (!_watcher.IsRunning) _watcher.Start();
    } catch (Exception) { }
  }

  void StopWatcher() {
    try {
      if (_watcher != null) {
        try { _watcher.Dispose(); } catch (Exception) { }
        _watcher = null;
      }
    } catch (Exception) { _watcher = null; }
  }

  void ApplyWatcherEdge(WatcherEvent ev, uint serial, string reason) {
    try {
      switch (ev) {
        case WatcherEvent.PhoneAudio:
          // Live session proof (same weight as a probe hit): link it now
          // instead of waiting for the next 3 s re-probe.
          _linkDownApplied = false; // sign of life: re-arm down edges
          // Log once per serial: chunk bursts of the same session skip the
          // log (full stack each) but still apply the idempotent link state.
          if (!_audioLoggedOnce || serial != _lastAudioLoggedSerial) {
            _audioLoggedOnce = true;
            _lastAudioLoggedSerial = serial;
            try {
              UnityEngine.Debug.Log("[MicSetup] Phone audio live (serial "
                + serial + ") — link confirmed.");
            } catch (Exception) { }
          }
          HandleLinkObservation(PhoneLinkState.PhoneLinked, _waitActive);
          break;
        case WatcherEvent.PhoneUp:
          _linkDownApplied = false; // sign of life: re-arm down edges
          try {
            UnityEngine.Debug.Log("[MicSetup] Phone page opened — waiting for START.");
          } catch (Exception) { }
          if (_dialog != null && _dialog.IsWaitShowing)
            _dialog.SetWaitStatus(MicSetupDialog.StatusPhoneOpen);
          break;
        case WatcherEvent.PhoneStopped:
          // Explicit STOP with the page possibly still open: NOT a drop —
          // stay Ready (next START resumes). Visible so it is "detected".
          try {
            UnityEngine.Debug.Log("[MicSetup] Phone STOP (serial "
              + serial + ") — page idle, START resumes next exercise.");
          } catch (Exception) { }
          if (_dialog != null && _dialog.IsWaitShowing)
            _dialog.SetWaitStatus(MicSetupDialog.StatusPhoneStopped);
          break;
        case WatcherEvent.PhoneDown:
        case WatcherEvent.TransportLost:
        case WatcherEvent.GatewayDown: {
          // Dedup: same down-episode re-posts on every reconnect tick.
          // Apply (and log) once; recovery signs re-arm above.
          if (_linkDownApplied) break;
          _linkDownApplied = true;
          _audioLoggedOnce = false; // next session logs even if serial repeats
          string why = ev == WatcherEvent.PhoneDown ? "phone page gone"
            : ev == WatcherEvent.TransportLost ? "bridge lost" : "gateway down";
          try {
            UnityEngine.Debug.LogWarning("[MicSetup] Phone link lost (" + why
              + ") — quiet skip, next exercise re-offers.");
          } catch (Exception) { }
          if (_phone != null) {
            try { _phone.ReportLinkDown(); } catch (Exception) { }
          }
          _gate.NotifySourcesChanged(); // silent -> Skipped when nothing Ready
          if (_dialog != null && _dialog.IsWaitShowing) {
            _dialog.SetWaitStatus(ev == WatcherEvent.PhoneDown
              ? MicSetupDialog.StatusPhoneLost
              : MicSetupDialog.StatusGatewayDown);
          }
          break;
        }
        default:
          break; // None / TransportOk: nothing to apply
      }
    } catch (Exception) { }
  }
}
