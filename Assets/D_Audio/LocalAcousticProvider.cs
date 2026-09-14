// D_Audio/LocalAcousticProvider.cs — Phase 2.1-local offline assessment provider.
// ISpeechAssessmentProvider over the pure DSP core: VAD + energy (like
// LocalSpeechProvider) PLUS target-constrained acoustic evidence from
// AcousticAnalysis when the target carries pronunciation data. No transcript
// engine, no network, no model, no GPU — CPU burst only, all buffers released
// on return (Analyze retains nothing; the mono scratch is scope-local).
// Honesty: transcript stays "" (never faked); words without pronunciation
// data degrade to VAD-only (acoustic NOT AVAILABLE, policy falls back to
// PossibleAttempt); this method never throws (provider error, not a crash).
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public sealed class LocalAcousticProvider : ISpeechAssessmentProvider {
  public string ProviderId => "local-acoustic";
  public bool ProvidesTranscript => false;
  public bool ProvidesPhonemeEvidence => false;
  public bool RequiresNetwork => false;

  readonly ITargetPronunciationProvider _targets; // may be null (VAD-only fallback)
  readonly float _speechEnergyFloor;

  public LocalAcousticProvider(ITargetPronunciationProvider targets, float speechEnergyFloor) {
    _targets = targets;
    _speechEnergyFloor = speechEnergyFloor > 0f ? speechEnergyFloor : 0.001f;
  }

  public LocalAcousticProvider(ITargetPronunciationProvider targets) : this(targets, 0.001f) { }

  public Task<SpeechRecognitionResult> RecognizeAsync(CapturedSpeech audio, WordId target, CancellationToken ct) {
    var sw = Stopwatch.StartNew();
    var result = new SpeechRecognitionResult();
    result.ProviderId = "local-acoustic";
    result.Pronunciation = PronunciationEvidence.None();
    result.ErrorReason = string.Empty;
    result.IsError = false;
    try {
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
      float energy = audio.Samples != null
        ? VoiceActivity.MeanAbsolute(audio.Samples) : audio.MeanEnergy;
      result.HasSpeech = VoiceActivity.HasSpeech(energy, _speechEnergyFloor) && audio.DurationSec > 0f;
      result.MeanEnergy = energy;
      result.AudioDurationSec = audio.DurationSec;
      result.SpeechDurationSec = audio.VoicedSec;
      result.Transcript = string.Empty; // NO local STT: honestly empty, never faked.
      result.RecognitionConfidence = 0f;
      if (result.HasSpeech && _targets != null && audio.Samples != null && audio.Samples.Length > 0) {
        TargetPronunciation pron;
        if (_targets.TryGet(target, out pron) && pron != null && pron.IsUsable()) {
          float[] mono = ToMono(audio.Samples, audio.Channels);
          AcousticEvidence evidence = AcousticAnalysis.Analyze(mono, audio.SampleRate, pron);
          mono = null; // release scratch (Analyze retains nothing)
          if (evidence.HasAcousticData)
            result.Pronunciation = PronunciationEvidence.FromAcoustic(evidence);
        }
      }
      result.LatencyMs = sw.ElapsedMilliseconds;
      return Task.FromResult(result);
    } catch (Exception) {
      result.IsError = true;
      result.ErrorReason = SpeechFailureReasons.ProviderError;
      result.HasSpeech = false;
      result.Pronunciation = PronunciationEvidence.None();
      result.LatencyMs = sw.ElapsedMilliseconds;
      return Task.FromResult(result);
    }
  }

  static float[] ToMono(float[] samples, int channels) {
    if (samples == null) return new float[0];
    if (channels <= 1) return samples; // zero-copy: Analyze reads only
    int frames = samples.Length / channels;
    var mono = new float[frames];
    for (int i = 0; i < frames; i++) {
      double sum = 0;
      for (int c = 0; c < channels; c++) sum += samples[i * channels + c];
      mono[i] = (float)(sum / channels);
    }
    return mono;
  }
}
