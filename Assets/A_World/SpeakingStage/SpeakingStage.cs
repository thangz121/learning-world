// A_World/SpeakingStage/SpeakingStage.cs — "Sân Khấu Nhí" (English -> Speaking).
// The child hears a word, sees its picture, and says it into the mic. The
// backend (acoustic engine + optional local whisper service, see
// HybridSpeechAssessmentProvider) decides; the child hears ONE of the four
// frozen levels: Xuất sắc / Đúng rồi / Gần đúng rồi / Thử lại nhé, plus a gentle
// hint. Environment failures (no mic/service dead) never fail the child.
// C# 9.0 only.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class SpeakingStage : MonoBehaviour {
  public enum Phase { Wait, Prompt, SayReady, Listening, Feedback, Done }

  public Phase Current { get; private set; }
  public int Level { get; private set; }
  public int RoundIndex { get; private set; }
  public string TargetWord { get; private set; }
  // The SPOKEN form the child is graded against (id "grape" -> "grapes": the
  // listening pack speaks "grapes" and children learn the plural, so the
  // lexical/acoustic target must be the spoken word, not the vocab id).
  public string SpokenTarget { get; private set; }
  public int AttemptsUsed { get; private set; }
  public int EnvErrors { get; private set; }
  public bool Completed { get; private set; }
  public SpeechLevel LastLevel { get; private set; }
  public bool HasLastLevel { get; private set; }
  public int LastPercent { get; private set; } // internal 0..100 (proxy, dev-facing)
  public SpeakingAssessment LastAssessment { get; private set; }

  SpeakingStageBuilder _builder;
  Transform _player;
  SmartCamera _cam;
  IAudioDirector _audio;
  ActivityLifecycle _life;
  ISpeechRecognizer _speech;
  MicSetupGate _gate;
  PacedVoice _voice;
  Transform _fx;
  float _phaseT;
  bool _waitSaid;
  bool _introSaid;
  float _ringT;
  CancellationTokenSource _cts;
  Task<SpeakingAssessment> _pending;
  bool _pendingReplaySlow;
  float _listenSafetyT;

  public void Build(SpeakingStageBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life, ISpeechRecognizer speech, MicSetupGate gate) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    _speech = speech;
    _gate = gate;
    _voice = new PacedVoice();
    _voice.Audio = audio;
    _voice.LogTag = "SpeakingStage";
    _fx = builder != null ? builder.transform : transform;
    if (_life != null) {
      try {
        if (_life.State == ActivityState.Completed) { Adopt(); return; }
        _life.MarkAvailable("speaking stage offered");
      } catch (Exception) { }
    }
    Level = 1;
    RoundIndex = 0;
    Current = Phase.Wait;
    _phaseT = 0f;
    Follow();
  }

  void Update() { Tick(Time.deltaTime); }

  public void Tick(float dt) {
    if (_voice != null) _voice.Tick(dt);
    if (dt <= 0f || _builder == null) return;
    if (Completed && Current == Phase.Done) return;
    _phaseT += dt;
    if (Current == Phase.Wait) TickWait();
    else if (Current == Phase.Prompt) TickPrompt();
    else if (Current == Phase.SayReady) TickSayReady();
    else if (Current == Phase.Listening) TickListening(dt);
    else if (Current == Phase.Feedback) TickFeedback();
  }

  void TickWait() {
    if (!_waitSaid && _phaseT >= 1.2f) {
      _waitSaid = true;
      Say("Come to the stage!", "Con lên sân khấu nhé!");
      ActivityGuide.PointAt(SpotWorld());
    }
    if (PlayerNear(SpotWorld(), 1.8f) || _phaseT >= 8f) BeginRound();
  }

  public const int SkipGuideThreshold = 10;

  public bool NeedsGuidance(string wordId) {
    if (string.IsNullOrEmpty(wordId)) return false;
    try {
      LocalSave save = new LocalSave();
      PlayerProgress progress = save.Load();
      if (progress != null && progress.Words != null && progress.Words.TryGetValue(wordId, out WordMastery m)) {
        if (m != null && m.SpeakingHit >= SkipGuideThreshold) return false;
      }
    } catch (Exception) { }
    return true;
  }

  void RecordCorrectSpeaking(string wordId) {
    if (string.IsNullOrEmpty(wordId)) return;
    try {
      LocalSave save = new LocalSave();
      PlayerProgress progress = save.Load() ?? new PlayerProgress();
      if (progress.Words == null) progress.Words = new Dictionary<string, WordMastery>();
      WordMastery m;
      if (!progress.Words.TryGetValue(wordId, out m) || m == null) {
        m = new WordMastery { Id = new WordId(wordId), Stage = MasteryStage.Unknown };
        progress.Words[wordId] = m;
      }
      m.SpeakingHit++;
      m.SpeakingTotal++;
      save.Save(progress);
    } catch (Exception) { }
  }

  public void BeginRound() {
    if (Completed) return;
    TargetWord = SpeakingPack.WordFor(Level, RoundIndex);
    SpokenTarget = ListeningWords.TextEn(TargetWord);
    AttemptsUsed = 0;
    if (_builder != null) _builder.ShowPicture(TargetWord);
    ActivityGuide.Clear();
    ActivityFeedback.ProgressKeep(GlobalRound(), SpeakingPack.TotalRounds());
    if (_life != null && _life.State == ActivityState.Available) {
      try { _life.Begin("speaking round"); } catch (Exception) { }
    }

    if (NeedsGuidance(TargetWord) && Application.isPlaying && _builder != null && _builder.GuideNpc != null && _builder.PictureRoot != null) {
      _builder.GuideNpc.PointAt(_builder.PictureRoot.position);
    } else if (_builder != null && _builder.GuideNpc != null) {
      _builder.GuideNpc.StopPointing();
    }

    if (!_introSaid) {
      _introSaid = true;
      Say("Listen, then say it!", "Nghe rồi nói theo nhé!");
      Current = Phase.Prompt;
      _phaseT = -0.9f; // the intro line gets a wider beat
    } else {
      StartPrompt();
    }
  }

  void StartPrompt() {
    Current = Phase.Prompt;
    _phaseT = 0f;
    _pendingReplaySlow = false;
  }

  void TickPrompt() {
    if (_phaseT < 0.9f) return;
    if (!_pendingReplaySlow) ListenPackAudio.Play(_audio, TargetWord, false);
    else ListenPackAudio.Play(_audio, TargetWord, true);
    _pendingReplaySlow = false;
    Current = Phase.SayReady;
    _phaseT = 0f;
  }

  void TickSayReady() {
    if (_phaseT < 0.95f) return;
    if (_builder != null && _builder.GuideNpc != null) _builder.GuideNpc.StopPointing();
    if (_speech == null || (_gate != null && _gate.ShouldSkipListening())) {
      HandleEnvironment();
      return;
    }
    Say("Now you say it!", "Con nói nào!");
    StartListening();
  }

  void StartListening() {
    CancelAttempt();
    if (_builder != null) _builder.SetMicRingActive(true);
    if (_builder != null && _builder.GuideNpc != null) _builder.GuideNpc.PoseListen();
    if (_speech == null) { HandleEnvironment(); return; }
    if (_gate != null && _gate.ShouldSkipListening()) { HandleEnvironment(); return; }
    try {
      _cts = new CancellationTokenSource();
      _pending = _speech.StartAttemptAsync(new WordId(SpokenTarget), _cts.Token);
      Current = Phase.Listening;
      _phaseT = 0f;
      _listenSafetyT = 0f;
    } catch (Exception) {
      HandleEnvironment();
    }
  }

  void TickListening(float dt) {
    _ringT += dt;
    if (_builder != null) _builder.PulseMicRing(_ringT);
    _listenSafetyT += dt;
    if (_listenSafetyT > 25f) { // defensive: the recognizer owns its own timeouts
      CancelAttempt();
      HandleEnvironment();
      return;
    }
    if (_pending == null || !_pending.IsCompleted) return;
    SpeakingAssessment assessment;
    try {
      assessment = _pending.Result;
    } catch (Exception) {
      _pending = null;
      HandleEnvironment();
      return;
    }
    _pending = null;
    HandleAssessment(assessment);
  }

  void HandleAssessment(SpeakingAssessment assessment) {
    LastAssessment = assessment;
    if (_builder != null && _builder.GuideNpc != null) _builder.GuideNpc.StopListening();
    if (assessment.IsEnvironmentError) { HandleEnvironment(); return; }
    if (_builder != null) _builder.SetMicRingActive(false);
    if (!assessment.AttemptDetected) {
      // NoSpeech: heard nothing usable — count it, encourage, retry.
      Say("I can't hear you. Speak up!", "Con nói to hơn nhé!");
      AttemptsUsed++;
      AfterFeedback(AttemptsUsed >= SpeakingPack.AttemptsPerWord
        ? FeedbackAction.GiveUp : FeedbackAction.ReplayNormal);
      return;
    }
    SpeechLevel level = assessment.ToSpeechLevel();
    LastLevel = level;
    HasLastLevel = true;
    LastPercent = Mathf.Clamp(Mathf.RoundToInt(assessment.OverallScore * 100f), 0, 100);
    try {
      Debug.Log("[SpeakingStage] target=" + SpokenTarget + " level=" + level
        + " score=" + LastPercent + "% attempt=" + (AttemptsUsed + 1)
        + " evidence={" + (assessment.Evidence ?? "") + "}");
    } catch (Exception) { }
    string en; string vi;
    SpeakingFeedback.PhraseFor(level, out en, out vi);
    Say(en, vi);
    if (level == SpeechLevel.Perfect || level == SpeechLevel.Great) {
      PlaySfx("success");
      GameJuice.CorrectFx(_fx, _player != null ? _player.position : Vector3.zero, false);
      if (_builder != null) _builder.PlayCorrectFx();
      ActivityFeedback.Correct();
      RecordCorrectSpeaking(TargetWord);
      if (_builder != null && _builder.GuideNpc != null) _builder.GuideNpc.Celebrate();
      AfterFeedback(FeedbackAction.Advance);
      return;
    }
    ActivityFeedback.Retry();
    GameJuice.WrongFx(_builder != null ? _builder.transform : transform, _fx,
      _player != null ? _player.position : Vector3.zero);
    if (_builder != null) _builder.PlayWrongFx();
    AttemptsUsed++;
    if (AttemptsUsed >= SpeakingPack.AttemptsPerWord) {
      AfterFeedback(FeedbackAction.GiveUp);
    } else {
      AfterFeedback(level == SpeechLevel.Almost
        ? FeedbackAction.ReplayNormal : FeedbackAction.ReplaySlow);
    }
  }

  enum FeedbackAction { Advance, ReplayNormal, ReplaySlow, GiveUp, ReturnToRound }

  FeedbackAction _feedbackAction;

  void AfterFeedback(FeedbackAction action) {
    _feedbackAction = action;
    Current = Phase.Feedback;
    _phaseT = 0f;
  }

  void TickFeedback() {
    float waitTarget = _feedbackAction == FeedbackAction.ReturnToRound ? 2.6f : 1.7f;
    if (_phaseT < waitTarget) return;
    switch (_feedbackAction) {
      case FeedbackAction.Advance:
        Advance(true);
        break;
      case FeedbackAction.ReplayNormal:
        Say("Listen again, then say!", "Nghe lại rồi nói nhé!");
        _pendingReplaySlow = false;
        BeginNextBeatPrompt();
        break;
      case FeedbackAction.ReplaySlow:
        Say("Listen again, then say!", "Nghe lại rồi nói nhé!");
        _pendingReplaySlow = true;
        BeginNextBeatPrompt();
        break;
      case FeedbackAction.ReturnToRound:
        BeginRound();
        break;
      default:
        Say("Let's try the next word!", "Mình thử từ khác nhé!");
        Advance(false);
        break;
    }
  }

  // Re-prompt the SAME word (retry within the round).
  void BeginNextBeatPrompt() {
    Current = Phase.Prompt;
    _phaseT = 0f;
  }

  void Advance(bool success) {
    if (_builder != null) _builder.SetMicRingActive(false);
    RoundIndex++;
    if (RoundIndex >= SpeakingPack.RoundsFor(Level)) {
      Level++;
      RoundIndex = 0;
      if (Level > SpeakingPack.LastLevel) { FinishVisit(); return; }
    }
    BeginRound();
  }

  void FinishVisit() {
    Completed = true;
    Current = Phase.Done;
    ActivityGuide.Clear();
    if (_builder != null) _builder.SetMicRingActive(false);
    Say("Well done!", "Giỏi lắm!");
    if (_life != null) {
      try { _life.MarkCompleted("speaking stage done"); } catch (Exception) { }
    }
  }

  // Environment failure (no mic / no service / gate skipped): never the child's
  // fault. One retry, then a kind stop with the exit pointed out.
  void HandleEnvironment() {
    if (_builder != null) _builder.SetMicRingActive(false);
    CancelAttempt();
    EnvErrors++;
    if (EnvErrors >= 2) {
      Say("Let's come back later!", "Mình quay lại sau nhé!");
      ActivityGuide.PointAt(ExitWorld());
      Current = Phase.Done; // Completed stays false: the visit can be retried
      return;
    }
    Say("No microphone yet.", "Chưa có micro.");
    Current = Phase.Feedback;
    _feedbackAction = FeedbackAction.ReturnToRound;
    _phaseT = 0f;
  }

  // Reuse the Feedback timer for the environment retry (no new action enum value
  // needed elsewhere): handled before the generic switch.
  void CancelAttempt() {
    try {
      if (_cts != null) { _cts.Cancel(); _cts.Dispose(); }
    } catch (Exception) { }
    _cts = null;
    _pending = null;
  }

  void Adopt() {
    Completed = true;
    Current = Phase.Done;
    Level = SpeakingPack.LastLevel;
  }

  public void SetLevelForTests(int level, int round) {
    Level = Mathf.Clamp(level, 1, SpeakingPack.LastLevel);
    RoundIndex = Mathf.Max(0, round);
    BeginRound();
  }

  int GlobalRound() { return (Level - 1) * SpeakingPack.RoundsPerLevel + RoundIndex; }

  void Say(string en, string vi) {
    if (_voice == null) return;
    _voice.Speak(DialogueLang.T(en, vi));
  }

  void PlaySfx(string id) {
    if (_audio == null) return;
    try { _audio.PlaySfx(new SfxId(id)); } catch (Exception) { }
  }

  void Follow() {
    if (_cam == null || _player == null) return;
    try { _cam.Follow(_player, SpeakingStageBuilder.FollowOffset); } catch (Exception) { }
  }

  bool PlayerNear(Vector3 world, float r) {
    if (_player == null) return true;
    Vector3 d = _player.position - world;
    d.y = 0f;
    return d.sqrMagnitude <= r * r;
  }

  Vector3 SpotWorld() {
    return _builder != null
      ? _builder.transform.TransformPoint(SpeakingStageBuilder.PlaySpotLocal)
      : SpeakingStageBuilder.PlaySpotLocal;
  }

  Vector3 ExitWorld() {
    return _builder != null
      ? _builder.transform.TransformPoint(SpeakingStageBuilder.ExitLocal)
      : SpeakingStageBuilder.ExitLocal;
  }

  void OnDisable() { CancelAttempt(); }
}
