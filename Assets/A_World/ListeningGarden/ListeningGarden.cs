// A_World/ListeningGarden/ListeningGarden.cs — "Vườn Tai Thính"
// (English -> Listening). Hear one English word, tap the matching picture.
// Voice-first: no text for the child; the bell replays; a wrong tap replays
// the word in slow mode. Starts at LV1, one visit plays through LV4.
// C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ListeningGarden : MonoBehaviour {
  public enum Phase { Wait, Play, Done }

  public Phase Current { get; private set; }
  public int Level { get; private set; }
  public int RoundIndex { get; private set; }
  public bool Completed { get; private set; }
  public int WrongCount { get; private set; }
  public bool LastCorrect { get; private set; }
  public string TargetWord { get; private set; }
  public int ChoiceCount { get { return _builder != null ? _builder.Choices.Count : 0; } }

  ListeningGardenBuilder _builder;
  Transform _player;
  SmartCamera _cam;
  IAudioDirector _audio;
  ActivityLifecycle _life;
  PacedVoice _voice;
  Transform _fx;
  float _phaseT;
  bool _waitSaid;
  float _promptT;
  float _restT;
  float _slowReplayT;
  float _idleT;
  bool _finishAfterRest;
  bool _introSaid;

  public void Build(ListeningGardenBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    _voice = new PacedVoice();
    _voice.Audio = audio;
    _voice.LogTag = "ListeningGarden";
    _fx = builder != null ? builder.transform : transform;
    BindStatic();
    if (_life != null) {
      try {
        if (_life.State == ActivityState.Completed) { Adopt(); return; }
        _life.MarkAvailable("listening garden offered");
      } catch (Exception) { }
    }
    Level = ListeningRounds.FirstLevel;
    RoundIndex = 0;
    Current = Phase.Wait;
    _phaseT = 0f;
    Follow();
  }

  void BindStatic() {
    if (_builder == null || _builder.Bell == null) return;
    _builder.Bell.Game = this;
  }

  void Update() { Tick(Time.deltaTime); }

  public void Tick(float dt) {
    if (_voice != null) _voice.Tick(dt);
    if (_builder != null && _builder.Bell != null) {
      bool shouldRetract = Current == Phase.Play || (Current == Phase.Wait && PlayerNear(SpotWorld(), 2.0f));
      _builder.Bell.SetRetracted(shouldRetract);
      _builder.Bell.Tick(dt);
    }
    if (dt <= 0f || _builder == null) return;
    if (_slowReplayT > 0f) {
      _slowReplayT -= dt;
      if (_slowReplayT <= 0f) PlayWord(true);
    }
    if (Completed && Current == Phase.Done) return;
    if (_restT > 0f) {
      _restT -= dt;
      if (_restT <= 0f) AfterRest();
      return;
    }
    if (_promptT > 0f) {
      _promptT -= dt;
      if (_promptT <= 0f) PlayWord(false);
    }
    _phaseT += dt;
    if (Current == Phase.Wait) TickWait(dt);
    else if (Current == Phase.Play) {
      if (_isGuiding) TickGuidance(dt);
      _idleT += dt;
      if (_idleT >= 8f && _idleT - dt < 8f) PointTask();
      else if (_idleT >= 15f && _idleT - dt < 15f) PointTask();
    }
  }

  void TickWait(float dt) {
    if (!_waitSaid && _phaseT >= 1.2f) {
      _waitSaid = true;
      Say("Come to the listening garden!", "Con ra vườn nghe nhé!");
      ActivityGuide.PointAt(SpotWorld());
    }
    if (PlayerNear(SpotWorld(), 1.5f) || _phaseT >= 8f) BeginRound();
  }

  public const int SkipGuideThreshold = 10;

  int _guideStep = -1;
  float _guideStepT = 0f;
  bool _isGuiding = false;

  public bool NeedsGuidance(string wordId) {
    if (string.IsNullOrEmpty(wordId)) return false;
    try {
      LocalSave save = new LocalSave();
      PlayerProgress progress = save.Load();
      if (progress != null && progress.Words != null && progress.Words.TryGetValue(wordId, out WordMastery m)) {
        if (m != null && m.RecognitionHit >= SkipGuideThreshold) return false;
      }
    } catch (Exception) { }
    return true;
  }

  void RecordCorrectAnswer(string wordId) {
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
      m.RecognitionHit++;
      m.RecognitionTotal++;
      save.Save(progress);
    } catch (Exception) { }
  }

  void TickGuidance(float dt) {
    if (!_isGuiding) return;
    _guideStepT += dt;
    if (_builder == null || _builder.Choices.Count == 0) {
      FinishGuidance();
      return;
    }

    if (_guideStep < _builder.Choices.Count) {
      if (_guideStepT >= 1.6f) {
        _guideStep++;
        _guideStepT = 0f;
        if (_guideStep < _builder.Choices.Count) {
          ListeningGardenChoice pod = _builder.Choices[_guideStep];
          if (pod != null) {
            if (_builder.GuideNpc != null) _builder.GuideNpc.PointAt(pod.transform.position);
            ListenPackAudio.Play(_audio, pod.WordId, false);
          }
        } else {
          // Finished introducing all items -> prompt child to find target
          if (_builder.GuideNpc != null) _builder.GuideNpc.StopPointing();
          ListenPackAudio.PlayFind(_audio, TargetWord);
          _isGuiding = false;
        }
      }
    }
  }

  void FinishGuidance() {
    _isGuiding = false;
    if (_builder != null && _builder.GuideNpc != null) _builder.GuideNpc.StopPointing();
  }

  public void BeginRound() {
    if (Completed) return;
    Current = Phase.Play;
    _phaseT = 0f;
    _idleT = 0f;
    _restT = 0f;
    _slowReplayT = 0f;
    _isGuiding = false;
    string target;
    string[] choices;
    int answerIndex;
    ListeningRounds.BuildRound(Level, RoundIndex, out target, out choices, out answerIndex);
    TargetWord = target;
    _builder.ClearRound();
    for (int i = 0; i < choices.Length; i++) {
      bool answer = i == answerIndex;
      ListeningGardenChoice pod = _builder.SpawnPod(i, choices.Length, choices[i], answer);
      if (pod != null) pod.Game = this;
    }
    ActivityFeedback.ProgressKeep(RoundIndex, ListeningRounds.RoundsFor(Level));
    if (_life != null && _life.State == ActivityState.Available) {
      try { _life.Begin("listening round"); } catch (Exception) { }
    }

    // Check if this round's target word needs guidance (< 10 correct hits)
    if (NeedsGuidance(TargetWord) && Application.isPlaying) {
      _isGuiding = true;
      _guideStep = 0;
      _guideStepT = 0f;
      if (_builder.GuideNpc != null && _builder.Choices.Count > 0) {
        ListeningGardenChoice firstPod = _builder.Choices[0];
        if (firstPod != null) {
          _builder.GuideNpc.PointAt(firstPod.transform.position);
          ListenPackAudio.Play(_audio, firstPod.WordId, false);
        }
      }
    } else {
      if (!_introSaid) {
        _introSaid = true;
        Say("Listen and find the picture!", "Nghe và tìm hình nhé!");
        _promptT = 1.7f;
      } else {
        _promptT = 0.8f;
      }
    }
    PointTask();
  }

  public void TryTapChoice(ListeningGardenChoice choice) {
    if (choice == null || Completed) return;
    if (Current != Phase.Play || _restT > 0f) return;
    if (_isGuiding && Application.isPlaying) return; // Prevent tapping while NPC is introducing words
    _idleT = 0f;
    // Halt any pending player walk so player stays on the play disc instead of walking into props
    if (_player != null) {
      ClickToMove ctm = _player.GetComponent<ClickToMove>();
      if (ctm != null) ctm.Stop();
    }
    if (choice.IsAnswer) {
      choice.PlayCorrectFx();
      WinRound();
    } else {
      choice.PlayWrongFx();
      Reject();
    }
  }

  // The bell: replay the current word any time during the round.
  public void ReplayPrompt() {
    if (Completed || Current != Phase.Play) return;
    if (_isGuiding && Application.isPlaying) return; // Wait until NPC finishes introduction
    _slowReplayT = 0f;
    PlaySfx("ding");
    PlayWord(false);
  }

  void Reject() {
    WrongCount++;
    LastCorrect = false;
    Say("Not yet. Listen again!", "Chưa đúng. Nghe lại nhé!");
    ActivityFeedback.Retry();
    GameJuice.WrongFx(_builder != null ? _builder.transform : transform, _fx,
      _player != null ? _player.position : Vector3.zero);
    _slowReplayT = 1.8f;
    // 10b/c clarity: after a SECOND miss in the same round, gently show where to look —
    // the NPC points at the right pod with a soft gold halo (guide, never scold).
    if (WrongCount >= 2 && _builder != null) {
      ListeningGardenChoice answerPod = FindAnswerPod();
      if (answerPod != null) {
        if (_builder.GuideNpc != null) _builder.GuideNpc.PointAt(answerPod.transform.position);
        MagicJuice.MagicHaloRing(answerPod.transform, new Vector3(0f, 0.5f, 0f), MagicJuice.MagicGold, 1.8f);
      }
    }
  }

  ListeningGardenChoice FindAnswerPod() {
    if (_builder == null) return null;
    for (int i = 0; i < _builder.Choices.Count; i++) {
      if (_builder.Choices[i] != null && _builder.Choices[i].IsAnswer) return _builder.Choices[i];
    }
    return null;
  }

  void WinRound() {
    LastCorrect = true;
    PlaySfx("success");
    GameJuice.CorrectFx(_fx, _player != null ? _player.position : Vector3.zero, false);
    ActivityFeedback.Correct();
    RecordCorrectAnswer(TargetWord);
    if (_builder != null && _builder.GuideNpc != null) _builder.GuideNpc.Celebrate();
    Say("Yes! " + Capitalize(ListeningWords.TextEn(TargetWord)) + "!",
      "Đúng rồi! " + Capitalize(ListeningWords.DisplayVi(TargetWord)) + "!");
    RoundIndex++;
    ActivityFeedback.ProgressKeep(RoundIndex, ListeningRounds.RoundsFor(Level));
    bool finish = false;
    if (RoundIndex >= ListeningRounds.RoundsFor(Level)) {
      Level++;
      RoundIndex = 0;
      finish = Level > ListeningRounds.LastLevel;
    }
    RestThenAdvance(finish);
  }

  void RestThenAdvance(bool finish) {
    _finishAfterRest = finish;
    _restT = Application.isPlaying ? 1.5f : 0f;
    if (_restT <= 0f) AfterRest();
  }

  void AfterRest() {
    _restT = 0f;
    if (_finishAfterRest) FinishVisit();
    else BeginRound();
  }

  void FinishVisit() {
    Completed = true;
    Current = Phase.Done;
    ActivityGuide.Clear();
    Say("Well done!", "Giỏi lắm!");
    if (_life != null) {
      try { _life.MarkCompleted("listening garden done"); } catch (Exception) { }
    }
  }

  void Adopt() {
    Completed = true;
    Current = Phase.Done;
    Level = ListeningRounds.LastLevel;
  }

  public void SetLevelForTests(int level, int round) {
    Level = Mathf.Clamp(level, ListeningRounds.FirstLevel, ListeningRounds.LastLevel);
    RoundIndex = Mathf.Max(0, round);
    BeginRound();
  }

  // The item prompt: the shipped pregen audio plays offline (voice listen_v1);
  // a cache miss would fall back to the worker, same as every other line.
  void PlayWord(bool slow) {
    if (_audio == null || string.IsNullOrEmpty(TargetWord)) return;
    ListenPackAudio.Play(_audio, TargetWord, slow);
  }

  static string Capitalize(string s) {
    if (string.IsNullOrEmpty(s)) return s;
    return char.ToUpperInvariant(s[0]) + s.Substring(1);
  }

  void PointTask() {
    // Child listens to the prompt; no spoiler beacon over the correct answer.
    ActivityGuide.Clear();
  }

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
    try { _cam.Follow(_player, ListeningGardenBuilder.FollowOffset); } catch (Exception) { }
  }

  bool PlayerNear(Vector3 world, float r) {
    if (_player == null) return true;
    Vector3 d = _player.position - world;
    d.y = 0f;
    return d.sqrMagnitude <= r * r;
  }

  Vector3 SpotWorld() {
    return _builder != null
      ? _builder.transform.TransformPoint(ListeningGardenBuilder.PlaySpotLocal)
      : ListeningGardenBuilder.PlaySpotLocal;
  }
}
