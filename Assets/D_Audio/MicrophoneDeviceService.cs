// D_Audio/MicrophoneDeviceService.cs — Phase 2.1 device lifecycle (Agent D).
// Responsibilities (§42): enumerate, select, readiness state, change detection.
// Unity exposes NO mic hot-plug event: detection is poll-based (Refresh() diffs
// Microphone.devices against the last snapshot; gameplay polls ~1Hz).
// devices.Length > 0 alone never proves usability (§11): a failed capture still
// flips status to Error. Authorization: Web platform needs explicit request;
// elsewhere we treat empty-list-at-startup as NoDevice, not denial.
using System;
using UnityEngine;

public sealed class MicrophoneDeviceService : IMicrophoneDevice {
  readonly Func<string[]> _deviceLister;
  readonly Func<string, bool> _permissionProbe; // optional: true = authorized
  string[] _devices = new string[0];
  string _selected;
  MicStatus _status = MicStatus.Unknown;

  public event Action<MicStatus> StatusChanged;

  public MicrophoneDeviceService()
    : this(ListSystemDevices, null) {
  }

  // Test seam: inject scripted device lists (hot-plug simulation without hardware).
  public MicrophoneDeviceService(Func<string[]> deviceLister, Func<string, bool> permissionProbe) {
    _deviceLister = deviceLister ?? ListSystemDevices;
    _permissionProbe = permissionProbe;
    Refresh();
  }

  public MicStatus Status => _status;
  public string SelectedDevice => _selected;
  public string[] Devices => (string[])_devices.Clone();

  public SpeechCapability Capability {
    get {
      return new SpeechCapability {
        Status = _status,
        DeviceName = _selected ?? string.Empty,
        DeviceCount = _devices.Length
      };
    }
  }

  public void Refresh() {
    string[] fresh;
    try {
      fresh = _deviceLister() ?? new string[0];
    } catch (Exception) {
      SetStatus(MicStatus.Error);
      return;
    }
    bool changed = fresh.Length != _devices.Length;
    if (!changed) {
      for (int i = 0; i < fresh.Length; i++) {
        if (Array.IndexOf(_devices, fresh[i]) < 0) { changed = true; break; }
      }
    }
    // Disappearance of the selected device also counts as a change.
    if (!changed && _selected != null && fresh.Length > 0 && Array.IndexOf(fresh, _selected) < 0)
      changed = true;
    _devices = fresh;
    if (_devices.Length == 0) {
      _selected = null;
      SetStatus(MicStatus.NoDevice);
      return;
    }
    if (_selected == null || Array.IndexOf(_devices, _selected) < 0) {
      _selected = MicDeviceClassifier.PickDevice(_devices); // ranked default
    } else {
      // Hot-plug takeover (user rule): a strictly better-ranked newcomer
      // wins (plug a USB mic => the game uses it); equal rank never flaps
      // the active device mid-session.
      string best = null;
      try { best = MicDeviceClassifier.PickDevice(_devices); } catch (Exception) { }
      if (!string.IsNullOrEmpty(best) && best != _selected) {
        int br = 99, cr = 99;
        try { br = MicDeviceClassifier.Rank(best); } catch (Exception) { }
        try { cr = MicDeviceClassifier.Rank(_selected); } catch (Exception) { }
        if (br < cr) {
          _selected = best;
          try { Debug.Log("[MicSetup] local mic selected: " + _selected); }
          catch (Exception) { }
          if (_status == MicStatus.Ready) {
            try { StatusChanged?.Invoke(_status); } catch (Exception) { }
          }
        }
      }
    }
    if (_permissionProbe != null) {
      bool ok = false;
      try { ok = _permissionProbe(_selected); } catch (Exception) { ok = false; }
      SetStatus(ok ? MicStatus.Ready : MicStatus.PermissionDenied);
      return;
    }
    SetStatus(MicStatus.Ready);
  }

  // Gameplay may pin a preferred device (settings UI future). Unknown name = ignore.
  public void SelectDevice(string name) {
    if (string.IsNullOrEmpty(name) || Array.IndexOf(_devices, name) < 0) return;
    if (_selected != name) {
      _selected = name;
      if (_status == MicStatus.Ready) {
        try { StatusChanged?.Invoke(_status); } catch (Exception) { }
      }
    }
  }

  // Called by the capture layer when Start/End throws: device listed but unusable.
  public void ReportCaptureFailure() {
    SetStatus(MicStatus.Error);
  }

  void SetStatus(MicStatus next) {
    if (next == _status) return;
    _status = next;
    try { StatusChanged?.Invoke(next); } catch (Exception) { }
  }

  static string[] ListSystemDevices() {
    try {
      return Microphone.devices ?? new string[0];
    } catch (Exception) {
      return new string[0];
    }
  }
}
