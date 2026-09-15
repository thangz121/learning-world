// D_Audio/CompositeMicrophoneDevice.cs — Phase 2.1 mic-setup gate (Agent D).
// Pure C# (NO UnityEngine). Presents "local PC mic OR phone mic" as ONE
// IMicrophoneDevice so the FROZEN pipeline (SpeechRecognizer short-circuit,
// SpeakingExerciseRunner SkippedNoMic/deferral) works UNCHANGED: it already
// skips/defers when Capability is unavailable, which is exactly the
// "tạm thời bỏ qua bài nghe" policy the setup gate needs.
//
// Precedence: local mic wins when both are Ready (lower latency, no LAN in
// the path); the phone is the fallback. StatusChanged re-fires whenever the
// EFFECTIVE status changes (either source flips).
using System;

public sealed class CompositeMicrophoneDevice : IMicrophoneDevice {
  readonly IMicrophoneDevice _local;
  readonly IMicrophoneDevice _phone;
  MicStatus _effective = MicStatus.Unknown;

  public event Action<MicStatus> StatusChanged;

  public CompositeMicrophoneDevice(IMicrophoneDevice local, IMicrophoneDevice phone) {
    _local = local ?? throw new ArgumentNullException(nameof(local));
    _phone = phone ?? throw new ArgumentNullException(nameof(phone));
    _local.StatusChanged += OnSourceChanged;
    _phone.StatusChanged += OnSourceChanged;
    Recompute();
  }

  public MicStatus Status => _effective;

  // Prefers the local label when both are Ready (tells the developer log
  // which path the audio will take).
  public string SelectedDevice {
    get {
      try {
        if (_local.Capability.IsAvailable()) return _local.SelectedDevice;
        if (_phone.Capability.IsAvailable()) return _phone.SelectedDevice;
      } catch (Exception) { }
      return _local.SelectedDevice ?? _phone.SelectedDevice;
    }
  }

  public string[] Devices {
    get {
      string[] a = SafeDevices(_local);
      string[] b = SafeDevices(_phone);
      var all = new string[a.Length + b.Length];
      Array.Copy(a, all, a.Length);
      Array.Copy(b, 0, all, a.Length, b.Length);
      return all;
    }
  }

  public SpeechCapability Capability {
    get {
      if (_local.Capability.IsAvailable()) return _local.Capability;
      if (_phone.Capability.IsAvailable()) return _phone.Capability;
      // Neither Ready: report the LOCAL state (NoDevice/PermissionDenied/
      // Error) so downstream keeps the most actionable reason. Phone-down
      // while local-down adds no new information.
      return _local.Capability;
    }
  }

  public void Refresh() {
    try { _local.Refresh(); } catch (Exception) { }
    try { _phone.Refresh(); } catch (Exception) { } // no-op, re-announces
    Recompute();
  }

  // A listed-but-broken device belongs to the source currently in use.
  public void ReportCaptureFailure() {
    try {
      if (_local.Capability.IsAvailable()) _local.ReportCaptureFailure();
      else _phone.ReportCaptureFailure();
    } catch (Exception) { }
  }

  void OnSourceChanged(MicStatus unused) {
    Recompute();
  }

  void Recompute() {
    MicStatus next;
    try {
      if (_local.Capability.IsAvailable()) next = MicStatus.Ready;
      else if (_phone.Capability.IsAvailable()) next = MicStatus.Ready;
      else next = _local.Status; // actionable reason comes from the PC side
    } catch (Exception) {
      next = MicStatus.Error;
    }
    if (next == _effective) return;
    _effective = next;
    try { StatusChanged?.Invoke(next); } catch (Exception) { }
  }

  static string[] SafeDevices(IMicrophoneDevice d) {
    try { return d.Devices ?? new string[0]; } catch (Exception) { return new string[0]; }
  }
}
