// D_Audio/ScriptedWavCapture.cs — DEV/TEST seam (flag-gated): replays WAV files
// instead of a physical microphone so a FOREGROUND journey can drive the REAL
// speaking pipeline (hybrid provider -> policy -> game) deterministically.
// Inert in production: constructed ONLY when GameInstaller sees
// "-speak-wav-dir <dir>" on the command line (never in a normal launch).
//
// Files: every *.wav in the dir (sorted by name) is consumed one per capture,
// cycling when exhausted; each pick is logged:
//   [ScriptedWav] capture #k -> <file>
// Accepts PCM16 mono/stereo WAV at any rate (linear-resampled to 16 kHz), and
// short PCM float32 WAVs (format 3). C# 9.0 only.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class ScriptedWavCapture : ISpeechAudioCapture {
  readonly List<string> _files = new List<string>();
  int _index;

  public ScriptedWavCapture(string dir) {
    try {
      if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) {
        string[] hits = Directory.GetFiles(dir, "*.wav");
        Array.Sort(hits, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < hits.Length; i++) _files.Add(hits[i]);
      }
    } catch (Exception) { }
  }

  public int Count { get { return _files.Count; } }

  public bool IsCapturing { get { return false; } }

  public void Cancel() { }

  public Task<CapturedSpeech> CaptureAsync(float maxDurationSec, float silenceTimeoutSec,
      CancellationToken ct) {
    var cs = new CapturedSpeech();
    if (_files.Count == 0) {
      cs.Samples = new float[0];
      cs.SampleRate = 16000;
      cs.Channels = 1;
      cs.Error = SpeechFailureReasons.MicUnavailable;
      return Task.FromResult(cs);
    }
    int pick = _index % _files.Count;
    _index++;
    string file = _files[pick];
    float[] samples;
    try {
      samples = ReadWav16k(file);
    } catch (Exception ex) {
      samples = new float[0];
      Debug.LogWarning("[ScriptedWav] read failed: " + file + " (" + ex.Message + ")");
    }
    cs.Samples = samples;
    cs.SampleRate = 16000;
    cs.Channels = 1;
    cs.DurationSec = samples.Length / 16000f;
    cs.MeanEnergy = VoiceActivity.MeanAbsolute(samples);
    float peak = 0f;
    for (int i = 0; i < samples.Length; i++) {
      float a = samples[i] < 0f ? -samples[i] : samples[i];
      if (a > peak) peak = a;
    }
    cs.PeakEnergy = peak;
    cs.VoicedSec = VoiceActivity.HasSpeech(cs.MeanEnergy, 0.001f) ? cs.DurationSec : 0f;
    cs.TimedOut = false;
    cs.Cancelled = false;
    cs.Error = "";
    Debug.Log("[ScriptedWav] capture #" + _index + " -> " + Path.GetFileName(file)
      + " (" + cs.DurationSec.ToString("F2") + "s mean=" + cs.MeanEnergy.ToString("F4") + ")");
    return Task.FromResult(cs);
  }

  // Minimal RIFF reader: PCM16 (fmt 1) mono/stereo, or float32 (fmt 3); any
  // sample rate -> linear resample to 16 kHz mono. No Unity audio dependency.
  public static float[] ReadWav16k(string path) {
    byte[] b = File.ReadAllBytes(path);
    if (b.Length < 44 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F') {
      throw new InvalidDataException("not RIFF");
    }
    int pos = 12;
    int fmt = 0, channels = 1, bits = 16, rate = 16000;
    int dataAt = -1, dataLen = 0;
    while (pos + 8 <= b.Length) {
      string id = "" + (char)b[pos] + (char)b[pos + 1] + (char)b[pos + 2] + (char)b[pos + 3];
      int size = BitConverter.ToInt32(b, pos + 4);
      int body = pos + 8;
      if (id == "fmt ") {
        fmt = BitConverter.ToInt16(b, body);
        channels = BitConverter.ToInt16(b, body + 2);
        rate = BitConverter.ToInt32(b, body + 4);
        bits = BitConverter.ToInt16(b, body + 14);
      } else if (id == "data") {
        dataAt = body;
        dataLen = Math.Min(size, b.Length - body);
        break;
      }
      pos = body + size + (size & 1);
    }
    if (dataAt < 0) throw new InvalidDataException("no data chunk");
    int bytesPerSample = bits / 8;
    if (bytesPerSample <= 0) bytesPerSample = 2;
    int frames = dataLen / (bytesPerSample * Math.Max(1, channels));
    float[] mono = new float[frames];
    for (int i = 0; i < frames; i++) {
      float sum = 0f;
      for (int c = 0; c < channels; c++) {
        int at = dataAt + (i * channels + c) * bytesPerSample;
        float v;
        if (fmt == 3 && bytesPerSample == 4) v = BitConverter.ToSingle(b, at);
        else if (bytesPerSample == 2) v = BitConverter.ToInt16(b, at) / 32768f;
        else v = (b[at] - 128) / 128f;
        sum += v;
      }
      mono[i] = sum / channels;
    }
    if (rate == 16000) return mono;
    int n = Math.Max(1, (int)((long)frames * 16000 / rate));
    float[] out16 = new float[n];
    for (int i = 0; i < n; i++) {
      float src = (float)i * (frames - 1) / Math.Max(1, n - 1);
      int i0 = (int)src;
      int i1 = Math.Min(frames - 1, i0 + 1);
      float t = src - i0;
      out16[i] = mono[i0] * (1f - t) + mono[i1] * t;
    }
    return out16;
  }
}

// Scripted device capability for the wav seam: SpeechRecognizer gates on the
// DEVICE capability before it ever asks the capture, so a scripted run must
// present a Ready device (no physical mic involved, no offer-dialog effect).
public sealed class ScriptedWavDevice : IMicrophoneDevice {
  public event Action<MicStatus> StatusChanged { add { } remove { } }
  public MicStatus Status { get { return MicStatus.Ready; } }
  public string SelectedDevice { get { return "scripted-wav"; } }
  public string[] Devices { get { return new string[] { "scripted-wav" }; } }
  public SpeechCapability Capability {
    get {
      return new SpeechCapability {
        Status = MicStatus.Ready, DeviceName = "scripted-wav", DeviceCount = 1
      };
    }
  }
  public void Refresh() { }
  public void ReportCaptureFailure() { }
}
