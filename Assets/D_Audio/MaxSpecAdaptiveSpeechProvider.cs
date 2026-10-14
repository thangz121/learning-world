// D_Audio/MaxSpecAdaptiveSpeechProvider.cs — Full Runtime Integration of Phase 1.2 -> 2.2 -> 3.1 -> 4.1 -> 5.0.
// Wraps ChildAudioFrontend, EnglishAcousticDspEngine, CloudSpeechResilienceEngine,
// CrossValidationPedagogicalEngine, and MultiTurnAdaptiveSessionEngine into an ISpeechAssessmentProvider.
// HONEST SCOPE: offline evidence chain (frontend VAD -> DSP -> resilience -> fusion -> session).
// There is NO ASR text in this provider (Azure not wired; local fallback placeholder returns an
// EMPTY transcript), so ProvidesTranscript=false and Transcript/Confidence stay empty/0.
//
// POLICY-COMPATIBLE ("option B", wp-1.9.41): the policy-facing evidence is produced by the SAME
// production-default path (LocalAcousticProvider = AcousticAnalysis + cohort margin + DSP payload).
// Fusion (4.1) and Session (5.0) outputs are ADVISORY payloads: they never rewrite AccuracyScore,
// AcousticMatch, margin, or any other decision-relevant field — so swapping the acoustic slot is
// behavior-compatible until calibration (option A) proves a fusion-driven variant is better.
// Runtime wiring: GameInstaller opt-in flag "-speak-maxspec" (inert by default).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public sealed class MaxSpecAdaptiveSpeechProvider : ISpeechAssessmentProvider {
  public string ProviderId => "max-spec-adaptive";
  public bool ProvidesTranscript => false; // no ASR text offline (see header)
  public bool ProvidesPhonemeEvidence => false; // policy-facing phoneme scores not claimed; fusion payload is advisory
  public bool RequiresNetwork => false; // fully offline evidence; transcript would require Azure (not bundled)

  readonly ITargetPronunciationProvider _targets;
  readonly LocalAcousticProvider _acousticBase; // policy-compatible evidence base (production default path)
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
    _speechEnergyFloor = speechEnergyFloor > 0f ? speechEnergyFloor : 0.001f;
    _acousticBase = new LocalAcousticProvider(targets, _speechEnergyFloor);
    _sessionEngine = sessionEngine ?? new MultiTurnAdaptiveSessionEngine();
  }

  public MultiTurnAdaptiveSessionEngine SessionEngine => _sessionEngine;

  public Task<SpeechRecognitionResult> RecognizeAsync(CapturedSpeech audio, WordId target, CancellationToken ct) {
    var sw = Stopwatch.StartNew();

    // 1) Policy-compatible base: identical evidence contract to the production default
    //    (VAD/energy + AcousticAnalysis + cohort margin + DSP payload + VN feedback).
    var result = _acousticBase.RecognizeAsync(audio, target, ct).Result;
    result.ProviderId = ProviderId;

    if (result.IsError || !result.HasSpeech || audio.Samples == null || audio.Samples.Length == 0) {
      result.LatencyMs = Math.Max(result.LatencyMs, sw.ElapsedMilliseconds);
      return Task.FromResult(result);
    }

    string targetWord = target.Value ?? string.Empty;
    float[] mono = audio.Channels > 1 ? ToMono(audio.Samples, audio.Channels) : audio.Samples;
    var pe = result.Pronunciation; // FromAcoustic evidence — POLICY FIELDS MUST STAY AS-IS (option B contract)

    TargetPronunciation pron = null;
    if (_targets != null) {
      _targets.TryGet(target, out pron);
    }

    // --- PHASE 1.2: Child Audio Frontend Telemetry (real on-device VAD analysis) ---
    var phase1Meta = ChildAudioFrontend.AnalyzeChildVAD(mono, audio.SampleRate);

    // --- PHASE 2.2: Local Acoustic DSP Engine (reuse the payload the base already computed) ---
    EnglishPhoneticPayload phase2Payload = pe.HasPhoneticPayload
      ? pe.PhoneticPayload
      : EnglishAcousticDspEngine.Analyze(mono, audio.SampleRate, pron);
    if (!pe.HasPhoneticPayload && phase2Payload.utterance_id != null) {
      pe.HasPhoneticPayload = true;
      pe.PhoneticPayload = phase2Payload;
    }

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

    // --- PHASE 4.1: Cross-Validation & Pedagogical Engine (advisory) ---
    var phase4Payload = CrossValidationPedagogicalEngine.Fuse(
        targetWord: targetWord,
        phase1: phase1Meta,
        phase2: phase2Payload,
        phase3: phase3Payload);

    // --- PHASE 5.0: Multi-Turn Session Orchestration (advisory) ---
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

    // 2) Attach advisory payloads ONLY. AccuracyScore / AcousticMatch / margin / fluency
    //    stay exactly as the LocalAcoustic path produced them (option B; pinned by CT-P21).
    pe.Source = "max-spec-adaptive";
    pe.HasFusionPayload = true;
    pe.FusionPayload = phase4Payload;
    pe.HasSessionPayload = true;
    pe.SessionPayload = phase5Payload;
    if (string.IsNullOrEmpty(pe.FeedbackVi)) pe.FeedbackVi = phase4Payload.pedagogical_feedback_for_4yo.voice_coach_prompt_vi;
    if (string.IsNullOrEmpty(pe.Tip)) pe.Tip = phase4Payload.pedagogical_feedback_for_4yo.phonetic_sound_effect_cue;
    if (string.IsNullOrEmpty(pe.ErrorPhoneme)) pe.ErrorPhoneme = phase4Payload.pedagogical_feedback_for_4yo.primary_error_focus;

    result.Pronunciation = pe;
    result.LatencyMs = Math.Max(result.LatencyMs, sw.ElapsedMilliseconds);
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
