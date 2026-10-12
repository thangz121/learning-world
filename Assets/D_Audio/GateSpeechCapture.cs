// D_Audio/GateSpeechCapture.cs — one capture seam over the mic-setup gate:
// ReadyLocal -> the PC capture, ReadyPhone -> the phone capture, anything else
// -> an honest mic_unavailable segment (the arena defers, never fails).
// C# 9.0 only.
using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class GateSpeechCapture : ISpeechAudioCapture {
  readonly ISpeechAudioCapture _local;
  readonly ISpeechAudioCapture _phone;
  readonly MicSetupGate _gate;

  public GateSpeechCapture(ISpeechAudioCapture local, ISpeechAudioCapture phone, MicSetupGate gate) {
    _local = local;
    _phone = phone;
    _gate = gate;
  }

  public bool IsCapturing {
    get {
      try {
        return (_local != null && _local.IsCapturing) || (_phone != null && _phone.IsCapturing);
      } catch (Exception) { return false; }
    }
  }

  public Task<CapturedSpeech> CaptureAsync(float maxDurationSec, float silenceTimeoutSec,
      CancellationToken ct) {
    if (_gate != null && _gate.State == MicSetupState.ReadyPhone && _phone != null) {
      return _phone.CaptureAsync(maxDurationSec, silenceTimeoutSec, ct);
    }
    if (_local != null) {
      // ReadyLocal, Checking or Skipped: attempt locally; the capture itself
      // reports mic_unavailable when no device is usable.
      return _local.CaptureAsync(maxDurationSec, silenceTimeoutSec, ct);
    }
    if (_phone != null) return _phone.CaptureAsync(maxDurationSec, silenceTimeoutSec, ct);
    return Task.FromResult(new CapturedSpeech { Error = SpeechFailureReasons.MicUnavailable });
  }

  public void Cancel() {
    try { if (_local != null) _local.Cancel(); } catch (Exception) { }
    try { if (_phone != null) _phone.Cancel(); } catch (Exception) { }
  }
}
