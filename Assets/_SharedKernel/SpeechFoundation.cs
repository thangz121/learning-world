// _SharedKernel/SpeechFoundation.cs — Phase 2.1 speech domain core (Lead owns).
// Pure C# (NO UnityEngine): every type here is EditMode-testable without a device.
//
// LAYER MODEL (conceptual, §2): Microphone -> Audio Capture -> Voice Activity
// Detection -> Speech Segment -> Recognition -> Transcript+confidence ->
// Pronunciation analysis -> Target comparison -> Intelligibility estimation ->
// SpeakingAssessment -> Quest decision. Transcript is ONE signal, never the verdict.
//
// SCORE HONESTY (§7/§69):
// - LexicalMatchScore   = recognition/transcript evidence that the TARGET word was
//   produced (string matching over transcript tokens). A lexical signal, NOT a
//   pronunciation measurement.
// - PronunciationScore  = acoustic/phonetic evidence closeness, ONLY meaningful when
//   HasPronunciationEvidence is true (phoneme-level provider such as Azure
//   Pronunciation Assessment). With transcript-only backends it is NaN and MUST be
//   reported as NOT AVAILABLE, never synthesized from string distance.
// - IntelligibilityScore = PROXY estimate that a listener/recognizer would resolve
//   the output as the target (IntelligibilityIsProxy is ALWAYS true in 2.1: no
//   human listener study exists, so HUMAN INTELLIGIBILITY is NOT PROVEN).
// - OverallScore        = decision-oriented aggregation (weights below), not a claim
//   of absolute pronunciation accuracy.
// - RecognitionConfidence (transcript confidence) is NEVER presented as
//   pronunciation confidence (§25).
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// Microphone capability (§11). Devices.Count > 0 alone never proves usability:
// Ready means "a device is enumerated"; a failed capture still reports Error.
public enum MicStatus {
  Unknown,
  Ready,             // >=1 device enumerated, selected device set
  NoDevice,          // zero devices (MIC_UNAVAILABLE)
  PermissionDenied,  // OS/Unity denied microphone authorization
  Error              // device exists but capture failed
}

// Assessment decision (§6). Kept to the 8 semantic states the phase requires.
public enum SpeakingDecision {
  NoSpeech,        // no voice activity captured
  TooWeak,         // activity below usable floor (quiet/silence-like)
  Unclear,         // speech present but evidence too conflicting to judge
  WrongWord,       // confident speech that does not resemble the target
  PossibleAttempt, // speech present, target resemblance unknown (e.g. no transcript engine)
  Partial,         // credible attempt at the target, incomplete/low evidence
  Pass,            // credible, understandable target production
  StrongPass       // high-confidence target production (+ phoneme evidence when available)
}

// Machine-readable reason codes (developer log + tests, never child-facing text).
public static class SpeechFailureReasons {
  public const string None = "none";
  public const string NoSpeech = "no_speech";
  public const string TooWeak = "too_weak";
  public const string Unclear = "unclear";
  public const string WrongWord = "wrong_word";
  public const string Partial = "partial";
  public const string PossibleAttempt = "possible_attempt";
  public const string TranscriptUnavailable = "transcript_unavailable";
  public const string LowConfidence = "low_confidence";
  public const string ProviderError = "provider_error";
  public const string NetworkError = "network_error";
  public const string Timeout = "timeout";
  public const string DeviceError = "device_error";
  public const string MicUnavailable = "mic_unavailable";
  public const string PermissionDenied = "permission_denied";
  public const string Cancelled = "cancelled";
  public const string Stale = "stale_result_ignored";
}

// Phoneme/acoustic evidence from a pronunciation backend (§8/§28).
// Default (HasPhonemeData=false, Source="none") means NOT AVAILABLE —
// scoring MUST NOT treat AccuracyScore as real data in that state.
[Serializable]
public struct PronunciationEvidence {
  public bool HasPhonemeData;   // true ONLY when the backend returned phoneme/word-level scores
  public string Source;         // "none" | "azure" (future backends extend here)
  public float AccuracyScore;   // 0..1 (phoneme-aggregated accuracy, HundredMark/100)
  public float FluencyScore;    // 0..1
  public float CompletenessScore; // 0..1
  public float WordAccuracy;    // 0..1 accuracy of the target word itself
  public string WordErrorType;  // "None" | "Mispronunciation" | "Omission" | "Insertion" | ""
  public int PhonemeCount;

  public static PronunciationEvidence None() {
    return new PronunciationEvidence {
      HasPhonemeData = false, Source = "none",
      AccuracyScore = float.NaN, FluencyScore = float.NaN,
      CompletenessScore = float.NaN, WordAccuracy = float.NaN,
      WordErrorType = string.Empty, PhonemeCount = 0
    };
  }
}

// Normalized provider output (§83). Provider SDK types NEVER leave the provider:
// every backend maps to this struct before gameplay sees it.
[Serializable]
public struct SpeechRecognitionResult {
  public string Transcript;            // raw transcript ("" when none/unavailable)
  public float RecognitionConfidence;  // 0..1 transcript confidence (NOT pronunciation)
  public bool HasSpeech;               // VAD: voice activity present in the segment
  public float AudioDurationSec;       // captured audio length
  public float SpeechDurationSec;      // voiced portion length (0 when unknown)
  public float MeanEnergy;             // 0..1 normalized mean energy
  public string ProviderId;            // "mock" | "fallback" | "local" | "azure"
  public long LatencyMs;               // provider round-trip (capture excluded)
  public string ErrorReason;           // "" when OK; otherwise a SpeechFailureReasons code family
  public bool IsError;                 // true for provider/device/network/timeout failures
  public PronunciationEvidence Pronunciation;

  public bool HasTranscript() { return !string.IsNullOrWhiteSpace(Transcript); }
}

// A single speaking attempt (§5). Gameplay reads this/assessment, never raw mic state.
[Serializable]
public sealed class SpeechAttempt {
  public string AttemptId;
  public WordId Target;
  public float CaptureDurationSec;
  public bool SpeechDetected;
  public string Transcript;
  public float RecognitionConfidence;
  public float LexicalMatchScore;
  public float PronunciationScore; // NaN when NOT AVAILABLE
  public float IntelligibilityScore;
  public float OverallScore;
  public SpeakingDecision Decision;
  public string FailureReason;
  public bool IsEnvironmentError;  // mic/provider/network/timeout: NOT child performance
  public DateTime AtUtc;
}

// Final assessment (§6). Decision drives quest policy; scores are evidence.
[Serializable]
public struct SpeakingAssessment {
  public string AttemptId;
  public WordId Target;
  public bool AttemptDetected;       // credible vocal attempt (any decision except NoSpeech/error-silence)
  public bool SpeechDetected;        // VAD fired
  public float RecognitionConfidence;
  public float LexicalMatchScore;    // 0..1 transcript-vs-target (NaN when no transcript)
  public float PronunciationScore;   // 0..1, NaN when NOT AVAILABLE
  public bool HasPronunciationEvidence;
  public float IntelligibilityScore; // PROXY (IntelligibilityIsProxy always true in 2.1)
  public bool IntelligibilityIsProxy;
  public float OverallScore;         // decision-oriented aggregation
  public SpeakingDecision Decision;
  public string FailureReason;
  public bool IsEnvironmentError;    // ENVIRONMENT FAILURE != CHILD FAILURE (§90)
  public string Evidence;            // compact developer summary (no PII, no audio)
  public long ProviderLatencyMs;
  public string ProviderId;
  public DateTime AtUtc;

  // Quest-facing convenience: which legacy SpeechLevel this maps to.
  // Frozen QuestManager.AdvanceOnSpoken advances Speak ONLY on Perfect/Great,
  // so Partial/PossibleAttempt (=Almost) encourage retry without failing.
  public SpeechLevel ToSpeechLevel() {
    switch (Decision) {
      case SpeakingDecision.StrongPass: return SpeechLevel.Perfect;
      case SpeakingDecision.Pass: return SpeechLevel.Great;
      case SpeakingDecision.Partial:
      case SpeakingDecision.PossibleAttempt: return SpeechLevel.Almost;
      default: return SpeechLevel.TryTogether;
    }
  }
}

// Microphone capability snapshot (UI + quest policy read this, not Mic APIs).
[Serializable]
public struct SpeechCapability {
  public MicStatus Status;
  public string DeviceName;
  public int DeviceCount;
  public bool IsAvailable() { return Status == MicStatus.Ready; }
}

// Lexical matching over transcript tokens (§29).
// Used ONLY for lexical evidence. NEVER presented as pronunciation quality:
// "bal" ~= "ball" is string similarity, not acoustic evidence.
public static class LexicalMatcher {
  // 1.0 when the transcript contains the target as a whole word
  // ("the ball", "ball please", "ball ball" all count — §44 cases 2/3/11).
  // Otherwise the best normalized token similarity (0..1).
  public static float MatchScore(string transcript, string target) {
    string normTarget = NormalizeToken(target);
    if (string.IsNullOrEmpty(normTarget)) return 0f;
    List<string> tokens = Tokenize(transcript);
    if (tokens.Count == 0) return 0f;
    float best = 0f;
    for (int i = 0; i < tokens.Count; i++) {
      if (tokens[i] == normTarget) return 1f;
      float s = Similarity(tokens[i], normTarget);
      if (s > best) best = s;
    }
    return best;
  }

  public static bool ContainsTargetWord(string transcript, string target) {
    string normTarget = NormalizeToken(target);
    if (string.IsNullOrEmpty(normTarget)) return false;
    List<string> tokens = Tokenize(transcript);
    for (int i = 0; i < tokens.Count; i++) {
      if (tokens[i] == normTarget) return true;
    }
    return false;
  }

  public static List<string> Tokenize(string text) {
    var tokens = new List<string>();
    if (string.IsNullOrEmpty(text)) return tokens;
    string lower = text.ToLowerInvariant();
    int start = -1;
    for (int i = 0; i <= lower.Length; i++) {
      char c = i < lower.Length ? lower[i] : ' ';
      bool isToken = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
      if (isToken) {
        if (start < 0) start = i;
      } else if (start >= 0) {
        tokens.Add(lower.Substring(start, i - start));
        start = -1;
      }
    }
    return tokens;
  }

  public static string NormalizeToken(string text) {
    List<string> tokens = Tokenize(text);
    if (tokens.Count == 0) return string.Empty;
    // Multi-word targets ("thank you") match on joined form; single words as-is.
    string joined = string.Empty;
    for (int i = 0; i < tokens.Count; i++) joined += tokens[i];
    return tokens.Count == 1 ? tokens[0] : joined;
  }

  public static float Similarity(string a, string b) {
    if (a == b) return 1f;
    if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0f;
    int dist = Levenshtein(a, b);
    int max = Math.Max(a.Length, b.Length);
    return 1f - (float)dist / (float)max;
  }

  static int Levenshtein(string a, string b) {
    int n = a.Length, m = b.Length;
    if (n == 0) return m;
    if (m == 0) return n;
    int[] prev = new int[m + 1];
    int[] cur = new int[m + 1];
    for (int j = 0; j <= m; j++) prev[j] = j;
    for (int i = 1; i <= n; i++) {
      cur[0] = i;
      for (int j = 1; j <= m; j++) {
        int cost = a[i - 1] == b[j - 1] ? 0 : 1;
        int del = prev[j] + 1;
        int ins = cur[j - 1] + 1;
        int sub = prev[j - 1] + cost;
        cur[j] = Math.Min(Math.Min(del, ins), sub);
      }
      int[] t = prev; prev = cur; cur = t;
    }
    return prev[m];
  }
}

// Configurable, named, documented thresholds (§70). No word-specific branches (§72):
// every target flows through the same numbers. Defaults encode the child policy
// (§71: attempt > perfection — Partial never fails, it encourages).
[Serializable]
public sealed class SpeakingPassPolicy {
  public float MinRecognitionConfidence = 0.35f;
  public float PassLexical = 0.80f;
  public float StrongLexical = 0.95f;
  public float StrongConfidence = 0.75f;
  public float PartialLexicalFloor = 0.40f;
  public float MinPronunciationForPass = 0.40f; // applies ONLY with phoneme evidence
  public float SilenceEnergyFloor = 0.001f;
  public float WeakEnergyFloor = 0.005f;
  public float MinSpeechDurationSec = 0.15f;

  public static SpeakingPassPolicy Default() { return new SpeakingPassPolicy(); }

  // Pure assessment: (recognition result, target) -> SpeakingAssessment.
  // Never throws (transcript may be any language/empty/null — §32).
  public SpeakingAssessment Decide(SpeechRecognitionResult result, WordId target, string attemptId) {
    var assessment = new SpeakingAssessment();
    assessment.AttemptId = attemptId ?? "spk-0";
    assessment.Target = target;
    assessment.AtUtc = DateTime.UtcNow;
    assessment.ProviderId = result.ProviderId ?? "unknown";
    assessment.ProviderLatencyMs = result.LatencyMs;
    assessment.IntelligibilityIsProxy = true; // ALWAYS in 2.1 (no human study)
    assessment.SpeechDetected = result.HasSpeech;
    assessment.RecognitionConfidence = result.RecognitionConfidence;
    assessment.HasPronunciationEvidence =
      result.Pronunciation.HasPhonemeData && !float.IsNaN(result.Pronunciation.AccuracyScore);

    // --- Environment failures are NEVER child failures (§14/§90). ---
    if (result.IsError) {
      assessment.AttemptDetected = false;
      assessment.Decision = SpeakingDecision.Unclear;
      assessment.FailureReason = string.IsNullOrEmpty(result.ErrorReason)
        ? SpeechFailureReasons.ProviderError : result.ErrorReason;
      assessment.IsEnvironmentError = true;
      assessment.LexicalMatchScore = float.NaN;
      assessment.PronunciationScore = float.NaN;
      assessment.IntelligibilityScore = 0f;
      assessment.OverallScore = 0f;
      assessment.Evidence = BuildEvidence(target, result, float.NaN, "error");
      return assessment;
    }

    bool speechLongEnough = result.SpeechDurationSec >= MinSpeechDurationSec || result.SpeechDurationSec <= 0f && result.HasSpeech;
    bool usableEnergy = result.MeanEnergy >= WeakEnergyFloor;

    // --- QUESTION A: did the child attempt to speak? (§4) ---
    if (!result.HasSpeech || result.MeanEnergy < SilenceEnergyFloor || !speechLongEnough && result.MeanEnergy < WeakEnergyFloor) {
      assessment.AttemptDetected = false;
      assessment.Decision = SpeakingDecision.NoSpeech;
      assessment.FailureReason = SpeechFailureReasons.NoSpeech;
      assessment.IsEnvironmentError = false;
      assessment.LexicalMatchScore = float.NaN;
      assessment.PronunciationScore = float.NaN;
      assessment.IntelligibilityScore = 0f;
      assessment.OverallScore = 0f;
      assessment.Evidence = BuildEvidence(target, result, float.NaN, "no-speech");
      return assessment;
    }
    if (!usableEnergy) {
      assessment.AttemptDetected = true;
      assessment.SpeechDetected = true;
      assessment.Decision = SpeakingDecision.TooWeak;
      assessment.FailureReason = SpeechFailureReasons.TooWeak;
      assessment.IsEnvironmentError = false;
      assessment.LexicalMatchScore = float.NaN;
      assessment.PronunciationScore = float.NaN;
      assessment.IntelligibilityScore = 0.1f;
      assessment.OverallScore = 0.1f;
      assessment.Evidence = BuildEvidence(target, result, float.NaN, "too-weak");
      return assessment;
    }

    assessment.AttemptDetected = true;

    // --- Speech without transcript: attempt is real, resemblance unknown (§87). ---
    if (!result.HasTranscript()) {
      assessment.Decision = SpeakingDecision.PossibleAttempt;
      assessment.FailureReason = SpeechFailureReasons.TranscriptUnavailable;
      assessment.IsEnvironmentError = false;
      assessment.LexicalMatchScore = float.NaN;
      assessment.PronunciationScore = float.NaN;
      assessment.IntelligibilityScore = Math.Min(0.49f, 0.3f + result.MeanEnergy);
      assessment.OverallScore = assessment.IntelligibilityScore;
      assessment.Evidence = BuildEvidence(target, result, float.NaN, "speech-no-transcript");
      return assessment;
    }

    // --- QUESTIONS B/C: resemblance + intelligibility proxy (§4). ---
    string transcript = result.Transcript ?? string.Empty;
    float lexical = LexicalMatcher.MatchScore(transcript, target.Value);
    assessment.LexicalMatchScore = lexical;
    bool contains = LexicalMatcher.ContainsTargetWord(transcript, target.Value);
    float conf = result.RecognitionConfidence;

    float pron = float.NaN;
    if (assessment.HasPronunciationEvidence) {
      pron = result.Pronunciation.AccuracyScore;
      if (pron < 0f) pron = 0f;
      if (pron > 1f) pron = 1f;
    }
    assessment.PronunciationScore = pron;

    // Intelligibility PROXY: recognizer-as-listener. Weights documented:
    // lexical 0.5 + confidence 0.3 + pronunciation-or-lexical-fallback 0.2.
    float pronOrLex = float.IsNaN(pron) ? lexical : pron;
    float intelligibility = 0.5f * lexical + 0.3f * conf + 0.2f * pronOrLex;
    assessment.IntelligibilityScore = intelligibility;
    assessment.OverallScore = intelligibility;

    if (contains && conf >= StrongConfidence && lexical >= StrongLexical
        && (float.IsNaN(pron) || pron >= MinPronunciationForPass)) {
      // StrongPass WITHOUT phoneme evidence is a lexical+confidence verdict
      // (recognizer understood clearly), never a pronunciation-perfection claim.
      assessment.Decision = SpeakingDecision.StrongPass;
      assessment.FailureReason = SpeechFailureReasons.None;
    } else if (contains && lexical >= PassLexical && conf >= MinRecognitionConfidence
        && (float.IsNaN(pron) || pron >= MinPronunciationForPass)) {
      assessment.Decision = SpeakingDecision.Pass;
      assessment.FailureReason = SpeechFailureReasons.None;
    } else if (lexical >= PartialLexicalFloor) {
      // "ba"/"bal"/"bol" for BALL land here: credible attempt, incomplete evidence.
      // Phoneme evidence below floor caps a lexical pass at Partial (downgrade path).
      assessment.Decision = SpeakingDecision.Partial;
      assessment.FailureReason = SpeechFailureReasons.Partial;
    } else if (conf < MinRecognitionConfidence && lexical >= PartialLexicalFloor * 0.5f) {
      assessment.Decision = SpeakingDecision.Unclear;
      assessment.FailureReason = SpeechFailureReasons.LowConfidence;
    } else {
      assessment.Decision = SpeakingDecision.WrongWord;
      assessment.FailureReason = SpeechFailureReasons.WrongWord;
    }
    if (!float.IsNaN(pron) && pron < MinPronunciationForPass
        && (assessment.Decision == SpeakingDecision.Pass || assessment.Decision == SpeakingDecision.StrongPass)) {
      assessment.Decision = SpeakingDecision.Partial;
      assessment.FailureReason = SpeechFailureReasons.Partial;
    }
    assessment.IsEnvironmentError = false;
    assessment.Evidence = BuildEvidence(target, result, lexical,
      contains ? "contains-target" : "no-target-token");
    return assessment;
  }

  static string BuildEvidence(WordId target, SpeechRecognitionResult result, float lexical, string note) {
    string lex = float.IsNaN(lexical) ? "n/a" : lexical.ToString("0.00");
    string pron = result.Pronunciation.HasPhonemeData ? result.Pronunciation.AccuracyScore.ToString("0.00") : "n/a";
    string heard = string.IsNullOrWhiteSpace(result.Transcript) ? "<empty>" : result.Transcript.Trim();
    if (heard.Length > 48) heard = heard.Substring(0, 48) + "...";
    return "target=" + target.Value + " heard=\"" + heard + "\" speech=" + (result.HasSpeech ? "Y" : "N")
      + " conf=" + result.RecognitionConfidence.ToString("0.00") + " lexical=" + lex
      + " pron=" + pron + " provider=" + result.ProviderId + " note=" + note;
  }
}

// ------- Ports (provider-agnostic seams, §9) -------

// Microphone device lifecycle (§42). Unity impl polls Microphone.devices;
// fakes inject scripted lists. Small on purpose (§84).
public interface IMicrophoneDevice {
  event Action<MicStatus> StatusChanged;
  MicStatus Status { get; }
  string SelectedDevice { get; }
  string[] Devices { get; }
  SpeechCapability Capability { get; }
  void Refresh();
  void ReportCaptureFailure(); // listed device proved unusable -> Error, not Ready
}

// Raw captured segment (discarded after recognition — never stored, §21).
public struct CapturedSpeech {
  public float[] Samples;
  public int SampleRate;
  public int Channels;
  public float DurationSec;
  public float MeanEnergy;
  public float PeakEnergy;
  public float VoicedSec;   // VAD estimate inside the segment
  public bool TimedOut;
  public bool Cancelled;
  public string Error;      // "" when OK
  public bool IsOk() { return string.IsNullOrEmpty(Error) && Samples != null; }
}

public interface ISpeechAudioCapture {
  bool IsCapturing { get; }
  Task<CapturedSpeech> CaptureAsync(float maxDurationSec, float silenceTimeoutSec, CancellationToken ct);
  void Cancel();
}

// Recognition + pronunciation backend (§9/§83). Implementations: local (VAD-only,
// honest no-transcript), Azure (phoneme evidence when key/SDK configured),
// fake (scripted), fallback (offline intent). SDK types never escape this seam.
public interface ISpeechAssessmentProvider {
  string ProviderId { get; }
  bool ProvidesTranscript { get; }
  bool ProvidesPhonemeEvidence { get; }
  bool RequiresNetwork { get; }
  Task<SpeechRecognitionResult> RecognizeAsync(CapturedSpeech audio, WordId target, CancellationToken ct);
}

// Gameplay-facing speech service (§10). Quest/presenters use ONLY this:
// never Microphone.*, never any SDK. Cancel/timeout/recover/errors included.
public interface ISpeechRecognizer {
  event Action<SpeechCapability> CapabilityChanged;
  SpeechCapability Capability { get; }
  Task<SpeakingAssessment> StartAttemptAsync(WordId target, CancellationToken ct);
  void Cancel();
}
