// D_Audio/HybridSpeechAssessmentProvider.cs — the speaking foundation backend:
// the offline ACOUSTIC engine (closed-set, always available) PLUS the local
// recognition service (transcript, when running). Merge rules are FRR-first
// (never punish strong audio evidence with a weak/hallucinated transcript):
//   - acoustic error / no speech            -> acoustic result as-is
//   - service error / empty transcript      -> acoustic result as-is
//   - transcript present                    -> attach it (lexical path in the
//     frozen speaking policy), EXCEPT when audio evidence is strong+clean while
//     the transcript matches nothing (hallucination guard: drop transcript).
// C# 9.0 only.
using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class HybridSpeechAssessmentProvider : ISpeechAssessmentProvider {
  readonly ISpeechAssessmentProvider _acoustic;
  readonly ISpeechAssessmentProvider _service; // may be null (acoustic-only)
  readonly SpeakingPassPolicy _policy;

  public HybridSpeechAssessmentProvider(
      ISpeechAssessmentProvider acoustic,
      ISpeechAssessmentProvider service,
      SpeakingPassPolicy policy) {
    _acoustic = acoustic ?? throw new ArgumentNullException(nameof(acoustic));
    _service = service;
    _policy = policy ?? SpeakingPassPolicy.Default();
  }

  public string ProviderId { get { return "hybrid"; } }
  public bool ProvidesTranscript { get { return true; } } // when the service is up
  public bool ProvidesPhonemeEvidence { get { return false; } }
  public bool RequiresNetwork { get { return false; } }

  public async Task<SpeechRecognitionResult> RecognizeAsync(CapturedSpeech audio, WordId target,
      CancellationToken ct) {
    SpeechRecognitionResult ac = await _acoustic.RecognizeAsync(audio, target, ct);
    if (_service == null) return ac;
    // Never ring the service when the capture itself failed or held no speech:
    // the acoustic result already carries the honest outcome (policy decides).
    if (ac.IsError || !ac.HasSpeech) return ac;

    SpeechRecognitionResult svc;
    try {
      svc = await _service.RecognizeAsync(audio, target, ct);
    } catch (OperationCanceledException) {
      throw;
    } catch (Exception) {
      return ac; // service unavailable: acoustic-only, never an error
    }
    if (svc.IsError || string.IsNullOrWhiteSpace(svc.Transcript)) return ac;

    // FRR-first hallucination guard: a strong, clean acoustic match while the
    // transcript resembles nothing near the target means the transcript is the
    // weaker evidence (whisper hears chatter/noise) — keep the acoustic verdict.
    float lexical = LexicalMatcher.MatchScore(svc.Transcript, target.Value);
    bool strongCleanAudio = ac.Pronunciation.HasAcousticData
      && ac.Pronunciation.AcousticMatch >= _policy.AcousticPassMatch
      && !ac.Pronunciation.AcousticMissingEnding
      && !ac.Pronunciation.AcousticWeakEnding;
    if (strongCleanAudio && lexical < _policy.PartialLexicalFloor) return ac;

    ac.Transcript = svc.Transcript;
    ac.RecognitionConfidence = svc.RecognitionConfidence;
    ac.LatencyMs += svc.LatencyMs;
    ac.ProviderId = "hybrid/" + (svc.ProviderId ?? "service");
    return ac;
  }
}
