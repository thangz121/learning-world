// D_Audio/NetworkMicrophoneCapture.cs — Phase 2.1-local M6 phone input.
// Additive only. ISpeechAudioCapture over phone-over-LAN audio (§3):
//   UnityMicrophoneCapture + NetworkMicrophoneCapture -> same SpeechRecognizer.
// The recognizer CANNOT tell phone audio from PC audio: both arrive as
// canonical CapturedSpeech (mono float32 @ 16 kHz). No DSP, no policy, no
// threshold lives here — transport only (NETWORK -> AUDIO, §6).
//
// Failure mapping (environment NEVER child, §10/§11):
// - transport down at start  -> Error=mic_unavailable (runner defers, no event)
// - link drop mid-capture    -> Error=network_error (Unclear/env, retryable)
// - cancel                   -> Cancelled (recognizer throws, no assessment)
// - clean stop, no voice     -> empty segment (policy NoSpeech, not an error)
// Session isolation (§12): the first sessionSerial after SUBSCRIBE is latched;
// frames from any other serial are dropped and counted, never merged.
// Performance (§18): chunked frames, bounded buffer (oldest dropped past the
// cap, counted), no per-sample allocation beyond the frame conversion, no DSP,
// no main-thread requirement (pure Task/async, no UnityEngine).
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public enum PhoneAudioEventKind {
  Audio,    // Samples carries canonical float32 mono 16 kHz
  Stopped,  // clean end of the phone session
  Error,    // Reason carries machine-readable cause (env, never child speech)
  LinkDown  // transport lost; capture must abort as environment failure
}

public struct PhoneAudioEvent {
  public PhoneAudioEventKind Kind;
  public uint SessionSerial;
  public uint Seq;
  public float[] Samples;
  public string Reason;

  public static PhoneAudioEvent Audio(uint serial, uint seq, float[] samples) {
    return new PhoneAudioEvent { Kind = PhoneAudioEventKind.Audio,
      SessionSerial = serial, Seq = seq, Samples = samples ?? new float[0] };
  }
  public static PhoneAudioEvent Stopped(uint serial) {
    return new PhoneAudioEvent { Kind = PhoneAudioEventKind.Stopped, SessionSerial = serial };
  }
  public static PhoneAudioEvent Error(string reason) {
    return new PhoneAudioEvent { Kind = PhoneAudioEventKind.Error, Reason = reason ?? "error" };
  }
  public static PhoneAudioEvent LinkDown() {
    return new PhoneAudioEvent { Kind = PhoneAudioEventKind.LinkDown, Reason = "link_down" };
  }
}

public interface IPhoneAudioTransport {
  bool IsConnected { get; }
  Task<PhoneAudioEvent> TakeAsync(CancellationToken ct);
  void Cancel();
}

public sealed class NetworkMicrophoneCapture : ISpeechAudioCapture {
  readonly IPhoneAudioTransport _transport;
  readonly int _sampleRate;
  volatile bool _capturing;
  volatile bool _cancelRequested;

  // Dev observables (§22, read-only diagnostics, never gameplay state).
  public int LastDroppedForeignFrames { get; private set; }
  public int LastMissingSeqGaps { get; private set; }
  public int LastOverflowDroppedSamples { get; private set; }

  public NetworkMicrophoneCapture(IPhoneAudioTransport transport, int sampleRate) {
    _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    _sampleRate = sampleRate > 0 ? sampleRate : PhoneMicProtocol.CanonicalSampleRate;
  }

  public NetworkMicrophoneCapture(IPhoneAudioTransport transport)
    : this(transport, PhoneMicProtocol.CanonicalSampleRate) {
  }

  public bool IsCapturing => _capturing;
  public void Cancel() {
    _cancelRequested = true;
    try { _transport.Cancel(); } catch (Exception) { }
  }

  public async Task<CapturedSpeech> CaptureAsync(
      float maxDurationSec, float silenceTimeoutSec, CancellationToken ct) {
    _cancelRequested = false;
    LastDroppedForeignFrames = 0;
    LastMissingSeqGaps = 0;
    LastOverflowDroppedSamples = 0;
    if (maxDurationSec <= 0f) maxDurationSec = 8f;
    if (silenceTimeoutSec <= 0f) silenceTimeoutSec = 3f;
    if (!_transport.IsConnected) {
      return new CapturedSpeech { Error = SpeechFailureReasons.MicUnavailable };
    }

    _capturing = true;
    var pcm = new List<float>(16000);
    uint latchedSerial = 0;
    bool serialLatched = false;
    uint expectedSeq = 0;
    float elapsed = 0f; // audio-clock (advances with frames)
    float wall = 0f;    // wall-clock (advances every slice: silent transport still exits)
    float silenceFor = 0f;
    bool heardVoice = false;
    bool timedOut = false;
    const float sliceSec = 0.1f;
    try {
      while (elapsed < maxDurationSec && wall < maxDurationSec) {
        PhoneAudioEvent ev;
        bool hadFrame = true;
        try {
          using (var slice = CancellationTokenSource.CreateLinkedTokenSource(ct)) {
            slice.CancelAfter(TimeSpan.FromSeconds(sliceSec));
            ev = await _transport.TakeAsync(slice.Token).ConfigureAwait(false);
          }
        } catch (OperationCanceledException) {
          if (_cancelRequested || ct.IsCancellationRequested)
            return Abort(pcm, elapsed, true);
          wall += sliceSec;
          continue; // quiet slice: no frame arrived in 100 ms
        }
        if (_cancelRequested || ct.IsCancellationRequested)
          return Abort(pcm, elapsed, true);

        switch (ev.Kind) {
          case PhoneAudioEventKind.Audio:
            if (!serialLatched) {
              serialLatched = true;
              latchedSerial = ev.SessionSerial;
              expectedSeq = ev.Seq;
            } else if (ev.SessionSerial != latchedSerial) {
              LastDroppedForeignFrames++; // §12: old/new session audio never merges
              continue;
            }
            if (ev.Seq != expectedSeq) {
              LastMissingSeqGaps++; // §23 case 5: detected, counted, tolerated
              expectedSeq = ev.Seq;
            }
            expectedSeq++;
            AppendBounded(pcm, ev.Samples);
            float chunkDur = ev.Samples != null && ev.Samples.Length > 0
              ? (float)ev.Samples.Length / _sampleRate : sliceSec;
            elapsed += chunkDur;
            float energy = VoiceActivity.MeanAbsolute(ev.Samples);
            if (energy > 0.004f) { heardVoice = true; silenceFor = 0f; }
            else silenceFor += chunkDur;
            if (heardVoice && silenceFor >= silenceTimeoutSec) break;
            if (elapsed >= maxDurationSec) timedOut = true;
            break;
          case PhoneAudioEventKind.Stopped:
            if (!serialLatched || ev.SessionSerial == latchedSerial) goto DONE;
            LastDroppedForeignFrames++;
            continue;
          case PhoneAudioEventKind.Error:
            return Fail(string.IsNullOrEmpty(ev.Reason)
              ? SpeechFailureReasons.NetworkError : ev.Reason);
          case PhoneAudioEventKind.LinkDown:
            return Fail(SpeechFailureReasons.NetworkError); // §11: env, never WrongWord
          default:
            return Fail(SpeechFailureReasons.ProviderError);
        }
        if (timedOut) break;
      }
    DONE:
      timedOut = elapsed >= maxDurationSec || wall >= maxDurationSec;
      return Finish(pcm, elapsed, false, timedOut);
    } catch (OperationCanceledException) {
      return Abort(pcm, elapsed, true);
    } catch (Exception) {
      return Fail(SpeechFailureReasons.DeviceError);
    } finally {
      _capturing = false;
    }
  }

  void AppendBounded(List<float> pcm, float[] samples) {
    if (samples == null || samples.Length == 0) return;
    int cap = PhoneMicProtocol.MaxBufferedSamples;
    if (pcm.Count + samples.Length > cap) {
      int drop = Math.Min(pcm.Count, pcm.Count + samples.Length - cap);
      pcm.RemoveRange(0, drop);
      LastOverflowDroppedSamples += drop; // §18: drop/recover is explicit
    }
    pcm.AddRange(samples);
  }

  CapturedSpeech Finish(List<float> pcm, float elapsed, bool cancelled, bool timedOut) {
    float[] data = pcm.ToArray();
    float mean = VoiceActivity.MeanAbsolute(data);
    float peak = 0f;
    for (int i = 0; i < data.Length; i++) {
      float a = Math.Abs(data[i]);
      if (a > peak) peak = a;
    }
    const int window = 160; // 10 ms @16kHz (same VAD rhythm as local capture)
    int voicedWindows = 0, totalWindows = 0;
    for (int i = window - 1; i < data.Length; i += window) {
      totalWindows++;
      float wsum = 0f;
      for (int k = i - window + 1; k <= i; k++) wsum += Math.Abs(data[k]);
      if (wsum / window > 0.004f) voicedWindows++;
    }
    float voiced = totalWindows > 0
      ? (float)voicedWindows / totalWindows * ((float)data.Length / _sampleRate) : 0f;
    return new CapturedSpeech {
      Samples = data, SampleRate = _sampleRate, Channels = 1,
      DurationSec = data.Length > 0 ? (float)data.Length / _sampleRate : 0f,
      MeanEnergy = mean, PeakEnergy = peak, VoicedSec = voiced,
      TimedOut = timedOut, Cancelled = cancelled, Error = string.Empty
    };
  }

  CapturedSpeech Abort(List<float> pcm, float elapsed, bool cancelled) {
    CapturedSpeech s = Finish(pcm, elapsed, cancelled, false);
    s.Cancelled = true;
    return s;
  }

  CapturedSpeech Fail(string reason) {
    return new CapturedSpeech { Error = reason };
  }
}

// Production transport: TCP loopback to tools/phone_mic_gateway.py (§6).
// System.Net.Sockets only (no extra packages); the gateway speaks the
// PhoneMicProtocol envelope. TLS is unnecessary here (same-machine loopback);
// it terminates at the gateway facing the phone (LAN).
public sealed class TcpPhoneAudioTransport : IPhoneAudioTransport, IDisposable {
  readonly string _host;
  readonly int _port;
  TcpClient _client;
  NetworkStream _stream;
  volatile bool _cancelRequested;
  bool _disposed;

  public TcpPhoneAudioTransport(string host, int port) {
    _host = string.IsNullOrEmpty(host) ? PhoneMicProtocol.LoopbackHost : host;
    _port = port > 0 ? port : PhoneMicProtocol.DefaultBridgePort;
  }

  public bool IsConnected {
    get {
      try {
        if (_client != null && _client.Connected) return true;
        TryConnectSync(); // lazy: first touch dials + subscribes (never throws)
        return _client != null && _client.Connected;
      } catch (Exception) { return false; }
    }
  }

  public void Cancel() { _cancelRequested = true; }

  void TryConnectSync() {
    Close();
    var c = new TcpClient();
    try {
      var ar = c.BeginConnect(_host, _port, null, null);
      if (!ar.AsyncWaitHandle.WaitOne(1500)) {
        try { c.Close(); } catch (Exception) { }
        return;
      }
      try { c.EndConnect(ar); } catch (Exception) {
        try { c.Close(); } catch (Exception) { }
        return;
      }
      _client = c;
      _stream = c.GetStream();
      try { _stream.WriteTimeout = 1500; } catch (Exception) { }
      byte[] sub = PhoneMicProtocol.EncodeBridgeFrame(
        PhoneMicProtocol.KindSubscribe, 0, 0, new byte[0]);
      _stream.Write(sub, 0, sub.Length);
    } catch (Exception) {
      try { c.Close(); } catch (Exception) { }
    }
  }

  public async Task<PhoneAudioEvent> TakeAsync(CancellationToken ct) {
    if (_cancelRequested || ct.IsCancellationRequested)
      throw new OperationCanceledException();
    await EnsureConnectedAsync(ct).ConfigureAwait(false);
    byte[] lenBuf = await ReadExactAsync(4, ct).ConfigureAwait(false);
    if (lenBuf == null) return PhoneAudioEvent.LinkDown();
    int bodyLen = (lenBuf[0] << 24) | (lenBuf[1] << 16) | (lenBuf[2] << 8) | lenBuf[3];
    if (bodyLen < PhoneMicProtocol.HeaderBytes || bodyLen > PhoneMicProtocol.MaxFrameBytes - 4)
      return PhoneAudioEvent.Error("protocol_error");
    byte[] body = await ReadExactAsync(bodyLen, ct).ConfigureAwait(false);
    if (body == null) return PhoneAudioEvent.LinkDown();
    var raw = new byte[4 + bodyLen];
    Buffer.BlockCopy(lenBuf, 0, raw, 0, 4);
    Buffer.BlockCopy(body, 0, raw, 4, bodyLen);
    PhoneMicProtocol.BridgeFrame frame;
    string reason;
    if (!PhoneMicProtocol.TryDecodeBridgeFrame(raw, 0, raw.Length, out frame, out reason))
      return PhoneAudioEvent.Error("protocol_error");
    switch (frame.Kind) {
      case PhoneMicProtocol.KindAudio:
        return PhoneAudioEvent.Audio(frame.SessionSerial, frame.Seq,
          PhoneMicProtocol.Pcm16ToFloat32(frame.Payload, 0,
            frame.Payload != null ? frame.Payload.Length : 0));
      case PhoneMicProtocol.KindStop:
        return PhoneAudioEvent.Stopped(frame.SessionSerial);
      case PhoneMicProtocol.KindError:
        return PhoneAudioEvent.Error(PhoneMicProtocol.Utf8(frame.Payload));
      case PhoneMicProtocol.KindHello:
        return await TakeAsync(ct).ConfigureAwait(false); // greeting, keep waiting
      default:
        return PhoneAudioEvent.Error("protocol_error");
    }
  }

  async Task EnsureConnectedAsync(CancellationToken ct) {
    if (IsConnected) return;
    Close();
    _client = new TcpClient();
    using (ct.Register(() => { try { _client.Close(); } catch (Exception) { } })) {
      await _client.ConnectAsync(_host, _port).ConfigureAwait(false);
    }
    ct.ThrowIfCancellationRequested();
    _stream = _client.GetStream();
    byte[] sub = PhoneMicProtocol.EncodeBridgeFrame(
      PhoneMicProtocol.KindSubscribe, 0, 0, new byte[0]);
    await _stream.WriteAsync(sub, 0, sub.Length, ct).ConfigureAwait(false);
  }

  async Task<byte[]> ReadExactAsync(int count, CancellationToken ct) {
    var buf = new byte[count];
    int got = 0;
    while (got < count) {
      if (_cancelRequested || ct.IsCancellationRequested)
        throw new OperationCanceledException();
      int n = 0;
      try {
        n = await _stream.ReadAsync(buf, got, count - got, ct).ConfigureAwait(false);
      } catch (OperationCanceledException) { throw; } catch (Exception) { return null; }
      if (n <= 0) return null; // orderly shutdown = link down (env, §11)
      got += n;
    }
    return buf;
  }

  void Close() {
    try { if (_stream != null) _stream.Dispose(); } catch (Exception) { }
    try { if (_client != null) _client.Close(); } catch (Exception) { }
    _stream = null;
    _client = null;
  }

  public void Dispose() {
    if (_disposed) return;
    _disposed = true;
    Close();
  }
}
