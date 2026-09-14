// D_Audio/SpeechRecognizer.cs — Phase 2.1 gameplay-facing speech service (§10).
// The ONLY speech entry point for quest/presenters: they never touch
// Microphone.* or any provider SDK. Responsibilities: capability gating,
// capture -> provider -> policy pipeline, attempt ids (§49), timeout, cancel,
// stale-result rejection (§48), typed events, developer logging (§52).
// Layering: pure seams (interfaces) keep this class unit-testable with fakes;
// the single Unity dependency is Debug.Log for the developer line.
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class SpeechRecognizer : ISpeechRecognizer {
  readonly IMicrophoneDevice _devices;
  readonly ISpeechAudioCapture _capture;
  ISpeechAssessmentProvider _provider;
  readonly SpeakingPassPolicy _policy;
  readonly IGameEventBus _bus; // optional (null in tests)
  readonly float _captureMaxSec;
  readonly float _captureSilenceSec;
  readonly float _providerTimeoutSec;

  int _generation;
  int _attemptSeq;
  CancellationTokenSource _currentCts;
  readonly object _gate = new object();

  public event Action<SpeechCapability> CapabilityChanged;

  public SpeechRecognizer(
    IMicrophoneDevice devices,
    ISpeechAudioCapture capture,
    ISpeechAssessmentProvider provider,
    SpeakingPassPolicy policy,
    IGameEventBus bus,
    float captureMaxSec,
    float captureSilenceSec,
    float providerTimeoutSec) {
    _devices = devices ?? throw new ArgumentNullException(nameof(devices));
    _capture = capture ?? throw new ArgumentNullException(nameof(capture));
    _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    _policy = policy ?? SpeakingPassPolicy.Default();
    _bus = bus;
    _captureMaxSec = captureMaxSec > 0f ? captureMaxSec : 8f;
    _captureSilenceSec = captureSilenceSec > 0f ? captureSilenceSec : 3f;
    _providerTimeoutSec = providerTimeoutSec > 0f ? providerTimeoutSec : 15f;
    _devices.StatusChanged += OnDeviceStatus;
  }

  public SpeechRecognizer(IMicrophoneDevice devices, ISpeechAudioCapture capture, ISpeechAssessmentProvider provider)
    : this(devices, capture, provider, SpeakingPassPolicy.Default(), null, 8f, 3f, 15f) {
  }

  public SpeechCapability Capability => _devices.Capability;

  // Online<->offline swap inside the service (router pattern, v6.3): consumers
  // keep THIS instance forever; the backend changes underneath (§9).
  public void SwitchProvider(ISpeechAssessmentProvider next) {
    if (next == null) return;
    lock (_gate) { _provider = next; }
  }

  public void RefreshCapability() {
    try { _devices.Refresh(); } catch (Exception) { }
  }

  public void Cancel() {
    CancellationTokenSource cts = null;
    lock (_gate) {
      _generation++; // stale continuations from the cancelled attempt die here
      cts = _currentCts;
      _currentCts = null;
    }
    try { _capture.Cancel(); } catch (Exception) { }
    try { if (cts != null) cts.Cancel(); } catch (Exception) { }
  }

  public async Task<SpeakingAssessment> StartAttemptAsync(WordId target, CancellationToken ct) {
    int gen;
    CancellationTokenSource linked = null;
    lock (_gate) {
      _generation++;
      gen = _generation;
      _attemptSeq++;
      linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
      if (_currentCts != null) {
        try { _currentCts.Cancel(); } catch (Exception) { }
      }
      _currentCts = linked;
    }
    string attemptId = "spk-" + _attemptSeq;
    CancellationToken token = linked.Token;
    PublishAttempted(attemptId, target);

    // --- No-microphone short-circuit (§13): NEVER a quest failure, NEVER a
    // retry loop here — the exercise policy skips/defers on this reason. ---
    if (!_devices.Capability.IsAvailable()) {
      var unavailable = new SpeechRecognitionResult();
      unavailable.ProviderId = "none";
      unavailable.IsError = true;
      unavailable.HasSpeech = false;
      unavailable.ErrorReason = _devices.Status == MicStatus.PermissionDenied
        ? SpeechFailureReasons.PermissionDenied : SpeechFailureReasons.MicUnavailable;
      unavailable.Pronunciation = PronunciationEvidence.None();
      SpeakingAssessment noMic = _policy.Decide(unavailable, target, attemptId);
      noMic.IsEnvironmentError = true;
      LogDeveloper(noMic, unavailable);
      PublishAssessed(gen, attemptId, target, noMic, null);
      lock (_gate) { if (_currentCts == linked) _currentCts = null; }
      return noMic;
    }

    // --- Capture (child -> system, §80). No permanent storage. ---
    CapturedSpeech segment;
    try {
      segment = await _capture.CaptureAsync(_captureMaxSec, _captureSilenceSec, token).ConfigureAwait(false);
    } catch (OperationCanceledException) {
      lock (_gate) { if (_currentCts == linked) _currentCts = null; }
      throw;
    }
    if (token.IsCancellationRequested || IsStale(gen)) {
      lock (_gate) { if (_currentCts == linked) _currentCts = null; }
      throw new OperationCanceledException();
    }
    if (segment.Cancelled) {
      lock (_gate) { if (_currentCts == linked) _currentCts = null; }
      throw new OperationCanceledException();
    }

    // --- Recognition with timeout (§24: timeout is environment, not Wrong). ---
    ISpeechAssessmentProvider provider;
    lock (_gate) { provider = _provider; }
    SpeechRecognitionResult result;
    try {
      result = await WithTimeout(
        provider.RecognizeAsync(segment, target, token),
        _providerTimeoutSec, token).ConfigureAwait(false);
    } catch (TimeoutException) {
      result = new SpeechRecognitionResult();
      result.ProviderId = provider.ProviderId;
      result.IsError = true;
      result.ErrorReason = SpeechFailureReasons.Timeout;
      result.Pronunciation = PronunciationEvidence.None();
    } catch (OperationCanceledException) {
      lock (_gate) { if (_currentCts == linked) _currentCts = null; }
      throw;
    } catch (Exception) {
      result = new SpeechRecognitionResult();
      result.ProviderId = provider.ProviderId;
      result.IsError = true;
      result.ErrorReason = SpeechFailureReasons.ProviderError;
      result.Pronunciation = PronunciationEvidence.None();
    }

    // Capture-layer device failure surfaces here (not as child speech).
    if (!string.IsNullOrEmpty(segment.Error) && !result.IsError) {
      result.IsError = true;
      result.ErrorReason = segment.Error;
    }
    // Stale guard (§48): Attempt A resolves after B started -> dropped silently.
    if (IsStale(gen)) {
      try {
        if (_bus != null) { /* deliberately no publish: stale must not affect B */ }
      } catch (Exception) { }
      lock (_gate) { if (_currentCts == linked) _currentCts = null; }
      var stale = _policy.Decide(result, target, attemptId);
      stale.FailureReason = SpeechFailureReasons.Stale;
      return stale;
    }

    SpeakingAssessment assessment = _policy.Decide(result, target, attemptId);
    LogDeveloper(assessment, result);
    PublishAssessed(gen, attemptId, target, assessment, result.HasTranscript() ? result : (SpeechRecognitionResult?)null);
    lock (_gate) { if (_currentCts == linked) _currentCts = null; }
    return assessment;
  }

  bool IsStale(int gen) {
    lock (_gate) { return gen != _generation; }
  }

  void OnDeviceStatus(MicStatus status) {
    SpeechCapability cap = _devices.Capability;
    try { CapabilityChanged?.Invoke(cap); } catch (Exception) { }
    try {
      if (_bus != null)
        _bus.Publish(new SpeechCapabilityChangedEvent(status, cap.DeviceName));
    } catch (Exception) { }
  }

  void PublishAttempted(string attemptId, WordId target) {
    try {
      if (_bus != null) _bus.Publish(new SpeechAttemptedEvent(attemptId, target, DateTime.UtcNow));
    } catch (Exception) { }
  }

  void PublishAssessed(int gen, string attemptId, WordId target, SpeakingAssessment assessment, SpeechRecognitionResult? recognized) {
    if (IsStale(gen)) return; // never let a stale result reach gameplay
    try {
      if (_bus == null) return;
      if (recognized.HasValue && !string.IsNullOrWhiteSpace(recognized.Value.Transcript)) {
        _bus.Publish(new SpeechRecognizedEvent(attemptId, target,
          recognized.Value.Transcript, recognized.Value.RecognitionConfidence));
      }
      _bus.Publish(new SpeechAssessedEvent(attemptId, target, assessment));
    } catch (Exception) { }
  }

  void LogDeveloper(SpeakingAssessment a, SpeechRecognitionResult r) {
    try {
      string lex = float.IsNaN(a.LexicalMatchScore) ? "n/a" : a.LexicalMatchScore.ToString("0.00");
      string pron = a.HasPronunciationEvidence ? a.PronunciationScore.ToString("0.00") : "NOT_AVAILABLE";
      string heard = string.IsNullOrWhiteSpace(r.Transcript) ? "<empty>" : r.Transcript.Trim();
      Debug.Log("[SpeechRecognizer] attempt=" + a.AttemptId
        + " target=" + a.Target.Value
        + " device=" + _devices.SelectedDevice
        + " capture=" + r.AudioDurationSec.ToString("0.0") + "s"
        + " speech=" + (r.HasSpeech ? "Y" : "N")
        + " heard=\"" + heard + "\""
        + " conf=" + r.RecognitionConfidence.ToString("0.00")
        + " lexical=" + lex
        + " pron=" + pron
        + " intellig=" + a.IntelligibilityScore.ToString("0.00") + "(proxy)"
        + " overall=" + a.OverallScore.ToString("0.00")
        + " decision=" + a.Decision
        + " reason=" + a.FailureReason
        + " env_err=" + (a.IsEnvironmentError ? "Y" : "N")
        + " provider=" + a.ProviderId + "/" + a.ProviderLatencyMs + "ms");
    } catch (Exception) { }
  }

  static async Task<SpeechRecognitionResult> WithTimeout(Task<SpeechRecognitionResult> task, float timeoutSec, CancellationToken ct) {
    Task delay = Task.Delay(TimeSpan.FromSeconds(timeoutSec), ct);
    Task first = await Task.WhenAny(task, delay).ConfigureAwait(false);
    if (first == task) return await task.ConfigureAwait(false);
    if (ct.IsCancellationRequested) throw new OperationCanceledException();
    throw new TimeoutException();
  }
}
