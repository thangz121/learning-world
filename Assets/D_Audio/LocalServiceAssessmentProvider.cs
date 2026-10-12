// D_Audio/LocalServiceAssessmentProvider.cs — transcript backend over the local
// speak-assess service (faster-whisper base.en). HONEST CONTRACT: transcript +
// recognition confidence only; NO pronunciation evidence. On any failure the
// result is an honest IsError (the hybrid falls back to acoustic-only, so a
// missing service NEVER blocks speaking practice). C# 9.0 only.
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public sealed class LocalServiceAssessmentProvider : ISpeechAssessmentProvider {
  readonly LocalAssessServiceClient _client;
  readonly float _speechEnergyFloor;

  public LocalServiceAssessmentProvider(LocalAssessServiceClient client, float speechEnergyFloor) {
    _client = client ?? new LocalAssessServiceClient();
    _speechEnergyFloor = speechEnergyFloor > 0f ? speechEnergyFloor : 0.001f;
  }

  public LocalServiceAssessmentProvider(LocalAssessServiceClient client)
    : this(client, 0.001f) { }

  public string ProviderId { get { return "local-service"; } }
  public bool ProvidesTranscript { get { return true; } }
  public bool ProvidesPhonemeEvidence { get { return false; } }
  public bool RequiresNetwork { get { return false; } } // localhost helper, offline-capable

  public async Task<SpeechRecognitionResult> RecognizeAsync(CapturedSpeech audio, WordId target,
      CancellationToken ct) {
    var sw = Stopwatch.StartNew();
    var result = new SpeechRecognitionResult();
    result.ProviderId = ProviderId;
    result.Pronunciation = PronunciationEvidence.None();
    result.ErrorReason = string.Empty;
    result.IsError = false;
    if (!string.IsNullOrEmpty(audio.Error)) {
      result.IsError = true;
      result.ErrorReason = audio.Error;
      result.LatencyMs = sw.ElapsedMilliseconds;
      return result;
    }
    if (audio.Cancelled) {
      result.IsError = true;
      result.ErrorReason = SpeechFailureReasons.Cancelled;
      result.LatencyMs = sw.ElapsedMilliseconds;
      return result;
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
      return result; // silence: the policy's NoSpeech path decides; never a service call
    }
    try {
      byte[] wav = LocalAssessServiceClient.EncodeWav(audio.Samples, audio.SampleRate, audio.Channels);
      LocalAssessServiceClient.Reply reply = await _client.AssessAsync(wav, target.Value, ct);
      result.Transcript = reply.transcript ?? string.Empty;
      result.RecognitionConfidence = reply.confidence;
      result.LatencyMs = sw.ElapsedMilliseconds;
      result.ProviderId = ProviderId + "/" + (reply.engine ?? "?"); // e.g. local-service/base.en
      return result;
    } catch (OperationCanceledException) {
      throw;
    } catch (Exception ex) {
      result.IsError = true;
      result.ErrorReason = SpeechFailureReasons.ProviderError;
      result.Transcript = string.Empty;
      result.RecognitionConfidence = 0f;
      result.LatencyMs = sw.ElapsedMilliseconds;
      if (ex != null) { /* details intentionally stay in the exception path */ }
      return result;
    }
  }
}
