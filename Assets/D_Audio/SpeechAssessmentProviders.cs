// D_Audio/SpeechAssessmentProviders.cs — Phase 2.1 provider layer (Agent D).
// Every backend implements ISpeechAssessmentProvider and returns the NORMALIZED
// SpeechRecognitionResult (§83). Gameplay never sees SDK types.
//
// PROVIDER SELECTION (§22/§23, researched 2026-09-14):
// - Azure AI Speech (PronunciationAssessmentConfig, GradingSystem.HundredMark,
//   Granularity.Phoneme, miscue + NBest phonemes, IPA/SAPI) is the ONLY
//   mainstream backend returning phoneme-level pronunciation evidence on
//   Windows/Unity/C#. Unity-compatible (known quirk: phoneme NAMES require the
//   recognizer locale pinned to en-US). Cloud: needs internet, subscription key,
//   per-call cost; child audio leaves the device (privacy §60).
// - Offline engines (Vosk/Whisper-Unity/Wav2Vec2/TEN-VAD) return TRANSCRIPT ONLY:
//   no pronunciation/phoneme scores, plus model size + CPU cost. Privacy-optimal
//   but CANNOT meet the §8 core requirement, so they are fallback-grade here.
// - CHOSEN: provider-agnostic seam + Azure as the pronunciation backend (wiring
//   point below, SDK/key NOT bundled) + LocalSpeechProvider (on-device VAD +
//   energy evidence, transcript NOT AVAILABLE by design) for offline/capture
//   validation. FallbackSpeechProvider (legacy intent mode) untouched.
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

// On-device VAD + energy evidence. HONEST LIMIT (§87): no STT engine ships in
// 2.1, so Transcript is always "" and pronunciation is NOT AVAILABLE. Speech
// presence/energy/duration evidence is real (from the captured segment).
public sealed class LocalSpeechProvider : ISpeechAssessmentProvider {
  public string ProviderId => "local";
  public bool ProvidesTranscript => false;
  public bool ProvidesPhonemeEvidence => false;
  public bool RequiresNetwork => false;

  readonly float _speechEnergyFloor;

  public LocalSpeechProvider(float speechEnergyFloor) {
    _speechEnergyFloor = speechEnergyFloor > 0f ? speechEnergyFloor : 0.001f;
  }

  public LocalSpeechProvider() : this(0.001f) { }

  public Task<SpeechRecognitionResult> RecognizeAsync(CapturedSpeech audio, WordId target, CancellationToken ct) {
    var sw = Stopwatch.StartNew();
    var result = new SpeechRecognitionResult();
    result.ProviderId = "local";
    result.Pronunciation = PronunciationEvidence.None();
    result.ErrorReason = string.Empty;
    result.IsError = false;
    if (!string.IsNullOrEmpty(audio.Error)) {
      result.IsError = true;
      result.ErrorReason = audio.Error;
      result.HasSpeech = false;
      result.LatencyMs = sw.ElapsedMilliseconds;
      return Task.FromResult(result);
    }
    if (audio.Cancelled) {
      result.IsError = true;
      result.ErrorReason = SpeechFailureReasons.Cancelled;
      result.HasSpeech = false;
      result.LatencyMs = sw.ElapsedMilliseconds;
      return Task.FromResult(result);
    }
    float energy = audio.Samples != null ? VoiceActivity.MeanAbsolute(audio.Samples) : audio.MeanEnergy;
    result.HasSpeech = VoiceActivity.HasSpeech(energy, _speechEnergyFloor) && audio.DurationSec > 0f;
    result.MeanEnergy = energy;
    result.AudioDurationSec = audio.DurationSec;
    result.SpeechDurationSec = audio.VoicedSec;
    result.Transcript = string.Empty; // NO local STT engine: honestly empty, never faked.
    result.RecognitionConfidence = 0f;
    if (audio.TimedOut && !result.HasSpeech) result.ErrorReason = string.Empty; // silence timeout -> NO_SPEECH, not error
    result.LatencyMs = sw.ElapsedMilliseconds;
    return Task.FromResult(result);
  }
}

// Azure pronunciation backend — WIRING POINT (not configured in 2.1).
// When the Speech SDK + key land, this class owns ALL Azure types:
//   SpeechConfig.FromSubscription(key, region) with SpeechRecognitionLanguage
//   pinned to "en-US" (phoneme names require it), AudioConfig.FromWavFileInput
//   or push-stream from CapturedSpeech (16kHz mono PCM), then
//   new PronunciationAssessmentConfig(referenceText: target, HundredMark,
//   Granularity.Phoneme, enableMiscue: true) + EnableProsodyAssessment() +
//   NBestPhonemeCount, RecognizeOnceAsync, PronunciationAssessmentResult.
//   FromResult -> Accuracy/Fluency/Completeness/Prosody + Words[].AccuracyScore
//   + ErrorType + Phonemes[] (HasPhonemeData=true, Source="azure").
//   Lexical substitution guard: compare result.Text vs target separately —
//   Azure scores acoustic similarity, NOT word correctness (known model limit).
// UNTIL THEN: returns IsError/provider_error "not-configured" — never a fake
// score, never a fake transcript (§69/§87). Key source: environment variable
// LWE_AZURE_SPEECH_KEY (never committed, never in Resources/StreamingAssets).
public sealed class AzureSpeechAssessmentProvider : ISpeechAssessmentProvider {
  public const string NotConfiguredReason = "not-configured";
  public string ProviderId => "azure";
  public bool ProvidesTranscript => true;
  public bool ProvidesPhonemeEvidence => true;
  public bool RequiresNetwork => true;

  readonly Func<string> _keyReader;

  public AzureSpeechAssessmentProvider() : this(ReadKeyFromEnvironment) { }

  // Test seam: inject key availability.
  public AzureSpeechAssessmentProvider(Func<string> keyReader) {
    _keyReader = keyReader ?? ReadKeyFromEnvironment;
  }

  public bool IsConfigured() {
    try {
      return !string.IsNullOrEmpty(_keyReader());
    } catch (Exception) {
      return false;
    }
  }

  public Task<SpeechRecognitionResult> RecognizeAsync(CapturedSpeech audio, WordId target, CancellationToken ct) {
    var sw = Stopwatch.StartNew();
    var result = new SpeechRecognitionResult();
    result.ProviderId = "azure";
    result.Pronunciation = PronunciationEvidence.None();
    // No SDK bundled in 2.1 and no key present: honest provider error.
    result.IsError = true;
    result.ErrorReason = IsConfigured()
      ? SpeechFailureReasons.ProviderError // key present but SDK path not implemented yet
      : NotConfiguredReason;
    result.HasSpeech = false;
    result.LatencyMs = sw.ElapsedMilliseconds;
    return Task.FromResult(result);
  }

  public static string ReadKeyFromEnvironment() {
    try {
      return Environment.GetEnvironmentVariable("LWE_AZURE_SPEECH_KEY");
    } catch (Exception) {
      return null;
    }
  }
}

// Scripted test double (§50): PASS / PARTIAL / WRONG / NO_SPEECH / ERROR /
// TIMEOUT without hardware or network. MOCK PASS != REAL SPEECH PASS (§51).
public sealed class FakeSpeechProvider : ISpeechAssessmentProvider {
  public enum Outcome { Pass, Partial, WrongWord, NoSpeech, Unclear, Error, Timeout, TranscriptOnly }

  public string ProviderId => "fake";
  public bool ProvidesTranscript => true;
  public bool ProvidesPhonemeEvidence => _withPhonemes;
  public bool RequiresNetwork => false;

  readonly Outcome _outcome;
  readonly bool _withPhonemes;
  readonly float _confidence;
  public int Calls { get; private set; }

  public FakeSpeechProvider(Outcome outcome, bool withPhonemes, float confidence) {
    _outcome = outcome;
    _withPhonemes = withPhonemes;
    _confidence = confidence;
  }

  public FakeSpeechProvider(Outcome outcome) : this(outcome, false, 0.9f) { }

  public Task<SpeechRecognitionResult> RecognizeAsync(CapturedSpeech audio, WordId target, CancellationToken ct) {
    Calls++;
    var r = new SpeechRecognitionResult();
    r.ProviderId = "fake";
    r.LatencyMs = 5;
    string t = target.Value;
    switch (_outcome) {
      case Outcome.Pass:
        r.HasSpeech = true; r.Transcript = t; r.RecognitionConfidence = _confidence;
        r.AudioDurationSec = 0.8f; r.SpeechDurationSec = 0.6f; r.MeanEnergy = 0.05f;
        break;
      case Outcome.Partial:
        r.HasSpeech = true; r.Transcript = t.Length > 2 ? t.Substring(0, 2) : t;
        r.RecognitionConfidence = 0.5f;
        r.AudioDurationSec = 0.5f; r.SpeechDurationSec = 0.3f; r.MeanEnergy = 0.03f;
        break;
      case Outcome.WrongWord:
        r.HasSpeech = true; r.Transcript = t == "ball" ? "apple" : "ball";
        r.RecognitionConfidence = 0.88f;
        r.AudioDurationSec = 0.8f; r.SpeechDurationSec = 0.6f; r.MeanEnergy = 0.05f;
        break;
      case Outcome.NoSpeech:
        r.HasSpeech = false; r.Transcript = string.Empty; r.RecognitionConfidence = 0f;
        r.AudioDurationSec = 2f; r.SpeechDurationSec = 0f; r.MeanEnergy = 0f;
        break;
      case Outcome.Unclear:
        r.HasSpeech = true; r.Transcript = "uh"; r.RecognitionConfidence = 0.12f;
        r.AudioDurationSec = 0.6f; r.SpeechDurationSec = 0.3f; r.MeanEnergy = 0.02f;
        break;
      case Outcome.Error:
        r.IsError = true; r.ErrorReason = SpeechFailureReasons.ProviderError;
        break;
      case Outcome.Timeout:
        r.IsError = true; r.ErrorReason = SpeechFailureReasons.Timeout;
        break;
      case Outcome.TranscriptOnly:
        r.HasSpeech = true; r.Transcript = "the " + t + " please"; r.RecognitionConfidence = 0.82f;
        r.AudioDurationSec = 1.4f; r.SpeechDurationSec = 1.0f; r.MeanEnergy = 0.05f;
        break;
      default:
        r.IsError = true; r.ErrorReason = SpeechFailureReasons.ProviderError;
        break;
    }
    if (_withPhonemes && !r.IsError && r.HasSpeech) {
      r.Pronunciation = new PronunciationEvidence {
        HasPhonemeData = true, Source = "azure",
        AccuracyScore = _outcome == Outcome.Pass ? 0.92f : 0.55f,
        FluencyScore = 0.9f, CompletenessScore = 1f, WordAccuracy = 0.9f,
        WordErrorType = "None", PhonemeCount = 3
      };
    } else {
      r.Pronunciation = PronunciationEvidence.None();
    }
    return Task.FromResult(r);
  }
}
