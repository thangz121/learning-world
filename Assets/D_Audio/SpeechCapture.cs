// D_Audio/SpeechCapture.cs — Phase 2.1 capture (Agent D).
// Unity Microphone wrapper (§20): sample rate/channels/buffer/duration, start/
// stop/cancel/timeout/silence. The buffer is DISCARDED after recognition —
// never persisted, never uploaded except to the configured provider (§21).
// Main-thread safe: Microphone.End + GetData run synchronously on short clips;
// the provider round-trip (network/inference) stays off the gameplay path via
// Task + CancellationToken (§47).
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class UnityMicrophoneCapture : ISpeechAudioCapture {
  readonly IMicrophoneDevice _devices;
  readonly int _sampleRate;
  volatile bool _capturing;
  volatile bool _cancelRequested;

  public UnityMicrophoneCapture(IMicrophoneDevice devices, int sampleRate) {
    _devices = devices ?? throw new ArgumentNullException(nameof(devices));
    _sampleRate = sampleRate > 0 ? sampleRate : 16000;
  }

  public UnityMicrophoneCapture(IMicrophoneDevice devices) : this(devices, 16000) { }

  public bool IsCapturing => _capturing;

  public void Cancel() { _cancelRequested = true; }

  public async Task<CapturedSpeech> CaptureAsync(float maxDurationSec, float silenceTimeoutSec, CancellationToken ct) {
    _cancelRequested = false;
    if (_devices.Status != MicStatus.Ready || string.IsNullOrEmpty(_devices.SelectedDevice)) {
      return new CapturedSpeech { Error = SpeechFailureReasons.MicUnavailable };
    }
    if (maxDurationSec <= 0f) maxDurationSec = 8f;
    if (silenceTimeoutSec <= 0f) silenceTimeoutSec = 3f;

    string device = _devices.SelectedDevice;
    AudioClip clip = null;
    try {
      clip = Microphone.Start(device, false, Mathf.CeilToInt(maxDurationSec), _sampleRate);
    } catch (Exception) {
      _devices.ReportCaptureFailure();
      return new CapturedSpeech { Error = SpeechFailureReasons.DeviceError };
    }
    if (clip == null) {
      _devices.ReportCaptureFailure();
      return new CapturedSpeech { Error = SpeechFailureReasons.DeviceError };
    }

    _capturing = true;
    int startPos = 0;
    float elapsed = 0f;
    float silenceFor = 0f;
    bool heardVoice = false;
    const float sliceSec = 0.1f;
    try {
      while (elapsed < maxDurationSec) {
        await Task.Delay(TimeSpan.FromSeconds(sliceSec), ct).ConfigureAwait(false);
        elapsed += sliceSec;
        if (_cancelRequested || ct.IsCancellationRequested) {
          return Finish(clip, device, elapsed, true, false);
        }
        float energy = TailEnergy(clip, device, ref startPos);
        if (energy > 0.004f) { heardVoice = true; silenceFor = 0f; }
        else silenceFor += sliceSec;
        // Silence timeout only AFTER some voice (lets quiet kids start, §33);
        // a fully silent window still exits via maxDuration (NO_SPEECH, not hang).
        if (heardVoice && silenceFor >= silenceTimeoutSec) break;
      }
      return Finish(clip, device, elapsed, false, elapsed >= maxDurationSec);
    } catch (OperationCanceledException) {
      return Finish(clip, device, elapsed, true, false);
    } catch (Exception) {
      _devices.ReportCaptureFailure();
      try { Microphone.End(device); } catch (Exception) { }
      return new CapturedSpeech { Error = SpeechFailureReasons.DeviceError };
    } finally {
      _capturing = false;
    }
  }

  CapturedSpeech Finish(AudioClip clip, string device, float elapsed, bool cancelled, bool timedOut) {
    int pos = 0;
    try { pos = Microphone.GetPosition(device); } catch (Exception) { pos = 0; }
    try { Microphone.End(device); } catch (Exception) { }
    int take = Math.Min(pos, clip.samples);
    if (take <= 0) {
      UnityEngine.Object.Destroy(clip);
      return new CapturedSpeech {
        Samples = new float[0], SampleRate = _sampleRate, Channels = clip.channels,
        DurationSec = 0f, MeanEnergy = 0f, PeakEnergy = 0f, VoicedSec = 0f,
        TimedOut = timedOut, Cancelled = cancelled, Error = string.Empty
      };
    }
    float[] data = new float[take * clip.channels];
    try {
      clip.GetData(data, 0);
    } catch (Exception) {
      UnityEngine.Object.Destroy(clip);
      return new CapturedSpeech { Error = SpeechFailureReasons.DeviceError };
    }
    UnityEngine.Object.Destroy(clip);
    float mean = 0f, peak = 0f, voiced = 0f;
    const int window = 160; // 10ms @16kHz
    int voicedWindows = 0, totalWindows = 0;
    for (int i = 0; i < data.Length; i++) {
      float a = Math.Abs(data[i]);
      mean += a;
      if (a > peak) peak = a;
      if (i % window == window - 1) {
        totalWindows++;
        // window energy recomputed cheaply: reuse running slice
        float wsum = 0f;
        for (int k = i - window + 1; k <= i; k++) wsum += Math.Abs(data[k]);
        if (wsum / window > 0.004f) voicedWindows++;
      }
    }
    mean /= Math.Max(1, data.Length);
    voiced = totalWindows > 0 ? (float)voicedWindows / totalWindows * ((float)take / _sampleRate) : 0f;
    return new CapturedSpeech {
      Samples = data, SampleRate = _sampleRate, Channels = clip.channels,
      DurationSec = (float)take / _sampleRate, MeanEnergy = mean, PeakEnergy = peak,
      VoicedSec = voiced, TimedOut = timedOut, Cancelled = cancelled, Error = string.Empty
    };
  }

  // Energy of samples appended since last call (poll cursor, no allocation).
  float TailEnergy(AudioClip clip, string device, ref int cursor) {
    int pos = 0;
    try { pos = Microphone.GetPosition(device); } catch (Exception) { return 0f; }
    return TailEnergyOn(clip, ref cursor, pos);
  }

  float TailEnergyOn(AudioClip clip, ref int cursor, int pos) {
    if (pos <= cursor) return 0f;
    int n = Math.Min(pos - cursor, clip.samples / 10);
    if (n <= 0) return 0f;
    float[] buf = new float[n * clip.channels];
    try {
      clip.GetData(buf, Math.Max(0, pos - n));
    } catch (Exception) { return 0f; }
    float sum = 0f;
    for (int i = 0; i < buf.Length; i++) sum += Math.Abs(buf[i]);
    cursor = pos;
    return sum / Math.Max(1, buf.Length);
  }
}

// Deterministic test double: scripted captures, no hardware.
public sealed class FakeSpeechCapture : ISpeechAudioCapture {
  readonly Func<CapturedSpeech> _next;
  public bool IsCapturing { get; private set; }
  public int CaptureCalls { get; private set; }

  public FakeSpeechCapture(Func<CapturedSpeech> next) { _next = next; }

  public Task<CapturedSpeech> CaptureAsync(float maxDurationSec, float silenceTimeoutSec, CancellationToken ct) {
    CaptureCalls++;
    IsCapturing = true;
    try {
      if (ct.IsCancellationRequested) {
        var c = _next != null ? _next() : new CapturedSpeech();
        c.Cancelled = true;
        return Task.FromResult(c);
      }
      CapturedSpeech s = _next != null ? _next() : new CapturedSpeech();
      return Task.FromResult(s);
    } finally {
      IsCapturing = false;
    }
  }

  public void Cancel() { }
}

// Shared VAD/energy helpers (pure, unit-testable).
public static class VoiceActivity {
  // Normalized mean-absolute-energy speech test with hysteresis margins.
  public static bool HasSpeech(float meanEnergy, float floor) {
    return meanEnergy >= floor;
  }

  public static float MeanAbsolute(float[] samples) {
    if (samples == null || samples.Length == 0) return 0f;
    double sum = 0;
    for (int i = 0; i < samples.Length; i++) sum += Math.Abs(samples[i]);
    return (float)(sum / samples.Length);
  }
}
