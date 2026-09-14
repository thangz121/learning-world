// CT-P12: Phase 2.1 speech foundation — deterministic EditMode suite (no hardware).
// Covers: lexical matching (§29/§44), policy decisions (§6/§71), score honesty
// (§7/§69/§87), mic lifecycle (§11-13), recognizer lifecycle incl. stale/timeout
// (§10/§48), exercise data-driven + bounded retry + no-mic policy (§16/17/35/57),
// learning/quest integration without frozen changes (§36/§37), provider honesty.
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

public class CT_P12_SpeechFoundation {
  static readonly WordId Ball = new WordId("ball");
  static readonly WordId Apple = new WordId("apple");

  // ---------- helpers ----------

  sealed class ScriptedMic : IMicrophoneDevice {
    public event Action<MicStatus> StatusChanged;
    public MicStatus Status { get; private set; }
    public string SelectedDevice => Status == MicStatus.Ready ? "test-mic" : null;
    public string[] Devices => Status == MicStatus.Ready ? new[] { "test-mic" } : new string[0];
    public SpeechCapability Capability => new SpeechCapability { Status = Status, DeviceName = SelectedDevice ?? "", DeviceCount = Devices.Length };
    public ScriptedMic(MicStatus s) { Status = s; }
    public void Set(MicStatus s) { Status = s; try { StatusChanged?.Invoke(s); } catch (Exception) { } }
    public void Refresh() { }
    public void ReportCaptureFailure() { Set(MicStatus.Error); }
  }

  static CapturedSpeech OkSegment(float energy) {
    return new CapturedSpeech {
      Samples = new float[1600], SampleRate = 16000, Channels = 1,
      DurationSec = 0.8f, MeanEnergy = energy, PeakEnergy = energy * 2f,
      VoicedSec = 0.6f, TimedOut = false, Cancelled = false, Error = string.Empty
    };
  }

  sealed class BlockingProvider : ISpeechAssessmentProvider {
    public string ProviderId => "blocking";
    public bool ProvidesTranscript => true;
    public bool ProvidesPhonemeEvidence => false;
    public bool RequiresNetwork => false;
    readonly TaskCompletionSource<SpeechRecognitionResult> _tcs = new TaskCompletionSource<SpeechRecognitionResult>();
    public Task<SpeechRecognitionResult> RecognizeAsync(CapturedSpeech audio, WordId target, CancellationToken ct) {
      return _tcs.Task;
    }
    public void Resolve(SpeechRecognitionResult r) { _tcs.TrySetResult(r); }
  }

  sealed class HangingProvider : ISpeechAssessmentProvider {
    public string ProviderId => "hanging";
    public bool ProvidesTranscript => true;
    public bool ProvidesPhonemeEvidence => false;
    public bool RequiresNetwork => false;
    public Task<SpeechRecognitionResult> RecognizeAsync(CapturedSpeech audio, WordId target, CancellationToken ct) {
      return new TaskCompletionSource<SpeechRecognitionResult>().Task; // never completes
    }
  }

  static SpeechRecognizer NewRecognizer(IMicrophoneDevice mic, ISpeechAssessmentProvider provider, IGameEventBus bus) {
    return new SpeechRecognizer(mic, new FakeSpeechCapture(() => OkSegment(0.05f)), provider,
      SpeakingPassPolicy.Default(), bus, 8f, 3f, 2f);
  }

  static SpeakingAssessment Run(SpeechRecognizer r, WordId target) {
    return r.StartAttemptAsync(target, CancellationToken.None).GetAwaiter().GetResult();
  }

  // ---------- A. lexical matching ----------

  [Test] public void P12A_ExactMatch() {
    Assert.AreEqual(1f, LexicalMatcher.MatchScore("ball", "ball"));
  }
  [Test] public void P12A_PhraseContainsTarget() {
    Assert.AreEqual(1f, LexicalMatcher.MatchScore("the ball", "ball"));
    Assert.AreEqual(1f, LexicalMatcher.MatchScore("ball please", "ball"));
    Assert.AreEqual(1f, LexicalMatcher.MatchScore("ball ball", "ball"));
    Assert.IsTrue(LexicalMatcher.ContainsTargetWord("the ball", "ball"));
  }
  [Test] public void P12A_PartialOrdering() {
    float ba = LexicalMatcher.MatchScore("ba", "ball");
    float bal = LexicalMatcher.MatchScore("bal", "ball");
    float bol = LexicalMatcher.MatchScore("bol", "ball");
    Assert.Greater(bal, ba, "bal is closer to ball than ba");
    Assert.Greater(bal, bol, "bal is closer to ball than bol");
    Assert.GreaterOrEqual(ba, 0.4f, "ba keeps partial-level resemblance");
    Assert.Less(bal, 1f, "partial is not a full match");
  }
  [Test] public void P12A_WrongWordLow() {
    Assert.Less(LexicalMatcher.MatchScore("apple", "ball"), 0.4f);
    Assert.IsFalse(LexicalMatcher.ContainsTargetWord("apple", "ball"));
  }
  [Test] public void P12A_VietnameseNoCrashLowScore() {
    float s = LexicalMatcher.MatchScore("quả bóng", "ball");
    Assert.GreaterOrEqual(s, 0f);
    Assert.Less(s, 0.4f, "Vietnamese label is not an English target match (no semantic claim)");
    Assert.AreEqual(0f, LexicalMatcher.MatchScore("", "ball"));
    Assert.AreEqual(1f, LexicalMatcher.MatchScore("BALL", "ball"), "case-insensitive");
  }

  // ---------- B. policy decisions ----------

  static SpeechRecognitionResult Reco(string transcript, float conf, bool speech, float energy) {
    return new SpeechRecognitionResult {
      Transcript = transcript, RecognitionConfidence = conf, HasSpeech = speech,
      AudioDurationSec = 0.8f, SpeechDurationSec = 0.6f, MeanEnergy = energy,
      ProviderId = "fake", LatencyMs = 5, ErrorReason = "", IsError = false,
      Pronunciation = PronunciationEvidence.None()
    };
  }

  [Test] public void P12B_CorrectWordPasses() {
    // conf 0.6 sits in the Pass band (Strong needs conf>=0.75 + lexical>=0.95).
    var a = SpeakingPassPolicy.Default().Decide(Reco("ball", 0.6f, true, 0.05f), Ball, "spk-1");
    Assert.AreEqual(SpeakingDecision.Pass, a.Decision);
    Assert.IsTrue(a.AttemptDetected);
    Assert.IsFalse(a.IsEnvironmentError);
  }
  [Test] public void P12B_HighConfidenceStrongPass() {
    var a = SpeakingPassPolicy.Default().Decide(Reco("ball", 0.95f, true, 0.05f), Ball, "spk-1");
    Assert.AreEqual(SpeakingDecision.StrongPass, a.Decision);
    Assert.AreEqual(SpeechLevel.Perfect, a.ToSpeechLevel());
  }
  [Test] public void P12B_PhrasePasses() {
    var a = SpeakingPassPolicy.Default().Decide(Reco("the ball", 0.6f, true, 0.05f), Ball, "spk-1");
    Assert.AreEqual(SpeakingDecision.Pass, a.Decision, "phrase containing target is not WRONG_WORD");
  }
  [Test] public void P12B_PartialAttempt() {
    var a = SpeakingPassPolicy.Default().Decide(Reco("ba", 0.6f, true, 0.04f), Ball, "spk-1");
    Assert.AreEqual(SpeakingDecision.Partial, a.Decision, "ba is a credible attempt, not a fail");
    Assert.IsTrue(a.AttemptDetected);
    Assert.IsFalse(a.IsEnvironmentError);
    Assert.AreEqual(SpeechLevel.Almost, a.ToSpeechLevel(), "partial never advances Speak (frozen rule)");
  }
  [Test] public void P12B_WrongWord() {
    var a = SpeakingPassPolicy.Default().Decide(Reco("apple", 0.88f, true, 0.05f), Ball, "spk-1");
    Assert.AreEqual(SpeakingDecision.WrongWord, a.Decision);
    Assert.IsFalse(a.IsEnvironmentError);
  }
  [Test] public void P12B_SilenceNoSpeech() {
    var a = SpeakingPassPolicy.Default().Decide(Reco("", 0f, false, 0f), Ball, "spk-1");
    Assert.AreEqual(SpeakingDecision.NoSpeech, a.Decision);
    Assert.IsFalse(a.AttemptDetected);
  }
  [Test] public void P12B_QuietTooWeak() {
    var a = SpeakingPassPolicy.Default().Decide(Reco("", 0f, true, 0.002f), Ball, "spk-1");
    Assert.AreEqual(SpeakingDecision.TooWeak, a.Decision);
    Assert.IsTrue(a.AttemptDetected, "quiet voice is still an attempt, not silence");
  }
  [Test] public void P12B_LowConfidenceUnclear() {
    // "buh" keeps mid similarity (0.25) with low confidence -> UNCLEAR, not WRONG_WORD.
    var a = SpeakingPassPolicy.Default().Decide(Reco("buh", 0.12f, true, 0.02f), Ball, "spk-1");
    Assert.AreEqual(SpeakingDecision.Unclear, a.Decision, "low confidence is UNCLEAR, not WRONG_WORD");
  }
  [Test] public void P12B_SpeechWithoutTranscriptPossible() {
    var a = SpeakingPassPolicy.Default().Decide(Reco("", 0f, true, 0.05f), Ball, "spk-1");
    Assert.AreEqual(SpeakingDecision.PossibleAttempt, a.Decision);
    Assert.IsTrue(float.IsNaN(a.LexicalMatchScore));
  }
  [Test] public void P12B_ProviderErrorIsEnvironment() {
    var r = Reco("", 0f, false, 0f);
    r.IsError = true; r.ErrorReason = SpeechFailureReasons.ProviderError;
    var a = SpeakingPassPolicy.Default().Decide(r, Ball, "spk-1");
    Assert.AreEqual(SpeakingDecision.Unclear, a.Decision);
    Assert.IsTrue(a.IsEnvironmentError, "server failure is never child failure");
  }
  [Test] public void P12B_ThresholdsConfigurable() {
    var strict = SpeakingPassPolicy.Default();
    strict.PartialLexicalFloor = 0.9f;
    var a = strict.Decide(Reco("ba", 0.6f, true, 0.04f), Ball, "spk-1");
    Assert.AreEqual(SpeakingDecision.WrongWord, a.Decision, "policy is data, not instinct");
  }

  // ---------- C. score honesty ----------

  [Test] public void P12C_TranscriptOnlyHasNoPronunciation() {
    var a = SpeakingPassPolicy.Default().Decide(Reco("ball", 0.9f, true, 0.05f), Ball, "spk-1");
    Assert.IsFalse(a.HasPronunciationEvidence);
    Assert.IsTrue(float.IsNaN(a.PronunciationScore), "never manufacture pronunciation from transcript");
    Assert.IsTrue(a.IntelligibilityIsProxy, "human intelligibility NOT proven");
  }
  [Test] public void P12C_PhonemeEvidenceFlowsWhenPresent() {
    var r = Reco("ball", 0.9f, true, 0.05f);
    r.Pronunciation = new PronunciationEvidence {
      HasPhonemeData = true, Source = "azure", AccuracyScore = 0.92f,
      FluencyScore = 0.9f, CompletenessScore = 1f, WordAccuracy = 0.9f,
      WordErrorType = "None", PhonemeCount = 3
    };
    var a = SpeakingPassPolicy.Default().Decide(r, Ball, "spk-1");
    Assert.IsTrue(a.HasPronunciationEvidence);
    Assert.AreEqual(0.92f, a.PronunciationScore, 0.001f);
  }
  [Test] public void P12C_LowPronunciationDowngradesLexicalPass() {
    var r = Reco("ball", 0.9f, true, 0.05f);
    r.Pronunciation = new PronunciationEvidence {
      HasPhonemeData = true, Source = "azure", AccuracyScore = 0.2f,
      FluencyScore = 0.5f, CompletenessScore = 1f, WordAccuracy = 0.2f,
      WordErrorType = "Mispronunciation", PhonemeCount = 3
    };
    var a = SpeakingPassPolicy.Default().Decide(r, Ball, "spk-1");
    Assert.AreEqual(SpeakingDecision.Partial, a.Decision, "acoustic evidence can only temper, transcript alone never perfects");
  }

  // ---------- D. mic lifecycle ----------

  [Test] public void P12D_NoDeviceAtStartup() {
    var mic = new MicrophoneDeviceService(() => new string[0], null);
    Assert.AreEqual(MicStatus.NoDevice, mic.Status);
    Assert.IsFalse(mic.Capability.IsAvailable());
  }
  [Test] public void P12D_DeviceEnumeratedAndSelected() {
    var mic = new MicrophoneDeviceService(() => new[] { "mic-a", "mic-b" }, null);
    Assert.AreEqual(MicStatus.Ready, mic.Status);
    Assert.AreEqual("mic-a", mic.SelectedDevice);
  }
  [Test] public void P12D_HotPlugDisconnectReconnect() {
    string[] list = { "mic-a" };
    var mic = new MicrophoneDeviceService(() => list, null);
    Assert.AreEqual(MicStatus.Ready, mic.Status);
    list = new string[0];
    mic.Refresh();
    Assert.AreEqual(MicStatus.NoDevice, mic.Status, "unplug -> gracefully unavailable");
    list = new[] { "mic-a" };
    MicStatus changed = MicStatus.Unknown;
    mic.StatusChanged += s => changed = s;
    mic.Refresh();
    Assert.AreEqual(MicStatus.Ready, mic.Status, "replug -> available without restart");
    Assert.AreEqual(MicStatus.Ready, changed);
  }
  [Test] public void P12D_PermissionDeniedDistinct() {
    var mic = new MicrophoneDeviceService(() => new[] { "mic-a" }, name => false);
    Assert.AreEqual(MicStatus.PermissionDenied, mic.Status);
  }
  [Test] public void P12D_CaptureFailureMarksError() {
    var mic = new MicrophoneDeviceService(() => new[] { "mic-a" }, null);
    mic.ReportCaptureFailure();
    Assert.AreEqual(MicStatus.Error, mic.Status, "listed device that cannot capture is Error, not Ready");
  }

  // ---------- E. recognizer ----------

  [Test] public void P12E_NoMicShortCircuitsWithoutProviderCall() {
    var mic = new ScriptedMic(MicStatus.NoDevice);
    var provider = new FakeSpeechProvider(FakeSpeechProvider.Outcome.Pass);
    var r = NewRecognizer(mic, provider, null);
    var a = Run(r, Ball);
    Assert.AreEqual(0, provider.Calls, "no capture, no upload without a microphone");
    Assert.IsTrue(a.IsEnvironmentError);
    Assert.AreEqual(SpeechFailureReasons.MicUnavailable, a.FailureReason);
  }
  [Test] public void P12E_PassFlowPublishesEvents() {
    var bus = new GameEventBus();
    var mic = new ScriptedMic(MicStatus.Ready);
    var r = NewRecognizer(mic, new FakeSpeechProvider(FakeSpeechProvider.Outcome.Pass), bus);
    SpeechAttemptedEvent? attempted = null;
    SpeechRecognizedEvent? recognized = null;
    SpeechAssessedEvent? assessed = null;
    bus.Subscribe<SpeechAttemptedEvent>(e => attempted = e);
    bus.Subscribe<SpeechRecognizedEvent>(e => recognized = e);
    bus.Subscribe<SpeechAssessedEvent>(e => assessed = e);
    var a = Run(r, Ball);
    Assert.AreEqual(SpeakingDecision.StrongPass, a.Decision, "clear 'ball' at 0.9 conf is Strong (lexical+confidence verdict)");
    Assert.IsTrue(attempted.HasValue && attempted.Value.AttemptId == a.AttemptId);
    Assert.IsTrue(recognized.HasValue && recognized.Value.Transcript == "ball");
    Assert.IsTrue(assessed.HasValue && assessed.Value.Assessment.Decision == SpeakingDecision.StrongPass);
  }
  [Test] public void P12E_StaleResultCannotAffectNewAttempt() {
    var bus = new GameEventBus();
    var mic = new ScriptedMic(MicStatus.Ready);
    var blocking = new BlockingProvider();
    var r = NewRecognizer(mic, blocking, bus);
    int assessedCount = 0;
    string lastId = null;
    bus.Subscribe<SpeechAssessedEvent>(e => { assessedCount++; lastId = e.AttemptId; });
    Task<SpeakingAssessment> taskA = r.StartAttemptAsync(Ball, CancellationToken.None);
    r.Cancel(); // attempt A dies here
    r.SwitchProvider(new FakeSpeechProvider(FakeSpeechProvider.Outcome.Pass));
    var b = Run(r, Ball);
    Assert.AreEqual(SpeakingDecision.StrongPass, b.Decision);
    // Late A resolves: must be dropped, never published.
    blocking.Resolve(Reco("apple", 0.9f, true, 0.05f));
    try { taskA.GetAwaiter().GetResult(); } catch (Exception) { }
    Assert.AreEqual(1, assessedCount, "stale A must not publish");
    Assert.AreEqual(b.AttemptId, lastId);
  }
  [Test] public void P12E_ProviderTimeoutIsEnvironment() {
    var mic = new ScriptedMic(MicStatus.Ready);
    var r = new SpeechRecognizer(mic, new FakeSpeechCapture(() => OkSegment(0.05f)),
      new HangingProvider(), SpeakingPassPolicy.Default(), null, 8f, 3f, 0.2f);
    var a = Run(r, Ball);
    Assert.IsTrue(a.IsEnvironmentError);
    Assert.AreEqual(SpeechFailureReasons.Timeout, a.FailureReason);
    Assert.AreNotEqual(SpeakingDecision.WrongWord, a.Decision, "timeout is never Wrong");
  }
  [Test] public void P12E_CancelledTokenThrowsNoAssessment() {
    var mic = new ScriptedMic(MicStatus.Ready);
    var r = NewRecognizer(mic, new FakeSpeechProvider(FakeSpeechProvider.Outcome.Pass), null);
    var cts = new CancellationTokenSource();
    cts.Cancel();
    Assert.Throws<OperationCanceledException>(() => {
      r.StartAttemptAsync(Ball, cts.Token).GetAwaiter().GetResult();
    });
  }
  [Test] public void P12E_DeveloperLogLineHasRequiredFields() {
    var a = SpeakingPassPolicy.Default().Decide(Reco("ball", 0.9f, true, 0.05f), Ball, "spk-7");
    Assert.IsTrue(a.Evidence.Contains("target=ball"));
    Assert.IsTrue(a.Evidence.Contains("conf="));
    Assert.IsTrue(a.Evidence.Contains("lexical="));
    Assert.IsTrue(a.Evidence.Contains("provider="));
  }

  // ---------- F. exercise ----------

  static SpeakingExerciseConfig ExerciseFor(string word, int attempts) {
    var c = SpeakingExerciseConfig.DefaultFor(new WordId(word));
    c.attemptsAllowed = attempts;
    return c;
  }

  [Test] public void P12F_NoMicSkipsWithoutLearningTrace() {
    var bus = new GameEventBus();
    var mic = new ScriptedMic(MicStatus.NoDevice);
    var r = NewRecognizer(mic, new FakeSpeechProvider(FakeSpeechProvider.Outcome.Pass), bus);
    bool spoken = false;
    bus.Subscribe<WordSpokenEvent>(e => spoken = true);
    var runner = new SpeakingExerciseRunner(r, null, bus);
    var result = runner.RunAsync(ExerciseFor("ball", 3), CancellationToken.None).GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.SkippedNoMic, result.Outcome);
    Assert.AreEqual(0, result.AttemptsUsed, "no microphone: no attempts consumed");
    Assert.IsFalse(spoken, "environment condition publishes no learning evidence");
  }
  [Test] public void P12F_PassPublishesSpokenAndCompletes() {
    var bus = new GameEventBus();
    var mic = new ScriptedMic(MicStatus.Ready);
    var r = NewRecognizer(mic, new FakeSpeechProvider(FakeSpeechProvider.Outcome.Pass), bus);
    WordSpokenEvent? spoken = null;
    bus.Subscribe<WordSpokenEvent>(e => spoken = e);
    var runner = new SpeakingExerciseRunner(r, null, bus);
    var result = runner.RunAsync(ExerciseFor("ball", 3), CancellationToken.None).GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.Passed, result.Outcome);
    Assert.AreEqual(1, result.AttemptsUsed);
    Assert.AreEqual("Great!", result.ChildMessage);
    Assert.IsTrue(spoken.HasValue);
    Assert.AreEqual(SpeechLevel.Perfect, spoken.Value.Result.Level, "clear pass maps to Perfect (advances frozen Speak)");
  }
  [Test] public void P12F_RetryBoundedWrongWord() {
    var bus = new GameEventBus();
    var mic = new ScriptedMic(MicStatus.Ready);
    var r = NewRecognizer(mic, new FakeSpeechProvider(FakeSpeechProvider.Outcome.WrongWord), bus);
    var runner = new SpeakingExerciseRunner(r, null, bus);
    var result = runner.RunAsync(ExerciseFor("ball", 3), CancellationToken.None).GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.NotPassed, result.Outcome);
    Assert.AreEqual(3, result.AttemptsUsed, "bounded retry, never infinite");
    Assert.AreEqual("Listen and try.", result.ChildMessage);
  }
  [Test] public void P12F_PartialCompletesAsPartial() {
    var bus = new GameEventBus();
    var mic = new ScriptedMic(MicStatus.Ready);
    var r = NewRecognizer(mic, new FakeSpeechProvider(FakeSpeechProvider.Outcome.Partial), bus);
    var runner = new SpeakingExerciseRunner(r, null, bus);
    var result = runner.RunAsync(ExerciseFor("ball", 1), CancellationToken.None).GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.PartialComplete, result.Outcome);
  }
  [Test] public void P12F_ExerciseIsDataDriven() {
    // Same code path, different target from DATA: apple wrong-word fake says "ball".
    var bus = new GameEventBus();
    var mic = new ScriptedMic(MicStatus.Ready);
    var r = NewRecognizer(mic, new FakeSpeechProvider(FakeSpeechProvider.Outcome.WrongWord), bus);
    var runner = new SpeakingExerciseRunner(r, null, bus);
    var result = runner.RunAsync(ExerciseFor("apple", 1), CancellationToken.None).GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.NotPassed, result.Outcome);
    Assert.AreEqual("apple", result.Config.targetWord);
  }
  [Test] public void P12F_SpeakingConfigLoadsFromJson() {
    // Proves the target ships as data: parse substitute JSON for a NEW word.
    string json = "{\"id\":\"say_red\",\"targetWord\":\"red\",\"questId\":\"\",\"attemptsAllowed\":2,"
      + "\"minDecisionForPass\":\"pass\",\"replayPromptOnRetry\":true,\"promptLine\":\"Can you say red?\","
      + "\"successLine\":\"Great!\",\"retryLine\":\"Try again.\",\"noSpeechLine\":\"Can you say it?\","
      + "\"wrongLine\":\"Listen and try.\",\"unclearLine\":\"Let's try again.\","
      + "\"micMissingLine\":\"Microphone not connected.\",\"errorLine\":\"Let's try again.\"}";
    var config = UnityEngine.JsonUtility.FromJson<SpeakingExerciseConfig>(json);
    Assert.AreEqual("red", config.targetWord);
    Assert.AreEqual(2, config.BoundedAttempts());
    Assert.AreEqual("red", config.TargetWordId().Value, "new word, zero code changes");
  }
  [Test] public void P12F_ChildMessagesNeverTechnical() {
    var c = SpeakingExerciseConfig.DefaultFor(Ball);
    Assert.AreEqual("Great!", c.ChildMessage(SpeakingDecision.StrongPass));
    Assert.AreEqual("Can you say it?", c.ChildMessage(SpeakingDecision.NoSpeech));
    Assert.AreEqual("Can you say it?", c.ChildMessage(SpeakingDecision.TooWeak));
    Assert.AreEqual("Listen and try.", c.ChildMessage(SpeakingDecision.WrongWord));
    StringAssert.DoesNotContain("%", c.ChildMessage(SpeakingDecision.Pass));
    StringAssert.DoesNotContain("62", c.ChildMessage(SpeakingDecision.Partial));
  }
  [Test] public void P12F_DeferralMarksAndReleases() {
    var d = new SpeechQuestDeferral();
    var q = new QuestId("w1_mia_ball");
    d.MarkDeferred(q);
    Assert.IsTrue(d.IsDeferred(q));
    string[] eligible = d.EligibleAfterReconnect(new SpeechCapability { Status = MicStatus.Ready });
    Assert.AreEqual(1, eligible.Length, "reconnect restores eligibility, never auto-starts");
    d.Clear(q);
    Assert.IsFalse(d.IsDeferred(q));
    string[] none = d.EligibleAfterReconnect(new SpeechCapability { Status = MicStatus.NoDevice });
    Assert.AreEqual(0, none.Length);
  }

  // ---------- G. learning + quest integration (frozen systems untouched) ----------

  [Test] public void P12G_SpeakingSuccessCompletesSpeakObjectiveViaExistingGlue() {
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var quests = new QuestManager(bus, learning, hints); // built-in: market_help_mia ends with speak_apple
    // Production glue (MarketBootstrap pattern): WordSpoken -> AdvanceOnSpoken.
    bus.Subscribe<WordSpokenEvent>(e => quests.AdvanceOnSpoken(e.WordId, e.Result.Level));
    var q = new QuestId("market_help_mia");
    quests.StartQuest(q);
    quests.AdvanceOnSeen(Apple);
    quests.ReportAction(PlayerAction.Bring, Apple);
    Assert.AreEqual(2, quests.GetState(q).ObjectiveIndex);
    var mic = new ScriptedMic(MicStatus.Ready);
    var r = NewRecognizer(mic, new FakeSpeechProvider(FakeSpeechProvider.Outcome.Pass), bus);
    var runner = new SpeakingExerciseRunner(r, null, bus);
    var result = runner.RunAsync(ExerciseFor("apple", 3), CancellationToken.None).GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.Passed, result.Outcome);
    Assert.IsTrue(quests.GetState(q).Completed, "Pass flows through the frozen Speak path");
  }
  [Test] public void P12G_FailedSpeechNeverFailsQuestOrMastery() {
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var quests = new QuestManager(bus, learning, hints);
    bus.Subscribe<WordSpokenEvent>(e => quests.AdvanceOnSpoken(e.WordId, e.Result.Level));
    var q = new QuestId("market_help_mia");
    quests.StartQuest(q);
    quests.AdvanceOnSeen(Apple);
    quests.ReportAction(PlayerAction.Bring, Apple);
    int totalBefore = learning.GetMastery(Apple).SpeakingTotal;
    var mic = new ScriptedMic(MicStatus.Ready);
    var r = NewRecognizer(mic, new FakeSpeechProvider(FakeSpeechProvider.Outcome.WrongWord), bus);
    var runner = new SpeakingExerciseRunner(r, null, bus);
    var result = runner.RunAsync(ExerciseFor("apple", 2), CancellationToken.None).GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.NotPassed, result.Outcome);
    Assert.IsFalse(quests.GetState(q).Completed, "quest stays OPEN (not failed)");
    Assert.AreEqual(2, quests.GetState(q).ObjectiveIndex, "no objective regression");
    Assert.GreaterOrEqual(learning.GetMastery(Apple).SpeakingTotal, totalBefore,
      "attempts are evidence; nothing decrements mastery");
  }
  [Test] public void P12G_NoMicLeavesMasteryUntouched() {
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var mic = new ScriptedMic(MicStatus.NoDevice);
    var r = NewRecognizer(mic, new FakeSpeechProvider(FakeSpeechProvider.Outcome.Pass), bus);
    bus.Subscribe<SpeechAssessedEvent>(e => {
      if (!e.Assessment.IsEnvironmentError)
        learning.ReportSpoken(e.Target, e.Assessment.ToSpeechLevel(), LearnSource.Quest);
    });
    Run(r, Ball);
    Assert.AreEqual(0, learning.GetMastery(Ball).SpeakingTotal,
      "mic-unavailable must not count as a speaking attempt");
  }

  // ---------- H. provider honesty ----------

  [Test] public void P12H_AzureUnconfiguredIsHonestError() {
    var azure = new AzureSpeechAssessmentProvider(() => null);
    Assert.IsFalse(azure.IsConfigured());
    var r = azure.RecognizeAsync(OkSegment(0.05f), Ball, CancellationToken.None).GetAwaiter().GetResult();
    Assert.IsTrue(r.IsError);
    Assert.IsFalse(r.Pronunciation.HasPhonemeData, "no fake phonemes, ever");
    Assert.IsTrue(string.IsNullOrEmpty(r.Transcript));
  }
  [Test] public void P12H_LocalProviderDetectsSpeechWithoutFakingTranscript() {
    var local = new LocalSpeechProvider();
    var loud = OkSegment(0.05f);
    loud.Samples = new float[1600];
    for (int i = 0; i < loud.Samples.Length; i++) loud.Samples[i] = 0.05f;
    var r = local.RecognizeAsync(loud, Ball, CancellationToken.None).GetAwaiter().GetResult();
    Assert.IsTrue(r.HasSpeech);
    Assert.IsTrue(string.IsNullOrEmpty(r.Transcript), "VAD evidence only — transcript NOT AVAILABLE");
    Assert.IsFalse(r.Pronunciation.HasPhonemeData);
    var silent = OkSegment(0f);
    silent.Samples = new float[1600];
    var s = local.RecognizeAsync(silent, Ball, CancellationToken.None).GetAwaiter().GetResult();
    Assert.IsFalse(s.HasSpeech);
  }
  [Test] public void P12H_FakeTimeoutAndErrorOutcomes() {
    var mic = new ScriptedMic(MicStatus.Ready);
    var err = Run(NewRecognizer(mic, new FakeSpeechProvider(FakeSpeechProvider.Outcome.Error), null), Ball);
    Assert.IsTrue(err.IsEnvironmentError);
    var to = Run(NewRecognizer(mic, new FakeSpeechProvider(FakeSpeechProvider.Outcome.Timeout), null), Ball);
    Assert.AreEqual(SpeechFailureReasons.Timeout, to.FailureReason);
  }
  [Test] public void P12H_PhraseTranscriptOutcome() {
    var mic = new ScriptedMic(MicStatus.Ready);
    var a = Run(NewRecognizer(mic, new FakeSpeechProvider(FakeSpeechProvider.Outcome.TranscriptOnly), null), Ball);
    Assert.AreEqual(SpeakingDecision.StrongPass, a.Decision, "\"the ball please\" contains the target at high confidence");
  }
}
