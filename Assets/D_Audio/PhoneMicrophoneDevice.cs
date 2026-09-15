// D_Audio/PhoneMicrophoneDevice.cs — Phase 2.1-local M6 phone capability.
// Additive only. IMicrophoneDevice over the phone-link state (§10): the phone
// is Ready ONLY while the gateway reports a live link; anything else maps to
// the existing MicStatus vocabulary (NoDevice / PermissionDenied / Error) so
// SpeechRecognizer's no-mic short-circuit and the runner's SkippedNoMic +
// deferral paths work UNCHANGED. Phone link state NEVER becomes speech
// evidence: it only gates eligibility (environment vs child, §10/§11).
using System;

public sealed class PhoneMicrophoneDevice : IMicrophoneDevice, IPhoneLinkDevice {
  MicStatus _status = MicStatus.NoDevice;
  string _selected; // phone link label, e.g. "phone@192.168.1.20"

  public event Action<MicStatus> StatusChanged;

  public PhoneMicrophoneDevice() { }

  public MicStatus Status => _status;
  public string SelectedDevice => _selected;

  public string[] Devices {
    get { return _status == MicStatus.NoDevice ? new string[0]
      : new[] { _selected ?? "phone" }; }
  }

  public SpeechCapability Capability {
    get {
      int count = _status == MicStatus.NoDevice ? 0 : 1;
      return new SpeechCapability {
        Status = _status,
        DeviceName = _selected ?? string.Empty,
        DeviceCount = count
      };
    }
  }

  // Gateway callbacks (link lifecycle, §9/§12). Each session gets a FRESH
  // device epoch: ReportLinkDown clears the label so a reconnect can never
  // inherit the previous session's identity.
  public void ReportLinkUp(string phoneLabel) {
    _selected = string.IsNullOrEmpty(phoneLabel) ? "phone" : phoneLabel;
    SetStatus(MicStatus.Ready);
  }

  public void ReportLinkDown() {
    _selected = null;
    SetStatus(MicStatus.NoDevice);
  }

  public void ReportPermissionDenied() {
    SetStatus(MicStatus.PermissionDenied);
  }

  public void ReportLinkError() {
    SetStatus(MicStatus.Error);
  }

  // Poll seam (Unity has no link event): the harness/gateway pump calls this;
  // state only changes via the Report* methods above, so Refresh is a no-op
  // that simply re-announces current state for late subscribers.
  public void Refresh() { }

  public void SelectDevice(string name) {
    // Single logical device: ignore pins to unknown names (like the PC
    // service ignores unknown names). Never throws.
  }

  public void ReportCaptureFailure() {
    SetStatus(MicStatus.Error);
  }

  void SetStatus(MicStatus next) {
    if (next == _status) return;
    _status = next;
    try { StatusChanged?.Invoke(next); } catch (Exception) { }
  }
}
