// B_Brain/SpeakingExercise.cs — Phase 2.1 speaking-only lesson (Agent B).
// Isolated "Say the word" activity (§16): NPC prompt -> listen -> your-turn cue ->
// capture -> assessment -> child feedback -> PASS/retry. No pickup/carry/world
// staging dependency. Target comes from DATA (SpeakingExerciseConfig), never
// from C# literals (§17): ball works today, apple/teddy/red/one/please/thank_you
// need no code changes (§73).
//
// Quest path (§39): Quest -> runner -> ISpeechRecognizer -> SpeakingAssessment ->
// WordSpokenEvent -> (existing MarketBootstrap glue) -> QuestManager.
// AdvanceOnSpoken. The runner NEVER calls provider SDKs or Microphone.* (§10).
// Learning path (§37): WordSpokenEvent carries the SpeechLevel; no new mastery
// system, no threshold changes (2C frozen). Silence publishes NOTHING (§71:
// quiet/absent speech is not evidence). Environment failures publish NOTHING
// and never touch mastery (§14/§90).
using System;
using System.Threading;
using System.Threading.Tasks;

public enum SpeakingExerciseOutcome {
  Passed,            // Pass/StrongPass within attempts
  PartialComplete,   // retries exhausted, best was Partial/PossibleAttempt (quest open)
  NotPassed,         // retries exhausted on Wrong/Unclear/NoSpeech (quest open, NOT failed)
  SkippedNoMic,      // deferred: speaking unavailable (§13/§57)
  ErrorAborted       // provider errors exhausted attempts (environment, NOT child failure)
}

// Data-driven exercise definition. Lives in Content/speaking/*.json;
// JsonUtility-compatible (public fields only). Decisions stored as strings
// ("pass"/"strong") so content stays human-readable.
[Serializable]
public sealed class SpeakingExerciseConfig {
  public string id = string.Empty;
  public string targetWord = string.Empty;
  public string questId = string.Empty; // "" = standalone (no quest binding)
  public int attemptsAllowed = 3;
  public string minDecisionForPass = "pass";
  public bool replayPromptOnRetry = true;
  public string promptLine = string.Empty;    // NPC instruction (content-owned, §55)
  public string successLine = string.Empty;
  public string retryLine = string.Empty;
  public string noSpeechLine = string.Empty;
  public string wrongLine = string.Empty;
  public string unclearLine = string.Empty;
  public string micMissingLine = string.Empty;
  public string errorLine = string.Empty;

  public WordId TargetWordId() { return new WordId(string.IsNullOrEmpty(targetWord) ? "ball" : targetWord); }

  public SpeakingDecision MinDecision() {
    if (string.Equals(minDecisionForPass, "strong", StringComparison.OrdinalIgnoreCase))
      return SpeakingDecision.StrongPass;
    return SpeakingDecision.Pass;
  }

  public int BoundedAttempts() {
    if (attemptsAllowed < 1) return 1;
    if (attemptsAllowed > 5) return 5; // bounded retry, never infinite (§35)
    return attemptsAllowed;
  }

  // Child-facing line for a decision (technical scores NEVER shown, §18/§53).
  public string ChildMessage(SpeakingDecision decision) {
    switch (decision) {
      case SpeakingDecision.StrongPass:
      case SpeakingDecision.Pass: return string.IsNullOrEmpty(successLine) ? "Great!" : successLine;
      case SpeakingDecision.Partial:
      case SpeakingDecision.PossibleAttempt: return string.IsNullOrEmpty(retryLine) ? "Try again." : retryLine;
      case SpeakingDecision.NoSpeech:
      case SpeakingDecision.TooWeak: return string.IsNullOrEmpty(noSpeechLine) ? "Can you say it?" : noSpeechLine;
      case SpeakingDecision.WrongWord: return string.IsNullOrEmpty(wrongLine) ? "Listen and try." : wrongLine;
      case SpeakingDecision.Unclear: return string.IsNullOrEmpty(unclearLine) ? "Let's try again." : unclearLine;
      default: return string.IsNullOrEmpty(errorLine) ? "Let's try again." : errorLine;
    }
  }

  public string MicMissingMessage() {
    return string.IsNullOrEmpty(micMissingLine) ? "Microphone not connected." : micMissingLine;
  }

  public static SpeakingExerciseConfig DefaultFor(WordId target) {
    return new SpeakingExerciseConfig {
      id = "say_" + target.Value,
      targetWord = target.Value,
      questId = string.Empty,
      attemptsAllowed = 3,
      minDecisionForPass = "pass",
      replayPromptOnRetry = true,
      promptLine = "Can you say " + target.Value + "?",
      successLine = "Great!",
      retryLine = "Try again.",
      noSpeechLine = "Can you say it?",
      wrongLine = "Listen and try.",
      unclearLine = "Let's try again.",
      micMissingLine = "Microphone not connected.",
      errorLine = "Let's try again."
    };
  }
}

public sealed class SpeakingExerciseResult {
  public SpeakingExerciseOutcome Outcome;
  public SpeakingExerciseConfig Config;
  public SpeakingAssessment BestAssessment;
  public int AttemptsUsed;
  public string ChildMessage;
  public bool HasAssessment;
}

// No-microphone quest policy (§57, Option B: defer the quest, transient).
// Speech-unavailable is recorded as an ENVIRONMENTAL condition, never as a
// learning failure. Session-memory only (no persistence per §57 minimal rule);
// capability-return only marks eligibility — the player still initiates (§58).
public sealed class SpeechQuestDeferral {
  readonly System.Collections.Generic.HashSet<string> _deferred =
    new System.Collections.Generic.HashSet<string>();

  public void MarkDeferred(QuestId quest) {
    if (!string.IsNullOrEmpty(quest.Value)) _deferred.Add(quest.Value);
  }

  public bool IsDeferred(QuestId quest) { return _deferred.Contains(quest.Value); }

  public void Clear(QuestId quest) { _deferred.Remove(quest.Value); }

  // Mic returned: deferred quests become eligible again (caller decides UX).
  public string[] EligibleAfterReconnect(SpeechCapability capability) {
    if (!capability.IsAvailable()) return new string[0];
    string[] all = new string[_deferred.Count];
    _deferred.CopyTo(all);
    return all;
  }
}

// The listen->speak loop (§19). Constructed by composition root / tests with
// explicit seams; audio + bus are optional (null-safe) so pure-logic tests run
// without Unity.
public sealed class SpeakingExerciseRunner {
  readonly ISpeechRecognizer _recognizer;
  readonly IAudioDirector _audio; // optional: NPC prompt + vocab replay
  readonly IGameEventBus _bus;    // optional: WordSpokenEvent -> quest/learning glue
  readonly SpeechQuestDeferral _deferral;

  public SpeakingExerciseRunner(ISpeechRecognizer recognizer, IAudioDirector audio, IGameEventBus bus)
    : this(recognizer, audio, bus, null) {
  }

  public SpeakingExerciseRunner(
    ISpeechRecognizer recognizer, IAudioDirector audio, IGameEventBus bus, SpeechQuestDeferral deferral) {
    _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
    _audio = audio;
    _bus = bus;
    _deferral = deferral ?? new SpeechQuestDeferral();
  }

  public SpeechQuestDeferral Deferral => _deferral;

  public async Task<SpeakingExerciseResult> RunAsync(SpeakingExerciseConfig config, CancellationToken ct) {
    if (config == null) throw new ArgumentNullException(nameof(config));
    var outcome = new SpeakingExerciseResult { Config = config, AttemptsUsed = 0, HasAssessment = false };
    WordId target = config.TargetWordId();

    // --- No-microphone policy (§13/§57): explain once, defer, NO failure. ---
    SpeechCapability cap;
    try { cap = _recognizer.Capability; } catch (Exception) { cap = new SpeechCapability(); }
    if (!cap.IsAvailable()) {
      if (!string.IsNullOrEmpty(config.questId)) _deferral.MarkDeferred(new QuestId(config.questId));
      outcome.Outcome = SpeakingExerciseOutcome.SkippedNoMic;
      outcome.ChildMessage = config.MicMissingMessage();
      return outcome;
    }
    if (!string.IsNullOrEmpty(config.questId)) _deferral.Clear(new QuestId(config.questId));

    // --- LISTEN: NPC says the target (canonical audio mapping reused, §54). ---
    await PlayPromptAsync(target, config, ct).ConfigureAwait(false);

    int maxAttempts = config.BoundedAttempts();
    SpeakingAssessment best = default(SpeakingAssessment);
    bool hasBest = false;

    for (int attempt = 1; attempt <= maxAttempts; attempt++) {
      ct.ThrowIfCancellationRequested();
      outcome.AttemptsUsed = attempt;
      SpeakingAssessment assessment = await _recognizer.StartAttemptAsync(target, ct).ConfigureAwait(false);
      outcome.BestAssessment = assessment;
      outcome.HasAssessment = true;
      if (!hasBest || assessment.OverallScore > best.OverallScore) { best = assessment; hasBest = true; }

      if (assessment.IsEnvironmentError) {
        // Provider/mic failure mid-exercise (§24): child hears "try again";
        // technical reason stays in the developer log only (§56).
        if (attempt >= maxAttempts) {
          outcome.Outcome = SpeakingExerciseOutcome.ErrorAborted;
          outcome.ChildMessage = config.ChildMessage(SpeakingDecision.Unclear);
          return outcome;
        }
        continue;
      }

      bool passed = assessment.Decision == SpeakingDecision.StrongPass
        || (assessment.Decision == SpeakingDecision.Pass && config.MinDecision() == SpeakingDecision.Pass);
      if (passed) {
        PublishSpoken(target, assessment);
        outcome.Outcome = SpeakingExerciseOutcome.Passed;
        outcome.ChildMessage = config.ChildMessage(assessment.Decision);
        return outcome;
      }

      // Learning evidence for REAL attempts (Partial/Possible/Wrong/Unclear carry
      // SpeechLevel; silence publishes nothing — it is not an attempt).
      if (assessment.Decision == SpeakingDecision.Partial
          || assessment.Decision == SpeakingDecision.PossibleAttempt
          || assessment.Decision == SpeakingDecision.WrongWord
          || assessment.Decision == SpeakingDecision.Unclear) {
        PublishSpoken(target, assessment);
      }

      if (attempt >= maxAttempts) {
        if (best.Decision == SpeakingDecision.Partial || best.Decision == SpeakingDecision.PossibleAttempt) {
          outcome.Outcome = SpeakingExerciseOutcome.PartialComplete;
          outcome.BestAssessment = best;
        } else {
          outcome.Outcome = SpeakingExerciseOutcome.NotPassed;
        }
        outcome.ChildMessage = config.ChildMessage(best.Decision);
        return outcome;
      }

      // Retry path: listen again (demo) before the next attempt (§35 ladder).
      if (config.replayPromptOnRetry) await PlayPromptAsync(target, config, ct).ConfigureAwait(false);
    }

    outcome.Outcome = SpeakingExerciseOutcome.NotPassed;
    outcome.ChildMessage = config.ChildMessage(SpeakingDecision.Unclear);
    return outcome;
  }

  void PublishSpoken(WordId target, SpeakingAssessment assessment) {
    try {
      if (_bus == null) return;
      _bus.Publish(new WordSpokenEvent(target,
        new SpeechResult { Expected = target, Heard = string.Empty, Similarity = assessment.OverallScore, Level = assessment.ToSpeechLevel() },
        LearnSource.Quest));
    } catch (Exception) { }
  }

  async Task PlayPromptAsync(WordId target, SpeakingExerciseConfig config, CancellationToken ct) {
    if (_audio == null || ct.IsCancellationRequested) return;
    try {
      await _audio.PlayVocabularyAsync(target, VocabularyAudioMode.Normal).ConfigureAwait(false);
    } catch (Exception) {
      // Audio must never block the speaking turn (2D fallback owns the warning).
    }
  }
}
