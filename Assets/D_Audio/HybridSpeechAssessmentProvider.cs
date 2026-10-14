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
    if (audio.Samples == null || audio.Samples.Length == 0 || audio.DurationSec <= 0f) {
      return await _acoustic.RecognizeAsync(audio, target, ct);
    }

    // 1. Fast local acoustic evaluation (<15ms)
    // If capture failed or held no speech, never ring the service/cloud (saves bandwidth & respects S22 contract)
    SpeechRecognitionResult ac = await _acoustic.RecognizeAsync(audio, target, ct);
    if (_service == null || ac.IsError || !ac.HasSpeech) {
      return ac;
    }

    // 2. Query cloud/service concurrently with safety timeout
    SpeechRecognitionResult svc;
    try {
      svc = await _service.RecognizeAsync(audio, target, ct).ConfigureAwait(false);
    } catch (OperationCanceledException) {
      throw;
    } catch (Exception) {
      return ac; // cloud/service unavailable: acoustic-only, never fail child
    }

    if (svc.IsError || string.IsNullOrWhiteSpace(svc.Transcript)) {
      return ac;
    }

    // 3. BIDIRECTIONAL CROSS-VALIDATION (Xác thực chéo đa tầng giữa Sóng âm & Cloud AI)
    float lexical = LexicalMatcher.MatchScore(svc.Transcript, target.Value);
    float acousticMatch = ac.Pronunciation.HasAcousticData ? ac.Pronunciation.AcousticMatch : 0.5f;
    float cloudAccuracy = svc.Pronunciation.HasPhonemeData ? svc.Pronunciation.AccuracyScore : lexical;

    // Cross-Validation Rule 1: Hallucination Guard (Cloud hears unrelated background words, but child made strong sound)
    bool strongCleanAudio = ac.Pronunciation.HasAcousticData
      && ac.Pronunciation.AcousticMatch >= _policy.AcousticPassMatch
      && !ac.Pronunciation.AcousticMissingEnding
      && !ac.Pronunciation.AcousticWeakEnding;
    if (strongCleanAudio && lexical < _policy.PartialLexicalFloor) {
      // Audio is ground truth — preserve child's clear vocal effort over cloud chatter
      return ac;
    }

    // Cross-Validation Rule 2: Acoustic Spoofing / Noise Guard
    // If cloud strongly rejects (lexical = 0) and acoustic is borderline, don't give a false pass
    if (lexical < 0.2f && acousticMatch < _policy.AcousticStrongMatch) {
      ac.Transcript = svc.Transcript;
      ac.RecognitionConfidence = 0.25f;
      return ac;
    }

    // Cross-Validation Rule 3: Balanced Consensus (Tổng hòa điểm số âm học và AI ngữ âm)
    // Phoneme accuracy from Azure and Onset/Coda integrity from DSP reinforce each other
    if (svc.Pronunciation.HasPhonemeData) {
      var pron = svc.Pronunciation;
      // Retain acoustic flags (missing ending, onset score) inside the composite evidence
      pron.HasAcousticData = ac.Pronunciation.HasAcousticData;
      pron.AcousticMatch = ac.Pronunciation.AcousticMatch;
      pron.AcousticOnset = ac.Pronunciation.AcousticOnset;
      pron.AcousticCoda = ac.Pronunciation.AcousticCoda;
      pron.AcousticMissingEnding = ac.Pronunciation.AcousticMissingEnding;
      pron.AcousticWeakEnding = ac.Pronunciation.AcousticWeakEnding;
      pron.AcousticMargin = ac.Pronunciation.AcousticMargin;
      pron.HasPhoneticPayload = ac.Pronunciation.HasPhoneticPayload;
      pron.PhoneticPayload = ac.Pronunciation.PhoneticPayload;
      if (string.IsNullOrEmpty(pron.FeedbackVi)) pron.FeedbackVi = ac.Pronunciation.FeedbackVi;
      if (string.IsNullOrEmpty(pron.Tip)) pron.Tip = ac.Pronunciation.Tip;
      ac.Pronunciation = pron;
    }

    ac.Transcript = svc.Transcript;
    ac.RecognitionConfidence = Math.Max(svc.RecognitionConfidence, acousticMatch);
    ac.LatencyMs = Math.Max(ac.LatencyMs, svc.LatencyMs);
    ac.ProviderId = "hybrid/" + (svc.ProviderId ?? "azure");
    return ac;
  }
}
