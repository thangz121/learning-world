// D_Audio/MaxSpecAdaptiveSpeechProvider.cs — Full Runtime Integration of Phase 1.2 -> 2.2 -> 3.1 -> 4.1 -> 5.0.
// Wraps ChildAudioFrontend, EnglishAcousticDspEngine, CloudSpeechResilienceEngine,
// CrossValidationPedagogicalEngine, and MultiTurnAdaptiveSessionEngine into an ISpeechAssessmentProvider.
// HONEST SCOPE: offline evidence chain (frontend VAD -> DSP -> resilience -> fusion -> session).
// There is NO ASR text in this provider (Azure not wired; local fallback placeholder returns an
// EMPTY transcript), so ProvidesTranscript=false and Transcript/Confidence stay empty/0.
// Runtime wiring: GameInstaller opt-in flag "-speak-maxspec" (inert by default).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public sealed class MaxSpecAdaptiveSpeechProvider : ISpeechAssessmentProvider {
  public string ProviderId => "max-spec-adaptive";
  public bool ProvidesTranscript => false; // no ASR text offline (see header)
  public bool ProvidesPhonemeEvidence => true;
  public bool RequiresNetwork => false; // fully offline evidence; transcript would require Azure (not bundled)

  readonly ITargetPronunciationProvider _targets;
  readonly MultiTurnAdaptiveSessionEngine _sessionEngine;
  readonly float _speechEnergyFloor;

  // Track attempt history for target words in current session
  readonly Dictionary<string, int> _wordAttempts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
  readonly Dictionary<string, float> _prevScores = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
  readonly Dictionary<string, string> _prevErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

  public MaxSpecAdaptiveSpeechProvider(
      ITargetPronunciationProvider targets,
      MultiTurnAdaptiveSessionEngine sessionEngine = null,
      float speechEnergyFloor = 0.001f) {

    _targets = targets;
    _sessionEngine = sessionEngine ?? new MultiTurnAdaptiveSessionEngine();
    _speechEnergyFloor = speechEnergyFloor > 0f ? speechEnergyFloor : 0.001f;
  }

  public MultiTurnAdaptiveSessionEngine SessionEngine => _sessionEngine;

  public Task<SpeechRecognitionResult> RecognizeAsync(CapturedSpeech audio, WordId target, CancellationToken ct) {
    var sw = Stopwatch.StartNew();
    var result = new SpeechRecognitionResult {
      ProviderId = ProviderId,
      Pronunciation = PronunciationEvidence.None(),
      ErrorReason = string.Empty,
      IsError = false
    };

    if (ct.IsCancellationRequested) {
      result.IsError = true;
      result.ErrorReason = SpeechFailureReasons.Cancelled;
      result.HasSpeech = false;
      result.LatencyMs = sw.ElapsedMilliseconds;
      return Task.FromResult(result);
    }

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
    result.Transcript = string.Empty;
    result.RecognitionConfidence = 0f;

    if (!result.HasSpeech || audio.Samples == null || audio.Samples.Length == 0) {
      result.LatencyMs = sw.ElapsedMilliseconds;
      return Task.FromResult(result);
    }

    string targetWord = target.Value ?? string.Empty;
    float[] mono = audio.Channels > 1 ? ToMono(audio.Samples, audio.Channels) : audio.Samples;

    // --- PHASE 1.2: Child Audio Frontend Telemetry (real on-device VAD analysis) ---
    var phase1Meta = ChildAudioFrontend.AnalyzeChildVAD(mono, audio.SampleRate);

    // --- PHASE 2.2: Local Acoustic DSP Engine ---
    TargetPronunciation pron = null;
    if (_targets != null) {
      _targets.TryGet(target, out pron);
    }

    var phase2Payload = EnglishAcousticDspEngine.Analyze(mono, audio.SampleRate, pron);

    // --- PHASE 3.1: Cloud AI Speech & Resilience Engine ---
    string utteranceId = "utt-" + Guid.NewGuid().ToString("N").Substring(0, 8);
    var phase3Payload = CloudSpeechResilienceEngine.Process(
        naturalPcmSamples: mono,
        sampleRate: audio.SampleRate,
        targetWord: targetWord,
        azureResult: null, // Autonomous / Fallback mode when online raw azure is absent
        utteranceId: utteranceId);

    // Transcript & confidence (honest): no ASR text exists in this provider — azureResult is
    // never supplied and the local fallback placeholder returns an EMPTY transcript. Echoing
    // targetWord as "transcript" or inventing 0.85/0.2 confidence would be a fake readout.

    // --- PHASE 4.1: Cross-Validation & Pedagogical Engine ---
    var phase4Payload = CrossValidationPedagogicalEngine.Fuse(
        targetWord: targetWord,
        phase1: phase1Meta,
        phase2: phase2Payload,
        phase3: phase3Payload);

    // --- PHASE 5.0: Multi-Turn Session Orchestration ---
    int attemptIdx;
    _wordAttempts.TryGetValue(targetWord, out attemptIdx);
    attemptIdx++;
    _wordAttempts[targetWord] = attemptIdx;

    float prevScore;
    _prevScores.TryGetValue(targetWord, out prevScore);
    _prevScores[targetWord] = phase4Payload.scores_summary.final_fusion_score;

    string prevErr;
    _prevErrors.TryGetValue(targetWord, out prevErr);
    _prevErrors[targetWord] = phase4Payload.pedagogical_feedback_for_4yo.primary_error_focus;

    // No TTS reference chunks are pushed here (playback is owned elsewhere), so the NCC
    // echo-guard has nothing to correlate against and honestly reports 0 / false.
    var echoGuard = _sessionEngine.ProcessBargeInAndEcho(mono, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), true);

    var phase5Payload = _sessionEngine.EvaluateTurn(
        utteranceId: phase4Payload.utterance_id,
        targetWord: targetWord,
        attemptIndex: attemptIdx,
        phase4Payload: phase4Payload,
        prevAttemptScore: prevScore,
        prevAttemptPrimaryError: prevErr,
        reactionLatencyMs: float.NaN,   // unknown here — engine skips NaN (never a fake latency)
        currentPeakRmsDb: PeakDb(mono), // real peak from the captured samples
        pitchSpanHz: float.NaN,         // not exposed by the DSP payload — no invented value
        echoGuard: echoGuard);

    // Assemble rich PronunciationEvidence
    var pe = PronunciationEvidence.None();
    pe.HasPhonemeData = true;
    pe.Source = "max-spec-adaptive";
    pe.AccuracyScore = phase4Payload.scores_summary.final_fusion_score / 100f;
    pe.FluencyScore = 0f;      // not measured offline (0 = unavailable; FallbackSpeechProvider convention)
    pe.CompletenessScore = 0f; // not measured offline
    pe.WordAccuracy = pe.AccuracyScore;
    pe.WordErrorType = phase4Payload.normative_decision == "Retry_Blocked" ? "Mispronunciation" : "None";

    // Acoustic block
    pe.HasAcousticData = true;
    pe.AcousticMatch = phase2Payload.overall_acoustic_score / 100f;
    pe.AcousticOnset = phase2Payload.english_phonetic_metrics.onset_and_clusters.score / 100f;
    pe.AcousticCoda = phase2Payload.english_phonetic_metrics.coda_and_clusters.score / 100f;
    pe.AcousticMissingEnding = phase2Payload.english_phonetic_metrics.coda_and_clusters.is_dropped;
    pe.AcousticWeakEnding = !phase2Payload.english_phonetic_metrics.coda_and_clusters.release_burst_detected;

    // Feedback
    pe.FeedbackVi = phase4Payload.pedagogical_feedback_for_4yo.voice_coach_prompt_vi;
    pe.Tip = phase4Payload.pedagogical_feedback_for_4yo.phonetic_sound_effect_cue;
    pe.ErrorPhoneme = phase4Payload.pedagogical_feedback_for_4yo.primary_error_focus;

    // Attach deep Phase 2.2, 4.1 & 5.0 payloads
    pe.HasPhoneticPayload = true;
    pe.PhoneticPayload = phase2Payload;
    pe.HasFusionPayload = true;
    pe.FusionPayload = phase4Payload;
    pe.HasSessionPayload = true;
    pe.SessionPayload = phase5Payload;

    result.Pronunciation = pe;
    result.LatencyMs = sw.ElapsedMilliseconds;
    return Task.FromResult(result);
  }

  static float PeakDb(float[] samples) {
    if (samples == null || samples.Length == 0) return float.NaN;
    float peak = 0f;
    for (int i = 0; i < samples.Length; i++) {
      float a = samples[i] < 0f ? -samples[i] : samples[i];
      if (a > peak) peak = a;
    }
    return peak <= 1e-6f ? -120f : 20f * (float)Math.Log10(peak);
  }

  static float[] ToMono(float[] src, int channels) {
    if (channels <= 1 || src == null) return src;
    int monoLen = src.Length / channels;
    float[] mono = new float[monoLen];
    for (int i = 0; i < monoLen; i++) {
      float sum = 0f;
      for (int c = 0; c < channels; c++) {
        sum += src[i * channels + c];
      }
      mono[i] = sum / channels;
    }
    return mono;
  }
}
